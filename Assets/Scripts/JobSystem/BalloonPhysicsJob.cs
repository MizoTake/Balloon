using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// Job for updating balloon physics including buoyancy, gravity, wind, and basic movement
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct BalloonPhysicsJob : IJobParallelFor
    {
        public NativeArray<BalloonData> balloons;
        
        [ReadOnly] public SimulationParameters parameters;
        [ReadOnly] public float deltaTime;
        [ReadOnly] public float time;
        
        public void Execute(int index)
        {
            BalloonData balloon = balloons[index];
            
            // Reset acceleration
            balloon.acceleration = float3.zero;
            
            // Calculate balloon volume (4/3 * π * r³)
            float volume = (4f / 3f) * math.PI * math.pow(balloon.radius, 3);
            
            // Calculate buoyancy force (Archimedes' principle)
            // Buoyancy = air density * gravity * volume
            float3 buoyancyForce = new float3(0, parameters.airDensity * parameters.gravity * volume * balloon.buoyancy, 0);
            
            // Calculate gravity force
            float3 gravityForce = new float3(0, -balloon.mass * parameters.gravity, 0);
            
            // Calculate wind force with Perlin noise variation
            float3 windForce = CalculateWindForce(balloon.position, balloon.radius);
            
            // Calculate air resistance (drag)
            float3 dragForce = -balloon.velocity * parameters.damping * balloon.radius;
            
            // Sum all forces
            float3 totalForce = buoyancyForce + gravityForce + windForce + dragForce;
            
            // Apply Newton's second law: F = ma => a = F/m
            balloon.acceleration = totalForce / balloon.mass;
            
            // Integrate velocity and position using Euler method
            balloon.velocity += balloon.acceleration * deltaTime;
            balloon.position += balloon.velocity * deltaTime;
            
            // Apply world bounds
            ApplyWorldBounds(ref balloon);
            
            // Update transform matrix for rendering
            balloon.UpdateMatrix();
            
            // Write back to array
            balloons[index] = balloon;
        }
        
        /// <summary>
        /// Calculates wind force using Perlin noise for realistic variation
        /// </summary>
        private float3 CalculateWindForce(float3 position, float radius)
        {
            // Use 3D Perlin noise for wind variation
            float noiseScale = 0.1f;
            float timeScale = 0.5f;
            
            float3 windSample = position * noiseScale + time * timeScale;
            
            float windX = noise.snoise(new float2(windSample.y, windSample.z));
            float windY = noise.snoise(new float2(windSample.x, windSample.z)) * 0.3f; // Less vertical wind
            float windZ = noise.snoise(new float2(windSample.x, windSample.y));
            
            float3 windVariation = new float3(windX, windY, windZ);
            
            // Combine base wind direction with variation
            float3 finalWind = parameters.windDirection * parameters.windStrength + windVariation * 2f;
            
            // Wind force proportional to cross-sectional area (πr²)
            float area = math.PI * radius * radius;
            return finalWind * area * 0.1f; // Scale factor for reasonable force
        }
        
        /// <summary>
        /// Keeps balloons within world bounds with elastic collision
        /// </summary>
        private void ApplyWorldBounds(ref BalloonData balloon)
        {
            float3 min = new float3(parameters.worldBounds.min.x, parameters.worldBounds.min.y, parameters.worldBounds.min.z);
            float3 max = new float3(parameters.worldBounds.max.x, parameters.worldBounds.max.y, parameters.worldBounds.max.z);
            
            // Check X bounds
            if (balloon.position.x - balloon.radius < min.x)
            {
                balloon.position.x = min.x + balloon.radius;
                balloon.velocity.x = math.abs(balloon.velocity.x) * parameters.collisionElasticity;
            }
            else if (balloon.position.x + balloon.radius > max.x)
            {
                balloon.position.x = max.x - balloon.radius;
                balloon.velocity.x = -math.abs(balloon.velocity.x) * parameters.collisionElasticity;
            }
            
            // Check Y bounds
            if (balloon.position.y - balloon.radius < min.y)
            {
                balloon.position.y = min.y + balloon.radius;
                balloon.velocity.y = math.abs(balloon.velocity.y) * parameters.collisionElasticity;
            }
            else if (balloon.position.y + balloon.radius > max.y)
            {
                balloon.position.y = max.y - balloon.radius;
                balloon.velocity.y = -math.abs(balloon.velocity.y) * parameters.collisionElasticity;
            }
            
            // Check Z bounds
            if (balloon.position.z - balloon.radius < min.z)
            {
                balloon.position.z = min.z + balloon.radius;
                balloon.velocity.z = math.abs(balloon.velocity.z) * parameters.collisionElasticity;
            }
            else if (balloon.position.z + balloon.radius > max.z)
            {
                balloon.position.z = max.z - balloon.radius;
                balloon.velocity.z = -math.abs(balloon.velocity.z) * parameters.collisionElasticity;
            }
        }
    }
}