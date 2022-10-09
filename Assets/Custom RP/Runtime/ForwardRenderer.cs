using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Custom.Internal;

namespace UnityEngine.Rendering.Custom
{
    public enum RenderingMode
    {
        /// <summary>Render all objects and lighting in one pass, with a hard limit on the number of lights that can be applied on an object.</summary>
        Forward,
        /// <summary>Render all objects first in a g-buffer pass, then apply all lighting in a separate pass using deferred shading.</summary>
        Deferred
    };

    public sealed class ForwardRenderer
        : ScriptableRenderer
    {
        RenderTargetHandle m_ActiveCameraColorAttachment;
        RenderTargetHandle m_ActiveCameraDepthAttachment;
        RenderTargetHandle m_CameraColorAttachment;
        RenderTargetHandle m_CameraDepthAttachment;

        

        RenderingMode m_RenderingMode;
        StencilState m_DefaultStencilState;

        // Rendering mode setup from UI.
        internal RenderingMode renderingMode { get { return RenderingMode.Forward; } }
        // Actual rendering mode, which may be different (ex: wireframe rendering, harware not capable of deferred rendering).
        internal RenderingMode actualRenderingMode { get { return GL.wireframe ? RenderingMode.Forward : this.renderingMode; } }

        DrawObjectsPass m_RenderOpaqueForwardPass;
        DrawSkyboxPass m_DrawSkyboxPass;
        TransparentSettingsPass m_TransparentSettingsPass;
        DrawObjectsPass m_RenderTransparentForwardPass;

        ForwardLights m_ForwardLights;

        public ForwardRenderer(ForwardRendererData data) : base(data)
        {
            StencilStateData stencilData = data.defaultStencilState;
            m_DefaultStencilState = StencilState.defaultValue;
            m_DefaultStencilState.enabled = stencilData.overrideStencilState;
            m_DefaultStencilState.SetCompareFunction(stencilData.stencilCompareFunction);
            m_DefaultStencilState.SetPassOperation(stencilData.passOperation);
            m_DefaultStencilState.SetFailOperation(stencilData.failOperation);
            m_DefaultStencilState.SetZFailOperation(stencilData.zFailOperation);

            m_ForwardLights = new ForwardLights();
            this.m_RenderingMode = RenderingMode.Forward;

            if (this.renderingMode == RenderingMode.Deferred)
            {
                //todo
            }

            // Always create this pass even in deferred because we use it for wireframe rendering in the Editor or offscreen depth texture rendering.
            m_RenderOpaqueForwardPass = new DrawObjectsPass(URPProfileId.DrawOpaqueObjects, true, RenderPassEvent.BeforeRenderingOpaques, RenderQueueRange.opaque, data.opaqueLayerMask, m_DefaultStencilState, stencilData.stencilReference);
            m_DrawSkyboxPass = new DrawSkyboxPass(RenderPassEvent.BeforeRenderingSkybox);

            m_TransparentSettingsPass = new TransparentSettingsPass(RenderPassEvent.BeforeRenderingTransparents, data.shadowTransparentReceive);
            m_RenderTransparentForwardPass = new DrawObjectsPass(URPProfileId.DrawTransparentObjects, false, RenderPassEvent.BeforeRenderingTransparents, RenderQueueRange.transparent, data.transparentLayerMask, m_DefaultStencilState, stencilData.stencilReference);


            // RenderTexture format depends on camera and pipeline (HDR, non HDR, etc)
            // Samples (MSAA) depend on camera and pipeline
            m_CameraColorAttachment.Init("_CameraColorTexture");
            m_CameraDepthAttachment.Init("_CameraDepthAttachment");

            supportedRenderingFeatures = new RenderingFeatures()
            {
                cameraStacking = true,
            };
        }

        /// <inheritdoc />
        public override void SetupLights(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            m_ForwardLights.Setup(context, ref renderingData);
        }

        /// <inheritdoc />
        public override void SetupCullingParameters(ref ScriptableCullingParameters cullingParameters,
            ref CameraData cameraData)
        {
            bool isShadowCastingDisabled = !CustomRenderPipeline.asset.supportsMainLightShadows && !CustomRenderPipeline.asset.supportsAdditionalLightShadows;
            bool isShadowDistanceZero = Mathf.Approximately(cameraData.maxShadowDistance, 0.0f);
            if (isShadowCastingDisabled || isShadowDistanceZero)
            {
                cullingParameters.cullingOptions &= ~CullingOptions.ShadowCasters;
            }

            if (this.actualRenderingMode == RenderingMode.Deferred)
                cullingParameters.maximumVisibleLights = 0xFFFF;
            else
            {
                // We set the number of maximum visible lights allowed and we add one for the mainlight...
                cullingParameters.maximumVisibleLights = CustomRenderPipeline.maxVisibleAdditionalLights + 1;
            }
            cullingParameters.shadowDistance = cameraData.maxShadowDistance;
        }

        /// <inheritdoc />
        public override void Setup(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;
            ref CameraData cameraData = ref renderingData.cameraData;
            RenderTextureDescriptor cameraTargetDescriptor = renderingData.cameraData.cameraTargetDescriptor;

            // Special path for depth only offscreen cameras. Only write opaques + transparents.
            bool isOffscreenDepthTexture = cameraData.targetTexture != null && cameraData.targetTexture.format == RenderTextureFormat.Depth;
            if (isOffscreenDepthTexture)
            {
                ConfigureCameraTarget(BuiltinRenderTextureType.CameraTarget, BuiltinRenderTextureType.CameraTarget);
                // TODO: Do we need to inject transparents and skybox when rendering depth only camera? They don't write to depth.
                EnqueuePass(m_DrawSkyboxPass);
                return;
            }

            bool transparentsNeedSettingsPass = m_TransparentSettingsPass.Setup(ref renderingData);

            var createColorTexture = false;
            bool createDepthTexture = false;

            // Configure all settings require to start a new camera stack (base camera only)
            if (cameraData.renderType == CameraRenderType.Base)
            {
                RenderTargetHandle cameraTargetHandle = RenderTargetHandle.GetCameraTarget();

                m_ActiveCameraColorAttachment = (createColorTexture) ? m_CameraColorAttachment : cameraTargetHandle;
                m_ActiveCameraDepthAttachment = (createDepthTexture) ? m_CameraDepthAttachment : cameraTargetHandle;
            }
            else
            {
                m_ActiveCameraColorAttachment = m_CameraColorAttachment;
                m_ActiveCameraDepthAttachment = m_CameraDepthAttachment;
            }

            // Assign camera targets (color and depth)
            var activeColorRenderTargetId = m_ActiveCameraColorAttachment.Identifier();
            var activeDepthRenderTargetId = m_ActiveCameraDepthAttachment.Identifier();
            ConfigureCameraTarget(activeColorRenderTargetId, activeDepthRenderTargetId);

            EnqueuePass(m_RenderOpaqueForwardPass);

            Skybox cameraSkybox;
            cameraData.camera.TryGetComponent<Skybox>(out cameraSkybox);
            bool isOverlayCamera = cameraData.renderType == CameraRenderType.Overlay;
            if (camera.clearFlags == CameraClearFlags.Skybox && (RenderSettings.skybox != null || cameraSkybox?.material != null) && !isOverlayCamera)
                EnqueuePass(m_DrawSkyboxPass);

            if (transparentsNeedSettingsPass)
            {
                EnqueuePass(m_TransparentSettingsPass);
            }

            EnqueuePass(m_RenderTransparentForwardPass);
        }
    }
}

