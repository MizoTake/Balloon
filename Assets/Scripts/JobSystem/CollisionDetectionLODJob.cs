using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// LOD-aware collision detection job that skips collision for distant balloons
    /// </summary>
    [BurstCompile]
    public struct CollisionDetectionLODJob : IJob
    {
        [ReadOnly] public NativeArray<BalloonData> balloons;
        [ReadOnly] public NativeParallelMultiHashMap<int, int> spatialHash;
        [ReadOnly] public SpatialHashGrid grid;
        [ReadOnly] public NativeArray<BalloonLODSystem.LODLevel> lodLevels;
        [ReadOnly] public NativeArray<bool> physicsEnabled;
        
        public NativeList<CollisionPairLOD> collisionPairs;
        
        public void Execute()
        {
            collisionPairs.Clear();
            
            for (int i = 0; i < balloons.Length; i++)
            {
                // Skip if physics is disabled
                if (!physicsEnabled[i])
                    continue;
                    
                // Skip collision detection for LOD3 and LOD4
                if (lodLevels[i] > BalloonLODSystem.LODLevel.LOD2)
                    continue;
                
                var balloon = balloons[i];
                int cellHash = grid.GetHashKey(balloon.position);
                
                // Check current cell and neighboring cells
                for (int offsetX = -1; offsetX <= 1; offsetX++)
                {
                    for (int offsetY = -1; offsetY <= 1; offsetY++)
                    {
                        for (int offsetZ = -1; offsetZ <= 1; offsetZ++)
                        {
                            int3 neighborCell = grid.GetGridPosition(balloon.position) + new int3(offsetX, offsetY, offsetZ);
                            int neighborHash = grid.GetHashKey(neighborCell);
                            
                            if (spatialHash.TryGetFirstValue(neighborHash, out int j, out var iterator))
                            {
                                do
                                {
                                    // Skip self and already processed pairs
                                    if (j <= i) continue;
                                    
                                    // Skip if other balloon has physics disabled
                                    if (!physicsEnabled[j])
                                        continue;
                                    
                                    // Check if collision should be calculated based on both LODs
                                    var otherLod = lodLevels[j];
                                    if (otherLod > BalloonLODSystem.LODLevel.LOD2)
                                        continue;
                                    
                                    var other = balloons[j];
                                    float distance = math.distance(balloon.position, other.position);
                                    float minDistance = balloon.radius + other.radius;
                                    
                                    if (distance < minDistance)
                                    {
                                        // Use the higher LOD level between the two balloons
                                        var effectiveLod = (BalloonLODSystem.LODLevel)math.max((int)lodLevels[i], (int)lodLevels[j]);
                                        
                                        collisionPairs.Add(new CollisionPairLOD
                                        {
                                            indexA = i,
                                            indexB = j,
                                            distance = distance,
                                            minDistance = minDistance,
                                            lodLevel = (byte)effectiveLod
                                        });
                                    }
                                } while (spatialHash.TryGetNextValue(out j, ref iterator));
                            }
                        }
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Enhanced collision pair with LOD information
    /// </summary>
    public struct CollisionPairLOD
    {
        public int indexA;
        public int indexB;
        public float distance;
        public float minDistance;
        public byte lodLevel;
    }
}