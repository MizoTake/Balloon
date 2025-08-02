using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// Enhanced memory management system with advanced pooling and leak detection
    /// Implements comprehensive memory management for large-scale balloon simulations
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
        
        // Enhanced pooling system
        private NativeArrayPool<float3> vectorPool;
        private NativeArrayPool<float4x4> matrixPool;
        private NativeArrayPool<int> intPool;
        
        // Counters
        private NativeArray<int> activeBalloonCount;
        private NativeArray<int> freeIndexCount;
        
        // Configuration
        private readonly int maxBalloonCount;
        private readonly int maxCollisionPairs;
        
        // Memory tracking and leak detection
        private long totalAllocatedMemory;
        private long peakMemoryUsage;
        private Dictionary<IntPtr, AllocationInfo> activeAllocations;
        private float lastGCTime;
        private long lastTotalMemory;
        private int gcCallCount;
        private readonly object memoryLock = new object();
        
        public int ActiveBalloonCount => activeBalloonCount[0];
        public int MaxBalloonCount => maxBalloonCount;
        public long TotalMemoryUsage => totalAllocatedMemory;
        public long PeakMemoryUsage => peakMemoryUsage;
        public int ActiveAllocationCount => activeAllocations?.Count ?? 0;
        public int GCCallCount => gcCallCount;
        public float MemoryEfficiency => totalAllocatedMemory > 0 ? (float)activeBalloonCount[0] / maxBalloonCount : 0f;
        
        public BalloonMemoryManager()
        {
            activeAllocations = new Dictionary<IntPtr, AllocationInfo>();
            lastGCTime = Time.realtimeSinceStartup;
            lastTotalMemory = GC.GetTotalMemory(false);
            gcCallCount = 0;
        }
        
        public BalloonMemoryManager(int maxBalloons = 50000, int maxCollisions = 100000)
        {
            activeAllocations = new Dictionary<IntPtr, AllocationInfo>();
            lastGCTime = Time.realtimeSinceStartup;
            lastTotalMemory = GC.GetTotalMemory(false);
            gcCallCount = 0;
            
            maxBalloonCount = maxBalloons;
            maxCollisionPairs = maxCollisions;
            
            AllocateMemory();
        }
        
        public void Initialize(int maxBalloons = 50000, int maxCollisions = 100000)
        {
            if (activeAllocations == null)
            {
                activeAllocations = new Dictionary<IntPtr, AllocationInfo>();
            }
            
            lastGCTime = Time.realtimeSinceStartup;
            lastTotalMemory = GC.GetTotalMemory(false);
            gcCallCount = 0;
            
            // Use reflection to set readonly fields during initialization
            var maxBalloonField = typeof(BalloonMemoryManager).GetField("maxBalloonCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var maxCollisionField = typeof(BalloonMemoryManager).GetField("maxCollisionPairs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            maxBalloonField?.SetValue(this, maxBalloons);
            maxCollisionField?.SetValue(this, maxCollisions);
            
            AllocateMemory();
        }
        
        private void AllocateMemory()
        {
            lock (memoryLock)
            {
                // Dispose existing allocations if they exist
                DisposeAllocations();
                
                // Allocate balloon data with tracking
                balloonDataArray = AllocateTrackedArray<EnhancedBalloonData>(maxBalloonCount, "BalloonData");
                activeBalloonIndices = AllocateTrackedArray<int>(maxBalloonCount, "ActiveIndices");
                freeBalloonIndices = AllocateTrackedArray<int>(maxBalloonCount, "FreeIndices");
                
                // Allocate collision data with tracking
                collisionPairs = AllocateTrackedArray<CollisionPair>(maxCollisionPairs, "CollisionPairs");
                deformationData = AllocateTrackedArray<DeformationData>(maxBalloonCount, "DeformationData");
                
                // Allocate counters with tracking
                activeBalloonCount = AllocateTrackedArray<int>(1, "ActiveCount");
                freeIndexCount = AllocateTrackedArray<int>(1, "FreeCount");
                
                // Initialize pooling systems
                vectorPool = new NativeArrayPool<float3>(1000, Allocator.Persistent);
                matrixPool = new NativeArrayPool<float4x4>(500, Allocator.Persistent);
                intPool = new NativeArrayPool<int>(2000, Allocator.Persistent);
                
                // Initialize free indices
                for (int i = 0; i < maxBalloonCount; i++)
                {
                    freeBalloonIndices[i] = i;
                }
                freeIndexCount[0] = maxBalloonCount;
                activeBalloonCount[0] = 0;
                
                // Calculate memory usage
                CalculateMemoryUsage();
                
                Debug.Log($"[BalloonMemoryManager] Enhanced allocation complete: {totalAllocatedMemory / (1024 * 1024)}MB, {activeAllocations.Count} tracked allocations");
            }
        }
        
        private NativeArray<T> AllocateTrackedArray<T>(int length, string name) where T : struct
        {
            var array = new NativeArray<T>(length, Allocator.Persistent);
            
            unsafe
            {
                var ptr = new IntPtr(array.GetUnsafePtr());
                var info = new AllocationInfo
                {
                    name = name,
                    size = UnsafeUtility.SizeOf<T>() * length,
                    elementCount = length,
                    allocatedTime = Time.realtimeSinceStartup
                };
                activeAllocations[ptr] = info;
            }
            
            return array;
        }
        
        private void CalculateMemoryUsage()
        {
            totalAllocatedMemory = 0;
            foreach (var allocation in activeAllocations.Values)
            {
                totalAllocatedMemory += allocation.size;
            }
            
            // Add pool memory
            totalAllocatedMemory += vectorPool?.EstimatedSize ?? 0;
            totalAllocatedMemory += matrixPool?.EstimatedSize ?? 0;
            totalAllocatedMemory += intPool?.EstimatedSize ?? 0;
            
            peakMemoryUsage = Math.Max(peakMemoryUsage, totalAllocatedMemory);
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
        /// Enhanced cleanup with smart garbage collection minimization
        /// </summary>
        public void CleanupUnusedResources()
        {
            lock (memoryLock)
            {
                float currentTime = Time.realtimeSinceStartup;
                long currentMemory = GC.GetTotalMemory(false);
                
                // Compact active balloon indices if memory usage is low
                if (activeBalloonCount[0] < activeBalloonIndices.Length / 2)
                {
                    Debug.Log($"[BalloonMemoryManager] Compacting memory - Active: {activeBalloonCount[0]}, Total: {maxBalloonCount}");
                }
                
                // Clean up pools
                vectorPool?.Cleanup();
                matrixPool?.Cleanup();
                intPool?.Cleanup();
                
                // Smart garbage collection - only if memory growth is significant
                bool shouldGC = false;
                if (currentMemory > lastTotalMemory * 1.3f) // 30% increase
                {
                    shouldGC = true;
                }
                else if (currentTime - lastGCTime > 10f && currentMemory > peakMemoryUsage * 1.2f) // 10 seconds since last GC and 20% over peak
                {
                    shouldGC = true;
                }
                
                if (shouldGC)
                {
                    PerformSmartGarbageCollection();
                    lastGCTime = currentTime;
                    gcCallCount++;
                }
                
                lastTotalMemory = currentMemory;
            }
        }
        
        /// <summary>
        /// Performs smart garbage collection with minimal impact
        /// </summary>
        private void PerformSmartGarbageCollection()
        {
            // Use incremental GC if available (Unity 2022.2+)
            #if UNITY_2022_2_OR_NEWER
            GC.Collect(0, GCCollectionMode.Optimized);
            #else
            GC.Collect(0, GCCollectionMode.Default);
            #endif
            
            // Only do full collection if memory is critically high
            long memoryAfterGen0 = GC.GetTotalMemory(false);
            if (memoryAfterGen0 > peakMemoryUsage * 2f)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
            
            Debug.Log($"[BalloonMemoryManager] Smart GC completed. Memory: {memoryAfterGen0 / (1024 * 1024)}MB");
        }
        
        /// <summary>
        /// Enhanced memory leak detection with auto-repair capabilities
        /// </summary>
        public void DetectMemoryLeaks()
        {
            lock (memoryLock)
            {
                long currentMemory = GC.GetTotalMemory(false);
                bool leakDetected = false;
                
                // Track peak memory usage
                if (currentMemory > peakMemoryUsage)
                {
                    peakMemoryUsage = currentMemory;
                    Debug.Log($"[BalloonMemoryManager] New peak memory usage: {peakMemoryUsage / (1024 * 1024)}MB");
                }
                
                // Check for index consistency
                int totalIndices = activeBalloonCount[0] + freeIndexCount[0];
                if (totalIndices != maxBalloonCount)
                {
                    Debug.LogError($"[BalloonMemoryManager] Index leak detected! Expected {maxBalloonCount} indices, found {totalIndices}");
                    leakDetected = true;
                    
                    // Auto-repair: Rebuild index arrays
                    RepairIndexArrays();
                }
                
                // Check for allocation tracking consistency
                ValidateAllocationTracking();
                
                // Check for pool leaks
                if (vectorPool != null && vectorPool.HasLeaks)
                {
                    Debug.LogWarning("[BalloonMemoryManager] Vector pool leak detected, cleaning up");
                    vectorPool.ForceCleanup();
                    leakDetected = true;
                }
                
                if (matrixPool != null && matrixPool.HasLeaks)
                {
                    Debug.LogWarning("[BalloonMemoryManager] Matrix pool leak detected, cleaning up");
                    matrixPool.ForceCleanup();
                    leakDetected = true;
                }
                
                if (intPool != null && intPool.HasLeaks)
                {
                    Debug.LogWarning("[BalloonMemoryManager] Int pool leak detected, cleaning up");
                    intPool.ForceCleanup();
                    leakDetected = true;
                }
                
                // Check for excessive memory growth
                if (currentMemory > totalAllocatedMemory * 3f)
                {
                    Debug.LogWarning($"[BalloonMemoryManager] Excessive memory growth detected. Allocated: {totalAllocatedMemory / (1024 * 1024)}MB, Current: {currentMemory / (1024 * 1024)}MB");
                    leakDetected = true;
                    
                    // Auto-repair: Force cleanup
                    PerformEmergencyCleanup();
                }
                
                if (leakDetected)
                {
                    Debug.Log("[BalloonMemoryManager] Memory leak auto-repair completed");
                }
            }
        }
        
        /// <summary>
        /// Repairs corrupted index arrays
        /// </summary>
        private void RepairIndexArrays()
        {
            Debug.Log("[BalloonMemoryManager] Repairing index arrays...");
            
            // Rebuild free indices array
            bool[] usedIndices = new bool[maxBalloonCount];
            
            // Mark active indices as used
            for (int i = 0; i < activeBalloonCount[0]; i++)
            {
                int index = activeBalloonIndices[i];
                if (index >= 0 && index < maxBalloonCount)
                {
                    usedIndices[index] = true;
                }
            }
            
            // Rebuild free indices
            int freeCount = 0;
            for (int i = 0; i < maxBalloonCount; i++)
            {
                if (!usedIndices[i])
                {
                    freeBalloonIndices[freeCount] = i;
                    freeCount++;
                }
            }
            
            freeIndexCount[0] = freeCount;
            
            Debug.Log($"[BalloonMemoryManager] Index arrays repaired. Active: {activeBalloonCount[0]}, Free: {freeCount}");
        }
        
        /// <summary>
        /// Validates allocation tracking consistency
        /// </summary>
        private void ValidateAllocationTracking()
        {
            // Check if tracked allocations match actual allocations
            if (balloonDataArray.IsCreated)
            {
                unsafe
                {
                    try
                    {
                        var ptr = new IntPtr(balloonDataArray.GetUnsafePtr());
                        if (!activeAllocations.ContainsKey(ptr))
                        {
                            Debug.LogWarning("[BalloonMemoryManager] Untracked allocation detected for balloon data array");
                        }
                    }
                    catch (System.InvalidOperationException)
                    {
                        Debug.LogWarning("[BalloonMemoryManager] Could not validate balloon data array - array may be disposed");
                    }
                }
            }
            
            // Remove invalid allocations from tracking
            var keysToRemove = new List<IntPtr>();
            foreach (var kvp in activeAllocations)
            {
                // Check if allocation is still valid (simplified check)
                if (kvp.Value.allocatedTime < Time.realtimeSinceStartup - 3600f) // 1 hour old
                {
                    keysToRemove.Add(kvp.Key);
                }
            }
            
            foreach (var key in keysToRemove)
            {
                activeAllocations.Remove(key);
            }
        }
        
        /// <summary>
        /// Performs emergency cleanup when memory usage is excessive
        /// </summary>
        private void PerformEmergencyCleanup()
        {
            Debug.LogWarning("[BalloonMemoryManager] Performing emergency cleanup...");
            
            // Force immediate garbage collection
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            
            // Clean up all pools aggressively
            vectorPool?.ForceCleanup();
            matrixPool?.ForceCleanup();
            intPool?.ForceCleanup();
            
            // Update memory tracking
            CalculateMemoryUsage();
            
            Debug.Log($"[BalloonMemoryManager] Emergency cleanup completed. Memory usage: {GC.GetTotalMemory(false) / (1024 * 1024)}MB");
        }
        
        /// <summary>
        /// Gets memory usage in megabytes
        /// </summary>
        public float GetMemoryUsageMB()
        {
            return totalAllocatedMemory / (1024.0f * 1024.0f);
        }
        
        /// <summary>
        /// Enhanced disposal with proper cleanup tracking
        /// </summary>
        public void Dispose()
        {
            lock (memoryLock)
            {
                DisposeAllocations();
                
                // Dispose pooling systems
                vectorPool?.Dispose();
                matrixPool?.Dispose();
                intPool?.Dispose();
                
                // Clear tracking
                activeAllocations?.Clear();
                
                Debug.Log("[BalloonMemoryManager] Complete disposal finished");
            }
        }
        
        /// <summary>
        /// Disposes all native array allocations
        /// </summary>
        private void DisposeAllocations()
        {
            DisposeTrackedArray(ref balloonDataArray, "BalloonData");
            DisposeTrackedArray(ref activeBalloonIndices, "ActiveIndices");
            DisposeTrackedArray(ref freeBalloonIndices, "FreeIndices");
            DisposeTrackedArray(ref collisionPairs, "CollisionPairs");
            DisposeTrackedArray(ref deformationData, "DeformationData");
            DisposeTrackedArray(ref activeBalloonCount, "ActiveCount");
            DisposeTrackedArray(ref freeIndexCount, "FreeCount");
        }
        
        /// <summary>
        /// Disposes a tracked native array and updates tracking
        /// </summary>
        private void DisposeTrackedArray<T>(ref NativeArray<T> array, string name) where T : struct
        {
            if (array.IsCreated)
            {
                unsafe
                {
                    var ptr = new IntPtr(array.GetUnsafePtr());
                    activeAllocations?.Remove(ptr);
                }
                
                array.Dispose();
                Debug.Log($"[BalloonMemoryManager] Disposed {name} array");
            }
        }
        
        /// <summary>
        /// Gets pool from the specified type
        /// </summary>
        public NativeArray<T> GetPooledArray<T>(int size) where T : struct
        {
            if (typeof(T) == typeof(float3))
                return (NativeArray<T>)(object)vectorPool.Get(size);
            if (typeof(T) == typeof(float4x4))
                return (NativeArray<T>)(object)matrixPool.Get(size);
            if (typeof(T) == typeof(int))
                return (NativeArray<T>)(object)intPool.Get(size);
            
            // Fallback to direct allocation
            return new NativeArray<T>(size, Allocator.TempJob);
        }
        
        /// <summary>
        /// Returns pooled array to the appropriate pool
        /// </summary>
        public void ReturnPooledArray<T>(NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated) return;
            
            if (typeof(T) == typeof(float3))
                vectorPool.Return((NativeArray<float3>)(object)array);
            else if (typeof(T) == typeof(float4x4))
                matrixPool.Return((NativeArray<float4x4>)(object)array);
            else if (typeof(T) == typeof(int))
                intPool.Return((NativeArray<int>)(object)array);
            else
                array.Dispose(); // Direct disposal for non-pooled types
        }
        
        /// <summary>
        /// Gets detailed memory statistics
        /// </summary>
        public MemoryStatistics GetMemoryStatistics()
        {
            lock (memoryLock)
            {
                return new MemoryStatistics
                {
                    TotalAllocatedMemory = totalAllocatedMemory,
                    PeakMemoryUsage = peakMemoryUsage,
                    ActiveBalloonCount = activeBalloonCount.IsCreated ? activeBalloonCount[0] : 0,
                    MaxBalloonCount = maxBalloonCount,
                    ActiveAllocationCount = activeAllocations.Count,
                    GCCallCount = gcCallCount,
                    MemoryEfficiency = MemoryEfficiency,
                    VectorPoolSize = vectorPool?.ActiveCount ?? 0,
                    MatrixPoolSize = matrixPool?.ActiveCount ?? 0,
                    IntPoolSize = intPool?.ActiveCount ?? 0,
                    ManagedMemoryUsage = GC.GetTotalMemory(false)
                };
            }
        }
    }
    
    /// <summary>
    /// Allocation tracking information for memory leak detection
    /// </summary>
    public struct AllocationInfo
    {
        public string name;
        public long size;
        public int elementCount;
        public float allocatedTime;
    }
    
    /// <summary>
    /// Comprehensive memory statistics
    /// </summary>
    public struct MemoryStatistics
    {
        public long TotalAllocatedMemory;
        public long PeakMemoryUsage;
        public int ActiveBalloonCount;
        public int MaxBalloonCount;
        public int ActiveAllocationCount;
        public int GCCallCount;
        public float MemoryEfficiency;
        public int VectorPoolSize;
        public int MatrixPoolSize;
        public int IntPoolSize;
        public long ManagedMemoryUsage;
        
        public override string ToString()
        {
            return $"Memory Stats: {TotalAllocatedMemory / (1024 * 1024)}MB allocated, " +
                   $"{PeakMemoryUsage / (1024 * 1024)}MB peak, " +
                   $"{ActiveBalloonCount}/{MaxBalloonCount} balloons, " +
                   $"{MemoryEfficiency:P1} efficiency, " +
                   $"{GCCallCount} GC calls";
        }
    }
    
    /// <summary>
    /// Generic NativeArray pooling system for efficient memory reuse with thread safety
    /// </summary>
    public class NativeArrayPool<T> : IDisposable where T : struct
    {
        private readonly Queue<NativeArray<T>> availableArrays;
        private readonly HashSet<NativeArray<T>> activeArrays;
        private readonly int maxPoolSize;
        private readonly Allocator allocator;
        private readonly object poolLock = new object();
        
        public int ActiveCount 
        { 
            get 
            { 
                lock (poolLock) 
                { 
                    return activeArrays.Count; 
                } 
            } 
        }
        
        public int AvailableCount 
        { 
            get 
            { 
                lock (poolLock) 
                { 
                    return availableArrays.Count; 
                } 
            } 
        }
        
        public bool HasLeaks 
        { 
            get 
            { 
                lock (poolLock) 
                { 
                    return activeArrays.Count > maxPoolSize * 2; 
                } 
            } 
        }
        
        public long EstimatedSize 
        { 
            get 
            { 
                lock (poolLock) 
                { 
                    // Calculate actual size based on tracked arrays instead of hardcoded estimate
                    long totalSize = 0;
                    foreach (var array in activeArrays)
                    {
                        if (array.IsCreated)
                            totalSize += array.Length * UnsafeUtility.SizeOf<T>();
                    }
                    foreach (var array in availableArrays)
                    {
                        if (array.IsCreated)
                            totalSize += array.Length * UnsafeUtility.SizeOf<T>();
                    }
                    return totalSize;
                } 
            } 
        }
        
        public NativeArrayPool(int maxSize = 100, Allocator allocator = Allocator.Persistent)
        {
            this.maxPoolSize = maxSize;
            this.allocator = allocator;
            availableArrays = new Queue<NativeArray<T>>();
            activeArrays = new HashSet<NativeArray<T>>();
        }
        
        /// <summary>
        /// Gets a pooled array of the specified size (thread-safe)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public NativeArray<T> Get(int size)
        {
            lock (poolLock)
            {
                // Try to find a suitable array from the pool
                while (availableArrays.Count > 0)
                {
                    var array = availableArrays.Dequeue();
                    if (array.IsCreated && array.Length >= size)
                    {
                        activeArrays.Add(array);
                        return array.GetSubArray(0, size);
                    }
                    else if (array.IsCreated)
                    {
                        array.Dispose();
                    }
                }
                
                // Create new array if pool is empty
                var newArray = new NativeArray<T>(size, allocator);
                activeArrays.Add(newArray);
                return newArray;
            }
        }
        
        /// <summary>
        /// Returns an array to the pool (thread-safe)
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Return(NativeArray<T> array)
        {
            if (!array.IsCreated) return;
            
            lock (poolLock)
            {
                if (activeArrays.Remove(array))
                {
                    if (availableArrays.Count < maxPoolSize)
                    {
                        availableArrays.Enqueue(array);
                    }
                    else
                    {
                        array.Dispose();
                    }
                }
            }
        }
        
        /// <summary>
        /// Cleans up unused arrays in the pool (thread-safe)
        /// </summary>
        public void Cleanup()
        {
            lock (poolLock)
            {
                // Remove half of the available arrays to free memory
                int toRemove = availableArrays.Count / POOL_CLEANUP_RATIO;
                for (int i = 0; i < toRemove; i++)
                {
                    if (availableArrays.Count > 0)
                    {
                        var array = availableArrays.Dequeue();
                        if (array.IsCreated)
                            array.Dispose();
                    }
                }
            }
        }
        
        /// <summary>
        /// Forces cleanup of all arrays (for leak repair, thread-safe)
        /// </summary>
        public void ForceCleanup()
        {
            lock (poolLock)
            {
                // Dispose all available arrays
                while (availableArrays.Count > 0)
                {
                    var array = availableArrays.Dequeue();
                    if (array.IsCreated)
                        array.Dispose();
                }
                
                // Log active arrays that might be leaked
                if (activeArrays.Count > 0)
                {
                    Debug.LogWarning($"[NativeArrayPool<{typeof(T).Name}>] {activeArrays.Count} arrays still active during force cleanup");
                }
            }
        }
        
        /// <summary>
        /// Disposes the entire pool (thread-safe)
        /// </summary>
        public void Dispose()
        {
            lock (poolLock)
            {
                // Dispose all available arrays
                while (availableArrays.Count > 0)
                {
                    var array = availableArrays.Dequeue();
                    if (array.IsCreated)
                        array.Dispose();
                }
                
                // Dispose all active arrays
                foreach (var array in activeArrays)
                {
                    if (array.IsCreated)
                        array.Dispose();
                }
                
                activeArrays.Clear();
            }
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