#pragma once

#define MAX_DEBUG_LOG_COUNT 512

#define LOG_TYPE_INT 0
#define LOG_TYPE_UINT 1
#define LOG_TYPE_FLOAT 2
#define LOG_TYPE_BOOL 3

#define LOG_TYPE_INT2 4
#define LOG_TYPE_UINT2 5
#define LOG_TYPE_FLOAT2 6
#define LOG_TYPE_BOOL2 7

#define LOG_TYPE_INT3 8
#define LOG_TYPE_UINT3 9
#define LOG_TYPE_FLOAT3 10
#define LOG_TYPE_BOOL3 11

#define LOG_TYPE_INT4 12
#define LOG_TYPE_UINT4 13
#define LOG_TYPE_FLOAT4 14
#define LOG_TYPE_BOOL4 15

RWByteAddressBuffer DebugBuffer;

#define DEBUG_BUFFER_CONCAT(a, b) a##b

#define ALLOCATE_DEBUG_BUFFER(name, count, success) \
uint name; \
bool success; \
{ \
    const uint __count = (count); \
    DebugBuffer.InterlockedAdd(0, __count, name); \
    success = name + __count < MAX_DEBUG_LOG_COUNT; \
    name = name * 8 + 8; \
}

#define DEF_DEBUG_LOG_FUNC(log_type, type, op) \
void DebugLog(type value) \
{ \
    ALLOCATE_DEBUG_BUFFER(offset, 1, success); \
    if (success) \
        DebugBuffer.Store2(offset, uint2(LOG_TYPE_##log_type, op(value))); \
} \
void DebugLog(type##2 value) \
{ \
    ALLOCATE_DEBUG_BUFFER(offset, 2, success); \
    if (success) \
        DebugBuffer.Store4(offset, uint4(DEBUG_BUFFER_CONCAT(LOG_TYPE_, DEBUG_BUFFER_CONCAT(log_type, 2)), op(value), 0)); \
} \
void DebugLog(type##3 value) \
{ \
    ALLOCATE_DEBUG_BUFFER(offset, 2, success); \
    if (success) \
        DebugBuffer.Store4(offset, uint4(DEBUG_BUFFER_CONCAT(LOG_TYPE_, DEBUG_BUFFER_CONCAT(log_type, 3)), op(value))); \
} \
void DebugLog(type##4 value) \
{ \
    ALLOCATE_DEBUG_BUFFER(offset, 3, success); \
    if (success) \
    { \
        DebugBuffer.Store4(offset, uint4(DEBUG_BUFFER_CONCAT(LOG_TYPE_, DEBUG_BUFFER_CONCAT(log_type, 4)), op(value.xyz))); \
        DebugBuffer.Store2(offset + 16, uint2(op(value.w), 0)); \
    } \
}

DEF_DEBUG_LOG_FUNC(INT, int, )
DEF_DEBUG_LOG_FUNC(UINT, uint, )
DEF_DEBUG_LOG_FUNC(FLOAT, float, asuint)
DEF_DEBUG_LOG_FUNC(BOOL, bool, )

#undef DEF_DEBUG_LOG_FUNC
#undef ALLOCATE_DEBUG_BUFFER
#undef LOG_TYPE_CONCAT
