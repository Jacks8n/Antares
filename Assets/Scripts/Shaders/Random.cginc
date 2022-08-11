#pragma once

struct Random
{
    uint Status;
    uint Increment;

    void Seed(uint seed)
    {
    }

    uint Next()
    {
        return 0;
    }

    float Next01()
    {
        return Next * (1.0 / (1u << 31));
    }

    float2 Next01x2()
    {
        return float2(Next01(), Next01());
    }

    float3 Next01x3()
    {
        return float3(Next01x2(), Next01());
    }

    float4 Next01x4()
    {
        return float4(Next01x3(), Next01());
    }
};
