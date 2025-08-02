using UnityEngine;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// Demo scene setup helper for balloon simulation
    /// </summary>
    public class BalloonSimulationDemo : MonoBehaviour
    {
        [Header("Quick Setup")]
        [SerializeField] private bool autoSetupOnStart = true;
        
        void Start()
        {
            if (autoSetupOnStart)
            {
                SetupDemoScene();
            }
        }
        
        [ContextMenu("Setup Demo Scene")]
        public void SetupDemoScene()
        {
            // Find or create BalloonManager
            BalloonManager manager = FindObjectOfType<BalloonManager>();
            if (manager == null)
            {
                GameObject managerGO = new GameObject("BalloonManager");
                manager = managerGO.AddComponent<BalloonManager>();
            }
            
            // Set up camera if needed
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                GameObject cameraGO = new GameObject("Main Camera");
                cameraGO.tag = "MainCamera";
                mainCamera = cameraGO.AddComponent<Camera>();
                cameraGO.AddComponent<AudioListener>();
            }
            
            // Position camera
            mainCamera.transform.position = new Vector3(0, 25, -50);
            mainCamera.transform.rotation = Quaternion.Euler(15, 0, 0);
            mainCamera.fieldOfView = 60;
            mainCamera.farClipPlane = 200;
            
            // Add camera controller
            SimpleDemoCamera cameraController = mainCamera.GetComponent<SimpleDemoCamera>();
            if (cameraController == null)
            {
                cameraController = mainCamera.gameObject.AddComponent<SimpleDemoCamera>();
            }
            
            // Set up lighting
            SetupLighting();
            
            // Set up ground plane
            SetupGround();
            
            Debug.Log("Demo scene setup complete!");
        }
        
        void SetupLighting()
        {
            // Find or create directional light
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
                GameObject lightGO = new GameObject("Directional Light");
                directionalLight = lightGO.AddComponent<Light>();
                directionalLight.type = LightType.Directional;
            }
            
            // Configure light
            directionalLight.transform.rotation = Quaternion.Euler(45, -30, 0);
            directionalLight.intensity = 1.2f;
            directionalLight.color = new Color(1f, 0.95f, 0.8f);
            directionalLight.shadows = LightShadows.Soft;
            
            // Set up ambient lighting
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.5f, 0.7f, 0.9f);
            RenderSettings.ambientEquatorColor = new Color(0.4f, 0.5f, 0.6f);
            RenderSettings.ambientGroundColor = new Color(0.2f, 0.2f, 0.2f);
        }
        
        void SetupGround()
        {
            // Find or create ground plane
            GameObject ground = GameObject.Find("Ground");
            if (ground == null)
            {
                ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                ground.name = "Ground";
            }
            
            // Scale and position
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(20, 1, 20);
            
            // Material
            Renderer renderer = ground.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material groundMat = new Material(Shader.Find("Standard"));
                groundMat.color = new Color(0.3f, 0.3f, 0.3f);
                groundMat.SetFloat("_Glossiness", 0.2f);
                renderer.material = groundMat;
            }
        }
    }
    
    /// <summary>
    /// Simple free look camera controller
    /// </summary>
    public class SimpleDemoCamera : MonoBehaviour
    {
        [Header("Movement")]
        public float moveSpeed = 20f;
        public float boostMultiplier = 3f;
        
        [Header("Look")]
        public float lookSensitivity = 2f;
        public float maxLookAngle = 80f;
        
        private float rotationX = 0f;
        
        void Update()
        {
            // Mouse look
            if (Input.GetMouseButton(1))
            {
                float mouseX = Input.GetAxis("Mouse X") * lookSensitivity;
                float mouseY = Input.GetAxis("Mouse Y") * lookSensitivity;
                
                transform.Rotate(0, mouseX, 0);
                
                rotationX -= mouseY;
                rotationX = Mathf.Clamp(rotationX, -maxLookAngle, maxLookAngle);
                
                Camera camera = GetComponent<Camera>();
                if (camera != null)
                {
                    camera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
                }
            }
            
            // Movement
            float speed = moveSpeed;
            if (Input.GetKey(KeyCode.LeftShift))
            {
                speed *= boostMultiplier;
            }
            
            float h = Input.GetAxis("Horizontal");
            float v = Input.GetAxis("Vertical");
            float u = 0f;
            
            if (Input.GetKey(KeyCode.Q)) u = -1f;
            if (Input.GetKey(KeyCode.E)) u = 1f;
            
            Vector3 movement = transform.right * h + transform.forward * v + transform.up * u;
            transform.position += movement * speed * Time.deltaTime;
        }
    }
}