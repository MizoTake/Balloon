using UnityEngine;
using UnityEditor;
using BalloonSimulation.JobSystem;

namespace BalloonSimulation.Editor
{
    /// <summary>
    /// Editor tools for Enhanced Balloon Simulation
    /// </summary>
    public class EnhancedBalloonSimulationEditor : EditorWindow
    {
        private EnhancedBalloonManager balloonManager;
        private PerformanceTestFramework testFramework;
        
        // Scene setup parameters
        private int initialBalloonCount = 10000;
        private Material balloonMaterial;
        private Mesh balloonMesh;
        private ComputeShader physicsCompute;
        
        [MenuItem("Tools/Balloon Physics/Enhanced Simulation Window")]
        public static void ShowWindow()
        {
            GetWindow<EnhancedBalloonSimulationEditor>("Enhanced Balloon Simulation");
        }
        
        [MenuItem("Tools/Balloon Physics/Quick Setup Enhanced Scene")]
        public static void QuickSetupEnhancedScene()
        {
            CreateEnhancedDemoScene();
        }
        
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Enhanced Balloon Simulation", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // Find existing components
            if (GUILayout.Button("Find Existing Components"))
            {
                FindComponents();
            }
            
            EditorGUILayout.Space();
            
            // Scene setup section
            EditorGUILayout.LabelField("Scene Setup", EditorStyles.boldLabel);
            
            initialBalloonCount = EditorGUILayout.IntSlider("Initial Balloon Count", initialBalloonCount, 100, 50000);
            balloonMaterial = EditorGUILayout.ObjectField("Balloon Material", balloonMaterial, typeof(Material), false) as Material;
            balloonMesh = EditorGUILayout.ObjectField("Balloon Mesh", balloonMesh, typeof(Mesh), false) as Mesh;
            physicsCompute = EditorGUILayout.ObjectField("Physics Compute Shader", physicsCompute, typeof(ComputeShader), false) as ComputeShader;
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Create Enhanced Demo Scene"))
            {
                CreateEnhancedDemoScene();
            }
            
            EditorGUILayout.Space();
            
            // Manager controls
            if (balloonManager != null)
            {
                EditorGUILayout.LabelField("Manager Controls", EditorStyles.boldLabel);
                
                EditorGUILayout.LabelField($"Current Balloons: {balloonManager.BalloonCount}");
                
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("1K")) balloonManager.ChangeBalloonCount(1000);
                if (GUILayout.Button("5K")) balloonManager.ChangeBalloonCount(5000);
                if (GUILayout.Button("10K")) balloonManager.ChangeBalloonCount(10000);
                if (GUILayout.Button("25K")) balloonManager.ChangeBalloonCount(25000);
                if (GUILayout.Button("50K")) balloonManager.ChangeBalloonCount(50000);
                EditorGUILayout.EndHorizontal();
                
                EditorGUILayout.Space();
                
                if (GUILayout.Button("Reset Simulation"))
                {
                    balloonManager.SendMessage("ResetSimulation");
                }
            }
            
            EditorGUILayout.Space();
            
            // Performance testing
            if (testFramework != null)
            {
                EditorGUILayout.LabelField("Performance Testing", EditorStyles.boldLabel);
                
                if (GUILayout.Button("Run Quick Performance Test"))
                {
                    testFramework.StartCoroutine(testFramework.RunQuickPerformanceCheck());
                }
                
                if (GUILayout.Button("Run Full Test Suite"))
                {
                    testFramework.StartCoroutine(testFramework.RunFullTestSuite());
                }
                
                if (GUILayout.Button("Run Scalability Test"))
                {
                    testFramework.StartCoroutine(testFramework.RunScalabilityTest());
                }
                
                if (GUILayout.Button("Generate Performance Report"))
                {
                    testFramework.GeneratePerformanceReport();
                }
                
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("Test Status: " + (testFramework.IsTestRunning ? "RUNNING" : "IDLE"), 
                                       testFramework.IsTestRunning ? MessageType.Info : MessageType.None);
            }
            
            EditorGUILayout.Space();
            
