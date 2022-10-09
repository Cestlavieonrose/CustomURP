using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.Rendering.Custom
{
    public enum RenderPassEvent
    {
        /// <summary>
        /// Executes a <c>ScriptableRenderPass</c> before rendering any other passes in the pipeline.
        /// Camera matrices and stereo rendering are not setup this point.
        /// You can use this to draw to custom input textures used later in the pipeline, f.ex LUT textures.
        /// </summary>
        BeforeRendering = 0,

        /// <summary>
        /// Executes a <c>ScriptableRenderPass</c> before rendering shadowmaps.
        /// Camera matrices and stereo rendering are not setup this point.
        /// </summary>
        BeforeRenderingShadows = 50,

        /// <summary>
        /// Executes a <c>ScriptableRenderPass</c> after rendering shadowmaps.
        /// Camera matrices and stereo rendering are not setup this point.
        /// </summary>
        AfterRenderingShadows = 100,

        /// <summary>
        /// Executes a <c>ScriptableRenderPass</c> before rendering prepasses, f.ex, depth prepass.
        /// Camera matrices and stereo rendering are already setup at this point.
        /// </summary>
        BeforeRenderingPrepasses = 150,

        /// <summary>
        /// Executes a <c>ScriptableRenderPass</c> after rendering prepasses, f.ex, depth prepass.
        /// Camera matrices and stereo rendering are already setup at this point.
        /// </summary>
        AfterRenderingPrePasses = 200,

        /// <summary>
        /// Executes a <c>ScriptableRenderPass</c> before rendering opaque objects.
        /// </summary>
        BeforeRenderingOpaques = 250,

        /// <summary>
        /// Executes a <c>ScriptableRenderPass</c> after rendering opaque objects.
        /// </summary>
        AfterRenderingOpaques = 300,

        /// <summary>
        /// Executes a <c>ScriptableRenderPass</c> before rendering the sky.
        /// </summary>
        BeforeRenderingSkybox = 350,

        /// <summary>
        /// Executes a <c>ScriptableRenderPass</c> after rendering the sky.
        /// </summary>
        AfterRenderingSkybox = 400,

        /// <summary>
        /// Executes a <c>ScriptableRenderPass</c> before rendering transparent objects.
        /// </summary>
        BeforeRenderingTransparents = 450,

        /// <summary>
        /// Executes a <c>ScriptableRenderPass</c> after rendering transparent objects.
        /// </summary>
        AfterRenderingTransparents = 500,

        /// <summary>
        /// Executes a <c>ScriptableRenderPass</c> before rendering post-processing effects.
        /// </summary>
        BeforeRenderingPostProcessing = 550,

        /// <summary>
        /// Executes a <c>ScriptableRenderPass</c> after rendering post-processing effects but before final blit, post-processing AA effects and color grading.
        /// </summary>
        AfterRenderingPostProcessing = 600,

        /// <summary>
        /// Executes a <c>ScriptableRenderPass</c> after rendering all effects.
        /// </summary>
        AfterRendering = 1000,
    }

    public abstract class ScriptableRenderPass
    {
        public RenderPassEvent renderPassEvent { get; set; }

        public RenderTargetIdentifier[] colorAttachments
        {
            get => m_ColorAttachments;
        }

        public RenderTargetIdentifier colorAttachment
        {
            get => m_ColorAttachments[0];
        }

        public RenderTargetIdentifier depthAttachment
        {
            get => m_DepthAttachment;
        }

        public ClearFlag clearFlag
        {
            get => m_ClearFlag;
        }

        public Color clearColor
        {
            get => m_ClearColor;
        }

        RenderTargetIdentifier[] m_ColorAttachments = new RenderTargetIdentifier[] { BuiltinRenderTextureType.CameraTarget };
        RenderTargetIdentifier m_DepthAttachment = BuiltinRenderTextureType.CameraTarget;

        protected internal ProfilingSampler profilingSampler { get; set; }
        public abstract void Execute(ScriptableRenderContext context, ref RenderingData renderingData);

        internal bool overrideCameraTarget { get; set; }

        ClearFlag m_ClearFlag = ClearFlag.None;
        Color m_ClearColor = Color.black;

        public ScriptableRenderPass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingOpaques;   
            profilingSampler = new ProfilingSampler(nameof(ScriptableRenderPass));
        }

        /// <summary>
        /// Called upon finish rendering a camera. You can use this callback to release any resources created
        /// by this render
        /// pass that need to be cleanup once camera has finished rendering.
        /// This method be called for all cameras in a camera stack.
        /// </summary>
        /// <param name="cmd">Use this CommandBuffer to cleanup any generated data</param>
        public virtual void OnCameraCleanup(CommandBuffer cmd)
        {

        }

        /// <summary>
        /// Creates <c>DrawingSettings</c> based on current the rendering state.
        /// </summary>
        /// <param name="shaderTagId">Shader pass tag to render.</param>
        /// <param name="renderingData">Current rendering state.</param>
        /// <param name="sortingCriteria">Criteria to sort objects being rendered.</param>
        /// <returns></returns>
        /// <seealso cref="DrawingSettings"/>
        public DrawingSettings CreateDrawingSettings(ShaderTagId shaderTagId, ref RenderingData renderingData, SortingCriteria sortingCriteria)
        {
            Camera camera = renderingData.cameraData.camera;
            //????????????????????????????????????????????????????????????????????SortingCriteria.CommonOpaque?????
            SortingSettings sortingSettings = new SortingSettings(camera) { criteria = sortingCriteria };
            DrawingSettings settings = new DrawingSettings(shaderTagId, sortingSettings)
            {
                perObjectData = renderingData.perObjectData,
                mainLightIndex = renderingData.lightData.mainLightIndex,
                //设置渲染时批处理的使用状�?
                enableDynamicBatching = renderingData.supportsDynamicBatching,

                // Disable instancing for preview cameras. This is consistent with the built-in forward renderer. Also fixes case 1127324.
                enableInstancing = camera.cameraType == CameraType.Preview ? false : true,
            };
            return settings;
        }

        /// <summary>
        /// Creates <c>DrawingSettings</c> based on current rendering state.
        /// </summary>
        /// /// <param name="shaderTagIdList">List of shader pass tag to render.</param>
        /// <param name="renderingData">Current rendering state.</param>
        /// <param name="sortingCriteria">Criteria to sort objects being rendered.</param>
        /// <returns></returns>
        /// <seealso cref="DrawingSettings"/>
        public DrawingSettings CreateDrawingSettings(List<ShaderTagId> shaderTagIdList,
            ref RenderingData renderingData, SortingCriteria sortingCriteria)
        {
            if (shaderTagIdList == null || shaderTagIdList.Count == 0)
            {
                Debug.LogWarning("ShaderTagId list is invalid. DrawingSettings is created with default pipeline ShaderTagId");
                return CreateDrawingSettings(new ShaderTagId("UniversalPipeline"), ref renderingData, sortingCriteria);
            }

            DrawingSettings settings = CreateDrawingSettings(shaderTagIdList[0], ref renderingData, sortingCriteria);
            for (int i = 1; i < shaderTagIdList.Count; ++i)
                settings.SetShaderPassName(i, shaderTagIdList[i]);
            return settings;
        }

        /// <summary>
        /// This method is called by the renderer before executing the render pass.
        /// Override this method if you need to to configure render targets and their clear state, and to create temporary render target textures.
        /// If a render pass doesn't override this method, this render pass renders to the active Camera's render target.
        /// You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
        /// </summary>
        /// <param name="cmd">CommandBuffer to enqueue rendering commands. This will be executed by the pipeline.</param>
        /// <param name="cameraTextureDescriptor">Render texture descriptor of the camera render target.</param>
        /// <seealso cref="ConfigureTarget"/>
        /// <seealso cref="ConfigureClear"/>
        public virtual void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        { }

        /// <summary>
        /// Called upon finish rendering a camera stack. You can use this callback to release any resources created
        /// by this render pass that need to be cleanup once all cameras in the stack have finished rendering.
        /// This method will be called once after rendering the last camera in the camera stack.
        /// Cameras that don't have an explicit camera stack are also considered stacked rendering.
        /// In that case the Base camera is the first and last camera in the stack.
        /// </summary>
        /// <param name="cmd">Use this CommandBuffer to cleanup any generated data</param>
        public virtual void OnFinishCameraStackRendering(CommandBuffer cmd)
        { }

        /// <summary>
        /// This method is called by the renderer before rendering a camera
        /// Override this method if you need to to configure render targets and their clear state, and to create temporary render target textures.
        /// If a render pass doesn't override this method, this render pass renders to the active Camera's render target.
        /// You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
        /// </summary>
        /// <param name="cmd">CommandBuffer to enqueue rendering commands. This will be executed by the pipeline.</param>
        /// <param name="renderingData">Current rendering state information</param>
        /// <seealso cref="ConfigureTarget"/>
        /// <seealso cref="ConfigureClear"/>
        public virtual void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        { }


        public static bool operator <(ScriptableRenderPass lhs, ScriptableRenderPass rhs)
        {
            return lhs.renderPassEvent < rhs.renderPassEvent;
        }

        public static bool operator >(ScriptableRenderPass lhs, ScriptableRenderPass rhs)
        {
            return lhs.renderPassEvent > rhs.renderPassEvent;
        }
    }

}
