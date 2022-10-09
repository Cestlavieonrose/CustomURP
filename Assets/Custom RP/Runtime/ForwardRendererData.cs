using System;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
#endif

namespace UnityEngine.Rendering.Custom
{
    [Serializable, ReloadGroup, ExcludeFromPreset]
    public class ForwardRendererData : ScriptableRendererData
    {
        [SerializeField] LayerMask m_OpaqueLayerMask = -1;
        [SerializeField] LayerMask m_TransparentLayerMask = -1;
        [SerializeField] StencilStateData m_DefaultStencilState = new StencilStateData() { passOperation = StencilOp.Replace }; // This default state is compatible with deferred renderer.
        //半透明的物体渲染是否需要接受阴影
        [SerializeField] bool m_ShadowTransparentReceive = true;

#if UNITY_EDITOR
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1812")]
        internal class CreateForwardRendererAsset : EndNameEditAction
        {
            public override void Action(int instanceId, string pathName, string resourceFile)
            {
                var instance = CreateInstance<ForwardRendererData>();
                AssetDatabase.CreateAsset(instance, pathName);
                ResourceReloader.ReloadAllNullIn(instance, CustomRenderPineAsset.packagePath);
                Selection.activeObject = instance;
            }
        }

        [MenuItem("Assets/Create/Rendering/Custom Render Pipeline/Forward Renderer", priority = CoreUtils.assetCreateMenuPriority2)]
        static void CreateForwardRendererData()
        {
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0, CreateInstance<CreateForwardRendererAsset>(), "CustomForwardRendererData.asset", null, null);
        }
#endif

        protected override ScriptableRenderer Create()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                ResourceReloader.TryReloadAllNullIn(this, CustomRenderPineAsset.packagePath);
               // ResourceReloader.TryReloadAllNullIn(postProcessData, UniversalRenderPipelineAsset.packagePath);
            }
#endif
            return new ForwardRenderer(this);
        }

        /// <summary>
        /// Use this to configure how to filter opaque objects.
        /// </summary>
        public LayerMask opaqueLayerMask
        {
            get => m_OpaqueLayerMask;
            set
            {
                SetDirty();
                m_OpaqueLayerMask = value;
            }
        }

        /// <summary>
        /// Use this to configure how to filter transparent objects.
        /// </summary>
        public LayerMask transparentLayerMask
        {
            get => m_TransparentLayerMask;
            set
            {
                SetDirty();
                m_TransparentLayerMask = value;
            }
        }

        public StencilStateData defaultStencilState
        {
            get => m_DefaultStencilState;
            set
            {
                SetDirty();
                m_DefaultStencilState = value;
            }
        }

        /// <summary>
        /// True if transparent objects receive shadows.
        /// </summary>
        public bool shadowTransparentReceive
        {
            get => m_ShadowTransparentReceive;
            set
            {
                SetDirty();
                m_ShadowTransparentReceive = value;
            }
        }
    }
}
