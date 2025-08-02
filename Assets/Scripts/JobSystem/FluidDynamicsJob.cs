using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// Job for calculating fluid dynamics effects on balloons
    /// Implements simplified Navier-Stokes equations for real-time performance
    /// </summary>
    [BurstCompile]
    public struct FluidDynamicsJob : IJobParallelFor
    {
        // Balloon data
        [ReadOnly] public NativeArray<EnhancedBalloonData> balloonsIn;
        public NativeArray<EnhancedBalloonData> balloonsOut;
        [ReadOnly] public NativeArray<int> activeIndices;
        
        // Fluid grid data
        [ReadOnly] public NativeArray<float3> velocityField;
        [ReadOnly] public NativeArray<float> pressureField;
        [ReadOnly] public NativeArray<float> densityField;
        [ReadOnly] public NativeArray<float> temperatureField;
        
        // Grid parameters
        [ReadOnly] public float3 gridOrigin;
        [ReadOnly] public float3 gridSize;
        [ReadOnly] public int3 gridDimensions;
        [ReadOnly] public float cellSize;
        
        // Simulation parameters
        [ReadOnly] public EnhancedSimulationParameters simParams;
        [ReadOnly] public float deltaTime;
        [ReadOnly] public float time;
        
        // Spatial grid for neighbor queries
        [ReadOnly] public HierarchicalSpatialGrid spatialGrid;
        [ReadOnly] public NativeArray<int> balloonToCellMapping;
        
        public void Execute(int index)
        {
            int balloonIndex = activeIndices[index];
            var balloon = balloonsIn[balloonIndex];
            
            // Skip destroyed balloons
            if (balloon.state == BalloonState.Destroyed)
            {
                balloonsOut[balloonIndex] = balloon;
                return;
            }
            
            // Calculate fluid forces
            float3 fluidVelocity = SampleFluidVelocity(balloon.position);
            float pressure = SamplePressure(balloon.position);
            float density = SampleDensity(balloon.position);
            float temperature = SampleTemperature(balloon.position);
            
            // 1. Drag force (Stokes' law for low Reynolds number)
            float3 relativeVelocity = balloon.velocity - fluidVelocity;
            float reynoldsNumber = CalculateReynoldsNumber(relativeVelocity, balloon.radius, density, simParams.viscosityCoefficient);
            float3 dragForce = CalculateDragForce(relativeVelocity, balloon.radius, reynoldsNumber);
            
            // 2. Pressure gradient force
            float3 pressureGradient = CalculatePressureGradient(balloon.position);
            float3 pressureForce = -pressureGradient * balloon.radius * balloon.radius * math.PI;
            
            // 3. Buoyancy force with environmental effects
            float effectiveBuoyancy = balloon.CalculateEffectiveBuoyancy(temperature, simParams.altitude);
            float3 buoyancyForce = new float3(0, effectiveBuoyancy * simParams.gravity, 0);
            
            // 4. Magnus effect (spinning balloon)
            float3 magnusForce = CalculateMagnusForce(balloon.velocity, balloon.angularVelocity, density, balloon.radius);
            
            // 5. Turbulence effects
            float3 turbulenceForce = CalculateTurbulenceForce(balloon.position, time);
            
            // 6. Vortex shedding
            float3 vortexForce = CalculateVortexSheddingForce(balloon.position, relativeVelocity, balloon.radius, time);
            
            // Total fluid force
            float3 totalFluidForce = dragForce + pressureForce + buoyancyForce + magnusForce + turbulenceForce + vortexForce;
            
            // Apply forces to acceleration
            balloon.acceleration += totalFluidForce / balloon.mass;
            
            // Update angular velocity due to fluid torque
            float3 fluidTorque = CalculateFluidTorque(balloon.position, balloon.radius, fluidVelocity, balloon.velocity);
            balloon.angularVelocity += fluidTorque / (0.4f * balloon.mass * balloon.radius * balloon.radius) * deltaTime;
            
            // Wake effects from nearby balloons
            ApplyWakeEffects(ref balloon, index);
            
            balloonsOut[balloonIndex] = balloon;
        }
        
        private float3 SampleFluidVelocity(float3 position)
        {
            int3 gridCoord = WorldToGrid(position);
            if (!IsValidGridCoord(gridCoord))
                return float3.zero;
            
            int gridIndex = GridCoordToIndex(gridCoord);
            return velocityField[gridIndex];
        }
        
        private float SamplePressure(float3 position)
        {
            int3 gridCoord = WorldToGrid(position);
            if (!IsValidGridCoord(gridCoord))
                return 101325f; // Atmospheric pressure
            
            int gridIndex = GridCoordToIndex(gridCoord);
            return pressureField[gridIndex];
        }
        
        private float SampleDensity(float3 position)
        {
            int3 gridCoord = WorldToGrid(position);
            if (!IsValidGridCoord(gridCoord))
                return simParams.airDensity;
            
            int gridIndex = GridCoordToIndex(gridCoord);
            return densityField[gridIndex];
        }
        
        private float SampleTemperature(float3 position)
        {
            int3 gridCoord = WorldToGrid(position);
            if (!IsValidGridCoord(gridCoord))
                return simParams.temperature;
            
            int gridIndex = GridCoordToIndex(gridCoord);
            return temperatureField[gridIndex];
        }
        
        private float3 CalculatePressureGradient(float3 position)
        {
            float h = cellSize * 0.5f;
            
            float px1 = SamplePressure(position + new float3(h, 0, 0));
            float px0 = SamplePressure(position - new float3(h, 0, 0));
            float py1 = SamplePressure(position + new float3(0, h, 0));
            float py0 = SamplePressure(position - new float3(0, h, 0));
            float pz1 = SamplePressure(position + new float3(0, 0, h));
            float pz0 = SamplePressure(position - new float3(0, 0, h));
            
            return new float3(
                (px1 - px0) / (2f * h),
                (py1 - py0) / (2f * h),
                (pz1 - pz0) / (2f * h)
            );
        }
        
        private float CalculateReynoldsNumber(float3 velocity, float radius, float density, float viscosity)
        {
            float speed = math.length(velocity);
            return density * speed * (2f * radius) / viscosity;
        }
        
        private float3 CalculateDragForce(float3 relativeVelocity, float radius, float reynoldsNumber)
        {
            float speed = math.length(relativeVelocity);
            if (speed < 0.0001f)
                return float3.zero;
            
            float3 direction = -math.normalize(relativeVelocity);
            float dragCoefficient;
            
            // Different drag regimes based on Reynolds number
            if (reynoldsNumber < 1f)
            {
                // Stokes flow regime
                dragCoefficient = 24f / reynoldsNumber;
            }
            else if (reynoldsNumber < 1000f)
            {
                // Intermediate regime
                dragCoefficient = 24f / reynoldsNumber * (1f + 0.15f * math.pow(reynoldsNumber, 0.687f));
            }
            else
            {
                // Turbulent regime
                dragCoefficient = simParams.dragCoefficient;
            }
            
            float area = math.PI * radius * radius;
            float dragMagnitude = 0.5f * simParams.airDensity * speed * speed * dragCoefficient * area;
            
            return direction * dragMagnitude;
        }
        
        private float3 CalculateMagnusForce(float3 velocity, float3 angularVelocity, float density, float radius)
        {
            if (math.lengthsq(angularVelocity) < 0.0001f)
                return float3.zero;
            
            // Magnus force: F = ρ * V * ω × v * Volume
            float volume = (4f / 3f) * math.PI * radius * radius * radius;
            return density * volume * math.cross(angularVelocity, velocity);
        }
        
        private float3 CalculateTurbulenceForce(float3 position, float time)
        {
            // Perlin noise-based turbulence
            float scale = 0.1f;
            float3 noisePos = position * scale + new float3(time * 0.1f, 0, 0);
            
            float3 turbulence = new float3(
                noise.snoise(noisePos),
                noise.snoise(noisePos + new float3(100f, 0, 0)),
                noise.snoise(noisePos + new float3(0, 100f, 0))
            );
            
            return turbulence * simParams.turbulenceStrength;
        }
        
        private float3 CalculateVortexSheddingForce(float3 position, float3 velocity, float radius, float time)
        {
            float speed = math.length(velocity);
            if (speed < 0.1f)
                return float3.zero;
            
            // Strouhal number for sphere ≈ 0.2
            float strouhalNumber = 0.2f;
            float sheddingFrequency = strouhalNumber * speed / (2f * radius);
            
            // Perpendicular force due to vortex shedding
            float3 flowDirection = math.normalize(velocity);
            float3 perpendicular = math.cross(flowDirection, new float3(0, 1, 0));
            if (math.lengthsq(perpendicular) < 0.0001f)
                perpendicular = math.cross(flowDirection, new float3(1, 0, 0));
            perpendicular = math.normalize(perpendicular);
            
            float vortexStrength = math.sin(2f * math.PI * sheddingFrequency * time);
            return perpendicular * vortexStrength * simParams.vortexStrength * speed;
        }
        
        private float3 CalculateFluidTorque(float3 position, float radius, float3 fluidVelocity, float3 balloonVelocity)
        {
            // Simplified torque calculation based on velocity difference
            float3 velocityDiff = fluidVelocity - balloonVelocity;
            float torqueMagnitude = math.length(velocityDiff) * radius * simParams.viscosityCoefficient;
            
            float3 torqueAxis = math.cross(new float3(0, 1, 0), velocityDiff);
            if (math.lengthsq(torqueAxis) > 0.0001f)
            {
                return math.normalize(torqueAxis) * torqueMagnitude;
            }
            
            return float3.zero;
        }
        
        private void ApplyWakeEffects(ref EnhancedBalloonData balloon, int index)
        {
            // Get nearby balloons from spatial grid
            var neighbors = new NativeList<int>(10, Allocator.Temp);
            spatialGrid.GetNeighboringCells(balloon.position, balloon.radius * 4f, 2, neighbors);
            
            float3 wakeForce = float3.zero;
            int nearbyCount = 0;
            
            for (int i = 0; i < neighbors.Length && nearbyCount < 10; i++)
            {
                int cellIndex = neighbors[i];
                if (cellIndex < 0 || cellIndex >= spatialGrid.level2Cells.Length)
                    continue;
                
                var cell = spatialGrid.level2Cells[cellIndex];
                
                // Simple wake effect based on cell density
                if (cell.density > 0.1f)
                {
                    float3 cellCenter = cell.bounds.center;
                    float3 toCell = cellCenter - balloon.position;
                    float distance = math.length(toCell);
                    
                    if (distance > 0.1f && distance < balloon.radius * 4f)
                    {
                        // Wake strength decreases with distance
                        float wakeStrength = (1f - distance / (balloon.radius * 4f)) * cell.density * 0.1f;
                        wakeForce += math.normalize(toCell) * wakeStrength;
                        nearbyCount++;
                    }
                }
            }
            
            balloon.acceleration += wakeForce;
            neighbors.Dispose();
        }
        
        private int3 WorldToGrid(float3 worldPos)
        {
            float3 localPos = worldPos - gridOrigin;
            return (int3)(localPos / cellSize);
        }
        
        private bool IsValidGridCoord(int3 coord)
        {
            return math.all(coord >= 0) && math.all(coord < gridDimensions);
        }
        
        private int GridCoordToIndex(int3 coord)
        {
            return coord.x + coord.y * gridDimensions.x + coord.z * gridDimensions.x * gridDimensions.y;
        }
    }
    
    /// <summary>
    /// Job to update the fluid grid based on balloon positions and velocities
    /// </summary>
    [BurstCompile]
    public struct UpdateFluidGridJob : IJob
    {
        [ReadOnly] public NativeArray<EnhancedBalloonData> balloons;
        [ReadOnly] public NativeArray<int> activeIndices;
        
        public NativeArray<float3> velocityField;
        public NativeArray<float> pressureField;
        public NativeArray<float> densityField;
        public NativeArray<float> temperatureField;
        
        [ReadOnly] public float3 gridOrigin;
        [ReadOnly] public int3 gridDimensions;
        [ReadOnly] public float cellSize;
        [ReadOnly] public float deltaTime;
        [ReadOnly] public EnhancedSimulationParameters simParams;
        
        public void Execute()
        {
            // Reset grid
            for (int i = 0; i < velocityField.Length; i++)
            {
                velocityField[i] = simParams.windDirection * simParams.windStrength;
                pressureField[i] = 101325f; // Atmospheric pressure
                densityField[i] = simParams.airDensity;
                temperatureField[i] = simParams.temperature;
            }
            
            // Update grid based on balloon positions
            for (int i = 0; i < activeIndices.Length; i++)
            {
                int balloonIndex = activeIndices[i];
                var balloon = balloons[balloonIndex];
                
                if (balloon.state == BalloonState.Destroyed)
                    continue;
                
                // Find affected grid cells
                int3 centerCell = WorldToGrid(balloon.position);
                int radius = (int)math.ceil(balloon.radius / cellSize);
                
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        for (int dz = -radius; dz <= radius; dz++)
                        {
                            int3 cellCoord = centerCell + new int3(dx, dy, dz);
                            if (!IsValidGridCoord(cellCoord))
                                continue;
                            
                            float3 cellWorldPos = GridToWorld(cellCoord);
                            float distance = math.distance(cellWorldPos, balloon.position);
                            
                            if (distance <= balloon.radius * 1.5f)
                            {
                                int gridIndex = GridCoordToIndex(cellCoord);
                                
                                // Balloon displaces air, creating velocity field
                                float influence = 1f - (distance / (balloon.radius * 1.5f));
                                velocityField[gridIndex] += balloon.velocity * influence * 0.5f;
                                
                                // Balloon affects local pressure
                                pressureField[gridIndex] += balloon.internalPressure * influence * 0.01f;
                                
                                // Balloon affects local density (displacement)
                                densityField[gridIndex] *= (1f - influence * 0.1f);
                            }
                        }
                    }
                }
            }
        }
        
        private int3 WorldToGrid(float3 worldPos)
        {
            float3 localPos = worldPos - gridOrigin;
            return (int3)(localPos / cellSize);
        }
        
        private float3 GridToWorld(int3 gridCoord)
        {
            return gridOrigin + (float3)gridCoord * cellSize + cellSize * 0.5f;
        }
        
        private bool IsValidGridCoord(int3 coord)
        {
            return math.all(coord >= 0) && math.all(coord < gridDimensions);
        }
        
        private int GridCoordToIndex(int3 coord)
        {
            return coord.x + coord.y * gridDimensions.x + coord.z * gridDimensions.x * gridDimensions.y;
        }
    }
}