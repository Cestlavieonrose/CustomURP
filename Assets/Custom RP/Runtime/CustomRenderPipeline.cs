using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.Rendering;

namespace UnityEngine.Rendering.Custom
{
    public partial class CustomRenderPipeline : RenderPipeline {

        private static class Profiling
        {
            private static Dictionary<int, ProfilingSampler> s_HashSamplerCache = new Dictionary<int, ProfilingSampler>();
            public static readonly ProfilingSampler unknownSampler = new ProfilingSampler("Unknown");
            // Specialization for camera loop to avoid allocations.
            public static ProfilingSampler TryGetOrAddCameraSampler(Camera camera)
            {
#if UNIVERSAL_PROFILING_NO_ALLOC
                    return unknownSampler;
#else
                ProfilingSampler ps = null;
                int cameraId = camera.GetHashCode();
                bool exists = s_HashSamplerCache.TryGetValue(cameraId, out ps);
                if (!exists)
                {
                    // NOTE: camera.name allocates!
                    ps = new ProfilingSampler($"{nameof(CustomRenderPipeline)}.{nameof(RenderSingleCamera)}: {camera.name}");
                    s_HashSamplerCache.Add(cameraId, ps);
                }
                return ps;
#endif
            }

            public static class Pipeline
            {
                public static readonly ProfilingSampler beginFrameRendering = new ProfilingSampler($"{nameof(RenderPipeline)}.{nameof(BeginFrameRendering)}");
                public static readonly ProfilingSampler endFrameRendering = new ProfilingSampler($"{nameof(RenderPipeline)}.{nameof(EndFrameRendering)}");

                public static readonly ProfilingSampler beginCameraRendering = new ProfilingSampler($"{nameof(RenderPipeline)}.{nameof(BeginCameraRendering)}");
                public static readonly ProfilingSampler endCameraRendering = new ProfilingSampler($"{nameof(RenderPipeline)}.{nameof(EndCameraRendering)}");

                const string k_Name = nameof(CustomRenderPipeline);
                public static readonly ProfilingSampler initializeCameraData = new ProfilingSampler($"{k_Name}.{nameof(InitializeCameraData)}");
                public static readonly ProfilingSampler initializeStackedCameraData = new ProfilingSampler($"{k_Name}.{nameof(InitializeStackedCameraData)}");
                public static readonly ProfilingSampler initializeAdditionalCameraData = new ProfilingSampler($"{k_Name}.{nameof(InitializeAdditionalCameraData)}");
                public static readonly ProfilingSampler initializeRenderingData = new ProfilingSampler($"{k_Name}.{nameof(InitializeRenderingData)}");
                public static readonly ProfilingSampler initializeLightData = new ProfilingSampler($"{k_Name}.{nameof(InitializeLightData)}");
                public static readonly ProfilingSampler initializeShadowData = new ProfilingSampler($"{k_Name}.{nameof(InitializeShadowData)}");
                public static readonly ProfilingSampler getPerObjectLightFlags = new ProfilingSampler($"{k_Name}.{nameof(GetPerObjectLightFlags)}");
                public static readonly ProfilingSampler getMainLightIndex = new ProfilingSampler($"{k_Name}.{nameof(GetMainLightIndex)}");
                public static readonly ProfilingSampler setupPerFrameShaderConstants = new ProfilingSampler($"{k_Name}.{nameof(SetupPerFrameShaderConstants)}");

                public static class Renderer
                {
                    const string k_Name = nameof(ScriptableRenderer);
                    public static readonly ProfilingSampler setupCullingParameters = new ProfilingSampler($"{k_Name}.{nameof(ScriptableRenderer.SetupCullingParameters)}");
                    public static readonly ProfilingSampler setup = new ProfilingSampler($"{k_Name}.{nameof(ScriptableRenderer.Setup)}");
                };

                public static class Context
                {
                    const string k_Name = nameof(Context);
                    public static readonly ProfilingSampler submit = new ProfilingSampler($"{k_Name}.{nameof(ScriptableRenderContext.Submit)}");
                };
            }
           

        }

        // Amount of Lights that can be shaded per object (in the for loop in the shader)
        public static int maxPerObjectLights
        {
            // No support to bitfield mask and int[] in gles2. Can't index fast more than 4 lights.
            // Check Lighting.hlsl for more details.
            get => (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES2) ? 4 : 8;
        }

