#ifndef UNITY_COMMON_MATERIAL_INCLUDED
#define UNITY_COMMON_MATERIAL_INCLUDED

//-----------------------------------------------------------------------------
// 22:Helper functions for roughness
//-----------------------------------------------------------------------------

real PerceptualRoughnessToRoughness(real perceptualRoughness)
{
    return perceptualRoughness * perceptualRoughness;
}

real RoughnessToPerceptualRoughness(real roughness)
{
    return sqrt(roughness);
}

real RoughnessToPerceptualSmoothness(real roughness)
{
    return 1.0 - sqrt(roughness);
}

real PerceptualSmoothnessToRoughness(real perceptualSmoothness)
{
    return (1.0 - perceptualSmoothness) * (1.0 - perceptualSmoothness);
}

real PerceptualSmoothnessToPerceptualRoughness(real perceptualSmoothness)
{
    return (1.0 - perceptualSmoothness);
}

#endif // UNITY_COMMON_MATERIAL_INCLUDED
