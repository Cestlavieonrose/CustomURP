#ifndef CUSTOM_LIT_PASS_INCLUDED
#define CUSTOM_LIT_PASS_INCLUDED
#include "Universal/ShaderLibrary/Lighting.hlsl"

//用作顶点函数的输入参数
struct Attributes
{
    float4 positionOS   : POSITION;
    float2 baseUV:TEXCOORD0;
    float2 lightmapUV   : TEXCOORD1;
    //表面法线
    float3 normalOS:NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

//用作片元函数的输入参数
struct Varyings
{
    DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 1);//声明lightmap or sh 变量，探针和lightmnap二选一
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

void InitializeInputData(Varyings input, half3 normalTS, out InputData inputData)
{
    inputData = (InputData)0;
#if defined(REQUIRES_WORLD_SPACE_POS_INTERPOLATOR)
    inputData.positionWS = input.positionWS;
#endif
    half3 viewDirWS = SafeNormalize(input.viewDirWS);

    inputData.normalWS = input.normalWS;
    inputData.normalWS = NormalizeNormalPerPixel(inputData.normalWS);

    inputData.viewDirectionWS = viewDirWS;
#if defined(MAIN_LIGHT_CALCULATE_SHADOWS) //材质开启接受，主光源开启阴影caster
    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif
    inputData.bakedGI = SAMPLE_GI(input.lightmapUV, input.vertexSH, inputData.normalWS);
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

    OUTPUT_LIGHTMAP_UV(input.lightmapUV, unity_LightmapST, output.lightmapUV);
    OUTPUT_SH(output.normalWS.xyz, output.vertexSH);
    return output;
}

//片元函数
half4 LitPassFragment(Varyings input):SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    //定义一个Surface并填充属性
    SurfaceData surface;
    InitializeStandardLitSurfaceData(input.baseUV, surface);
    // surface.normalTS = normalize(input.normalWS);
    //定义一个inputData
    InputData inputData;
    InitializeInputData(input, surface.normalTS, inputData);
    //通过表面属性计算最终光照结果
    half4 color = UniversalFragmentPBR(inputData, surface);
    color.a = OutputAlpha(color.a, 1);
    return color;
}

#endif

