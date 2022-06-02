// this module contains data structures that might be needed by other modules and functions to access them.

// todo
// handle the case that the number of blocks exceeds the limit

#pragma once

#define FLUID_MAX_PARTICLE_COUNT (1 << 20)
#define FLUID_EOS_BULK_MODULUS 50.0
#define FLUID_EOS_REST_DENSITY 4.0
#define FLUID_VISCOSITY_COEFFICIENT 0.1

struct ParticlePositionIndexed
{
    float3 Position;

    uint Index;
};

// these properties are read and written only once in each iteration, so it's
// unworthy to sort them every frame. instead they stay at where they are initialized.
struct ParticleProperties
{
    float3 AffineRow0;

    float VelocityX;

    float3 AffineRow1;

    float VelocityY;

    float3 AffineRow2;

    float VelocityZ;

    float Mass;

    float3 GetVelocity()
    {
        return float3(VelocityX, VelocityY, VelocityZ);
    }

    void SetVelocity(float3 velocity)
    {
        VelocityX = velocity.x;
        VelocityY = velocity.y;
        VelocityZ = velocity.z;
    }

    float3x3 GetAffine()
    {
        return float3x3(AffineRow0, AffineRow1, AffineRow2);
    }

    void SetAffine(float3x3 affine)
    {
        AffineRow0 = affine._m00_m01_m02;
        AffineRow1 = affine._m10_m11_m12;
        AffineRow2 = affine._m20_m21_m22;
    }

    void SetZeroAffine()
    {
        AffineRow0 = float(0.0).xxx;
        AffineRow1 = float(0.0).xxx;
        AffineRow2 = float(0.0).xxx;
    }
};

// layout: { particle count, ping-pong flag, prefix sum of particle count, unused, (indexed position{n}){2} }
// initial value: { n, 0, 0, x, <valid indexed position>{n}, x{n} }
extern RWByteAddressBuffer FluidParticlePositions;
// layout: { ((alignas(32) particle properties){n} }
// initial value: { <valid particle properties>{n}, x* }
extern RWByteAddressBuffer FluidParticleProperties;

// begin particle access

uint GetFluidParticleCountByteOffset()
{
    return 0;
}

uint GetFluidParticleCount()
{
    const uint byteOffset = GetFluidParticleCountByteOffset();
    return min(FluidParticlePositions.Load(byteOffset), FLUID_MAX_PARTICLE_COUNT);
}

// to parallelize sorting particles, a buffer whose size is twice as the number of particles are used.
// the buffer is splitted into low and high parts. and this function returns whether the high part is
// used at the beginning of each iteration.
bool GetFluidParticlePositionPingPongFlag()
{
    return FluidParticlePositions.Load(4);
}

void InvertFluidParticlePositionPingPongFlag()
{
    const bool pingpongFlag = GetFluidParticlePositionPingPongFlag();
    FluidParticlePositions.Store(4, !pingpongFlag);
}

uint AddParticleCountAtomic(uint particleCount)
{
    uint total;
    FluidParticleProperties.InterlockedAdd(8, particleCount, total);

    return total;
}

uint GetFluidParticlePositionByteOffset(uint index, bool pingpong)
{
    const uint stride = 16; // sizeof(ParticlePositionIndexed)
    const uint pingpongOffset = pingpong ? FLUID_MAX_PARTICLE_COUNT : 0;
    return 16 + index * stride + pingpongOffset;
}

ParticlePositionIndexed GetFluidParticlePosition(uint index, bool pingpong)
{
    const uint offset = GetFluidParticlePositionByteOffset(index, pingpong);
    const uint4 word = FluidParticlePositions.Load4(offset);

    ParticlePositionIndexed position;
    position.Position = asfloat(word.xyz);
    position.Index = word.w;

    return position;
}

void SetFluidParticlePosition(uint index, ParticlePositionIndexed position, bool pingpong)
{
    const uint offset = GetFluidParticlePositionByteOffset(index, pingpong);
    const uint4 word = uint4(asuint(position.Position), position.Index);

    FluidParticlePositions.Store4(offset, word);
}

uint GetFluidParticlePropertiesByteOffset(uint index)
{
    return index * 32;
}

