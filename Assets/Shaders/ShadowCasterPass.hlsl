#ifndef CUSTOM_SHADOW_CASTER_PASS_INCLUDED
#define CUSTOM_SHADOW_CASTER_PASS_INCLUDED

#include "Universal/ShaderLibrary/Core.hlsl"
// #include "Universal/ShaderLibrary/Shadows.hlsl"


float3 _LightDirection;
//用作顶点函数的输入参数
struct Attributes
{
    float4 positionOS   : POSITION;
    float3 normalOS     : NORMAL;
    float2 baseUV:TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

//用作片元函数的输入参数
struct Varyings
{
    float4 positionCS               : SV_POSITION;
    float2 baseUV:VAR_BASE_UV;
};

float4 GetShadowPositionHClip(Attributes input)
{
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

    // float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
    float4 positionCS = TransformWorldToHClip(positionWS);

#if UNITY_REVERSED_Z
    positionCS.z = min(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#else
    positionCS.z = max(positionCS.z, positionCS.w * UNITY_NEAR_CLIP_VALUE);
#endif
    return positionCS;
}

//顶点函数
Varyings ShadowCasterPassVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
    //计算缩放和便宜后的UV坐标
    output.baseUV = baseST.xy*input.baseUV + baseST.zw;
    output.positionCS = GetShadowPositionHClip(input);
    return output;
}

//片元函数
void ShadowCasterPassFragment(Varyings input)
{
    // float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
    // float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
    // float4 base = baseMap*baseColor;
    // #if defined(_CLIPPING)
    //     clip(base.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff));
    // #endif
}

#endif

