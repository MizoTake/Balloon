using UnityEngine;
using UnityEditor;
using BalloonSimulation.JobSystem;

namespace BalloonSimulation.Editor
{
    /// <summary>
    /// Editor tools for quickly setting up balloon simulation demo scenes
    /// </summary>
    public class BalloonSimulationEditorTools : EditorWindow
    {
        private int balloonCount = 1000;
        private float worldSize = 100f;
        private bool includeSkybox = true;
        private bool includePostProcessing = false;
        private bool includeWindZones = true;
        
        [MenuItem("Tools/Balloon Simulation/Setup Demo Scene")]
        public static void ShowWindow()
        {
            var window = GetWindow<BalloonSimulationEditorTools>("Balloon Demo Setup");
            window.minSize = new Vector2(400, 500);
        }
        
        [MenuItem("Tools/Balloon Simulation/Quick Setup (Default)")]
        public static void QuickSetup()
        {
            SetupDemoScene(1000, 100f, true, false, true);
            Debug.Log("Balloon simulation demo scene created with default settings!");
        }
        
        private void OnGUI()
        {
            GUILayout.Label("Balloon Simulation Demo Setup", EditorStyles.boldLabel);
            EditorGUILayout.Space();
            
            // Scene Settings
            EditorGUILayout.LabelField("Scene Configuration", EditorStyles.boldLabel);
            balloonCount = EditorGUILayout.IntSlider("Balloon Count", balloonCount, 100, 5000);
            worldSize = EditorGUILayout.Slider("World Size", worldSize, 50f, 200f);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Visual Options", EditorStyles.boldLabel);
            includeSkybox = EditorGUILayout.Toggle("Include Skybox", includeSkybox);
            includePostProcessing = EditorGUILayout.Toggle("Include Post Processing", includePostProcessing);
            includeWindZones = EditorGUILayout.Toggle("Include Wind Zones", includeWindZones);
            
            EditorGUILayout.Space();
            
            // Performance Presets
            EditorGUILayout.LabelField("Performance Presets", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Mobile (500)"))
            {
                balloonCount = 500;
                worldSize = 50f;
                includePostProcessing = false;
            }
            if (GUILayout.Button("Desktop (2000)"))
            {
                balloonCount = 2000;
                worldSize = 100f;
                includePostProcessing = true;
            }
            if (GUILayout.Button("High-End (5000)"))
            {
                balloonCount = 5000;
                worldSize = 150f;
                includePostProcessing = true;
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // Create Button
            GUI.backgroundColor = Color.green;
            if (GUILayout.Button("Create Demo Scene", GUILayout.Height(40)))
            {
                SetupDemoScene(balloonCount, worldSize, includeSkybox, includePostProcessing, includeWindZones);
            }
            GUI.backgroundColor = Color.white;
            
            EditorGUILayout.Space();
            
            // Help Box
            EditorGUILayout.HelpBox(
                "This will create a complete demo scene with:\n" +
                "• Balloon Manager with physics simulation\n" +
                "• Configured camera with controls\n" +
                "• Lighting and environment setup\n" +
                "• Optional visual effects\n\n" +
                "Controls:\n" +
                "• R - Reset simulation\n" +
                "• W - Toggle wind\n" +
                "• +/- - Change balloon count\n" +
                "• Right click + drag - Look around\n" +
                "• WASD - Move camera\n" +
                "• Q/E - Move up/down",
                MessageType.Info);
        }
        
        private static void SetupDemoScene(int balloonCount, float worldSize, bool includeSkybox, 
            bool includePostProcessing, bool includeWindZones)
        {
            // Clear existing scene objects (optional)
            if (EditorUtility.DisplayDialog("Clear Scene?", 
                "Do you want to clear all existing objects in the scene?", "Yes", "No"))
            {
                GameObject[] allObjects = Object.FindObjectsOfType<GameObject>();
                foreach (var obj in allObjects)
                {
                    DestroyImmediate(obj);
                }
            }
            
            // Create Balloon Manager
            GameObject managerObj = new GameObject("Balloon Manager");
            BalloonManager manager = managerObj.AddComponent<BalloonManager>();
            
            // Configure manager
            SerializedObject managerSO = new SerializedObject(manager);
            managerSO.FindProperty("balloonCount").intValue = balloonCount;
            
            // Set simulation parameters
            var simParams = managerSO.FindProperty("simulationParameters");
            simParams.FindPropertyRelative("gravity").floatValue = 9.81f;
            simParams.FindPropertyRelative("airDensity").floatValue = 1.225f;
            simParams.FindPropertyRelative("windStrength").floatValue = 2.0f;
            simParams.FindPropertyRelative("damping").floatValue = 0.98f;
            simParams.FindPropertyRelative("collisionElasticity").floatValue = 0.8f;
            
            // Set world bounds
            var worldBounds = simParams.FindPropertyRelative("worldBounds");
            var boundsCenter = worldBounds.FindPropertyRelative("m_Center");
            boundsCenter.FindPropertyRelative("x").floatValue = 0;
            boundsCenter.FindPropertyRelative("y").floatValue = worldSize / 2f;
            boundsCenter.FindPropertyRelative("z").floatValue = 0;
            
            var boundsExtents = worldBounds.FindPropertyRelative("m_Extent");
            boundsExtents.FindPropertyRelative("x").floatValue = worldSize / 2f;
            boundsExtents.FindPropertyRelative("y").floatValue = worldSize / 2f;
            boundsExtents.FindPropertyRelative("z").floatValue = worldSize / 2f;
            
            // Create and assign material
            Material balloonMat = CreateBalloonMaterial();
            managerSO.FindProperty("balloonMaterial").objectReferenceValue = balloonMat;
            
            managerSO.ApplyModifiedProperties();
            
            // Create Camera
            GameObject cameraObj = new GameObject("Main Camera");
            cameraObj.tag = "MainCamera";
            Camera camera = cameraObj.AddComponent<Camera>();
            cameraObj.AddComponent<AudioListener>();
            
            // Position camera
            cameraObj.transform.position = new Vector3(0, worldSize * 0.3f, -worldSize * 0.5f);
            cameraObj.transform.rotation = Quaternion.Euler(15, 0, 0);
            camera.fieldOfView = 60;
            camera.farClipPlane = worldSize * 3f;
            camera.nearClipPlane = 0.3f;
            
            // Add camera controller
            cameraObj.AddComponent<FreeLookCamera>();
            
            // Create Lighting
            SetupLighting(worldSize);
            
            // Create Ground
            CreateGround(worldSize);
            
            // Create Environment
            if (includeSkybox)
            {
                SetupSkybox();
            }
            
            // Create Wind Zones
            if (includeWindZones)
            {
                CreateWindZones(worldSize);
            }
            
            // Create UI Canvas for info display
            CreateUICanvas();
            
            // Set scene view
            SceneView sceneView = SceneView.lastActiveSceneView;
            if (sceneView != null)
            {
                sceneView.AlignViewToObject(cameraObj.transform);
            }
            
            Debug.Log($"Demo scene created successfully with {balloonCount} balloons!");
        }
        
        private static Material CreateBalloonMaterial()
        {
            // Try to find existing balloon material
            string[] guids = AssetDatabase.FindAssets("t:Material BalloonMaterial");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<Material>(path);
            }
            
            // Create new material with the custom shader
            Shader balloonShader = Shader.Find("BalloonSimulation/InstancedIndirect");
            if (balloonShader == null)
            {
                // Fallback to standard shader
                balloonShader = Shader.Find("Standard");
            }
            
            Material mat = new Material(balloonShader);
            mat.name = "BalloonMaterial";
            
            // Configure material properties
            mat.SetFloat("_Glossiness", 0.8f);
            mat.SetFloat("_Metallic", 0.0f);
            mat.SetColor("_RimColor", new Color(1f, 1f, 1f, 0.5f));
            mat.SetFloat("_RimPower", 3.0f);
            
            // Save material as asset
            string materialPath = "Assets/Materials/BalloonMaterial.mat";
            AssetDatabase.CreateAsset(mat, materialPath);
            AssetDatabase.SaveAssets();
            
            return mat;
        }
        
        private static void SetupLighting(float worldSize)
        {
            // Directional Light
            GameObject lightObj = new GameObject("Directional Light");
            Light light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.95f, 0.8f);
            light.shadows = LightShadows.Soft;
            lightObj.transform.rotation = Quaternion.Euler(45f, -30f, 0);
            
            // Configure shadow settings
            light.shadowStrength = 0.8f;
            light.shadowBias = 0.05f;
            light.shadowNormalBias = 0.4f;
            
            // Ambient lighting
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.5f, 0.7f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.4f, 0.5f, 0.6f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.2f, 0.2f);
            RenderSettings.ambientIntensity = 1.0f;
            
