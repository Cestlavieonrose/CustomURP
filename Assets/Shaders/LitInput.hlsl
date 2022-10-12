#ifndef CUSTOM_LIT_INPUT_INCLUDED
#define CUSTOM_LIT_INPUT_INCLUDED

#include "Universal/ShaderLibrary/Core.hlsl"
#include "Core/ShaderLibrary/CommonMaterial.hlsl"
#include "Universal/ShaderLibrary/SurfaceData.hlsl"

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

TEXTURE2D(_BaseMap);                    SAMPLER(sampler_BaseMap);

#endif

