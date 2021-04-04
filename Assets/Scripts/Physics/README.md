[TOC]

# Physics (Under Development)
## 1. Objectives
1. Fluid simulation
2. Point cloud collision
3. Constraints
3. Triggers
4. Cloth simulation*

## 1.1 Detail
1. Creature
    1. Point cloud based (mesh)
    2. Animation compatible
    3. Joint, IK    
2. Environment
    1. Modification
3. Fluid
    1. Viscose

## 2. Passes
1. Update point cloud (mesh)
2. Notify triggers*
3. Solve constraints
    Particle <-> SDF
4. Shape matching [^sm]
5. SPH
    Particle <-> Particle
    Particle <-> SDF
6. Generate fluid sdf

[^sm]: Needed for plastic objects