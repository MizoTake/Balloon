using System.Collections.Generic;
using UnityEngine;
using Unity.Collections;
using Unity.Profiling;
using System.Text;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// Real-time performance monitoring system for balloon simulation
    /// Tracks FPS, physics time, rendering time, and system-specific metrics
    /// </summary>
    public class PerformanceProfiler : MonoBehaviour
    {
        [Header("Display Settings")]
        [SerializeField] private bool showDetailedStats = false;
        [SerializeField] private KeyCode toggleKey = KeyCode.F1;
        [SerializeField] private float updateInterval = 0.5f;
        
        [Header("Performance Thresholds")]
        [SerializeField] private float warningFPSThreshold = 45f;
        [SerializeField] private float criticalFPSThreshold = 30f;
        [SerializeField] private float warningPhysicsTime = 12f; // ms
        [SerializeField] private float criticalPhysicsTime = 16f; // ms
        
        [Header("UI Settings")]
        [SerializeField] private bool showGraph = true;
        [SerializeField] private int graphHistorySize = 120;
        [SerializeField] private Rect displayRect = new Rect(10, 10, 400, 300);
        
        // Performance metrics
        private float currentFPS;
        private float averageFPS;
        private float minFPS;
        private float maxFPS;
        
        private float physicsTime;
        private float renderTime;
        private float updateTime;
        private float totalFrameTime;
        
        // Memory metrics
        private long totalMemory;
        private long gcMemory;
        
        // System-specific metrics
        private int activeBalloons;
        private int totalBalloons;
        private int collisionChecks;
        private int activeCollisions;
        
        // History for graphing
        private Queue<float> fpsHistory = new Queue<float>();
        private Queue<float> physicsHistory = new Queue<float>();
        private Queue<float> renderHistory = new Queue<float>();
        
        // Update tracking
        private float lastUpdateTime;
        private int frameCount;
        private float frameTimeAccumulator;
        
        // Profiler markers
        private ProfilerMarker physicsMarker;
        private ProfilerMarker renderingMarker;
        private ProfilerMarker updateMarker;
        
        // References
        private BalloonManager balloonManager;
        private BalloonLODSystem lodSystem;
        
        // GUIStyle cache
        private GUIStyle labelStyle;
        private GUIStyle warningStyle;
        private GUIStyle criticalStyle;
        private GUIStyle boxStyle;
        
        private void Awake()
        {
            // Initialize profiler markers
            physicsMarker = new ProfilerMarker("BalloonPhysics");
            renderingMarker = new ProfilerMarker("BalloonRendering");
            updateMarker = new ProfilerMarker("BalloonUpdate");
            
            // Find systems
            balloonManager = FindObjectOfType<BalloonManager>();
            lodSystem = FindObjectOfType<BalloonLODSystem>();
            
            // Initialize history
            for (int i = 0; i < graphHistorySize; i++)
            {
                fpsHistory.Enqueue(60f);
                physicsHistory.Enqueue(0f);
                renderHistory.Enqueue(0f);
            }
            
            lastUpdateTime = Time.time;
        }
        
        private void Update()
        {
            // Toggle detailed stats
            if (Input.GetKeyDown(toggleKey))
            {
                showDetailedStats = !showDetailedStats;
                Debug.Log($"Performance Profiler: Detailed stats {(showDetailedStats ? "ON" : "OFF")}");
            }
            
            // Log performance report
            if (Input.GetKeyDown(KeyCode.P))
            {
                LogPerformanceReport();
            }
            
            // Track frame time
            frameCount++;
            frameTimeAccumulator += Time.deltaTime;
            
            // Update metrics at interval
            if (Time.time - lastUpdateTime >= updateInterval)
            {
                UpdateMetrics();
                lastUpdateTime = Time.time;
            }
        }
        
        private void UpdateMetrics()
        {
            // Calculate FPS
            currentFPS = frameCount / frameTimeAccumulator;
            averageFPS = Mathf.Lerp(averageFPS, currentFPS, 0.1f);
            
            if (frameCount > 10) // Skip initial frames
            {
                minFPS = Mathf.Min(minFPS == 0 ? currentFPS : minFPS, currentFPS);
                maxFPS = Mathf.Max(maxFPS, currentFPS);
            }
            
            // Get timing from Unity profiler
            physicsTime = Time.fixedDeltaTime * 1000f;
            renderTime = Time.deltaTime * 1000f - physicsTime;
            totalFrameTime = Time.deltaTime * 1000f;
            
            // Get memory info
            totalMemory = System.GC.GetTotalMemory(false) / (1024 * 1024); // MB
            gcMemory = UnityEngine.Profiling.Profiler.GetMonoUsedSizeLong() / (1024 * 1024); // MB
            
            // Get balloon statistics
            if (balloonManager != null)
            {
                totalBalloons = balloonManager.BalloonCount;
            }
            
            if (lodSystem != null)
            {
                lodSystem.UpdateMetrics();
            }
            
            // Update history
            fpsHistory.Dequeue();
            fpsHistory.Enqueue(currentFPS);
            
            physicsHistory.Dequeue();
            physicsHistory.Enqueue(physicsTime);
            
            renderHistory.Dequeue();
            renderHistory.Enqueue(renderTime);
            
            // Reset counters
            frameCount = 0;
            frameTimeAccumulator = 0f;
            
            // Check performance warnings
            CheckPerformanceWarnings();
        }
        
        private void CheckPerformanceWarnings()
        {
            if (currentFPS < criticalFPSThreshold)
            {
                Debug.LogError($"[CRITICAL] FPS dropped below {criticalFPSThreshold}: {currentFPS:F1} FPS");
            }
            else if (currentFPS < warningFPSThreshold)
            {
                Debug.LogWarning($"[WARNING] FPS below target: {currentFPS:F1} FPS (target: {warningFPSThreshold})");
            }
            
            if (physicsTime > criticalPhysicsTime)
            {
                Debug.LogError($"[CRITICAL] Physics time exceeded {criticalPhysicsTime}ms: {physicsTime:F1}ms");
            }
            else if (physicsTime > warningPhysicsTime)
            {
                Debug.LogWarning($"[WARNING] Physics time high: {physicsTime:F1}ms (target: {warningPhysicsTime}ms)");
            }
        }
        
        private void OnGUI()
        {
            if (!showDetailedStats) return;
            
            InitializeGUIStyles();
            
            // Main display box
            GUI.Box(displayRect, "Performance Profiler", boxStyle);
            
            GUILayout.BeginArea(new Rect(displayRect.x + 10, displayRect.y + 30, displayRect.width - 20, displayRect.height - 40));
            
            // FPS Section
            DrawFPSSection();
            
            GUILayout.Space(10);
            
            // Timing Section
            DrawTimingSection();
            
            GUILayout.Space(10);
            
            // Memory Section
            DrawMemorySection();
            
            GUILayout.Space(10);
            
            // Balloon Statistics
            DrawBalloonStatistics();
            
            if (showGraph)
            {
                GUILayout.Space(10);
                DrawPerformanceGraph();
            }
            
            GUILayout.EndArea();
        }
        
        private void DrawFPSSection()
        {
            GUILayout.Label("Frame Rate", labelStyle);
            
            var fpsStyle = GetStyleForFPS(currentFPS);
            GUILayout.Label($"Current: {currentFPS:F1} FPS", fpsStyle);
            GUILayout.Label($"Average: {averageFPS:F1} FPS", labelStyle);
            GUILayout.Label($"Min/Max: {minFPS:F1} / {maxFPS:F1} FPS", labelStyle);
        }
        
        private void DrawTimingSection()
        {
            GUILayout.Label("Frame Timing", labelStyle);
            
            var physicsStyle = GetStyleForPhysicsTime(physicsTime);
            GUILayout.Label($"Physics: {physicsTime:F2}ms", physicsStyle);
            GUILayout.Label($"Rendering: {renderTime:F2}ms", labelStyle);
            GUILayout.Label($"Total Frame: {totalFrameTime:F2}ms", labelStyle);
        }
        
        private void DrawMemorySection()
        {
            GUILayout.Label("Memory Usage", labelStyle);
            
            GUILayout.Label($"Total Memory: {totalMemory} MB", labelStyle);
            GUILayout.Label($"GC Memory: {gcMemory} MB", labelStyle);
        }
        
        private void DrawBalloonStatistics()
        {
            GUILayout.Label("Balloon Statistics", labelStyle);
            
            GUILayout.Label($"Total Balloons: {totalBalloons}", labelStyle);
            
            if (lodSystem != null)
            {
                string lodStats = lodSystem.GetStatistics();
                foreach (var line in lodStats.Split('\n'))
                {
                    if (!string.IsNullOrEmpty(line))
                        GUILayout.Label(line, labelStyle);
                }
            }
        }
        
        private void DrawPerformanceGraph()
        {
            // Simple performance graph
            Rect graphRect = GUILayoutUtility.GetRect(displayRect.width - 20, 60);
            GUI.Box(graphRect, GUIContent.none);
            
            // Draw FPS graph
            DrawGraph(graphRect, fpsHistory, Color.green, 0, 120);
            
            // Draw physics time graph (scaled)
            DrawGraph(graphRect, physicsHistory, Color.red, 0, 20);
        }
        
        private void DrawGraph(Rect rect, Queue<float> data, Color color, float minValue, float maxValue)
        {
            if (data.Count < 2) return;
            
            var points = new List<Vector2>();
            int index = 0;
            float step = rect.width / (data.Count - 1);
            
            foreach (var value in data)
            {
                float x = rect.x + index * step;
                float normalizedValue = Mathf.InverseLerp(minValue, maxValue, value);
                float y = rect.y + rect.height - (normalizedValue * rect.height);
                points.Add(new Vector2(x, y));
                index++;
            }
            
            // Draw lines between points
            for (int i = 0; i < points.Count - 1; i++)
            {
                DrawLine(points[i], points[i + 1], color);
            }
        }
        
        private void DrawLine(Vector2 start, Vector2 end, Color color)
        {
            Color previousColor = GUI.color;
            GUI.color = color;
            
            float angle = Mathf.Atan2(end.y - start.y, end.x - start.x) * Mathf.Rad2Deg;
            float length = Vector2.Distance(start, end);
            
            GUIUtility.RotateAroundPivot(angle, start);
            GUI.DrawTexture(new Rect(start.x, start.y - 1, length, 2), Texture2D.whiteTexture);
            GUIUtility.RotateAroundPivot(-angle, start);
            
            GUI.color = previousColor;
        }
        
        private void InitializeGUIStyles()
        {
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label);
                labelStyle.fontSize = 12;
                labelStyle.normal.textColor = Color.white;
                
                warningStyle = new GUIStyle(labelStyle);
                warningStyle.normal.textColor = Color.yellow;
                
                criticalStyle = new GUIStyle(labelStyle);
                criticalStyle.normal.textColor = Color.red;
                
                boxStyle = new GUIStyle(GUI.skin.box);
                boxStyle.normal.background = MakeColorTexture(new Color(0, 0, 0, 0.8f));
            }
        }
        
        private Texture2D MakeColorTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
        
        private GUIStyle GetStyleForFPS(float fps)
        {
            if (fps < criticalFPSThreshold) return criticalStyle;
            if (fps < warningFPSThreshold) return warningStyle;
            return labelStyle;
        }
        
        private GUIStyle GetStyleForPhysicsTime(float time)
        {
            if (time > criticalPhysicsTime) return criticalStyle;
            if (time > warningPhysicsTime) return warningStyle;
            return labelStyle;
        }
        
        public void LogPerformanceReport()
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("=== PERFORMANCE REPORT ===");
            report.AppendLine($"Time: {System.DateTime.Now}");
            report.AppendLine($"");
            report.AppendLine($"Frame Rate:");
            report.AppendLine($"  Current: {currentFPS:F1} FPS");
            report.AppendLine($"  Average: {averageFPS:F1} FPS");
            report.AppendLine($"  Min/Max: {minFPS:F1} / {maxFPS:F1} FPS");
            report.AppendLine($"");
            report.AppendLine($"Frame Timing:");
            report.AppendLine($"  Physics: {physicsTime:F2}ms");
            report.AppendLine($"  Rendering: {renderTime:F2}ms");
            report.AppendLine($"  Total: {totalFrameTime:F2}ms");
            report.AppendLine($"");
            report.AppendLine($"Memory:");
            report.AppendLine($"  Total: {totalMemory} MB");
            report.AppendLine($"  GC: {gcMemory} MB");
            report.AppendLine($"");
            report.AppendLine($"Balloons: {totalBalloons}");
            
            if (lodSystem != null)
            {
                report.AppendLine($"");
                report.AppendLine(lodSystem.GetStatistics());
            }
            
            report.AppendLine("========================");
            
            Debug.Log(report.ToString());
        }
        
        /// <summary>
        /// Update balloon count for external tracking
        /// </summary>
        public void UpdateBalloonCount(int total, int active)
        {
            totalBalloons = total;
            activeBalloons = active;
        }
        
        /// <summary>
        /// Update collision statistics
        /// </summary>
        public void UpdateCollisionStats(int checks, int collisions)
        {
            collisionChecks = checks;
            activeCollisions = collisions;
        }
    }
}