            // Fog
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.7f, 0.8f, 0.9f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = worldSize * 0.5f;
            RenderSettings.fogEndDistance = worldSize * 2f;
        }
        
        private static void CreateGround(float worldSize)
        {
            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.localScale = new Vector3(worldSize / 10f, 1, worldSize / 10f);
            
            // Create ground material
            Material groundMat = new Material(Shader.Find("Standard"));
            groundMat.color = new Color(0.3f, 0.35f, 0.3f);
            groundMat.SetFloat("_Glossiness", 0.2f);
            ground.GetComponent<Renderer>().material = groundMat;
            
            // Add some visual interest with additional planes at different heights
            for (int i = 0; i < 3; i++)
            {
                GameObject platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
                platform.name = $"Platform_{i}";
                platform.transform.position = new Vector3(
                    Random.Range(-worldSize * 0.3f, worldSize * 0.3f),
                    Random.Range(5f, 15f),
                    Random.Range(-worldSize * 0.3f, worldSize * 0.3f)
                );
                platform.transform.localScale = new Vector3(
                    Random.Range(10f, 20f),
                    Random.Range(1f, 3f),
                    Random.Range(10f, 20f)
                );
                
                Material platformMat = new Material(Shader.Find("Standard"));
                platformMat.color = new Color(0.4f, 0.4f, 0.4f);
                platform.GetComponent<Renderer>().material = platformMat;
            }
        }
        
