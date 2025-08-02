using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// Central coordinator for all balloon simulation systems
    /// Manages lifecycle, communication, and error handling between subsystems
    /// </summary>
    public class SystemCoordinator : MonoBehaviour, IPerformanceMonitor
    {
        [Header("System Configuration")]
        [SerializeField] private bool autoDetectSystems = true;
        [SerializeField] private bool enableErrorRecovery = true;
        [SerializeField] private float healthCheckInterval = 1f;
        [SerializeField] private int maxErrorRetries = 3;
        
        // Registered systems
        private readonly List<IBalloonManager> managers = new List<IBalloonManager>();
        private readonly List<IPerformanceMonitor> performanceMonitors = new List<IPerformanceMonitor>();
        private readonly Dictionary<Type, ISubSystem> subSystems = new Dictionary<Type, ISubSystem>();
        
        // Error tracking
        private readonly Dictionary<Type, int> errorCounts = new Dictionary<Type, int>();
        private readonly Dictionary<Type, float> lastErrorTimes = new Dictionary<Type, float>();
        
        // Performance aggregation
        private PerformanceMetrics aggregatedMetrics;
        private float lastHealthCheck;
        private bool isInitialized;
        
        // Events
        public event Action<PerformanceMetrics> OnPerformanceUpdate;
        public event Action<SimulationState> OnSystemStateChanged;
        public event Action<Type, Exception> OnSystemError;
        
        // Singleton access
        private static SystemCoordinator instance;
        public static SystemCoordinator Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindObjectOfType<SystemCoordinator>();
                    if (instance == null)
                    {
                        var go = new GameObject("SystemCoordinator");
                        instance = go.AddComponent<SystemCoordinator>();
                        DontDestroyOnLoad(go);
                    }
                }
                return instance;
            }
        }
        
        void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        
        void Start()
        {
            Initialize();
        }
        
        void Initialize()
        {
            try
            {
                Debug.Log("[SystemCoordinator] Initializing system coordination...");
                
                if (autoDetectSystems)
                {
                    AutoDetectSystems();
                }
                
                RegisterSelfAsMonitor();
                isInitialized = true;
                
                Debug.Log($"[SystemCoordinator] Initialized with {managers.Count} managers and {subSystems.Count} subsystems");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SystemCoordinator] Failed to initialize: {ex.Message}");
                HandleSystemError(typeof(SystemCoordinator), ex);
            }
        }
        
        void AutoDetectSystems()
        {
            // Find all balloon managers
            var balloonManagers = FindObjectsOfType<MonoBehaviour>();
            foreach (var component in balloonManagers)
            {
                if (component is IBalloonManager manager)
                {
                    RegisterManager(manager);
                }
                
                if (component is ISubSystem subSystem)
                {
                    RegisterSubSystem(subSystem);
                }
                
                if (component is IPerformanceMonitor monitor && monitor != this)
                {
                    RegisterPerformanceMonitor(monitor);
                }
            }
        }
        
        void RegisterSelfAsMonitor()
        {
            foreach (var manager in managers)
            {
                try
                {
                    manager.RegisterPerformanceMonitor(this);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SystemCoordinator] Failed to register with manager {manager.GetType().Name}: {ex.Message}");
                }
            }
        }
        
        /// <summary>
        /// Register a balloon manager with the coordinator
        /// </summary>
        public void RegisterManager(IBalloonManager manager)
        {
            if (manager == null) return;
            
            if (!managers.Contains(manager))
            {
                managers.Add(manager);
                manager.RegisterPerformanceMonitor(this);
                Debug.Log($"[SystemCoordinator] Registered manager: {manager.GetType().Name}");
            }
        }
        
        /// <summary>
        /// Unregister a balloon manager
        /// </summary>
        public void UnregisterManager(IBalloonManager manager)
        {
            if (manager == null) return;
            
            if (managers.Remove(manager))
            {
                manager.UnregisterPerformanceMonitor(this);
                Debug.Log($"[SystemCoordinator] Unregistered manager: {manager.GetType().Name}");
            }
        }
        
        /// <summary>
        /// Register a subsystem
        /// </summary>
        public void RegisterSubSystem<T>(T subSystem) where T : ISubSystem
        {
            if (subSystem == null) return;
            
            var type = typeof(T);
            if (!subSystems.ContainsKey(type))
            {
                subSystems[type] = subSystem;
                Debug.Log($"[SystemCoordinator] Registered subsystem: {type.Name}");
            }
        }
        
        /// <summary>
        /// Get a registered subsystem
        /// </summary>
        public T GetSubSystem<T>() where T : class, ISubSystem
        {
            subSystems.TryGetValue(typeof(T), out var subSystem);
            return subSystem as T;
        }
        
        /// <summary>
        /// Register a performance monitor
        /// </summary>
        public void RegisterPerformanceMonitor(IPerformanceMonitor monitor)
        {
            if (monitor == null || performanceMonitors.Contains(monitor)) return;
            
            performanceMonitors.Add(monitor);
            Debug.Log($"[SystemCoordinator] Registered performance monitor: {monitor.GetType().Name}");
        }
        
        /// <summary>
        /// Unregister a performance monitor
        /// </summary>
        public void UnregisterPerformanceMonitor(IPerformanceMonitor monitor)
        {
            if (monitor == null) return;
            
            if (performanceMonitors.Remove(monitor))
            {
                Debug.Log($"[SystemCoordinator] Unregistered performance monitor: {monitor.GetType().Name}");
            }
        }
        
        void Update()
        {
            if (!isInitialized) return;
            
            try
            {
                // Periodic health check
                if (Time.time - lastHealthCheck >= healthCheckInterval)
                {
                    PerformHealthCheck();
                    lastHealthCheck = Time.time;
                }
                
                // Aggregate performance metrics
                AggregatePerformanceMetrics();
            }
            catch (Exception ex)
            {
                HandleSystemError(typeof(SystemCoordinator), ex);
            }
        }
        
        void PerformHealthCheck()
        {
            foreach (var manager in managers)
            {
                try
                {
                    // Check if manager is responsive
                    var metrics = manager.GetPerformanceMetrics();
                    
                    // Check for performance issues
                    if (metrics.fps < 30f && enableErrorRecovery)
                    {
                        Debug.LogWarning($"[SystemCoordinator] Low FPS detected in {manager.GetType().Name}: {metrics.fps:F1}");
                        TryPerformanceRecovery(manager);
                    }
                    
                    // Check memory usage
                    if (metrics.memoryUsageMB > 1000f) // 1GB threshold
                    {
                        Debug.LogWarning($"[SystemCoordinator] High memory usage in {manager.GetType().Name}: {metrics.memoryUsageMB:F1}MB");
                    }
                }
                catch (Exception ex)
                {
                    HandleSystemError(manager.GetType(), ex);
                }
            }
        }
        
        void TryPerformanceRecovery(IBalloonManager manager)
        {
            try
            {
                // Try to reduce balloon count if supported
                if (manager.BalloonCount > 1000 && manager.TryChangeBalloonCount(manager.BalloonCount - 1000))
                {
                    Debug.Log($"[SystemCoordinator] Reduced balloon count for performance recovery");
                }
                
                // Try to disable expensive features
                if (manager.SupportsFeature(SimulationFeature.FluidDynamics))
                {
                    manager.TrySetFeatureEnabled(SimulationFeature.FluidDynamics, false);
                    Debug.Log($"[SystemCoordinator] Disabled fluid dynamics for performance recovery");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SystemCoordinator] Performance recovery failed: {ex.Message}");
            }
        }
        
        void AggregatePerformanceMetrics()
        {
            if (managers.Count == 0) return;
            
            aggregatedMetrics = PerformanceMetrics.Default;
            int validManagers = 0;
            
            foreach (var manager in managers)
            {
                try
                {
                    var metrics = manager.GetPerformanceMetrics();
                    
                    // Aggregate metrics (average FPS, sum others)
                    aggregatedMetrics.fps += metrics.fps;
                    aggregatedMetrics.physicsTimeMs += metrics.physicsTimeMs;
                    aggregatedMetrics.renderTimeMs += metrics.renderTimeMs;
                    aggregatedMetrics.memoryUsageMB += metrics.memoryUsageMB;
                    aggregatedMetrics.renderedInstances += metrics.renderedInstances;
                    aggregatedMetrics.activeCollisions += metrics.activeCollisions;
                    aggregatedMetrics.jobSchedulingTimeMs += metrics.jobSchedulingTimeMs;
                    
                    validManagers++;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[SystemCoordinator] Failed to get metrics from {manager.GetType().Name}: {ex.Message}");
                }
            }
            
            if (validManagers > 0)
            {
                aggregatedMetrics.fps /= validManagers; // Average FPS
                
                // Broadcast aggregated metrics
                OnPerformanceUpdate?.Invoke(aggregatedMetrics);
                
                foreach (var monitor in performanceMonitors)
                {
                    try
                    {
                        monitor.OnPerformanceUpdate(aggregatedMetrics);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[SystemCoordinator] Performance monitor {monitor.GetType().Name} failed: {ex.Message}");
                    }
                }
            }
        }
        
        void HandleSystemError(Type systemType, Exception exception)
        {
            // Track error count
            if (!errorCounts.ContainsKey(systemType))
                errorCounts[systemType] = 0;
                
            errorCounts[systemType]++;
            lastErrorTimes[systemType] = Time.time;
            
            Debug.LogError($"[SystemCoordinator] System error in {systemType.Name}: {exception.Message}");
            
            // Notify listeners
            OnSystemError?.Invoke(systemType, exception);
            
            // Attempt recovery if enabled and under retry limit
            if (enableErrorRecovery && errorCounts[systemType] <= maxErrorRetries)
            {
                TrySystemRecovery(systemType);
            }
            else if (errorCounts[systemType] > maxErrorRetries)
            {
                Debug.LogError($"[SystemCoordinator] System {systemType.Name} exceeded max error retries ({maxErrorRetries})");
            }
        }
        
        void TrySystemRecovery(Type systemType)
        {
            try
            {
                Debug.Log($"[SystemCoordinator] Attempting recovery for {systemType.Name}");
                
                // System-specific recovery logic
                if (typeof(IBalloonManager).IsAssignableFrom(systemType))
                {
                    var manager = managers.Find(m => m.GetType() == systemType);
                    if (manager != null && manager.State == SimulationState.Error)
                    {
                        manager.ResetSimulation();
                        Debug.Log($"[SystemCoordinator] Reset simulation for {systemType.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SystemCoordinator] Recovery failed for {systemType.Name}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Get the current aggregated performance metrics
        /// </summary>
        public PerformanceMetrics GetAggregatedMetrics()
        {
            return aggregatedMetrics;
        }
        
        /// <summary>
        /// Force a system health check
        /// </summary>
        public void ForceHealthCheck()
        {
            PerformHealthCheck();
        }
        
        /// <summary>
        /// Get error statistics for a system type
        /// </summary>
        public (int errorCount, float lastErrorTime) GetErrorStats(Type systemType)
        {
            var errorCount = errorCounts.GetValueOrDefault(systemType, 0);
            var lastError = lastErrorTimes.GetValueOrDefault(systemType, 0f);
            return (errorCount, lastError);
        }
        
        // IPerformanceMonitor implementation
        public void OnPerformanceUpdate(PerformanceMetrics metrics)
        {
            // This is handled in AggregatePerformanceMetrics
        }
        
        public void OnSimulationStateChanged(SimulationState oldState, SimulationState newState)
        {
            Debug.Log($"[SystemCoordinator] Simulation state changed: {oldState} -> {newState}");
            OnSystemStateChanged?.Invoke(newState);
        }
        
        public void OnFeatureToggled(SimulationFeature feature, bool enabled)
        {
            Debug.Log($"[SystemCoordinator] Feature {feature} toggled: {enabled}");
        }
        
        void OnDestroy()
        {
            // Cleanup
            managers.Clear();
            performanceMonitors.Clear();
            subSystems.Clear();
            
            if (instance == this)
            {
                instance = null;
            }
        }
    }
    
    /// <summary>
    /// Interface for subsystems that can be managed by the coordinator
    /// </summary>
    public interface ISubSystem
    {
        string Name { get; }
        bool IsInitialized { get; }
        void Initialize();
        void Shutdown();
        void Reset();
    }
    
    /// <summary>
    /// Extension methods for better error handling
    /// </summary>
    public static class SystemExtensions
    {
        public static void SafeExecute(this Action action, string context = "Unknown")
        {
            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SafeExecute] Error in {context}: {ex.Message}");
            }
        }
        
        public static T SafeExecute<T>(this Func<T> func, T defaultValue = default, string context = "Unknown")
        {
            try
            {
                return func != null ? func() : defaultValue;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SafeExecute] Error in {context}: {ex.Message}");
                return defaultValue;
            }
        }
    }
}