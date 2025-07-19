using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// Enhanced rendering system supporting 50000+ balloons with GPU instancing
    /// Implements LOD, frustum culling, and adaptive quality
    /// </summary>
    public class EnhancedRenderingSystem : MonoBehaviour
    {
        [Header("Rendering Configuration")]
        [SerializeField] private Material balloonMaterial;
        [SerializeField] private Mesh balloonMesh;
        [SerializeField] private ComputeShader physicsComputeShader;
        [SerializeField] private int maxInstanceCount = 50000;
        
        [Header("LOD Settings")]
        [SerializeField] private float lodDistance0 = 25f;  // High detail
        [SerializeField] private float lodDistance1 = 50f;  // Medium detail
        [SerializeField] private float lodDistance2 = 100f; // Low detail
        [SerializeField] private float cullingDistance = 200f;
        
        [Header("Performance")]
        [SerializeField] private bool enableGPUFrustumCulling = true;
        [SerializeField] private bool enableOcclusionCulling = false;
        [SerializeField] private int instancesPerBatch = 1023; // GPU limit
        
        // GPU Buffers
        private ComputeBuffer transformBuffer;
        private ComputeBuffer colorBuffer;
        private ComputeBuffer materialPropertyBuffer;
        private ComputeBuffer argsBuffer;
        private ComputeBuffer visibilityBuffer;
        private ComputeBuffer lodBuffer;
        
        // Culling data
        private ComputeBuffer frustumPlanesBuffer;
        private ComputeShader cullingComputeShader;
        
        // Rendering data
        private MaterialPropertyBlock materialPropertyBlock;
        private Matrix4x4[] transformMatrices;
        private Vector4[] colorData;
        private Vector4[] materialData;
        private uint[] argsArray;
        
        // LOD system
        private Mesh[] lodMeshes;
        private Material[] lodMaterials;
        private int[] lodInstanceCounts;
        
        // Camera reference
        private Camera mainCamera;
        private Plane[] frustumPlanes;
        
        // Performance monitoring
        private int lastFrameInstanceCount;
        private float lastFrameRenderTime;
        
        public int RenderedInstanceCount => lastFrameInstanceCount;
        public float LastRenderTime => lastFrameRenderTime;
        
        private void Awake()
        {
            InitializeBuffers();
            InitializeLODSystem();
            SetupMaterialPropertyBlock();
            LoadCullingComputeShader();
            
            mainCamera = Camera.main;
            if (mainCamera == null)
                mainCamera = FindObjectOfType<Camera>();
        }
        
        private void InitializeBuffers()
        {
            // Transform matrices (4x4 = 16 floats per instance)
            transformBuffer = new ComputeBuffer(maxInstanceCount, sizeof(float) * 16);
            
            // Color data (RGBA)
            colorBuffer = new ComputeBuffer(maxInstanceCount, sizeof(float) * 4);
            
            // Material properties (metallic, roughness, transparency, lodLevel)
            materialPropertyBuffer = new ComputeBuffer(maxInstanceCount, sizeof(float) * 4);
            
            // Visibility buffer (1 float per instance - 0=hidden, 1=visible)
            visibilityBuffer = new ComputeBuffer(maxInstanceCount, sizeof(float));
            
            // LOD buffer (LOD level per instance)
            lodBuffer = new ComputeBuffer(maxInstanceCount, sizeof(int));
            
            // Arguments for DrawMeshInstancedIndirect
            argsArray = new uint[5] { 0, 0, 0, 0, 0 };
            argsBuffer = new ComputeBuffer(1, sizeof(uint) * 5, ComputeBufferType.IndirectArguments);
            
            // Frustum planes for culling
            frustumPlanesBuffer = new ComputeBuffer(6, sizeof(float) * 4);
            
            // Initialize arrays
            transformMatrices = new Matrix4x4[maxInstanceCount];
            colorData = new Vector4[maxInstanceCount];
            materialData = new Vector4[maxInstanceCount];
            
            Debug.Log($"[EnhancedRenderingSystem] Initialized buffers for {maxInstanceCount} instances");
        }
        
        private void InitializeLODSystem()
        {
            // Create LOD meshes with different detail levels
            lodMeshes = new Mesh[3];
            lodMaterials = new Material[3];
            lodInstanceCounts = new int[3];
            
            // LOD 0 - High detail (original mesh)
            lodMeshes[0] = balloonMesh;
            lodMaterials[0] = balloonMaterial;
            
            // LOD 1 - Medium detail (simplified mesh)
            lodMeshes[1] = CreateSimplifiedMesh(balloonMesh, 0.6f);
            lodMaterials[1] = new Material(balloonMaterial);
            lodMaterials[1].SetFloat("_LODLevel", 1f);
            
            // LOD 2 - Low detail (very simple mesh)
            lodMeshes[2] = CreateSimplifiedMesh(balloonMesh, 0.3f);
            lodMaterials[2] = new Material(balloonMaterial);
            lodMaterials[2].SetFloat("_LODLevel", 2f);
            
            Debug.Log("[EnhancedRenderingSystem] LOD system initialized");
        }
        
        private Mesh CreateSimplifiedMesh(Mesh originalMesh, float simplificationRatio)
        {
            // Simple mesh decimation - in production, use proper mesh simplification
            var vertices = originalMesh.vertices;
            var triangles = originalMesh.triangles;
            var normals = originalMesh.normals;
            var uvs = originalMesh.uv;
            
            int targetVertexCount = Mathf.RoundToInt(vertices.Length * simplificationRatio);
            int step = vertices.Length / targetVertexCount;
            step = Mathf.Max(1, step);
            
            var newVertices = new Vector3[targetVertexCount];
            var newNormals = new Vector3[targetVertexCount];
            var newUvs = new Vector2[targetVertexCount];
            
            for (int i = 0; i < targetVertexCount; i++)
            {
                int originalIndex = i * step;
                if (originalIndex >= vertices.Length)
                    originalIndex = vertices.Length - 1;
                
                newVertices[i] = vertices[originalIndex];
                newNormals[i] = normals[originalIndex];
                newUvs[i] = uvs[originalIndex];
            }
            
            // Simplified triangle generation (not optimal, but functional)
            var newTriangles = new int[targetVertexCount * 3];
            for (int i = 0; i < targetVertexCount - 2; i += 3)
            {
                newTriangles[i * 3] = i;
                newTriangles[i * 3 + 1] = i + 1;
                newTriangles[i * 3 + 2] = i + 2;
            }
            
            var mesh = new Mesh();
            mesh.vertices = newVertices;
            mesh.triangles = newTriangles;
            mesh.normals = newNormals;
            mesh.uv = newUvs;
            mesh.RecalculateBounds();
            
            return mesh;
        }
        
        private void SetupMaterialPropertyBlock()
        {
            materialPropertyBlock = new MaterialPropertyBlock();
        }
        
        private void LoadCullingComputeShader()
        {
            // Load or create culling compute shader
            cullingComputeShader = Resources.Load<ComputeShader>("BalloonCullingCompute");
            if (cullingComputeShader == null)
            {
                Debug.LogWarning("[EnhancedRenderingSystem] Culling compute shader not found. Frustum culling disabled.");
                enableGPUFrustumCulling = false;
            }
        }
        
        public void UpdateRenderingData(NativeArray<EnhancedBalloonData> balloonData, NativeArray<int> activeIndices)
        {
            float startTime = Time.realtimeSinceStartup;
            
            // Update camera frustum planes
            if (enableGPUFrustumCulling && mainCamera != null)
            {
                UpdateFrustumPlanes();
            }
            
            // Process balloon data for rendering
            int visibleCount = ProcessBalloonData(balloonData, activeIndices);
            
            // Update GPU buffers
            UpdateGPUBuffers(visibleCount);
            
            // Perform culling if enabled
            if (enableGPUFrustumCulling)
            {
                PerformGPUCulling(visibleCount);
            }
            
            lastFrameInstanceCount = visibleCount;
            lastFrameRenderTime = (Time.realtimeSinceStartup - startTime) * 1000f; // Convert to ms
        }
        
        private void UpdateFrustumPlanes()
        {
            frustumPlanes = GeometryUtility.CalculateFrustumPlanes(mainCamera);
            
            Vector4[] planeData = new Vector4[6];
            for (int i = 0; i < 6; i++)
            {
                planeData[i] = new Vector4(
                    frustumPlanes[i].normal.x,
                    frustumPlanes[i].normal.y,
                    frustumPlanes[i].normal.z,
                    frustumPlanes[i].distance
                );
            }
            
            frustumPlanesBuffer.SetData(planeData);
        }
        
        private int ProcessBalloonData(NativeArray<EnhancedBalloonData> balloonData, NativeArray<int> activeIndices)
        {
            int visibleCount = 0;
            Vector3 cameraPos = mainCamera != null ? mainCamera.transform.position : Vector3.zero;
            
            // Reset LOD counts
            for (int i = 0; i < lodInstanceCounts.Length; i++)
                lodInstanceCounts[i] = 0;
            
            for (int i = 0; i < activeIndices.Length && visibleCount < maxInstanceCount; i++)
            {
                int balloonIndex = activeIndices[i];
                if (balloonIndex < 0 || balloonIndex >= balloonData.Length)
                    continue;
                
                var balloon = balloonData[balloonIndex];
                
                // Skip destroyed balloons
                if (balloon.state == BalloonState.Destroyed)
                    continue;
                
                // Calculate distance to camera
                float distance = Vector3.Distance(balloon.position, cameraPos);
                
                // Distance culling
                if (distance > cullingDistance)
                    continue;
                
                // Determine LOD level
                int lodLevel = GetLODLevel(distance);
                balloon.lodLevel = lodLevel;
                
                // Store rendering data
                transformMatrices[visibleCount] = balloon.transformMatrix;
                colorData[visibleCount] = balloon.baseColor;
                materialData[visibleCount] = new Vector4(
                    balloon.metallic,
                    balloon.roughness,
                    balloon.transparency,
                    lodLevel
                );
                
                lodInstanceCounts[lodLevel]++;
                visibleCount++;
            }
            
            return visibleCount;
        }
        
        private int GetLODLevel(float distance)
        {
            if (distance < lodDistance0)
                return 0; // High detail
            else if (distance < lodDistance1)
                return 1; // Medium detail
            else
                return 2; // Low detail
        }
        
        private void UpdateGPUBuffers(int instanceCount)
        {
            if (instanceCount == 0)
                return;
            
            // Update buffers with only the visible instances
            transformBuffer.SetData(transformMatrices, 0, 0, instanceCount);
            colorBuffer.SetData(colorData, 0, 0, instanceCount);
            materialPropertyBuffer.SetData(materialData, 0, 0, instanceCount);
            
            // Update material property block
            materialPropertyBlock.SetBuffer("_TransformBuffer", transformBuffer);
            materialPropertyBlock.SetBuffer("_ColorBuffer", colorBuffer);
            materialPropertyBlock.SetBuffer("_MaterialPropertyBuffer", materialPropertyBuffer);
        }
        
        private void PerformGPUCulling(int instanceCount)
        {
            if (cullingComputeShader == null || instanceCount == 0)
                return;
            
            // Set compute shader data
            int kernelIndex = cullingComputeShader.FindKernel("FrustumCulling");
            cullingComputeShader.SetBuffer(kernelIndex, "_TransformBuffer", transformBuffer);
            cullingComputeShader.SetBuffer(kernelIndex, "_VisibilityBuffer", visibilityBuffer);
            cullingComputeShader.SetBuffer(kernelIndex, "_FrustumPlanes", frustumPlanesBuffer);
            cullingComputeShader.SetInt("_InstanceCount", instanceCount);
            
            // Dispatch culling compute
            int threadGroups = Mathf.CeilToInt(instanceCount / 64.0f);
            cullingComputeShader.Dispatch(kernelIndex, threadGroups, 1, 1);
            
            // Update material with visibility buffer
            materialPropertyBlock.SetBuffer("_VisibilityBuffer", visibilityBuffer);
        }
        
        public void RenderBalloons()
        {
            if (balloonMesh == null || balloonMaterial == null || lastFrameInstanceCount == 0)
                return;
            
            // Render each LOD level separately
            for (int lodLevel = 0; lodLevel < 3; lodLevel++)
            {
                if (lodInstanceCounts[lodLevel] == 0)
                    continue;
                
                Mesh meshToRender = lodMeshes[lodLevel];
                Material materialToRender = lodMaterials[lodLevel];
                
                // Update args buffer for this LOD
                argsArray[0] = meshToRender.GetIndexCount(0);
                argsArray[1] = (uint)lodInstanceCounts[lodLevel];
                argsArray[2] = meshToRender.GetIndexStart(0);
                argsArray[3] = meshToRender.GetBaseVertex(0);
                argsArray[4] = 0;
                argsBuffer.SetData(argsArray);
                
                // Render with GPU instancing
                Graphics.DrawMeshInstancedIndirect(
                    meshToRender,
                    0,
                    materialToRender,
                    new Bounds(Vector3.zero, Vector3.one * 1000f), // Large bounds for safety
                    argsBuffer,
                    0,
                    materialPropertyBlock,
                    ShadowCastingMode.On,
                    true,
                    0,
                    mainCamera
                );
            }
        }
        
        private void Update()
        {
            // Performance monitoring
            if (Time.frameCount % 60 == 0) // Log every 60 frames
            {
                LogPerformanceStats();
            }
        }
        
        private void LogPerformanceStats()
        {
            float renderTimeMs = lastFrameRenderTime;
            int instances = lastFrameInstanceCount;
            
            string lodBreakdown = $"LOD0: {lodInstanceCounts[0]}, LOD1: {lodInstanceCounts[1]}, LOD2: {lodInstanceCounts[2]}";
            
            Debug.Log($"[EnhancedRenderingSystem] Rendered {instances} instances in {renderTimeMs:F2}ms. {lodBreakdown}");
            
            // Performance warnings
            if (renderTimeMs > 10f)
            {
                Debug.LogWarning($"[EnhancedRenderingSystem] High render time: {renderTimeMs:F2}ms. Consider reducing instance count or LOD distances.");
            }
        }
        
        private void OnDestroy()
        {
            // Clean up GPU buffers
            transformBuffer?.Dispose();
            colorBuffer?.Dispose();
            materialPropertyBuffer?.Dispose();
            argsBuffer?.Dispose();
            visibilityBuffer?.Dispose();
            lodBuffer?.Dispose();
            frustumPlanesBuffer?.Dispose();
            
            // Clean up LOD materials
            if (lodMaterials != null)
            {
                for (int i = 1; i < lodMaterials.Length; i++) // Skip original material
                {
                    if (lodMaterials[i] != null)
                        DestroyImmediate(lodMaterials[i]);
                }
            }
            
            // Clean up LOD meshes
            if (lodMeshes != null)
            {
                for (int i = 1; i < lodMeshes.Length; i++) // Skip original mesh
                {
                    if (lodMeshes[i] != null)
                        DestroyImmediate(lodMeshes[i]);
                }
            }
        }
        
        // Public API for external systems
        public void SetMaxInstanceCount(int count)
        {
            if (count != maxInstanceCount)
            {
                maxInstanceCount = count;
                OnDestroy(); // Clean up old buffers
                InitializeBuffers(); // Recreate with new size
            }
        }
        
        public void SetLODDistances(float lod0, float lod1, float lod2, float culling)
        {
            lodDistance0 = lod0;
            lodDistance1 = lod1;
            lodDistance2 = lod2;
            cullingDistance = culling;
        }
        
        public void EnableGPUCulling(bool enable)
        {
            enableGPUFrustumCulling = enable && cullingComputeShader != null;
        }
        
        // Editor debugging
        private void OnDrawGizmosSelected()
        {
            if (mainCamera == null)
                return;
            
            // Draw LOD distance spheres
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(mainCamera.transform.position, lodDistance0);
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(mainCamera.transform.position, lodDistance1);
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(mainCamera.transform.position, lodDistance2);
            
            Gizmos.color = Color.black;
            Gizmos.DrawWireSphere(mainCamera.transform.position, cullingDistance);
        }
    }
}