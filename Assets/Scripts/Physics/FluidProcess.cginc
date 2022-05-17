extern RWByteAddressBuffer Particles;

/// begin particle read/update

typedef uint2 ParticleWord;

struct ParticleProperties
{
    float3 Position;
    
    uint Flags;
    
    bool IsParticleHasFlagAny(uint mask)
    {
        return Flags & mask;
    }

    bool IsParticleHasFlagAll(uint mask)
    {
        return Flags & mask == mask;
    }
};

#define PARTICLE_FLAG_ALIVE 0x1
#define PARTICLE_FLAG_FLUID 0x2
#define PARTICLE_FLAG_ALL 0xffff

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

void DeleteParticle(uint index)
{
    Particles.Store(index * 8 + 12, 0);
}

ParticleProperties GetParticleProperties(RWByteAddressBuffer buffer, uint index)
{
    const ParticleWord packed = LoadParticleWord(index);
    
    ParticleProperties props;
    props.Position = float3(f16tof32(packed.x >> 16),
                            f16tof32(packed.x & 0xffff),
                            f16tof32(packed.y >> 16));
    props.Flags = packed.y & 0xffff;
    
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

/// begin particle create/delete

extern ByteAddressBuffer ParticlesToCreate;
// global counter | dispatch x | dispatch y (always 1) | dispatch z (always 1) | elements...
// this buffer is used as indirect buffer for several dispatches
extern RWByteAddressBuffer ParticlesToDelete;

uint GetCreateParticleCount()
{
    return ParticlesToCreate.Load(0) - 1;
}

uint GetDeleteParticleCount()
{
    return ParticlesToDelete.Load(0) - 1;
}

ParticleWord GetParticleToCreate(uint index)
{
    return ParticlesToCreate.Load2(index * 8 + 4);
}

uint GetParticleToDelete(uint index)
{
    return ParticlesToDelete.Load(index * 4 + 4);
}

groupshared uint ParticlesToDeleteCount_GSM = 0;
groupshared uint ParticlesToDelete_GSM[256];
groupshared uint ParticlesToRemveOffset_GSM;

void DeleteParticleDelay_GSM(uint index)
{
    uint poolSize;
    InterlockedAdd(ParticlesToDeleteCount_GSM, 1, poolSize);
    
    ParticlesToDelete_GSM[poolSize] = index;
}

// need to be used with DeleteParticleDelay_GSM
void DeleteParticleDelay(uint groupIndex, uint numthreads)
{
    if (!groupIndex)
        ParticlesToDelete.InterlockedAdd(0, ParticlesToDeleteCount_GSM, ParticlesToRemveOffset_GSM);
    
    for (uint i = groupIndex; i < ParticlesToDeleteCount_GSM; i += numthreads)
        ParticlesToDelete.Store((i + ParticlesToRemveOffset_GSM) * 4, ParticlesToDelete_GSM[i]);
}

/// end particle create/delete
