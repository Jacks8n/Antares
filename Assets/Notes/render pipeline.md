## Flow Map
```flow
bg=>start: Begin Render
ed=>end: End Render
mod=>operation: Modify SDFs' Mip 0
modm=>operation: Modify volume material
mip=>operation: Update SDFs' Mip Map
clr=>operation: Clear Render Targets
cul=>operation: Cull mesh (GPU)
culs=>parallel: Cull SDFs (CPU)*
op=>operation: Draw Opaque Mesh to GBuffers
opd=>parallel: Draw Opaque Mesh to Depth
tr=>operation: Draw Transparent Mesh (Forward)
sdfb=>operation: Draw SDF Bounds
rm=>operation: Raymarch SDFs to GBuffers, Depth
sd=>operation: Opaque Shading
bl=>operation: Blending
pp=>operation: Post Processing
ui=>operation: Draw UI

bg->clr
clr->culs
culs(path1,bottom)->mod->mip->rm->cul->opd
culs(path2,right)->modm->rm
opd(path1,bottom)->op->sd->bl
opd(path2,left)->tr->bl
bl->pp->ui
ui->ed
```

## Lighting
Whole pipeline is performed inside the linear color space, enabling hdr, pbr based
Thus lights are written into volume material being mixed with other materials, no extra light pass is performed

However, it's not compatible with mesh shadows, since shadow map is difficult to be rendered with "volume light"

## Culling
Due to the complexity and majority of SDFs, culling is to be done on gpu

## Modifying SDFs
Types of modification
1. Physical Deformation
2. Animation

TODO how to contact between SDF and physical engine

### SDF Animation
How to perform transition for robust SDFs? Interpolation between SDF key frames works poorly, on the other hand, more key frames introduce more memory usage

support for transparent?

how to render particles?

aalaton benchmarked sparse sdf with 8\*8*8 virtual texture, then the performance is impacted by about 13%, whether and how to take the tradeoff?

instead of drawing sdf bounds by rendering standalone, stroing bounds into cbuffer and computing may be feasible

claybook didn't rm within cs, but ps

how to assign different materials to arbitrary spaces?
claybook uses 1/4 volume texture, trilinear filtering

sdf should be nearest 3d tex or 2darray to avoid filtering
