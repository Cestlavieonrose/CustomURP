Shader "CustomRP/Lit"
{
    Properties
    {
        //金属度和光滑度
        _Metallic("Metallic", Range(0,  1)) =  0
        _Smoothness("Smoothness", Range(0,  1)) = 0.5

        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1.0

        _BaseColor("Color", Color) = (0.5, 0.5, 0.5, 1.0)
        _BaseMap("Texture", 2D) = "white" {}
        
        //透明度测试的阈值
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [Toggle(_ALPHATEST_ON)] _Clipping("Alpha Clipping", Float) = 0
        [Toggle(_ALPHAPREMULTIPLY_ON)] _PremulAlpha("Alpha Premultiply", Float) = 0
        [Toggle(_RECEIVE_SHADOWS_OFF)] _ReceiveShadowsOff ("不接受阴影", Float) = 0
        _Shadows ("是否接受阴影投射", Float) = 0
        //设置混合模式
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        //默认写入深度缓冲区
        [Enum(Off, 0, On, 1)]_ZWrite("Z Write", Float) = 1

        [Toggle(_EMISSION)] _Emision("自发光", Float) = 0
        [HDR] _EmissionColor("Color", Color) = (0,0,0)
        _EmissionMap("Emission", 2D) = "white" {}

        [HideInInspector] _MainTex("Texture for lightmap", 2D) = "white" {}
        [HideInInspector] _Color("Color for lightmap", Color) = (0.5, 0.5, 0.5, 1.0)
    }
    SubShader
    {
        Pass
        {
            Tags
            {
                "LightMode" = "CustomLit"
            }
            //定义混合模式
            Blend[_SrcBlend][_DstBlend]
            //是否写入深度
            ZWrite[_ZWrite]
            HLSLPROGRAM
            //renderdoc debugger
            #pragma enable_d3d11_debug_symbols

            //在Pass中将着色器编译目标级别设置为3.5，该级别越高，允许使用现代GPU的功能越多。
            //如果不设置，Unity默认将着色器编译目标级别设为2.5，介于DirectX着色器模型2.0和3.0之间。
            //但OpenGL ES 2.0和WebGL 1.0的图形API是不能处理可变长度的循环的，也不支持线性空间。
            //所以我们在工程构建时可以关闭对OpenGL ES 2.0和WebGL 1.0的支持。
            #pragma target 3.5

            // Universal Pipeline keywords
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS//MainLightShadowCasterPass.cs中如果进行主光源阴影投射就开启
            #pragma multi_compile _ _MIXED_LIGHTING_SUBTRACTIVE //light为mix，且shadow设置不为none（主要控制bakeGI和实时阴影的混合，因此light的shadow必须开），混合模式为substractive的时候开启
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING //substractive 和 shadowmask的非distance下都开启
            #pragma multi_compile _ SHADOWS_SHADOWMASK //和_MIXED_LIGHTING_SUBTRACTIVE是一对互斥的
            // Material Keywords
            #pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF //PBR是否忽略高光部分的计算，材质设置
            #pragma shader_feature_local_fragment _ENVIRONMENTREFLECTIONS_OFF  //是否关闭环境反射球计算，材质上设置
            #pragma shader_feature_local _RECEIVE_SHADOWS_OFF //材质是否开启接受光照
            #pragma shader_feature_local_fragment _EMISSION //自发光
            

            // Unity defined keywords
            #pragma multi_compile _ LIGHTMAP_ON//设置lightmap还是probe需要重新烘焙后才会生效
            
            #pragma multi_compile_instancing
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment
            #include "LitInput.hlsl"
            #include "LitPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Tags 
            {
                "LightMode" = "ShadowCaster"
            }
            ColorMask 0
    
            HLSLPROGRAM
            #pragma target 3.5
            //renderdoc debugger
            #pragma enable_d3d11_debug_symbols

            // Material Keywords
            #pragma shader_feature_local_fragment _ALPHATEST_ON



            //gpu instancing
            #pragma multi_compile_instancing
            #pragma vertex ShadowCasterPassVertex
            #pragma fragment ShadowCasterPassFragment
            #include "LitInput.hlsl"
            #include "ShadowCasterPass.hlsl"
            ENDHLSL
        }

        Pass
        {
            Name "Meta"
            Tags{"LightMode" = "Meta"}

            Cull Off

            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma target 4.5

            #pragma vertex UniversalVertexMeta
            #pragma fragment UniversalFragmentMeta

            // #pragma shader_feature_local_fragment _SPECULAR_SETUP
            #pragma shader_feature_local_fragment _EMISSION
            // #pragma shader_feature_local_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_local_fragment _ALPHATEST_ON
            // #pragma shader_feature_local_fragment _ _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            // #pragma shader_feature_local _ _DETAIL_MULX2 _DETAIL_SCALED

            // #pragma shader_feature_local_fragment _SPECGLOSSMAP

            #include "LitInput.hlsl"
            #include "LitMetaPass.hlsl"

            ENDHLSL
        }
    }
    CustomEditor "CustomShaderGUI"
}
