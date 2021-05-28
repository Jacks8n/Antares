extern RWByteAddressBuffer Particles;

/// begin particle read/update

typedef uint2 ParticleWord;

struct ParticleProperties
{
    float3 Position;
    
    uint Flags;
};

uint GetParticleCapacity()
{
    return Particles.Load(0);
}

void SetParticleCapacity(uint capacity)
{
    Particles.Store(0, capacity);
}

ParticleWord LoadParticleWord(uint index)
{
    return Particles.Load2(index * 8 + 8);
}

void StoreParticleWord(uint index, ParticleWord word)
{
    Particles.Store2(index * 8 + 8, word.xy);
}

ParticleProperties GetParticleProperties(RWByteAddressBuffer buffer, uint index)
{
    const ParticleWord packed = LoadParticleWord(index);
    
    ParticleProperties props;
    props.Position = float3(f16tof32(packed.x >> 16),
                            f16tof32(packed.x & 0xffff),
                            f16tof32(packed.y >> 16));
    props.Flags = packed.y;
    
    return props;
}

ParticleProperties GetParticleProperties(uint index)
{
    return GetParticleProperties(Particles, index);
}

void SetParticleProperties(uint index, ParticleProperties props)
{
    const uint3 unpacked = f32tof16(props.Position);
    const uint2 packed = uint2(unpacked.x << 16 | unpacked.y, unpacked.z << 16 | props.Flags);
    
    StoreParticleWord(index, packed);
}

/// end particle read/update

/// begin particle flags

#define PARTICLE_FLAG_ALIVE 0x1
#define PARTICLE_FLAG_FLUID 0x2
#define PARTICLE_FLAG_ALL 0xffff

bool IsParticleHasFlagAny(uint flags, uint mask)
{
    return flags & mask;
}

bool IsParticleHasFlagAll(uint flags, uint mask)
{
    return flags & mask == mask;
}

/// end particle flags

/// begin particle create/remove

extern ByteAddressBuffer ParticlesToCreate;
// global counter | dispatch x | dispatch y (always 1) | dispatch z (always 1) | elements...
// this buffer is used as indirect buffer for several dispatches
extern RWByteAddressBuffer ParticlesToRemove;

uint GetCreateParticleCount()
{
    return ParticlesToCreate.Load(0) - 1;
}

uint GetRemoveParticleCount()
{
    return ParticlesToRemove.Load(0) - 1;
}

ParticleWord GetParticleToCreate(uint index)
{
    return ParticlesToCreate.Load2(index * 8 + 4);
}

uint GetParticleToRemove(uint index)
{
    return ParticlesToRemove.Load(index * 4 + 4);
}

groupshared uint ParticlesToRemoveCount_GSM = 0;
groupshared uint ParticlesToRemove_GSM[256];
groupshared uint ParticlesToRemveOffset_GSM;

void RemoveParticleDelay_GSM(uint index)
{
    uint poolSize;
    InterlockedAdd(ParticlesToRemoveCount_GSM, 1, poolSize);
    
    ParticlesToRemove_GSM[poolSize] = index;
}

// need to be used with RemoveParticleDelay_GSM
void RemoveParticleDealy(uint groupIndex, uint numthreads)
{
    if (!groupIndex)
        ParticlesToRemove.InterlockedAdd(0, ParticlesToRemoveCount_GSM, ParticlesToRemveOffset_GSM);
    
    for (uint i = groupIndex; i < ParticlesToRemoveCount_GSM; i += numthreads)
        ParticlesToRemove.Store((i + ParticlesToRemveOffset_GSM) * 4, ParticlesToRemove_GSM[i]);
}

/// end particle create/remove
