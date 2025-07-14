using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// Simplified collision detection that stores collision pairs
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct CollisionDetectionJob : IJob
    {
        [ReadOnly] public NativeArray<BalloonData> balloons;
        [ReadOnly] public NativeParallelMultiHashMap<int, int> spatialHash;
        [ReadOnly] public SpatialHashGrid grid;
        
        public NativeList<CollisionPair> collisionPairs;
        
        public void Execute()
        {
            // Clear previous collision pairs
            collisionPairs.Clear();
            
            // Check each balloon
            for (int i = 0; i < balloons.Length; i++)
            {
                BalloonData balloon = balloons[i];
                int hashKey = grid.GetHashKey(balloon.position);
                
                // Check all neighboring cells
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            int3 neighborCell = grid.GetGridPosition(balloon.position) + new int3(dx, dy, dz);
                            int neighborHash = grid.GetHashKey(neighborCell);
                            
                            // Check all balloons in this cell
                            if (spatialHash.TryGetFirstValue(neighborHash, out int neighborIndex, out var iterator))
                            {
                                do
                                {
                                    // Only process each pair once (i < neighborIndex)
                                    if (neighborIndex > i)
                                    {
                                        BalloonData neighbor = balloons[neighborIndex];
                                        float distance = math.distance(balloon.position, neighbor.position);
                                        float minDistance = balloon.radius + neighbor.radius;
                                        
                                        if (distance < minDistance && distance > 0.0001f)
                                        {
                                            collisionPairs.Add(new CollisionPair
                                            {
                                                indexA = i,
                                                indexB = neighborIndex,
                                                distance = distance,
                                                minDistance = minDistance
                                            });
                                        }
                                    }
                                }
                                while (spatialHash.TryGetNextValue(out neighborIndex, ref iterator));
                            }
                        }
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Resolves detected collisions - simplified without atomic operations
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct CollisionResolutionJob : IJob
    {
        [ReadOnly] public NativeArray<BalloonData> balloons;
        [ReadOnly] public NativeList<CollisionPair> collisionPairs;
        [ReadOnly] public SimulationParameters parameters;
        [ReadOnly] public float deltaTime;
        
        public NativeArray<float3> velocityDeltas;
        
        public void Execute()
        {
            // Process all collision pairs sequentially
            for (int i = 0; i < collisionPairs.Length; i++)
            {
                CollisionPair pair = collisionPairs[i];
                BalloonData balloonA = balloons[pair.indexA];
                BalloonData balloonB = balloons[pair.indexB];
                
                // Calculate collision normal
                float3 delta = balloonB.position - balloonA.position;
                float3 normal = delta / pair.distance;
                
                // Calculate overlap
                float overlap = pair.minDistance - pair.distance;
                float3 separation = normal * (overlap * 0.5f);
                
                // Calculate relative velocity
                float3 relativeVelocity = balloonB.velocity - balloonA.velocity;
                float velocityAlongNormal = math.dot(relativeVelocity, normal);
                
                // Don't resolve if velocities are separating
                if (velocityAlongNormal > 0)
                    continue;
                
                // Calculate impulse
                float e = parameters.collisionElasticity;
                float impulse = (1f + e) * velocityAlongNormal;
                impulse /= (1f / balloonA.mass + 1f / balloonB.mass);
                
                // Calculate velocity changes
                float3 velocityChangeA = -(impulse / balloonA.mass) * normal;
                float3 velocityChangeB = (impulse / balloonB.mass) * normal;
                
                // Add separation velocities
                velocityChangeA -= separation / deltaTime;
                velocityChangeB += separation / deltaTime;
                
                // Apply velocity changes
                velocityDeltas[pair.indexA] += velocityChangeA;
                velocityDeltas[pair.indexB] += velocityChangeB;
            }
        }
    }
    
    /// <summary>
    /// Collision pair data
    /// </summary>
    public struct CollisionPair
    {
        public int indexA;
        public int indexB;
        public float distance;
        public float minDistance;
    }
}