            // System information
            EditorGUILayout.LabelField("System Information", EditorStyles.boldLabel);
            EditorGUILayout.LabelField($"Unity Version: {Application.unityVersion}");
            EditorGUILayout.LabelField($"Platform: {Application.platform}");
            EditorGUILayout.LabelField($"Graphics Device: {SystemInfo.graphicsDeviceName}");
            EditorGUILayout.LabelField($"System Memory: {SystemInfo.systemMemorySize}MB");
            EditorGUILayout.LabelField($"Graphics Memory: {SystemInfo.graphicsMemorySize}MB");
            
            EditorGUILayout.Space();
            
            // Helpful tips
            EditorGUILayout.HelpBox(
                "Runtime Controls:\n" +
                "R - Reset Simulation\n" +
                "P - Performance Report\n" +
                "F - Toggle Fluid Dynamics\n" +
                "S - Toggle Swarm Physics\n" +
                "W - Toggle Wind\n" +
                "+/- - Adjust Count\n" +
                "F1 - Debug Info\n" +
                "F5 - Full Test Suite\n" +
                "F6 - Quick Test", 
                MessageType.Info);
        }
        
        private void FindComponents()
        {
            balloonManager = FindObjectOfType<EnhancedBalloonManager>();
            testFramework = FindObjectOfType<PerformanceTestFramework>();
            
            if (balloonManager == null)
            {
                Debug.Log("[Enhanced Editor] EnhancedBalloonManager not found in scene");
            }
            else
            {
                Debug.Log("[Enhanced Editor] Found EnhancedBalloonManager");
            }
            
            if (testFramework == null)
            {
                Debug.Log("[Enhanced Editor] PerformanceTestFramework not found in scene");
            }
            else
            {
                Debug.Log("[Enhanced Editor] Found PerformanceTestFramework");
            }
        }
        
        private static void CreateEnhancedDemoScene()
        {
            Debug.Log("[Enhanced Editor] Creating enhanced demo scene...");
            
            // Create main simulation object
            GameObject simObject = new GameObject("EnhancedBalloonSimulation");
            var manager = simObject.AddComponent<EnhancedBalloonManager>();
            
            // Create performance test framework
            GameObject testObject = new GameObject("PerformanceTestFramework");
            var testFramework = testObject.AddComponent<PerformanceTestFramework>();
            
            // Link test framework to manager via reflection
            var balloonManagerField = typeof(PerformanceTestFramework).GetField("balloonManager", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            balloonManagerField?.SetValue(testFramework, manager);
            
            // Create demo environment
            CreateDemoEnvironment();
            
            // Setup camera
            SetupDemoCamera();
            
            // Setup lighting
            SetupDemoLighting();
            
            // Load default assets
            LoadDefaultAssets(manager);
            
            // Select the main simulation object
            Selection.activeGameObject = simObject;
            
            Debug.Log("[Enhanced Editor] Enhanced demo scene created successfully!");
            Debug.Log("[Enhanced Editor] Use F5-F8 keys for performance testing during play mode");
        }
        
        private static void CreateDemoEnvironment()
        {
            // Create world bounds visualization
            GameObject bounds = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bounds.name = "WorldBounds";
            bounds.transform.position = Vector3.zero;
            bounds.transform.localScale = new Vector3(100f, 50f, 100f);
            
            // Make it wireframe
            var renderer = bounds.GetComponent<Renderer>();
            var material = new Material(Shader.Find("Standard"));
            material.SetFloat("_Mode", 1f); // Transparent
            material.color = new Color(0.5f, 0.5f, 1f, 0.1f);
            renderer.material = material;
            
            // Remove collider so it doesn't interfere
            DestroyImmediate(bounds.GetComponent<Collider>());
            
            // Create some obstacles
            CreateObstacle("Obstacle1", new Vector3(-20f, 0f, 0f), new Vector3(5f, 10f, 5f));
            CreateObstacle("Obstacle2", new Vector3(20f, 0f, 0f), new Vector3(8f, 15f, 3f));
            CreateObstacle("Obstacle3", new Vector3(0f, -20f, 0f), new Vector3(30f, 2f, 30f));
        }
        
        private static void CreateObstacle(string name, Vector3 position, Vector3 scale)
        {
            GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.name = name;
            obstacle.transform.position = position;
            obstacle.transform.localScale = scale;
            
            var material = new Material(Shader.Find("Standard"));
            material.color = new Color(0.7f, 0.3f, 0.2f);
            obstacle.GetComponent<Renderer>().material = material;
        }
        
        private static void SetupDemoCamera()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                GameObject cameraObj = new GameObject("Main Camera");
                mainCamera = cameraObj.AddComponent<Camera>();
                cameraObj.tag = "MainCamera";
            }
            
            mainCamera.transform.position = new Vector3(0f, 20f, -50f);
            mainCamera.transform.LookAt(Vector3.zero);
            mainCamera.farClipPlane = 500f;
            
            // Add camera controller
            if (mainCamera.GetComponent<CameraController>() == null)
            {
                var controller = mainCamera.gameObject.AddComponent<CameraController>();
            }
        }
        
        private static void SetupDemoLighting()
        {
            // Ensure we have a directional light
            Light[] lights = FindObjectsOfType<Light>();
            Light directionalLight = null;
            
            foreach (var light in lights)
            {
                if (light.type == LightType.Directional)
                {
                    directionalLight = light;
                    break;
                }
            }
            
            if (directionalLight == null)
            {
                GameObject lightObj = new GameObject("Directional Light");
                directionalLight = lightObj.AddComponent<Light>();
                directionalLight.type = LightType.Directional;
            }
            
            directionalLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            directionalLight.intensity = 1.2f;
            directionalLight.shadows = LightShadows.Soft;
        }
        
        private static void LoadDefaultAssets(EnhancedBalloonManager manager)
        {
            // Try to find default balloon assets
            Material balloonMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/BalloonMaterial.mat");
            if (balloonMat == null)
            {
                // Create a basic material
                balloonMat = new Material(Shader.Find("Standard"));
                balloonMat.name = "BalloonMaterial";
                balloonMat.SetFloat("_Metallic", 0.1f);
                balloonMat.SetFloat("_Smoothness", 0.8f);
                balloonMat.color = new Color(1f, 0.5f, 0.3f, 0.8f);
                
                AssetDatabase.CreateAsset(balloonMat, "Assets/BalloonMaterial.mat");
            }
            
            // Try to find compute shader
            ComputeShader physicsCompute = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Shaders/BalloonPhysicsCompute.compute");
            
            // Set assets via reflection
            var materialField = typeof(EnhancedBalloonManager).GetField("balloonMaterial", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            materialField?.SetValue(manager, balloonMat);
            
            var computeField = typeof(EnhancedBalloonManager).GetField("physicsComputeShader", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            computeField?.SetValue(manager, physicsCompute);
            
            Debug.Log("[Enhanced Editor] Default assets loaded");
        }
    }
    
    /// <summary>
    /// Simple camera controller for demo scenes
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        public float moveSpeed = 10f;
        public float lookSpeed = 2f;
        public float zoomSpeed = 5f;
        
        private Vector3 lastMousePosition;
        
        void Update()
        {
            HandleMovement();
            HandleLook();
            HandleZoom();
        }
        
        void HandleMovement()
        {
            Vector3 movement = Vector3.zero;
            
            if (Input.GetKey(KeyCode.W)) movement += transform.forward;
            if (Input.GetKey(KeyCode.S)) movement -= transform.forward;
            if (Input.GetKey(KeyCode.A)) movement -= transform.right;
            if (Input.GetKey(KeyCode.D)) movement += transform.right;
            if (Input.GetKey(KeyCode.Q)) movement += Vector3.up;
            if (Input.GetKey(KeyCode.E)) movement -= Vector3.up;
            
            transform.position += movement * moveSpeed * Time.deltaTime;
        }
        
        void HandleLook()
        {
            if (Input.GetMouseButtonDown(1))
                lastMousePosition = Input.mousePosition;
            
            if (Input.GetMouseButton(1))
            {
                Vector3 delta = Input.mousePosition - lastMousePosition;
                transform.Rotate(-delta.y * lookSpeed, delta.x * lookSpeed, 0);
                lastMousePosition = Input.mousePosition;
            }
        }
        
        void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            transform.position += transform.forward * scroll * zoomSpeed;
        }
    }
}