
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
float4 unity_LODFade;//106
real4 unity_WorldTransformParams; //107

// Light Indices block feature
// These are set internally by the engine upon request by RendererConfiguration.
real4 unity_LightData;
real4 unity_LightIndices[2];

// Lightmap block feature
float4 unity_LightmapST;//120:lightmap开启下，对于每一个渲染对象在lightmap中的位置，xy  缩放  zw 偏移
CBUFFER_END//133

float4x4 unity_MatrixV; //192
//定义一个从世界空间转换到裁剪空间的矩阵
float4x4 unity_MatrixVP;//195

//209： Main lightmap
TEXTURE2D(unity_Lightmap);//lightmap贴图
SAMPLER(samplerunity_Lightmap);

#endif // UNIVERSAL_SHADER_VARIABLES_INCLUDED


