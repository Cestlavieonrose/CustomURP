#ifndef UNIVERSAL_LIGHTING_INCLUDED
#define UNIVERSAL_LIGHTING_INCLUDED

#include "../../Core/ShaderLibrary/Common.hlsl"
#include "../../Core/ShaderLibrary/CommonMaterial.hlsl"
#include "Core.hlsl"
#include "SurfaceData.hlsl"
#include "Shadows.hlsl"

///////////////////////////////////////////////////////////////////////////////
//                         44: Light Helpers                                    //
///////////////////////////////////////////////////////////////////////////////

// Abstraction over Light shading data.
struct Light
{
    half3   direction;
    half3   color;
    half    shadowAttenuation; //阴影度
};

///////////////////////////////////////////////////////////////////////////////
//                   103:   Light Abstraction                                    //
///////////////////////////////////////////////////////////////////////////////

Light GetMainLight()
{
    Light light;
    light.direction = _MainLightPosition.xyz;
    light.shadowAttenuation = 1.0;
    light.color = _MainLightColor.rgb;
    return light;
}

Light GetMainLight(float4 shadowCoord, float3 positionWS)
{
    Light light = GetMainLight();
    light.shadowAttenuation = MainLightShadow(shadowCoord, positionWS);
    return light;
}

//132: Fills a light struct given a perObjectLightIndex
Light GetAdditionalPerObjectLight(int perObjectLightIndex, float3 positionWS)
{
    // unity目前并不支持SB
#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
    // float4 lightPositionWS = _AdditionalLightsBuffer[perObjectLightIndex].position;
    // half3 color = _AdditionalLightsBuffer[perObjectLightIndex].color.rgb;
    // half4 distanceAndSpotAttenuation = _AdditionalLightsBuffer[perObjectLightIndex].attenuation;
    // half4 spotDirection = _AdditionalLightsBuffer[perObjectLightIndex].spotDirection;
#else
    float4 lightPositionWS = _AdditionalLightsPosition[perObjectLightIndex];
    half3 color = _AdditionalLightsColor[perObjectLightIndex].rgb;
    // half4 distanceAndSpotAttenuation = _AdditionalLightsAttenuation[perObjectLightIndex];
    // half4 spotDirection = _AdditionalLightsSpotDir[perObjectLightIndex];
#endif

    // Directional lights store direction in lightPosition.xyz and have .w set to 0.0.
    // This way the following code will work for both directional and punctual lights.
    float3 lightVector = lightPositionWS.xyz - positionWS * lightPositionWS.w;
    float distanceSqr = max(dot(lightVector, lightVector), HALF_MIN);

    half3 lightDirection = half3(lightVector * rsqrt(distanceSqr));

    Light light;
    light.direction = lightDirection;
    light.color = color;

    return light;
}

//176: Returns a per-object index given a loop index.
// This abstract the underlying data implementation for storing lights/light indices
int GetPerObjectLightIndex(uint index)
{
/////////////////////////////////////////////////////////////////////////////////////////////
// Structured Buffer Path                                                                   /
//                                                                                          /
// Lights and light indices are stored in StructuredBuffer. We can just index them.         /
// Currently all non-mobile platforms take this path :(                                     /
// There are limitation in mobile GPUs to use SSBO (performance / no vertex shader support) /
/////////////////////////////////////////////////////////////////////////////////////////////
#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
    uint offset = unity_LightData.x;
    return _AdditionalLightsIndices[offset + index];

/////////////////////////////////////////////////////////////////////////////////////////////
// UBO path                                                                                 /
//                                                                                          /
// We store 8 light indices in float4 unity_LightIndices[2];                                /
// Due to memory alignment unity doesn't support int[] or float[]                           /
// Even trying to reinterpret cast the unity_LightIndices to float[] won't work             /
// it will cast to float4[] and create extra register pressure. :(                          /
/////////////////////////////////////////////////////////////////////////////////////////////
#elif !defined(SHADER_API_GLES)
    // since index is uint shader compiler will implement
    // div & mod as bitfield ops (shift and mask).

    // TODO: Can we index a float4? Currently compiler is
    // replacing unity_LightIndicesX[i] with a dp4 with identity matrix.
    // u_xlat16_40 = dot(unity_LightIndices[int(u_xlatu13)], ImmCB_0_0_0[u_xlati1]);
    // This increases both arithmetic and register pressure.
    return unity_LightIndices[index / 4][index % 4];
#else
    // Fallback to GLES2. No bitfield magic here :(.
    // We limit to 4 indices per object and only sample unity_4LightIndices0.
    // Conditional moves are branch free even on mali-400
    // small arithmetic cost but no extra register pressure from ImmCB_0_0_0 matrix.
    half2 lightIndex2 = (index < 2.0h) ? unity_LightIndices[0].xy : unity_LightIndices[0].zw;
    half i_rem = (index < 2.0h) ? index : index - 2.0h;
    return (i_rem < 1.0h) ? lightIndex2.x : lightIndex2.y;
#endif
}