        // These limits have to match same limits in Input.hlsl
        const int k_MaxVisibleAdditionalLightsMobileShaderLevelLessThan45 = 16;
        const int k_MaxVisibleAdditionalLightsMobile = 32;
        const int k_MaxVisibleAdditionalLightsNonMobile = 256;
        public static int maxVisibleAdditionalLights
        {
            get
            {
                bool isMobile = Application.isMobilePlatform;
                if (isMobile && (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES2 || (SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3 && Graphics.minOpenGLESVersion <= OpenGLESVersion.OpenGLES30)))
                    return k_MaxVisibleAdditionalLightsMobileShaderLevelLessThan45;

                // GLES can be selected as platform on Windows (not a mobile platform) but uniform buffer size so we must use a low light count.
                return (isMobile || SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore || SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES2 || SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3)
                        ? k_MaxVisibleAdditionalLightsMobile : k_MaxVisibleAdditionalLightsNonMobile;
            }
        }

        public static float maxShadowBias
        {
            get => 10.0f;
        }

        public CustomRenderPipeline(CustomRenderPineAsset asset)
        {
            // In QualitySettings.antiAliasing disabled state uses value 0, where in URP 1
            int qualitySettingsMsaaSampleCount = QualitySettings.antiAliasing > 0 ? QualitySettings.antiAliasing : 1;
            bool msaaSampleCountNeedsUpdate = qualitySettingsMsaaSampleCount != asset.msaaSampleCount;
            // Let engine know we have MSAA on for cases where we support MSAA backbuffer
            if (msaaSampleCountNeedsUpdate)
            {
                QualitySettings.antiAliasing = asset.msaaSampleCount;
            }

            //ֻ�д���UniversalPipeline��LightweightPipeline��Tag��SubShader�Ż���Ч.��Ҫ���������ڱ�ǵ�ǰ���SubShader�������ĸ������µ�.
            Shader.globalRenderPipeline = "UniversalPipeline,LightweightPipeline,CustomPipeline";

            Lightmapping.SetDelegate(lightsDelegate);

            CameraCaptureBridge.enabled = true;

            RenderingUtils.ClearSystemInfoCache();
        }

        //ÿһ֡Unity�������RPʵ����Render����
        //renderContext:�ýṹ���ṩ����ǰ��������ӣ����ǿ���ʹ������������Ⱦ
        //cameras:��������飬��Ϊ�����ж�������ڵ�ǰ�����������ṩ�������˳�������Ⱦ��RP�����Ρ�
        protected override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
        {
            using var profScope = new ProfilingScope(null, ProfilingSampler.Get(URPProfileId.CustomRenderTotal));

            using (new ProfilingScope(null, Profiling.Pipeline.beginFrameRendering))
            {
                BeginFrameRendering(renderContext, cameras);
            }

            GraphicsSettings.lightsUseLinearIntensity = (QualitySettings.activeColorSpace == ColorSpace.Linear);
            GraphicsSettings.useScriptableRenderPipelineBatching = asset.useSRPBatcher;
            SetupPerFrameShaderConstants();

            SortCameras(cameras);

            for (int i = 0; i < cameras.Length; ++i)
            {
                var camera = cameras[i];
                if (IsGameCamera(camera))
                {
                    RenderCameraStack(renderContext, camera);
                }
                else
                {
                    //sceneҳ���µ���Ⱦ�߼�
                    using (new ProfilingScope(null, Profiling.Pipeline.beginCameraRendering))
                    {
                        BeginCameraRendering(renderContext, camera);
                    }
#if VISUAL_EFFECT_GRAPH_0_0_1_OR_NEWER
                //It should be called before culling to prepare material. When there isn't any VisualEffect component, this method has no effect.
                VFX.VFXManager.PrepareCamera(camera);
#endif
                    RenderSingleCamera(renderContext, camera);

                    using (new ProfilingScope(null, Profiling.Pipeline.endCameraRendering))
                    {
                        EndCameraRendering(renderContext, camera);
                    }
                }
            }


            using (new ProfilingScope(null, Profiling.Pipeline.endFrameRendering))
            {
                EndFrameRendering(renderContext, cameras);
            }
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            Shader.globalRenderPipeline = "";
            SupportedRenderingFeatures.active = new SupportedRenderingFeatures();
            //ShaderData.instance.Dispose();
            //DeferredShaderData.instance.Dispose();

//#if UNITY_EDITOR
//            SceneViewDrawMode.ResetDrawMode();
//#endif
            Lightmapping.ResetDelegate();
            CameraCaptureBridge.enabled = false;
        }

