Shader "CustomRP/Lit"
{
    Properties
    {
        //金属度和光滑度
        _Metallic("Metallic", Range(0,  1)) =  0
        _Smoothness("Smoothness", Range(0,  1)) = 0.5
        _BaseColor("Color", Color) = (0.5, 0.5, 0.5, 1.0)
        _BaseMap("Texture", 2D) = "white" {}
        //透明度测试的阈值
        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5
        [Toggle(_CLIPPING)] _Clipping("Alpha Clipping", Float) = 0
        [Toggle(_ALPHAPREMULTIPLY_ON)] _PremulAlpha("Alpha Premultiply", Float) = 0
        //设置混合模式
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Sec Blend", Float) = 1
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0
        //默认写入深度缓冲区
        [Enum(Off, 0, On, 1)]_ZWrite("Z Write", Float) = 1
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
            //在Pass中将着色器编译目标级别设置为3.5，该级别越高，允许使用现代GPU的功能越多。
            //如果不设置，Unity默认将着色器编译目标级别设为2.5，介于DirectX着色器模型2.0和3.0之间。
            //但OpenGL ES 2.0和WebGL 1.0的图形API是不能处理可变长度的循环的，也不支持线性空间。
            //所以我们在工程构建时可以关闭对OpenGL ES 2.0和WebGL 1.0的支持。
            #pragma target 3.5

            // Universal Pipeline keywords
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            // Material Keywords
            #pragma shader_feature_local_fragment _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_local_fragment _CLIPPING
            #pragma shader_feature_local_fragment _SPECULARHIGHLIGHTS_OFF //PBR是否忽略高光部分的计算

            
            #pragma multi_compile_instancing
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment
            #include "LitPass.hlsl"
            ENDHLSL
           
        }
    }
    CustomEditor "CustomShaderGUI"
}
