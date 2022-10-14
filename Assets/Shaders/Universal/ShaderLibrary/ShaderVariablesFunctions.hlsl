#ifndef UNITY_SHADER_VARIABLES_FUNCTIONS_INCLUDED
#define UNITY_SHADER_VARIABLES_FUNCTIONS_INCLUDED

//45：判断是否是透视相机 Returns 'true' if the current view performs a perspective projection.
bool IsPerspectiveProjection()
{
    return (unity_OrthoParams.w == 0);
}

//50：相机在世界空间中的位置
float3 GetCameraPositionWS()
{
    // Currently we do not support Camera Relative Rendering so
    // we simply return the _WorldSpaceCameraPos until then
    return _WorldSpaceCameraPos;
}

//65： Could be e.g. the position of a primary camera or a shadow-casting light.
float3 GetCurrentViewPosition()
{
    // Currently we do not support Camera Relative Rendering so
    // we simply return the _WorldSpaceCameraPos until then
    return GetCameraPositionWS();
}


//91:获取相机观察view向量 Computes the world space view direction (pointing towards the viewer).
float3 GetWorldSpaceViewDir(float3 positionWS)
{
    if (IsPerspectiveProjection())
    {
        // Perspective
        return GetCurrentViewPosition() - positionWS;
    }
    else
    {
        // Orthographic 正交相机暂时不考虑
        return float3(0,0,1.0);
        //return -GetViewForwardDir();
    }
}


half OutputAlpha(half outputAlpha, half surfaceType)
{
    return surfaceType == 1 ? outputAlpha : 1.0;
}

#endif // UNITY_SHADER_VARIABLES_FUNCTIONS_INCLUDED
