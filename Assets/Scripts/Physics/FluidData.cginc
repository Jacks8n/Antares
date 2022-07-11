// this module contains data structures that might be needed by other modules and functions to access them.

// todo
// handle the case that the number of blocks exceeds the limit

#pragma once

#include "../Graphics/UAVSupport.cginc"

#define FLUID_MAX_PARTICLE_COUNT (1 << 20)

#define FLUID_EOS_BULK_MODULUS 10.0
#define FLUID_EOS_REST_DENSITY 8.0
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

// layout: { indirect arg 0, particle count, indirect args{2}, ping-pong flag, prefix sum of particle count, (indexed position{n}){2} }
// initial value: { 1, n, 0, 0, 1, 0, <valid indexed position>{n}, x{n} }
extern A_RWBUFFER(uint) FluidParticlePositions;
// layout: { ((alignas(32) particle properties){n} }
// initial value: { <valid particle properties>{n}, x* }
extern A_RWBYTEADDRESS_BUFFER FluidParticleProperties;

/// begin particle access

uint GetFluidParticleCountOffset()
{
    return 1;
}

// to parallelize sorting particles, a buffer whose size is twice as the number of particles are used.
// the buffer is splitted into low and high parts. and this function returns whether the high part is
// used at the beginning of each iteration.
uint GetFluidParticlePositionPingPongFlagOffset()
{
    return 4;
}

uint GetFluidParticleCountPrefixSumOffset()
{
    return GetFluidParticlePositionPingPongFlagOffset() + 1;
}

