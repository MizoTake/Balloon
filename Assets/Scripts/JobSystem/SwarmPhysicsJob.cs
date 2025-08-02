using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// Job for calculating swarm physics and collective balloon behaviors
    /// Implements flocking, density separation, and vortex formation
    /// </summary>
    [BurstCompile]
    public struct SwarmPhysicsJob : IJobParallelFor
    {
        // Balloon data
        [ReadOnly] public NativeArray<EnhancedBalloonData> balloonsIn;
        public NativeArray<EnhancedBalloonData> balloonsOut;
        [ReadOnly] public NativeArray<int> activeIndices;
        
        // Spatial data
        [ReadOnly] public HierarchicalSpatialGrid spatialGrid;
        [ReadOnly] public NativeArray<int> balloonToCellMapping;
        
        // Swarm parameters
        [ReadOnly] public SwarmParameters swarmParams;
        [ReadOnly] public EnhancedSimulationParameters simParams;
        [ReadOnly] public float deltaTime;
        [ReadOnly] public float time;
        
        // Density field for buoyancy calculations
        [ReadOnly] public NativeArray<float> localDensityField;
        
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
            
            // Get local neighborhood data
            var neighborData = GatherNeighborData(balloon.position, balloonIndex);
            
            // 1. Separation force (avoid crowding)
            float3 separationForce = CalculateSeparationForce(balloon, neighborData);
            
            // 2. Alignment force (match velocity with neighbors)
            float3 alignmentForce = CalculateAlignmentForce(balloon, neighborData);
            
            // 3. Cohesion force (move toward group center)
            float3 cohesionForce = CalculateCohesionForce(balloon, neighborData);
            
            // 4. Density gradient force (buoyancy separation)
            float3 densityForce = CalculateDensityGradientForce(balloon, balloonIndex);
            
            // 5. Vortex formation force
            float3 vortexForce = CalculateVortexForce(balloon, neighborData, time);
            
            // 6. Turbulent mixing force
            float3 turbulentForce = CalculateTurbulentMixing(balloon, neighborData, time);
            
            // 7. Boundary layer effects
            float3 boundaryForce = CalculateBoundaryLayerEffect(balloon);
            
            // Combine all swarm forces with weights
            float3 totalSwarmForce = 
                separationForce * swarmParams.separationWeight +
                alignmentForce * swarmParams.alignmentWeight +
                cohesionForce * swarmParams.cohesionWeight +
                densityForce * swarmParams.densityWeight +
                vortexForce * swarmParams.vortexWeight +
                turbulentForce * swarmParams.turbulenceWeight +
                boundaryForce * swarmParams.boundaryWeight;
            
            // Apply swarm forces to acceleration
            balloon.acceleration += totalSwarmForce / balloon.mass;
            
            // Update angular velocity for swirling motion
            UpdateAngularVelocity(ref balloon, neighborData, vortexForce);
            
            // Dispose neighbor data
            neighborData.Dispose();
            
            balloonsOut[balloonIndex] = balloon;
        }
        
        private NeighborData GatherNeighborData(float3 position, int selfIndex)
        {
            var data = NeighborData.Create();
            
            // Get neighboring cells
            var neighbors = new NativeList<int>(27, Allocator.Temp);
            spatialGrid.GetNeighboringCells(position, swarmParams.neighborRadius, 2, neighbors);
            
            // Process balloons in neighboring cells
            for (int i = 0; i < neighbors.Length && data.count < 50; i++)
            {
                int cellIndex = neighbors[i];
                if (cellIndex < 0 || cellIndex >= spatialGrid.level2Cells.Length)
                    continue;
                
                var cell = spatialGrid.level2Cells[cellIndex];
                
                // For each balloon in the cell
                for (int j = 0; j < activeIndices.Length && data.count < 50; j++)
                {
                    int balloonIndex = activeIndices[j];
                    if (balloonIndex == selfIndex)
                        continue;
                    
                    if (balloonToCellMapping[balloonIndex] == cellIndex)
                    {
                        var neighbor = balloonsIn[balloonIndex];
                        float distance = math.distance(position, neighbor.position);
                        
                        if (distance < swarmParams.neighborRadius && neighbor.state != BalloonState.Destroyed)
                        {
                            data.positions[data.count] = neighbor.position;
                            data.velocities[data.count] = neighbor.velocity;
                            data.masses[data.count] = neighbor.mass;
                            data.distances[data.count] = distance;
                            
                            data.centerOfMass += neighbor.position * neighbor.mass;
                            data.averageVelocity += neighbor.velocity;
                            data.totalMass += neighbor.mass;
                            data.count++;
                        }
                    }
                }
            }
            
            neighbors.Dispose();
            
            // Finalize averages
            if (data.count > 0)
            {
                data.centerOfMass /= data.totalMass;
                data.averageVelocity /= data.count;
            }
            
            return data;
        }
        
        private float3 CalculateSeparationForce(EnhancedBalloonData balloon, NeighborData neighbors)
        {
            if (neighbors.count == 0)
                return float3.zero;
            
            float3 separationForce = float3.zero;
            float personalSpace = balloon.radius * 2.5f;
            
            for (int i = 0; i < neighbors.count; i++)
            {
                float distance = neighbors.distances[i];
                if (distance < personalSpace && distance > 0.001f)
                {
                    float3 awayVector = balloon.position - neighbors.positions[i];
                    float strength = 1f - (distance / personalSpace);
                    strength = strength * strength; // Quadratic falloff
                    
                    separationForce += math.normalize(awayVector) * strength;
                }
            }
            
            return separationForce * swarmParams.separationStrength;
        }
        
        private float3 CalculateAlignmentForce(EnhancedBalloonData balloon, NeighborData neighbors)
        {
            if (neighbors.count == 0)
                return float3.zero;
            
            float3 desiredVelocity = neighbors.averageVelocity;
            float3 steeringForce = desiredVelocity - balloon.velocity;
            
            // Limit steering force
            float maxSteer = swarmParams.alignmentStrength;
            if (math.lengthsq(steeringForce) > maxSteer * maxSteer)
            {
                steeringForce = math.normalize(steeringForce) * maxSteer;
            }
            
            return steeringForce;
        }
        
        private float3 CalculateCohesionForce(EnhancedBalloonData balloon, NeighborData neighbors)
        {
            if (neighbors.count == 0)
                return float3.zero;
            
            float3 toCenter = neighbors.centerOfMass - balloon.position;
            float distance = math.length(toCenter);
            
            if (distance < swarmParams.cohesionRadius && distance > 0.001f)
            {
                // Weak force when close, stronger when far
                float strength = distance / swarmParams.cohesionRadius;
                return math.normalize(toCenter) * strength * swarmParams.cohesionStrength;
            }
            
            return float3.zero;
        }
        
        private float3 CalculateDensityGradientForce(EnhancedBalloonData balloon, int balloonIndex)
        {
            // Sample density field at balloon position
            int cellIndex = balloonToCellMapping[balloonIndex];
            if (cellIndex < 0 || cellIndex >= localDensityField.Length)
                return float3.zero;
            
            float localDensity = localDensityField[cellIndex];
            
            // Calculate density gradient
            float3 gradient = CalculateDensityGradient(balloon.position);
            
            // Buoyancy effect based on balloon density vs local air density
            float balloonDensity = balloon.mass / (4f/3f * math.PI * balloon.radius * balloon.radius * balloon.radius);
            float densityDifference = localDensity - balloonDensity;
            
            // Force proportional to density difference and opposite to gradient
            float3 buoyancyForce = -gradient * densityDifference * swarmParams.densityStrength;
            
            // Add vertical stratification tendency
            float heightFactor = balloon.position.y / swarmParams.stratificationHeight;
            float targetDensityLayer = balloon.buoyancy * 10f; // Arbitrary scaling
            float layerDifference = heightFactor - targetDensityLayer;
            
            buoyancyForce.y -= layerDifference * swarmParams.stratificationStrength;
            
            return buoyancyForce;
        }
        
        private float3 CalculateDensityGradient(float3 position)
        {
            float h = 1f; // Sample distance
            
            // Sample density at neighboring points
            float densityPx = SampleDensity(position + new float3(h, 0, 0));
            float densityNx = SampleDensity(position - new float3(h, 0, 0));
            float densityPy = SampleDensity(position + new float3(0, h, 0));
            float densityNy = SampleDensity(position - new float3(0, h, 0));
            float densityPz = SampleDensity(position + new float3(0, 0, h));
            float densityNz = SampleDensity(position - new float3(0, 0, h));
            
            return new float3(
                (densityPx - densityNx) / (2f * h),
                (densityPy - densityNy) / (2f * h),
                (densityPz - densityNz) / (2f * h)
            );
        }
        
        private float SampleDensity(float3 position)
        {
            int cellIndex = spatialGrid.GetCellIndex(position, 2);
            if (cellIndex >= 0 && cellIndex < localDensityField.Length)
                return localDensityField[cellIndex];
            return simParams.airDensity;
        }
        
        private float3 CalculateVortexForce(EnhancedBalloonData balloon, NeighborData neighbors, float time)
        {
            if (neighbors.count < 3)
                return float3.zero;
            
            // Calculate local vorticity
            float3 vorticity = float3.zero;
            float3 centerPos = neighbors.centerOfMass;
            
            for (int i = 0; i < neighbors.count; i++)
            {
                float3 r = neighbors.positions[i] - centerPos;
                float3 v = neighbors.velocities[i];
                vorticity += math.cross(r, v) / (neighbors.distances[i] + 0.1f);
            }
            
            vorticity /= neighbors.count;
            
            // Apply vortex force perpendicular to radial direction
            float3 toBalloon = balloon.position - centerPos;
            float distanceToCenter = math.length(toBalloon);
            
            if (distanceToCenter > 0.1f && distanceToCenter < swarmParams.vortexRadius)
            {
                float3 radial = toBalloon / distanceToCenter;
                float3 tangential = math.cross(vorticity, radial);
                
                // Vortex strength decreases with distance
                float strength = 1f - (distanceToCenter / swarmParams.vortexRadius);
                strength *= swarmParams.vortexStrength;
                
                // Add time-varying component for dynamic vortices
                float phase = time * swarmParams.vortexFrequency + distanceToCenter * 0.5f;
                strength *= (1f + 0.3f * math.sin(phase));
                
                return tangential * strength;
            }
            
            return float3.zero;
        }
        
        private float3 CalculateTurbulentMixing(EnhancedBalloonData balloon, NeighborData neighbors, float time)
        {
            // Use curl noise for realistic turbulence
            float3 turbulence = float3.zero;
            float scale = swarmParams.turbulenceScale;
            
            // Multi-octave turbulence
            for (int octave = 0; octave < 3; octave++)
            {
                float octaveScale = scale * math.pow(2f, octave);
                float octaveAmp = 1f / math.pow(2f, octave);
                
                float3 samplePos = balloon.position * octaveScale + time * 0.1f;
                turbulence += CurlNoise(samplePos) * octaveAmp;
            }
            
            // Scale by local density for realistic behavior
            float densityScale = math.saturate(neighbors.count / 20f);
            return turbulence * swarmParams.turbulenceStrength * densityScale;
        }
        
        private float3 CurlNoise(float3 p)
        {
            // Compute curl of 3D noise field
            float eps = 0.1f;
            
            float n1 = noise.snoise(p + new float3(0, eps, 0));
            float n2 = noise.snoise(p - new float3(0, eps, 0));
            float n3 = noise.snoise(p + new float3(0, 0, eps));
            float n4 = noise.snoise(p - new float3(0, 0, eps));
            float n5 = noise.snoise(p + new float3(eps, 0, 0));
            float n6 = noise.snoise(p - new float3(eps, 0, 0));
            
            float3 curl;
            curl.x = (n1 - n2 - n3 + n4) / (2f * eps);
            curl.y = (n3 - n4 - n5 + n6) / (2f * eps);
            curl.z = (n5 - n6 - n1 + n2) / (2f * eps);
            
            return curl;
        }
        
        private float3 CalculateBoundaryLayerEffect(EnhancedBalloonData balloon)
        {
            float3 boundaryForce = float3.zero;
            
            // Check proximity to world bounds
            var bounds = simParams.worldBounds;
            float3 center = bounds.center;
            float3 extents = bounds.extents;
            
            float3 relativePos = balloon.position - center;
            float3 distances = extents - math.abs(relativePos);
            
            float boundaryThickness = 5f;
            
            // Apply repulsion from boundaries
            if (distances.x < boundaryThickness)
            {
                float strength = 1f - (distances.x / boundaryThickness);
                boundaryForce.x = math.sign(relativePos.x) * -strength * swarmParams.boundaryStrength;
            }
            
            if (distances.y < boundaryThickness)
            {
                float strength = 1f - (distances.y / boundaryThickness);
                boundaryForce.y = math.sign(relativePos.y) * -strength * swarmParams.boundaryStrength;
            }
            
            if (distances.z < boundaryThickness)
            {
                float strength = 1f - (distances.z / boundaryThickness);
                boundaryForce.z = math.sign(relativePos.z) * -strength * swarmParams.boundaryStrength;
            }
            
            return boundaryForce;
        }
        
        private void UpdateAngularVelocity(ref EnhancedBalloonData balloon, in NeighborData neighbors, float3 vortexForce)
        {
            // Add rotation based on local flow
            float3 torque = float3.zero;
            
            // Torque from vortex motion
            if (math.lengthsq(vortexForce) > 0.001f)
            {
                torque += math.cross(new float3(0, 1, 0), vortexForce) * 0.1f;
            }
            
            // Torque from velocity difference with neighbors
            if (neighbors.count > 0)
            {
                float3 relativeVel = balloon.velocity - neighbors.averageVelocity;
                torque += math.cross(math.normalize(relativeVel), new float3(0, 1, 0)) * 0.05f;
            }
            
            // Apply torque with inertia
            float inertia = 0.4f * balloon.mass * balloon.radius * balloon.radius;
            balloon.angularVelocity += torque / inertia * deltaTime;
            
            // Apply damping
            balloon.angularVelocity *= 0.98f;
        }
    }
    
    /// <summary>
    /// Parameters for swarm behavior
    /// </summary>
    [System.Serializable]
    public struct SwarmParameters
    {
        // Flocking parameters
        public float neighborRadius;
        public float separationStrength;
        public float separationWeight;
        public float alignmentStrength;
        public float alignmentWeight;
        public float cohesionRadius;
        public float cohesionStrength;
        public float cohesionWeight;
        
        // Density separation
        public float densityStrength;
        public float densityWeight;
        public float stratificationHeight;
        public float stratificationStrength;
        
        // Vortex formation
        public float vortexRadius;
        public float vortexStrength;
        public float vortexWeight;
        public float vortexFrequency;
        
        // Turbulence
        public float turbulenceScale;
        public float turbulenceStrength;
        public float turbulenceWeight;
        
        // Boundary effects
        public float boundaryStrength;
        public float boundaryWeight;
        
        public static SwarmParameters CreateDefault()
        {
            return new SwarmParameters
            {
                neighborRadius = 5f,
                separationStrength = 2f,
                separationWeight = 1f,
                alignmentStrength = 1f,
                alignmentWeight = 0.5f,
                cohesionRadius = 10f,
                cohesionStrength = 0.5f,
                cohesionWeight = 0.3f,
                
                densityStrength = 1f,
                densityWeight = 0.8f,
                stratificationHeight = 20f,
                stratificationStrength = 0.5f,
                
                vortexRadius = 15f,
                vortexStrength = 1f,
                vortexWeight = 0.4f,
                vortexFrequency = 0.5f,
                
                turbulenceScale = 0.1f,
                turbulenceStrength = 0.5f,
                turbulenceWeight = 0.3f,
                
                boundaryStrength = 5f,
                boundaryWeight = 1f
            };
        }
    }
    
    /// <summary>
    /// Neighbor data structure for efficient processing
    /// </summary>
    public struct NeighborData
    {
        public int count;
        public float3 centerOfMass;
        public float3 averageVelocity;
        public float totalMass;
        
        // Use NativeArrays in temp allocator for neighbor data
        public NativeArray<float3> positions;
        public NativeArray<float3> velocities;
        public NativeArray<float> masses;
        public NativeArray<float> distances;
        
        public static NeighborData Create()
        {
            return new NeighborData
            {
                count = 0,
                centerOfMass = float3.zero,
                averageVelocity = float3.zero,
                totalMass = 0f,
                positions = new NativeArray<float3>(50, Allocator.Temp),
                velocities = new NativeArray<float3>(50, Allocator.Temp),
                masses = new NativeArray<float>(50, Allocator.Temp),
                distances = new NativeArray<float>(50, Allocator.Temp)
            };
        }
        
        public void Dispose()
        {
            if (positions.IsCreated) positions.Dispose();
            if (velocities.IsCreated) velocities.Dispose();
            if (masses.IsCreated) masses.Dispose();
            if (distances.IsCreated) distances.Dispose();
        }
    }
    
    /// <summary>
    /// Job to calculate local density field for swarm physics
    /// </summary>
    [BurstCompile]
    public struct UpdateDensityFieldJob : IJob
    {
        [ReadOnly] public NativeArray<EnhancedBalloonData> balloons;
        [ReadOnly] public NativeArray<int> activeIndices;
        [ReadOnly] public HierarchicalSpatialGrid spatialGrid;
        
        public NativeArray<float> densityField;
        
        public void Execute()
        {
            // Reset density field
            for (int i = 0; i < densityField.Length; i++)
            {
                densityField[i] = 1.225f; // Default air density
            }
            
            // Update based on balloon positions
            for (int i = 0; i < activeIndices.Length; i++)
            {
                int balloonIndex = activeIndices[i];
                var balloon = balloons[balloonIndex];
                
                if (balloon.state == BalloonState.Destroyed)
                    continue;
                
                // Get cell and update density
                int cellIndex = spatialGrid.GetCellIndex(balloon.position, 2);
                if (cellIndex >= 0 && cellIndex < densityField.Length)
                {
                    // Balloon displaces air, reducing local density
                    float balloonVolume = (4f/3f) * math.PI * balloon.radius * balloon.radius * balloon.radius;
                    float cellVolume = spatialGrid.baseCellSize * spatialGrid.baseCellSize * spatialGrid.baseCellSize;
                    float volumeRatio = balloonVolume / cellVolume;
                    
                    densityField[cellIndex] *= (1f - volumeRatio * 0.1f);
                }
            }
        }
    }
}