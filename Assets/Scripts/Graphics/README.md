﻿[TOC]

# Overview of `ARenderPipeline`(Provisional)
## 1. Basic Concepts
- Scene Volume: A `Texture3D` representing SDF grid.
- Material Volume: A `Texture3D` indexing into materials.
- Supremum: Limited by precision of scene volume, numerical SDFs are bounded practically.
- Shape: A geometry, which is either analytical built-in or `Texture3D`.
- Brush: An aggregate of shape, transform, material.

### 1.1 Scene/Material Volume
A scene volume is mipmapped `snorm R8` `Texture3D`, which is sampled with bilinear sampler. Any length of its edge must be multiple of `MIN_SCENE_SIZE` due to the thread size of compute shader kernel.

A material volume is mipmapped `uint R16` `Texture3D`, whose mipmap level is same as paired scene volume. 
>Note: Non-zero mips of material volume have different meaning.

Both volumes are generated on the fly.

### 1.2 Shape/Brush
There are analytical/numerical shapes, both of which are represented by several floating numbers.

To assist the edit of scene, `BrushHelper` is introduced. Objects attached with it will be tagged as `EditOnly`. Later `BrushCollection` gathers used shapes and serialize them into an file.

## 2. Pipeline Stages
### 2.1 Generate Scene
The material volume and scene volume are generated on loading and modified on the fly. The implementation is at `SDFGeneration.compute`.

`GenerateMatVolume` first culls brushes in material volume tile scale, then culls in material volume grid scale. Indices to culled brushes are linearly and compactly stored to an index buffer, and mip 0 of the material volume stores offsets at the index buffer.
For non-empty material grids, their coordinates are stored to a dispatch buffer for dispatch usage. Meanwhile, a global counter counts the number of grids and yields the dispatch thread gorup of `GenerateSceneVolume`.

`GenerateMipDispatch` is dispatched multiple times to generate non-zero mips of material volume. It reads previous mip level and checks if a mip update is required. When an update needs to be done, its coordinate is stored in a way similar to `GenerateMatVolume`.

`GenerateSceneVolume` first reads its associated coordinate in material volume from the dispatch buffer generated by `GenerateMatVolume`, then use the coordinate to read index buffer offset from mip 0 material volume, finally indexed brushes are loaded.
Brushes are evaluated at each scene volume grid and interpolated with `SmoothMin`.

`GenerateMipMap` is dispatched multiple times to generate non-zero mips of scene volume. The indirection steps is similar to `GenerateSceneVolume`, then it average the previous mips to attain half-band sdf which is enlarged by solving eikonal equation numerically.

### 2.2 Render Scene

#### 2.2.1 Ray Marching

#### 2.2.2 Post-processing