        private static void SetupSkybox()
        {
            // Try to find a procedural skybox material
            Material skyboxMat = new Material(Shader.Find("Skybox/Procedural"));
            skyboxMat.SetFloat("_SunSize", 0.04f);
            skyboxMat.SetFloat("_AtmosphereThickness", 1.0f);
            skyboxMat.SetColor("_SkyTint", new Color(0.5f, 0.7f, 1f));
            skyboxMat.SetColor("_GroundColor", new Color(0.3f, 0.3f, 0.3f));
            skyboxMat.SetFloat("_Exposure", 1.3f);
            
            RenderSettings.skybox = skyboxMat;
            DynamicGI.UpdateEnvironment();
        }
        
        private static void CreateWindZones(float worldSize)
        {
            // Main wind zone
            GameObject windObj = new GameObject("Wind Zone (Main)");
            WindZone wind = windObj.AddComponent<WindZone>();
            wind.mode = WindZoneMode.Directional;
            wind.windMain = 0.5f;
            wind.windTurbulence = 0.25f;
            wind.windPulseMagnitude = 0.5f;
            wind.windPulseFrequency = 0.25f;
            windObj.transform.rotation = Quaternion.Euler(0, 45, 0);
            
            // Additional spherical wind zones for variety
            for (int i = 0; i < 2; i++)
            {
                GameObject sphericalWind = new GameObject($"Wind Zone (Spherical {i})");
                WindZone sWind = sphericalWind.AddComponent<WindZone>();
                sWind.mode = WindZoneMode.Spherical;
                sWind.radius = worldSize * 0.3f;
                sWind.windMain = 0.3f;
                sWind.windTurbulence = 0.5f;
                
                sphericalWind.transform.position = new Vector3(
                    Random.Range(-worldSize * 0.3f, worldSize * 0.3f),
                    Random.Range(10f, 30f),
                    Random.Range(-worldSize * 0.3f, worldSize * 0.3f)
                );
            }
        }
        
        private static void CreateUICanvas()
        {
            GameObject canvasObj = new GameObject("UI Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            
            // Create info panel
            GameObject panel = new GameObject("Info Panel");
            panel.transform.SetParent(canvasObj.transform, false);
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0, 1);
            panelRect.anchorMax = new Vector2(0, 1);
            panelRect.pivot = new Vector2(0, 1);
            panelRect.anchoredPosition = new Vector2(10, -10);
            panelRect.sizeDelta = new Vector2(300, 200);
            
            // Background
            UnityEngine.UI.Image bg = panel.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0, 0, 0, 0.7f);
            
            // Info text
            GameObject textObj = new GameObject("Info Text");
            textObj.transform.SetParent(panel.transform, false);
            RectTransform textRect = textObj.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10, 10);
            textRect.offsetMax = new Vector2(-10, -10);
            
            UnityEngine.UI.Text text = textObj.AddComponent<UnityEngine.UI.Text>();
            text.text = "Balloon Simulation\n\nControls:\nR - Reset\nW - Toggle Wind\n+/- - Change Count\n\nRight Click + Drag - Look\nWASD - Move\nQ/E - Up/Down";
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 14;
            text.color = Color.white;
        }
    }
}