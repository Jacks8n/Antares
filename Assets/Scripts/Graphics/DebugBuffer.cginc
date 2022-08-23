#pragma once

#define MAX_DEBUG_LOG_COUNT 128

#define LOG_TYPE_INT 0
#define LOG_TYPE_UINT 1
#define LOG_TYPE_FLOAT 2
#define LOG_TYPE_BOOL 3

RWByteAddressBuffer DebugBuffer;

void TryAddDebugLog(uint2 word)
{
    int offset;
    DebugBuffer.InterlockedAdd(0, 1, offset);

    if (offset < MAX_DEBUG_LOG_COUNT)
    {
        offset = offset * 8 + 8;
        DebugBuffer.Store2(offset, word);
    }
}

void DebugLog(int value)
{
    TryAddDebugLog(uint2(LOG_TYPE_INT, value));
}

void DebugLog(uint value)
{
    TryAddDebugLog(uint2(LOG_TYPE_UINT, value));
}

void DebugLog(float value)
{
    TryAddDebugLog(uint2(LOG_TYPE_FLOAT, asuint(value)));
}

void DebugLog(bool value)
{
    TryAddDebugLog(uint2(LOG_TYPE_INT, value));
}