        /// <summary>
        // Renders a camera stack. This method calls RenderSingleCamera for each valid camera in the stack.
        // The last camera resolves the final target to screen.
        /// </summary>
        /// <param name="context">Render context used to record commands during execution.</param>
        /// <param name="camera">Camera to render.</param>
        static void RenderCameraStack(ScriptableRenderContext context, Camera baseCamera)
        {
            using var profScope = new ProfilingScope(null, ProfilingSampler.Get(URPProfileId.RenderCameraStack));

            baseCamera.TryGetComponent<CustomAdditionalCameraData>(out var baseCameraAdditionalData);

            // Overlay cameras will be rendered stacked while rendering base cameras
            if (baseCameraAdditionalData != null && baseCameraAdditionalData.renderType == CameraRenderType.Overlay)
                return;

            // renderer contains a stack if it has additional data and the renderer supports stacking
            var renderer = baseCameraAdditionalData?.scriptableRenderer;
            bool supportsCameraStacking = renderer != null && renderer.supportedRenderingFeatures.cameraStacking;
            List<Camera> cameraStack = (supportsCameraStacking) ? baseCameraAdditionalData?.cameraStack : null;

            bool anyPostProcessingEnabled = baseCameraAdditionalData != null && baseCameraAdditionalData.renderPostProcessing;

            // We need to know the last active camera in the stack to be able to resolve
            // rendering to screen when rendering it. The last camera in the stack is not
            // necessarily the last active one as it users might disable it.
            int lastActiveOverlayCameraIndex = -1;
            if (cameraStack != null)
            {
                var baseCameraRendererType = baseCameraAdditionalData?.scriptableRenderer.GetType();
                bool shouldUpdateCameraStack = false;

                for (int i = 0; i < cameraStack.Count; ++i)
                {
                    Camera currCamera = cameraStack[i];
                    if (currCamera == null)
                    {
                        shouldUpdateCameraStack = true;
                        continue;
                    }

                    if (currCamera.isActiveAndEnabled)
                    {
                        currCamera.TryGetComponent<CustomAdditionalCameraData>(out var data);

                        if (data == null || data.renderType != CameraRenderType.Overlay)
                        {
                            Debug.LogWarning(string.Format("Stack can only contain Overlay cameras. {0} will skip rendering.", currCamera.name));
                            continue;
                        }

                        var currCameraRendererType = data?.scriptableRenderer.GetType();
                        if (currCameraRendererType != baseCameraRendererType)
                        {
                            //todo UI��Ⱦ��ʱ��֧��
                            //var renderer2DType = typeof(Experimental.Rendering.Universal.Renderer2D);
                            //if (currCameraRendererType != renderer2DType && baseCameraRendererType != renderer2DType)
                            //{
                            Debug.LogWarning(string.Format("Only cameras with compatible renderer types can be stacked. {0} will skip rendering", currCamera.name));
                            continue;
                            //}
                        }

                        anyPostProcessingEnabled |= data.renderPostProcessing;
                        lastActiveOverlayCameraIndex = i;
                    }
                }
                if (shouldUpdateCameraStack)
                {
                    baseCameraAdditionalData.UpdateCameraStack();
                }
            }

            bool isStackedRendering = lastActiveOverlayCameraIndex != -1;

            InitializeCameraData(baseCamera, baseCameraAdditionalData, !isStackedRendering, out var baseCameraData);

            using (new ProfilingScope(null, Profiling.Pipeline.beginCameraRendering))
            {
                BeginCameraRendering(context, baseCamera);
            }

            RenderSingleCamera(context, baseCameraData, anyPostProcessingEnabled);

            using (new ProfilingScope(null, Profiling.Pipeline.endCameraRendering))
            {
                EndCameraRendering(context, baseCamera);
            }
        }

        static void InitializeCameraData(Camera camera, CustomAdditionalCameraData additionalCameraData, bool resolveFinalTarget, out CameraData cameraData)
        {
            using var profScope = new ProfilingScope(null, Profiling.Pipeline.initializeCameraData);

            cameraData = new CameraData();

            //todo ��ʱ������һ�� �����Ǹ���stack�ĵ�
            cameraData.defaultOpaqueSortFlags = SortingCriteria.CommonOpaque;

            InitializeStackedCameraData(camera, additionalCameraData, ref cameraData);
            InitializeAdditionalCameraData(camera, additionalCameraData, resolveFinalTarget, ref cameraData);

        }

