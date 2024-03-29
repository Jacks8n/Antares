// a variant of pcg32 is implemented

#pragma once

struct Random
{
    uint State;
    uint Increment;

    // `seq` selects the output stream, only the low 31 bits are sinificant
    // the outputs of different streams are not likely to coincide
    void Seed(uint state, uint seq)
    {
        State = 0;
        Increment = (seq << 1) | 1;

        Step();
        State += state;
        Step();
    }

    void Seed(uint state)
    {
        Seed(state, 0);
    }

    void Step()
    {
        State = State * 747796405u + Increment;
    }

    uint Next()
    {
        const uint state = State;
        Step();

        uint word = ((state >> ((state >> 28) + 4u)) ^ state) * 277803737u;
        return (word >> 22) ^ word;
    }

    uint2 Next2()
    {
        return uint2(Next(), Next());
    }

    uint3 Next3()
    {
        return uint3(Next2(), Next());
    }

    uint4 Next4()
    {
        return uint4(Next3(), Next());
    }

    float ToFloat01(uint value)
    {
        const uint2 word = uint2(value >> 16, value & 16);
        const float2 wordf = word * float2(0.0000152587890625, 0.00000000023283064365386962890625);
        return wordf.x + wordf.y;
    }

    float Next01()
    {
        return ToFloat01(Next());
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
