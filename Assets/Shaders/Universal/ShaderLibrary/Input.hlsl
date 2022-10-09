#ifndef UNIVERSAL_INPUT_INCLUDED
#define UNIVERSAL_INPUT_INCLUDED
//10:额外灯光最大数量限制
#if defined(SHADER_API_MOBILE) && (defined(SHADER_API_GLES) || defined(SHADER_API_GLES30))
    #define MAX_VISIBLE_LIGHTS 16
#elif defined(SHADER_API_MOBILE) || (defined(SHADER_API_GLCORE) && !defined(SHADER_API_SWITCH)) || defined(SHADER_API_GLES) || defined(SHADER_API_GLES3) // Workaround for bug on Nintendo Switch where SHADER_API_GLCORE is mistakenly defined
    #define MAX_VISIBLE_LIGHTS 32
#else
    #define MAX_VISIBLE_LIGHTS 256
#endif

//18:
struct InputData
{
    float3  positionWS;
    // half3   normalWS;
    half3   viewDirectionWS;
    // float4  shadowCoord;
    // half    fogCoord;
    // half3   vertexLighting;
    // half3   bakedGI;
    // float2  normalizedScreenSpaceUV;
    // half4   shadowMask;
};

//主光源，方向光：方向，否则传递位置
float4 _MainLightPosition;//41
half4 _MainLightColor;//42

half4 _AdditionalLightsCount;//49

#if USE_STRUCTURED_BUFFER_FOR_LIGHT_DATA
StructuredBuffer<LightData> _AdditionalLightsBuffer;
StructuredBuffer<int> _AdditionalLightsIndices;
#else
// GLES3 causes a performance regression in some devices when using CBUFFER.
#ifndef SHADER_API_GLES3
CBUFFER_START(AdditionalLights)
#endif
float4 _AdditionalLightsPosition[MAX_VISIBLE_LIGHTS];
half4 _AdditionalLightsColor[MAX_VISIBLE_LIGHTS];
half4 _AdditionalLightsAttenuation[MAX_VISIBLE_LIGHTS];
half4 _AdditionalLightsSpotDir[MAX_VISIBLE_LIGHTS];
half4 _AdditionalLightsOcclusionProbes[MAX_VISIBLE_LIGHTS];
#ifndef SHADER_API_GLES3
CBUFFER_END
#endif
#endif

#define UNITY_MATRIX_M     unity_ObjectToWorld //69
#define UNITY_MATRIX_I_M   unity_WorldToObject //70
#define UNITY_MATRIX_V     unity_MatrixV //71
#define UNITY_MATRIX_VP    unity_MatrixVP //75

#include "UnityInput.hlsl"//86
// #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityInstancing.hlsl"
#include "../../Core/ShaderLibrary/UnityInstancing.hlsl" //87
#include "../../Core/ShaderLibrary/SpaceTransforms.hlsl"//89

#endif