        /// <summary>
        /// Initialize camera data settings common for all cameras in the stack. Overlay cameras will inherit
        /// settings from base camera.
        /// </summary>
        /// <param name="baseCamera">Base camera to inherit settings from.</param>
        /// <param name="baseAdditionalCameraData">Component that contains additional base camera data.</param>
        /// <param name="cameraData">Camera data to initialize setttings.</param>
        static void InitializeStackedCameraData(Camera baseCamera, CustomAdditionalCameraData baseAdditionalCameraData, ref CameraData cameraData)
        {
            using var profScope = new ProfilingScope(null, Profiling.Pipeline.initializeStackedCameraData);

            var settings = asset;
            cameraData.targetTexture = baseCamera.targetTexture;
            cameraData.cameraType = baseCamera.cameraType;
            bool isSceneViewCamera = cameraData.isSceneViewCamera;

            ///////////////////////////////////////////////////////////////////
            // Environment and Post-processing settings                       /
            ///////////////////////////////////////////////////////////////////
            if (isSceneViewCamera)
            {
                cameraData.volumeLayerMask = 1; // "Default"
                cameraData.volumeTrigger = null;
                cameraData.isStopNaNEnabled = false;
                cameraData.isDitheringEnabled = false;
                cameraData.antialiasing = AntialiasingMode.None;
                cameraData.antialiasingQuality = AntialiasingQuality.High;

            }
            else if (baseAdditionalCameraData != null)
            {
                cameraData.volumeLayerMask = baseAdditionalCameraData.volumeLayerMask;
                cameraData.volumeTrigger = baseAdditionalCameraData.volumeTrigger == null ? baseCamera.transform : baseAdditionalCameraData.volumeTrigger;
                cameraData.isStopNaNEnabled = baseAdditionalCameraData.stopNaN && SystemInfo.graphicsShaderLevel >= 35;
                cameraData.isDitheringEnabled = baseAdditionalCameraData.dithering;
                cameraData.antialiasing = baseAdditionalCameraData.antialiasing;
                cameraData.antialiasingQuality = baseAdditionalCameraData.antialiasingQuality;
            }
            else
            {
                cameraData.volumeLayerMask = 1; // "Default"
                cameraData.volumeTrigger = null;
                cameraData.isStopNaNEnabled = false;
                cameraData.isDitheringEnabled = false;
                cameraData.antialiasing = AntialiasingMode.None;
                cameraData.antialiasingQuality = AntialiasingQuality.High;
            }
        }

        /// <summary>
        /// Initialize settings that can be different for each camera in the stack.
        /// </summary>
        /// <param name="camera">Camera to initialize settings from.</param>
        /// <param name="additionalCameraData">Additional camera data component to initialize settings from.</param>
        /// <param name="resolveFinalTarget">True if this is the last camera in the stack and rendering should resolve to camera target.</param>
        /// <param name="cameraData">Settings to be initilized.</param>
        static void InitializeAdditionalCameraData(Camera camera, CustomAdditionalCameraData additionalCameraData, bool resolveFinalTarget, ref CameraData cameraData)
        {
            using var profScope = new ProfilingScope(null, Profiling.Pipeline.initializeAdditionalCameraData);

            var settings = asset;
            cameraData.camera = camera;

            bool anyShadowsEnabled = settings.supportsMainLightShadows || settings.supportsAdditionalLightShadows;
            cameraData.maxShadowDistance = Mathf.Min(settings.shadowDistance, camera.farClipPlane);
            cameraData.maxShadowDistance = (anyShadowsEnabled && cameraData.maxShadowDistance >= camera.nearClipPlane) ? cameraData.maxShadowDistance : 0.0f;

            bool isSceneViewCamera = cameraData.isSceneViewCamera;
            if (isSceneViewCamera)
            {
                cameraData.renderType = CameraRenderType.Base;
                cameraData.renderer = asset.scriptableRenderer;
            }
            else if (additionalCameraData != null)
            {
                cameraData.renderType = additionalCameraData.renderType;
                cameraData.renderer = additionalCameraData.scriptableRenderer;
                cameraData.maxShadowDistance = (additionalCameraData.renderShadows) ? cameraData.maxShadowDistance : 0.0f;
            }
            else
            {
                cameraData.renderType = CameraRenderType.Base;
                cameraData.renderer = asset.scriptableRenderer;
            }

            // Disable depth and color copy. We should add it in the renderer instead to avoid performance pitfalls
            // of camera stacking breaking render pass execution implicitly.
            bool isOverlayCamera = (cameraData.renderType == CameraRenderType.Overlay);

            Matrix4x4 projectionMatrix = camera.projectionMatrix;

            // Overlay cameras inherit viewport from base.
            // If the viewport is different between them we might need to patch the projection to adjust aspect ratio
            // matrix to prevent squishing when rendering objects in overlay cameras.
            if (isOverlayCamera && !camera.orthographic && cameraData.pixelRect != camera.pixelRect)
            {
                // m00 = (cotangent / aspect), therefore m00 * aspect gives us cotangent.
                float cotangent = camera.projectionMatrix.m00 * camera.aspect;

                // Get new m00 by dividing by base camera aspectRatio.
                float newCotangent = cotangent / cameraData.aspectRatio;
                projectionMatrix.m00 = newCotangent;
            }

            cameraData.SetViewAndProjectionMatrix(camera.worldToCameraMatrix, projectionMatrix);
        }

