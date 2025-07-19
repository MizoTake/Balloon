using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// Performance testing framework for validating 50K balloon target
    /// Implements .kiro task 12.2 - Performance test suite
    /// </summary>
    public class PerformanceTestFramework : MonoBehaviour
    {
        [Header("Test Configuration")]
        [SerializeField] private EnhancedBalloonManager balloonManager;
        [SerializeField] private bool runTestsOnStart = false;
        [SerializeField] private bool enableAutoTesting = true;
        [SerializeField] private float testInterval = 30f; // seconds
        
        [Header("Test Targets")]
        [SerializeField] private int[] testBalloonCounts = { 1000, 5000, 10000, 25000, 50000 };
        [SerializeField] private float targetFPS = 60f;
        [SerializeField] private float minimumFPS = 30f;
        [SerializeField] private float maxAcceptableFrameTime = 16.67f; // ms for 60fps
        
        [Header("Memory Limits")]
        [SerializeField] private float maxMemoryUsageMB = 500f;
        [SerializeField] private float maxGPUMemoryMB = 1000f;
        
        // Test results storage
        private List<PerformanceTestResult> testResults = new List<PerformanceTestResult>();
        private Stopwatch frameTimer = new Stopwatch();
        private Queue<float> frameTimeHistory = new Queue<float>();
        private const int FRAME_HISTORY_SIZE = 300; // 5 seconds at 60fps
        
        // Current test state
        private bool isTestRunning = false;
        private int currentTestIndex = 0;
        private float testStartTime;
        private float lastTestTime;
        
        // Performance monitoring
        private float averageFrameTime;
        private float minFrameTime;
        private float maxFrameTime;
        private int droppedFrames;
        private float memoryUsageAtTest;
        
        public bool IsTestRunning => isTestRunning;
        public List<PerformanceTestResult> TestResults => testResults;
        
        private void Start()
        {
            if (balloonManager == null)
                balloonManager = FindObjectOfType<EnhancedBalloonManager>();
                
            if (runTestsOnStart)
            {
                StartCoroutine(RunFullTestSuite());
            }
            
            lastTestTime = Time.time;
        }
        
        private void Update()
        {
            // Track frame times
            TrackFramePerformance();
            
            // Auto testing
            if (enableAutoTesting && !isTestRunning && Time.time - lastTestTime > testInterval)
            {
                StartCoroutine(RunQuickPerformanceCheck());
                lastTestTime = Time.time;
            }
            
            // Handle manual test triggers
            HandleTestInputs();
        }
        
        private void TrackFramePerformance()
        {
            float currentFrameTime = Time.unscaledDeltaTime * 1000f; // Convert to milliseconds
            
            frameTimeHistory.Enqueue(currentFrameTime);
            if (frameTimeHistory.Count > FRAME_HISTORY_SIZE)
                frameTimeHistory.Dequeue();
            
            // Track dropped frames
            if (currentFrameTime > maxAcceptableFrameTime)
                droppedFrames++;
        }
        
        private void HandleTestInputs()
        {
            if (Input.GetKeyDown(KeyCode.F5))
            {
                StartCoroutine(RunFullTestSuite());
            }
            
            if (Input.GetKeyDown(KeyCode.F6))
            {
                StartCoroutine(RunQuickPerformanceCheck());
            }
            
            if (Input.GetKeyDown(KeyCode.F7))
            {
                GeneratePerformanceReport();
            }
            
            if (Input.GetKeyDown(KeyCode.F8))
            {
                StartCoroutine(RunScalabilityTest());
            }
        }
        
        /// <summary>
        /// Runs the complete performance test suite
        /// </summary>
        public IEnumerator RunFullTestSuite()
        {
            if (isTestRunning)
            {
                Debug.LogWarning("[PerformanceTestFramework] Test already running");
                yield break;
            }
            
            Debug.Log("=== STARTING FULL PERFORMANCE TEST SUITE ===");
            isTestRunning = true;
            testResults.Clear();
            
            for (int i = 0; i < testBalloonCounts.Length; i++)
            {
                currentTestIndex = i;
                int balloonCount = testBalloonCounts[i];
                
                Debug.Log($"[Test {i + 1}/{testBalloonCounts.Length}] Testing {balloonCount} balloons...");
                
                PerformanceTestResult result = null;
                yield return StartCoroutine(RunSinglePerformanceTestWithCallback(balloonCount, (testResult) => {
                    result = testResult;
                }));
                if (result != null) testResults.Add(result);
                
                // Short break between tests
                yield return new WaitForSeconds(2f);
            }
            
            isTestRunning = false;
            GenerateFullTestReport();
            
            Debug.Log("=== PERFORMANCE TEST SUITE COMPLETED ===");
        }
        
        /// <summary>
        /// Runs a quick performance check with current settings
        /// </summary>
        public IEnumerator RunQuickPerformanceCheck()
        {
            if (isTestRunning) yield break;
            
            isTestRunning = true;
            int currentBalloons = balloonManager.BalloonCount;
            
            PerformanceTestResult result = null;
            yield return StartCoroutine(RunSinglePerformanceTestWithCallback(currentBalloons, (testResult) => {
                result = testResult;
            }, 5f));
            
            Debug.Log($"[Quick Test] {currentBalloons} balloons: {result.averageFPS:F1}fps, {result.memoryUsageMB:F1}MB");
            
            if (result.averageFPS < minimumFPS)
            {
                Debug.LogWarning($"[Performance Warning] FPS ({result.averageFPS:F1}) below minimum ({minimumFPS})");
            }
            
            isTestRunning = false;
        }
        
        /// <summary>
        /// Runs scalability test to find maximum balloon count for target FPS
        /// </summary>
        public IEnumerator RunScalabilityTest()
        {
            if (isTestRunning) yield break;
            
            Debug.Log("=== STARTING SCALABILITY TEST ===");
            isTestRunning = true;
            
            int minCount = 1000;
            int maxCount = 50000;
            int step = 2000;
            int optimalCount = minCount;
            
            for (int count = minCount; count <= maxCount; count += step)
            {
                Debug.Log($"[Scalability Test] Testing {count} balloons...");
                
                PerformanceTestResult result = null;
                yield return StartCoroutine(RunSinglePerformanceTestWithCallback(count, (testResult) => {
                    result = testResult;
                }, 8f));
                
                if (result.averageFPS >= targetFPS && result.memoryUsageMB <= maxMemoryUsageMB)
                {
                    optimalCount = count;
                    Debug.Log($"[Scalability] {count} balloons: PASS ({result.averageFPS:F1}fps, {result.memoryUsageMB:F1}MB)");
                }
                else
                {
                    Debug.Log($"[Scalability] {count} balloons: FAIL ({result.averageFPS:F1}fps, {result.memoryUsageMB:F1}MB)");
                    break;
                }
                
                yield return new WaitForSeconds(1f);
            }
            
            Debug.Log($"=== SCALABILITY TEST COMPLETE: Optimal count = {optimalCount} balloons ===");
            isTestRunning = false;
        }
        
        /// <summary>
        /// Runs a single performance test with callback for result
        /// </summary>
        private IEnumerator RunSinglePerformanceTestWithCallback(int balloonCount, System.Action<PerformanceTestResult> callback, float testDuration = 10f)
        {
            var result = new PerformanceTestResult();
            yield return StartCoroutine(RunSinglePerformanceTestInternal(balloonCount, testDuration, result));
            callback?.Invoke(result);
        }
        
        /// <summary>
        /// Runs a single performance test for specified balloon count
        /// </summary>
        private IEnumerator RunSinglePerformanceTest(int balloonCount, float testDuration = 10f)
        {
            var result = new PerformanceTestResult();
            yield return StartCoroutine(RunSinglePerformanceTestInternal(balloonCount, testDuration, result));
        }
        
        /// <summary>
        /// Internal implementation of performance test
        /// </summary>
        private IEnumerator RunSinglePerformanceTestInternal(int balloonCount, float testDuration, PerformanceTestResult result)
        {
            result.balloonCount = balloonCount;
            result.testDuration = testDuration;
            result.timestamp = System.DateTime.Now;
            
            // Setup test
            balloonManager.ChangeBalloonCount(balloonCount);
            yield return new WaitForSeconds(2f); // Stabilization time
            
            // Reset measurements
            frameTimeHistory.Clear();
            droppedFrames = 0;
            testStartTime = Time.time;
            
            var frameRates = new List<float>();
            var frameTimes = new List<float>();
            var memoryReadings = new List<float>();
            
            Stopwatch testTimer = Stopwatch.StartNew();
            
            // Collect data during test
            while (testTimer.Elapsed.TotalSeconds < testDuration)
            {
                float currentFPS = 1f / Time.unscaledDeltaTime;
                float currentFrameTime = Time.unscaledDeltaTime * 1000f;
                float currentMemory = GC.GetTotalMemory(false) / (1024f * 1024f);
                
                frameRates.Add(currentFPS);
                frameTimes.Add(currentFrameTime);
                memoryReadings.Add(currentMemory);
                
                yield return null; // Wait one frame
            }
            
            testTimer.Stop();
            
            // Calculate results
            result.averageFPS = CalculateAverage(frameRates);
            result.minFPS = CalculateMin(frameRates);
            result.maxFPS = CalculateMax(frameRates);
            result.averageFrameTime = CalculateAverage(frameTimes);
            result.maxFrameTime = CalculateMax(frameTimes);
            result.frameDrops = droppedFrames;
            result.memoryUsageMB = CalculateAverage(memoryReadings);
            result.maxMemoryMB = CalculateMax(memoryReadings);
            
            // Performance analysis
            result.targetFPSAchieved = result.averageFPS >= targetFPS;
            result.memoryWithinLimits = result.memoryUsageMB <= maxMemoryUsageMB;
            result.stableFrameRate = (result.maxFPS - result.minFPS) < 20f;
            result.overallPass = result.targetFPSAchieved && result.memoryWithinLimits && result.stableFrameRate;
            
            // GPU performance estimation
            result.estimatedGPUUsage = EstimateGPUUsage(balloonCount, result.averageFrameTime);
            
            Debug.Log($"[Test Result] {balloonCount} balloons: {result.averageFPS:F1}fps ({result.averageFrameTime:F2}ms), " +
                     $"Memory: {result.memoryUsageMB:F1}MB, Pass: {result.overallPass}");
        }
        
        private float EstimateGPUUsage(int balloonCount, float frameTime)
        {
            // Rough estimation based on instance count and frame time
            float baselineFrameTime = 16.67f; // 60fps baseline
            float usageRatio = frameTime / baselineFrameTime;
            float instanceFactor = balloonCount / 10000f; // Normalized to 10K balloons
            
            return Mathf.Clamp01(usageRatio * instanceFactor) * 100f;
        }
        
        private float CalculateAverage(List<float> values)
        {
            if (values.Count == 0) return 0f;
            float sum = 0f;
            foreach (float value in values)
                sum += value;
            return sum / values.Count;
        }
        
        private float CalculateMin(List<float> values)
        {
            if (values.Count == 0) return 0f;
            float min = float.MaxValue;
            foreach (float value in values)
                if (value < min) min = value;
            return min;
        }
        
        private float CalculateMax(List<float> values)
        {
            if (values.Count == 0) return 0f;
            float max = float.MinValue;
            foreach (float value in values)
                if (value > max) max = value;
            return max;
        }
        
        /// <summary>
        /// Generates a comprehensive performance report
        /// </summary>
        public void GeneratePerformanceReport()
        {
            if (testResults.Count == 0)
            {
                Debug.LogWarning("[PerformanceTestFramework] No test results to report");
                return;
            }
            
            Debug.Log("=== PERFORMANCE TEST REPORT ===");
            
            foreach (var result in testResults)
            {
                Debug.Log($"[{result.balloonCount} balloons] " +
                         $"FPS: {result.averageFPS:F1} (min: {result.minFPS:F1}, max: {result.maxFPS:F1}) " +
                         $"Frame: {result.averageFrameTime:F2}ms (max: {result.maxFrameTime:F2}ms) " +
                         $"Memory: {result.memoryUsageMB:F1}MB " +
                         $"Drops: {result.frameDrops} " +
                         $"GPU: {result.estimatedGPUUsage:F1}% " +
                         $"Status: {(result.overallPass ? "PASS" : "FAIL")}");
            }
            
            // Find optimal configuration
            var bestResult = FindOptimalConfiguration();
            if (bestResult != null)
            {
                Debug.Log($"[OPTIMAL CONFIG] {bestResult.balloonCount} balloons with {bestResult.averageFPS:F1}fps");
            }
            
            // Performance warnings
            CheckPerformanceWarnings();
            
            Debug.Log("================================");
        }
        
        private void GenerateFullTestReport()
        {
            GeneratePerformanceReport();
            
            // Generate CSV data for external analysis
            string csvData = GenerateCSVReport();
            Debug.Log("[CSV Report]\n" + csvData);
            
            // Summary statistics
            Debug.Log("=== SUMMARY STATISTICS ===");
            Debug.Log($"Tests Run: {testResults.Count}");
            Debug.Log($"Highest Balloon Count Tested: {testResults[testResults.Count - 1].balloonCount}");
            Debug.Log($"50K Balloon Target: {(testResults.Count > 0 && testResults[testResults.Count - 1].balloonCount >= 50000 ? "TESTED" : "NOT REACHED")}");
        }
        
        private PerformanceTestResult FindOptimalConfiguration()
        {
            PerformanceTestResult bestResult = null;
            int highestPassingCount = 0;
            
            foreach (var result in testResults)
            {
                if (result.overallPass && result.balloonCount > highestPassingCount)
                {
                    highestPassingCount = result.balloonCount;
                    bestResult = result;
                }
            }
            
            return bestResult;
        }
        
        private void CheckPerformanceWarnings()
        {
            foreach (var result in testResults)
            {
                if (!result.targetFPSAchieved)
                {
                    Debug.LogWarning($"[WARNING] {result.balloonCount} balloons: FPS below target ({result.averageFPS:F1} < {targetFPS})");
                }
                
                if (!result.memoryWithinLimits)
                {
                    Debug.LogWarning($"[WARNING] {result.balloonCount} balloons: Memory usage high ({result.memoryUsageMB:F1}MB > {maxMemoryUsageMB}MB)");
                }
                
                if (!result.stableFrameRate)
                {
                    Debug.LogWarning($"[WARNING] {result.balloonCount} balloons: Unstable frame rate (range: {result.minFPS:F1}-{result.maxFPS:F1}fps)");
                }
            }
        }
        
        private string GenerateCSVReport()
        {
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("BalloonCount,AverageFPS,MinFPS,MaxFPS,AverageFrameTime,MaxFrameTime,MemoryMB,FrameDrops,GPUUsage,Pass");
            
            foreach (var result in testResults)
            {
                csv.AppendLine($"{result.balloonCount},{result.averageFPS:F2},{result.minFPS:F2},{result.maxFPS:F2}," +
                              $"{result.averageFrameTime:F2},{result.maxFrameTime:F2},{result.memoryUsageMB:F2}," +
                              $"{result.frameDrops},{result.estimatedGPUUsage:F2},{result.overallPass}");
            }
            
            return csv.ToString();
        }
        
        private void OnGUI()
        {
            if (!isTestRunning && testResults.Count == 0) return;
            
            GUILayout.BeginArea(new Rect(Screen.width - 300, 10, 290, 200));
            GUILayout.BeginVertical("box");
            
            var boldStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            GUILayout.Label("PERFORMANCE TEST", boldStyle);
            
            if (isTestRunning)
            {
                GUILayout.Label($"Running Test {currentTestIndex + 1}/{testBalloonCounts.Length}");
                GUILayout.Label($"Current: {testBalloonCounts[currentTestIndex]} balloons");
                GUILayout.Label($"Time: {Time.time - testStartTime:F1}s");
            }
            else
            {
                GUILayout.Label($"Tests Complete: {testResults.Count}");
                if (testResults.Count > 0)
                {
                    var latest = testResults[testResults.Count - 1];
                    GUILayout.Label($"Latest: {latest.balloonCount} balloons");
                    GUILayout.Label($"FPS: {latest.averageFPS:F1}");
                    GUILayout.Label($"Status: {(latest.overallPass ? "PASS" : "FAIL")}");
                }
            }
            
            GUILayout.Label("\nControls:");
            GUILayout.Label("F5 - Full Test Suite");
            GUILayout.Label("F6 - Quick Test");
            GUILayout.Label("F7 - Generate Report");
            GUILayout.Label("F8 - Scalability Test");
            
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }
    }
    
    /// <summary>
    /// Stores results from a single performance test
    /// </summary>
    [System.Serializable]
    public class PerformanceTestResult
    {
        public int balloonCount;
        public float testDuration;
        public System.DateTime timestamp;
        
        // FPS metrics
        public float averageFPS;
        public float minFPS;
        public float maxFPS;
        
        // Frame time metrics
        public float averageFrameTime;
        public float maxFrameTime;
        public int frameDrops;
        
        // Memory metrics
        public float memoryUsageMB;
        public float maxMemoryMB;
        
        // GPU metrics
        public float estimatedGPUUsage;
        
        // Pass/fail criteria
        public bool targetFPSAchieved;
        public bool memoryWithinLimits;
        public bool stableFrameRate;
        public bool overallPass;
    }
}