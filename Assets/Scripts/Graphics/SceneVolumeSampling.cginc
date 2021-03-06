// two macros can be defined before including. once they're defined,
// related arguments can be omitted during invocation
// SCENE_VOLUME_TEXEL
// SCENE_VOLUME_SAMPLER

extern Texture3D<snorm float> SceneVolume;

float SampleNormalizedSDF(SamplerState state, float3 uvw, float mip)
{
    return SceneVolume.SampleLevel(state, uvw, mip);
}

float3 SampleNormal(SamplerState state, float3 texel, float3 uvw, float mip)
{
    const float2 offset = float2(1.0, -1.0);
    return normalize(SampleNormalizedSDF(state, uvw + texel, mip) * offset.xxx +
                     SampleNormalizedSDF(state, uvw + offset.yyx * texel, mip) * offset.yyx +
                     SampleNormalizedSDF(state, uvw + offset.xyy * texel, mip) * offset.xyy +
                     SampleNormalizedSDF(state, uvw + offset.yxy * texel, mip) * offset.yxy);
}

#ifdef SCENE_VOLUME_TEXEL
float3 SampleNormal(SamplerState state, float3 uvw, float mip)
{
    const float2 offset = float2(1.0, -1.0);
    return normalize(SampleNormalizedSDF(state, uvw + texel, mip) * offset.xxx +
                     SampleNormalizedSDF(state, uvw + offset.yyx * texel, mip) * offset.yyx +
                     SampleNormalizedSDF(state, uvw + offset.xyy * texel, mip) * offset.xyy +
                     SampleNormalizedSDF(state, uvw + offset.yxy * texel, mip) * offset.yxy);
}

float SampleNormalizedSDFLocal(SamplerState state, float3 localPos, float mip)
{
    const float3 uvw = localPos * SCENE_VOLUME_TEXEL;
    return SceneVolume.SampleLevel(state, uvw, mip);
}

float3 SampleNormalLocal(SamplerState state, float3 localPos, float mip)
{
    const float2 offset = float2(1.0, -1.0);
    return normalize(SampleNormalizedSDFLocal(state, localPos + offset.xxx, mip) * offset.xxx +
                     SampleNormalizedSDFLocal(state, localPos + offset.yyx, mip) * offset.yyx +
                     SampleNormalizedSDFLocal(state, localPos + offset.xyy, mip) * offset.xyy +
                     SampleNormalizedSDFLocal(state, localPos + offset.yxy, mip) * offset.yxy);
}
#endif

#ifdef SCENE_VOLUME_SAMPLER
float SampleNormalizedSDF(float3 uvw, float mip)
{
    return SceneVolume.SampleLevel(SCENE_VOLUME_SAMPLER, uvw, mip);
}

float3 SampleNormal(float3 texel, float3 uvw, float mip)
{
    const float2 offset = float2(1.0, -1.0);
    return normalize(SampleNormalizedSDF(uvw + texel, mip) * offset.xxx +
                     SampleNormalizedSDF(uvw + offset.yyx * texel, mip) * offset.yyx +
                     SampleNormalizedSDF(uvw + offset.xyy * texel, mip) * offset.xyy +
                     SampleNormalizedSDF(uvw + offset.yxy * texel, mip) * offset.yxy);
}
#endif

#if defined(SCENE_VOLUME_TEXEL) && defined(SCENE_VOLUME_SAMPLER)
float3 SampleNormal(float3 uvw, float mip)
{
    const float2 offset = float2(1.0, -1.0);
    return normalize(SampleNormalizedSDF(state, uvw + texel, mip) * offset.xxx +
                     SampleNormalizedSDF(state, uvw + offset.yyx * texel, mip) * offset.yyx +
                     SampleNormalizedSDF(state, uvw + offset.xyy * texel, mip) * offset.xyy +
                     SampleNormalizedSDF(state, uvw + offset.yxy * texel, mip) * offset.yxy);
}

float SampleNormalizedSDFLocal(float3 localPos, float mip)
{
    const float3 uvw = localPos * SCENE_VOLUME_TEXEL;
    return SceneVolume.SampleLevel(SCENE_VOLUME_SAMPLER, uvw, mip);
}

float3 SampleNormalLocal(float3 localPos, float mip)
{
    const float2 offset = float2(1.0, -1.0);
    return normalize(SampleNormalizedSDF(localPos + offset.xxx, mip) * offset.xxx +
                     SampleNormalizedSDF(localPos + offset.yyx, mip) * offset.yyx +
                     SampleNormalizedSDF(localPos + offset.xyy, mip) * offset.xyy +
                     SampleNormalizedSDF(localPos + offset.yxy, mip) * offset.yxy);
}
#endif
