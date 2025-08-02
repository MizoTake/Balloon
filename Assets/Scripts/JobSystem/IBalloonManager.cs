using Unity.Collections;
using UnityEngine;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// Unified interface for balloon simulation managers
    /// Provides consistent API for all manager implementations
    /// </summary>
    public interface IBalloonManager
    {
        /// <summary>
        /// Current number of active balloons in the simulation
        /// </summary>
        int BalloonCount { get; }
        
        /// <summary>
        /// Maximum number of balloons supported by this manager
        /// </summary>
        int MaxBalloonCount { get; }
        
        /// <summary>
        /// Current simulation state
        /// </summary>
        SimulationState State { get; }
        
        /// <summary>
        /// Change the number of active balloons
        /// </summary>
        /// <param name="newCount">New balloon count (must be <= MaxBalloonCount)</param>
        /// <returns>True if successful, false if count is invalid</returns>
        bool TryChangeBalloonCount(int newCount);
        
        /// <summary>
        /// Reset the simulation to initial state
        /// </summary>
        void ResetSimulation();
        
        /// <summary>
        /// Get performance metrics for the current frame
        /// </summary>
        /// <returns>Current performance data</returns>
        PerformanceMetrics GetPerformanceMetrics();
        
        /// <summary>
        /// Log detailed performance report to console
        /// </summary>
        void LogPerformanceReport();
        
        /// <summary>
        /// Try to get balloon data array if available
        /// Thread-safe method that ensures jobs are complete
        /// </summary>
        /// <typeparam name="T">Type of balloon data</typeparam>
        /// <param name="balloonData">Output balloon data array</param>
        /// <returns>True if data is available and safe to read</returns>
        bool TryGetBalloonData<T>(out NativeArray<T> balloonData) where T : struct;
        
        /// <summary>
        /// Check if the manager supports a specific feature
        /// </summary>
        /// <param name="feature">Feature to check</param>
        /// <returns>True if feature is supported</returns>
        bool SupportsFeature(SimulationFeature feature);
        
        /// <summary>
        /// Enable or disable a specific simulation feature
        /// </summary>
        /// <param name="feature">Feature to toggle</param>
        /// <param name="enabled">Enable or disable</param>
        /// <returns>True if successfully toggled</returns>
        bool TrySetFeatureEnabled(SimulationFeature feature, bool enabled);
        
        /// <summary>
        /// Get memory usage information
        /// </summary>
        /// <returns>Memory usage in MB</returns>
        float GetMemoryUsageMB();
        
        /// <summary>
        /// Register a performance monitor to receive updates
        /// </summary>
        /// <param name="monitor">Monitor to register</param>
        void RegisterPerformanceMonitor(IPerformanceMonitor monitor);
        
        /// <summary>
        /// Unregister a performance monitor
        /// </summary>
        /// <param name="monitor">Monitor to unregister</param>
        void UnregisterPerformanceMonitor(IPerformanceMonitor monitor);
    }
    
    /// <summary>
    /// Simulation state enumeration
    /// </summary>
    public enum SimulationState
    {
        Uninitialized,
        Initializing,
        Running,
        Paused,
        Resetting,
        Error
    }
    
    /// <summary>
    /// Available simulation features
    /// </summary>
    public enum SimulationFeature
    {
        FluidDynamics,
        SwarmPhysics,
        GPUPhysics,
        Deformation,
        LODSystem,
        PerformanceProfiling,
        AutoOptimization
    }
    
    /// <summary>
    /// Performance metrics structure
    /// </summary>
    public struct PerformanceMetrics
    {
        public float fps;
        public float physicsTimeMs;
        public float renderTimeMs;
        public float memoryUsageMB;
        public int renderedInstances;
        public int activeCollisions;
        public float jobSchedulingTimeMs;
        
        public static PerformanceMetrics Default => new PerformanceMetrics
        {
            fps = 60f,
            physicsTimeMs = 0f,
            renderTimeMs = 0f,
            memoryUsageMB = 0f,
            renderedInstances = 0,
            activeCollisions = 0,
            jobSchedulingTimeMs = 0f
        };
    }
    
    /// <summary>
    /// Interface for performance monitoring systems
    /// </summary>
    public interface IPerformanceMonitor
    {
        void OnPerformanceUpdate(PerformanceMetrics metrics);
        void OnSimulationStateChanged(SimulationState oldState, SimulationState newState);
        void OnFeatureToggled(SimulationFeature feature, bool enabled);
    }
}