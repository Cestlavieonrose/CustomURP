#ifndef UNITY_SPACE_TRANSFORMS_INCLUDED
#define UNITY_SPACE_TRANSFORMS_INCLUDED

//11
float4x4 GetObjectToWorldMatrix()
{
    return UNITY_MATRIX_M;
}
//16
float4x4 GetWorldToObjectMatrix()
{
    return UNITY_MATRIX_I_M;
}
//21
float4x4 GetWorldToViewMatrix()
{
    return UNITY_MATRIX_V;
}

//27
float4x4 GetWorldToHClipMatrix()
{
    return UNITY_MATRIX_VP;
}

//64:函数功能：顶点从模型空间转换到世界空间
float3 TransformObjectToWorld(float3 positionOS)
{
    #if defined(SHADER_STAGE_RAY_TRACING)
    return mul(ObjectToWorld3x4(), float4(positionOS, 1.0)).xyz;
    #else
    return mul(GetObjectToWorldMatrix(), float4(positionOS, 1.0)).xyz;
    #endif
}

//95:函数功能：顶点从世界空间转换到裁剪空间
float4 TransformWorldToHClip(float3 positionWS)
{
    return mul(GetWorldToHClipMatrix(), float4(positionWS, 1.0));
}

//107: Normalize to support uniform scaling
float3 TransformObjectToWorldDir(float3 dirOS, bool doNormalize = true)
{
    #ifndef SHADER_STAGE_RAY_TRACING
    float3 dirWS = mul((float3x3)GetObjectToWorldMatrix(), dirOS);
    #else
    float3 dirWS = mul((float3x3)ObjectToWorld3x4(), dirOS);
    #endif
    if (doNormalize)
        return SafeNormalize(dirWS);

    return dirWS;
}

//154: Transforms normal from object to world space
float3 TransformObjectToWorldNormal(float3 normalOS, bool doNormalize = true)
{
#ifdef UNITY_ASSUME_UNIFORM_SCALING
    return TransformObjectToWorldDir(normalOS, doNormalize);
#else
    // Normal need to be multiply by inverse transpose
    float3 normalWS = mul(normalOS, (float3x3)GetWorldToObjectMatrix());
    if (doNormalize)
        return SafeNormalize(normalWS);

    return normalWS;
#endif
}

#endif
