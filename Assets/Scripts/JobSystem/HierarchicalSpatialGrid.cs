using Unity.Collections;
using Unity.Mathematics;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// Hierarchical spatial grid for efficient collision detection
    /// Supports multiple levels of detail for performance optimization
    /// </summary>
    public struct HierarchicalSpatialGrid : System.IDisposable
    {
        // Grid levels (0 = coarse, 1 = medium, 2 = fine)
        public NativeArray<SpatialCell> level0Cells;
        public NativeArray<SpatialCell> level1Cells;
        public NativeArray<SpatialCell> level2Cells;
        
        // Grid configuration
        public float baseCellSize;
        public float3 gridOrigin;
        public int3 gridDimensions;
        public float densityThreshold;
        public int maxObjectsPerCell;
        
        // Cell metadata
        public NativeArray<int> cellObjectCounts;
        public NativeArray<int> cellStartIndices;
        
        // Constants
        private const int LEVEL_0_SCALE = 4;
        private const int LEVEL_1_SCALE = 2;
        private const int LEVEL_2_SCALE = 1;
        
        public HierarchicalSpatialGrid(float cellSize, float3 origin, int3 dimensions, int maxObjects, Allocator allocator)
        {
            baseCellSize = cellSize;
            gridOrigin = origin;
            gridDimensions = dimensions;
            densityThreshold = 10f;
            maxObjectsPerCell = maxObjects;
            
            // Calculate total cells per level
            int totalCellsL0 = (dimensions.x / LEVEL_0_SCALE) * (dimensions.y / LEVEL_0_SCALE) * (dimensions.z / LEVEL_0_SCALE);
            int totalCellsL1 = (dimensions.x / LEVEL_1_SCALE) * (dimensions.y / LEVEL_1_SCALE) * (dimensions.z / LEVEL_1_SCALE);
            int totalCellsL2 = dimensions.x * dimensions.y * dimensions.z;
            
            // Allocate cell arrays
            level0Cells = new NativeArray<SpatialCell>(totalCellsL0, allocator);
            level1Cells = new NativeArray<SpatialCell>(totalCellsL1, allocator);
            level2Cells = new NativeArray<SpatialCell>(totalCellsL2, allocator);
            
            // Allocate metadata
            cellObjectCounts = new NativeArray<int>(totalCellsL2, allocator);
            cellStartIndices = new NativeArray<int>(totalCellsL2, allocator);
            
            // Initialize cells
            InitializeCells();
        }
        
        private void InitializeCells()
        {
            // Initialize level 0 (coarse)
            for (int i = 0; i < level0Cells.Length; i++)
            {
                level0Cells[i] = new SpatialCell
                {
                    bounds = CalculateCellBounds(i, 0),
                    objectCount = 0,
                    startIndex = -1
                };
            }
            
            // Initialize level 1 (medium)
            for (int i = 0; i < level1Cells.Length; i++)
            {
                level1Cells[i] = new SpatialCell
                {
                    bounds = CalculateCellBounds(i, 1),
                    objectCount = 0,
                    startIndex = -1
                };
            }
            
            // Initialize level 2 (fine)
            for (int i = 0; i < level2Cells.Length; i++)
            {
                level2Cells[i] = new SpatialCell
                {
                    bounds = CalculateCellBounds(i, 2),
                    objectCount = 0,
                    startIndex = -1
                };
            }
        }
        
        /// <summary>
        /// Calculates the bounds of a cell at a given level
        /// </summary>
        private Bounds CalculateCellBounds(int cellIndex, int level)
        {
            int scale = level == 0 ? LEVEL_0_SCALE : (level == 1 ? LEVEL_1_SCALE : LEVEL_2_SCALE);
            int3 dims = gridDimensions / scale;
            
            int x = cellIndex % dims.x;
            int y = (cellIndex / dims.x) % dims.y;
            int z = cellIndex / (dims.x * dims.y);
            
            float cellSizeAtLevel = baseCellSize * scale;
            float3 cellCenter = gridOrigin + new float3(x + 0.5f, y + 0.5f, z + 0.5f) * cellSizeAtLevel;
            
            return new Bounds(cellCenter, Vector3.one * cellSizeAtLevel);
        }
        
        /// <summary>
        /// Gets the appropriate level for a given density
        /// </summary>
        public int GetAppropriateLevel(float localDensity)
        {
            if (localDensity > densityThreshold * 2f)
                return 2; // Fine level for high density
            else if (localDensity > densityThreshold)
                return 1; // Medium level
            else
                return 0; // Coarse level for low density
        }
        
        /// <summary>
        /// Gets the cell index for a position at a specific level
        /// </summary>
        public int GetCellIndex(float3 position, int level)
        {
            int scale = level == 0 ? LEVEL_0_SCALE : (level == 1 ? LEVEL_1_SCALE : LEVEL_2_SCALE);
            float cellSizeAtLevel = baseCellSize * scale;
            int3 dims = gridDimensions / scale;
            
            float3 localPos = position - gridOrigin;
            int3 cellCoords = (int3)(localPos / cellSizeAtLevel);
            
            // Clamp to grid bounds
            cellCoords = math.clamp(cellCoords, 0, dims - 1);
            
            return cellCoords.x + cellCoords.y * dims.x + cellCoords.z * dims.x * dims.y;
        }
        
        /// <summary>
        /// Gets neighboring cell indices for broad phase collision detection
        /// </summary>
        public void GetNeighboringCells(float3 position, float searchRadius, int level, NativeList<int> neighbors)
        {
            int scale = level == 0 ? LEVEL_0_SCALE : (level == 1 ? LEVEL_1_SCALE : LEVEL_2_SCALE);
            float cellSizeAtLevel = baseCellSize * scale;
            int3 dims = gridDimensions / scale;
            
            float3 localPos = position - gridOrigin;
            int3 centerCell = (int3)(localPos / cellSizeAtLevel);
            int searchCells = (int)math.ceil(searchRadius / cellSizeAtLevel);
            
            for (int dx = -searchCells; dx <= searchCells; dx++)
            {
                for (int dy = -searchCells; dy <= searchCells; dy++)
                {
                    for (int dz = -searchCells; dz <= searchCells; dz++)
                    {
                        int3 neighborCoords = centerCell + new int3(dx, dy, dz);
                        
                        // Check bounds
                        if (math.all(neighborCoords >= 0) && math.all(neighborCoords < dims))
                        {
                            int neighborIndex = neighborCoords.x + neighborCoords.y * dims.x + neighborCoords.z * dims.x * dims.y;
                            neighbors.Add(neighborIndex);
                        }
                    }
                }
            }
        }
        
        public void Dispose()
        {
            if (level0Cells.IsCreated) level0Cells.Dispose();
            if (level1Cells.IsCreated) level1Cells.Dispose();
            if (level2Cells.IsCreated) level2Cells.Dispose();
            if (cellObjectCounts.IsCreated) cellObjectCounts.Dispose();
            if (cellStartIndices.IsCreated) cellStartIndices.Dispose();
        }
    }
    
    /// <summary>
    /// Represents a single cell in the spatial grid
    /// </summary>
    [System.Serializable]
    public struct SpatialCell
    {
        public Bounds bounds;
        public int objectCount;
        public int startIndex;
        public float density;
    }
    
    /// <summary>
    /// Job to update the spatial grid with balloon positions
    /// </summary>
    [BurstCompile]
    public struct UpdateSpatialGridJob : IJob
    {
        [ReadOnly] public NativeArray<EnhancedBalloonData> balloons;
        [ReadOnly] public NativeArray<int> activeIndices;
        public HierarchicalSpatialGrid grid;
        public NativeArray<int> balloonToCellMapping;
        
        public void Execute()
        {
            // Reset cell counts
            for (int i = 0; i < grid.cellObjectCounts.Length; i++)
            {
                grid.cellObjectCounts[i] = 0;
            }
            
            // First pass: count objects per cell
            for (int i = 0; i < activeIndices.Length; i++)
            {
                int balloonIndex = activeIndices[i];
                var balloon = balloons[balloonIndex];
                
                // For now, use level 2 (finest) for all balloons
                int cellIndex = grid.GetCellIndex(balloon.position, 2);
                grid.cellObjectCounts[cellIndex]++;
                balloonToCellMapping[balloonIndex] = cellIndex;
            }
            
            // Calculate start indices
            int runningIndex = 0;
            for (int i = 0; i < grid.cellObjectCounts.Length; i++)
            {
                grid.cellStartIndices[i] = runningIndex;
                runningIndex += grid.cellObjectCounts[i];
                
                // Update density
                float cellVolume = grid.baseCellSize * grid.baseCellSize * grid.baseCellSize;
                float density = grid.cellObjectCounts[i] / cellVolume;
                
                var cell = grid.level2Cells[i];
                cell.density = density;
                cell.objectCount = grid.cellObjectCounts[i];
                cell.startIndex = grid.cellStartIndices[i];
                grid.level2Cells[i] = cell;
            }
        }
    }
    
    /// <summary>
    /// Job to query nearby balloons using the spatial grid
    /// </summary>
    [BurstCompile]
    public struct SpatialQueryJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<EnhancedBalloonData> balloons;
        [ReadOnly] public NativeArray<int> activeIndices;
        [ReadOnly] public HierarchicalSpatialGrid grid;
        [ReadOnly] public NativeArray<int> balloonToCellMapping;
        public NativeArray<int> nearbyBalloonCounts;
        
        public void Execute(int index)
        {
            int balloonIndex = activeIndices[index];
            var balloon = balloons[balloonIndex];
            
            var neighbors = new NativeList<int>(27, Allocator.Temp);
            grid.GetNeighboringCells(balloon.position, balloon.radius * 2f, 2, neighbors);
            
            int nearbyCount = 0;
            for (int i = 0; i < neighbors.Length; i++)
            {
                int cellIndex = neighbors[i];
                if (cellIndex >= 0 && cellIndex < grid.level2Cells.Length)
                {
                    nearbyCount += grid.level2Cells[cellIndex].objectCount;
                }
            }
            
            nearbyBalloonCounts[index] = nearbyCount;
            neighbors.Dispose();
        }
    }
}