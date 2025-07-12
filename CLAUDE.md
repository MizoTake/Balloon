# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a Unity balloon physics simulation project designed to handle 100 balloons with realistic physics and performance optimization. The system implements an architecture based on performance analysis targeting 60-75 FPS on desktop and 30-40 FPS on mobile platforms.

## Unity Environment

- **Unity Version**: 2022.3.28f1 (LTS)
- **Key Dependencies**: Unity Mathematics (1.2.6), Unity Physics modules
- **Target Platform**: Windows (primary), with mobile optimization considerations

## Core Architecture

The balloon physics system follows a modular architecture with specialized systems:

### Central Management
- `BalloonManager` - Main orchestrator that auto-configures and coordinates all systems
- Handles runtime controls (R=restart, P=performance report)

### Physics Systems
- `BalloonController` - Core balloon behavior with optimized SphereColliders
- `BalloonPhysics` - Archimedes' principle buoyancy simulation with environmental factors
- `PhysicsOptimizer` - Global physics settings optimization (25% performance improvement via timestep tuning)

### Performance Systems
- `BalloonLODSystem` - Distance-based Level of Detail providing 40-60% performance gains
- `PerformanceProfiler` - Real-time monitoring with F1 toggle for detailed stats
- `WindSystem` - Perlin noise-based wind effects with performance-conscious updates

### Advanced Features
- `VerletRope` - High-performance rope physics using Unity Jobs System and Burst compilation
- `BalloonSpawner` - Manages balloon instantiation with variations

## Key Performance Optimizations

1. **Fixed Timestep**: Reduced from 0.02s to 0.025s (25% improvement)
2. **Collision Layers**: Dedicated "Balloon" layer reduces unnecessary collision checks by 30-40%
3. **LOD System**: Distance-based physics disabling beyond 50 units
4. **Solver Iterations**: Conservative settings (4 iterations default, 2 for distant objects)
5. **Job System**: Burst-compiled Verlet integration for rope physics

## Demo Setup

Use the Editor extension for quick project setup:
- `Tools > Balloon Physics > Quick Setup` - Creates complete demo scene
- `Tools > Balloon Physics > Setup Demo Scene` - Opens configuration window

## Runtime Controls

- **R** - Restart simulation
- **P** - Log performance report
- **F1** - Toggle detailed performance stats
- **WASD** - Camera movement
- **Right Click + Mouse** - Camera look
- **QE** - Camera up/down

## Performance Targets

- **Desktop**: 60-75 FPS with 100 balloons
- **Memory**: ~2.5MB for balloon objects
- **Physics Time**: 8-12ms per frame target
- **Warning Threshold**: Performance alerts below 45 FPS

## Development Notes

- The Job System constraint solver was converted from `IJobParallelFor` to `IJob` to avoid index range issues with adjacent point access
- Buoyancy calculations support environmental factors (temperature, altitude) with performance caching
- Wind system uses staggered updates to balance realism with performance
- All physics systems implement frame-rate independent updates through FixedUpdate

## Layer Configuration

Manually create "Balloon" layer in Project Settings > Tags and Layers for optimal collision performance. Configure collision matrix so Balloon layer collides with Default and Balloon layers only.