using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// Enhanced balloon manager implementing the full kiro specification
    /// Supports 50000+ balloons with advanced physics and rendering
    /// </summary>
    public class EnhancedBalloonManager : MonoBehaviour
    {
        [Header("Enhanced Simulation Settings")]
        [SerializeField] private int balloonCount = 10000;
        public int BalloonCount => balloonCount;
        [SerializeField] private int maxBalloonCount = 50000;
        [SerializeField] public EnhancedSimulationParameters simulationParameters;
        [SerializeField] public SwarmParameters swarmParameters;
        
        [Header("Rendering")]
        [SerializeField] private Material balloonMaterial;
        [SerializeField] private Mesh balloonMesh;
        [SerializeField] private ComputeShader physicsComputeShader;
        
        [Header("Performance")]
        [SerializeField] private int innerLoopBatchCount = 64;
        [SerializeField] private float maxVelocity = 20f;
        [SerializeField] private bool enableGPUPhysics = true;
        [SerializeField] private bool enableFluidDynamics = true;
        [SerializeField] private bool enableSwarmPhysics = true;
        
        // Enhanced native collections
        private NativeArray<EnhancedBalloonData> balloons;
        private NativeArray<int> activeIndices;
        private NativeList<int> activeBalloonsList;
        private HierarchicalSpatialGrid spatialGrid;
        private NativeArray<int> balloonToCellMapping;
        
        // Fluid dynamics data
        private NativeArray<float3> velocityField;
        private NativeArray<float> pressureField;
        private NativeArray<float> densityField;
        private NativeArray<float> temperatureField;
        private NativeArray<float> localDensityField;
        
        // Collision and scene data
        private NativeList<CollisionPair> collisionPairs;
        private NativeArray<SceneColliderData> sceneColliders;
        
        // Systems
        private EnhancedRenderingSystem renderingSystem;
        private BalloonMemoryManager memoryManager;
        
        // Job handles for parallel execution
        private JobHandle physicsJobHandle;
        private JobHandle fluidJobHandle;
        private JobHandle swarmJobHandle;
        private JobHandle deformationJobHandle;
        private JobHandle renderingJobHandle;
        
        // Grid parameters for fluid simulation
        private const int GRID_RESOLUTION = 64;
        private float3 gridOrigin;
        private float3 gridSize;
        private int3 gridDimensions;
        private float cellSize;
        
        // Random number generator
        private Random random;
        
        // Performance tracking
        private float lastUpdateTime;
        private int frameCount;
        private float averageFPS;
        private float physicsTime;
        private float renderTime;
        
        // Debug and monitoring
        private bool showDebugInfo = true;
        private int destructionCount = 0;
        
        void Start()
        {
            InitializeEnhancedSimulation();
        }
        
        void InitializeEnhancedSimulation()
        {
            Debug.Log("[EnhancedBalloonManager] Initializing enhanced simulation system...");
            
            // Initialize random with seed
            random = new Random((uint)System.DateTime.Now.Millisecond);
            
            // Set up default parameters if not configured
            if (simulationParameters.gravity == 0)
            {
                simulationParameters = EnhancedSimulationParameters.CreateDefault();
            }
            if (swarmParameters.neighborRadius == 0)
            {
                swarmParameters = SwarmParameters.CreateDefault();
            }
            
            // Initialize memory manager
            memoryManager = new BalloonMemoryManager();
            memoryManager.Initialize(maxBalloonCount);
            
            // Setup fluid grid
            InitializeFluidGrid();
            
            // Initialize spatial grid
            InitializeSpatialGrid();
            
            // Collect scene colliders
            CollectSceneColliders();
            
            // Create balloon mesh if not assigned
            if (balloonMesh == null)
            {
                balloonMesh = BalloonMeshGenerator.CreateBalloonMesh(16, 16);
            }
            
            // Initialize enhanced native collections
            InitializeCollections();
            
            // Initialize balloons with enhanced data
            InitializeEnhancedBalloons();
            
            // Initialize rendering system
            InitializeRenderingSystem();
            
            Debug.Log($"[EnhancedBalloonManager] Enhanced simulation initialized with {balloonCount} balloons");
        }
        
        void InitializeFluidGrid()
        {
            // Calculate grid parameters
            var bounds = simulationParameters.worldBounds;
            gridOrigin = bounds.min;
            gridSize = bounds.size;
            
            // Use fixed grid resolution for performance
            gridDimensions = new int3(GRID_RESOLUTION, GRID_RESOLUTION, GRID_RESOLUTION);
            cellSize = math.max(gridSize.x, math.max(gridSize.y, gridSize.z)) / GRID_RESOLUTION;
            
            int totalCells = gridDimensions.x * gridDimensions.y * gridDimensions.z;
            
            // Initialize fluid fields
            velocityField = new NativeArray<float3>(totalCells, Allocator.Persistent);
            pressureField = new NativeArray<float>(totalCells, Allocator.Persistent);
            densityField = new NativeArray<float>(totalCells, Allocator.Persistent);
            temperatureField = new NativeArray<float>(totalCells, Allocator.Persistent);
            
            // Initialize with default values
            for (int i = 0; i < totalCells; i++)
            {
                velocityField[i] = simulationParameters.windDirection * simulationParameters.windStrength;
                pressureField[i] = 101325f; // Atmospheric pressure
                densityField[i] = simulationParameters.airDensity;
                temperatureField[i] = simulationParameters.temperature;
            }
            
            Debug.Log($"[EnhancedBalloonManager] Fluid grid initialized: {gridDimensions} cells, cell size: {cellSize:F2}");
        }
        
        void InitializeSpatialGrid()
        {
            float cellSizeForSpatial = 2f; // 2 units per cell
            int3 spatialDims = new int3(
                Mathf.CeilToInt(gridSize.x / cellSizeForSpatial),
                Mathf.CeilToInt(gridSize.y / cellSizeForSpatial),
                Mathf.CeilToInt(gridSize.z / cellSizeForSpatial)
            );
            
            spatialGrid = new HierarchicalSpatialGrid(
                cellSizeForSpatial,
                gridOrigin,
                spatialDims,
                100, // max objects per cell
                Allocator.Persistent
            );
            
            Debug.Log($"[EnhancedBalloonManager] Spatial grid initialized: {spatialDims}");
        }
        
        void InitializeCollections()
        {
            // Enhanced balloon data
            balloons = new NativeArray<EnhancedBalloonData>(maxBalloonCount, Allocator.Persistent);
            activeIndices = new NativeArray<int>(maxBalloonCount, Allocator.Persistent);
            activeBalloonsList = new NativeList<int>(maxBalloonCount, Allocator.Persistent);
            
            // Mapping and collision data
            balloonToCellMapping = new NativeArray<int>(maxBalloonCount, Allocator.Persistent);
            collisionPairs = new NativeList<CollisionPair>(maxBalloonCount * 4, Allocator.Persistent);
            
            // Density field for swarm physics
            int densityFieldSize = spatialGrid.level2Cells.Length;
            localDensityField = new NativeArray<float>(densityFieldSize, Allocator.Persistent);
            
            // Initialize active indices
            for (int i = 0; i < balloonCount; i++)
            {
                activeIndices[i] = i;
                activeBalloonsList.Add(i);
            }
            for (int i = balloonCount; i < maxBalloonCount; i++)
            {
                activeIndices[i] = -1; // Inactive
            }
        }
        
        void InitializeEnhancedBalloons()
        {
            float3 spawnMin = new float3(simulationParameters.worldBounds.min.x,
                                         simulationParameters.worldBounds.min.y,
                                         simulationParameters.worldBounds.min.z);
            float3 spawnMax = new float3(simulationParameters.worldBounds.max.x,
                                         simulationParameters.worldBounds.max.y * 0.4f, // Spawn in lower area
                                         simulationParameters.worldBounds.max.z);
            
            for (int i = 0; i < balloonCount; i++)
            {
                // Random spawn position
                float3 position = random.NextFloat3(spawnMin, spawnMax);
                
                // Create enhanced balloon with procedural properties
                var balloon = CreateProceduralBalloon(position, i);
                
                balloons[i] = balloon;
            }
            
            // Initialize inactive balloons
            for (int i = balloonCount; i < maxBalloonCount; i++)
            {
                var balloon = EnhancedBalloonData.CreateDefault(float3.zero, 0.1f);
                balloon.state = BalloonState.Destroyed;
                balloons[i] = balloon;
            }
            
            Debug.Log($"[EnhancedBalloonManager] Initialized {balloonCount} enhanced balloons");
        }
        
        EnhancedBalloonData CreateProceduralBalloon(float3 position, int index)
        {
            // Procedural balloon generation based on .kiro requirements
            float balloonType = random.NextFloat(0f, 100f);
            float radius, mass, buoyancy;
            float4 baseColor;
            float metallic, roughness, transparency;
            
            // Balloon size categories with natural distribution
            if (balloonType < 15f) // Small balloons (15%)
            {
                radius = random.NextFloat(0.15f, 0.25f);
                mass = random.NextFloat(0.001f, 0.002f);
                buoyancy = random.NextFloat(0.018f, 0.020f);
            }
            else if (balloonType < 25f) // Large balloons (10%)
            {
                radius = random.NextFloat(0.6f, 0.8f);
                mass = random.NextFloat(0.008f, 0.012f);
                buoyancy = random.NextFloat(0.011f, 0.014f);
            }
            else // Standard balloons (75%)
            {
                radius = random.NextFloat(0.3f, 0.5f);
                mass = random.NextFloat(0.003f, 0.006f);
                buoyancy = random.NextFloat(0.014f, 0.018f);
            }
            
            // Procedural color generation with aesthetic variety
            float hue = random.NextFloat(0f, 1f);
            float saturation = random.NextFloat(0.6f, 1f);
            float brightness = random.NextFloat(0.7f, 1f);
            baseColor = HSVtoRGB(hue, saturation, brightness);
            
            // Material properties based on balloon type
            if (balloonType < 5f) // Metallic balloons (5%)
            {
                metallic = random.NextFloat(0.8f, 1f);
                roughness = random.NextFloat(0.1f, 0.3f);
                transparency = random.NextFloat(0.1f, 0.3f);
            }
            else if (balloonType < 15f) // Matte balloons (10%)
            {
                metallic = random.NextFloat(0f, 0.1f);
                roughness = random.NextFloat(0.8f, 1f);
                transparency = random.NextFloat(0.05f, 0.15f);
            }
            else // Standard translucent balloons (85%)
            {
                metallic = random.NextFloat(0.05f, 0.2f);
                roughness = random.NextFloat(0.3f, 0.7f);
                transparency = random.NextFloat(0.7f, 0.9f);
            }
            
            // Create enhanced balloon
            var balloon = EnhancedBalloonData.CreateDefault(position, radius);
            balloon.mass = mass;
            balloon.buoyancy = buoyancy;
            balloon.baseColor = baseColor;
            balloon.metallic = metallic;
            balloon.roughness = roughness;
            balloon.transparency = transparency;
            
            // Add initial velocity variation
            balloon.velocity = random.NextFloat3(-0.5f, 0.5f);
            
            // Enhanced physical properties
            balloon.elasticity = random.NextFloat(0.7f, 0.9f);
            balloon.viscosity = random.NextFloat(0.0005f, 0.002f);
            balloon.surfaceTension = random.NextFloat(0.06f, 0.08f);
            balloon.internalPressure = random.NextFloat(101325f, 103000f);
            
            balloon.UpdateMatrix();
            
            return balloon;
        }
        
        float4 HSVtoRGB(float h, float s, float v)
        {
            float c = v * s;
            float x = c * (1f - math.abs((h * 6f) % 2f - 1f));
            float m = v - c;
            
            float3 rgb;
            if (h < 1f/6f) rgb = new float3(c, x, 0);
            else if (h < 2f/6f) rgb = new float3(x, c, 0);
            else if (h < 3f/6f) rgb = new float3(0, c, x);
            else if (h < 4f/6f) rgb = new float3(0, x, c);
            else if (h < 5f/6f) rgb = new float3(x, 0, c);
            else rgb = new float3(c, 0, x);
            
            rgb += m;
            return new float4(rgb, 1f);
        }
        
        void InitializeRenderingSystem()
        {
            var renderingSystemGO = new GameObject("EnhancedRenderingSystem");
            renderingSystemGO.transform.SetParent(transform);
            
            renderingSystem = renderingSystemGO.AddComponent<EnhancedRenderingSystem>();
            
            // Configure rendering system via reflection to set private fields
            var type = typeof(EnhancedRenderingSystem);
            type.GetField("balloonMaterial", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(renderingSystem, balloonMaterial);
            type.GetField("balloonMesh", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(renderingSystem, balloonMesh);
            type.GetField("physicsComputeShader", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(renderingSystem, physicsComputeShader);
            type.GetField("maxInstanceCount", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(renderingSystem, maxBalloonCount);
            
            renderingSystem.SetMaxInstanceCount(maxBalloonCount);
            renderingSystem.SetLODDistances(25f, 50f, 100f, 200f);
            renderingSystem.EnableGPUCulling(true);
            
            Debug.Log("[EnhancedBalloonManager] Enhanced rendering system initialized");
        }
        
        void CollectSceneColliders()
        {
            var collidersList = new System.Collections.Generic.List<SceneColliderData>();
            
            // Find all colliders in the scene
            Collider[] allColliders = FindObjectsOfType<Collider>();
            
            foreach (var collider in allColliders)
            {
                // Skip balloon objects, triggers, and inactive objects
                if (collider.isTrigger || collider.gameObject == gameObject || !collider.enabled || !collider.gameObject.activeInHierarchy)
                    continue;
                
                // Convert Unity colliders to job-friendly data
                if (collider is BoxCollider box)
                {
                    collidersList.Add(SceneColliderData.FromBoxCollider(box));
                }
                else if (collider is SphereCollider sphere)
                {
                    collidersList.Add(SceneColliderData.FromSphereCollider(sphere));
                }
                else if (collider is CapsuleCollider capsule)
                {
                    collidersList.Add(SceneColliderData.FromCapsuleCollider(capsule));
                }
            }
            
            // Only reallocate if size changed
            if (!sceneColliders.IsCreated || sceneColliders.Length != collidersList.Count)
            {
                if (sceneColliders.IsCreated)
                    sceneColliders.Dispose();
                    
                sceneColliders = new NativeArray<SceneColliderData>(collidersList.Count, Allocator.Persistent);
            }
            
            // Update collider data
            for (int i = 0; i < collidersList.Count; i++)
            {
                sceneColliders[i] = collidersList[i];
            }
        }
        
        void Update()
        {
            // Handle input
            HandleEnhancedInput();
            
            // Update performance metrics
            UpdatePerformanceMetrics();
        }
        
        void FixedUpdate()
        {
            float startTime = Time.realtimeSinceStartup;
            float deltaTime = Time.fixedDeltaTime;
            float time = Time.time;
            
            // Complete previous frame's jobs
            CompleteAllJobs();
            
            // Update scene colliders
            CollectSceneColliders();
            
            // Schedule all physics jobs in parallel
            SchedulePhysicsJobs(deltaTime, time);
            
            physicsTime = (Time.realtimeSinceStartup - startTime) * 1000f;
        }
        
        void SchedulePhysicsJobs(float deltaTime, float time)
        {
            // 1. Update spatial grid
            var updateSpatialJob = new UpdateSpatialGridJob
            {
                balloons = balloons,
                activeIndices = activeBalloonsList.AsArray(),
                grid = spatialGrid,
                balloonToCellMapping = balloonToCellMapping
            };
            var spatialHandle = updateSpatialJob.Schedule();
            
            // 2. Update fluid grid
            var updateFluidJob = new UpdateFluidGridJob
            {
                balloons = balloons,
                activeIndices = activeBalloonsList.AsArray(),
                velocityField = velocityField,
                pressureField = pressureField,
                densityField = densityField,
                temperatureField = temperatureField,
                gridOrigin = gridOrigin,
                gridDimensions = gridDimensions,
                cellSize = cellSize,
                deltaTime = deltaTime,
                simParams = simulationParameters
            };
            var fluidUpdateHandle = updateFluidJob.Schedule(spatialHandle);
            
            // 3. Update density field for swarm physics
            var densityFieldJob = new UpdateDensityFieldJob
            {
                balloons = balloons,
                activeIndices = activeBalloonsList.AsArray(),
                spatialGrid = spatialGrid,
                densityField = localDensityField
            };
            var densityHandle = densityFieldJob.Schedule(fluidUpdateHandle);
            
            // 4. Enhanced physics job (handles buoyancy, basic forces)
            var enhancedPhysicsJob = new EnhancedPhysicsJob
            {
                balloons = balloons,
                activeIndices = activeBalloonsList.AsArray(),
                parameters = simulationParameters,
                deltaTime = deltaTime,
                time = time
            };
            physicsJobHandle = enhancedPhysicsJob.Schedule(activeBalloonsList.Length, innerLoopBatchCount, densityHandle);
            
            // 5. Fluid dynamics job (parallel with physics)
            if (enableFluidDynamics)
            {
                var fluidDynamicsJob = new FluidDynamicsJob
                {
                    balloonsIn = balloons,
                    balloonsOut = balloons,
                    activeIndices = activeBalloonsList.AsArray(),
                    velocityField = velocityField,
                    pressureField = pressureField,
                    densityField = densityField,
                    temperatureField = temperatureField,
                    gridOrigin = gridOrigin,
                    gridSize = gridSize,
                    gridDimensions = gridDimensions,
                    cellSize = cellSize,
                    simParams = simulationParameters,
                    deltaTime = deltaTime,
                    time = time,
                    spatialGrid = spatialGrid,
                    balloonToCellMapping = balloonToCellMapping
                };
                fluidJobHandle = fluidDynamicsJob.Schedule(activeBalloonsList.Length, innerLoopBatchCount, physicsJobHandle);
            }
            else
            {
                fluidJobHandle = physicsJobHandle;
            }
            
            // 6. Swarm physics job (parallel with fluid)
            if (enableSwarmPhysics)
            {
                var swarmPhysicsJob = new SwarmPhysicsJob
                {
                    balloonsIn = balloons,
                    balloonsOut = balloons,
                    activeIndices = activeBalloonsList.AsArray(),
                    spatialGrid = spatialGrid,
                    balloonToCellMapping = balloonToCellMapping,
                    swarmParams = swarmParameters,
                    simParams = simulationParameters,
                    deltaTime = deltaTime,
                    time = time,
                    localDensityField = localDensityField
                };
                swarmJobHandle = swarmPhysicsJob.Schedule(activeBalloonsList.Length, innerLoopBatchCount, fluidJobHandle);
            }
            else
            {
                swarmJobHandle = fluidJobHandle;
            }
            
            // 7. Deformation job
            var deformationJob = new DeformationJob
            {
                balloonsIn = balloons,
                balloonsOut = balloons,
                activeIndices = activeBalloonsList.AsArray(),
                spatialGrid = spatialGrid,
                balloonToCellMapping = balloonToCellMapping,
                deltaTime = deltaTime,
                time = time
            };
            deformationJobHandle = deformationJob.Schedule(activeBalloonsList.Length, innerLoopBatchCount, swarmJobHandle);
            
            // Schedule batch for optimization
            JobHandle.ScheduleBatchedJobs();
        }
        
        void LateUpdate()
        {
            float startTime = Time.realtimeSinceStartup;
            
            // Ensure physics jobs are complete
            CompleteAllJobs();
            
            // Update rendering data
            renderingSystem.UpdateRenderingData(balloons, activeBalloonsList.AsArray());
            
            // Render balloons
            renderingSystem.RenderBalloons();
            
            renderTime = (Time.realtimeSinceStartup - startTime) * 1000f;
        }
        
        void CompleteAllJobs()
        {
            deformationJobHandle.Complete();
            swarmJobHandle.Complete();
            fluidJobHandle.Complete();
            physicsJobHandle.Complete();
        }
        
        void HandleEnhancedInput()
        {
            // Reset simulation
            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetSimulation();
            }
            
            // Performance report
            if (Input.GetKeyDown(KeyCode.P))
            {
                LogPerformanceReport();
            }
            
            // Toggle systems
            if (Input.GetKeyDown(KeyCode.F))
            {
                enableFluidDynamics = !enableFluidDynamics;
                Debug.Log($"Fluid Dynamics: {(enableFluidDynamics ? "ON" : "OFF")}");
            }
            
            if (Input.GetKeyDown(KeyCode.S))
            {
                enableSwarmPhysics = !enableSwarmPhysics;
                Debug.Log($"Swarm Physics: {(enableSwarmPhysics ? "ON" : "OFF")}");
            }
            
            // Toggle wind
            if (Input.GetKeyDown(KeyCode.W))
            {
                simulationParameters.windStrength = simulationParameters.windStrength > 0 ? 0 : 3f;
                Debug.Log($"Wind: {(simulationParameters.windStrength > 0 ? "ON" : "OFF")}");
            }
            
            // Adjust balloon count
            if (Input.GetKeyDown(KeyCode.Plus) || Input.GetKeyDown(KeyCode.Equals))
            {
                ChangeBalloonCount(Mathf.Min(balloonCount + 1000, maxBalloonCount));
            }
            if (Input.GetKeyDown(KeyCode.Minus))
            {
                ChangeBalloonCount(Mathf.Max(balloonCount - 1000, 100));
            }
            
            // Debug info toggle
            if (Input.GetKeyDown(KeyCode.F1))
            {
                showDebugInfo = !showDebugInfo;
            }
        }
        
        void ResetSimulation()
        {
            CompleteAllJobs();
            
            activeBalloonsList.Clear();
            for (int i = 0; i < balloonCount; i++)
            {
                activeBalloonsList.Add(i);
            }
            
            InitializeEnhancedBalloons();
            Debug.Log("Enhanced simulation reset");
        }
        
        public void ChangeBalloonCount(int newCount)
        {
            if (newCount == balloonCount) return;
            
            CompleteAllJobs();
            
            int oldCount = balloonCount;
            balloonCount = newCount;
            
            activeBalloonsList.Clear();
            for (int i = 0; i < balloonCount; i++)
            {
                activeIndices[i] = i;
                activeBalloonsList.Add(i);
            }
            
            // Initialize new balloons if count increased
            if (newCount > oldCount)
            {
                for (int i = oldCount; i < newCount; i++)
                {
                    float3 spawnPos = random.NextFloat3(
                        new float3(simulationParameters.worldBounds.min.x, simulationParameters.worldBounds.min.y, simulationParameters.worldBounds.min.z),
                        new float3(simulationParameters.worldBounds.max.x, simulationParameters.worldBounds.max.y * 0.4f, simulationParameters.worldBounds.max.z)
                    );
                    balloons[i] = CreateProceduralBalloon(spawnPos, i);
                }
            }
            // Deactivate balloons if count decreased
            else
            {
                for (int i = newCount; i < oldCount; i++)
                {
                    var balloon = balloons[i];
                    balloon.state = BalloonState.Destroyed;
                    balloons[i] = balloon;
                    activeIndices[i] = -1;
                }
            }
            
            Debug.Log($"Balloon count changed to: {balloonCount}");
        }
        
        void UpdatePerformanceMetrics()
        {
            frameCount++;
            float currentTime = Time.time;
            
            if (currentTime - lastUpdateTime >= 1f)
            {
                averageFPS = frameCount / (currentTime - lastUpdateTime);
                frameCount = 0;
                lastUpdateTime = currentTime;
            }
        }
        
        void LogPerformanceReport()
        {
            Debug.Log($"=== ENHANCED BALLOON SIMULATION PERFORMANCE REPORT ===");
            Debug.Log($"Balloons: {balloonCount} / {maxBalloonCount}");
            Debug.Log($"FPS: {averageFPS:F1}");
            Debug.Log($"Physics Time: {physicsTime:F2}ms");
            Debug.Log($"Render Time: {renderTime:F2}ms");
            Debug.Log($"Rendered Instances: {renderingSystem.RenderedInstanceCount}");
            Debug.Log($"Systems: Fluid={enableFluidDynamics}, Swarm={enableSwarmPhysics}");
            Debug.Log($"Memory Usage: {memoryManager?.GetMemoryUsageMB():F1}MB");
            Debug.Log($"Scene Colliders: {(sceneColliders.IsCreated ? sceneColliders.Length : 0)}");
            Debug.Log($"=======================================================");
        }
        
        void OnGUI()
        {
            if (!showDebugInfo) return;
            
            // Display enhanced performance info
            GUI.color = Color.black;
            GUI.Label(new Rect(11, 11, 400, 300), GetEnhancedPerformanceText());
            GUI.color = Color.white;
            GUI.Label(new Rect(10, 10, 400, 300), GetEnhancedPerformanceText());
        }
        
        string GetEnhancedPerformanceText()
        {
            return $"ENHANCED BALLOON SIMULATION\n" +
                   $"Balloons: {balloonCount} / {maxBalloonCount}\n" +
                   $"FPS: {averageFPS:F1}\n" +
                   $"Physics: {physicsTime:F1}ms\n" +
                   $"Render: {renderTime:F1}ms\n" +
                   $"Rendered: {renderingSystem?.RenderedInstanceCount ?? 0}\n" +
                   $"Memory: {memoryManager?.GetMemoryUsageMB():F1}MB\n" +
                   $"\nSystems:\n" +
                   $"  Fluid Dynamics: {(enableFluidDynamics ? "ON" : "OFF")}\n" +
                   $"  Swarm Physics: {(enableSwarmPhysics ? "ON" : "OFF")}\n" +
                   $"  GPU Physics: {(enableGPUPhysics ? "ON" : "OFF")}\n" +
                   $"\nControls:\n" +
                   $"  R - Reset  P - Performance Report\n" +
                   $"  W - Wind   F - Fluid   S - Swarm\n" +
                   $"  +/- Count  F1 - Debug Info\n" +
                   $"Scene Colliders: {(sceneColliders.IsCreated ? sceneColliders.Length : 0)}";
        }
        
        void OnDestroy()
        {
            CompleteAllJobs();
            
            // Dispose enhanced collections
            if (balloons.IsCreated) balloons.Dispose();
            if (activeIndices.IsCreated) activeIndices.Dispose();
            if (activeBalloonsList.IsCreated) activeBalloonsList.Dispose();
            if (balloonToCellMapping.IsCreated) balloonToCellMapping.Dispose();
            if (collisionPairs.IsCreated) collisionPairs.Dispose();
            if (sceneColliders.IsCreated) sceneColliders.Dispose();
            
            // Dispose fluid fields
            if (velocityField.IsCreated) velocityField.Dispose();
            if (pressureField.IsCreated) pressureField.Dispose();
            if (densityField.IsCreated) densityField.Dispose();
            if (temperatureField.IsCreated) temperatureField.Dispose();
            if (localDensityField.IsCreated) localDensityField.Dispose();
            
            // Dispose spatial grid
            spatialGrid.Dispose();
            
            // Dispose memory manager
            memoryManager?.Dispose();
            
            Debug.Log("[EnhancedBalloonManager] Enhanced simulation disposed");
        }
    }
    
    /// <summary>
    /// Enhanced physics job combining multiple forces
    /// </summary>
    [Unity.Burst.BurstCompile]
    public struct EnhancedPhysicsJob : IJobParallelFor
    {
        public NativeArray<EnhancedBalloonData> balloons;
        [ReadOnly] public NativeArray<int> activeIndices;
        [ReadOnly] public EnhancedSimulationParameters parameters;
        [ReadOnly] public float deltaTime;
        [ReadOnly] public float time;
        
        public void Execute(int index)
        {
            int balloonIndex = activeIndices[index];
            if (balloonIndex < 0 || balloonIndex >= balloons.Length)
                return;
                
            var balloon = balloons[balloonIndex];
            
            if (balloon.state == BalloonState.Destroyed)
                return;
            
            // Apply basic forces
            float3 forces = float3.zero;
            
            // 1. Gravity
            forces.y -= parameters.gravity * balloon.mass;
            
            // 2. Buoyancy (with environmental effects)
            float effectiveBuoyancy = balloon.CalculateEffectiveBuoyancy(parameters.temperature, parameters.altitude);
            forces.y += effectiveBuoyancy * parameters.gravity;
            
            // 3. Wind
            float3 windForce = parameters.windDirection * parameters.windStrength * balloon.radius * balloon.radius * math.PI;
            forces += windForce;
            
            // 4. Damping
            forces -= balloon.velocity * parameters.damping;
            
            // Apply forces
            balloon.acceleration = forces / balloon.mass;
            
            // Integrate motion
            balloon.velocity += balloon.acceleration * deltaTime;
            balloon.position += balloon.velocity * deltaTime;
            
            // Update age
            balloon.age += deltaTime;
            
            // Update deformation recovery
            balloon.UpdateDeformationRecovery(deltaTime);
            
            // Update state
            balloon.UpdateState();
            
            // Update transform matrix
            balloon.UpdateMatrix();
            
            // World bounds constraint
            var bounds = parameters.worldBounds;
            if (balloon.position.x < bounds.min.x || balloon.position.x > bounds.max.x ||
                balloon.position.y < bounds.min.y || balloon.position.y > bounds.max.y ||
                balloon.position.z < bounds.min.z || balloon.position.z > bounds.max.z)
            {
                // Bounce off boundaries
                if (balloon.position.x < bounds.min.x || balloon.position.x > bounds.max.x)
                    balloon.velocity.x *= -parameters.collisionElasticity;
                if (balloon.position.y < bounds.min.y || balloon.position.y > bounds.max.y)
                    balloon.velocity.y *= -parameters.collisionElasticity;
                if (balloon.position.z < bounds.min.z || balloon.position.z > bounds.max.z)
                    balloon.velocity.z *= -parameters.collisionElasticity;
                
                // Clamp position
                balloon.position = math.clamp(balloon.position, bounds.min, bounds.max);
            }
            
            balloons[balloonIndex] = balloon;
        }
    }
}