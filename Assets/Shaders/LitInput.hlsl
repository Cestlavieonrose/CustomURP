#ifndef CUSTOM_LIT_INPUT_INCLUDED
#define CUSTOM_LIT_INPUT_INCLUDED

#include "Universal/ShaderLibrary/Core.hlsl"
#include "Core/ShaderLibrary/CommonMaterial.hlsl"
#include "Universal/ShaderLibrary/SurfaceInput.hlsl"

//所有材质的属性我们需要在常量区缓冲区里定义
//并非所有的平台都支持常量缓冲区，可以使用CBUFFER_START/CBUFFER_END带代替cbuffer，这样补支持常量的缓冲区平台就会忽略掉这个cbuff
// CBUFFER_START(UnityPerMaterial)
//     float4 _BaseColor;
// CBUFFER_END

//目前我们还不支持每个物体实例的材质数据，且SRP Batcher优先级比较高，我们还不能得到想要的结果。
//首先我们需要使用一个数组引用替换_BaseColor，
//并使用UNITY_INSTANCING_BUFFER_START和UNITY_INSTANCING_BUFFER_END替换CBUFFER_START和CBUFFER_END。
UNITY_INSTANCING_BUFFER_START(UnityPerMaterial)
//提供纹理的缩放和平移
UNITY_DEFINE_INSTANCED_PROP(float4, _BaseMap_ST)
UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
UNITY_DEFINE_INSTANCED_PROP(half4, _EmissionColor)
UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)

UNITY_DEFINE_INSTANCED_PROP(half, _Metallic)
UNITY_DEFINE_INSTANCED_PROP(half, _Smoothness)
UNITY_DEFINE_INSTANCED_PROP(half, _Surface)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)

//79
// #ifdef _SPECULAR_SETUP
//     #define SAMPLE_METALLICSPECULAR(uv) SAMPLE_TEXTURE2D(_SpecGlossMap, sampler_SpecGlossMap, uv)
// #else
//     #define SAMPLE_METALLICSPECULAR(uv) SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv)
// #endif
//85
half4 SampleMetallicSpecGloss(float2 uv, half albedoAlpha)
{
    half4 specGloss;

// #ifdef _METALLICSPECGLOSSMAP
//     // specGloss = SAMPLE_METALLICSPECULAR(uv);
//     // #ifdef _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
//     //     specGloss.a = albedoAlpha * _Smoothness;
//     // #else
//     //     specGloss.a *= _Smoothness;
//     // #endif
// #else // _METALLICSPECGLOSSMAP
//     #if _SPECULAR_SETUP
//         specGloss.rgb = _SpecColor.rgb;
//     #else
        specGloss.rgb = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Metallic).rrr;
    // #endif

    // #ifdef _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
    //     specGloss.a = albedoAlpha * _Smoothness;
    // #else
        specGloss.a = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Smoothness);
    // #endif
// #endif

    return specGloss;
}

//113:
half SampleOcclusion(float2 uv)
{
// #ifdef _OCCLUSIONMAP
// // TODO: Controls things like these by exposing SHADER_QUALITY levels (low, medium, high)
// #if defined(SHADER_API_GLES)
//     return SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).g;
// #else
//     half occ = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, uv).g;
//     return LerpWhiteTo(occ, _OcclusionStrength);
// #endif
// #else
    return 1.0;
// #endif
}

//205:
inline void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData outSurfaceData)
{
    half4 albedoAlpha = SampleAlbedoAlpha(uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap));
    outSurfaceData.alpha = Alpha(albedoAlpha.a, UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor), UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff));

    half4 specGloss = SampleMetallicSpecGloss(uv, albedoAlpha.a);
    outSurfaceData.albedo = albedoAlpha.rgb * _BaseColor.rgb;

// #if _SPECULAR_SETUP
//     outSurfaceData.metallic = 1.0h;
//     outSurfaceData.specular = specGloss.rgb;
// #else
    outSurfaceData.metallic = specGloss.r;
    // outSurfaceData.specular = half3(0.0h, 0.0h, 0.0h);
// #endif

    outSurfaceData.smoothness = specGloss.a;
    outSurfaceData.normalTS = SampleNormal();//SampleNormal(uv, TEXTURE2D_ARGS(_BumpMap, sampler_BumpMap), _BumpScale);
    outSurfaceData.occlusion = SampleOcclusion(uv);
    outSurfaceData.emission = SampleEmission(uv, _EmissionColor.rgb, TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap));

    // outSurfaceData.clearCoatMask       = 0.0h;
    // outSurfaceData.clearCoatSmoothness = 0.0h;

// #if defined(_DETAIL)
//     half detailMask = SAMPLE_TEXTURE2D(_DetailMask, sampler_DetailMask, uv).a;
//     float2 detailUv = uv * _DetailAlbedoMap_ST.xy + _DetailAlbedoMap_ST.zw;
//     outSurfaceData.albedo = ApplyDetailAlbedo(detailUv, outSurfaceData.albedo, detailMask);
//     outSurfaceData.normalTS = ApplyDetailNormal(detailUv, outSurfaceData.normalTS, detailMask);

// #endif
}

#endif

