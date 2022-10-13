#ifndef UNIVERSAL_SHADOWS_INCLUDED
#define UNIVERSAL_SHADOWS_INCLUDED

#include "../../Core/ShaderLibrary/Common.hlsl"
#include "Core.hlsl"

#define MAX_SHADOW_CASCADES 4

#if !defined(_RECEIVE_SHADOWS_OFF)
    #if defined(_MAIN_LIGHT_SHADOWS)
        #define MAIN_LIGHT_CALCULATE_SHADOWS  //材质开启接受，主光源开启阴影caster
    #endif
#endif
#define REQUIRES_WORLD_SPACE_POS_INTERPOLATOR //Varyings结构体中是否加入positionWS


TEXTURE2D_SHADOW(_MainLightShadowmapTexture);
// TEXTURE2D(_MainLightShadowmapTexture)
// SamplerComparisonState sampler_MainLightShadowmapTexture
SAMPLER_CMP(sampler_MainLightShadowmapTexture);

// GLES3 causes a performance regression in some devices when using CBUFFER.
#ifndef SHADER_API_GLES3
CBUFFER_START(MainLightShadows)
#endif
// Last cascade is initialized with a no-op matrix. It always transforms
// shadow coord to half3(0, 0, NEAR_PLANE). We use this trick to avoid
// branching since ComputeCascadeIndex can return cascade index = MAX_SHADOW_CASCADES
float4x4    _MainLightWorldToShadow[MAX_SHADOW_CASCADES + 1];//阴影矩阵
float4      _CascadeShadowSplitSpheres0; //包围球数据，xyz：球心  w：半径
float4      _CascadeShadowSplitSpheres1;
float4      _CascadeShadowSplitSpheres2;
float4      _CascadeShadowSplitSpheres3;
float4      _CascadeShadowSplitSphereRadii;//每个包围球半径的平方
half4       _MainLightShadowOffset0;
half4       _MainLightShadowOffset1;
half4       _MainLightShadowOffset2;
half4       _MainLightShadowOffset3;
half4       _MainLightShadowParams;  // (x: shadowStrength, y: 1.0 if soft shadows, 0.0 otherwise, z: oneOverFadeDist, w: minusStartFade)
float4      _MainLightShadowmapSize; // (xy: 1/width and 1/height, zw: width and height)
#ifndef SHADER_API_GLES3
CBUFFER_END
#endif


//108
#define BEYOND_SHADOW_FAR(shadowCoord) shadowCoord.z <= 0.0 || shadowCoord.z >= 1.0
//级联包围球数据结构
struct ShadowSamplingData
{
    half4 shadowOffset0;
    half4 shadowOffset1;
    half4 shadowOffset2;
    half4 shadowOffset3;
    float4 shadowmapSize;
};

ShadowSamplingData GetMainLightShadowSamplingData()
{
    ShadowSamplingData shadowSamplingData;
    shadowSamplingData.shadowOffset0 = _MainLightShadowOffset0;
    shadowSamplingData.shadowOffset1 = _MainLightShadowOffset1;
    shadowSamplingData.shadowOffset2 = _MainLightShadowOffset2;
    shadowSamplingData.shadowOffset3 = _MainLightShadowOffset3;
    shadowSamplingData.shadowmapSize = _MainLightShadowmapSize;
    return shadowSamplingData;
}

//141： ShadowParams
// x: ShadowStrength
// y: 1.0 if shadow is soft, 0.0 otherwise
half4 GetMainLightShadowParams()
{
    return _MainLightShadowParams;
}

//209
real SampleShadowmap(TEXTURE2D_SHADOW_PARAM(ShadowMap, sampler_ShadowMap), float4 shadowCoord, ShadowSamplingData samplingData, half4 shadowParams, bool isPerspectiveProjection = true)
{
    // Compiler will optimize this branch away as long as isPerspectiveProjection is known at compile time
    if (isPerspectiveProjection)
        shadowCoord.xyz /= shadowCoord.w;

    real attenuation;
    real shadowStrength = shadowParams.x;

//     // TODO: We could branch on if this light has soft shadows (shadowParams.y) to save perf on some platforms.
// #ifdef _SHADOWS_SOFT
//     attenuation = SampleShadowmapFiltered(TEXTURE2D_SHADOW_ARGS(ShadowMap, sampler_ShadowMap), shadowCoord, samplingData);
// #else
    // 1-tap hardware comparison
    attenuation = SAMPLE_TEXTURE2D_SHADOW(ShadowMap, sampler_ShadowMap, shadowCoord.xyz);
// #endif

    // attenuation = LerpWhiteTo(attenuation, shadowStrength);
    attenuation = LerpWhiteTo(attenuation, shadowStrength);

    // Shadow coords that fall out of the light frustum volume must always return attenuation 1.0
    // TODO: We could use branch here to save some perf on some platforms.
    return BEYOND_SHADOW_FAR(shadowCoord) ? 1.0 : attenuation;
}