        /// <summary>
        /// Standalone camera rendering. Use this to render procedural cameras.
        /// This method doesn't call <c>BeginCameraRendering</c> and <c>EndCameraRendering</c> callbacks.
        /// </summary>
        /// <param name="context">Render context used to record commands during execution.</param>
        /// <param name="camera">Camera to render.</param>
        /// <seealso cref="ScriptableRenderContext"/>
        public static void RenderSingleCamera(ScriptableRenderContext context, Camera camera)
        {
            CustomAdditionalCameraData additionalCameraData = null;
            if (IsGameCamera(camera))
                camera.gameObject.TryGetComponent(out additionalCameraData);

            if (additionalCameraData != null && additionalCameraData.renderType != CameraRenderType.Base)
            {
                Debug.LogWarning("Only Base cameras can be rendered with standalone RenderSingleCamera. Camera will be skipped.");
                return;
            }

            InitializeCameraData(camera, additionalCameraData, true, out var cameraData);
#if ADAPTIVE_PERFORMANCE_2_0_0_OR_NEWER
            if (asset.useAdaptivePerformance)
                ApplyAdaptivePerformance(ref cameraData);
#endif
            RenderSingleCamera(context, cameraData, cameraData.postProcessEnabled);
        }

        /// <summary>
        /// Renders a single camera. This method will do culling, setup and execution of the renderer.
        /// </summary>
        /// <param name="context">Render context used to record commands during execution.</param>
        /// <param name="cameraData">Camera rendering data. This might contain data inherited from a base camera.</param>
        /// <param name="anyPostProcessingEnabled">True if at least one camera has post-processing enabled in the stack, false otherwise.</param>
        static void RenderSingleCamera(ScriptableRenderContext context, CameraData cameraData, bool anyPostProcessingEnabled)
        {
            Camera camera = cameraData.camera;
            var renderer = cameraData.renderer;
            if (renderer == null)
            {
                Debug.LogWarning(string.Format("Trying to render {0} with an invalid renderer. Camera rendering will be skipped.", camera.name));
                return;
            }
            //����ֻ����Ⱦ��Щ����ܿ��������塣���Դӳ�����������renderer��������忪ʼ��Ȼ���޳�����Щ�����������Ұ��������塣
            //�����ȡ�޳�����
            if (!TryGetCullingParameters(cameraData, out var cullingParameters))
                return;

            ScriptableRenderer.current = renderer;
            bool isSceneViewCamera = cameraData.isSceneViewCamera;

            // NOTE: Do NOT mix ProfilingScope with named CommandBuffers i.e. CommandBufferPool.Get("name").
            // Currently there's an issue which results in mismatched markers.
            // The named CommandBuffer will close its "profiling scope" on execution.
            // That will orphan ProfilingScope markers as the named CommandBuffer markers are their parents.
            // Resulting in following pattern:
            // exec(cmd.start, scope.start, cmd.end) and exec(cmd.start, scope.end, cmd.end)
            CommandBuffer cmd = CommandBufferPool.Get();

            // TODO: move skybox code from C++ to URP in order to remove the call to context.Submit() inside DrawSkyboxPass
            // Until then, we can't use nested profiling scopes with XR multipass
            CommandBuffer cmdScope = cmd;

            ProfilingSampler sampler = Profiling.TryGetOrAddCameraSampler(camera);
            using (new ProfilingScope(cmdScope, sampler)) // Enqueues a "BeginSample" command into the CommandBuffer cmd
            {
                renderer.Clear(cameraData.renderType);

                using (new ProfilingScope(cmd, Profiling.Pipeline.Renderer.setupCullingParameters))
                {
                    renderer.SetupCullingParameters(ref cullingParameters, ref cameraData);
                }
                //����ĿǰΪֹ�� CommandBuffer cmd ���Ŷӵ���������͵� ScriptableRenderContext ������,��ǿ��һ�Σ��ⲻ������ִ��������ǽ����Ǹ��Ƶ������ĵ��ڲ�������
                context.ExecuteCommandBuffer(cmd); 
                cmd.Clear();
                //����Ⱦ��������ʱ�����Ǳ�����ʽ�ؽ�UI���ӵ����缸���У��������������Ϊ��������ScriptableRenderContext.EmitWorldGeometryForSceneView�����µ�ֻ�ڱ༭�������е�PrepareForSceneWindow�����е��á�������CameraType���Ե���CameraTypes.SceneViewʱ�����Ǳ���ʹ�ó����������Ⱦ��
#if UNITY_EDITOR
                // Emit scene view UI
                if (isSceneViewCamera)
                {
                    ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
                }
#endif
                //һ�������޳��������Ϳ���ʹ�����ǽ����޳�������ͨ�����þ�̬CullResults.Cull���������޳���������������Ϊ����������ɵġ������һ��CullResults�ṹ�����а����йؿɼ����ݵ���Ϣ��
                var cullResults = context.Cull(ref cullingParameters);
                InitializeRenderingData(asset, ref cameraData, ref cullResults, anyPostProcessingEnabled, out var renderingData);

                using (new ProfilingScope(cmd, Profiling.Pipeline.Renderer.setup))
                {
                    renderer.Setup(context, ref renderingData);
                }

                // Timing scope inside
                renderer.Execute(context, ref renderingData);

            } // When ProfilingSample goes out of scope, an "EndSample" command is enqueued into CommandBuffer cmd

            context.ExecuteCommandBuffer(cmd); 
            //�������Ҫ����Դ�Խ�������洢��Unity����ı�������������ǲ�����Ҫ��Щ��Դ������������ͷ����ǡ�
            CommandBufferPool.Release(cmd);

            using (new ProfilingScope(cmd, Profiling.Pipeline.Context.submit))
            {
                //�����Ļ��ӳ�ʵ����Ⱦ��ֱ�������ύ��Ϊֹ��
                //������Ȼû�п�����պг�������Ϸ�����С�������Ϊ���Ƕ������ķ����������ǻ���ġ�ʵ�ʵĹ���������ͨ��Submit���������ύִ�к���
                context.Submit(); 
            }

            ScriptableRenderer.current = null;
        }