//219: Fills a light struct given a loop i index. This will convert the i
// index to a perObjectLightIndex
Light GetAdditionalLight(uint i, float3 positionWS)
{
    int perObjectLightIndex = GetPerObjectLightIndex(i);
    return GetAdditionalPerObjectLight(perObjectLightIndex, positionWS);
}

//240:获取副光源数量
int GetAdditionalLightsCount()
{
    // TODO: we need to expose in SRP api an ability for the pipeline cap the amount of lights
    // in the culling. This way we could do the loop branch with an uniform
    // This would be helpful to support baking exceeding lights in SH as well
    return min(_AdditionalLightsCount.x, unity_LightData.y);
}

///////////////////////////////////////////////////////////////////////////////
//                         248:BRDF Functions                                    //
///////////////////////////////////////////////////////////////////////////////

#define kDielectricSpec half4(0.04, 0.04, 0.04, 1.0 - 0.04) // standard dielectric reflectivity coef at incident angle (= 4%)

struct BRDFData
{
    half3 diffuse;
    half3 specular;
    half roughness;
    half roughness2;

    // We save some light invariant BRDF terms so we don't have to recompute
    // them in the light loop. Take a look at DirectBRDF function for detailed explaination.
    half normalizationTerm;     // roughness * 4.0 + 2.0
    half roughness2MinusOne;    // roughness^2 - 1.0
};

//279:
half OneMinusReflectivityMetallic(half metallic)
{
    // We'll need oneMinusReflectivity, so
    //   1-reflectivity = 1-lerp(dielectricSpec, 1, metallic) = lerp(1-dielectricSpec, 0, metallic)
    // store (1-dielectricSpec) in kDielectricSpec.a, then
    //   1-reflectivity = lerp(alpha, 0, metallic) = alpha + metallic*(0 - alpha) =
    //                  = alpha - metallic * alpha
    //2. 实际上一些电介质（通常不导电物质），如玻璃、塑料等非金属物体，还会有一点光从表面反射出来，平均约为0.04，这给了它们亮点。
    //它将作为我们的最小反射率，添加一个OneMinusReflectivity方法计算不反射的值，将范围从 0-1 调整到 0-0.96，保持和URP中一样。
    half oneMinusDielectricSpec = kDielectricSpec.a;
    return oneMinusDielectricSpec - metallic * oneMinusDielectricSpec;
}

inline void InitializeBRDFDataDirect(half3 diffuse, half3 specular, half reflectivity, half oneMinusReflectivity, half smoothness, inout half alpha, out BRDFData outBRDFData)
{
    outBRDFData.diffuse = diffuse;
    outBRDFData.specular = specular;
    // outBRDFData.reflectivity = reflectivity;
    //粗糙度和光滑度相反，只需要使用1减去光滑度即可。
    //我们使用源码库中CommonMaterial.hlsl的PerceptualSmoothnessToPerceptualRoughness方法，
    //通过感知到的光滑度得到粗糙度，然后通过PerceptualRoughnessToRoughness方法将感知到的粗糙度平方，
    //得到实际的粗糙度，这与迪士尼光照模型匹配。
    outBRDFData.roughness           = max(PerceptualSmoothnessToRoughness(smoothness), HALF_MIN_SQRT);
    outBRDFData.roughness2          = max(outBRDFData.roughness * outBRDFData.roughness, HALF_MIN);
    outBRDFData.normalizationTerm   = outBRDFData.roughness * 4.0h + 2.0h;
    outBRDFData.roughness2MinusOne  = outBRDFData.roughness2 - 1.0h;

#ifdef _ALPHAPREMULTIPLY_ON
    outBRDFData.diffuse *= alpha;
    alpha = alpha * oneMinusReflectivity + reflectivity; // NOTE: alpha modified and propagated up.
#endif
}

//309:初始化BRDF结构数据
inline void InitializeBRDFData(half3 albedo, half metallic, half smoothness, inout half alpha, out BRDFData outBRDFData)
{
    //1. 当使用金属工作流时，物体表面对光线的反射率（Reflectivity）会受到Metallic（金属度）的影响，
    //物体的Metallic越大，其自身反照率（Albedo）颜色越不明显，对周围环境景象的反射就越清晰，
    //达到最大时就完全反射显示了周围的环境景象。
    //我们调整BRDF的GetBRDF方法，用1减去金属度得到的不反射的值，然后跟表面颜色相乘得到BRDF的漫反射部分
    half oneMinusReflectivity = OneMinusReflectivityMetallic(metallic);
    half reflectivity = 1.0 - oneMinusReflectivity;
    half3 brdfDiffuse = albedo * oneMinusReflectivity;
    //但这忽略了一个事实，即金属影响镜面反射的颜色，而非金属不影响。
    //非金属的镜面反射应该是白色的，最后我们通过金属度在最小反射率和表面颜色之间进行插值得到BRDF的镜面反射颜色。
    half3 brdfSpecular = lerp(kDielectricSpec.rgb, albedo, metallic);

    InitializeBRDFDataDirect(brdfDiffuse, brdfSpecular, reflectivity, oneMinusReflectivity, smoothness, alpha, outBRDFData);
}


