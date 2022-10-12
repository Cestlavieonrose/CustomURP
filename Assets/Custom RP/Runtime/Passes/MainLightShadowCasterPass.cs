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
            public static int _WorldToShadow; //���絽��Ӱ�ռ��ת������
            public static int _ShadowParams;//��Ӱ������light.shadowStrength, softShadowsProp, oneOverFadeDist, minusStartFade
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
        RenderTargetHandle m_MainLightShadowmap;
        RenderTexture m_MainLightShadowmapTexture;
        int m_ShadowmapWidth;
        int m_ShadowmapHeight;
        float m_MaxShadowDistance;
        int m_ShadowCasterCascadesCount;

        Matrix4x4 viewMatrix; 
        Matrix4x4 projectionMatrix;
        ShadowSliceData[] m_CascadeSlices;
        Matrix4x4[] m_MainLightShadowMatrices;

        const int k_MaxCascades = 4;
        ProfilingSampler m_ProfilingSetupSampler = new ProfilingSampler("Setup Main Shadowmap");

        public MainLightShadowCasterPass(RenderPassEvent evt)
        {
            base.profilingSampler = new ProfilingSampler(nameof(MainLightShadowCasterPass));
            renderPassEvent = evt;

            m_MainLightShadowMatrices = new Matrix4x4[k_MaxCascades + 1];
            m_CascadeSlices = new ShadowSliceData[k_MaxCascades];

            MainLightShadowConstantBuffer._WorldToShadow = Shader.PropertyToID("_MainLightWorldToShadow");
            MainLightShadowConstantBuffer._ShadowParams = Shader.PropertyToID("_MainLightShadowParams");

            m_MainLightShadowmap.Init("_MainLightShadowmapTexture");
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
            for (int i = 0; i < m_MainLightShadowMatrices.Length; ++i)
                m_MainLightShadowMatrices[i] = Matrix4x4.identity;

            for (int i = 0; i < m_CascadeSlices.Length; ++i)
                m_CascadeSlices[i].Clear();
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
                ShadowUtils.SetupShadowCasterConstantBuffer(cmd, ref shadowLight);
                ShadowUtils.RenderShadowSlice(cmd, ref context, ref m_CascadeSlices[0],  ref settings, projectionMatrix, viewMatrix);

                CoreUtils.SetKeyword(cmd, ShaderKeywordStrings.MainLightShadows, true);

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
            // for (int i = 0; i < cascadeCount; ++i)
                m_MainLightShadowMatrices[0] = m_CascadeSlices[0].shadowTransform;

            // We setup and additional a no-op WorldToShadow matrix in the last index
            // because the ComputeCascadeIndex function in Shadows.hlsl can return an index
            // out of bounds. (position not inside any cascade) and we want to avoid branching
            // Matrix4x4 noOpShadowMatrix = Matrix4x4.zero;
            // noOpShadowMatrix.m22 = (SystemInfo.usesReversedZBuffer) ? 1.0f : 0.0f;
            // for (int i = cascadeCount; i <= k_MaxCascades; ++i)
                // m_MainLightShadowMatrices[0] = noOpShadowMatrix;

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

            //������Ӱ��ͼ
            cmd.SetGlobalTexture(m_MainLightShadowmap.id, m_MainLightShadowmapTexture);
            //����Ⱦ����Ӱ�󣬵���buffer.SetGlobalMatrixArray������ת�������͵�GPU��
            cmd.SetGlobalMatrixArray(MainLightShadowConstantBuffer._WorldToShadow, m_MainLightShadowMatrices);
            cmd.SetGlobalVector(MainLightShadowConstantBuffer._ShadowParams, new Vector4(light.shadowStrength, softShadowsProp, oneOverFadeDist, minusStartFade));
        }
    };
}