        static bool TryGetCullingParameters(CameraData cameraData, out ScriptableCullingParameters cullingParams)
        {
            return cameraData.camera.TryGetCullingParameters(false, out cullingParams);
        }

        static void InitializeRenderingData(CustomRenderPineAsset settings, ref CameraData cameraData, ref CullingResults cullResults,
            bool anyPostProcessingEnabled, out RenderingData renderingData)
        {
            using var profScope = new ProfilingScope(null, Profiling.Pipeline.initializeRenderingData);

            var visibleLights = cullResults.visibleLights;

            int mainLightIndex = GetMainLightIndex(settings, visibleLights);
            bool mainLightCastShadows = false;
            bool additionalLightsCastShadows = false;

            if (cameraData.maxShadowDistance > 0.0f)
            {
                mainLightCastShadows = (mainLightIndex != -1 && visibleLights[mainLightIndex].light != null &&
                                        visibleLights[mainLightIndex].light.shadows != LightShadows.None);

                // If additional lights are shaded per-pixel they cannot cast shadows
                if (settings.additionalLightsRenderingMode == LightRenderingMode.PerPixel)
                {
                    for (int i = 0; i < visibleLights.Length; ++i)
                    {
                        if (i == mainLightIndex)
                            continue;

                        Light light = visibleLights[i].light;

                        // UniversalRP doesn't support additional directional lights or point light shadows yet
                        if (visibleLights[i].lightType == LightType.Spot && light != null && light.shadows != LightShadows.None)
                        {
                            additionalLightsCastShadows = true;
                            break;
                        }
                    }
                }
            }

            renderingData.cullResults = cullResults;
            renderingData.cameraData = cameraData;
            InitializeLightData(settings, visibleLights, mainLightIndex, out renderingData.lightData);
            InitializeShadowData(settings, visibleLights, mainLightCastShadows, additionalLightsCastShadows && !renderingData.lightData.shadeAdditionalLightsPerVertex, out renderingData.shadowData);
            InitializePostProcessingData(settings, out renderingData.postProcessingData);
            renderingData.supportsDynamicBatching = settings.supportsDynamicBatching;
            renderingData.perObjectData = GetPerObjectLightFlags(renderingData.lightData.additionalLightsCount);
            renderingData.postProcessingEnabled = anyPostProcessingEnabled;
        }

