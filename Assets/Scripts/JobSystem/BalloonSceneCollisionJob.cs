using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// Job for handling balloon collisions with scene colliders
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct BalloonSceneCollisionJob : IJobParallelFor
    {
        public NativeArray<BalloonData> balloons;
        
        [ReadOnly] public NativeArray<SceneColliderData> sceneColliders;
        [ReadOnly] public SimulationParameters parameters;
        [ReadOnly] public float deltaTime;
        
        public void Execute(int index)
        {
            BalloonData balloon = balloons[index];
            float3 totalPush = float3.zero;
            float maxPenetration = 0f;
            
            // Check collision with all scene colliders
            for (int i = 0; i < sceneColliders.Length; i++)
            {
                SceneColliderData collider = sceneColliders[i];
                
                if (collider.TestSphereCollision(balloon.position, balloon.radius, 
                    out float3 pushDirection, out float penetrationDepth))
                {
                    // Accumulate push forces
                    totalPush += pushDirection * penetrationDepth;
                    maxPenetration = math.max(maxPenetration, penetrationDepth);
                    
                    // Calculate collision response
                    float velocityAlongNormal = math.dot(balloon.velocity, pushDirection);
                    
                    // Only apply impulse if moving towards collider
                    if (velocityAlongNormal < 0)
                    {
                        // Reflect velocity
                        balloon.velocity -= pushDirection * velocityAlongNormal * (1f + parameters.collisionElasticity);
                        
                        // Apply damping
                        balloon.velocity *= 0.95f;
                    }
                }
            }
            
            // Apply position correction to prevent penetration
            if (maxPenetration > 0)
            {
                balloon.position += math.normalize(totalPush) * maxPenetration;
                
                // Ensure balloon stays within world bounds after collision
                ApplyWorldBounds(ref balloon);
            }
            
            // Update transform matrix
            balloon.UpdateMatrix();
            
            balloons[index] = balloon;
        }
        
        private void ApplyWorldBounds(ref BalloonData balloon)
        {
            float3 min = new float3(parameters.worldBounds.min.x, parameters.worldBounds.min.y, parameters.worldBounds.min.z);
            float3 max = new float3(parameters.worldBounds.max.x, parameters.worldBounds.max.y, parameters.worldBounds.max.z);
            
            // Clamp position within bounds
            balloon.position = math.clamp(balloon.position, min + balloon.radius, max - balloon.radius);
        }
    }
}