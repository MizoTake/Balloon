using UnityEngine;

public class BalloonManager : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] private BalloonSpawner spawner;
    [SerializeField] private PhysicsOptimizer physicsOptimizer;
    [SerializeField] private BalloonLODSystem lodSystem;
    [SerializeField] private WindSystem windSystem;
    [SerializeField] private PerformanceProfiler profiler;
    
    [Header("Auto Setup")]
    [SerializeField] private bool autoSetupComponents = true;
    [SerializeField] private bool enablePerformanceMonitoring = true;
    
    void Start()
    {
        if (autoSetupComponents)
        {
            SetupComponents();
        }
        
        StartBalloonSimulation();
    }
    
    void SetupComponents()
    {
        // Physics Optimizer
        if (physicsOptimizer == null)
        {
            physicsOptimizer = FindObjectOfType<PhysicsOptimizer>();
            if (physicsOptimizer == null)
            {
                GameObject physicsObj = new GameObject("PhysicsOptimizer");
                physicsOptimizer = physicsObj.AddComponent<PhysicsOptimizer>();
            }
        }
        
        // LOD System
        if (lodSystem == null)
        {
            lodSystem = FindObjectOfType<BalloonLODSystem>();
            if (lodSystem == null)
            {
                GameObject lodObj = new GameObject("BalloonLODSystem");
                lodSystem = lodObj.AddComponent<BalloonLODSystem>();
            }
        }
        
        // Wind System
        if (windSystem == null)
        {
            windSystem = FindObjectOfType<WindSystem>();
            if (windSystem == null)
            {
                GameObject windObj = new GameObject("WindSystem");
                windSystem = windObj.AddComponent<WindSystem>();
            }
        }
        
        // Performance Profiler
        if (enablePerformanceMonitoring && profiler == null)
        {
            profiler = FindObjectOfType<PerformanceProfiler>();
            if (profiler == null)
            {
                GameObject profilerObj = new GameObject("PerformanceProfiler");
                profiler = profilerObj.AddComponent<PerformanceProfiler>();
            }
        }
        
        // Balloon Spawner
        if (spawner == null)
        {
            spawner = FindObjectOfType<BalloonSpawner>();
            if (spawner == null)
            {
                GameObject spawnerObj = new GameObject("BalloonSpawner");
                spawner = spawnerObj.AddComponent<BalloonSpawner>();
            }
        }
    }
    
    void StartBalloonSimulation()
    {
        Debug.Log("Starting optimized balloon physics simulation for 100 balloons");
        Debug.Log("Expected performance: 60-75 FPS on desktop, 30-40 FPS on mobile");
        
        if (profiler != null)
        {
            Invoke(nameof(LogInitialPerformanceReport), 5f);
        }
    }
    
    void LogInitialPerformanceReport()
    {
        if (profiler != null)
        {
            profiler.LogPerformanceReport();
        }
    }
    
    void Update()
    {
        // Runtime controls
        if (Input.GetKeyDown(KeyCode.R))
        {
            RestartSimulation();
        }
        
        if (Input.GetKeyDown(KeyCode.P) && profiler != null)
        {
            profiler.LogPerformanceReport();
        }
    }
    
    public void RestartSimulation()
    {
        if (spawner != null)
        {
            spawner.ClearBalloons();
            spawner.Invoke(nameof(BalloonSpawner.SpawnBalloons), 0.1f);
        }
        
        if (lodSystem != null)
        {
            lodSystem.Invoke(nameof(BalloonLODSystem.RegisterAllBalloons), 0.2f);
        }
        
        if (windSystem != null)
        {
            windSystem.RefreshBalloonList();
        }
    }
    
    public void SetQualityLevel(PhysicsOptimizer.QualityLevel level)
    {
        if (physicsOptimizer != null)
        {
            physicsOptimizer.SetQualityLevel(level);
        }
    }
}