        static void InitializePostProcessingData(CustomRenderPineAsset settings, out PostProcessingData postProcessingData)
        {
            postProcessingData.gradingMode = settings.supportsHDR
                ? settings.colorGradingMode
                : ColorGradingMode.LowDynamicRange;

            postProcessingData.lutSize = settings.colorGradingLutSize;
        }

        static void InitializeShadowData(CustomRenderPineAsset settings, NativeArray<VisibleLight> visibleLights, bool mainLightCastShadows, bool additionalLightsCastShadows, out ShadowData shadowData)
        {
            using var profScope = new ProfilingScope(null, Profiling.Pipeline.initializeShadowData);

            m_ShadowBiasData.Clear();

            for (int i = 0; i < visibleLights.Length; ++i)
            {
                Light light = visibleLights[i].light;
                CustomAdditionalLightData data = null;
                if (light != null)
                {
                    light.gameObject.TryGetComponent(out data);
                }

                if (data && !data.usePipelineSettings)
                    m_ShadowBiasData.Add(new Vector4(light.shadowBias, light.shadowNormalBias, 0.0f, 0.0f));
                else
                    m_ShadowBiasData.Add(new Vector4(settings.shadowDepthBias, settings.shadowNormalBias, 0.0f, 0.0f));
            }

            shadowData.bias = m_ShadowBiasData;
            shadowData.supportsMainLightShadows = SystemInfo.supportsShadows && settings.supportsMainLightShadows && mainLightCastShadows;

            // We no longer use screen space shadows in URP.
            // This change allows us to have particles & transparent objects receive shadows.
            shadowData.requiresScreenSpaceShadowResolve = false;

            shadowData.mainLightShadowCascadesCount = settings.shadowCascadeCount;
            shadowData.mainLightShadowmapWidth = settings.mainLightShadowmapResolution;
            shadowData.mainLightShadowmapHeight = settings.mainLightShadowmapResolution;

            switch (shadowData.mainLightShadowCascadesCount)
            {
                case 1:
                    shadowData.mainLightShadowCascadesSplit = new Vector3(1.0f, 0.0f, 0.0f);
                    break;

                case 2:
                    shadowData.mainLightShadowCascadesSplit = new Vector3(settings.cascade2Split, 1.0f, 0.0f);
                    break;

                case 3:
                    shadowData.mainLightShadowCascadesSplit = new Vector3(settings.cascade3Split.x, settings.cascade3Split.y, 0.0f);
                    break;

                default:
                    shadowData.mainLightShadowCascadesSplit = settings.cascade4Split;
                    break;
            }

            shadowData.supportsAdditionalLightShadows = SystemInfo.supportsShadows && settings.supportsAdditionalLightShadows && additionalLightsCastShadows;
            shadowData.additionalLightsShadowmapWidth = shadowData.additionalLightsShadowmapHeight = settings.additionalLightsShadowmapResolution;
            shadowData.supportsSoftShadows = settings.supportsSoftShadows && (shadowData.supportsMainLightShadows || shadowData.supportsAdditionalLightShadows);
            shadowData.shadowmapDepthBufferBits = 16;
        }

        // Main Light is always a directional light
        static int GetMainLightIndex(CustomRenderPineAsset settings, NativeArray<VisibleLight> visibleLights)
        {
            using var profScope = new ProfilingScope(null, Profiling.Pipeline.getMainLightIndex);

            int totalVisibleLights = visibleLights.Length;

            if (totalVisibleLights == 0 || settings.mainLightRenderingMode != LightRenderingMode.PerPixel)
                return -1;

            Light sunLight = RenderSettings.sun;
            int brightestDirectionalLightIndex = -1;
            float brightestLightIntensity = 0.0f;
            for (int i = 0; i < totalVisibleLights; ++i)
            {
                VisibleLight currVisibleLight = visibleLights[i];
                Light currLight = currVisibleLight.light;

                // Particle system lights have the light property as null. We sort lights so all particles lights
                // come last. Therefore, if first light is particle light then all lights are particle lights.
                // In this case we either have no main light or already found it.
                if (currLight == null)
                    break;

                if (currVisibleLight.lightType == LightType.Directional)
                {
                    // Sun source needs be a directional light
                    if (currLight == sunLight)
                        return i;

                    // In case no sun light is present we will return the brightest directional light
                    if (currLight.intensity > brightestLightIntensity)
                    {
                        brightestLightIntensity = currLight.intensity;
                        brightestDirectionalLightIndex = i;
                    }
                }
            }

            return brightestDirectionalLightIndex;
        }

