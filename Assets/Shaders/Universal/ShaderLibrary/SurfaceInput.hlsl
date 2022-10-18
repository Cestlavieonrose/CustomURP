#ifndef UNIVERSAL_INPUT_SURFACE_INCLUDED
#define UNIVERSAL_INPUT_SURFACE_INCLUDED

#include "Core.hlsl"
#include "SurfaceData.hlsl"
#include "../../Core/ShaderLibrary/CommonMaterial.hlsl"

TEXTURE2D(_BaseMap);            SAMPLER(sampler_BaseMap);
// TEXTURE2D(_BumpMap);            SAMPLER(sampler_BumpMap);
TEXTURE2D(_EmissionMap);        SAMPLER(sampler_EmissionMap);

///////////////////////////////////////////////////////////////////////////////
//                      Material Property Helpers                            //
///////////////////////////////////////////////////////////////////////////////
half Alpha(half albedoAlpha, half4 color, half cutoff)
{
    half alpha = albedoAlpha * color.a;

#if defined(_ALPHATEST_ON)
    clip(alpha - cutoff);
#endif

    return alpha;
}

half4 SampleAlbedoAlpha(float2 uv, TEXTURE2D_PARAM(albedoAlphaMap, sampler_albedoAlphaMap))
{
    return SAMPLE_TEXTURE2D(albedoAlphaMap, sampler_albedoAlphaMap, uv);
}

// half3 SampleNormal(float2 uv, TEXTURE2D_PARAM(bumpMap, sampler_bumpMap), half scale = 1.0h)
half3 SampleNormal()
{
// #ifdef _NORMALMAP
//     half4 n = SAMPLE_TEXTURE2D(bumpMap, sampler_bumpMap, uv);
//     #if BUMP_SCALE_NOT_SUPPORTED
//         return UnpackNormal(n);
//     #else
//         return UnpackNormalScale(n, scale);
//     #endif
// #else
    return half3(0.0h, 0.0h, 1.0h);
// #endif
}

half3 SampleEmission(float2 uv, half3 emissionColor, TEXTURE2D_PARAM(emissionMap, sampler_emissionMap))
{
#ifndef _EMISSION
    return 0;
#else
    return SAMPLE_TEXTURE2D(emissionMap, sampler_emissionMap, uv).rgb * emissionColor;
#endif
}

#endif
