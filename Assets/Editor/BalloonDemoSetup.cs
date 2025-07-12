using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class BalloonDemoSetup : EditorWindow
{
    [MenuItem("Tools/Balloon Physics/Setup Demo Scene")]
    public static void ShowWindow()
    {
        GetWindow<BalloonDemoSetup>("Balloon Demo Setup");
    }
    
    [MenuItem("Tools/Balloon Physics/Quick Setup")]
    public static void QuickSetup()
    {
        CreateDemoScene();
    }
    
    private int balloonCount = 100;
    private Vector3 spawnAreaSize = new Vector3(10f, 5f, 10f);
    private bool createCamera = true;
    private bool createLighting = true;
    private bool createGround = true;
    private bool createUI = true;
    
    void OnGUI()
    {
        GUILayout.Label("Balloon Physics Demo Setup", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        EditorGUILayout.LabelField("Scene Configuration", EditorStyles.boldLabel);
        balloonCount = EditorGUILayout.IntSlider("Balloon Count", balloonCount, 10, 500);
        spawnAreaSize = EditorGUILayout.Vector3Field("Spawn Area Size", spawnAreaSize);
        
        EditorGUILayout.Space();
        
        createCamera = EditorGUILayout.Toggle("Create Camera", createCamera);
        createLighting = EditorGUILayout.Toggle("Setup Lighting", createLighting);
        createGround = EditorGUILayout.Toggle("Create Ground", createGround);
        createUI = EditorGUILayout.Toggle("Create UI Controls", createUI);
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Create Demo Scene", GUILayout.Height(30)))
        {
            CreateDemoScene();
        }
        
        EditorGUILayout.Space();
        
        if (GUILayout.Button("Create Balloon Prefab Only"))
        {
            CreateBalloonPrefab();
        }
        
        if (GUILayout.Button("Setup Physics Layers"))
        {
            SetupPhysicsLayers();
        }
    }
    
    static void CreateDemoScene()
    {
        // Create main manager
        GameObject manager = CreateBalloonManager();
        
        // Create balloon prefab
        GameObject balloonPrefab = CreateBalloonPrefab();
        
        // Setup spawner
        BalloonSpawner spawner = manager.GetComponentInChildren<BalloonSpawner>();
        if (spawner != null)
        {
            SerializedObject spawnerSO = new SerializedObject(spawner);
            spawnerSO.FindProperty("balloonPrefab").objectReferenceValue = balloonPrefab;
            spawnerSO.FindProperty("balloonCount").intValue = 100;
            spawnerSO.ApplyModifiedProperties();
        }
        
        // Create camera
        CreateDemoCamera();
        
        // Create ground
        CreateGround();
        
        // Setup lighting
        SetupLighting();
        
        // Create UI
        CreateDemoUI();
        
        // Setup physics layers
        SetupPhysicsLayers();
        
        Debug.Log("Balloon Physics Demo Scene created successfully!");
        EditorUtility.DisplayDialog("Success", "Balloon Physics Demo Scene has been created!\n\nPress Play to start the simulation.\nUse R to restart, P for performance report, F1 for detailed stats.", "OK");
    }
    
    static GameObject CreateBalloonManager()
    {
        GameObject manager = new GameObject("BalloonManager");
        manager.AddComponent<BalloonManager>();
        
        // Create child objects for each system
        GameObject physicsOptimizer = new GameObject("PhysicsOptimizer");
        physicsOptimizer.transform.SetParent(manager.transform);
        physicsOptimizer.AddComponent<PhysicsOptimizer>();
        
        GameObject spawner = new GameObject("BalloonSpawner");
        spawner.transform.SetParent(manager.transform);
        spawner.AddComponent<BalloonSpawner>();
        
        GameObject lodSystem = new GameObject("BalloonLODSystem");
        lodSystem.transform.SetParent(manager.transform);
        lodSystem.AddComponent<BalloonLODSystem>();
        
        GameObject windSystem = new GameObject("WindSystem");
        windSystem.transform.SetParent(manager.transform);
        windSystem.AddComponent<WindSystem>();
        
        GameObject profiler = new GameObject("PerformanceProfiler");
        profiler.transform.SetParent(manager.transform);
        profiler.AddComponent<PerformanceProfiler>();
        
        return manager;
    }
    
    static GameObject CreateBalloonPrefab()
    {
        // Create balloon prefab
        GameObject balloon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        balloon.name = "BalloonPrefab";
        balloon.transform.localScale = Vector3.one * 0.5f;
        
        // Add physics components
        balloon.AddComponent<BalloonController>();
        balloon.AddComponent<BalloonPhysics>();
        
        // Create rope
        GameObject rope = new GameObject("Rope");
        rope.transform.SetParent(balloon.transform);
        rope.transform.localPosition = Vector3.down * 0.25f;
        VerletRope verletRope = rope.AddComponent<VerletRope>();
        
        // Setup LineRenderer for rope
        LineRenderer lineRenderer = rope.AddComponent<LineRenderer>();
        Material ropeMaterial = new Material(Shader.Find("Sprites/Default"));
        ropeMaterial.color = new Color(0.6f, 0.4f, 0.2f);
        lineRenderer.material = ropeMaterial;
        lineRenderer.startWidth = 0.02f;
        lineRenderer.endWidth = 0.02f;
        lineRenderer.positionCount = 20;
        
        // Set balloon layer
        balloon.layer = LayerMask.NameToLayer("Default"); // Will be set to Balloon layer if it exists
        
        // Create prefab asset
        string prefabPath = "Assets/Prefabs/";
        if (!AssetDatabase.IsValidFolder(prefabPath))
        {
            AssetDatabase.CreateFolder("Assets", "Prefabs");
        }
        
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(balloon, prefabPath + "BalloonPrefab.prefab");
        DestroyImmediate(balloon);
        
        return prefab;
    }
    
    static void CreateDemoCamera()
    {
        // Check if main camera exists
        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            GameObject cameraObj = new GameObject("Main Camera");
            cameraObj.tag = "MainCamera";
            Camera camera = cameraObj.AddComponent<Camera>();
            cameraObj.AddComponent<AudioListener>();
            
            // Position camera
            cameraObj.transform.position = new Vector3(0, 5, -10);
            cameraObj.transform.rotation = Quaternion.LookRotation(Vector3.forward + Vector3.down * 0.3f);
            
            // Add camera controller
            cameraObj.AddComponent<SimpleCameraController>();
        }
    }
    
    static void CreateGround()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
        ground.name = "Ground";
        ground.transform.position = Vector3.down * 2f;
        ground.transform.localScale = Vector3.one * 5f;
        
        // Create ground material
        Material groundMaterial = new Material(Shader.Find("Standard"));
        groundMaterial.color = new Color(0.3f, 0.7f, 0.3f);
        ground.GetComponent<Renderer>().material = groundMaterial;
    }
    
    static void SetupLighting()
    {
        // Create directional light
        GameObject lightObj = new GameObject("Directional Light");
        Light light = lightObj.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1f;
        lightObj.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        
        // Set ambient lighting
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.5f, 0.7f, 1f);
        RenderSettings.ambientEquatorColor = new Color(0.4f, 0.4f, 0.4f);
        RenderSettings.ambientGroundColor = new Color(0.2f, 0.2f, 0.2f);
    }
    
    static void CreateDemoUI()
    {
        // Create UI Canvas
        GameObject canvasObj = new GameObject("DemoUI");
        Canvas canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvasObj.AddComponent<CanvasScaler>();
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Create EventSystem
        GameObject eventSystemObj = new GameObject("EventSystem");
        eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
        eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        
        // Create control panel
        GameObject panelObj = new GameObject("ControlPanel");
        panelObj.transform.SetParent(canvasObj.transform, false);
        
        UnityEngine.UI.Image panel = panelObj.AddComponent<UnityEngine.UI.Image>();
        panel.color = new Color(0, 0, 0, 0.7f);
        
        RectTransform panelRect = panelObj.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0, 1);
        panelRect.anchorMax = new Vector2(0, 1);
        panelRect.pivot = new Vector2(0, 1);
        panelRect.sizeDelta = new Vector2(250, 150);
        panelRect.anchoredPosition = new Vector2(10, -10);
        
        // Add demo UI controller
        panelObj.AddComponent<BalloonDemoUI>();
    }
    
    static void SetupPhysicsLayers()
    {
        // This would need to be done manually in Project Settings
        // or through SerializedObject manipulation of TagManager
        Debug.Log("Please manually create 'Balloon' layer in Project Settings > Tags and Layers");
        Debug.Log("Then configure collision matrix: Balloon layer should collide with Default and Balloon layers");
    }
}

