using UnityEngine;
using UnityEngine.Profiling;
using System.Collections.Generic;
using System.Text;

public class PerformanceProfiler : MonoBehaviour
{
    [Header("Profiling Settings")]
    [SerializeField] private bool enableProfiling = true;
    [SerializeField] private float updateInterval = 1f;
    [SerializeField] private int maxFrameHistory = 100;
    [SerializeField] private bool displayOnScreen = true;
    
    [Header("Performance Thresholds")]
    [SerializeField] private float targetFrameRate = 60f;
    [SerializeField] private float warningFrameRate = 45f;
    [SerializeField] private long maxMemoryUsageMB = 100;
    
    public struct FrameData
    {
        public float frameTime;
        public float physicsTime;
        public float renderTime;
        public long memoryUsage;
        public int balloonCount;
    }
    
    private List<FrameData> frameHistory = new List<FrameData>();
    private float nextUpdateTime;
    private float lastFrameTime;
    private float physicsStartTime;
    private StringBuilder displayText = new StringBuilder();
    
    // Performance metrics
    private float averageFrameRate;
    private float averagePhysicsTime;
    private float averageRenderTime;
    private long currentMemoryUsage;
    private int activeBalloonCount;
    
    // GUI display
    private GUIStyle textStyle;
    private bool showDetailedStats = false;
    
    void Start()
    {
        if (enableProfiling)
        {
            InvokeRepeating(nameof(UpdateProfilerData), 0f, updateInterval);
        }
        
        // Setup GUI style
        textStyle = new GUIStyle();
        textStyle.fontSize = 14;
        textStyle.normal.textColor = Color.white;
    }
    
    void Update()
    {
        if (!enableProfiling) return;
        
        // Record frame time
        lastFrameTime = Time.unscaledDeltaTime;
        
        // Toggle detailed stats with F1
        if (Input.GetKeyDown(KeyCode.F1))
        {
            showDetailedStats = !showDetailedStats;
        }
    }
    
    void FixedUpdate()
    {
        if (!enableProfiling) return;
        
        physicsStartTime = Time.realtimeSinceStartup;
    }
    
    void LateUpdate()
    {
        if (!enableProfiling) return;
        
        // Calculate physics time (approximate)
        float physicsTime = Time.realtimeSinceStartup - physicsStartTime;
        
        // Record frame data
        FrameData frameData = new FrameData
        {
            frameTime = lastFrameTime,
            physicsTime = physicsTime,
            renderTime = lastFrameTime - physicsTime,
            memoryUsage = Profiler.GetTotalAllocatedMemory(),
            balloonCount = CountActiveBalloons()
        };
        
        frameHistory.Add(frameData);
        
        // Maintain history limit
        if (frameHistory.Count > maxFrameHistory)
        {
            frameHistory.RemoveAt(0);
        }
    }
    
    void UpdateProfilerData()
    {
        if (frameHistory.Count == 0) return;
        
        // Calculate averages
        float totalFrameTime = 0f;
        float totalPhysicsTime = 0f;
        float totalRenderTime = 0f;
        long totalMemory = 0;
        
        foreach (FrameData frame in frameHistory)
        {
            totalFrameTime += frame.frameTime;
            totalPhysicsTime += frame.physicsTime;
            totalRenderTime += frame.renderTime;
            totalMemory += frame.memoryUsage;
        }
        
        int frameCount = frameHistory.Count;
        averageFrameRate = 1f / (totalFrameTime / frameCount);
        averagePhysicsTime = (totalPhysicsTime / frameCount) * 1000f; // Convert to ms
        averageRenderTime = (totalRenderTime / frameCount) * 1000f; // Convert to ms
        currentMemoryUsage = totalMemory / frameCount;
        activeBalloonCount = CountActiveBalloons();
        
        // Check performance thresholds
        CheckPerformanceThresholds();
        
        // Update display text
        UpdateDisplayText();
    }
    
    void CheckPerformanceThresholds()
    {
        if (averageFrameRate < warningFrameRate)
        {
            Debug.LogWarning($"Performance Warning: Frame rate dropped to {averageFrameRate:F1} FPS");
        }
        
        long memoryMB = currentMemoryUsage / (1024 * 1024);
        if (memoryMB > maxMemoryUsageMB)
        {
            Debug.LogWarning($"Memory Warning: Usage is {memoryMB} MB (limit: {maxMemoryUsageMB} MB)");
        }
    }
    
    void UpdateDisplayText()
    {
        displayText.Clear();
        
        // Basic stats
        displayText.AppendLine($"FPS: {averageFrameRate:F1}");
        displayText.AppendLine($"Balloons: {activeBalloonCount}");
        displayText.AppendLine($"Memory: {currentMemoryUsage / (1024 * 1024)} MB");
        
        if (showDetailedStats)
        {
            displayText.AppendLine("");
            displayText.AppendLine("DETAILED STATS:");
            displayText.AppendLine($"Physics: {averagePhysicsTime:F2}ms");
            displayText.AppendLine($"Render: {averageRenderTime:F2}ms");
            displayText.AppendLine($"Total: {(averagePhysicsTime + averageRenderTime):F2}ms");
            displayText.AppendLine("");
            displayText.AppendLine($"Target FPS: {targetFrameRate}");
            displayText.AppendLine($"Warning FPS: {warningFrameRate}");
            displayText.AppendLine("");
            displayText.AppendLine("Press F1 to toggle details");
        }
        else
        {
            displayText.AppendLine("Press F1 for details");
        }
    }
    
    int CountActiveBalloons()
    {
        BalloonController[] balloons = FindObjectsOfType<BalloonController>();
        int activeCount = 0;
        
        foreach (BalloonController balloon in balloons)
        {
            if (balloon.gameObject.activeInHierarchy)
            {
                activeCount++;
            }
        }
        
        return activeCount;
    }
    
    void OnGUI()
    {
        if (!enableProfiling || !displayOnScreen) return;
        
        // Performance color coding
        Color textColor = Color.white;
        if (averageFrameRate < warningFrameRate)
            textColor = Color.yellow;
        if (averageFrameRate < targetFrameRate * 0.5f)
            textColor = Color.red;
        
        textStyle.normal.textColor = textColor;
        
        // Display performance info
        GUI.Label(new Rect(10, 10, 300, 400), displayText.ToString(), textStyle);
    }
    
    public void LogPerformanceReport()
    {
        StringBuilder report = new StringBuilder();
        report.AppendLine("=== BALLOON PHYSICS PERFORMANCE REPORT ===");
        report.AppendLine($"Active Balloons: {activeBalloonCount}");
        report.AppendLine($"Average FPS: {averageFrameRate:F2}");
        report.AppendLine($"Physics Time: {averagePhysicsTime:F2}ms");
        report.AppendLine($"Render Time: {averageRenderTime:F2}ms");
        report.AppendLine($"Memory Usage: {currentMemoryUsage / (1024 * 1024)} MB");
        report.AppendLine($"Fixed Timestep: {Time.fixedDeltaTime:F3}s");
        report.AppendLine($"Solver Iterations: {Physics.defaultSolverIterations}");
        report.AppendLine("==========================================");
        
        Debug.Log(report.ToString());
    }
    
    public FrameData GetCurrentFrameData()
    {
        if (frameHistory.Count > 0)
            return frameHistory[frameHistory.Count - 1];
        
        return new FrameData();
    }
    
    public bool IsPerformanceAcceptable()
    {
        return averageFrameRate >= warningFrameRate && 
               currentMemoryUsage < maxMemoryUsageMB * 1024 * 1024;
    }
}