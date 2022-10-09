#ifndef UNIVERSAL_SURFACE_DATA_INCLUDED
#define UNIVERSAL_SURFACE_DATA_INCLUDED

// Must match Universal ShaderGraph master node
struct SurfaceData
{
    half3 albedo;
    half  metallic;
    half  smoothness;
    half3 normalTS;
    half  alpha;
};

#endif
