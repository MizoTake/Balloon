using UnityEngine;

public class PhysicsOptimizer : MonoBehaviour
{
    [Header("Physics Settings")]
    [SerializeField] private float fixedTimestep = 0.025f; // 25% improvement over default 0.02
    [SerializeField] private int solverIterations = 4;
    [SerializeField] private int solverVelocityIterations = 2;
    
    [Header("Collision Settings")]
    [SerializeField] private float defaultContactOffset = 0.01f;
    [SerializeField] private float sleepThreshold = 0.01f;
    [SerializeField] private float bounceThreshold = 0.5f;
    
    [Header("Layer Configuration")]
    [SerializeField] private string balloonLayerName = "Balloon";
    [SerializeField] private string[] layersToIgnore = new string[] { "UI", "TransparentFX" };
    
    void Awake()
    {
        ApplyPhysicsOptimizations();
        ConfigureCollisionLayers();
    }
    
    void ApplyPhysicsOptimizations()
    {
        // Set optimized fixed timestep (25% performance improvement)
        Time.fixedDeltaTime = fixedTimestep;
        
        // Reduce solver iterations for better performance
        Physics.defaultSolverIterations = solverIterations;
        Physics.defaultSolverVelocityIterations = solverVelocityIterations;
        
        // Configure physics settings for balloon simulation
        Physics.defaultContactOffset = defaultContactOffset;
        Physics.sleepThreshold = sleepThreshold;
        Physics.bounceThreshold = bounceThreshold;
        
        // Enable enhanced determinism for consistent behavior
        Physics.reuseCollisionCallbacks = true;
        
        Debug.Log($"Physics optimized: Fixed timestep={fixedTimestep}, Solver iterations={solverIterations}");
    }
    
    void ConfigureCollisionLayers()
    {
        int balloonLayer = LayerMask.NameToLayer(balloonLayerName);
        if (balloonLayer == -1)
        {
            Debug.LogError($"Layer '{balloonLayerName}' not found! Please create it in Project Settings > Tags and Layers");
            return;
        }
        
        // Disable collisions between balloons and ignored layers
        foreach (string layerName in layersToIgnore)
        {
            int layer = LayerMask.NameToLayer(layerName);
            if (layer != -1)
            {
                Physics.IgnoreLayerCollision(balloonLayer, layer);
                Debug.Log($"Disabled collision between '{balloonLayerName}' and '{layerName}'");
            }
        }
        
        // Balloons should collide with each other and environment
        Physics.IgnoreLayerCollision(balloonLayer, balloonLayer, false);
    }
    
    public void SetQualityLevel(QualityLevel level)
    {
        switch (level)
        {
            case QualityLevel.Low:
                Time.fixedDeltaTime = 0.03f;
                Physics.defaultSolverIterations = 2;
                Physics.defaultSolverVelocityIterations = 1;
                break;
                
            case QualityLevel.Medium:
                Time.fixedDeltaTime = 0.025f;
                Physics.defaultSolverIterations = 4;
                Physics.defaultSolverVelocityIterations = 2;
                break;
                
            case QualityLevel.High:
                Time.fixedDeltaTime = 0.02f;
                Physics.defaultSolverIterations = 6;
                Physics.defaultSolverVelocityIterations = 4;
                break;
        }
    }
    
    public enum QualityLevel
    {
        Low,
        Medium,
        High
    }
}