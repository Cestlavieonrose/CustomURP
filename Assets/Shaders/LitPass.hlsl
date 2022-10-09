#ifndef CUSTOM_LIT_PASS_INCLUDED
#define CUSTOM_LIT_PASS_INCLUDED
//temp define
#define REQUIRES_WORLD_SPACE_POS_INTERPOLATOR
#include "Universal/ShaderLibrary/Core.hlsl"
#include "Universal/ShaderLibrary/SurfaceData.hlsl"
#include "Universal/ShaderLibrary/Lighting.hlsl"


TEXTURE2D(_BaseMap);
SAMPLER(sampler_BaseMap);
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
UNITY_DEFINE_INSTANCED_PROP(float, _Cutoff)

UNITY_DEFINE_INSTANCED_PROP(half, _Metallic)
UNITY_DEFINE_INSTANCED_PROP(half, _Smoothness)
UNITY_INSTANCING_BUFFER_END(UnityPerMaterial)


//用作顶点函数的输入参数
struct Attributes
{
    float4 positionOS   : POSITION;
    float2 baseUV:TEXCOORD0;
    //表面法线
    float3 normalOS:NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

//用作片元函数的输入参数
struct Varyings
{
#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    float3 positionWS               : TEXCOORD2;
#endif
    float3 viewDirWS                : TEXCOORD5;

    float4 positionCS               : SV_POSITION;
    float2 baseUV:VAR_BASE_UV;
    //世界法线
    float3 normalWS:VAR_NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID

};

void InitializeInputData(Varyings input, out InputData inputData)
{
    inputData = (InputData)0;

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    inputData.positionWS = input.positionWS;
#endif

    half3 viewDirWS = SafeNormalize(input.viewDirWS);
    inputData.viewDirectionWS = viewDirWS;
}


//顶点函数
Varyings LitPassVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    output.positionCS = TransformWorldToHClip(positionWS);
    //计算世界空间的法线
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    float4 baseST = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseMap_ST);
    //计算缩放和便宜后的UV坐标
    output.baseUV = baseST.xy*input.baseUV + baseST.zw;

#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    output.positionWS = positionWS;
#endif
    half3 viewDirWS = GetWorldSpaceViewDir(positionWS);
    output.viewDirWS = viewDirWS;
    return output;
}

//片元函数
float4 LitPassFragment(Varyings input):SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);

    float4 baseMap = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.baseUV);
    float4 baseColor = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _BaseColor);
    float4 base = baseMap*baseColor;
    #if defined(_CLIPPING)
        clip(base.a - UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Cutoff));
    #endif

    //定义一个Surface并填充属性
    SurfaceData surface;
    surface.normalTS = normalize(input.normalWS);
    surface.albedo = base.rgb;
    surface.alpha = base.a;
    surface.metallic = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Metallic);
    surface.smoothness = UNITY_ACCESS_INSTANCED_PROP(UnityPerMaterial, _Smoothness);
    //定义一个inputData
    InputData inputData = (InputData)0;
    InitializeInputData(input, inputData);

    //通过表面属性计算最终光照结果
    float3 color = UniversalFragmentPBR(inputData, surface);
    return float4(color, surface.alpha);
    // return float4(1,1,1,1);
}

#endif

