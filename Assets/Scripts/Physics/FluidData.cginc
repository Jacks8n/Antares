﻿// this module contains data structures that might be needed by other modules and functions to access them.

// todo
// handle the case that the number of blocks exceeds the limit

#pragma once

#define FLUID_MAX_PARTICLE_COUNT (1 << 20)

#define FLUID_EOS_BULK_MODULUS 50.0
#define FLUID_EOS_REST_DENSITY 4.0
#define FLUID_VISCOSITY_COEFFICIENT 0.1

#define FLUID_PARTICLE_RADIUS 1.5

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

// layout: { particle count, indirect arg{3}, ping-pong flag, prefix sum of particle count, (indexed position{n}){2} }
// initial value: { n, 1, 0, 0, 0, 0, <valid indexed position>{n}, x{n} }
extern RWBuffer<uint> FluidParticlePositions;
// layout: { ((alignas(32) particle properties){n} }
// initial value: { <valid particle properties>{n}, x* }
extern RWByteAddressBuffer FluidParticleProperties;

/// begin particle access

uint GetFluidParticleCountOffset()
{
    return 0;
}

// to parallelize sorting particles, a buffer whose size is twice as the number of particles are used.
// the buffer is splitted into low and high parts. and this function returns whether the high part is
// used at the beginning of each iteration.
uint GetFluidParticlePositionPingPongFlagOffset()
{
    return 16;
}

uint GetFluidParticleCountPrefixSumOffset()
{
    return GetFluidParticlePositionPingPongFlagOffset() + 4;
}

uint GetFluidParticlePositionOffset(uint index, bool pingpong)
{
    const uint stride = 4; // compressed size of ParticlePositionIndexed / 4
    const uint pingpongOffset = pingpong ? FLUID_MAX_PARTICLE_COUNT : 0;
    return GetFluidParticleCountPrefixSumOffset() + 1 + index * stride + pingpongOffset;
}

uint GetFluidParticleCount()
{
    const uint offset = GetFluidParticleCountOffset();
    return min(FluidParticlePositions.Load(offset), FLUID_MAX_PARTICLE_COUNT);
}

bool GetFluidParticlePositionPingPongFlag()
{
    const uint offset = GetFluidParticlePositionPingPongFlagOffset();
    return FluidParticlePositions.Load(offset);
}

void InvertFluidParticlePositionPingPongFlag()
{
    const uint offset = GetFluidParticlePositionPingPongFlagOffset();
    const bool pingpongFlag = GetFluidParticlePositionPingPongFlag();

    FluidParticlePositions[offset] = uint(!pingpongFlag);
}

void ResetAccumulateParticleCount()
{
    const uint offset = GetFluidParticleCountPrefixSumOffset();

    FluidParticlePositions[offset] = 0;
}

uint AccumulateParticleCountAtomic(uint particleCount)
{
    const uint offset = GetFluidParticleCountPrefixSumOffset();

    uint total;
    InterlockedAdd(FluidParticlePositions[offset], particleCount, total);

    return total;
}

ParticlePositionIndexed GetFluidParticlePosition(uint index, bool pingpong)
{
    const uint offset = GetFluidParticlePositionOffset(index, pingpong);
    const uint4 word = uint4(
        FluidParticlePositions.Load(offset),
        FluidParticlePositions.Load(offset +1),
        FluidParticlePositions.Load(offset +2),
        FluidParticlePositions.Load(offset +3)
    );

    ParticlePositionIndexed position;
    position.Position = asfloat(word.xyz);
    position.Index = word.w;

    return position;
}

