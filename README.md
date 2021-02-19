# Antares

## To-Do
- debug(in progress)
    - sdf generation
    - ray marching    
- simulation
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
