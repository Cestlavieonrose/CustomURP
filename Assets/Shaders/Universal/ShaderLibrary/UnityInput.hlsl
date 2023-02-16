
//unity标准输入库
// UNITY_SHADER_NO_UPGRADE
#ifndef UNIVERSAL_SHADER_VARIABLES_INCLUDED
#define UNIVERSAL_SHADER_VARIABLES_INCLUDED

//46
#if !defined(USING_STEREO_MATRICES)
float3 _WorldSpaceCameraPos;
#endif

//74: x = orthographic camera's width
// y = orthographic camera's height
// z = unused
// w = 1.0 if camera is ortho, 0.0 if perspective
float4 unity_OrthoParams;

//UnityPerDraw里的值每组都是特定的 必须一起给出，否则srp编译会通不过
CBUFFER_START(UnityPerDraw) //102
//定义一个从模型空间转换到世界空间的转换矩阵
float4x4 unity_ObjectToWorld;//104
float4x4 unity_WorldToObject;//105
float4 unity_LODFade;//106 x is the fade value ranging within [0,1]. y is x quantized into 16 levels
real4 unity_WorldTransformParams; //107

// Light Indices block feature
// These are set internally by the engine upon request by RendererConfiguration.
real4 unity_LightData;
real4 unity_LightIndices[2];

float4 unity_ProbesOcclusion; //shadowmask不但会烘焙到贴图中，同时也会烘焙到探针中，动态物体就通过这个探针获取阴影遮蔽

// Reflection Probe 0 block feature
// HDR environment map decode instructions
real4 unity_SpecCube0_HDR;

// Lightmap block feature
float4 unity_LightmapST;//120:lightmap开启下，对于每一个渲染对象在lightmap中的位置，xy  缩放  zw 偏移
float4 unity_LightmapIndex;
float4 unity_DynamicLightmapST;

// 125：SH block feature 采样光照探针的时候使用
real4 unity_SHAr;
real4 unity_SHAg;
real4 unity_SHAb;
real4 unity_SHBr;
real4 unity_SHBg;
real4 unity_SHBb;
real4 unity_SHC;
CBUFFER_END//133

float4x4 unity_MatrixV; //192
//定义一个从世界空间转换到裁剪空间的矩阵
float4x4 unity_MatrixVP;//195

// Unity specific 
//镜面反射反映了环境，默认情况下是天空盒，它是一个立方体纹理（Cube Map），这里声明该纹理和对应采样器。
TEXTURECUBE(unity_SpecCube0);
SAMPLER(samplerunity_SpecCube0);

//209： Main lightmap
TEXTURE2D(unity_Lightmap);//lightmap贴图
SAMPLER(samplerunity_Lightmap);

//219:shadowmask贴图
TEXTURE2D(unity_ShadowMask);
SAMPLER(samplerunity_ShadowMask);

#endif // UNIVERSAL_SHADER_VARIABLES_INCLUDED


