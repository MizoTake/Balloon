using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// Enhanced physics job that respects LOD levels for performance optimization
    /// </summary>
    [BurstCompile]
    public struct BalloonPhysicsLODJob : IJobParallelFor
    {
        public NativeArray<BalloonData> balloons;
        [ReadOnly] public SimulationParameters parameters;
        [ReadOnly] public float deltaTime;
        [ReadOnly] public float time;
        [ReadOnly] public NativeArray<BalloonLODSystem.LODLevel> lodLevels;
        [ReadOnly] public NativeArray<bool> physicsEnabled;
        
        public void Execute(int index)
        {
            // Skip if physics is disabled for this balloon
            if (!physicsEnabled[index])
                return;
            
            var balloon = balloons[index];
            var lod = lodLevels[index];
            
            // Get timestep multiplier based on LOD
            float lodTimestep = deltaTime * GetTimestepMultiplier(lod);
            
            // Calculate forces based on LOD level
            float3 totalForce = float3.zero;
            
            // Gravity (always applied)
            float3 gravity = new float3(0, -parameters.gravity * balloon.mass, 0);
            totalForce += gravity;
            
            // Buoyancy (always applied)
            float volume = (4f / 3f) * math.PI * math.pow(balloon.radius, 3);
            float3 buoyancy = new float3(0, balloon.buoyancy * volume * parameters.airDensity * parameters.gravity, 0);
            totalForce += buoyancy;
            
            // Wind (LOD0 and LOD1 only)
            if (ShouldCalculateWind(lod) && parameters.windStrength > 0)
            {
                float3 windForce = CalculateWindForce(balloon.position, time, parameters);
                totalForce += windForce * balloon.radius * balloon.radius;
            }
            
            // Drag (simplified for higher LODs)
            float3 drag = CalculateDrag(balloon.velocity, balloon.radius, parameters.airDensity, lod);
            totalForce += drag;
            
            // Update velocity
            float3 acceleration = totalForce / balloon.mass;
            balloon.velocity += acceleration * lodTimestep;
            
            // Apply damping (more aggressive for higher LODs)
            float damping = GetDampingFactor(parameters.damping, lod);
            balloon.velocity *= damping;
            
            // Update position
            balloon.position += balloon.velocity * lodTimestep;
            
            // Keep within world bounds
            KeepInBounds(ref balloon, parameters.worldBounds);
            
            // Update transform matrix
            balloon.UpdateMatrix();
            
            balloons[index] = balloon;
        }
        
        float GetTimestepMultiplier(BalloonLODSystem.LODLevel lod)
        {
            switch (lod)
            {
                case BalloonLODSystem.LODLevel.LOD0: return 1.0f;
                case BalloonLODSystem.LODLevel.LOD1: return 1.5f;
                case BalloonLODSystem.LODLevel.LOD2: return 2.0f;
                case BalloonLODSystem.LODLevel.LOD3: return 3.0f;
                case BalloonLODSystem.LODLevel.LOD4: return 4.0f;
                default: return 1.0f;
            }
        }
        
        bool ShouldCalculateWind(BalloonLODSystem.LODLevel lod)
        {
            return lod <= BalloonLODSystem.LODLevel.LOD1;
        }
        
        float GetDampingFactor(float baseDamping, BalloonLODSystem.LODLevel lod)
        {
            // More aggressive damping for higher LODs to stabilize motion
            switch (lod)
            {
                case BalloonLODSystem.LODLevel.LOD0: return baseDamping;
                case BalloonLODSystem.LODLevel.LOD1: return baseDamping * 0.98f;
                case BalloonLODSystem.LODLevel.LOD2: return baseDamping * 0.96f;
                case BalloonLODSystem.LODLevel.LOD3: return baseDamping * 0.94f;
                case BalloonLODSystem.LODLevel.LOD4: return baseDamping * 0.92f;
                default: return baseDamping;
            }
        }
        
        float3 CalculateWindForce(float3 position, float time, SimulationParameters parameters)
        {
            // Perlin noise based wind
            float windX = noise.snoise(new float2(position.y * 0.1f, time * 0.5f));
            float windZ = noise.snoise(new float2(position.x * 0.1f, time * 0.5f + 100f));
            
            float3 wind = new float3(windX, 0, windZ) * parameters.windStrength;
            wind += parameters.windDirection * parameters.windStrength * 0.5f;
            
            return wind;
        }
        
        float3 CalculateDrag(float3 velocity, float radius, float airDensity, BalloonLODSystem.LODLevel lod)
        {
            float speed = math.length(velocity);
            if (speed < 0.01f) return float3.zero;
            
            // Simplified drag for higher LODs
            float dragCoefficient = 0.47f; // Sphere drag coefficient
            if (lod >= BalloonLODSystem.LODLevel.LOD2)
            {
                // Use simplified linear drag for higher LODs
                return -velocity * dragCoefficient * 2f;
            }
            
            // Full quadratic drag for LOD0 and LOD1
            float crossSection = math.PI * radius * radius;
            float dragMagnitude = 0.5f * dragCoefficient * airDensity * crossSection * speed * speed;
            float3 dragDirection = -math.normalize(velocity);
            
            return dragDirection * dragMagnitude;
        }
        
        void KeepInBounds(ref BalloonData balloon, Bounds bounds)
        {
            float3 min = new float3(bounds.min.x, bounds.min.y, bounds.min.z);
            float3 max = new float3(bounds.max.x, bounds.max.y, bounds.max.z);
            
            // Clamp position
            balloon.position = math.clamp(balloon.position, min + balloon.radius, max - balloon.radius);
            
            // Bounce off boundaries
            if (balloon.position.x <= min.x + balloon.radius || balloon.position.x >= max.x - balloon.radius)
                balloon.velocity.x *= -0.5f;
            if (balloon.position.y <= min.y + balloon.radius || balloon.position.y >= max.y - balloon.radius)
                balloon.velocity.y *= -0.5f;
            if (balloon.position.z <= min.z + balloon.radius || balloon.position.z >= max.z - balloon.radius)
                balloon.velocity.z *= -0.5f;
        }
    }
}