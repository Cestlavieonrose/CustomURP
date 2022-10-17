#ifndef UNITY_MACROS_INCLUDED
#define UNITY_MACROS_INCLUDED

#define FLT_MIN  1.175494351e-38 //42: Minimum normalized positive floating-point number
#define HALF_MIN 6.103515625e-5  // 45:2^-14, the same value for 10, 11 and 16-bit: https://www.khronos.org/opengl/wiki/Small_Float_Formats
#define HALF_MIN_SQRT 0.0078125  // 2^-7 == sqrt(HALF_MIN), useful for ensuring HALF_MIN after x^2
#define HALF_MAX 65504.0

//100
#define TEMPLATE_2_FLT(FunctionName, Parameter1, Parameter2, FunctionBody) \
    float  FunctionName(float  Parameter1, float  Parameter2) { FunctionBody; } \
    float2 FunctionName(float2 Parameter1, float2 Parameter2) { FunctionBody; } \
    float3 FunctionName(float3 Parameter1, float3 Parameter2) { FunctionBody; } \
    float4 FunctionName(float4 Parameter1, float4 Parameter2) { FunctionBody; }

#define TEMPLATE_2_HALF(FunctionName, Parameter1, Parameter2, FunctionBody) \
    half  FunctionName(half  Parameter1, half  Parameter2) { FunctionBody; } \
    half2 FunctionName(half2 Parameter1, half2 Parameter2) { FunctionBody; } \
    half3 FunctionName(half3 Parameter1, half3 Parameter2) { FunctionBody; } \
    half4 FunctionName(half4 Parameter1, half4 Parameter2) { FunctionBody; } \
    float  FunctionName(float  Parameter1, float  Parameter2) { FunctionBody; } \
    float2 FunctionName(float2 Parameter1, float2 Parameter2) { FunctionBody; } \
    float3 FunctionName(float3 Parameter1, float3 Parameter2) { FunctionBody; } \
    float4 FunctionName(float4 Parameter1, float4 Parameter2) { FunctionBody; }

//228: MACRO from Legacy Untiy
// Transforms 2D UV by scale/bias property
#define TRANSFORM_TEX(tex, name) ((tex.xy) * name##_ST.xy + name##_ST.zw)

#endif // UNITY_MACROS_INCLUDED