ParticleProperties GetFluidParticleProperties(uint index)
{
    const uint offset = GetFluidParticlePropertiesByteOffset(index);

    const uint4 word0 = FluidParticleProperties.Load4(offset);
    const uint4 word1 = FluidParticleProperties.Load4(offset +16);

    const float4 value0 = f16tof32(word0);
    const float4 value1 = f16tof32(word0 >> 16);
    const float4 value2 = f16tof32(word1);
    const float4 value3 = f16tof32(word1.x >> 16);

    ParticleProperties properties;
    properties.AffineRow0 = value0.xyz;
    properties.VelocityX = value0.w;
    properties.AffineRow1 = value1.xyz;
    properties.VelocityY = value1.w;
    properties.AffineRow2 = value2.xyz;
    properties.VelocityZ = value3.x;
    properties.Mass = value3.w;

    return properties;
}

void SetFluidParticleProperties(uint index, ParticleProperties properties)
{
    const uint offset = GetFluidParticlePropertiesByteOffset(index);

    const uint4 word0lo = f32tof16(float4(properties.AffineRow0, properties.VelocityX));
    const uint4 word0hi = f32tof16(float4(properties.AffineRow1, properties.VelocityY)) << 16;
    const uint3 word1lo = f32tof16(properties.AffineRow2);
    const uint2 word1hi = f32tof16(float2(properties.VelocityZ, properties.Mass)) << 16;

    const uint word0 = word0lo | word0hi;
    const uint word1 = uint4(word1lo.x | word1hi.x, word1lo.yz, word1hi.y);

    FluidParticleProperties.Store4(offset, word0);
    FluidParticleProperties.Store4(offset +16, word1);
}

void SetFluidParticleAffineVelocity(uint index, float3x3 affine, float3 velocity)
{
    const uint offset = GetFluidParticlePropertiesByteOffset(index);

    const uint4 word0lo = f32tof16(float4(affine._m00_m01_m02, velocity.x));
    const uint4 word0hi = f32tof16(float4(affine._m10_m11_m12, velocity.y)) << 16;
    const uint4 word1lohi = f32tof16(float4(affine._m20_m21_m22, velocity.z));

    const uint4 word0 = word0lo | word0hi;
    const uint3 word1 = uint3(word1lohi.x | word1lohi.w, word1lohi.yz);

    FluidParticleProperties.Store4(offset, word0);
    FluidParticleProperties.Store3(offset +16, word1);
}

// end particle access

// it must be power of two
#define FLUID_BLOCK_SIZE_LEVEL0 4
#define FLUID_BLOCK_GRID_COUNT_LEVEL0 (FLUID_BLOCK_SIZE_LEVEL0 * FLUID_BLOCK_SIZE_LEVEL0 * FLUID_BLOCK_SIZE_LEVEL0)
#define FLUID_BLOCK_COUNT_X_LEVEL0 16
#define FLUID_BLOCK_COUNT_Y_LEVEL0 16
#define FLUID_BLOCK_COUNT_Z_LEVEL0 16
#define FLUID_BLOCK_COUNT_LEVEL0 (FLUID_BLOCK_COUNT_X_LEVEL0 * FLUID_BLOCK_COUNT_Y_LEVEL0 * FLUID_BLOCK_COUNT_Z_LEVEL0)
#define FLUID_GRID_EXTENT_LEVEL0 (FLUID_BLOCK_SIZE_LEVEL0 * FLUID_BLOCK_COUNT_LEVEL0)

#define FLUID_BLOCK_SIZE_LEVEL1 4
#define FLUID_BLOCK_GRID_COUNT_LEVEL1 (FLUID_BLOCK_SIZE_LEVEL1 * FLUID_BLOCK_SIZE_LEVEL1 * FLUID_BLOCK_SIZE_LEVEL1)
#define FLUID_BLOCK_COUNT_X_LEVEL1 16
#define FLUID_BLOCK_COUNT_Y_LEVEL1 16
#define FLUID_BLOCK_COUNT_Z_LEVEL1 16
#define FLUID_BLOCK_COUNT_LEVEL1 (FLUID_BLOCK_COUNT_X_LEVEL1 * FLUID_BLOCK_COUNT_Y_LEVEL1 * FLUID_BLOCK_COUNT_Z_LEVEL1)
#define FLUID_GRID_EXTENT_LEVEL1 (FLUID_BLOCK_SIZE_LEVEL1 * FLUID_BLOCK_COUNT_LEVEL1)

