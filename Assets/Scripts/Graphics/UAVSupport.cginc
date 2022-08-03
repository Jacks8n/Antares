// supported macros:
// A_UAV_READONLY: when defined, uavs are replaced by readonly resource types

#pragma once

#ifdef A_UAV_READONLY
#define A_RWSTORAGE(storage)
#define A_RWTEXTURE2D(type) Texture2D<type>
#define A_RWTEXTURE3D(type) Texture3D<type>
#define A_RWBUFFER(type) Buffer<type>
#define A_RWSTRUCTURED_BUFFER(type) StructuredBuffer<type>
#define A_RWBYTEADDRESS_BUFFER ByteAddressBuffer
#else
#define A_RWSTORAGE(storage) storage
#define A_RWTEXTURE2D(type) RWTexture2D<type>
#define A_RWTEXTURE3D(type) RWTexture3D<type>
#define A_RWBUFFER(type) RWBuffer<type>
#define A_RWSTRUCTURED_BUFFER(type) RWStructuredBuffer<type>
#define A_RWBYTEADDRESS_BUFFER RWByteAddressBuffer
#endif
