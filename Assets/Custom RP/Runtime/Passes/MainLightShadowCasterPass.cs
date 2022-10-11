using System;

namespace UnityEngine.Rendering.Custom.Internal
{
    /// <summary>
    /// Renders a shadow map for the main Light.
    /// </summary>
    public class MainLightShadowCasterPass : ScriptableRenderPass
    {
        private static class MainLightShadowConstantBuffer
        {
            // public static int _WorldToShadow;
            // public static int _ShadowParams;
            // public static int _CascadeShadowSplitSpheres0;
            // public static int _CascadeShadowSplitSpheres1;
            // public static int _CascadeShadowSplitSpheres2;
            // public static int _CascadeShadowSplitSpheres3;
            // public static int _CascadeShadowSplitSphereRadii;
            // public static int _ShadowOffset0;
            // public static int _ShadowOffset1;
            // public static int _ShadowOffset2;
            // public static int _ShadowOffset3;
            // public static int _ShadowmapSize;
        }
        //shadow贴图的位数
        const int k_ShadowmapBufferBits = 16;
        RenderTexture m_MainLightShadowmapTexture;
        int m_ShadowmapWidth;
        int m_ShadowmapHeight;
        float m_MaxShadowDistance;
        int m_ShadowCasterCascadesCount;

        Matrix4x4 viewMatrix; 
        Matrix4x4 projectionMatrix;
        ShadowSliceData[] m_CascadeSlices;

        const int k_MaxCascades = 4;
        ProfilingSampler m_ProfilingSetupSampler = new ProfilingSampler("Setup Main Shadowmap");

        public MainLightShadowCasterPass(RenderPassEvent evt)
        {
            base.profilingSampler = new ProfilingSampler(nameof(MainLightShadowCasterPass));
            renderPassEvent = evt;

            m_CascadeSlices = new ShadowSliceData[k_MaxCascades];
        }

        public bool Setup(ref RenderingData renderingData)
        {
            using var profScope = new ProfilingScope(null, m_ProfilingSetupSampler);

            //是否支持需要满足三个条件：
            //1.系统是否支持  2.asset是否勾选支持 3 light组件是否支持
            if (!renderingData.shadowData.supportsMainLightShadows)
                return false;

            Clear();
            int shadowLightIndex = renderingData.lightData.mainLightIndex;
            if (shadowLightIndex == -1)
                return false;

            VisibleLight shadowLight = renderingData.lightData.visibleLights[shadowLightIndex];
            Light light = shadowLight.light;
            if (light.shadows == LightShadows.None)
                return false;

            if (shadowLight.lightType != LightType.Directional)
            {
                Debug.LogWarning("Only directional lights are supported as main light.");
            }
            //判断当前灯光没有照射任何物体
            Bounds bounds;
            if (!renderingData.cullResults.GetShadowCasterBounds(shadowLightIndex, out bounds))
                return false;
            
            m_ShadowCasterCascadesCount = renderingData.shadowData.mainLightShadowCascadesCount;
            int shadowResolution = ShadowUtils.GetMaxTileResolutionInAtlas(renderingData.shadowData.mainLightShadowmapWidth,
                renderingData.shadowData.mainLightShadowmapHeight, m_ShadowCasterCascadesCount);
            m_ShadowmapWidth = renderingData.shadowData.mainLightShadowmapWidth;
            m_ShadowmapHeight = renderingData.shadowData.mainLightShadowmapHeight;
//阴影贴图本质也是一张深度图，它记录了从光源位置出发，到能看到的场景中距离它最近的表面位置（深度信息）。
//但是方向光并没有一个真实位置，我们要做地是找出与光的方向匹配的视图和投影矩阵，并给我们一个裁剪空间的立方体，
//该立方体与包含光源阴影的摄影机的可见区域重叠，这些数据的获取我们不用自己去实现，
//可以直接调用cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives方法，
//它需要9个参数。第1个是可见光的索引，第2、3、4个参数用于设置阴影级联数据，后面我们会处理它，第5个参数是阴影贴图的尺寸，
//第6个参数是阴影近平面偏移，我们先忽略它。最后三个参数都是输出参数，一个是视图矩阵，一个是投影矩阵，一个是ShadowSplitData对象，
//它描述有关给定阴影分割（如定向级联）的剔除信息。
           bool success = ShadowUtils.ExtractDirectionalLightMatrix(ref renderingData.cullResults, ref renderingData.shadowData,
                    shadowLightIndex, 0, m_ShadowmapWidth, m_ShadowmapHeight, shadowResolution, light.shadowNearPlane,
                    out m_CascadeSlices[0], out viewMatrix, out projectionMatrix);

            if (!success)
                return false;


            m_MaxShadowDistance = renderingData.cameraData.maxShadowDistance * renderingData.cameraData.maxShadowDistance;
            return true;
        }

        void Clear()
        {
            m_MainLightShadowmapTexture = null;
        }


        //创建RT，并指定该类型是阴影贴图
        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            m_MainLightShadowmapTexture = ShadowUtils.GetTemporaryShadowTexture(m_ShadowmapWidth,
                    m_ShadowmapHeight, k_ShadowmapBufferBits);
            ConfigureTarget(new RenderTargetIdentifier(m_MainLightShadowmapTexture));
            ConfigureClear(ClearFlag.All, Color.black);
        }

        void RenderMainLightCascadeShadowmap(ref ScriptableRenderContext context, ref CullingResults cullResults, ref LightData lightData, ref ShadowData shadowData)
        {
            int shadowLightIndex = lightData.mainLightIndex;
            if (shadowLightIndex == -1)
                return;

            VisibleLight shadowLight = lightData.visibleLights[shadowLightIndex];

            // NOTE: Do NOT mix ProfilingScope with named CommandBuffers i.e. CommandBufferPool.Get("name").
            // Currently there's an issue which results in mismatched markers.
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, ProfilingSampler.Get(URPProfileId.MainLightShadow)))
            {
                var settings = new ShadowDrawingSettings(cullResults, shadowLightIndex);
                ShadowUtils.RenderShadowSlice(cmd, ref context, ref m_CascadeSlices[0],  ref settings, projectionMatrix, viewMatrix);

                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadows, true);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            RenderMainLightCascadeShadowmap(ref context, ref renderingData.cullResults, ref renderingData.lightData, ref renderingData.shadowData);
        }

        /// <inheritdoc/>
        public override void OnCameraCleanup(CommandBuffer cmd)
        {
            if (cmd == null)
                throw new ArgumentNullException("cmd");

            if (m_MainLightShadowmapTexture)
            {
                RenderTexture.ReleaseTemporary(m_MainLightShadowmapTexture);
                m_MainLightShadowmapTexture = null;
            }
        }

    };
}
