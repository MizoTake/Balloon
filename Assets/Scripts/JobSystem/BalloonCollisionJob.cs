using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// Job for handling balloon-to-balloon collisions using spatial hash grid
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct BalloonCollisionJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<BalloonData> balloons;
        
        [ReadOnly] public NativeParallelMultiHashMap<int, int> spatialHash;
        [ReadOnly] public SpatialHashGrid grid;
        [ReadOnly] public SimulationParameters parameters;
        [ReadOnly] public float deltaTime;
        
        // Thread-safe velocity updates using atomic operations
        [NativeDisableParallelForRestriction]
        public NativeArray<float3> velocityDeltas;
        
        public void Execute(int index)
        {
            BalloonData balloon = balloons[index];
            float3 totalVelocityDelta = float3.zero;
            
            // Get hash key for current balloon position
            int hashKey = grid.GetHashKey(balloon.position);
            
            // Check all neighboring cells (3x3x3 grid)
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dz = -1; dz <= 1; dz++)
                    {
                        int3 neighborCell = grid.GetGridPosition(balloon.position) + new int3(dx, dy, dz);
                        int neighborHash = grid.GetHashKey(neighborCell);
                        
                        // Iterate through all balloons in this cell
                        if (spatialHash.TryGetFirstValue(neighborHash, out int neighborIndex, out var iterator))
                        {
                            do
                            {
                                if (neighborIndex != index) // Don't collide with self
                                {
                                    BalloonData neighbor = balloons[neighborIndex];
                                    float3 collisionResponse = CalculateCollisionResponse(balloon, neighbor);
                                    totalVelocityDelta += collisionResponse;
                                }
                            }
                            while (spatialHash.TryGetNextValue(out neighborIndex, ref iterator));
                        }
                    }
                }
            }
            
            // Store velocity delta for later application
            velocityDeltas[index] = totalVelocityDelta;
        }
        
        /// <summary>
        /// Calculates collision response between two balloons
        /// </summary>
        private float3 CalculateCollisionResponse(BalloonData balloon1, BalloonData balloon2)
        {
            float3 delta = balloon2.position - balloon1.position;
            float distance = math.length(delta);
            float minDistance = balloon1.radius + balloon2.radius;
            
            // Check if balloons are colliding
            if (distance < minDistance && distance > 0.0001f)
            {
                // Normalize collision direction
                float3 normal = delta / distance;
                
                // Calculate overlap amount
                float overlap = minDistance - distance;
                
                // Separate balloons to prevent penetration
                float3 separation = normal * (overlap * 0.5f);
                
                // Calculate relative velocity
                float3 relativeVelocity = balloon2.velocity - balloon1.velocity;
                float velocityAlongNormal = math.dot(relativeVelocity, normal);
                
                // Don't resolve if velocities are separating
                if (velocityAlongNormal > 0)
                {
                    return separation / deltaTime;
                }
                
                // Calculate impulse scalar
                float e = parameters.collisionElasticity;
                float impulse = (1f + e) * velocityAlongNormal;
                impulse /= (1f / balloon1.mass + 1f / balloon2.mass);
                
                // Apply impulse to velocity
                float3 velocityChange = (impulse / balloon1.mass) * normal;
                
                // Combine separation and velocity change
                return velocityChange + (separation / deltaTime);
            }
            
            return float3.zero;
        }
    }
    
    /// <summary>
    /// Job to apply collision velocity deltas to balloons
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct ApplyCollisionDeltasJob : IJobParallelFor
    {
        public NativeArray<BalloonData> balloons;
        [ReadOnly] public NativeArray<float3> velocityDeltas;
        [ReadOnly] public float maxVelocity;
        
        public void Execute(int index)
        {
            BalloonData balloon = balloons[index];
            
            // Apply velocity delta from collisions
            balloon.velocity += velocityDeltas[index];
            
            // Clamp velocity to prevent instability
            float speed = math.length(balloon.velocity);
            if (speed > maxVelocity)
            {
                balloon.velocity = (balloon.velocity / speed) * maxVelocity;
            }
            
            // Update matrix for rendering
            balloon.UpdateMatrix();
            
            balloons[index] = balloon;
        }
    }
}