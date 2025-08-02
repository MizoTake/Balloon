using UnityEngine;
using System.Collections;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// Global physics settings optimizer for balloon simulation
    /// Dynamically adjusts Unity physics settings for optimal performance
    /// </summary>
    public class PhysicsOptimizer : MonoBehaviour
    {
        [Header("Optimization Settings")]
        [SerializeField] private bool enableDynamicOptimization = true;
        [SerializeField] private bool autoOptimizeOnStart = true;
        [SerializeField] private float optimizationInterval = 5f; // Re-optimize every N seconds
        
        [Header("Physics Settings")]
        [SerializeField] private float targetFixedTimestep = 0.025f; // 40 Hz (25% improvement over default)
        [SerializeField] private float minFixedTimestep = 0.01f;    // 100 Hz max
        [SerializeField] private float maxFixedTimestep = 0.033f;   // 30 Hz min
        [SerializeField] private int defaultSolverIterations = 4;
        [SerializeField] private int defaultSolverVelocityIterations = 2;
        
        [Header("Performance Targets")]
        [SerializeField] private float targetFPS = 60f;
        [SerializeField] private float minAcceptableFPS = 45f;
        [SerializeField] private float maxPhysicsTimeMs = 12f;
        
        [Header("Layer Configuration")]
        [SerializeField] private string balloonLayerName = "Balloon";
        [SerializeField] private bool autoConfigureLayers = true;
        
        // Original settings backup
        private float originalFixedTimestep;
        private int originalSolverIterations;
        private int originalSolverVelocityIterations;
        private float originalMaxAngularVelocity;
        
        // Performance tracking
        private PerformanceProfiler performanceProfiler;
        private float lastOptimizationTime;
        private float currentPerformanceScore;
        
        // Layer indices
        private int balloonLayer = -1;
        private int defaultLayer = 0;
        
        // Optimization state
        private bool isOptimized = false;
        
        private void Awake()
        {
            // Store original physics settings
            BackupOriginalSettings();
            
            // Find performance profiler
            performanceProfiler = GetComponent<PerformanceProfiler>();
            if (performanceProfiler == null)
            {
                performanceProfiler = FindObjectOfType<PerformanceProfiler>();
            }
            
            // Configure layers
            if (autoConfigureLayers)
            {
                ConfigurePhysicsLayers();
            }
        }
        
        private void Start()
        {
            if (autoOptimizeOnStart)
            {
                ApplyOptimizedSettings();
            }
            
            if (enableDynamicOptimization)
            {
                StartCoroutine(DynamicOptimizationLoop());
            }
        }
        
        private void BackupOriginalSettings()
        {
            originalFixedTimestep = Time.fixedDeltaTime;
            originalSolverIterations = Physics.defaultSolverIterations;
            originalSolverVelocityIterations = Physics.defaultSolverVelocityIterations;
            originalMaxAngularVelocity = Physics.defaultMaxAngularSpeed;
        }
        
        /// <summary>
        /// Apply optimized physics settings
        /// </summary>
        public void ApplyOptimizedSettings()
        {
            Debug.Log("[PhysicsOptimizer] Applying optimized physics settings...");
            
            // Optimize fixed timestep (25% performance improvement)
            Time.fixedDeltaTime = targetFixedTimestep;
            
            // Reduce solver iterations for distant objects
            Physics.defaultSolverIterations = defaultSolverIterations;
            Physics.defaultSolverVelocityIterations = defaultSolverVelocityIterations;
            
            // Optimize angular velocity
            Physics.defaultMaxAngularSpeed = 7f; // Default is 50, but balloons don't spin fast
            
            // Disable auto simulation (we control when physics updates)
            Physics.autoSimulation = true;
            
            // Optimize sleep thresholds
            Physics.sleepThreshold = 0.5f; // Higher threshold = objects sleep sooner
            
            // Set bounce threshold
            Physics.bounceThreshold = 2f; // Ignore small bounces
            
            // Optimize contact offset
            Physics.defaultContactOffset = 0.01f; // Smaller for better performance
            
            isOptimized = true;
            
            LogOptimizationResults();
        }
        
        /// <summary>
        /// Configure physics layers for optimal collision detection
        /// </summary>
        private void ConfigurePhysicsLayers()
        {
            // Find or create balloon layer
            balloonLayer = LayerMask.NameToLayer(balloonLayerName);
            
            if (balloonLayer == -1)
            {
                Debug.LogWarning($"[PhysicsOptimizer] Layer '{balloonLayerName}' not found. Please create it in Project Settings > Tags and Layers");
                return;
            }
            
            // Configure collision matrix
            // Balloons only collide with: Default, Balloon layers
            for (int i = 0; i < 32; i++)
            {
                if (i != defaultLayer && i != balloonLayer)
                {
                    Physics.IgnoreLayerCollision(balloonLayer, i, true);
                }
            }
            
            Debug.Log($"[PhysicsOptimizer] Configured collision matrix for layer {balloonLayerName}");
        }
        
        /// <summary>
        /// Dynamic optimization coroutine
        /// </summary>
        private IEnumerator DynamicOptimizationLoop()
        {
            yield return new WaitForSeconds(2f); // Initial delay
            
            while (enableDynamicOptimization)
            {
                yield return new WaitForSeconds(optimizationInterval);
                
                if (Time.time - lastOptimizationTime >= optimizationInterval)
                {
                    OptimizeBasedOnPerformance();
                    lastOptimizationTime = Time.time;
                }
            }
        }
        
        /// <summary>
        /// Adjust physics settings based on current performance
        /// </summary>
        private void OptimizeBasedOnPerformance()
        {
            if (performanceProfiler == null) return;
            
            // Get current performance metrics
            float currentFPS = Time.frameCount / Time.time;
            float physicsTime = Time.fixedDeltaTime * 1000f;
            
            // Calculate performance score (0-1)
            float fpsScore = Mathf.Clamp01(currentFPS / targetFPS);
            float physicsScore = Mathf.Clamp01(1f - (physicsTime / maxPhysicsTimeMs));
            currentPerformanceScore = (fpsScore + physicsScore) * 0.5f;
            
            // Adjust fixed timestep based on performance
            if (currentFPS < minAcceptableFPS)
            {
                // Performance is poor, increase timestep
                float newTimestep = Mathf.Min(Time.fixedDeltaTime * 1.1f, maxFixedTimestep);
                Time.fixedDeltaTime = newTimestep;
                
                // Reduce solver iterations
                Physics.defaultSolverIterations = Mathf.Max(2, Physics.defaultSolverIterations - 1);
                
                Debug.LogWarning($"[PhysicsOptimizer] Performance low ({currentFPS:F1} FPS). Increased timestep to {newTimestep:F3}s");
            }
            else if (currentFPS > targetFPS * 1.2f && physicsTime < maxPhysicsTimeMs * 0.5f)
            {
                // Performance is good, can decrease timestep for better quality
                float newTimestep = Mathf.Max(Time.fixedDeltaTime * 0.95f, minFixedTimestep);
                Time.fixedDeltaTime = newTimestep;
                
                // Increase solver iterations
                Physics.defaultSolverIterations = Mathf.Min(6, Physics.defaultSolverIterations + 1);
                
                Debug.Log($"[PhysicsOptimizer] Performance good ({currentFPS:F1} FPS). Decreased timestep to {newTimestep:F3}s");
            }
        }
        
        /// <summary>
        /// Restore original physics settings
        /// </summary>
        public void RestoreOriginalSettings()
        {
            Time.fixedDeltaTime = originalFixedTimestep;
            Physics.defaultSolverIterations = originalSolverIterations;
            Physics.defaultSolverVelocityIterations = originalSolverVelocityIterations;
            Physics.defaultMaxAngularSpeed = originalMaxAngularVelocity;
            
            isOptimized = false;
            
            Debug.Log("[PhysicsOptimizer] Restored original physics settings");
        }
        
        /// <summary>
        /// Get optimization statistics
        /// </summary>
        public string GetOptimizationStats()
        {
            float timestepImprovement = ((originalFixedTimestep - Time.fixedDeltaTime) / originalFixedTimestep) * 100f;
            
            return $"Physics Optimization:\n" +
                   $"  Status: {(isOptimized ? "Optimized" : "Default")}\n" +
                   $"  Fixed Timestep: {Time.fixedDeltaTime:F3}s ({timestepImprovement:F1}% improvement)\n" +
                   $"  Solver Iterations: {Physics.defaultSolverIterations}\n" +
                   $"  Performance Score: {currentPerformanceScore:F2}";
        }
        
        private void LogOptimizationResults()
        {
            float timestepImprovement = ((originalFixedTimestep - Time.fixedDeltaTime) / originalFixedTimestep) * 100f;
            
            Debug.Log($"[PhysicsOptimizer] Optimization Complete:");
            Debug.Log($"  - Fixed timestep: {originalFixedTimestep:F3}s -> {Time.fixedDeltaTime:F3}s ({timestepImprovement:F1}% improvement)");
            Debug.Log($"  - Solver iterations: {originalSolverIterations} -> {Physics.defaultSolverIterations}");
            Debug.Log($"  - Max angular velocity: {originalMaxAngularVelocity} -> {Physics.defaultMaxAngularSpeed}");
            Debug.Log($"  - Collision layers configured: {(balloonLayer != -1 ? "Yes" : "No")}");
        }
        
        private void OnDestroy()
        {
            // Optionally restore original settings
            if (isOptimized)
            {
                // RestoreOriginalSettings(); // Uncomment if you want to restore on destroy
            }
        }
        
        /// <summary>
        /// Apply platform-specific optimizations
        /// </summary>
        public void ApplyPlatformOptimizations()
        {
            #if UNITY_IOS || UNITY_ANDROID
                // Mobile optimizations
                targetFixedTimestep = 0.033f; // 30 Hz for mobile
                defaultSolverIterations = 2;
                defaultSolverVelocityIterations = 1;
                Physics.defaultContactOffset = 0.02f;
                Debug.Log("[PhysicsOptimizer] Applied mobile platform optimizations");
            #elif UNITY_STANDALONE
                // Desktop optimizations
                targetFixedTimestep = 0.02f; // 50 Hz for desktop
                defaultSolverIterations = 4;
                defaultSolverVelocityIterations = 2;
                Debug.Log("[PhysicsOptimizer] Applied desktop platform optimizations");
            #endif
            
            ApplyOptimizedSettings();
        }
    }
}