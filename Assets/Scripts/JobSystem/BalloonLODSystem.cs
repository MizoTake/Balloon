using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;
using UnityEngine;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// Level of Detail system for balloon physics optimization
    /// Reduces physics calculations based on distance from camera
    /// </summary>
    public class BalloonLODSystem : MonoBehaviour
    {
        [Header("LOD Settings")]
        [SerializeField] private float lodDistance1 = 30f;  // Full physics
        [SerializeField] private float lodDistance2 = 50f;  // Simplified physics
        [SerializeField] private float lodDistance3 = 80f;  // Basic physics
        [SerializeField] private float lodDistance4 = 120f; // Minimal physics
        [SerializeField] private float cullDistance = 150f; // No physics beyond this
        
        [Header("LOD Physics Settings")]
        [SerializeField] private bool enableDistanceBasedPhysics = true;
        [SerializeField] private bool enableCollisionLOD = true;
        [SerializeField] private bool enableWindLOD = true;
        
        [Header("Performance")]
        [SerializeField] private int updateFrequency = 3; // Update LOD every N frames
        
        // Native arrays for LOD data
        private NativeArray<LODLevel> balloonLODs;
        private NativeArray<float> balloonDistances;
        private NativeArray<bool> physicsEnabled;
        
        // Camera reference
        private Transform cameraTransform;
        private int frameCounter = 0;
        
        // Performance metrics
        private int activeBalloons = 0;
        private int[] lodCounts = new int[5];
        
        public enum LODLevel : byte
        {
            LOD0 = 0, // Full quality (closest)
            LOD1 = 1, // High quality
            LOD2 = 2, // Medium quality
            LOD3 = 3, // Low quality
            LOD4 = 4  // Minimal quality
        }
        
        private void Awake()
        {
            // Find main camera
            cameraTransform = Camera.main?.transform;
            if (cameraTransform == null)
            {
                Debug.LogError("BalloonLODSystem: No main camera found!");
                enabled = false;
            }
        }
        
        /// <summary>
        /// Initialize LOD system with balloon count
        /// </summary>
        public void Initialize(int balloonCount)
        {
            // Dispose existing arrays if any
            Cleanup();
            
            // Allocate native arrays
            balloonLODs = new NativeArray<LODLevel>(balloonCount, Allocator.Persistent);
            balloonDistances = new NativeArray<float>(balloonCount, Allocator.Persistent);
            physicsEnabled = new NativeArray<bool>(balloonCount, Allocator.Persistent);
            
            // Initialize all balloons as active
            for (int i = 0; i < balloonCount; i++)
            {
                balloonLODs[i] = LODLevel.LOD0;
                physicsEnabled[i] = true;
            }
            
            Debug.Log($"BalloonLODSystem initialized for {balloonCount} balloons");
        }
        
        /// <summary>
        /// Update LOD levels for all balloons
        /// </summary>
        public JobHandle UpdateLODs(NativeArray<BalloonData> balloons, JobHandle dependency)
        {
            if (!enableDistanceBasedPhysics || cameraTransform == null)
                return dependency;
            
            // Only update every N frames to save performance
            frameCounter++;
            if (frameCounter % updateFrequency != 0)
                return dependency;
            
            float3 cameraPos = cameraTransform.position;
            
            // Calculate distances and LOD levels
            var lodJob = new CalculateLODJob
            {
                balloons = balloons,
                cameraPosition = cameraPos,
                lodDistance1 = lodDistance1,
                lodDistance2 = lodDistance2,
                lodDistance3 = lodDistance3,
                lodDistance4 = lodDistance4,
                cullDistance = cullDistance,
                balloonLODs = balloonLODs,
                balloonDistances = balloonDistances,
                physicsEnabled = physicsEnabled
            };
            
            return lodJob.Schedule(balloons.Length, 64, dependency);
        }
        
        /// <summary>
        /// Update LOD levels for enhanced balloon data
        /// </summary>
        public JobHandle UpdateLODs(NativeArray<EnhancedBalloonData> balloons, JobHandle dependency)
        {
            if (!enableDistanceBasedPhysics || cameraTransform == null)
                return dependency;
            
            frameCounter++;
            if (frameCounter % updateFrequency != 0)
                return dependency;
            
            float3 cameraPos = cameraTransform.position;
            
            var lodJob = new CalculateEnhancedLODJob
            {
                balloons = balloons,
                cameraPosition = cameraPos,
                lodDistance1 = lodDistance1,
                lodDistance2 = lodDistance2,
                lodDistance3 = lodDistance3,
                lodDistance4 = lodDistance4,
                cullDistance = cullDistance,
                balloonLODs = balloonLODs,
                balloonDistances = balloonDistances,
                physicsEnabled = physicsEnabled
            };
            
            return lodJob.Schedule(balloons.Length, 64, dependency);
        }
        
        /// <summary>
        /// Get LOD level for a specific balloon
        /// </summary>
        public LODLevel GetBalloonLOD(int index)
        {
            if (balloonLODs.IsCreated && index >= 0 && index < balloonLODs.Length)
                return balloonLODs[index];
            return LODLevel.LOD0;
        }
        
        /// <summary>
        /// Check if physics should be calculated for a balloon
        /// </summary>
        public bool IsPhysicsEnabled(int index)
        {
            if (physicsEnabled.IsCreated && index >= 0 && index < physicsEnabled.Length)
                return physicsEnabled[index];
            return true;
        }
        
        /// <summary>
        /// Get native array of physics enabled flags
        /// </summary>
        public NativeArray<bool> GetPhysicsEnabledArray()
        {
            return physicsEnabled;
        }
        
        /// <summary>
        /// Get native array of LOD levels
        /// </summary>
        public NativeArray<LODLevel> GetLODArray()
        {
            return balloonLODs;
        }
        
        /// <summary>
        /// Should collision be calculated based on LOD
        /// </summary>
        public bool ShouldCalculateCollision(LODLevel lod)
        {
            if (!enableCollisionLOD) return true;
            return lod <= LODLevel.LOD2; // Only LOD0, LOD1, LOD2 have collision
        }
        
        /// <summary>
        /// Should wind be calculated based on LOD
        /// </summary>
        public bool ShouldCalculateWind(LODLevel lod)
        {
            if (!enableWindLOD) return true;
            return lod <= LODLevel.LOD1; // Only LOD0 and LOD1 have wind
        }
        
        /// <summary>
        /// Get simplified physics timestep multiplier for LOD
        /// </summary>
        public float GetPhysicsTimestepMultiplier(LODLevel lod)
        {
            switch (lod)
            {
                case LODLevel.LOD0: return 1.0f;   // Full physics
                case LODLevel.LOD1: return 1.5f;   // 1.5x timestep
                case LODLevel.LOD2: return 2.0f;   // 2x timestep
                case LODLevel.LOD3: return 3.0f;   // 3x timestep
                case LODLevel.LOD4: return 4.0f;   // 4x timestep
                default: return 1.0f;
            }
        }
        
        /// <summary>
        /// Update performance metrics
        /// </summary>
        public void UpdateMetrics()
        {
            if (!balloonLODs.IsCreated) return;
            
            // Reset counts
            activeBalloons = 0;
            for (int i = 0; i < lodCounts.Length; i++)
                lodCounts[i] = 0;
            
            // Count balloons per LOD
            for (int i = 0; i < balloonLODs.Length; i++)
            {
                if (physicsEnabled[i])
                {
                    activeBalloons++;
                    lodCounts[(int)balloonLODs[i]]++;
                }
            }
        }
        
        /// <summary>
        /// Get performance statistics
        /// </summary>
        public string GetStatistics()
        {
            UpdateMetrics();
            
            return $"LOD Statistics:\n" +
                   $"Active: {activeBalloons}/{balloonLODs.Length}\n" +
                   $"LOD0: {lodCounts[0]} (Full)\n" +
                   $"LOD1: {lodCounts[1]} (High)\n" +
                   $"LOD2: {lodCounts[2]} (Medium)\n" +
                   $"LOD3: {lodCounts[3]} (Low)\n" +
                   $"LOD4: {lodCounts[4]} (Minimal)\n" +
                   $"Culled: {balloonLODs.Length - activeBalloons}";
        }
        
        private void OnDestroy()
        {
            Cleanup();
        }
        
        private void Cleanup()
        {
            if (balloonLODs.IsCreated) balloonLODs.Dispose();
            if (balloonDistances.IsCreated) balloonDistances.Dispose();
            if (physicsEnabled.IsCreated) physicsEnabled.Dispose();
        }
        
        /// <summary>
        /// Job to calculate LOD levels based on camera distance
        /// </summary>
        [BurstCompile]
        private struct CalculateLODJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<BalloonData> balloons;
            [ReadOnly] public float3 cameraPosition;
            [ReadOnly] public float lodDistance1;
            [ReadOnly] public float lodDistance2;
            [ReadOnly] public float lodDistance3;
            [ReadOnly] public float lodDistance4;
            [ReadOnly] public float cullDistance;
            
            [WriteOnly] public NativeArray<LODLevel> balloonLODs;
            [WriteOnly] public NativeArray<float> balloonDistances;
            [WriteOnly] public NativeArray<bool> physicsEnabled;
            
            public void Execute(int index)
            {
                float3 balloonPos = balloons[index].position;
                float distance = math.distance(cameraPosition, balloonPos);
                
                balloonDistances[index] = distance;
                
                // Determine LOD level
                LODLevel lod;
                bool enabled = true;
                
                if (distance > cullDistance)
                {
                    lod = LODLevel.LOD4;
                    enabled = false; // Disable physics completely
                }
                else if (distance > lodDistance4)
                {
                    lod = LODLevel.LOD4;
                }
                else if (distance > lodDistance3)
                {
                    lod = LODLevel.LOD3;
                }
                else if (distance > lodDistance2)
                {
                    lod = LODLevel.LOD2;
                }
                else if (distance > lodDistance1)
                {
                    lod = LODLevel.LOD1;
                }
                else
                {
                    lod = LODLevel.LOD0;
                }
                
                balloonLODs[index] = lod;
                physicsEnabled[index] = enabled;
            }
        }
        
        /// <summary>
        /// Job to calculate LOD levels for enhanced balloon data
        /// </summary>
        [BurstCompile]
        private struct CalculateEnhancedLODJob : IJobParallelFor
        {
            [ReadOnly] public NativeArray<EnhancedBalloonData> balloons;
            [ReadOnly] public float3 cameraPosition;
            [ReadOnly] public float lodDistance1;
            [ReadOnly] public float lodDistance2;
            [ReadOnly] public float lodDistance3;
            [ReadOnly] public float lodDistance4;
            [ReadOnly] public float cullDistance;
            
            [WriteOnly] public NativeArray<LODLevel> balloonLODs;
            [WriteOnly] public NativeArray<float> balloonDistances;
            [WriteOnly] public NativeArray<bool> physicsEnabled;
            
            public void Execute(int index)
            {
                // Skip if balloon is already destroyed
                if (balloons[index].state == BalloonState.Destroyed)
                {
                    balloonLODs[index] = LODLevel.LOD4;
                    physicsEnabled[index] = false;
                    balloonDistances[index] = float.MaxValue;
                    return;
                }
                
                float3 balloonPos = balloons[index].position;
                float distance = math.distance(cameraPosition, balloonPos);
                
                balloonDistances[index] = distance;
                
                // Determine LOD level
                LODLevel lod;
                bool enabled = true;
                
                if (distance > cullDistance)
                {
                    lod = LODLevel.LOD4;
                    enabled = false;
                }
                else if (distance > lodDistance4)
                {
                    lod = LODLevel.LOD4;
                }
                else if (distance > lodDistance3)
                {
                    lod = LODLevel.LOD3;
                }
                else if (distance > lodDistance2)
                {
                    lod = LODLevel.LOD2;
                }
                else if (distance > lodDistance1)
                {
                    lod = LODLevel.LOD1;
                }
                else
                {
                    lod = LODLevel.LOD0;
                }
                
                balloonLODs[index] = lod;
                physicsEnabled[index] = enabled;
            }
        }
    }
}