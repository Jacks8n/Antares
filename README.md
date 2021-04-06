# Antares

## To-Do
- debug(almost finished)
    - sdf generation
    - ray marching    
- simulation(in progress)
    - pbd, sdf-particle/particle-particle
- [sdf, simulation] bake
    - openvdb integration?
- shader optimization
    - benchmark
    - cbuffer layout
    - bank conflict
- rendering module
    - material
    - shadow
    - ao
    - tone mapping

## Known Issues
1. non-serialized reference types that are initialized in `ScriptableObject.OnEnable()` become `null` after initialization of editor
2. `ComputeBuffer` created with `ComputeBufferMode.SubUpdate` can't be set via `ComputeBuffeer.SetData()` and `CommandBuffer.SetComputeBufferData()`
3. `ComputeBuffer` created with `ComputeBufferMode.SubUpdate` or `ComputeBufferMode.Dynamic` can't be bound as UAV
4. UBO must not be smaller than 16 bytes (dx11)