// Simple camera controller for demo
public class SimpleCameraController : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float rotateSpeed = 2f;
    
    void Update()
    {
        // WASD movement
        Vector3 move = Vector3.zero;
        if (Input.GetKey(KeyCode.W)) move += transform.forward;
        if (Input.GetKey(KeyCode.S)) move -= transform.forward;
        if (Input.GetKey(KeyCode.A)) move -= transform.right;
        if (Input.GetKey(KeyCode.D)) move += transform.right;
        if (Input.GetKey(KeyCode.Q)) move -= transform.up;
        if (Input.GetKey(KeyCode.E)) move += transform.up;
        
        transform.position += move * moveSpeed * Time.deltaTime;
        
        // Mouse look
        if (Input.GetMouseButton(1))
        {
            float mouseX = Input.GetAxis("Mouse X") * rotateSpeed;
            float mouseY = Input.GetAxis("Mouse Y") * rotateSpeed;
            
            transform.Rotate(-mouseY, mouseX, 0);
        }
    }
}

// Simple UI controller for demo
public class BalloonDemoUI : MonoBehaviour
{
    void Start()
    {
        CreateUIElements();
    }
    
    void CreateUIElements()
    {
        // Create instruction text
        GameObject textObj = new GameObject("Instructions");
        textObj.transform.SetParent(transform, false);
        
        UnityEngine.UI.Text text = textObj.AddComponent<UnityEngine.UI.Text>();
        text.text = "BALLOON PHYSICS DEMO\n\nControls:\nR - Restart simulation\nP - Performance report\nF1 - Toggle detailed stats\n\nCamera:\nWASD - Move\nRight Click + Mouse - Look\nQE - Up/Down";
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 12;
        text.color = Color.white;
        
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(10, 10);
        textRect.offsetMax = new Vector2(-10, -10);
    }
}