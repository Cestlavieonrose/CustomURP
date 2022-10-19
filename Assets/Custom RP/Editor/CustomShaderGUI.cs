using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class CustomShaderGUI : ShaderGUI
{
    enum ShadowMode 
    {
        On = 0,
        Off
    }
    MaterialEditor editor;
    Object[] materials;
    MaterialProperty[] properties;
    bool showPresets;
    public override void OnGUI(MaterialEditor materialEditor, MaterialProperty[] properties)
    {
        base.OnGUI(materialEditor, properties);
        editor = materialEditor;
        materials = materialEditor.targets;
        this.properties = properties;
        BakeEmission();
        EditorGUILayout.Space();
        showPresets = EditorGUILayout.Foldout(showPresets, "Presets", true);
        if (showPresets)
        {
            OpaquePreset();
            ClipPreset();
            FadePreset();
            TransparentPreset();
        }

        if (EditorGUI.EndChangeCheck())
        {
           // CopyLightMappingProperties();
           SetShadowCasterPass();
        }
        // ReceiveShadowPreset();
    }

    //设置材质的shadowcaster pass 是否启用
    void SetShadowCasterPass()
    {
        MaterialProperty shadows = FindProperty("_Shadows", properties, false);
        if (shadows == null || shadows.hasMixedValue)
        {
            return;
        }

        bool enabled = shadows.floatValue < (float)ShadowMode.Off;
        foreach(Material m in materials)
        {
            m.SetShaderPassEnabled("ShadowCaster", enabled);
        }
    }

    void CopyLightMappingProperties()
    {
        MaterialProperty mainTex = FindProperty("_MainTex", properties, false);
        MaterialProperty baseMap = FindProperty("_BaseMap", properties, false);

        if(mainTex != null && baseMap != null)
        {
            mainTex.textureValue = baseMap.textureValue;
            mainTex.textureScaleAndOffset = baseMap.textureScaleAndOffset;
        }
        MaterialProperty color = FindProperty("_Color", properties, false);
        MaterialProperty baseColor = FindProperty("_BaseColor", properties, false);
        if(color != null && baseColor != null)
        {
            color.colorValue = baseColor.colorValue;
        }
    }

    //烘焙自发光
    void BakeEmission()
    {
        EditorGUI.BeginChangeCheck();
        editor.LightmapEmissionProperty();
        if (EditorGUI.EndChangeCheck())
        {
            foreach (Material m in editor.targets)
            {
                m.globalIlluminationFlags &=~MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }
        }
    }

    //设置材质属性
    void SetProperty(string name, float value)
    {
        if (HasProperty(name))
            FindProperty(name, properties).floatValue = value;
    }

    //设置关键字状态
    void SetKeyWord(string keyword, bool endabled)
    {
        
        if (endabled)
        {
            foreach (Material m in materials)
            {
                m.EnableKeyword(keyword);
            }
        } else
        {
            foreach (Material m in materials)
            {
                m.DisableKeyword(keyword);
            }
        }
    }

    //同时设置关键字和属性
    void SetProperty(string name, string keyword, bool value)
    {
        SetProperty(name, value ? 1f:0f);
        SetKeyWord(keyword, value);
    }

    bool Clipping
    {
        set => SetProperty("_Clipping", "_ALPHATEST_ON", value);
    }

    float SurfaceType
    {
        set => SetProperty("_Surface", value);
    }

    bool PremultiplyAlpha 
    {
        set => SetProperty("_PremulAlpha", "_ALPHAPREMULTIPLY_ON", value);
    }

    BlendMode SrcBlend
    {
        set => SetProperty("_SrcBlend", (float)value);
    }

    BlendMode DstBlend
    {
        set => SetProperty("_DstBlend", (float)value);
    }

    bool ZWrite 
    {
        set => SetProperty("_ZWrite", value ? 1f : 0f);
    }

    RenderQueue RenderQueue
    {
        set
        {
            foreach (Material m in materials)
            {
                m.renderQueue = (int) value;
            }
        }
    }

    bool PresetButton(string name)
    {
        if (GUILayout.Button(name))
        {
            //属性重置
            editor.RegisterPropertyChangeUndo(name);
            return true;
        }
        return false;
    }

    void ReceiveShadowPreset()
    {
        if (GUILayout.Toggle(true, "接受阴影"))
        {
            SetKeyWord("_RECEIVE_SHADOWS_OFF", false);
        } else 
        {
            SetKeyWord("_RECEIVE_SHADOWS_OFF", true);
        }
    }

    void OpaquePreset()
    {
        if (PresetButton("Opaque"))
        {
            Clipping = false;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.Zero;
            ZWrite = true;
            RenderQueue = RenderQueue.Geometry;
        }
    }

    void ClipPreset()
    {
        if (PresetButton("Clip"))
        {
            Clipping = true;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.One;
            DstBlend = BlendMode.Zero;
            ZWrite = true;
            RenderQueue = RenderQueue.AlphaTest;
        }
    }
    //标准的透明渲染模式
    void FadePreset()
    {
        if (HasPremultiplyAlpha && PresetButton("Fade"))
        {
            Clipping = false;
            PremultiplyAlpha = true;
            SrcBlend = BlendMode.SrcAlpha;
            DstBlend = BlendMode.OneMinusSrcAlpha;
            ZWrite = false;
            RenderQueue = RenderQueue.Transparent;
        }
    }

    void TransparentPreset()
    {
        if (PresetButton("Transparent"))
        {
            Clipping = false;
            PremultiplyAlpha = false;
            SrcBlend = BlendMode.SrcAlpha;
            DstBlend = BlendMode.OneMinusSrcAlpha;
            ZWrite = false;
            RenderQueue = RenderQueue.Transparent;
        }
    }

    //如果shader的预乘属性不存在，不需要现实对应渲染模式的预设置按钮
    bool HasProperty(string name) => FindProperty(name, properties, false) != null;

    bool HasPremultiplyAlpha => HasProperty("_PremulAlpha");
}
