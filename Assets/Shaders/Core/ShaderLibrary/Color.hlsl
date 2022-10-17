#ifndef UNITY_COLOR_INCLUDED
#define UNITY_COLOR_INCLUDED

real3 LinearToSRGB(real3 c)
{
    real3 sRGBLo = c * 12.92;
    real3 sRGBHi = (PositivePow(c, real3(1.0/2.4, 1.0/2.4, 1.0/2.4)) * 1.055) - 0.055;
    real3 sRGB   = (c <= 0.0031308) ? sRGBLo : sRGBHi;
    return sRGB;
}
#endif // UNITY_COLOR_INCLUDED
