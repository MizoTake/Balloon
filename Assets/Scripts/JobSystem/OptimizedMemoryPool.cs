using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// High-performance memory pool for native arrays and job system objects
    /// Reduces garbage collection and allocation overhead
    /// </summary>
    public class OptimizedMemoryPool : IDisposable
    {
        private readonly Dictionary<Type, Queue<object>> pooledObjects = new Dictionary<Type, Queue<object>>();
        private readonly Dictionary<(Type, int), Queue<Array>> pooledArrays = new Dictionary<(Type, int), Queue<Array>>();
        private readonly Dictionary<(Type, int, Allocator), Queue<INativeDisposable>> pooledNativeArrays = new Dictionary<(Type, int, Allocator), Queue<INativeDisposable>>();
        
        private readonly object lockObject = new object();
        private bool isDisposed = false;
        
        // Statistics
        private int totalRents = 0;
        private int totalReturns = 0;
        private int totalAllocations = 0;
        private long totalMemorySaved = 0;
        
        private static OptimizedMemoryPool instance;
        public static OptimizedMemoryPool Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new OptimizedMemoryPool();
                }
                return instance;
            }
        }
        
        /// <summary>
        /// Rent a NativeArray from the pool or create new if none available
        /// </summary>
        public NativeArray<T> RentNativeArray<T>(int length, Allocator allocator) where T : struct
        {
            if (isDisposed)
                throw new ObjectDisposedException(nameof(OptimizedMemoryPool));
                
            lock (lockObject)
            {
                var key = (typeof(T), length, allocator);
                
                if (pooledNativeArrays.TryGetValue(key, out var queue) && queue.Count > 0)
                {
                    var pooledArray = (NativeArray<T>)queue.Dequeue();
                    totalRents++;
                    totalMemorySaved += UnsafeUtility.SizeOf<T>() * length;
                    
                    // Clear the array for safety
                    UnsafeUtility.MemClear(pooledArray.GetUnsafePtr(), UnsafeUtility.SizeOf<T>() * length);
                    
                    return pooledArray;
                }
                
                totalAllocations++;
                return new NativeArray<T>(length, allocator);
            }
        }
        
        /// <summary>
        /// Return a NativeArray to the pool for reuse
        /// </summary>
        public void ReturnNativeArray<T>(NativeArray<T> array) where T : struct
        {
            if (isDisposed || !array.IsCreated)
                return;
                
            lock (lockObject)
            {
                var key = (typeof(T), array.Length, array.GetAllocator());
                
                if (!pooledNativeArrays.TryGetValue(key, out var queue))
                {
                    queue = new Queue<INativeDisposable>();
                    pooledNativeArrays[key] = queue;
                }
                
                // Only pool persistent allocations to avoid job system issues
                if (array.GetAllocator() == Allocator.Persistent && queue.Count < GetMaxPoolSize<T>())
                {
                    queue.Enqueue(array);
                    totalReturns++;
                }
                else
                {
                    array.Dispose();
                }
            }
        }
        
        /// <summary>
        /// Rent a managed array from the pool
        /// </summary>
        public T[] RentArray<T>(int length)
        {
            if (isDisposed)
                throw new ObjectDisposedException(nameof(OptimizedMemoryPool));
                
            lock (lockObject)
            {
                var key = (typeof(T), length);
                
                if (pooledArrays.TryGetValue(key, out var queue) && queue.Count > 0)
                {
                    var pooledArray = (T[])queue.Dequeue();
                    totalRents++;
                    
                    // Clear the array
                    Array.Clear(pooledArray, 0, pooledArray.Length);
                    return pooledArray;
                }
                
                totalAllocations++;
                return new T[length];
            }
        }
        
        /// <summary>
        /// Return a managed array to the pool
        /// </summary>
        public void ReturnArray<T>(T[] array)
        {
            if (isDisposed || array == null)
                return;
                
            lock (lockObject)
            {
                var key = (typeof(T), array.Length);
                
                if (!pooledArrays.TryGetValue(key, out var queue))
                {
                    queue = new Queue<Array>();
                    pooledArrays[key] = queue;
                }
                
                if (queue.Count < GetMaxPoolSize<T>())
                {
                    queue.Enqueue(array);
                    totalReturns++;
                }
            }
        }
        
        /// <summary>
        /// Rent an object from the pool
        /// </summary>
        public T RentObject<T>() where T : class, new()
        {
            if (isDisposed)
                throw new ObjectDisposedException(nameof(OptimizedMemoryPool));
                
            lock (lockObject)
            {
                var type = typeof(T);
                
                if (pooledObjects.TryGetValue(type, out var queue) && queue.Count > 0)
                {
                    var pooledObj = (T)queue.Dequeue();
                    totalRents++;
                    
                    // Reset object if it implements IResettable
                    if (pooledObj is IResettable resettable)
                    {
                        resettable.Reset();
                    }
                    
                    return pooledObj;
                }
                
                totalAllocations++;
                return new T();
            }
        }
        
        /// <summary>
        /// Return an object to the pool
        /// </summary>
        public void ReturnObject<T>(T obj) where T : class
        {
            if (isDisposed || obj == null)
                return;
                
            lock (lockObject)
            {
                var type = typeof(T);
                
                if (!pooledObjects.TryGetValue(type, out var queue))
                {
                    queue = new Queue<object>();
                    pooledObjects[type] = queue;
                }
                
                if (queue.Count < GetMaxPoolSize<T>())
                {
                    queue.Enqueue(obj);
                    totalReturns++;
                }
            }
        }
        
        /// <summary>
        /// Pre-warm the pool with objects of a specific type
        /// </summary>
        public void PreWarm<T>(int count) where T : class, new()
        {
            if (isDisposed)
                return;
                
            lock (lockObject)
            {
                var type = typeof(T);
                
                if (!pooledObjects.TryGetValue(type, out var queue))
                {
                    queue = new Queue<object>();
                    pooledObjects[type] = queue;
                }
                
                for (int i = 0; i < count; i++)
                {
                    if (queue.Count < GetMaxPoolSize<T>())
                    {
                        queue.Enqueue(new T());
                    }
                }
            }
        }
        
        /// <summary>
        /// Pre-warm the pool with native arrays
        /// </summary>
        public void PreWarmNativeArrays<T>(int arrayLength, int count, Allocator allocator) where T : struct
        {
            if (isDisposed)
                return;
                
            lock (lockObject)
            {
                var key = (typeof(T), arrayLength, allocator);
                
                if (!pooledNativeArrays.TryGetValue(key, out var queue))
                {
                    queue = new Queue<INativeDisposable>();
                    pooledNativeArrays[key] = queue;
                }
                
                for (int i = 0; i < count; i++)
                {
                    if (queue.Count < GetMaxPoolSize<T>())
                    {
                        queue.Enqueue(new NativeArray<T>(arrayLength, allocator));
                    }
                }
            }
        }
        
        /// <summary>
        /// Clear all pooled objects and dispose native collections
        /// </summary>
        public void Clear()
        {
            lock (lockObject)
            {
                // Dispose all native arrays
                foreach (var kvp in pooledNativeArrays)
                {
                    while (kvp.Value.Count > 0)
                    {
                        var nativeArray = kvp.Value.Dequeue();
                        nativeArray.Dispose();
                    }
                }
                pooledNativeArrays.Clear();
                
                // Clear managed objects
                pooledObjects.Clear();
                pooledArrays.Clear();
                
                // Reset statistics
                totalRents = 0;
                totalReturns = 0;
                totalAllocations = 0;
                totalMemorySaved = 0;
            }
        }
        
        /// <summary>
        /// Get memory usage statistics
        /// </summary>
        public PoolStatistics GetStatistics()
        {
            lock (lockObject)
            {
                return new PoolStatistics
                {
                    TotalRents = totalRents,
                    TotalReturns = totalReturns,
                    TotalAllocations = totalAllocations,
                    MemorySavedBytes = totalMemorySaved,
                    PooledObjectTypes = pooledObjects.Count,
                    PooledArrayTypes = pooledArrays.Count,
                    PooledNativeArrayTypes = pooledNativeArrays.Count,
                    PoolEfficiency = totalAllocations > 0 ? (float)totalRents / (totalRents + totalAllocations) : 0f
                };
            }
        }
        
        private int GetMaxPoolSize<T>()
        {
            // Adjust pool size based on object size and memory constraints
            if (typeof(T).IsValueType)
            {
                int size = UnsafeUtility.SizeOf<T>();
                if (size < 64) return 100;      // Small structs
                if (size < 1024) return 50;     // Medium structs
                return 10;                      // Large structs
            }
            
            return 20; // Default for reference types
        }
        
        public void Dispose()
        {
            if (isDisposed)
                return;
                
            Clear();
            isDisposed = true;
            
            if (instance == this)
            {
                instance = null;
            }
        }
    }
    
    /// <summary>
    /// Interface for objects that can be reset when returned to pool
    /// </summary>
    public interface IResettable
    {
        void Reset();
    }
    
    /// <summary>
    /// Pool usage statistics
    /// </summary>
    public struct PoolStatistics
    {
        public int TotalRents;
        public int TotalReturns;
        public int TotalAllocations;
        public long MemorySavedBytes;
        public int PooledObjectTypes;
        public int PooledArrayTypes;
        public int PooledNativeArrayTypes;
        public float PoolEfficiency;
        
        public float MemorySavedMB => MemorySavedBytes / (1024f * 1024f);
        
        public override string ToString()
        {
            return $"Pool Stats: Rents={TotalRents}, Returns={TotalReturns}, Allocations={TotalAllocations}, " +
                   $"Efficiency={PoolEfficiency:P1}, Memory Saved={MemorySavedMB:F2}MB";
        }
    }
    
    /// <summary>
    /// Extension methods for easier pool usage
    /// </summary>
    public static class MemoryPoolExtensions
    {
        public static NativeArray<T> RentNativeArray<T>(int length, Allocator allocator = Allocator.TempJob) where T : struct
        {
            return OptimizedMemoryPool.Instance.RentNativeArray<T>(length, allocator);
        }
        
        public static void ReturnToPool<T>(this NativeArray<T> array) where T : struct
        {
            OptimizedMemoryPool.Instance.ReturnNativeArray(array);
        }
        
        public static T[] RentArray<T>(int length)
        {
            return OptimizedMemoryPool.Instance.RentArray<T>(length);
        }
        
        public static void ReturnToPool<T>(this T[] array)
        {
            OptimizedMemoryPool.Instance.ReturnArray(array);
        }
        
        public static T RentObject<T>() where T : class, new()
        {
            return OptimizedMemoryPool.Instance.RentObject<T>();
        }
        
        public static void ReturnToPool<T>(this T obj) where T : class
        {
            OptimizedMemoryPool.Instance.ReturnObject(obj);
        }
    }
}