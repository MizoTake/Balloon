using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using System;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// Manages memory allocation and pooling for balloon simulation
    /// </summary>
    public class BalloonMemoryManager : IDisposable
    {
        // Native arrays for balloon data
        private NativeArray<EnhancedBalloonData> balloonDataArray;
        private NativeArray<int> activeBalloonIndices;
        private NativeArray<int> freeBalloonIndices;
        
        // Collision data pools
        private NativeArray<CollisionPair> collisionPairs;
        private NativeArray<DeformationData> deformationData;
        
        // Counters
        private NativeArray<int> activeBalloonCount;
        private NativeArray<int> freeIndexCount;
        
        // Configuration
        private readonly int maxBalloonCount;
        private readonly int maxCollisionPairs;
        
        // Memory tracking
        private long totalAllocatedMemory;
        private long peakMemoryUsage;
        
        public int ActiveBalloonCount => activeBalloonCount[0];
        public int MaxBalloonCount => maxBalloonCount;
        public long TotalMemoryUsage => totalAllocatedMemory;
        public long PeakMemoryUsage => peakMemoryUsage;
        
        public BalloonMemoryManager()
        {
        }
        
        public BalloonMemoryManager(int maxBalloons = 50000, int maxCollisions = 100000)
        {
            maxBalloonCount = maxBalloons;
            maxCollisionPairs = maxCollisions;
            
            AllocateMemory();
        }
        
        public void Initialize(int maxBalloons = 50000, int maxCollisions = 100000)
        {
            // Use reflection to set readonly fields during initialization
            var maxBalloonField = typeof(BalloonMemoryManager).GetField("maxBalloonCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var maxCollisionField = typeof(BalloonMemoryManager).GetField("maxCollisionPairs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            maxBalloonField?.SetValue(this, maxBalloons);
            maxCollisionField?.SetValue(this, maxCollisions);
            
            AllocateMemory();
        }
        
        private void AllocateMemory()
        {
            // Allocate balloon data
            balloonDataArray = new NativeArray<EnhancedBalloonData>(maxBalloonCount, Allocator.Persistent);
            activeBalloonIndices = new NativeArray<int>(maxBalloonCount, Allocator.Persistent);
            freeBalloonIndices = new NativeArray<int>(maxBalloonCount, Allocator.Persistent);
            
            // Allocate collision data
            collisionPairs = new NativeArray<CollisionPair>(maxCollisionPairs, Allocator.Persistent);
            deformationData = new NativeArray<DeformationData>(maxBalloonCount, Allocator.Persistent);
            
            // Allocate counters
            activeBalloonCount = new NativeArray<int>(1, Allocator.Persistent);
            freeIndexCount = new NativeArray<int>(1, Allocator.Persistent);
            
            // Initialize free indices
            for (int i = 0; i < maxBalloonCount; i++)
            {
                freeBalloonIndices[i] = i;
            }
            freeIndexCount[0] = maxBalloonCount;
            activeBalloonCount[0] = 0;
            
            // Calculate memory usage
            totalAllocatedMemory = 0;
            totalAllocatedMemory += UnsafeUtility.SizeOf<EnhancedBalloonData>() * maxBalloonCount;
            totalAllocatedMemory += sizeof(int) * maxBalloonCount * 2; // active and free indices
            totalAllocatedMemory += UnsafeUtility.SizeOf<CollisionPair>() * maxCollisionPairs;
            totalAllocatedMemory += UnsafeUtility.SizeOf<DeformationData>() * maxBalloonCount;
            totalAllocatedMemory += sizeof(int) * 2; // counters
            
            peakMemoryUsage = totalAllocatedMemory;
            
            Debug.Log($"[BalloonMemoryManager] Allocated {totalAllocatedMemory / (1024 * 1024)}MB for balloon simulation");
        }
        
        /// <summary>
        /// Allocates a new balloon from the pool
        /// </summary>
        public int AllocateBalloon(EnhancedBalloonData balloonData)
        {
            if (freeIndexCount[0] <= 0)
            {
                Debug.LogWarning("[BalloonMemoryManager] No free balloon slots available");
                return -1;
            }
            
            // Get a free index
            int freeIndex = freeBalloonIndices[freeIndexCount[0] - 1];
            freeIndexCount[0]--;
            
            // Add to active indices
            activeBalloonIndices[activeBalloonCount[0]] = freeIndex;
            activeBalloonCount[0]++;
            
            // Store balloon data
            balloonDataArray[freeIndex] = balloonData;
            
            return freeIndex;
        }
        
        /// <summary>
        /// Deallocates a balloon and returns it to the pool
        /// </summary>
        public void DeallocateBalloon(int balloonIndex)
        {
            if (balloonIndex < 0 || balloonIndex >= maxBalloonCount)
                return;
            
            // Find and remove from active indices
            for (int i = 0; i < activeBalloonCount[0]; i++)
            {
                if (activeBalloonIndices[i] == balloonIndex)
                {
                    // Swap with last active and reduce count
                    activeBalloonIndices[i] = activeBalloonIndices[activeBalloonCount[0] - 1];
                    activeBalloonCount[0]--;
                    
                    // Add to free indices
                    freeBalloonIndices[freeIndexCount[0]] = balloonIndex;
                    freeIndexCount[0]++;
                    
                    break;
                }
            }
        }
        
        /// <summary>
        /// Gets native arrays for job processing
        /// </summary>
        public NativeArray<EnhancedBalloonData> GetBalloonDataArray() => balloonDataArray;
        public NativeArray<int> GetActiveBalloonIndices() => activeBalloonIndices.GetSubArray(0, activeBalloonCount[0]);
        public NativeArray<CollisionPair> GetCollisionPairs() => collisionPairs;
        public NativeArray<DeformationData> GetDeformationData() => deformationData;
        
        /// <summary>
        /// Cleans up unused resources and performs garbage collection
        /// </summary>
        public void CleanupUnusedResources()
        {
            // Compact active balloon indices
            if (activeBalloonCount[0] < activeBalloonIndices.Length / 2)
            {
                Debug.Log($"[BalloonMemoryManager] Compacting memory - Active: {activeBalloonCount[0]}, Total: {maxBalloonCount}");
            }
            
            // Force garbage collection if needed
            if (GC.GetTotalMemory(false) > peakMemoryUsage * 1.5f)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }
        
        /// <summary>
        /// Detects potential memory leaks
        /// </summary>
        public void DetectMemoryLeaks()
        {
            long currentMemory = GC.GetTotalMemory(false);
            if (currentMemory > peakMemoryUsage)
            {
                peakMemoryUsage = currentMemory;
                Debug.LogWarning($"[BalloonMemoryManager] New peak memory usage: {peakMemoryUsage / (1024 * 1024)}MB");
            }
            
            // Check for consistency
            int totalIndices = activeBalloonCount[0] + freeIndexCount[0];
            if (totalIndices != maxBalloonCount)
            {
                Debug.LogError($"[BalloonMemoryManager] Memory leak detected! Expected {maxBalloonCount} indices, found {totalIndices}");
            }
        }
        
        /// <summary>
        /// Gets memory usage in megabytes
        /// </summary>
        public float GetMemoryUsageMB()
        {
            return totalAllocatedMemory / (1024.0f * 1024.0f);
        }
        
        public void Dispose()
        {
            if (balloonDataArray.IsCreated) balloonDataArray.Dispose();
            if (activeBalloonIndices.IsCreated) activeBalloonIndices.Dispose();
            if (freeBalloonIndices.IsCreated) freeBalloonIndices.Dispose();
            if (collisionPairs.IsCreated) collisionPairs.Dispose();
            if (deformationData.IsCreated) deformationData.Dispose();
            if (activeBalloonCount.IsCreated) activeBalloonCount.Dispose();
            if (freeIndexCount.IsCreated) freeIndexCount.Dispose();
        }
    }
    
    /// <summary>
    /// Stores deformation data for a balloon
    /// </summary>
    [System.Serializable]
    public struct DeformationData
    {
        public float4x4 deformationMatrix;
        public float3 deformationVelocity;
        public float deformationAmount;
        public float recoveryTimer;
    }
}