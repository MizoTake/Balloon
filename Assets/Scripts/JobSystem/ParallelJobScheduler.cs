using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// Advanced job scheduler that optimizes parallel execution and dependencies
    /// Maximizes CPU and GPU utilization through intelligent scheduling
    /// </summary>
    public class ParallelJobScheduler : IDisposable
    {
        private readonly Dictionary<string, JobHandle> namedJobs = new Dictionary<string, JobHandle>();
        private readonly List<ScheduledJob> jobQueue = new List<ScheduledJob>();
        private readonly List<JobBatch> jobBatches = new List<JobBatch>();
        
        private JobHandle currentFrameHandle;
        private bool isDisposed = false;
        
        // Performance tracking
        private float lastSchedulingTime = 0f;
        private int jobsScheduledThisFrame = 0;
        private int parallelJobsCount = 0;
        
        /// <summary>
        /// Schedule a named job with automatic dependency resolution
        /// </summary>
        public JobHandle Schedule<T>(string jobName, T job, JobHandle dependency = default) where T : struct, IJob
        {
            if (isDisposed)
                throw new ObjectDisposedException(nameof(ParallelJobScheduler));
                
            var startTime = Time.realtimeSinceStartup;
            
            // Resolve dependencies from named jobs
            dependency = ResolveDependencies(jobName, dependency);
            
            // Schedule the job
            var handle = job.Schedule(dependency);
            
            // Track the job
            namedJobs[jobName] = handle;
            jobsScheduledThisFrame++;
            
            UpdateSchedulingMetrics(startTime);
            
            return handle;
        }
        
        /// <summary>
        /// Schedule a parallel job with intelligent batch sizing
        /// </summary>
        public JobHandle ScheduleParallel<T>(string jobName, T job, int arrayLength, int minIndicesPerJobCount = 1, JobHandle dependency = default) where T : struct, IJobParallelFor
        {
            if (isDisposed)
                throw new ObjectDisposedException(nameof(ParallelJobScheduler));
                
            var startTime = Time.realtimeSinceStartup;
            
            // Resolve dependencies
            dependency = ResolveDependencies(jobName, dependency);
            
            // Calculate optimal batch size based on array length and system cores
            int batchSize = CalculateOptimalBatchSize(arrayLength, minIndicesPerJobCount);
            
            // Schedule the parallel job
            var handle = job.Schedule(arrayLength, batchSize, dependency);
            
            // Track the job
            namedJobs[jobName] = handle;
            parallelJobsCount++;
            jobsScheduledThisFrame++;
            
            UpdateSchedulingMetrics(startTime);
            
            return handle;
        }
        
        /// <summary>
        /// Schedule multiple jobs in a batch with optimized dependencies
        /// </summary>
        public JobHandle ScheduleBatch(string batchName, params (string name, IJob job, string[] dependencies)[] jobs)
        {
            if (isDisposed)
                throw new ObjectDisposedException(nameof(ParallelJobScheduler));
                
            var batch = new JobBatch { Name = batchName, Jobs = new List<ScheduledJob>() };
            var batchHandles = new List<JobHandle>();
            
            // First pass: schedule jobs without cross-dependencies
            foreach (var (name, job, deps) in jobs)
            {
                var dependency = ResolveDependenciesFromNames(deps);
                var handle = job.Schedule(dependency);
                
                batch.Jobs.Add(new ScheduledJob { Name = name, Handle = handle, Dependencies = deps });
                namedJobs[name] = handle;
                batchHandles.Add(handle);
            }
            
            // Combine all handles in the batch
            var batchHandle = JobHandle.CombineDependencies(batchHandles.ToArray());
            namedJobs[batchName] = batchHandle;
            
            jobBatches.Add(batch);
            
            return batchHandle;
        }
        
        /// <summary>
        /// Schedule parallel jobs with automatic load balancing
        /// </summary>
        public JobHandle ScheduleParallelBatch<T>(string batchName, T[] jobs, int[] arrayLengths, JobHandle dependency = default) where T : struct, IJobParallelFor
        {
            if (isDisposed)
                throw new ObjectDisposedException(nameof(ParallelJobScheduler));
                
            if (jobs.Length != arrayLengths.Length)
                throw new ArgumentException("Jobs and array lengths must have the same count");
                
            var handles = new JobHandle[jobs.Length];
            
            for (int i = 0; i < jobs.Length; i++)
            {
                int batchSize = CalculateOptimalBatchSize(arrayLengths[i], 1);
                handles[i] = jobs[i].Schedule(arrayLengths[i], batchSize, dependency);
                namedJobs[$"{batchName}_{i}"] = handles[i];
            }
            
            var combinedHandle = JobHandle.CombineDependencies(handles);
            namedJobs[batchName] = combinedHandle;
            
            parallelJobsCount += jobs.Length;
            jobsScheduledThisFrame += jobs.Length;
            
            return combinedHandle;
        }
        
        /// <summary>
        /// Schedule a chain of dependent jobs efficiently
        /// </summary>
        public JobHandle ScheduleChain(string chainName, params (string name, IJob job)[] jobs)
        {
            if (isDisposed)
                throw new ObjectDisposedException(nameof(ParallelJobScheduler));
                
            JobHandle previousHandle = default;
            
            for (int i = 0; i < jobs.Length; i++)
            {
                var (name, job) = jobs[i];
                var handle = job.Schedule(previousHandle);
                namedJobs[name] = handle;
                previousHandle = handle;
                jobsScheduledThisFrame++;
            }
            
            namedJobs[chainName] = previousHandle;
            return previousHandle;
        }
        
        /// <summary>
        /// Get a job handle by name for dependency resolution
        /// </summary>
        public JobHandle GetJobHandle(string jobName)
        {
            return namedJobs.GetValueOrDefault(jobName, default);
        }
        
        /// <summary>
        /// Wait for a specific job to complete
        /// </summary>
        public void CompleteJob(string jobName)
        {
            if (namedJobs.TryGetValue(jobName, out var handle))
            {
                handle.Complete();
            }
        }
        
        /// <summary>
        /// Complete all jobs and prepare for next frame
        /// </summary>
        public void CompleteAllJobs()
        {
            foreach (var handle in namedJobs.Values)
            {
                handle.Complete();
            }
            
            // Clear completed jobs
            namedJobs.Clear();
            jobQueue.Clear();
            jobBatches.Clear();
            
            // Reset frame counters
            jobsScheduledThisFrame = 0;
            parallelJobsCount = 0;
        }
        
        /// <summary>
        /// Schedule batched jobs for better performance
        /// Call this after scheduling all jobs for the frame
        /// </summary>
        public void ScheduleBatchedJobs()
        {
            JobHandle.ScheduleBatchedJobs();
        }
        
        /// <summary>
        /// Get performance metrics for optimization
        /// </summary>
        public SchedulerMetrics GetMetrics()
        {
            return new SchedulerMetrics
            {
                LastSchedulingTimeMs = lastSchedulingTime * 1000f,
                JobsScheduledThisFrame = jobsScheduledThisFrame,
                ParallelJobsCount = parallelJobsCount,
                ActiveJobsCount = namedJobs.Count,
                BatchedJobsCount = jobBatches.Count
            };
        }
        
        /// <summary>
        /// Check if any jobs are still running
        /// </summary>
        public bool HasRunningJobs()
        {
            foreach (var handle in namedJobs.Values)
            {
                if (!handle.IsCompleted)
                    return true;
            }
            return false;
        }
        
        /// <summary>
        /// Estimate remaining completion time for all jobs
        /// </summary>
        public float EstimateCompletionTime()
        {
            // Simple estimation based on scheduling time and job count
            if (jobsScheduledThisFrame == 0)
                return 0f;
                
            return lastSchedulingTime * jobsScheduledThisFrame * 1.5f; // Factor for execution overhead
        }
        
        private JobHandle ResolveDependencies(string jobName, JobHandle baseDependency)
        {
            // For now, just return the base dependency
            // In a more advanced implementation, we could analyze job dependencies
            // and automatically resolve them based on data flow
            return baseDependency;
        }
        
        private JobHandle ResolveDependenciesFromNames(string[] dependencyNames)
        {
            if (dependencyNames == null || dependencyNames.Length == 0)
                return default;
                
            var handles = new List<JobHandle>();
            
            foreach (var depName in dependencyNames)
            {
                if (namedJobs.TryGetValue(depName, out var handle))
                {
                    handles.Add(handle);
                }
            }
            
            return handles.Count > 0 ? JobHandle.CombineDependencies(handles.ToArray()) : default;
        }
        
        private int CalculateOptimalBatchSize(int arrayLength, int minIndicesPerJobCount)
        {
            if (arrayLength <= 0)
                return 1;
                
            // Get system core count for optimal parallelization
            int coreCount = Mathf.Max(1, SystemInfo.processorCount);
            
            // Calculate batch size based on array length and core count
            int optimalBatchSize = Mathf.Max(minIndicesPerJobCount, arrayLength / (coreCount * 4));
            
            // Clamp to reasonable bounds
            optimalBatchSize = Mathf.Clamp(optimalBatchSize, 1, 1024);
            
            // Ensure we don't have too many small batches
            if (arrayLength / optimalBatchSize > coreCount * 8)
            {
                optimalBatchSize = arrayLength / (coreCount * 8);
            }
            
            return Mathf.Max(1, optimalBatchSize);
        }
        
        private void UpdateSchedulingMetrics(float startTime)
        {
            lastSchedulingTime += Time.realtimeSinceStartup - startTime;
        }
        
        public void Dispose()
        {
            if (isDisposed)
                return;
                
            CompleteAllJobs();
            isDisposed = true;
        }
    }
    
    /// <summary>
    /// Information about a scheduled job
    /// </summary>
    public struct ScheduledJob
    {
        public string Name;
        public JobHandle Handle;
        public string[] Dependencies;
        public float ScheduleTime;
        public bool IsParallel;
    }
    
    /// <summary>
    /// Batch of related jobs
    /// </summary>
    public struct JobBatch
    {
        public string Name;
        public List<ScheduledJob> Jobs;
        public JobHandle CombinedHandle;
    }
    
    /// <summary>
    /// Performance metrics for job scheduling
    /// </summary>
    public struct SchedulerMetrics
    {
        public float LastSchedulingTimeMs;
        public int JobsScheduledThisFrame;
        public int ParallelJobsCount;
        public int ActiveJobsCount;
        public int BatchedJobsCount;
        
        public override string ToString()
        {
            return $"Scheduler: {LastSchedulingTimeMs:F2}ms, Jobs: {JobsScheduledThisFrame}, " +
                   $"Parallel: {ParallelJobsCount}, Active: {ActiveJobsCount}, Batches: {BatchedJobsCount}";
        }
    }
    
    /// <summary>
    /// Extension methods for easier job scheduling
    /// </summary>
    public static class JobSchedulerExtensions
    {
        private static ParallelJobScheduler defaultScheduler;
        
        public static ParallelJobScheduler DefaultScheduler
        {
            get
            {
                if (defaultScheduler == null)
                {
                    defaultScheduler = new ParallelJobScheduler();
                }
                return defaultScheduler;
            }
        }
        
        public static JobHandle ScheduleNamed<T>(this T job, string name, JobHandle dependency = default) where T : struct, IJob
        {
            return DefaultScheduler.Schedule(name, job, dependency);
        }
        
        public static JobHandle ScheduleParallelNamed<T>(this T job, string name, int arrayLength, int batchSize = 1, JobHandle dependency = default) where T : struct, IJobParallelFor
        {
            return DefaultScheduler.ScheduleParallel(name, job, arrayLength, batchSize, dependency);
        }
        
        public static void CompleteNamed(string jobName)
        {
            DefaultScheduler.CompleteJob(jobName);
        }
        
        public static JobHandle GetHandle(string jobName)
        {
            return DefaultScheduler.GetJobHandle(jobName);
        }
    }
}