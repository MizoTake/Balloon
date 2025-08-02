using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// Main manager that orchestrates all balloon simulation systems
    /// </summary>
    public class BalloonManager : MonoBehaviour
    {
        [Header("Simulation Settings")]
        [SerializeField] private int balloonCount = 1000;
        public int BalloonCount => balloonCount;
        [SerializeField] public SimulationParameters simulationParameters;
        
        [Header("Rendering")]
        [SerializeField] private Material balloonMaterial;
        [SerializeField] private Mesh balloonMesh;
        
        [Header("Performance")]
        [SerializeField] private int innerLoopBatchCount = 64;
        [SerializeField] private float maxVelocity = 10f;
        
        // Native collections
        private NativeArray<BalloonData> balloons;
        private NativeArray<float3> velocityDeltas;
        private NativeParallelMultiHashMap<int, int> spatialHash;
        private NativeList<CollisionPair> collisionPairs;
        private NativeArray<SceneColliderData> sceneColliders;
        
        // Systems
        private BalloonRenderingSystem renderingSystem;
        private SpatialHashGrid spatialGrid;
        private BalloonLODSystem lodSystem;
        private PerformanceProfiler performanceProfiler;
        private PhysicsOptimizer physicsOptimizer;
        
        // Job handles
        private JobHandle physicsJobHandle;
        private JobHandle collisionJobHandle;
        
        // Random number generator
        private Random random;
        
        // Performance tracking
        private float lastUpdateTime;
        private int frameCount;
        private float averageFPS;
        
        void Start()
        {
            InitializeSimulation();
        }
        
        void InitializeSimulation()
        {
            // Initialize random with seed
            random = new Random((uint)System.DateTime.Now.Millisecond);
            
            // Set up default simulation parameters if not configured
            if (simulationParameters.gravity == 0)
            {
                simulationParameters = SimulationParameters.CreateDefault();
            }
            
            // Collect scene colliders
            CollectSceneColliders();
            
            // Create balloon mesh if not assigned
            if (balloonMesh == null)
            {
                balloonMesh = BalloonMeshGenerator.CreateBalloonMesh(16, 16);
            }
            
            // Initialize native collections
            balloons = new NativeArray<BalloonData>(balloonCount, Allocator.Persistent);
            velocityDeltas = new NativeArray<float3>(balloonCount, Allocator.Persistent);
            collisionPairs = new NativeList<CollisionPair>(balloonCount * 4, Allocator.Persistent);
            
            // Calculate optimal cell size for spatial hash (2x max balloon radius)
            float maxRadius = 0.5f;
            spatialGrid = new SpatialHashGrid(maxRadius * 2f, simulationParameters.worldBounds);
            
            // Initialize spatial hash with estimated capacity
            int hashMapCapacity = balloonCount * 8; // Estimate 8 cells per balloon average
            spatialHash = new NativeParallelMultiHashMap<int, int>(hashMapCapacity, Allocator.Persistent);
            
            // Initialize balloons with random positions and properties
            InitializeBalloons();
            
            // Create rendering system
            renderingSystem = new BalloonRenderingSystem(balloonMesh, balloonMaterial, balloonCount);
            renderingSystem.UpdateBounds(simulationParameters.worldBounds);
            
            // Create LOD system
            lodSystem = GetComponent<BalloonLODSystem>();
            if (lodSystem == null)
            {
                lodSystem = gameObject.AddComponent<BalloonLODSystem>();
            }
            lodSystem.Initialize(balloonCount);
            
            // Create performance profiler
            performanceProfiler = GetComponent<PerformanceProfiler>();
            if (performanceProfiler == null)
            {
                performanceProfiler = gameObject.AddComponent<PerformanceProfiler>();
            }
            
            // Create physics optimizer
            physicsOptimizer = GetComponent<PhysicsOptimizer>();
            if (physicsOptimizer == null)
            {
                physicsOptimizer = gameObject.AddComponent<PhysicsOptimizer>();
            }
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
        
        void InitializeBalloons()
        {
            float3 spawnMin = new float3(simulationParameters.worldBounds.min.x,
                                         simulationParameters.worldBounds.min.y,
                                         simulationParameters.worldBounds.min.z);
            float3 spawnMax = new float3(simulationParameters.worldBounds.max.x,
                                         simulationParameters.worldBounds.max.y * 0.3f, // Spawn in lower third
                                         simulationParameters.worldBounds.max.z);
            
            for (int i = 0; i < balloonCount; i++)
            {
                // Random spawn position
                float3 position = random.NextFloat3(spawnMin, spawnMax);
                
                // Random balloon properties with realistic values
                float radius, mass, buoyancy;
                
                // Use preset values with variation
                float balloonType = random.NextFloat(0f, 100f);
                if (balloonType < 20f) // 20% small balloons
                {
                    radius = random.NextFloat(0.2f, 0.3f);
                    mass = random.NextFloat(0.002f, 0.003f);
                    buoyancy = random.NextFloat(0.017f, 0.019f);
                }
                else if (balloonType < 30f) // 10% large balloons
                {
                    radius = random.NextFloat(0.5f, 0.7f);
                    mass = random.NextFloat(0.007f, 0.010f);
                    buoyancy = random.NextFloat(0.013f, 0.015f);
                }
                else // 70% standard balloons
                {
                    radius = random.NextFloat(0.35f, 0.45f);
                    mass = random.NextFloat(0.003f, 0.005f);
                    buoyancy = random.NextFloat(0.015f, 0.017f);
                }
                
                // Random color
                float4 color = new float4(
                    random.NextFloat(0.5f, 1f),
                    random.NextFloat(0.5f, 1f),
                    random.NextFloat(0.5f, 1f),
                    1f
                );
                
                // Create balloon
                BalloonData balloon = BalloonData.CreateDefault(position, radius);
                balloon.mass = mass;
                balloon.buoyancy = buoyancy;
                balloon.color = color;
                balloon.velocity = random.NextFloat3(-1f, 1f);
                balloon.UpdateMatrix();
                
                balloons[i] = balloon;
            }
        }
        
        void Update()
        {
            // Handle input
            HandleInput();
            
            // Update performance metrics
            UpdatePerformanceMetrics();
        }
        
        void FixedUpdate()
        {
            float deltaTime = Time.fixedDeltaTime;
            float time = Time.time;
            
            // Complete previous frame's jobs
            CompleteJobs();
            
            // Update scene colliders after jobs are complete
            CollectSceneColliders();
            
            // Update LOD levels
            var lodHandle = lodSystem.UpdateLODs(balloons, default);
            
            // Schedule spatial hash rebuild
            var buildHashJob = new BuildSpatialHashJob
            {
                balloons = balloons,
                grid = spatialGrid,
                spatialHash = spatialHash
            };
            var buildHashHandle = buildHashJob.Schedule(lodHandle);
            
            // Schedule physics job with LOD support
            var physicsJob = new BalloonPhysicsLODJob
            {
                balloons = balloons,
                parameters = simulationParameters,
                deltaTime = deltaTime,
                time = time,
                lodLevels = lodSystem.GetLODArray(),
                physicsEnabled = lodSystem.GetPhysicsEnabledArray()
            };
            physicsJobHandle = physicsJob.Schedule(balloonCount, innerLoopBatchCount, buildHashHandle);
            
            // Clear velocity deltas
            var clearDeltasJob = new ClearArrayJob<float3>
            {
                array = velocityDeltas
            };
            var clearHandle = clearDeltasJob.Schedule(balloonCount, innerLoopBatchCount, physicsJobHandle);
            
            // Schedule collision detection
            var collisionDetectionJob = new CollisionDetectionJob
            {
                balloons = balloons,
                spatialHash = spatialHash,
                grid = spatialGrid,
                collisionPairs = collisionPairs
            };
            var detectionHandle = collisionDetectionJob.Schedule(clearHandle);
            
            // Schedule collision resolution
            var collisionResolutionJob = new CollisionResolutionJob
            {
                balloons = balloons,
                collisionPairs = collisionPairs,
                parameters = simulationParameters,
                deltaTime = deltaTime,
                velocityDeltas = velocityDeltas
            };
            var collisionHandle = collisionResolutionJob.Schedule(detectionHandle);
            
            // Apply collision deltas
            var applyDeltasJob = new ApplyCollisionDeltasJob
            {
                balloons = balloons,
                velocityDeltas = velocityDeltas,
                maxVelocity = maxVelocity
            };
            var applyHandle = applyDeltasJob.Schedule(balloonCount, innerLoopBatchCount, collisionHandle);
            
            // Schedule scene collision job
            var sceneCollisionJob = new BalloonSceneCollisionJob
            {
                balloons = balloons,
                sceneColliders = sceneColliders,
                parameters = simulationParameters,
                deltaTime = deltaTime
            };
            collisionJobHandle = sceneCollisionJob.Schedule(balloonCount, innerLoopBatchCount, applyHandle);
            
            // Schedule completion for rendering
            JobHandle.ScheduleBatchedJobs();
        }
        
        void LateUpdate()
        {
            // Ensure jobs are complete before rendering
            CompleteJobs();
            
            // Update GPU buffers
            renderingSystem.UpdateBuffers(balloons, balloonCount);
            
            // Render balloons
            renderingSystem.Render();
        }
        
        void CompleteJobs()
        {
            collisionJobHandle.Complete();
            physicsJobHandle.Complete();
        }
        
        void HandleInput()
        {
            // Reset simulation
            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetSimulation();
            }
            
            // Toggle wind
            if (Input.GetKeyDown(KeyCode.W))
            {
                simulationParameters.windStrength = simulationParameters.windStrength > 0 ? 0 : 2f;
                Debug.Log($"Wind: {(simulationParameters.windStrength > 0 ? "ON" : "OFF")}");
            }
            
            // Adjust balloon count
            if (Input.GetKeyDown(KeyCode.Plus) || Input.GetKeyDown(KeyCode.Equals))
            {
                ChangeBalloonCount(Mathf.Min(balloonCount + 500, 5000));
            }
            if (Input.GetKeyDown(KeyCode.Minus))
            {
                ChangeBalloonCount(Mathf.Max(balloonCount - 500, 100));
            }
            
            // Reset camera
            if (Input.GetKeyDown(KeyCode.C))
            {
                var freeLookCamera = Camera.main?.GetComponent<BalloonFreeLookCamera>();
                if (freeLookCamera != null)
                {
                    freeLookCamera.ResetCamera();
                    Debug.Log("Camera reset");
                }
            }
        }
        
        void ResetSimulation()
        {
            CompleteJobs();
            InitializeBalloons();
            Debug.Log("Simulation reset");
        }
        
        void ChangeBalloonCount(int newCount)
        {
            if (newCount == balloonCount) return;
            
            CompleteJobs();
            
            // Dispose old arrays
            balloons.Dispose();
            velocityDeltas.Dispose();
            spatialHash.Dispose();
            collisionPairs.Dispose();
            sceneColliders.Dispose();
            
            // Update count and reinitialize
            balloonCount = newCount;
            InitializeSimulation();
            
            // Re-initialize LOD system
            if (lodSystem != null)
                lodSystem.Initialize(balloonCount);
            
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
        
        void OnGUI()
        {
            // Display performance info
            GUI.color = Color.black;
            GUI.Label(new Rect(11, 11, 300, 200), GetPerformanceText());
            GUI.color = Color.white;
            GUI.Label(new Rect(10, 10, 300, 200), GetPerformanceText());
        }
        
        string GetPerformanceText()
        {
            string lodStats = lodSystem != null ? "\n" + lodSystem.GetStatistics() : "";
            
            return $"Balloons: {balloonCount}\n" +
                   $"FPS: {averageFPS:F1}\n" +
                   $"Physics: {Time.fixedDeltaTime * 1000f:F1}ms\n" +
                   $"Scene Colliders: {(sceneColliders.IsCreated ? sceneColliders.Length : 0)}" +
                   lodStats + "\n\n" +
                   $"Controls:\n" +
                   $"  R - Reset\n" +
                   $"  W - Toggle Wind\n" +
                   $"  +/- Change Count\n" +
                   $"  C - Reset Camera\n" +
                   $"  F1 - Toggle Stats\n" +
                   $"  WASD/QE - Move\n" +
                   $"  Right Click - Look";
        }
        
        void OnDestroy()
        {
            CompleteJobs();
            
            // Dispose native collections
            if (balloons.IsCreated) balloons.Dispose();
            if (velocityDeltas.IsCreated) velocityDeltas.Dispose();
            if (spatialHash.IsCreated) spatialHash.Dispose();
            if (collisionPairs.IsCreated) collisionPairs.Dispose();
            if (sceneColliders.IsCreated) sceneColliders.Dispose();
            
            // Dispose rendering system
            renderingSystem?.Dispose();
        }
        
        /// <summary>
        /// Get a copy of current balloon data for external systems
        /// </summary>
        public bool TryGetBalloonData(out NativeArray<BalloonData> balloonData)
        {
            if (balloons.IsCreated)
            {
                balloonData = balloons;
                return true;
            }
            balloonData = default;
            return false;
        }
        
        /// <summary>
        /// Update balloon data from external systems (e.g., teleportation)
        /// </summary>
        public void UpdateBalloonData(NativeArray<BalloonData> updatedBalloons)
        {
            if (balloons.IsCreated && updatedBalloons.Length == balloons.Length)
            {
                CompleteJobs();
                updatedBalloons.CopyTo(balloons);
            }
        }
    }
    
    /// <summary>
    /// Helper job to clear arrays
    /// </summary>
    [Unity.Burst.BurstCompile]
    public struct ClearArrayJob<T> : IJobParallelFor where T : struct
    {
        public NativeArray<T> array;
        
        public void Execute(int index)
        {
            array[index] = default(T);
        }
    }
}