        static void InitializeLightData(CustomRenderPineAsset settings, NativeArray<VisibleLight> visibleLights, int mainLightIndex, out LightData lightData)
        {
            using var profScope = new ProfilingScope(null, Profiling.Pipeline.initializeLightData);

            int maxPerObjectAdditionalLights = CustomRenderPipeline.maxPerObjectLights;
            int maxVisibleAdditionalLights = CustomRenderPipeline.maxVisibleAdditionalLights;

            lightData.mainLightIndex = mainLightIndex;

            if (settings.additionalLightsRenderingMode != LightRenderingMode.Disabled)
            {
                lightData.additionalLightsCount =
                    Math.Min((mainLightIndex != -1) ? visibleLights.Length - 1 : visibleLights.Length,
                        maxVisibleAdditionalLights);
                lightData.maxPerObjectAdditionalLightsCount = Math.Min(settings.maxAdditionalLightsCount, maxPerObjectAdditionalLights);
            }
            else
            {
                lightData.additionalLightsCount = 0;
                lightData.maxPerObjectAdditionalLightsCount = 0;
            }

            lightData.shadeAdditionalLightsPerVertex = settings.additionalLightsRenderingMode == LightRenderingMode.PerVertex;
            lightData.visibleLights = visibleLights;
            lightData.supportsMixedLighting = settings.supportsMixedLighting;
        }

        static PerObjectData GetPerObjectLightFlags(int additionalLightsCount)
        {
            using var profScope = new ProfilingScope(null, Profiling.Pipeline.getPerObjectLightFlags);
            //ShadowMask  阴影蒙版贴图，对应unity_ShadowMask，shaowmask烘焙的情况下，对于静态物体并且是开lightmap的情况下，会从这里获取阴影信息
            //OcclusionProbe  遮蔽探针，对应unity_ProbesOcclusion，shaowmask烘焙的情况下，对于动态物体会从这里获取阴影信息
            //ReflectionProbes  反射探针，对应unity_SpecCube0，默认情况下是天空盒，它是一个立方体纹理（Cube Map）
            var configuration = PerObjectData.ReflectionProbes | PerObjectData.Lightmaps | PerObjectData.LightProbe 
                                    | PerObjectData.LightData | PerObjectData.OcclusionProbe | PerObjectData.ShadowMask;

            if (additionalLightsCount > 0)
            {
                configuration |= PerObjectData.LightData;

                // In this case we also need per-object indices (unity_LightIndices)
                if (!RenderingUtils.useStructuredBuffer)
                    configuration |= PerObjectData.LightIndices;
            }

            return configuration;
        }

        static void SetupPerFrameShaderConstants()
        {
            using var profScope = new ProfilingScope(null, Profiling.Pipeline.setupPerFrameShaderConstants);

            // When glossy reflections are OFF in the shader we set a constant color to use as indirect specular
            //某个对象的光照探针或光照贴图不存在或未启用，系统会将它用作最终的回退项
            SphericalHarmonicsL2 ambientSH = RenderSettings.ambientProbe;
            Color linearGlossyEnvColor = new Color(ambientSH[0, 0], ambientSH[1, 0], ambientSH[2, 0]) * RenderSettings.reflectionIntensity;
            Color glossyEnvColor = CoreUtils.ConvertLinearToActiveColorSpace(linearGlossyEnvColor);
            Shader.SetGlobalVector(ShaderPropertyId.glossyEnvironmentColor, glossyEnvColor);

            // Ambient
            Shader.SetGlobalVector(ShaderPropertyId.ambientSkyColor, CoreUtils.ConvertSRGBToActiveColorSpace(RenderSettings.ambientSkyColor));
            Shader.SetGlobalVector(ShaderPropertyId.ambientEquatorColor, CoreUtils.ConvertSRGBToActiveColorSpace(RenderSettings.ambientEquatorColor));
            Shader.SetGlobalVector(ShaderPropertyId.ambientGroundColor, CoreUtils.ConvertSRGBToActiveColorSpace(RenderSettings.ambientGroundColor));

            // Used when subtractive mode is selected
            Shader.SetGlobalVector(ShaderPropertyId.subtractiveShadowColor, CoreUtils.ConvertSRGBToActiveColorSpace(RenderSettings.subtractiveShadowColor));

            // Required for 2D Unlit Shadergraph master node as it doesn't currently support hidden properties.
            Shader.SetGlobalColor(ShaderPropertyId.rendererColor, Color.white);
        }

    }
}