uint GetFluidParticlePositionOffset(uint index, bool pingpong)
{
    const uint stride = 4; // compressed size of ParticlePositionIndexed / 4
    const uint pingpongOffset = pingpong ? FLUID_MAX_PARTICLE_COUNT : 0;
    return GetFluidParticleCountPrefixSumOffset() + 1 + (index + pingpongOffset) * stride;
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

uint GetFluidParticleCountPrefixSum()
{
    const uint offset = GetFluidParticleCountPrefixSumOffset();
    return FluidParticlePositions[offset];
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

#ifndef A_UAV_READONLY

    void SetFluidParticleCount(uint value)
    {
        const uint offset = GetFluidParticleCountOffset();
        FluidParticlePositions[offset] = value;
    }

    void SetFluidParticlePositionPingPongFlag(bool flag)
    {
        const uint offset = GetFluidParticlePositionPingPongFlagOffset();
        FluidParticlePositions[offset] = uint(flag);
    }

    void SetFluidParticleCountPrefixSum(uint value)
    {
        const uint offset = GetFluidParticleCountPrefixSumOffset();
        FluidParticlePositions[offset] = value;
    }

    uint AccumulateParticleCountAtomic(uint particleCount)
    {
        const uint offset = GetFluidParticleCountPrefixSumOffset();

        uint count;
        InterlockedAdd(FluidParticlePositions[offset], particleCount, count);

        return count;
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

#endif

uint GetFluidParticlePropertiesByteOffset(uint index)
{
    return index * 32;
}

ParticleProperties GetFluidParticleProperties(uint index)
{
    const uint offset = GetFluidParticlePropertiesByteOffset(index);

    const uint4 word0 = FluidParticleProperties.Load4(offset);
    const uint4 word1 = FluidParticleProperties.Load4(offset +16);

    const float4 word0lo = f16tof32(word0);
    const float4 word0hi = f16tof32(word0 >> 16);
    const float4 word1lo = f16tof32(word1);
    const float4 word1hi = f16tof32(word1 >> 16);

    ParticleProperties properties;
    properties.AffineRow0 = word0lo.xyz;
    properties.VelocityX = word0lo.w;
    properties.AffineRow1 = word0hi.xyz;
    properties.VelocityY = word0hi.w;
    properties.AffineRow2 = word1lo.xyz;
    properties.VelocityZ = word1hi.x;
    properties.Mass = word1hi.w;

    return properties;
}

#ifndef A_UAV_READONLY

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
        FluidParticleProperties.Store4(offset +16, word1);
    }

    void SetFluidParticleAffineVelocity(uint index, float3x3 affine, float3 velocity)
    {
        const uint offset = GetFluidParticlePropertiesByteOffset(index);

        const uint4 word0lo = f32tof16(float4(affine._m00_m01_m02, velocity.x));
        const uint4 word0hi = f32tof16(float4(affine._m10_m11_m12, velocity.y)) << 16;
        const uint4 word1lohi = f32tof16(float4(affine._m20_m21_m22, velocity.z));

        const uint4 word0 = word0lo | word0hi;
        const uint3 word1 = uint3(word1lohi.x | (word1lohi.w << 16), word1lohi.yz);

        FluidParticleProperties.Store4(offset, word0);
        FluidParticleProperties.Store3(offset +16, word1);
    }

#endif

/// end particle access

// it must be power of two
#define FLUID_BLOCK_SIZE_LEVEL0 4
#define FLUID_BLOCK_GRID_COUNT_LEVEL0 (FLUID_BLOCK_SIZE_LEVEL0 * FLUID_BLOCK_SIZE_LEVEL0 * FLUID_BLOCK_SIZE_LEVEL0)
#define FLUID_BLOCK_COUNT_X_LEVEL0 64
#define FLUID_BLOCK_COUNT_Y_LEVEL0 32
#define FLUID_BLOCK_COUNT_Z_LEVEL0 32
#define FLUID_GRID_COUNT_X_LEVEL0 (FLUID_BLOCK_COUNT_X_LEVEL0 * FLUID_BLOCK_SIZE_LEVEL0)
#define FLUID_GRID_COUNT_Y_LEVEL0 (FLUID_BLOCK_COUNT_Y_LEVEL0 * FLUID_BLOCK_SIZE_LEVEL0)
#define FLUID_GRID_COUNT_Z_LEVEL0 (FLUID_BLOCK_COUNT_Z_LEVEL0 * FLUID_BLOCK_SIZE_LEVEL0)
#define FLUID_GRID_COUNT_LEVEL0 (FLUID_GRID_COUNT_X_LEVEL0 * FLUID_GRID_COUNT_Y_LEVEL0 * FLUID_GRID_COUNT_Z_LEVEL0)
#define FLUID_BLOCK_COUNT_LEVEL0 (FLUID_BLOCK_COUNT_X_LEVEL0 * FLUID_BLOCK_COUNT_Y_LEVEL0 * FLUID_BLOCK_COUNT_Z_LEVEL0)
#define FLUID_GRID_EXTENT_LEVEL0 (FLUID_BLOCK_SIZE_LEVEL0 * uint3(FLUID_BLOCK_COUNT_X_LEVEL0, FLUID_BLOCK_COUNT_Y_LEVEL0, FLUID_BLOCK_COUNT_Z_LEVEL0))

#define FLUID_BLOCK_SIZE_LEVEL1 4
#define FLUID_BLOCK_GRID_COUNT_LEVEL1 (FLUID_BLOCK_SIZE_LEVEL1 * FLUID_BLOCK_SIZE_LEVEL0 * FLUID_BLOCK_SIZE_LEVEL0)
#define FLUID_BLOCK_COUNT_X_LEVEL1 64
#define FLUID_BLOCK_COUNT_Y_LEVEL1 32
#define FLUID_BLOCK_COUNT_Z_LEVEL1 32
#define FLUID_GRID_COUNT_X_LEVEL1 (FLUID_BLOCK_COUNT_X_LEVEL1 * FLUID_BLOCK_SIZE_LEVEL1)
#define FLUID_GRID_COUNT_Y_LEVEL1 (FLUID_BLOCK_COUNT_Y_LEVEL1 * FLUID_BLOCK_SIZE_LEVEL1)
#define FLUID_GRID_COUNT_Z_LEVEL1 (FLUID_BLOCK_COUNT_Z_LEVEL1 * FLUID_BLOCK_SIZE_LEVEL1)
#define FLUID_GRID_COUNT_LEVEL1 (FLUID_GRID_COUNT_X_LEVEL1 * FLUID_GRID_COUNT_Y_LEVEL1 * FLUID_GRID_COUNT_Z_LEVEL1)
#define FLUID_BLOCK_COUNT_LEVEL1 (FLUID_BLOCK_COUNT_X_LEVEL1 * FLUID_BLOCK_COUNT_Y_LEVEL1 * FLUID_BLOCK_COUNT_Z_LEVEL1)
#define FLUID_GRID_EXTENT_LEVEL1 (FLUID_BLOCK_SIZE_LEVEL1 * uint3(FLUID_BLOCK_COUNT_X_LEVEL1, FLUID_BLOCK_COUNT_Y_LEVEL1, FLUID_BLOCK_COUNT_Z_LEVEL1))

// though it's named 'block', it represents each cell
#define FLUID_GRID_COUNT_X_LEVEL2 256
#define FLUID_GRID_COUNT_Y_LEVEL2 256
#define FLUID_GRID_COUNT_Z_LEVEL2 256
#define FLUID_GRID_COUNT_LEVEL2 (FLUID_GRID_COUNT_X_LEVEL2 * FLUID_GRID_COUNT_Y_LEVEL2 * FLUID_GRID_COUNT_Z_LEVEL2)
#define FLUID_GRID_EXTENT_LEVEL2 uint3(FLUID_GRID_COUNT_X_LEVEL2, FLUID_GRID_COUNT_Y_LEVEL2, FLUID_GRID_COUNT_Z_LEVEL2)

#define FLUID_VIRTUAL_GRID_EXTENT (FLUID_BLOCK_SIZE_LEVEL0 * FLUID_BLOCK_SIZE_LEVEL1 * FLUID_GRID_EXTENT_LEVEL2 - 2)

#define FLUID_GRID_ATOMIC_LOCK_OFFSET_LEVEL1 0
#define FLUID_GRID_ATOMIC_LOCK_OFFSET_LEVEL2 (FLUID_GRID_ATOMIC_LOCK_OFFSET_LEVEL1 + FLUID_BLOCK_COUNT_LEVEL0)

// n.b.: remember to update here if the number of per block info is changed
#define FLUID_BLOCK_PARTICLE_STRIDE 64
#define FLUID_BLOCK_MAX_PARTICLE_COUNT (FLUID_BLOCK_PARTICLE_STRIDE - 5)

// mass
#define FLUID_GRID_CHANNEL0_OFFSET uint3(0, 0, 0)
// velocity/momentum
#define FLUID_GRID_CHANNEL1_OFFSET uint3(FLUID_BLOCK_COUNT_X_LEVEL0 * FLUID_BLOCK_SIZE_LEVEL0, 0, 0)
#define FLUID_GRID_CHANNEL2_OFFSET uint3(FLUID_BLOCK_COUNT_X_LEVEL0 * FLUID_BLOCK_SIZE_LEVEL0 * 2, 0, 0)
#define FLUID_GRID_CHANNEL3_OFFSET uint3(FLUID_BLOCK_COUNT_X_LEVEL0 * FLUID_BLOCK_SIZE_LEVEL0 * 3, 0, 0)
// sdf
#define FLUID_GRID_CHANNEL4_OFFSET uint3(FLUID_BLOCK_COUNT_X_LEVEL0 * FLUID_BLOCK_SIZE_LEVEL0 * 4, 0, 0)

// values are nomalized and converted to integer to perform atomic operations
// 1073741824, largest 32-bit signed integer whose reciprocal can be exactly represented by 32-bit float
#define FLUID_GRID_ENCODED_VALUE_MAX int(1 << 30)
#define FLUID_GRID_BOUND_MASS 512.0
#define FLUID_GRID_BOUND_MOMENTUM 2048.0
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

int3 EncodeFluidGridMomentum(float3 momentum)
{
    return EncodeGridValue(momentum, FLUID_GRID_BOUND_MOMENTUM, -1.0, 1.0);
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
//  level 1  : 64*32*32 | 4x4x4 nodes | 64mb
//  level 2  : 256^3    | dense root  | 64mb
//  if dense : 4096^3   | dense grids | 1tb, which is impractical
//
// virtual volume  : (256*4*4*5cm)^3 = (204.8m)^3
// physical volume : (64*4*5cm)^3 = (12.8m)^3

// initial value: 0
extern A_RWTEXTURE3D(int) FluidGridLevel0;
// initial value: 0
extern A_RWTEXTURE3D(int) FluidGridLevel1;
// initial value: 0
extern A_RWTEXTURE3D(int) FluidGridLevel2;

// layout:
// {
//   level 0 block count, indirect groups.yz, level 1 block count, indirect groups.yz,
//   {block position, particle count, prefix sum of particle count, particle index*}*
// }
// initial value:
// { 0, 1, 1, 0, 1, 1, {x{3}, 0, 0, 0*}* }
extern A_RWBUFFER(uint) FluidBlockParticleIndices;

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

uint GetGridIndexLinear(uint3 index, uint2 blockSizeXY)
{
    const uint3 offset = uint3(1, blockSizeXY.x, blockSizeXY.x * blockSizeXY.y);
    return dot(offset, index);
}

uint3 GetGridIndexSpatial(uint index, uint2 blockSizeXY)
{
    const uint x = index % blockSizeXY.x;
    const uint y = (index / blockSizeXY.x) % blockSizeXY.y;
    const uint z = index / (blockSizeXY.x * blockSizeXY.y);

    return uint3(x, y, z);
}

uint EncodeFluidBlockIndex(uint index)
{
    return (1u << 31) | index;
}

uint DecodeFluidBlockIndex(uint index)
{
    return ((1u << 31) - 1) & index;
}

// there are some limits on the dimension of textures, so non-zero-level blocks are
// not arranged in a long-cube manner. consequently encoding/decoding is required.
// it forms a bijection between continuous indices and indices of grid blocks.
#define DEF_LINEARIZE_FLUID_BLOCK_INDEX_FUNC(level) \
uint GetGridIndexLinearLevel##level(uint3 index) \
{ \
    return GetGridIndexLinear(index, uint2(FLUID_BLOCK_COUNT_X_LEVEL##level, FLUID_BLOCK_COUNT_Y_LEVEL##level)); \
} \
uint3 GetFluidBlockIndexSpatialLevel##level(uint index) \
{ \
    return GetGridIndexSpatial(index, uint2(FLUID_BLOCK_COUNT_X_LEVEL##level, FLUID_BLOCK_COUNT_Y_LEVEL##level)); \
}

DEF_LINEARIZE_FLUID_BLOCK_INDEX_FUNC(0)
DEF_LINEARIZE_FLUID_BLOCK_INDEX_FUNC(1)

#undef DEF_LINEARIZE_FLUID_BLOCK_INDEX_FUNC

#define DEF_LINEARIZE_FLUID_GRID_INDEX_FUNC(level) \
uint GetFluidGridIndexLinearLevel##level(uint3 index) \
{ \
    return GetGridIndexLinear(index, uint2(FLUID_GRID_COUNT_X_LEVEL##level, FLUID_GRID_COUNT_Y_LEVEL##level)); \
} \
uint3 GetFluidGridIndexSpatialLevel##level(uint index) \
{ \
    return GetGridIndexSpatial(index, uint2(FLUID_GRID_COUNT_X_LEVEL##level, FLUID_GRID_COUNT_Y_LEVEL##level)); \
}

DEF_LINEARIZE_FLUID_GRID_INDEX_FUNC(0)
DEF_LINEARIZE_FLUID_GRID_INDEX_FUNC(1)
DEF_LINEARIZE_FLUID_GRID_INDEX_FUNC(2)

#undef DEF_LINEARIZE_FLUID_GRID_INDEX_FUNC

#define DEF_LINEARIZE_FLUID_BLOCK_GRID_INDEX_FUNC(level) \
uint GetFluidBlockGridIndexLinearLevel##level(uint3 index, uint3 padding) \
{ \
    const uint3 size = FLUID_BLOCK_SIZE_LEVEL##level + padding; \
    return GetGridIndexLinear(index, size.xy); \
} \
uint GetFluidBlockGridIndexLinearLevel##level(uint3 index, uint padding) \
{ \
    return GetFluidBlockGridIndexLinearLevel##level(index, padding.xxx); \
} \
uint GetFluidBlockGridIndexLinearLevel##level(uint3 index) \
{ \
    return GetFluidBlockGridIndexLinearLevel##level(index, 0); \
} \
uint3 GetFluidBlockGridIndexSpatialLevel##level(uint index, uint3 padding) \
{ \
    const uint3 size = FLUID_BLOCK_SIZE_LEVEL##level + padding; \
    return GetGridIndexSpatial(index, size.xy); \
} \
uint3 GetFluidBlockGridIndexSpatialLevel##level(uint index, uint padding) \
{ \
    return GetFluidBlockGridIndexSpatialLevel##level(index, padding.xxx); \
} \
uint3 GetFluidBlockGridIndexSpatialLevel##level(uint index) \
{ \
    return GetFluidBlockGridIndexSpatialLevel##level(index, 0); \
}

DEF_LINEARIZE_FLUID_BLOCK_GRID_INDEX_FUNC(0)
DEF_LINEARIZE_FLUID_BLOCK_GRID_INDEX_FUNC(1)

#undef DEF_LINEARIZE_FLUID_BLOCK_GRID_INDEX_FUNC

bool IsValidFluidBlockIndex(uint index)
{
    const uint msb = 1 << 31;
    return (index & msb) != 0;
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

uint3 GetFluidBlockPosition(uint blockIndexLevel0)
{
    const uint offset = GetFluidBlockPositionOffset(blockIndexLevel0);
    return uint3(
        FluidBlockParticleIndices[offset],
        FluidBlockParticleIndices[offset +1],
        FluidBlockParticleIndices[offset +2]
    );
}

uint GetFluidBlockParticleCount(uint blockIndexLevel0)
{
    const uint offset = GetFluidBlockParticleCountOffset(blockIndexLevel0);
    return min(FluidBlockParticleIndices[offset], FLUID_BLOCK_MAX_PARTICLE_COUNT);
}

uint GetFluidBlockParticleCountPrefixSum(uint blockIndex)
{
    const uint offset = GetFluidBlockParticleCountPrefixSumOffset(blockIndex);
    return FluidBlockParticleIndices[offset];
}

void GetFluidBlockInfo(uint blockIndexLevel0, out uint3 gridPositionLevel1, out uint particleCount)
{
    gridPositionLevel1 = GetFluidBlockPosition(blockIndexLevel0);
    particleCount = GetFluidBlockParticleCount(blockIndexLevel0);
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

// doesn't check whether the block is allocated
uint GetFluidBlockIndexLevel0Unchecked(uint3 gridPositionLevel1)
{
    const uint3 gridPositionLevel2 = gridPositionLevel1 / FLUID_BLOCK_SIZE_LEVEL1;
    const uint3 gridOffsetLevel1 = gridPositionLevel1 % FLUID_BLOCK_SIZE_LEVEL1;

    const uint blockIndexLevel1Linear = DecodeFluidBlockIndex(FluidGridLevel2[gridPositionLevel2]);
    const uint3 gridIndexLevel1 = GetFluidBlockIndexSpatialLevel1(blockIndexLevel1Linear) * FLUID_BLOCK_SIZE_LEVEL1 + gridOffsetLevel1;

    return FluidGridLevel1[gridIndexLevel1];
}

#ifndef A_UAV_READONLY

    void SetFluidBlockParticleCountPrefixSum(uint blockIndex, uint prefixSum)
    {
        const uint offset = GetFluidBlockParticleCountPrefixSumOffset(blockIndex);
        FluidBlockParticleIndices[offset] = prefixSum;
    }

#endif

/// end grid access