//233:判断该点在哪个CascadeIndex包围球，这个算法不严格，但是毕竟会选出一个合适正确的index
half ComputeCascadeIndex(float3 positionWS)
{
    float3 fromCenter0 = positionWS - _CascadeShadowSplitSpheres0.xyz;
    float3 fromCenter1 = positionWS - _CascadeShadowSplitSpheres1.xyz;
    float3 fromCenter2 = positionWS - _CascadeShadowSplitSpheres2.xyz;
    float3 fromCenter3 = positionWS - _CascadeShadowSplitSpheres3.xyz;
    float4 distances2 = float4(dot(fromCenter0, fromCenter0), dot(fromCenter1, fromCenter1), dot(fromCenter2, fromCenter2), dot(fromCenter3, fromCenter3));
    //根据包围球的半径长度和面片到球心的长度做比较，判断该点在哪个包围球
    half4 weights = half4(distances2 < _CascadeShadowSplitSphereRadii);
    weights.yzw = saturate(weights.yzw - weights.xyz);

    return 4 - dot(weights, half4(4, 3, 2, 1));
}

//247:
float4 TransformWorldToShadowCoord(float3 positionWS)
{
#ifdef _MAIN_LIGHT_SHADOWS_CASCADE
    half cascadeIndex = ComputeCascadeIndex(positionWS);
#else
    half cascadeIndex = 0;
#endif

    float4 shadowCoord = mul(_MainLightWorldToShadow[cascadeIndex], float4(positionWS, 1.0));

    return float4(shadowCoord.xyz, cascadeIndex);
}

//260：//采样阴影图集
half MainLightRealtimeShadow(float4 shadowCoord)
{
#if !defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    return 1.0h;
#endif
    ShadowSamplingData shadowSamplingData = GetMainLightShadowSamplingData();
    half4 shadowParams = GetMainLightShadowParams();
    return SampleShadowmap(TEXTURE2D_ARGS(_MainLightShadowmapTexture, sampler_MainLightShadowmapTexture), shadowCoord, shadowSamplingData, shadowParams, false);
}

half MixRealtimeAndBakedShadows(half realtimeShadow, half bakedShadow, half shadowFade)
{
    return lerp(realtimeShadow, bakedShadow, shadowFade);
}
//突然切断阴影最大距离处的阴影会显得很突兀，我们通过一种线性淡化的方式使阴影过渡变得柔和自然一些。阴影淡化应从阴影最大距离之前的一段距离开始，直到最大距离时阴影强度为0
half GetShadowFade(float3 positionWS)
{
    float3 camToPixel = positionWS - _WorldSpaceCameraPos;
    float distanceCamToPixel2 = dot(camToPixel, camToPixel);

    half fade = saturate(distanceCamToPixel2 * _MainLightShadowParams.z + _MainLightShadowParams.w);
    return fade * fade;
}

//325:获取主光源的阴影衰减
half MainLightShadow(float4 shadowCoord, float3 positionWS)
{
    half realtimeShadow = MainLightRealtimeShadow(shadowCoord);
    half bakedShadow = 1.0h;
#ifdef MAIN_LIGHT_CALCULATE_SHADOWS
    half shadowFade = GetShadowFade(positionWS);
#else
    half shadowFade = 1.0h;
#endif
    return MixRealtimeAndBakedShadows(realtimeShadow, bakedShadow, shadowFade);
}
//370://世界坐标转到阴影相机的视图坐标下
float4 GetShadowCoord(float3 positionWS)
{
    return TransformWorldToShadowCoord(positionWS);
}
//375
float3 ApplyShadowBias(float3 positionWS, float3 normalWS, float3 lightDirection)
{
    // float invNdotL = 1.0 - saturate(dot(lightDirection, normalWS));
    // float scale = invNdotL * _ShadowBias.y;

    // // normal bias is negative since we want to apply an inset normal offset
    // positionWS = lightDirection * _ShadowBias.xxx + positionWS;
    // positionWS = normalWS * scale.xxx + positionWS;
    return positionWS;
}

#define _MainLightShadowData _MainLightShadowParams //391

#endif
