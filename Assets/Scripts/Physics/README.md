# Physics (Under Development)
## 1 Overview
The physics simulation module adopts the idea of material point method, a hybrid eulerian-lagrangian method. Read `FluidSolver.compute` for detailed references.

Illustration of one physics frame:
```dot
digraph PhysicsFrame {
    size=8

    prev [label="Previous Frame", shape=none]
    sort_particles [label="Sort Particles", shape=box]
    generate_indirect_args [label="Generate Indirect Args", shape=box]
    p2g [label="Particle to Grid", shape=box]
    pressure [label="Solve Pressure", shape=box]
    solve0 [label="Solve Level 0 Grids", shape=box]
    solve1 [label="Solve Level 1 Grids", shape=box]
    g2p [label="Grid to Particle", shape=box]
    clear [label="Clear Grids", shape=box]
    next [label="Next Frame", shape=none]
    collision [label="Detect Particle Collision", shape=box]

    add_particles [label="Add Particles", shape=box]
    update_particles [label="Update Particle Properties", shape=box]

    render [label="Render Fluid", shape=box]

    update_particles->add_particles
    add_particles->generate_indirect_args [style=dotted]

    prev->generate_indirect_args
    generate_indirect_args->sort_particles
    sort_particles->p2g
    p2g->pressure
    pressure->solve0
    pressure->solve1
    solve0->g2p
    solve1->g2p
    g2p->clear
    clear->next

    sort_particles->collision [style=dotted]
    p2g->render [style=dotted]

    {rank=same;generate_indirect_args;add_particles}
}
```
*GraphViz is required to render the graph above*

For better understanding, it's recommended to read the source code. Since docs might fall behind the source code.

## 2 Memory Footprints
### 2.1 Global Memory
Most of the textures and buffers that involve in simulation are defined in `FluidSolver.compute` and `FluidData.cginc`. You can search "extern" to find these definitions.

Before continuing, some of important optimizations must be explained:
1. As pointed out by [Gao et al.](https://doi.org/10.1145/3272127.3275044), it's more optimal to store particles' position in a standalone buffer, seperated from other quantities.
2. Not all lagrangian grids are finest. Similar to [SPGrid](https://pages.cs.wisc.edu/~sifakis/project_pages/SPGrid.html), multi-resolution scheme is opted.

### 2.2 Group Shared Memory
Group shared memories are postfixed by `_GSM`.

## 3 CRUD of Particles
To better parallelize simulation, Particles are stored compactly within one buffer, see `FluidProcess.cginc` for detailed layout.

### 3.1 Read/Update
These two operations can be done by directly indexing into the buffer.

### 3.2 Create/Delete
Since CPU might want to directly create/delete particles, these particles are transferred to a buffer to which afterwards GPU issues requests. Actual create/delete are fully handled by GPU and processed within one kernel:
1. Newly created particles simply overwrite particles that is to be removed.
2. If created particles are more than removed particles, exceeding part is simply appended to the particle buffer.
3. If removed particles are more than created particles, *stream compaction* is needed, which maintains the compactness of buffer. It is carried out in subsequent dispatches.
