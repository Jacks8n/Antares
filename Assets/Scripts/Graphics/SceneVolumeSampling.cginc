#pragma once

// two macros can be defined before including. once they're defined,
// related arguments can be omitted during invocation
// SCENE_VOLUME_TEXEL : float3
// SCENE_VOLUME_SAMPLER : SamplerState

extern Texture3D<snorm float> SceneVolume;

float3 SafeNormalize(float3 v)
{
    const float vv = dot(v, v);
    return vv > 0.0001 ? v * rsqrt(vv) : float3(0.0, 0.0, 0.0);
}

float SampleNormalizedSDF(SamplerState state, float3 uvw, float mip)
{
    return SceneVolume.SampleLevel(state, uvw, mip);
}

float3 SampleNormal(SamplerState state, float3 texel, float3 uvw, float mip)
{
    const float2 offset = float2(0.5, -0.5);
    return SafeNormalize(SampleNormalizedSDF(state, uvw + texel, mip) * offset.xxx +
                         SampleNormalizedSDF(state, uvw + offset.yyx * texel, mip) * offset.yyx +
                         SampleNormalizedSDF(state, uvw + offset.xyy * texel, mip) * offset.xyy +
                         SampleNormalizedSDF(state, uvw + offset.yxy * texel, mip) * offset.yxy);
}

#ifdef SCENE_VOLUME_TEXEL

float3 SampleNormal(SamplerState state, float3 uvw, float mip)
{
    const float2 offset = float2(0.5, -0.5);
    return SafeNormalize(SampleNormalizedSDF(state, uvw + SCENE_VOLUME_TEXEL, mip) * offset.xxx +
                         SampleNormalizedSDF(state, uvw + offset.yyx * SCENE_VOLUME_TEXEL, mip) * offset.yyx +
                         SampleNormalizedSDF(state, uvw + offset.xyy * SCENE_VOLUME_TEXEL, mip) * offset.xyy +
                         SampleNormalizedSDF(state, uvw + offset.yxy * SCENE_VOLUME_TEXEL, mip) * offset.yxy);
}

float SampleNormalizedSDFLocal(SamplerState state, float3 localPos, float mip)
{
    const float3 uvw = localPos * SCENE_VOLUME_TEXEL;
    return SceneVolume.SampleLevel(state, uvw, mip);
}

float3 SampleNormalLocal(SamplerState state, float3 localPos, float mip)
{
    const float3 uvw = localPos * SCENE_VOLUME_TEXEL;
    return SampleNormal(state, uvw, mip);
}

#endif

#ifdef SCENE_VOLUME_SAMPLER

float SampleNormalizedSDF(float3 uvw, float mip)
{
    return SceneVolume.SampleLevel(SCENE_VOLUME_SAMPLER, uvw, mip);
}

float3 SampleNormal(float3 texel, float3 uvw, float mip)
{
    const float2 offset = float2(0.5, -0.5);
    return SafeNormalize(SampleNormalizedSDF(uvw + texel, mip) * offset.xxx +
                         SampleNormalizedSDF(uvw + offset.yyx * texel, mip) * offset.yyx +
                         SampleNormalizedSDF(uvw + offset.xyy * texel, mip) * offset.xyy +
                         SampleNormalizedSDF(uvw + offset.yxy * texel, mip) * offset.yxy);
}

#endif

#if defined(SCENE_VOLUME_TEXEL) && defined(SCENE_VOLUME_SAMPLER)

float SampleNormalizedSDFLocal(float3 localPos, float mip)
{
    const float3 uvw = localPos * SCENE_VOLUME_TEXEL;
    return SceneVolume.SampleLevel(SCENE_VOLUME_SAMPLER, uvw, mip);
}

float3 SampleNormal(float3 uvw, float mip)
{
    const float2 offset = float2(0.5, -0.5);
    return SafeNormalize(SampleNormalizedSDF(SCENE_VOLUME_SAMPLER, uvw + SCENE_VOLUME_TEXEL, mip) * offset.xxx +
                         SampleNormalizedSDF(SCENE_VOLUME_SAMPLER, uvw + offset.yyx * SCENE_VOLUME_TEXEL, mip) * offset.yyx +
                         SampleNormalizedSDF(SCENE_VOLUME_SAMPLER, uvw + offset.xyy * SCENE_VOLUME_TEXEL, mip) * offset.xyy +
                         SampleNormalizedSDF(SCENE_VOLUME_SAMPLER, uvw + offset.yxy * SCENE_VOLUME_TEXEL, mip) * offset.yxy);
}

float3 SampleNormalLocal(float3 localPos, float mip)
{
    const float3 uvw = localPos * SCENE_VOLUME_TEXEL;
    return SampleNormal(uvw, mip);
}

#endif