// though it's named 'block', it represents each cell
#define FLUID_BLOCK_COUNT_X_LEVEL2 16
#define FLUID_BLOCK_COUNT_Y_LEVEL2 16
#define FLUID_BLOCK_COUNT_Z_LEVEL2 16
#define FLUID_BLOCK_COUNT_LEVEL2 uint3(FLUID_BLOCK_COUNT_X_LEVEL2, FLUID_BLOCK_COUNT_Y_LEVEL2, FLUID_BLOCK_COUNT_Z_LEVEL2)
#define FLUID_GRID_EXTENT_LEVEL2 FLUID_BLOCK_COUNT_LEVEL2

#define FLUID_VIRTUAL_GRID_EXTENT (FLUID_BLOCK_SIZE_LEVEL0 * FLUID_BLOCK_SIZE_LEVEL1 * FLUID_GRID_EXTENT_LEVEL2)

#define FLUID_GRID_ATOMIC_LOCK_OFFSET_LEVEL0 0
#define FLUID_GRID_ATOMIC_LOCK_OFFSET_LEVEL1 (FLUID_GRID_ATOMIC_LOCK_OFFSET_LEVEL0 + FLUID_BLOCK_COUNT_LEVEL0)
#define FLUID_GRID_ATOMIC_LOCK_OFFSET_LEVEL2 (FLUID_GRID_ATOMIC_LOCK_OFFSET_LEVEL1 + FLUID_BLOCK_COUNT_LEVEL1)

#define FLUID_BLOCK_MAX_PARTICLE_COUNT (64 - 5)
#define FLUID_BLOCK_PARTICLE_STRIDE (FLUID_BLOCK_MAX_PARTICLE_COUNT + 5)

#define FLUID_GRID_CHANNEL_COUNT 4
#define FLUID_GRID_CHANNEL0_OFFSET uint3(0, 0, 0)
#define FLUID_GRID_CHANNEL1_OFFSET uint3(FLUID_BLOCK_COUNT_X_LEVEL0 * FLUID_BLOCK_SIZE_LEVEL0, 0, 0)
#define FLUID_GRID_CHANNEL2_OFFSET uint3(0, FLUID_BLOCK_COUNT_Y_LEVEL0 * FLUID_BLOCK_SIZE_LEVEL0, 0)
#define FLUID_GRID_CHANNEL3_OFFSET uint3(FLUID_BLOCK_COUNT_X_LEVEL0 * FLUID_BLOCK_SIZE_LEVEL0, FLUID_BLOCK_COUNT_Y_LEVEL0 * FLUID_BLOCK_SIZE_LEVEL0, 0)

// to perform atomic operations, nomalized and converted to integer are the values to be written to level 0 grid
#define FLUID_GRID_BOUND_MASS 1024.0
#define FLUID_GRID_BOUND_MOMENTUM float3(8192.0, 8192.0, 8192.0)

int EncodeFluidGridMass(float mass)
{
    const float normalization = 1.0 / FLUID_GRID_BOUND_MASS;
    const float intMax = float(1 << 31);

    const float normalized = clamp(normalization * mass, -1.0, 1.0);
    return int(normalized * intMax);
}

int3 EncodeFluidGridMomentum(float3 velocity)
{
    const float normalization = 1.0 / FLUID_GRID_BOUND_MOMENTUM;
    const float intMax = float(1 << 31);

    const float3 normalized = clamp(normalization * velocity, -1.0, 1.0);
    return int3(normalized * intMax);
}

float DecodeFluidGridMass(int value)
{
    const float bound = FLUID_GRID_BOUND_MASS;
    const float intMaxInv = 1.0 / float(1 << 31);

    const float normalized = value * intMaxInv;
    return normalized * bound;
}

float3 DecodeFluidGridMomentum(int3 value)
{
    const float3 bound = FLUID_GRID_BOUND_MOMENTUM;
    const float intMaxInv = 1.0 / float(1 << 31);

    const float3 normalized = value * intMaxInv;
    return normalized * bound;
}

// these objects below constitute sparse grid structure.
//  level 0             : tex3d, 4x4x4 eulerian grid blocks ((5x4cm)^3))
//  level 1             : tex3d, 4x4x4 nodes ((20x4cm)^3)
//  level 2             : tex3d, dense root

// initial value: 0
extern RWTexture3D<int> FluidGridLevel0;
// initial value: FLUID_BLOCK_INDEX_NULL
extern RWTexture3D<uint> FluidGridLevel1;
// initial value: FLUID_BLOCK_INDEX_NULL
extern RWTexture3D<uint> FluidGridLevel2;
// layout:
// { level 0 block count, unused{2}, level 1 block count, {block position, particle count, prefix sum of particle count, particle index*}* }
// initial value:
// { 0, 1, 1, 0, {x{3}, 0, 0, 0*}* }
extern RWByteAddressBuffer FluidBlockParticleIndices;
// initial value:
// { 0* }
extern RWBuffer<uint> FluidGridAtomicLock;

