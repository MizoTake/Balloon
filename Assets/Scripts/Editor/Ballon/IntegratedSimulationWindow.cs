using UnityEngine;
using UnityEditor;
using BalloonSimulation.JobSystem;
using System.Collections.Generic;
using System.Linq;

namespace BalloonSimulation.Editor
{
    /// <summary>
    /// Integrated simulation control window for comprehensive system management
    /// Provides unified interface for all balloon simulation systems
    /// </summary>
    public class IntegratedSimulationWindow : EditorWindow
    {
        private SystemCoordinator coordinator;
        private List<IBalloonManager> managers = new List<IBalloonManager>();
        private Dictionary<string, bool> systemStates = new Dictionary<string, bool>();
        private PerformanceMetrics lastMetrics;
        
        // UI State
        private Vector2 scrollPosition;
        private bool showAdvancedOptions = false;
        private bool autoRefresh = true;
        private float refreshInterval = 0.5f;
        private double lastRefreshTime;
        
        // Performance tracking
        private Queue<float> fpsHistory = new Queue<float>();
        private Queue<float> memoryHistory = new Queue<float>();
        private const int maxHistoryPoints = 100;
        
        [MenuItem("Tools/Balloon Physics/Integrated Simulation Control")]
        public static void ShowWindow()
        {
            var window = GetWindow<IntegratedSimulationWindow>("Integrated Simulation Control");
            window.minSize = new Vector2(500, 600);
        }
        
        void OnEnable()
        {
            RefreshSystemReferences();
            EditorApplication.update += OnEditorUpdate;
        }
        
        void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
        }
        
        void OnEditorUpdate()
        {
            if (autoRefresh && EditorApplication.timeSinceStartup - lastRefreshTime > refreshInterval)
            {
                RefreshData();
                lastRefreshTime = EditorApplication.timeSinceStartup;
                Repaint();
            }
        }
        
        void RefreshSystemReferences()
        {
            // Find SystemCoordinator
            coordinator = FindObjectOfType<SystemCoordinator>();
            
            // Find all balloon managers
            managers.Clear();
            var allManagers = FindObjectsOfType<MonoBehaviour>().OfType<IBalloonManager>();
            managers.AddRange(allManagers);
            
            // Initialize system states
            systemStates.Clear();
            systemStates["FluidDynamics"] = false;
            systemStates["SwarmPhysics"] = false;
            systemStates["GPUPhysics"] = false;
            systemStates["Deformation"] = false;
            systemStates["LODSystem"] = true;
            systemStates["PerformanceProfiling"] = true;
        }
        
        void RefreshData()
        {
            if (coordinator != null)
            {
                lastMetrics = coordinator.GetAggregatedMetrics();
                
                // Update performance history
                fpsHistory.Enqueue(lastMetrics.fps);
                memoryHistory.Enqueue(lastMetrics.memoryUsageMB);
                
                if (fpsHistory.Count > maxHistoryPoints)
                    fpsHistory.Dequeue();
                if (memoryHistory.Count > maxHistoryPoints)
                    memoryHistory.Dequeue();
            }
            else if (managers.Count > 0)
            {
                // Aggregate from individual managers
                var aggregated = PerformanceMetrics.Default;
                int validManagers = 0;
                
                foreach (var manager in managers)
                {
                    try
                    {
                        var metrics = manager.GetPerformanceMetrics();
                        aggregated.fps += metrics.fps;
                        aggregated.physicsTimeMs += metrics.physicsTimeMs;
                        aggregated.renderTimeMs += metrics.renderTimeMs;
                        aggregated.memoryUsageMB += metrics.memoryUsageMB;
                        aggregated.renderedInstances += metrics.renderedInstances;
                        validManagers++;
                    }
                    catch
                    {
                        // Skip failed managers
                    }
                }
                
                if (validManagers > 0)
                {
                    aggregated.fps /= validManagers;
                    lastMetrics = aggregated;
                }
            }
        }
        
        void OnGUI()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            
            DrawHeader();
            EditorGUILayout.Space();
            
            DrawSystemStatus();
            EditorGUILayout.Space();
            
