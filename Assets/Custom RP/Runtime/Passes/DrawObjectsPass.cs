

using System.Collections.Generic;

namespace UnityEngine.Rendering.Custom.Internal
{
    public class DrawObjectsPass : ScriptableRenderPass
    {
        FilteringSettings m_FilteringSettings;
        RenderStateBlock m_RenderStateBlock;
        List<ShaderTagId> m_ShaderTagIdList = new List<ShaderTagId>();
        string m_ProfilerTag;
        ProfilingSampler m_ProfilingSampler;

        static readonly int s_DrawObjectPassDataPropID = Shader.PropertyToID("_DrawObjectPassData");

        bool m_IsOpaque;
        public DrawObjectsPass(string profilerTag, ShaderTagId[] shaderTagIds, bool opaque, RenderPassEvent evt, RenderQueueRange renderQueueRange, LayerMask layerMask, StencilState stencilState, int stencilReference)
        {
            base.profilingSampler = new ProfilingSampler(nameof(DrawObjectsPass));
            m_ProfilingSampler = new ProfilingSampler(profilerTag);
            foreach (ShaderTagId sid in shaderTagIds)
                m_ShaderTagIdList.Add(sid);
            renderPassEvent = evt;
            //������ָ����Щ render ����������ġ���RenderQueueRange.all���ݸ�FilteringSettings���캯�����������ܰ����������ݡ�
            m_FilteringSettings = new FilteringSettings(renderQueueRange, layerMask);
            m_RenderStateBlock = new RenderStateBlock(RenderStateMask.Nothing);
            m_IsOpaque = opaque;
        }

        public DrawObjectsPass(string profilerTag, bool opaque, RenderPassEvent evt, RenderQueueRange renderQueueRange, LayerMask layerMask, StencilState stencilState, int stencilReference)
    : this(profilerTag,
        new ShaderTagId[] { new ShaderTagId("SRPDefaultUnlit"), new ShaderTagId("CustomLit"), new ShaderTagId("UniversalForwardOnly"), new ShaderTagId("LightweightForward") },
        opaque, evt, renderQueueRange, layerMask, stencilState, stencilReference)
        { }

        internal DrawObjectsPass(URPProfileId profileId, bool opaque, RenderPassEvent evt, RenderQueueRange renderQueueRange, LayerMask layerMask, StencilState stencilState, int stencilReference)
            : this(profileId.GetType().Name, opaque, evt, renderQueueRange, layerMask, stencilState, stencilReference)
        {
            m_ProfilingSampler = ProfilingSampler.Get(profileId);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            // NOTE: Do NOT mix ProfilingScope with named CommandBuffers i.e. CommandBufferPool.Get("name").
            // Currently there's an issue which results in mismatched markers.
            CommandBuffer cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, m_ProfilingSampler))
            {
                // Global render pass data containing various settings.
                // x,y,z are currently unused
                // w is used for knowing whether the object is opaque(1) or alpha blended(0)
                Vector4 drawObjectPassData = new Vector4(0.0f, 0.0f, 0.0f, (m_IsOpaque) ? 1.0f : 0.0f);
                cmd.SetGlobalVector(s_DrawObjectPassDataPropID, drawObjectPassData);

                // scaleBias.x = flipSign
                // scaleBias.y = scale
                // scaleBias.z = bias
                // scaleBias.w = unused
                float flipSign = (renderingData.cameraData.IsCameraProjectionMatrixFlipped()) ? -1.0f : 1.0f;
                Vector4 scaleBias = (flipSign < 0.0f)
                    ? new Vector4(flipSign, 1.0f, -1.0f, 1.0f)
                    : new Vector4(flipSign, 0.0f, 1.0f, 1.0f);
                cmd.SetGlobalVector(ShaderPropertyId.scaleBiasRt, scaleBias);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

                Camera camera = renderingData.cameraData.camera;
                var sortFlags = (m_IsOpaque) ? renderingData.cameraData.defaultOpaqueSortFlags : SortingCriteria.CommonTransparent;
                var drawSettings = CreateDrawingSettings(m_ShaderTagIdList, ref renderingData, sortFlags);
                var filterSettings = m_FilteringSettings;

#if UNITY_EDITOR
                // When rendering the preview camera, we want the layer mask to be forced to Everything
                if (renderingData.cameraData.isPreviewCamera)
                {
                    filterSettings.layerMask = -1;
                }
#endif

                context.DrawRenderers(renderingData.cullResults, ref drawSettings, ref filterSettings, ref m_RenderStateBlock);
                //Ϊ�������ָ����Щ����ʹ���˲�֧�ֵ���ɫ�������ǽ�ʹ��UnityError��ɫ���������ǡ��������ɫ����Ϊ��������һ���²��ʣ����ǿ���ͨ������Shader.Find�ҵ�һ������Hidden/InternalErrorShader�ַ�����Ϊ�����Ĳ��ʡ�ͨ����̬�ֶλ�����ʣ��������ǾͲ���ÿ֡����һ���µ��ˡ�Ȼ����������ͼ���õ�overrideMaterial����
                // Render objects that did not match any shader pass with error shader
                RenderingUtils.RenderObjectsWithError(context, ref renderingData.cullResults, camera, filterSettings, SortingCriteria.None);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

    }

}