//temp:计算入射光照
half3 IncomingLight(SurfaceData surface, Light light)
{
    return saturate(dot(surface.normalTS, light.direction))*light.color;
}

//temp:获取最终照明结果
half3 GetLighting(SurfaceData surface, BRDFData outBRDFData, Light light)
{
    return IncomingLight(surface, light) * outBRDFData.diffuse;
}

//393：根据公式得到镜面反射强度 Computes the scalar specular term for Minimalist CookTorrance BRDF
// NOTE: needs to be multiplied with reflectance f0, i.e. specular color to complete
half DirectBRDFSpecular(BRDFData brdfData, half3 normalWS, half3 lightDirectionWS, half3 viewDirectionWS)
{
    float3 halfDir = SafeNormalize(float3(lightDirectionWS) + float3(viewDirectionWS));

    float NoH = saturate(dot(normalWS, halfDir));
    half LoH = saturate(dot(lightDirectionWS, halfDir));

    // GGX Distribution multiplied by combined approximation of Visibility and Fresnel
    // BRDFspec = (D * V * F) / 4.0
    // D = roughness^2 / ( NoH^2 * (roughness^2 - 1) + 1 )^2
    // V * F = 1.0 / ( LoH^2 * (roughness + 0.5) )
    // See "Optimizing PBR for Mobile" from Siggraph 2015 moving mobile graphics course
    // https://community.arm.com/events/1155

    // Final BRDFspec = roughness^2 / ( NoH^2 * (roughness^2 - 1) + 1 )^2 * (LoH^2 * (roughness + 0.5) * 4.0)
    // We further optimize a few light invariant terms
    // brdfData.normalizationTerm = (roughness + 0.5) * 4.0 rewritten as roughness * 4.0 + 2.0 to a fit a MAD.
    float d = NoH * NoH * brdfData.roughness2MinusOne + 1.00001f;

    half LoH2 = LoH * LoH;
    half specularTerm = brdfData.roughness2 / ((d * d) * max(0.1h, LoH2) * brdfData.normalizationTerm);

    // On platforms where half actually means something, the denominator has a risk of overflow
    // clamp below was added specifically to "fix" that, but dx compiler (we convert bytecode to metal/gles)
    // sees that specularTerm have only non-negative terms, so it skips max(0,..) in clamp (leaving only min(100,...))
#if defined (SHADER_API_MOBILE) || defined (SHADER_API_SWITCH)
    specularTerm = specularTerm - HALF_MIN;
    specularTerm = clamp(specularTerm, 0.0, 100.0); // Prevent FP16 overflow on mobiles
#endif

return specularTerm;
}

//702：pbr光照计算
half3 LightingPhysicallyBased(BRDFData brdfData, 
    half3 lightColor, half3 lightDirectionWS, half lightAttenuation,
    half3 normalWS, half3 viewDirectionWS,
    bool specularHighlightsOff)
{
    half NdotL = saturate(dot(normalWS, lightDirectionWS));
    half3 radiance = lightColor * (lightAttenuation * NdotL);

    half3 brdf = brdfData.diffuse;
#ifndef _SPECULARHIGHLIGHTS_OFF
    [branch] if (!specularHighlightsOff)
    {
        brdf += brdfData.specular * DirectBRDFSpecular(brdfData, normalWS, lightDirectionWS, viewDirectionWS);
    }
#endif // _SPECULARHIGHLIGHTS_OFF

    return brdf * radiance;
}
//737：pbr光照计算
half3 LightingPhysicallyBased(BRDFData brdfData, Light light, half3 normalWS, half3 viewDirectionWS, bool specularHighlightsOff)
{
    return LightingPhysicallyBased(brdfData, light.color, light.direction, light.shadowAttenuation, normalWS, viewDirectionWS, specularHighlightsOff);
}


///////////////////////////////////////////////////////////////////////////////
//                 797:     Fragment Functions                                   //
//       Used by ShaderGraph and others builtin renderers                    //
///////////////////////////////////////////////////////////////////////////////
//根据物体的表面信息获取最终的光照结果
half4 UniversalFragmentPBR(InputData inputData, SurfaceData surfaceData)
{
#ifdef _SPECULARHIGHLIGHTS_OFF
    bool specularHighlightsOff = true;
#else
    bool specularHighlightsOff = false;
#endif

    BRDFData brdfData;
    // NOTE: can modify alpha
    InitializeBRDFData(surfaceData.albedo, surfaceData.metallic, surfaceData.smoothness, surfaceData.alpha, brdfData);

    Light mainLight = GetMainLight(inputData.shadowCoord, inputData.positionWS);
    // half3 color = GetLighting(surfaceData, mainLight);
    half3 color = LightingPhysicallyBased(brdfData, mainLight, surfaceData.normalTS, inputData.viewDirectionWS, specularHighlightsOff);

#ifdef _ADDITIONAL_LIGHTS
    uint pixelLightCount = GetAdditionalLightsCount();
    for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
    {
        Light light = GetAdditionalLight(lightIndex, inputData.positionWS);
        color += GetLighting(surfaceData, brdfData, light);
    }
#endif
    return half4(color, surfaceData.alpha);
}
#endif
