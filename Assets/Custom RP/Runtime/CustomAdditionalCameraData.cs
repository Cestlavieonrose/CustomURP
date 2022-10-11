
using System;
using System.Collections.Generic;
using UnityEditor;

namespace UnityEngine.Rendering.Custom
{
    /// <summary>
    /// Holds information about the render type of a camera. Options are Base or Overlay.
    /// Base rendering type allows the camera to render to either the screen or to a texture.
    /// Overlay rendering type allows the camera to render on top of a previous camera output, thus compositing camera results.
    /// </summary>
    public enum CameraRenderType
    {
        Base,
        Overlay,
    }

    /// <summary>
    /// Holds information about the post-processing anti-aliasing mode.
    /// When set to <c>None</c> no post-processing anti-aliasing pass will be performed.
    /// When set to <c>Fast</c> a fast approximated anti-aliasing pass will render when resolving the camera to screen.
    /// When set to <c>SubpixelMorphologicalAntiAliasing</c> SMAA pass will render when resolving the camera to screen. You can choose the SMAA quality by setting <seealso cref="AntialiasingQuality"/>
    /// </summary>
    public enum AntialiasingMode
    {
        None,
        FastApproximateAntialiasing,
        SubpixelMorphologicalAntiAliasing,
        //TemporalAntialiasing
    }

    /// <summary>
    /// Controls SMAA anti-aliasing quality.
    /// </summary>
    public enum AntialiasingQuality
    {
        Low,
        Medium,
        High
    }

    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    [ImageEffectAllowedInSceneView]
    public class CustomAdditionalCameraData : MonoBehaviour, ISerializationCallbackReceiver
    {
        [SerializeField]
        bool m_RenderShadows = true;
        [SerializeField] CameraRenderType m_CameraType = CameraRenderType.Base;
        [SerializeField] List<Camera> m_Cameras = new List<Camera>();
        [SerializeField] int m_RendererIndex = -1;

        [SerializeField] LayerMask m_VolumeLayerMask = 1; // "Default"
        [SerializeField] Transform m_VolumeTrigger = null;

        [SerializeField] bool m_RenderPostProcessing = false;
        [SerializeField] AntialiasingMode m_Antialiasing = AntialiasingMode.None;
        [SerializeField] AntialiasingQuality m_AntialiasingQuality = AntialiasingQuality.High;
        [SerializeField] bool m_StopNaN = false;
        [SerializeField] bool m_Dithering = false;

        [NonSerialized] Camera m_Camera;

#if UNITY_EDITOR
        internal new Camera camera
#else
        internal Camera camera
#endif
        {
            get
            {
                if (!m_Camera)
                {
                    gameObject.TryGetComponent<Camera>(out m_Camera);
                }
                return m_Camera;
            }
        }

        /// <summary>
        /// Returns the camera renderType.
        /// <see cref="CameraRenderType"/>.
        /// </summary>
        public CameraRenderType renderType
        {
            get => m_CameraType;
            set => m_CameraType = value;
        }

        /// <summary>
        /// Returns true if this camera should render post-processing.
        /// </summary>
        public bool renderPostProcessing
        {
            get => m_RenderPostProcessing;
            set => m_RenderPostProcessing = value;
        }

        /// <summary>
        /// Controls if this camera should render shadows.
        /// </summary>
        public bool renderShadows
        {
            get => m_RenderShadows;
            set => m_RenderShadows = value;
        }

        /// <summary>
        /// Returns the <see cref="ScriptableRenderer"/> that is used to render this camera.
        /// </summary>
        public ScriptableRenderer scriptableRenderer
        {
            get
            {
                if (CustomRenderPipeline.asset is null)
                    return null;
                if (!CustomRenderPipeline.asset.ValidateRendererData(m_RendererIndex))
                {
                    int defaultIndex = CustomRenderPipeline.asset.m_DefaultRendererIndex;
                    Debug.LogWarning(
                        $"Renderer at <b>index {m_RendererIndex.ToString()}</b> is missing for camera <b>{camera.name}</b>, falling back to Default Renderer. <b>{CustomRenderPipeline.asset.m_RendererDataList[defaultIndex].name}</b>",
                        CustomRenderPipeline.asset);
                    return CustomRenderPipeline.asset.GetRenderer(defaultIndex);
                }
                return CustomRenderPipeline.asset.GetRenderer(m_RendererIndex);
            }
        }

        /// <summary>
        /// Returns the camera stack. Only valid for Base cameras.
        /// Overlay cameras have no stack and will return null.
        /// <seealso cref="CameraRenderType"/>.
        /// </summary>
        public List<Camera> cameraStack
        {
            get
            {
                if (renderType != CameraRenderType.Base)
                {
                    var camera = gameObject.GetComponent<Camera>();
                    Debug.LogWarning(string.Format("{0}: This camera is of {1} type. Only Base cameras can have a camera stack.", camera.name, renderType));
                    return null;
                }

                if (scriptableRenderer.supportedRenderingFeatures.cameraStacking == false)
                {
                    var camera = gameObject.GetComponent<Camera>();
                    Debug.LogWarning(string.Format("{0}: This camera has a ScriptableRenderer that doesn't support camera stacking. Camera stack is null.", camera.name));
                    return null;
                }
                return m_Cameras;
            }
        }

        internal void UpdateCameraStack()
        {
#if UNITY_EDITOR
            Undo.RecordObject(this, "Update camera stack");
#endif
            int prev = m_Cameras.Count;
            m_Cameras.RemoveAll(cam => cam == null);
            int curr = m_Cameras.Count;
            int removedCamsCount = prev - curr;
            if (removedCamsCount != 0)
            {
                Debug.LogWarning(name + ": " + removedCamsCount + " camera overlay" + (removedCamsCount > 1 ? "s" : "") + " no longer exists and will be removed from the camera stack.");
            }
        }

        public LayerMask volumeLayerMask
        {
            get => m_VolumeLayerMask;
            set => m_VolumeLayerMask = value;
        }

        public Transform volumeTrigger
        {
            get => m_VolumeTrigger;
            set => m_VolumeTrigger = value;
        }

        /// <summary>
        /// Returns the current anti-aliasing mode used by this camera.
        /// <see cref="AntialiasingMode"/>.
        /// </summary>
        public AntialiasingMode antialiasing
        {
            get => m_Antialiasing;
            set => m_Antialiasing = value;
        }

        /// <summary>
        /// Returns the current anti-aliasing quality used by this camera.
        /// <seealso cref="antialiasingQuality"/>.
        /// </summary>
        public AntialiasingQuality antialiasingQuality
        {
            get => m_AntialiasingQuality;
            set => m_AntialiasingQuality = value;
        }

        public bool stopNaN
        {
            get => m_StopNaN;
            set => m_StopNaN = value;
        }

        public bool dithering
        {
            get => m_Dithering;
            set => m_Dithering = value;
        }


        public void OnAfterDeserialize()
        {
            
        }

        public void OnBeforeSerialize()
        {
            
        }
    }

}
