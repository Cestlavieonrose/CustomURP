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
        //shadow��ͼ��λ��
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

            //�Ƿ�֧����Ҫ��������������
            //1.ϵͳ�Ƿ�֧��  2.asset�Ƿ�ѡ֧�� 3 light����Ƿ�֧��
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
            //�жϵ�ǰ�ƹ�û�������κ�����
            Bounds bounds;
            if (!renderingData.cullResults.GetShadowCasterBounds(shadowLightIndex, out bounds))
                return false;
            
            m_ShadowCasterCascadesCount = renderingData.shadowData.mainLightShadowCascadesCount;
            int shadowResolution = ShadowUtils.GetMaxTileResolutionInAtlas(renderingData.shadowData.mainLightShadowmapWidth,
                renderingData.shadowData.mainLightShadowmapHeight, m_ShadowCasterCascadesCount);
            m_ShadowmapWidth = renderingData.shadowData.mainLightShadowmapWidth;
            m_ShadowmapHeight = renderingData.shadowData.mainLightShadowmapHeight;
//��Ӱ��ͼ����Ҳ��һ�����ͼ������¼�˴ӹ�Դλ�ó��������ܿ����ĳ����о���������ı���λ�ã������Ϣ����
//���Ƿ���Ⲣû��һ����ʵλ�ã�����Ҫ�������ҳ����ķ���ƥ�����ͼ��ͶӰ���󣬲�������һ���ü��ռ�������壬
//���������������Դ��Ӱ����Ӱ���Ŀɼ������ص�����Щ���ݵĻ�ȡ���ǲ����Լ�ȥʵ�֣�
//����ֱ�ӵ���cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives������
//����Ҫ9����������1���ǿɼ������������2��3��4����������������Ӱ�������ݣ��������ǻᴦ��������5����������Ӱ��ͼ�ĳߴ磬
//��6����������Ӱ��ƽ��ƫ�ƣ������Ⱥ�������������������������������һ������ͼ����һ����ͶӰ����һ����ShadowSplitData����
//�������йظ�����Ӱ�ָ�綨���������޳���Ϣ��
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


        //����RT����ָ������������Ӱ��ͼ
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