// begin grid access

int3 GetFluidGridPosition(float3 gridSpacePosition)
{
    return int3(floor(gridSpacePosition - 0.5));
}

// there are some limits on the dimension of textures, so non-zero-level blocks are
// not arranged in a long-cube manner. consequently encoding/decoding is required.
// it forms a bijection between continuous indices and indices of grid blocks.
#define DEF_ENCODE_DECODE_FLUID_BLOCK_INDEX_FUNC(level) \
uint EncodeFluidBlockIndexLevel##level(uint3 index) \
{ \
    const uint3 offset = uint3(\
        1, \
        FLUID_BLOCK_COUNT_X_LEVEL##level, \
        FLUID_BLOCK_COUNT_X_LEVEL##level * FLUID_BLOCK_COUNT_Y_LEVEL##level); \
    return dot(offset, index); \
} \
uint3 DecodeFluidBlockIndexLevel##level(uint index) \
{ \
    const uint x = index % FLUID_BLOCK_COUNT_X_LEVEL##level; \
    const uint y = (index / FLUID_BLOCK_COUNT_X_LEVEL##level) % FLUID_BLOCK_COUNT_Y_LEVEL##level; \
    const uint z = index / (FLUID_BLOCK_COUNT_X_LEVEL##level * FLUID_BLOCK_COUNT_Y_LEVEL##level); \
    return uint3(x, y, z); \
}

DEF_ENCODE_DECODE_FLUID_BLOCK_INDEX_FUNC(0)
DEF_ENCODE_DECODE_FLUID_BLOCK_INDEX_FUNC(1)
DEF_ENCODE_DECODE_FLUID_BLOCK_INDEX_FUNC(2)

#undef DEF_ENCODE_DECODE_FLUID_BLOCK_INDEX_FUNC

#define DEF_ENCODE_DECODE_FLUID_GRID_INDEX_FUNC(level) \
uint EncodeFluidGridIndexLevel##level(uint3 index, uint3 padding) \
{ \
    const uint3 size = FLUID_BLOCK_SIZE_LEVEL0 + padding; \
    const uint3 offset = uint3(1, size.x, size.x * size.y); \
    return dot(offset, index); \
} \
uint EncodeFluidGridIndexLevel##level(uint3 index, uint padding) \
{ \
    return EncodeFluidGridIndexLevel##level(index, padding.xxx); \
} \
uint EncodeFluidGridIndexLevel##level(uint3 index) \
{ \
    return EncodeFluidGridIndexLevel##level(index, 0); \
} \
uint3 DecodeFluidGridIndexLevel##level(uint index, uint3 padding) \
{ \
    const uint3 size = FLUID_BLOCK_SIZE_LEVEL0 + padding; \
    const uint x = index % size.x; \
    const uint y = (index / size.x) % size.y; \
    const uint z = index / (size.x * size.y); \
    return uint3(x, y, z); \
} \
uint3 DecodeFluidGridIndexLevel##level(uint index, uint padding) \
{ \
    return DecodeFluidGridIndexLevel##level(index, padding.xxx); \
} \
uint3 DecodeFluidGridIndexLevel##level(uint index) \
{ \
    return DecodeFluidGridIndexLevel##level(index, 0); \
}

DEF_ENCODE_DECODE_FLUID_GRID_INDEX_FUNC(0)
DEF_ENCODE_DECODE_FLUID_GRID_INDEX_FUNC(1)

#undef DEF_ENCODE_DECODE_FLUID_GRID_INDEX_FUNC

#define FLUID_BLOCK_INDEX_NULL uint(-1)

uint GetFluidBlockCountByteOffset(uint level)
{
    return level ? 12 : 0;
}

uint GetFluidBlockInfoByteOffset(uint blockIndex)
{
    return GetFluidBlockCountByteOffset(1) + 4 + blockIndex * FLUID_BLOCK_PARTICLE_STRIDE;
}

uint GetFluidBlockPositionByteOffset(uint blockIndex)
{
    return GetFluidBlockInfoByteOffset(blockIndex);
}

uint GetFluidBlockParticleCountByteOffset(uint blockIndex)
{
    return GetFluidBlockPositionByteOffset(blockIndex) + 12;
}

uint GetFluidBlockParticleCountPrefixSumByteOffset(uint blockIndex)
{
    return GetFluidBlockParticleCountByteOffset(blockIndex) + 4;
}

uint GetFluidBlockParticleIndexByteOffset(uint blockIndex, uint particleIndex)
{
    return GetFluidBlockParticleCountPrefixSumByteOffset(blockIndex) + 4 + particleIndex * 4;
}

uint GetFluidBlockCount(uint level)
{
    const uint byteOffset = GetFluidBlockCountByteOffset(level);

    return FluidBlockParticleIndices.Load(byteOffset);
}

uint GetFluidBlockParticleCount(uint blockIndexLevel0)
{
    const uint byteOffset = GetFluidBlockParticleCountByteOffset(blockIndexLevel0);
    const uint particleCount = FluidBlockParticleIndices.Load(byteOffset);

    return min(particleCount, FLUID_BLOCK_MAX_PARTICLE_COUNT);
}

uint GetFluidBlockParticleCountPrefixSum(uint blockIndex)
{
    const uint byteOffset = GetFluidBlockParticleCountPrefixSumByteOffset(blockIndex);
    return FluidBlockParticleIndices.Load(byteOffset);
}

void SetFluidBlockParticleCountPrefixSum(uint blockIndex, uint prefixSum)
{
    const uint byteOffset = GetFluidBlockParticleCountPrefixSumByteOffset(blockIndex);
    FluidBlockParticleIndices.Store(byteOffset, prefixSum);
}

void GetFluidBlockInfo(uint blockIndexLevel0, out uint3 blockPositionLevel1, out uint particleCount)
{
    const uint byteOffset = GetFluidBlockInfoByteOffset(blockIndexLevel0);
    const uint4 word = FluidBlockParticleIndices.Load4(byteOffset);

    particleCount = min(word.x, FLUID_BLOCK_MAX_PARTICLE_COUNT);
    blockPositionLevel1 = word.yzw;
}

// prefix sum of particle count is undefiend before particle to grid transfer
void GetFluidBlockInfo(uint blockIndexLevel0, out uint3 blockPositionLevel1, out uint particleCount, out uint particleCountPrefixSum)
{
    GetFluidBlockInfo(blockIndexLevel0, blockPositionLevel1, particleCount);
    particleCountPrefixSum = GetFluidBlockParticleCountPrefixSum(blockIndexLevel0);
}

uint GetFluidBlockParticleIndex(uint blockIndexLevel0, uint particleIndex)
{
    const uint byteOffset = GetFluidBlockParticleIndexByteOffset(blockIndexLevel0, particleIndex);
    return FluidBlockParticleIndices.Load(byteOffset);
}

// return: the allocated level n-1 index
uint ActivateFluidBlockLevel0(uint3 blockIndexLevel1, uint3 blockPositionLevel1)
{
    uint indexSublevel = FluidGridLevel1[blockIndexLevel1];
    if (indexSublevel != FLUID_BLOCK_INDEX_NULL)
        return indexSublevel;

    uint initialized;
    const uint atomicIndex = EncodeFluidBlockIndexLevel1(blockIndexLevel1);
    InterlockedCompareExchange(
        FluidGridAtomicLock[atomicIndex + FLUID_GRID_ATOMIC_LOCK_OFFSET_LEVEL1],
        0, 1, initialized);
    
    if (!initialized)
    {
        FluidBlockParticleIndices.InterlockedAdd(GetFluidBlockCountByteOffset(0), 1, indexSublevel);
        FluidGridLevel1[blockIndexLevel1] = indexSublevel;

        const uint offset = GetFluidBlockPositionByteOffset(indexSublevel);
        // w component is the particle count
        // N.B.:
        // make sure GetFluidBlockPositionByteOffset(indexSublevel) == GetFluidBlockPositionByteOffset(indexSublevel) + 12
        // if the assumption above doesn't hold, remember to modify here either.
        FluidBlockParticleIndices.Store4(offset, uint4(blockPositionLevel1, 0));
    }
    else
    {
        do
        {
            indexSublevel = FluidGridLevel1[blockIndexLevel1];
        } while (indexSublevel == FLUID_BLOCK_INDEX_NULL);
    }

    return indexSublevel;
}

// return: the allocated level n-1 index
uint ActivateFluidBlockLevel1(uint3 gridIndexLevel2)
{
    uint indexSublevel = FluidGridLevel2[gridIndexLevel2];
    if (indexSublevel != FLUID_BLOCK_INDEX_NULL)
        return indexSublevel;

    uint initialized;
    const uint atomicIndex = EncodeFluidBlockIndexLevel2(gridIndexLevel2);
    InterlockedCompareExchange(
        FluidGridAtomicLock[atomicIndex + FLUID_GRID_ATOMIC_LOCK_OFFSET_LEVEL2],
        0, 1, initialized);

    if (!initialized)
    {
        FluidBlockParticleIndices.InterlockedAdd(GetFluidBlockCountByteOffset(1), 1, indexSublevel);
        FluidGridLevel2[gridIndexLevel2] = indexSublevel;
    }
    else
    {
        do
        {
            indexSublevel = FluidGridLevel2[gridIndexLevel2];
        } while (indexSublevel == FLUID_BLOCK_INDEX_NULL);
    }

    return indexSublevel;
}

// returns whether the grid is within the simulation domain
bool ActivateFluidBlock(int3 gridPositionLevel0, out uint blockIndexLevel0)
{
    if (any(gridPositionLevel0 < 0 | gridPositionLevel0 >= FLUID_VIRTUAL_GRID_EXTENT - FLUID_BLOCK_SIZE_LEVEL0))
        return false;

    const uint3 gridPositionLevel1 = gridPositionLevel0 / FLUID_BLOCK_SIZE_LEVEL0;
    const uint3 gridPositionLevel2 = gridPositionLevel1 / FLUID_BLOCK_SIZE_LEVEL1;
    const uint3 gridOffsetLevel1 = gridPositionLevel1 % FLUID_BLOCK_SIZE_LEVEL1;

    const uint blockIndexLevel1Encoded = ActivateFluidBlockLevel1(gridPositionLevel2);
    const uint3 gridIndexLevel1 = DecodeFluidBlockIndexLevel1(blockIndexLevel1Encoded) * FLUID_BLOCK_SIZE_LEVEL1 + gridOffsetLevel1;

    blockIndexLevel0 = ActivateFluidBlockLevel0(gridIndexLevel1, gridPositionLevel1);
    return true;
}

uint GetFluidBlockIndexLevel0(uint3 gridPositionLevel1)
{
    const uint3 gridPositionLevel2 = gridPositionLevel1 / FLUID_BLOCK_SIZE_LEVEL1;
    const uint3 gridOffsetLevel1 = gridPositionLevel1 % FLUID_BLOCK_SIZE_LEVEL1;

    const uint blockIndexLevel1Encoded = FluidGridLevel2[gridPositionLevel2];
    const uint3 gridIndexLevel1 = DecodeFluidBlockIndexLevel1(blockIndexLevel1Encoded) * FLUID_BLOCK_SIZE_LEVEL1 + gridOffsetLevel1;

    return FluidGridLevel1[gridIndexLevel1];
}

void AddParticleToFluidBlock(int3 gridPositionLevel0, uint particleIndex)
{
    uint blockIndexLevel0;
    if (ActivateFluidBlock(gridPositionLevel0, blockIndexLevel0))
        return;

    uint _;
    ActivateFluidBlock(gridPositionLevel0 + int3(2, 0, 0), _);
    ActivateFluidBlock(gridPositionLevel0 + int3(2, 2, 0), _);
    ActivateFluidBlock(gridPositionLevel0 + int3(0, 2, 0), _);
    ActivateFluidBlock(gridPositionLevel0 + int3(0, 2, 2), _);
    ActivateFluidBlock(gridPositionLevel0 + int3(0, 0, 2), _);
    ActivateFluidBlock(gridPositionLevel0 + int3(2, 0, 2), _);
    ActivateFluidBlock(gridPositionLevel0 + int3(2, 2, 2), _);

    const uint particleCountByteOffset = GetFluidBlockParticleCountByteOffset(blockIndexLevel0);

    uint particleCount;
    FluidBlockParticleIndices.InterlockedAdd(particleCountByteOffset, 1, particleCount);

    if (particleCount < FLUID_BLOCK_MAX_PARTICLE_COUNT)
    {
        const uint particleByteOffset = GetFluidBlockParticleIndexByteOffset(blockIndexLevel0, particleCount);
        FluidBlockParticleIndices.Store(particleByteOffset, particleIndex);
    }
}

// end grid access