void SetFluidParticlePosition(uint index, ParticlePositionIndexed position, bool pingpong)
{
    const uint offset = GetFluidParticlePositionOffset(index, pingpong);
    const uint4 word = uint4(asuint(position.Position), position.Index);

    FluidParticlePositions[offset] = word.x;
    FluidParticlePositions[offset +1] = word.y;
    FluidParticlePositions[offset +2] = word.z;
    FluidParticlePositions[offset +3] = word.w;
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

    const uint4 word0 = word0lo | word0hi;
    const uint4 word1 = uint4(word1lo.x | word1hi.x, word1lo.yz, word1hi.y);

    FluidParticleProperties.Store4(offset, word0);
    FluidParticleProperties.Store4(offset + 16, word1);
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

/// end particle access

// it must be power of two
#define FLUID_BLOCK_SIZE_LEVEL0 4
#define FLUID_BLOCK_GRID_COUNT_LEVEL0 (FLUID_BLOCK_SIZE_LEVEL0 * FLUID_BLOCK_SIZE_LEVEL0 * FLUID_BLOCK_SIZE_LEVEL0)
#define FLUID_BLOCK_COUNT_X_LEVEL0 64
#define FLUID_BLOCK_COUNT_Y_LEVEL0 32
#define FLUID_BLOCK_COUNT_Z_LEVEL0 32
#define FLUID_BLOCK_COUNT_LEVEL0 (FLUID_BLOCK_COUNT_X_LEVEL0 * FLUID_BLOCK_COUNT_Y_LEVEL0 * FLUID_BLOCK_COUNT_Z_LEVEL0)
#define FLUID_GRID_EXTENT_LEVEL0 (FLUID_BLOCK_SIZE_LEVEL0 * uint3(FLUID_BLOCK_COUNT_X_LEVEL0, FLUID_BLOCK_COUNT_Y_LEVEL0, FLUID_BLOCK_COUNT_Z_LEVEL0))

#define FLUID_BLOCK_SIZE_LEVEL1 4
#define FLUID_BLOCK_GRID_COUNT_LEVEL1 (FLUID_BLOCK_SIZE_LEVEL1 * FLUID_BLOCK_SIZE_LEVEL0 * FLUID_BLOCK_SIZE_LEVEL0)
#define FLUID_BLOCK_COUNT_X_LEVEL1 64
#define FLUID_BLOCK_COUNT_Y_LEVEL1 32
#define FLUID_BLOCK_COUNT_Z_LEVEL1 32
#define FLUID_BLOCK_COUNT_LEVEL1 (FLUID_BLOCK_COUNT_X_LEVEL1 * FLUID_BLOCK_COUNT_Y_LEVEL1 * FLUID_BLOCK_COUNT_Z_LEVEL1)
#define FLUID_GRID_EXTENT_LEVEL1 (FLUID_BLOCK_SIZE_LEVEL1 * uint3(FLUID_BLOCK_COUNT_X_LEVEL1, FLUID_BLOCK_COUNT_Y_LEVEL1, FLUID_BLOCK_COUNT_Z_LEVEL1))

// though it's named 'block', it represents each cell
#define FLUID_BLOCK_COUNT_X_LEVEL2 256
#define FLUID_BLOCK_COUNT_Y_LEVEL2 256
#define FLUID_BLOCK_COUNT_Z_LEVEL2 256
#define FLUID_BLOCK_COUNT_LEVEL2 (FLUID_BLOCK_COUNT_X_LEVEL2 * FLUID_BLOCK_COUNT_Y_LEVEL2 * FLUID_BLOCK_COUNT_Z_LEVEL2)
#define FLUID_GRID_EXTENT_LEVEL2 uint3(FLUID_BLOCK_COUNT_X_LEVEL2, FLUID_BLOCK_COUNT_Y_LEVEL2, FLUID_BLOCK_COUNT_Z_LEVEL2)

#define FLUID_VIRTUAL_GRID_EXTENT (FLUID_BLOCK_SIZE_LEVEL0 * FLUID_BLOCK_SIZE_LEVEL1 * FLUID_GRID_EXTENT_LEVEL2)

#define FLUID_GRID_ATOMIC_LOCK_OFFSET_LEVEL0 0
#define FLUID_GRID_ATOMIC_LOCK_OFFSET_LEVEL1 (FLUID_GRID_ATOMIC_LOCK_OFFSET_LEVEL0 + FLUID_BLOCK_COUNT_LEVEL0)
#define FLUID_GRID_ATOMIC_LOCK_OFFSET_LEVEL2 (FLUID_GRID_ATOMIC_LOCK_OFFSET_LEVEL1 + FLUID_BLOCK_COUNT_LEVEL1)

#define FLUID_BLOCK_MAX_PARTICLE_COUNT (64 - 5)
#define FLUID_BLOCK_PARTICLE_STRIDE (FLUID_BLOCK_MAX_PARTICLE_COUNT + 5)

// mass
#define FLUID_GRID_CHANNEL0_OFFSET uint3(0, 0, 0)
// velocity/momentum
#define FLUID_GRID_CHANNEL1_OFFSET uint3(FLUID_BLOCK_COUNT_X_LEVEL0 * FLUID_BLOCK_SIZE_LEVEL0, 0, 0)
#define FLUID_GRID_CHANNEL2_OFFSET uint3(FLUID_BLOCK_COUNT_X_LEVEL0 * FLUID_BLOCK_SIZE_LEVEL0 * 2, 0, 0)
#define FLUID_GRID_CHANNEL3_OFFSET uint3(FLUID_BLOCK_COUNT_X_LEVEL0 * FLUID_BLOCK_SIZE_LEVEL0 * 3, 0, 0)
// sdf
#define FLUID_GRID_CHANNEL4_OFFSET uint3(FLUID_BLOCK_COUNT_X_LEVEL0 * FLUID_BLOCK_SIZE_LEVEL0 * 4, 0, 0)

// to perform atomic operations, nomalized and converted to integer are the values to be written to level 0 grid
#define FLUID_GRID_ENCODED_VALUE_MAX int(1 << 30)
#define FLUID_GRID_BOUND_MASS 1024.0
#define FLUID_GRID_BOUND_MOMENTUM 8192.0
#define FLUID_GRID_BOUND_SDF 3.0

#define DEF_ENCODE_DECODE_GRID_VALUE(channel) \
int##channel EncodeGridValue(float##channel value, float bound, float normMin, float normMax) \
{ \
    const float normalization = 1.0 / bound; \
    const float intMax = float(FLUID_GRID_ENCODED_VALUE_MAX); \
    const float##channel normalized = clamp(normalization * value, normMin, normMax); \
    return int##channel(normalized * intMax); \
} \
float##channel DecodeGridValue(int##channel value, float bound) \
{ \
    const float intMaxInv = 1.0 / float(FLUID_GRID_ENCODED_VALUE_MAX); \
    const float##channel normalized = value * intMaxInv; \
    return normalized * bound; \
}

DEF_ENCODE_DECODE_GRID_VALUE(1)
DEF_ENCODE_DECODE_GRID_VALUE(2)
DEF_ENCODE_DECODE_GRID_VALUE(3)
DEF_ENCODE_DECODE_GRID_VALUE(4)

#undef DEF_ENCODE_DECODE_GRID_VALUE

int EncodeFluidGridMass(float mass)
{
    return EncodeGridValue(mass, FLUID_GRID_BOUND_MASS, 0.0, 1.0);
}

int3 EncodeFluidGridMomentum(float3 velocity)
{
    return EncodeGridValue(velocity, FLUID_GRID_BOUND_MOMENTUM, -1.0, 1.0);
}

int EncodeFluidGridSDF(float sdf)
{
    return EncodeGridValue(sdf, FLUID_GRID_BOUND_SDF, -1.0, 1.0);
}

float DecodeFluidGridMass(int value)
{
    return DecodeGridValue(value, FLUID_GRID_BOUND_MASS);
}

float3 DecodeFluidGridMomentum(int3 value)
{
    return DecodeGridValue(value, FLUID_GRID_BOUND_MOMENTUM);
}

float DecodeFluidGridSDF(int value)
{
    return DecodeGridValue(value, FLUID_GRID_BOUND_SDF);
}

// sparse transfer grid(updated in each physics frame):
//  level 0  : 64*32*32 | 4x4x4 grids | 64mb(velocity/momemntum, mass)
//  level 1  : 64^3     | 4x4x4 nodes | 64mb
//  level 2  : 256^3    | dense root  | 64mb
//  if dense : 4096^3   | dense grids | 1tb, which is impractical
//
// virtual volume  : (256*4*4*5cm)^3 = (204.8m)^3
// physical volume : (64*4*5cm)^3 = (12.8m)^3

// initial value: 0
extern RWTexture3D<int> FluidGridLevel0;
// initial value: 0
extern RWTexture3D<int> FluidGridLevel1;
// initial value: 0
extern RWTexture3D<int> FluidGridLevel2;

// layout:
// {
//   level 0 block count, indirect groups.yz, level 1 block count, indirect groups.yz,
//   {block position, particle count, prefix sum of particle count, particle index*}*
// }
// initial value:
// { 0, 1, 1, 0, 1, 1, {x{3}, 0, 0, 0*}* }
extern RWBuffer<uint> FluidBlockParticleIndices;
// initial value:
// { <flag>* }
extern RWBuffer<uint> FluidGridAtomicLock;

/// begin grid access

int3 GetFluidGridPositionNearest(float3 gridSpacePosition)
{
    return int3(round(gridSpacePosition));
}

void GetFluidGridPositions(uint3 gridPositionLevel0, out uint3 gridPositionLevel1, out uint3 gridPositionLevel2, out uint3 gridOffsetLevel1)
{
    gridPositionLevel1 = gridPositionLevel0 / FLUID_BLOCK_SIZE_LEVEL0;
    gridPositionLevel2 = gridPositionLevel1 / FLUID_BLOCK_SIZE_LEVEL1;
    gridOffsetLevel1 = gridPositionLevel1 % FLUID_BLOCK_SIZE_LEVEL1;
}

void GetFluidGridPositions(uint3 gridPositionLevel0, out uint3 gridPositionLevel2, out uint3 gridOffsetLevel1)
{
    uint3 _;
    GetFluidGridPositions(gridPositionLevel0, _, gridPositionLevel2, gridOffsetLevel1);
}

uint EncodeGridIndexLinear(uint3 index, uint2 blockSizeXY)
{
    const uint3 offset = uint3(1, blockSizeXY.x, blockSizeXY.x * blockSizeXY.y);
    return dot(offset, index);
}

uint3 DecodeGridIndexLinear(uint index, uint2 blockSizeXY)
{
    const uint x = index % blockSizeXY.x;
    const uint y = (index / blockSizeXY.x) % blockSizeXY.y;
    const uint z = index / (blockSizeXY.x * blockSizeXY.y);

    return uint3(x, y, z);
}

uint EncodeFluidBlockIndex(uint3 index, uint2 blockSizeXY)
{
    return uint(1 << 31) | EncodeGridIndexLinear(index, blockSizeXY);
}

uint3 DecodeFluidBlockIndex(uint index, uint2 blockSizeXY)
{
    return DecodeGridIndexLinear(index & (uint(1 << 31) - 1), blockSizeXY);
}

// there are some limits on the dimension of textures, so non-zero-level blocks are
// not arranged in a long-cube manner. consequently encoding/decoding is required.
// it forms a bijection between continuous indices and indices of grid blocks.
#define DEF_ENCODE_DECODE_FLUID_BLOCK_INDEX_FUNC(level) \
uint EncodeFluidBlockIndexLevel##level(uint3 index) \
{ \
    return EncodeFluidBlockIndex(index, uint2(FLUID_BLOCK_COUNT_X_LEVEL##level, FLUID_BLOCK_COUNT_Y_LEVEL##level)); \
} \
uint3 DecodeFluidBlockIndexLevel##level(uint index) \
{ \
    return DecodeFluidBlockIndex(index, uint2(FLUID_BLOCK_COUNT_X_LEVEL##level, FLUID_BLOCK_COUNT_Y_LEVEL##level)); \
}

DEF_ENCODE_DECODE_FLUID_BLOCK_INDEX_FUNC(0)
DEF_ENCODE_DECODE_FLUID_BLOCK_INDEX_FUNC(1)
DEF_ENCODE_DECODE_FLUID_BLOCK_INDEX_FUNC(2)

#undef DEF_ENCODE_DECODE_FLUID_BLOCK_INDEX_FUNC

#define DEF_ENCODE_DECODE_FLUID_GRID_INDEX_FUNC(level) \
uint EncodeFluidGridIndexLevel##level(uint3 index, uint3 padding) \
{ \
    const uint3 size = FLUID_BLOCK_SIZE_LEVEL##level + padding; \
    return EncodeGridIndexLinear(index, size.xy); \
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
    const uint3 size = FLUID_BLOCK_SIZE_LEVEL##level + padding; \
    return DecodeGridIndexLinear(index, size.xy); \
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

bool IsValidFluidBlockIndex(uint index)
{
    const uint msb = 1 << 31;
    return index & msb;
}

uint GetFluidBlockCountOffset(uint level)
{
    return level ? 3 : 0;
}

uint GetFluidBlockInfoOffset(uint blockIndex)
{
    return GetFluidBlockCountOffset(1) + 3 + blockIndex * FLUID_BLOCK_PARTICLE_STRIDE;
}

uint GetFluidBlockPositionOffset(uint blockIndex)
{
    return GetFluidBlockInfoOffset(blockIndex);
}

uint GetFluidBlockParticleCountOffset(uint blockIndex)
{
    return GetFluidBlockPositionOffset(blockIndex) + 3;
}

uint GetFluidBlockParticleCountPrefixSumOffset(uint blockIndex)
{
    return GetFluidBlockParticleCountOffset(blockIndex) + 1;
}

uint GetFluidBlockParticleIndexOffset(uint blockIndex, uint particleIndex)
{
    return GetFluidBlockParticleCountPrefixSumOffset(blockIndex) + 1 + particleIndex;
}

uint GetFluidBlockCount(uint level)
{
    const uint offset = GetFluidBlockCountOffset(level);

    return FluidBlockParticleIndices[offset];
}

uint GetFluidBlockParticleCount(uint blockIndexLevel0)
{
    const uint offset = GetFluidBlockParticleCountOffset(blockIndexLevel0);
    const uint particleCount = FluidBlockParticleIndices[offset];

    return min(particleCount, FLUID_BLOCK_MAX_PARTICLE_COUNT);
}

uint GetFluidBlockParticleCountPrefixSum(uint blockIndex)
{
    const uint offset = GetFluidBlockParticleCountPrefixSumOffset(blockIndex);
    return FluidBlockParticleIndices[offset];
}

void SetFluidBlockParticleCountPrefixSum(uint blockIndex, uint prefixSum)
{
    const uint offset = GetFluidBlockParticleCountPrefixSumOffset(blockIndex);
    FluidBlockParticleIndices[offset] = prefixSum;
}

void GetFluidBlockInfo(uint blockIndexLevel0, out uint3 gridPositionLevel1, out uint particleCount)
{
    const uint offset = GetFluidBlockInfoOffset(blockIndexLevel0);

    particleCount = min(FluidBlockParticleIndices[offset], FLUID_BLOCK_MAX_PARTICLE_COUNT);
    gridPositionLevel1 = uint3(
        FluidBlockParticleIndices[offset +1],
        FluidBlockParticleIndices[offset +2],
        FluidBlockParticleIndices[offset +3]
    );
}

// prefix sum of particle count is undefiend before particle to grid transfer
void GetFluidBlockInfo(uint blockIndexLevel0, out uint3 gridPositionLevel1, out uint particleCount, out uint particleCountPrefixSum)
{
    GetFluidBlockInfo(blockIndexLevel0, gridPositionLevel1, particleCount);
    particleCountPrefixSum = GetFluidBlockParticleCountPrefixSum(blockIndexLevel0);
}

uint GetFluidBlockParticleIndex(uint blockIndexLevel0, uint particleIndex)
{
    const uint offset = GetFluidBlockParticleIndexOffset(blockIndexLevel0, particleIndex);
    return FluidBlockParticleIndices[offset];
}

// return: the allocated level n-1 index
uint ActivateFluidBlockLevel0(uint3 blockIndexLevel1, uint3 gridPositionLevel1, bool atomicFlag)
{
    uint indexSublevel = FluidGridLevel1[blockIndexLevel1];
    if (!IsValidFluidBlockIndex(indexSublevel))
        return indexSublevel;

    uint acquired;
    const uint atomicIndex = EncodeFluidBlockIndexLevel1(blockIndexLevel1);
    InterlockedCompareExchange(
        FluidGridAtomicLock[atomicIndex + FLUID_GRID_ATOMIC_LOCK_OFFSET_LEVEL1],
        atomicFlag, !atomicFlag, acquired);
    
    if (acquired == atomicFlag)
    {
        InterlockedAdd(FluidBlockParticleIndices[GetFluidBlockCountOffset(0)], 1, indexSublevel);
        FluidGridLevel1[blockIndexLevel1] = indexSublevel;

        uint offset = GetFluidBlockPositionOffset(indexSublevel);
        FluidBlockParticleIndices[offset] = gridPositionLevel1.x;
        FluidBlockParticleIndices[offset +1] = gridPositionLevel1.y;
        FluidBlockParticleIndices[offset +2] = gridPositionLevel1.z;

        offset = GetFluidBlockParticleCountOffset(indexSublevel);
        FluidBlockParticleIndices[offset] = 0;
    }
    else
    {
        do
        {
            indexSublevel = FluidGridLevel1[blockIndexLevel1];
        } while (IsValidFluidBlockIndex(indexSublevel));
    }

    return indexSublevel;
}

// return: the allocated level n-1 index
uint ActivateFluidBlockLevel1(uint3 gridIndexLevel2)
{
    uint indexSublevel = FluidGridLevel2[gridIndexLevel2];
    if (!IsValidFluidBlockIndex(indexSublevel))
        return indexSublevel;

    uint initialized;
    const uint atomicIndex = EncodeFluidBlockIndexLevel2(gridIndexLevel2);
    InterlockedCompareExchange(
        FluidGridAtomicLock[atomicIndex + FLUID_GRID_ATOMIC_LOCK_OFFSET_LEVEL2],
        0, 1, initialized);

    if (!initialized)
    {
        InterlockedAdd(FluidBlockParticleIndices[GetFluidBlockCountOffset(1)], 1, indexSublevel);
        FluidGridLevel2[gridIndexLevel2] = indexSublevel;
    }
    else
    {
        do
        {
            indexSublevel = FluidGridLevel2[gridIndexLevel2];
        } while (IsValidFluidBlockIndex(indexSublevel));
    }

    return indexSublevel;
}

bool ActivateFluidBlock(int3 gridPositionLevel0, bool atomicFlag, out uint blockIndexLevel0)
{
    if (any(gridPositionLevel0 < 0 || gridPositionLevel0 >= FLUID_VIRTUAL_GRID_EXTENT - FLUID_BLOCK_SIZE_LEVEL0))
        return false;

    uint3 gridPositionLevel1;
    uint3 gridPositionLevel2;
    uint3 gridOffsetLevel1;
    GetFluidGridPositions(gridPositionLevel0, gridPositionLevel1, gridPositionLevel2, gridOffsetLevel1);

    const uint blockIndexLevel1Encoded = ActivateFluidBlockLevel1(gridPositionLevel2);
    const uint3 gridIndexLevel1 = DecodeFluidBlockIndexLevel1(blockIndexLevel1Encoded) * FLUID_BLOCK_SIZE_LEVEL1 + gridOffsetLevel1;

    blockIndexLevel0 = ActivateFluidBlockLevel0(gridIndexLevel1, gridPositionLevel1, atomicFlag);
    return true;
}

// doesn't check whether the block is allocated
uint GetFluidBlockIndexLevel0Unchecked(uint3 gridPositionLevel1)
{
    const uint3 gridPositionLevel2 = gridPositionLevel1 / FLUID_BLOCK_SIZE_LEVEL1;
    const uint3 gridOffsetLevel1 = gridPositionLevel1 % FLUID_BLOCK_SIZE_LEVEL1;

    const uint blockIndexLevel1Encoded = FluidGridLevel2[gridPositionLevel2];
    const uint3 gridIndexLevel1 = DecodeFluidBlockIndexLevel1(blockIndexLevel1Encoded) * FLUID_BLOCK_SIZE_LEVEL1 + gridOffsetLevel1;

    return FluidGridLevel1[gridIndexLevel1];
}

void AddParticleToFluidBlock(int3 gridPositionLevel0, uint particleIndex, bool atomicFlag)
{
    uint blockIndexLevel0;
    if (!ActivateFluidBlock(gridPositionLevel0, atomicFlag, blockIndexLevel0))
        return;

    const uint particleCountOffset = GetFluidBlockParticleCountOffset(blockIndexLevel0);

    uint particleCount;
    InterlockedAdd(FluidBlockParticleIndices[particleCountOffset], 1, particleCount);

    if (particleCount < FLUID_BLOCK_MAX_PARTICLE_COUNT)
    {
        const uint particleOffset = GetFluidBlockParticleIndexOffset(blockIndexLevel0, particleCount);
        FluidBlockParticleIndices[particleOffset] = particleIndex;
    }
}

/// end grid access