            DrawPerformanceMetrics();
            EditorGUILayout.Space();
            
            DrawSystemControls();
            EditorGUILayout.Space();
            
            DrawManagerControls();
            EditorGUILayout.Space();
            
            if (showAdvancedOptions)
            {
                DrawAdvancedOptions();
                EditorGUILayout.Space();
            }
            
            DrawPerformanceGraphs();
            
            EditorGUILayout.EndScrollView();
        }
        
        void DrawHeader()
        {
            EditorGUILayout.LabelField("Integrated Balloon Simulation Control", EditorStyles.largeLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Refresh Systems"))
            {
                RefreshSystemReferences();
            }
            if (GUILayout.Button("Force Health Check") && coordinator != null)
            {
                coordinator.ForceHealthCheck();
            }
            showAdvancedOptions = GUILayout.Toggle(showAdvancedOptions, "Advanced", "Button");
            EditorGUILayout.EndHorizontal();
            
            // Auto-refresh controls
            EditorGUILayout.BeginHorizontal();
            autoRefresh = EditorGUILayout.Toggle("Auto Refresh", autoRefresh);
            if (autoRefresh)
            {
                refreshInterval = EditorGUILayout.Slider("Interval", refreshInterval, 0.1f, 2f);
            }
            EditorGUILayout.EndHorizontal();
        }
        
        void DrawSystemStatus()
        {
            EditorGUILayout.LabelField("System Status", EditorStyles.boldLabel);
            
            // Coordinator status
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("System Coordinator:", GUILayout.Width(150));
            if (coordinator != null)
            {
                GUI.color = Color.green;
                EditorGUILayout.LabelField("✓ Active");
            }
            else
            {
                GUI.color = Color.red;
                EditorGUILayout.LabelField("✗ Not Found");
                if (GUILayout.Button("Create", GUILayout.Width(60)))
                {
                    CreateSystemCoordinator();
                }
            }
            GUI.color = Color.white;
            EditorGUILayout.EndHorizontal();
            
            // Manager status
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Balloon Managers:", GUILayout.Width(150));
            EditorGUILayout.LabelField($"{managers.Count} found");
            if (managers.Count == 0)
            {
                if (GUILayout.Button("Setup Demo", GUILayout.Width(80)))
                {
                    BalloonSimulationEditorTools.QuickSetup();
                    RefreshSystemReferences();
                }
            }
            EditorGUILayout.EndHorizontal();
            
            // Individual manager status
            if (managers.Count > 0)
            {
                EditorGUI.indentLevel++;
                foreach (var manager in managers)
                {
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField($"• {manager.GetType().Name}:", GUILayout.Width(200));
                    
                    var state = manager.State;
                    GUI.color = GetStateColor(state);
                    EditorGUILayout.LabelField($"{state} ({manager.BalloonCount}/{manager.MaxBalloonCount})");
                    GUI.color = Color.white;
                    EditorGUILayout.EndHorizontal();
                }
                EditorGUI.indentLevel--;
            }
        }
        
        void DrawPerformanceMetrics()
        {
            EditorGUILayout.LabelField("Performance Metrics", EditorStyles.boldLabel);
            
            if (lastMetrics.fps > 0)
            {
                // Main metrics
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("FPS:", GUILayout.Width(100));
                GUI.color = GetFPSColor(lastMetrics.fps);
                EditorGUILayout.LabelField($"{lastMetrics.fps:F1}", EditorStyles.boldLabel);
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Physics Time:", GUILayout.Width(100));
                EditorGUILayout.LabelField($"{lastMetrics.physicsTimeMs:F2} ms");
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Render Time:", GUILayout.Width(100));
                EditorGUILayout.LabelField($"{lastMetrics.renderTimeMs:F2} ms");
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Memory Usage:", GUILayout.Width(100));
                GUI.color = GetMemoryColor(lastMetrics.memoryUsageMB);
                EditorGUILayout.LabelField($"{lastMetrics.memoryUsageMB:F1} MB", EditorStyles.boldLabel);
                GUI.color = Color.white;
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Rendered Instances:", GUILayout.Width(100));
                EditorGUILayout.LabelField($"{lastMetrics.renderedInstances:N0}");
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("No performance data available. Make sure simulation is running.", MessageType.Info);
            }
        }
        
        void DrawSystemControls()
        {
            EditorGUILayout.LabelField("System Controls", EditorStyles.boldLabel);
            
            if (managers.Count == 0)
            {
                EditorGUILayout.HelpBox("No balloon managers found. Use 'Setup Demo' to create a test scene.", MessageType.Warning);
                return;
            }
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset All Simulations"))
            {
                foreach (var manager in managers)
                {
                    manager.ResetSimulation();
                }
            }
            if (GUILayout.Button("Log Performance Reports"))
            {
                foreach (var manager in managers)
                {
                    manager.LogPerformanceReport();
                }
            }
            EditorGUILayout.EndHorizontal();
            
            // Feature toggles
            EditorGUILayout.LabelField("Features:", EditorStyles.miniLabel);
            EditorGUI.indentLevel++;
            
            foreach (var feature in System.Enum.GetValues(typeof(SimulationFeature)).Cast<SimulationFeature>())
            {
                EditorGUILayout.BeginHorizontal();
                string featureName = feature.ToString();
                bool currentState = systemStates.GetValueOrDefault(featureName, false);
                bool newState = EditorGUILayout.Toggle(featureName, currentState);
                
                if (newState != currentState)
                {
                    systemStates[featureName] = newState;
                    
                    foreach (var manager in managers)
                    {
                        if (manager.SupportsFeature(feature))
                        {
                            manager.TrySetFeatureEnabled(feature, newState);
                        }
                    }
                }
                
                // Show which managers support this feature
                int supportCount = managers.Count(m => m.SupportsFeature(feature));
                EditorGUILayout.LabelField($"({supportCount}/{managers.Count})", GUILayout.Width(60));
                
                EditorGUILayout.EndHorizontal();
            }
            
            EditorGUI.indentLevel--;
        }
        
        void DrawManagerControls()
        {
            EditorGUILayout.LabelField("Individual Manager Controls", EditorStyles.boldLabel);
            
            foreach (var manager in managers)
            {
                EditorGUILayout.BeginVertical("box");
                EditorGUILayout.LabelField($"{manager.GetType().Name}", EditorStyles.boldLabel);
                
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"Balloons: {manager.BalloonCount}/{manager.MaxBalloonCount}");
                EditorGUILayout.LabelField($"Memory: {manager.GetMemoryUsageMB():F1} MB");
                EditorGUILayout.EndHorizontal();
                
                // Balloon count control
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Count:", GUILayout.Width(50));
                
                if (GUILayout.Button("-1000", GUILayout.Width(50)))
                {
                    manager.TryChangeBalloonCount(Mathf.Max(100, manager.BalloonCount - 1000));
                }
                if (GUILayout.Button("-100", GUILayout.Width(50)))
                {
                    manager.TryChangeBalloonCount(Mathf.Max(100, manager.BalloonCount - 100));
                }
                if (GUILayout.Button("+100", GUILayout.Width(50)))
                {
                    manager.TryChangeBalloonCount(Mathf.Min(manager.MaxBalloonCount, manager.BalloonCount + 100));
                }
                if (GUILayout.Button("+1000", GUILayout.Width(50)))
                {
                    manager.TryChangeBalloonCount(Mathf.Min(manager.MaxBalloonCount, manager.BalloonCount + 1000));
                }
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.EndVertical();
            }
        }
        
        void DrawAdvancedOptions()
        {
            EditorGUILayout.LabelField("Advanced Options", EditorStyles.boldLabel);
            
            if (coordinator != null)
            {
                // Error statistics
                EditorGUILayout.LabelField("Error Statistics:", EditorStyles.miniLabel);
                EditorGUI.indentLevel++;
                
                foreach (var manager in managers)
                {
                    var (errorCount, lastErrorTime) = coordinator.GetErrorStats(manager.GetType());
                    if (errorCount > 0)
                    {
                        EditorGUILayout.LabelField($"{manager.GetType().Name}: {errorCount} errors, last at {lastErrorTime:F1}s");
                    }
                }
                
                EditorGUI.indentLevel--;
                
                // Memory pool statistics
                var poolStats = OptimizedMemoryPool.Instance.GetStatistics();
                EditorGUILayout.LabelField("Memory Pool:", EditorStyles.miniLabel);
                EditorGUI.indentLevel++;
                EditorGUILayout.LabelField($"Efficiency: {poolStats.PoolEfficiency:P1}");
                EditorGUILayout.LabelField($"Memory Saved: {poolStats.MemorySavedMB:F2} MB");
                EditorGUILayout.LabelField($"Rents/Returns: {poolStats.TotalRents}/{poolStats.TotalReturns}");
                EditorGUI.indentLevel--;
                
                if (GUILayout.Button("Clear Memory Pool"))
                {
                    OptimizedMemoryPool.Instance.Clear();
                }
            }
        }
        
        void DrawPerformanceGraphs()
        {
            if (fpsHistory.Count < 2)
                return;
                
            EditorGUILayout.LabelField("Performance Graphs", EditorStyles.boldLabel);
            
            // FPS Graph
            Rect fpsRect = GUILayoutUtility.GetRect(0, 60, GUILayout.ExpandWidth(true));
            DrawGraph(fpsRect, fpsHistory.ToArray(), "FPS", 0f, 120f, Color.green);
            
            // Memory Graph
            Rect memRect = GUILayoutUtility.GetRect(0, 60, GUILayout.ExpandWidth(true));
            float maxMem = memoryHistory.Count > 0 ? memoryHistory.Max() * 1.2f : 100f;
            DrawGraph(memRect, memoryHistory.ToArray(), "Memory (MB)", 0f, maxMem, Color.cyan);
        }
        
        void DrawGraph(Rect rect, float[] data, string label, float minY, float maxY, Color color)
        {
            EditorGUI.DrawRect(rect, new Color(0.1f, 0.1f, 0.1f, 0.8f));
            
            if (data.Length < 2)
                return;
                
            var oldColor = Handles.color;
            Handles.color = color;
            
            for (int i = 1; i < data.Length; i++)
            {
                float x1 = rect.x + (float)(i - 1) / (data.Length - 1) * rect.width;
                float x2 = rect.x + (float)i / (data.Length - 1) * rect.width;
                float y1 = rect.y + rect.height - (data[i - 1] - minY) / (maxY - minY) * rect.height;
                float y2 = rect.y + rect.height - (data[i] - minY) / (maxY - minY) * rect.height;
                
                Handles.DrawLine(new Vector3(x1, y1), new Vector3(x2, y2));
            }
            
            Handles.color = oldColor;
            
            // Label
            GUI.Label(new Rect(rect.x + 5, rect.y + 5, 100, 20), label);
            
            if (data.Length > 0)
            {
                GUI.Label(new Rect(rect.x + rect.width - 50, rect.y + 5, 45, 20), $"{data[data.Length - 1]:F1}");
            }
        }
        
        void CreateSystemCoordinator()
        {
            var coordinatorObj = new GameObject("SystemCoordinator");
            coordinator = coordinatorObj.AddComponent<SystemCoordinator>();
            Undo.RegisterCreatedObjectUndo(coordinatorObj, "Create System Coordinator");
        }
        
        Color GetStateColor(SimulationState state)
        {
            switch (state)
            {
                case SimulationState.Running: return Color.green;
                case SimulationState.Paused: return Color.yellow;
                case SimulationState.Error: return Color.red;
                case SimulationState.Initializing: return Color.cyan;
                default: return Color.gray;
            }
        }
        
        Color GetFPSColor(float fps)
        {
            if (fps >= 60f) return Color.green;
            if (fps >= 30f) return Color.yellow;
            return Color.red;
        }
        
        Color GetMemoryColor(float memoryMB)
        {
            if (memoryMB < 500f) return Color.green;
            if (memoryMB < 1000f) return Color.yellow;
            return Color.red;
        }
    }
}