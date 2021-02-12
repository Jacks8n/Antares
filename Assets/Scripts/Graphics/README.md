[TOC]

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

To assist the edit of scene, `BrushHelper` is introduced. Objects attached with it will be tagged as EditOnly. Later `BrushCollection` gathers used shapes and serialize them into an file.

## Specification Constants
| Name | Value | Comment|
|---|---|---|
| GENERATE_MAT_KERNEL_SIZE | 4 |
| MATERIAL_GRID_SIZE | 4 |how many times scene volume is big as material volume|
| MIN_SCENE_SIZE | GENERATE_MAT_KERNEL_SIZE * MATERIAL_GRID_SIZE |
