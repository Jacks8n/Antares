#pragma once

// commutative, associative
float SmoothMin(float a, float b)
{
    const float k = -16.0;
    const float invk = 1.0 / k;
    return invk * log2(exp2(k * a) + exp2(k * b)) - invk;
}
