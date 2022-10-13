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
            public static int _WorldToShadow; //世界到阴影空间的转换矩阵
            public static int _ShadowParams;//阴影参数：light.shadowStrength, softShadowsProp, oneOverFadeDist, minusStartFade
            public static int _CascadeShadowSplitSpheres0;//每个包围球的数据 center：xyz， radius：w
            public static int _CascadeShadowSplitSpheres1;
            public static int _CascadeShadowSplitSpheres2;
            public static int _CascadeShadowSplitSpheres3;
            public static int _CascadeShadowSplitSphereRadii;//我们后续需要在着色器中判断物体表面的片元是否在包围球中，可以通过该片元到球心距离的平方和球体半径的平方来比较，我们传递数据之前先计算好球体半径的平方，就不用再在着色器中计算了。
            // public static int _ShadowOffset0;
            // public static int _ShadowOffset1;
            // public static int _ShadowOffset2;
            // public static int _ShadowOffset3;
            // public static int _ShadowmapSize;
        }
        //shadow贴图的位数
        const int k_ShadowmapBufferBits = 16;
        RenderTargetHandle m_MainLightShadowmap;
        RenderTexture m_MainLightShadowmapTexture;
        int m_ShadowmapWidth;
        int m_ShadowmapHeight;
        float m_MaxShadowDistance;
        int m_ShadowCasterCascadesCount;

        ShadowSliceData[] m_CascadeSlices;
        //设置每个级联包围球数据cullingSphere，为一个vector4，前三位是球心坐标，最后一位是半径
        //但因为在渲染时，摄像机的位置朝向等属性会及时改变，所以每个层级的子视截体都会不断变换，子视截体的轴对齐包围盒也要跟着变化，
        //但这样可能导致出现前后两帧轴对齐包围盒发生突变，进而导致生成的阴影贴图的有效分辨率可能在连续的两帧中发生突变，产生阴影抖动问题，
        //解决方案是把包围盒改为包围球，包围球随着子视截体的变化而发生大小的变化程度相对于包围盒来说小很多
        //光的方向和球无关，所以我们所有的方向光都使用相同的包围球。
        Vector4[] m_CascadeSplitDistances;
        Matrix4x4[] m_MainLightShadowMatrices;

        const int k_MaxCascades = 4;
        ProfilingSampler m_ProfilingSetupSampler = new ProfilingSampler("Setup Main Shadowmap");

        public MainLightShadowCasterPass(RenderPassEvent evt)
        {
            base.profilingSampler = new ProfilingSampler(nameof(MainLightShadowCasterPass));
            renderPassEvent = evt;

            m_MainLightShadowMatrices = new Matrix4x4[k_MaxCascades + 1];
            m_CascadeSlices = new ShadowSliceData[k_MaxCascades];
            m_CascadeSplitDistances = new Vector4[k_MaxCascades];

            MainLightShadowConstantBuffer._WorldToShadow = Shader.PropertyToID("_MainLightWorldToShadow");
            MainLightShadowConstantBuffer._ShadowParams = Shader.PropertyToID("_MainLightShadowParams");
            MainLightShadowConstantBuffer._CascadeShadowSplitSpheres0 = Shader.PropertyToID("_CascadeShadowSplitSpheres0");
            MainLightShadowConstantBuffer._CascadeShadowSplitSpheres1 = Shader.PropertyToID("_CascadeShadowSplitSpheres1");
            MainLightShadowConstantBuffer._CascadeShadowSplitSpheres2 = Shader.PropertyToID("_CascadeShadowSplitSpheres2");
            MainLightShadowConstantBuffer._CascadeShadowSplitSpheres3 = Shader.PropertyToID("_CascadeShadowSplitSpheres3");
            MainLightShadowConstantBuffer._CascadeShadowSplitSphereRadii = Shader.PropertyToID("_CascadeShadowSplitSphereRadii");

            m_MainLightShadowmap.Init("_MainLightShadowmapTexture");
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
            m_ShadowmapHeight = (m_ShadowCasterCascadesCount == 2) ?
                renderingData.shadowData.mainLightShadowmapHeight >> 1 :
                renderingData.shadowData.mainLightShadowmapHeight;

//阴影贴图本质也是一张深度图，它记录了从光源位置出发，到能看到的场景中距离它最近的表面位置（深度信息）。
//但是方向光并没有一个真实位置，我们要做地是找出与光的方向匹配的视图和投影矩阵，并给我们一个裁剪空间的立方体，
//该立方体与包含光源阴影的摄影机的可见区域重叠，这些数据的获取我们不用自己去实现，
//可以直接调用cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives方法，
//它需要9个参数。第1个是可见光的索引，第2、3、4个参数用于设置阴影级联数据，后面我们会处理它，第5个参数是阴影贴图的尺寸，
//第6个参数是阴影近平面偏移，我们先忽略它。最后三个参数都是输出参数，一个是视图矩阵，一个是投影矩阵，一个是ShadowSplitData对象，
//它描述有关给定阴影分割（如定向级联）的剔除信息。
            for (int cascadeIndex = 0; cascadeIndex < m_ShadowCasterCascadesCount; ++cascadeIndex)
            {
                bool success = ShadowUtils.ExtractDirectionalLightMatrix(ref renderingData.cullResults, ref renderingData.shadowData,
                    shadowLightIndex, cascadeIndex, m_ShadowmapWidth, m_ShadowmapHeight, shadowResolution, light.shadowNearPlane,
                    out m_CascadeSplitDistances[cascadeIndex], out m_CascadeSlices[cascadeIndex], out m_CascadeSlices[cascadeIndex].viewMatrix, out m_CascadeSlices[cascadeIndex].projectionMatrix);

                if (!success)
                    return false;
            }

            m_MaxShadowDistance = renderingData.cameraData.maxShadowDistance * renderingData.cameraData.maxShadowDistance;
            return true;
        }

        void Clear()
        {
            m_MainLightShadowmapTexture = null;
            for (int i = 0; i < m_MainLightShadowMatrices.Length; ++i)
                m_MainLightShadowMatrices[i] = Matrix4x4.identity;
            
            for (int i = 0; i < m_CascadeSplitDistances.Length; ++i)
                m_CascadeSplitDistances[i] = new Vector4(0.0f, 0.0f, 0.0f, 0.0f);

            for (int i = 0; i < m_CascadeSlices.Length; ++i)
                m_CascadeSlices[i].Clear();
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

                for (int cascadeIndex = 0; cascadeIndex < m_ShadowCasterCascadesCount; ++cascadeIndex)
                {
                    var splitData = settings.splitData;
                    splitData.cullingSphere = m_CascadeSplitDistances[cascadeIndex];
                    settings.splitData = splitData;
                    //调整深度偏差
                    Vector4 shadowBias = ShadowUtils.GetShadowBias(ref shadowLight, shadowLightIndex, ref shadowData, m_CascadeSlices[cascadeIndex].projectionMatrix, m_CascadeSlices[cascadeIndex].resolution);
                    ShadowUtils.SetupShadowCasterConstantBuffer(cmd, ref shadowLight, shadowBias);
                    ShadowUtils.RenderShadowSlice(cmd, ref context, ref m_CascadeSlices[cascadeIndex],
                        ref settings, m_CascadeSlices[cascadeIndex].projectionMatrix, m_CascadeSlices[cascadeIndex].viewMatrix);
                }

                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadows, true);
                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadowCascades, shadowData.mainLightShadowCascadesCount > 1);

                SetupMainLightShadowReceiverConstants(cmd, shadowLight, shadowData.supportsSoftShadows);
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

        void SetupMainLightShadowReceiverConstants(CommandBuffer cmd, VisibleLight shadowLight, bool supportsSoftShadows)
        {
            Light light = shadowLight.light;
            bool softShadows = shadowLight.light.shadows == LightShadows.Soft && supportsSoftShadows;

            int cascadeCount = m_ShadowCasterCascadesCount;
            for (int i = 0; i < cascadeCount; ++i)
                m_MainLightShadowMatrices[0] = m_CascadeSlices[i].shadowTransform;

            // We setup and additional a no-op WorldToShadow matrix in the last index
            // because the ComputeCascadeIndex function in Shadows.hlsl can return an index
            // out of bounds. (position not inside any cascade) and we want to avoid branching
            Matrix4x4 noOpShadowMatrix = Matrix4x4.zero;
            noOpShadowMatrix.m22 = (SystemInfo.usesReversedZBuffer) ? 1.0f : 0.0f;
            for (int i = cascadeCount; i <= k_MaxCascades; ++i)
                m_MainLightShadowMatrices[i] = noOpShadowMatrix;

            float invShadowAtlasWidth = 1.0f / m_ShadowmapWidth;
            float invShadowAtlasHeight = 1.0f / m_ShadowmapHeight;
            float invHalfShadowAtlasWidth = 0.5f * invShadowAtlasWidth;
            float invHalfShadowAtlasHeight = 0.5f * invShadowAtlasHeight;
            float softShadowsProp = softShadows ? 1.0f : 0.0f;

            //To make the shadow fading fit into a single MAD instruction:
            //distanceCamToPixel2 * oneOverFadeDist + minusStartFade (single MAD)
            float startFade = m_MaxShadowDistance * 0.9f;
            float oneOverFadeDist = 1/(m_MaxShadowDistance - startFade);
            float minusStartFade = -startFade * oneOverFadeDist;

            //设置阴影贴图
            cmd.SetGlobalTexture(m_MainLightShadowmap.id, m_MainLightShadowmapTexture);
            //当渲染完阴影后，调用buffer.SetGlobalMatrixArray方法将转换矩阵发送到GPU。
            cmd.SetGlobalMatrixArray(MainLightShadowConstantBuffer._WorldToShadow, m_MainLightShadowMatrices);
            cmd.SetGlobalVector(MainLightShadowConstantBuffer._ShadowParams, new Vector4(light.shadowStrength, softShadowsProp, oneOverFadeDist, minusStartFade));

            if (m_ShadowCasterCascadesCount > 1)
            {
                cmd.SetGlobalVector(MainLightShadowConstantBuffer._CascadeShadowSplitSpheres0,
                    m_CascadeSplitDistances[0]);
                cmd.SetGlobalVector(MainLightShadowConstantBuffer._CascadeShadowSplitSpheres1,
                    m_CascadeSplitDistances[1]);
                cmd.SetGlobalVector(MainLightShadowConstantBuffer._CascadeShadowSplitSpheres2,
                    m_CascadeSplitDistances[2]);
                cmd.SetGlobalVector(MainLightShadowConstantBuffer._CascadeShadowSplitSpheres3,
                    m_CascadeSplitDistances[3]);
                cmd.SetGlobalVector(MainLightShadowConstantBuffer._CascadeShadowSplitSphereRadii, new Vector4(
                    m_CascadeSplitDistances[0].w * m_CascadeSplitDistances[0].w,
                    m_CascadeSplitDistances[1].w * m_CascadeSplitDistances[1].w,
                    m_CascadeSplitDistances[2].w * m_CascadeSplitDistances[2].w,
                    m_CascadeSplitDistances[3].w * m_CascadeSplitDistances[3].w));
            }
        }
    };
}
