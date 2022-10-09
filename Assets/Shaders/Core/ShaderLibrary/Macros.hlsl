#ifndef UNITY_MACROS_INCLUDED
#define UNITY_MACROS_INCLUDED

#define FLT_MIN  1.175494351e-38 //42: Minimum normalized positive floating-point number
#define HALF_MIN 6.103515625e-5  // 45:2^-14, the same value for 10, 11 and 16-bit: https://www.khronos.org/opengl/wiki/Small_Float_Formats
#define HALF_MIN_SQRT 0.0078125  // 2^-7 == sqrt(HALF_MIN), useful for ensuring HALF_MIN after x^2

#endif // UNITY_MACROS_INCLUDED
