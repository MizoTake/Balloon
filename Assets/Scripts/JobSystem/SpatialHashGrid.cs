using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// Spatial hash grid for efficient collision detection
    /// </summary>
    public struct SpatialHashGrid
    {
        public float cellSize;
        public int3 gridDimensions;
        public float3 gridOrigin;
        
        public SpatialHashGrid(float cellSize, Bounds worldBounds)
        {
            this.cellSize = cellSize;
            this.gridOrigin = new float3(worldBounds.min.x, worldBounds.min.y, worldBounds.min.z);
            
            float3 worldSize = new float3(worldBounds.size.x, worldBounds.size.y, worldBounds.size.z);
            this.gridDimensions = new int3(
                (int)math.ceil(worldSize.x / cellSize),
                (int)math.ceil(worldSize.y / cellSize),
                (int)math.ceil(worldSize.z / cellSize)
            );
        }
        
        /// <summary>
        /// Converts a world position to grid coordinates
        /// </summary>
        public int3 GetGridPosition(float3 worldPosition)
        {
            float3 localPos = worldPosition - gridOrigin;
            return new int3(
                (int)math.floor(localPos.x / cellSize),
                (int)math.floor(localPos.y / cellSize),
                (int)math.floor(localPos.z / cellSize)
            );
        }
        
        /// <summary>
        /// Converts grid coordinates to a hash value
        /// </summary>
        public int GetHashKey(int3 gridPos)
        {
            // Clamp to grid bounds
            gridPos = math.clamp(gridPos, int3.zero, gridDimensions - 1);
            
            // Use Morton encoding (Z-order curve) for better spatial locality
            return MortonEncode3D(gridPos);
        }
        
        /// <summary>
        /// Gets hash key directly from world position
        /// </summary>
        public int GetHashKey(float3 worldPosition)
        {
            return GetHashKey(GetGridPosition(worldPosition));
        }
        
        /// <summary>
        /// Morton encoding for 3D coordinates (up to 10 bits per axis)
        /// </summary>
        private int MortonEncode3D(int3 pos)
        {
            int x = math.min(pos.x, 1023);
            int y = math.min(pos.y, 1023);
            int z = math.min(pos.z, 1023);
            
            x = (x | (x << 16)) & 0x030000FF;
            x = (x | (x << 8)) & 0x0300F00F;
            x = (x | (x << 4)) & 0x030C30C3;
            x = (x | (x << 2)) & 0x09249249;
            
            y = (y | (y << 16)) & 0x030000FF;
            y = (y | (y << 8)) & 0x0300F00F;
            y = (y | (y << 4)) & 0x030C30C3;
            y = (y | (y << 2)) & 0x09249249;
            
            z = (z | (z << 16)) & 0x030000FF;
            z = (z | (z << 8)) & 0x0300F00F;
            z = (z | (z << 4)) & 0x030C30C3;
            z = (z | (z << 2)) & 0x09249249;
            
            return x | (y << 1) | (z << 2);
        }
    }
    
    /// <summary>
    /// Job to build spatial hash grid from balloon positions
    /// </summary>
    [BurstCompile(CompileSynchronously = true)]
    public struct BuildSpatialHashJob : IJob
    {
        [ReadOnly] public NativeArray<BalloonData> balloons;
        [ReadOnly] public SpatialHashGrid grid;
        
        public NativeParallelMultiHashMap<int, int> spatialHash;
        
        public void Execute()
        {
            spatialHash.Clear();
            
            for (int i = 0; i < balloons.Length; i++)
            {
                float3 pos = balloons[i].position;
                float radius = balloons[i].radius;
                
                // Get all grid cells that this balloon overlaps
                int3 minGrid = grid.GetGridPosition(pos - radius);
                int3 maxGrid = grid.GetGridPosition(pos + radius);
                
                // Add balloon to all overlapping cells
                for (int x = minGrid.x; x <= maxGrid.x; x++)
                {
                    for (int y = minGrid.y; y <= maxGrid.y; y++)
                    {
                        for (int z = minGrid.z; z <= maxGrid.z; z++)
                        {
                            int hashKey = grid.GetHashKey(new int3(x, y, z));
                            spatialHash.Add(hashKey, i);
                        }
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Helper to get neighbor indices from spatial hash
    /// </summary>
    public struct NeighborIterator
    {
        private NativeParallelMultiHashMapIterator<int> enumerator;
        private NativeParallelMultiHashMap<int, int> hashMap;
        private bool isValid;
        private int currentValue;
        
        public NeighborIterator(NativeParallelMultiHashMap<int, int> spatialHash, int hashKey)
        {
            hashMap = spatialHash;
            isValid = spatialHash.TryGetFirstValue(hashKey, out currentValue, out enumerator);
            Current = currentValue;
        }
        
        public bool IsValid => isValid;
        public int Current { get; private set; }
        
        public bool MoveNext()
        {
            isValid = hashMap.TryGetNextValue(out currentValue, ref enumerator);
            if (isValid)
            {
                Current = currentValue;
            }
            return isValid;
        }
    }
}