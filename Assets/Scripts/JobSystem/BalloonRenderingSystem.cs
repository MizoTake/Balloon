using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// Handles GPU instanced indirect rendering of balloons
    /// </summary>
    public class BalloonRenderingSystem : IDisposable
    {
        // Rendering resources
        private ComputeBuffer argsBuffer;
        private ComputeBuffer matricesBuffer;
        private ComputeBuffer colorBuffer;
        
        private MaterialPropertyBlock propertyBlock;
        private Mesh balloonMesh;
        private Material balloonMaterial;
        
        // Buffer IDs for shader properties
        private static readonly int MatricesBufferId = Shader.PropertyToID("_Matrices");
        private static readonly int ColorsBufferId = Shader.PropertyToID("_Colors");
        
        // Rendering bounds
        private Bounds renderBounds;
        
        // Current capacity
        private int currentCapacity;
        
        public BalloonRenderingSystem(Mesh mesh, Material material, int initialCapacity = 1000)
        {
            balloonMesh = mesh;
            balloonMaterial = material;
            propertyBlock = new MaterialPropertyBlock();
            
            // Initialize buffers with initial capacity
            ResizeBuffers(initialCapacity);
            
            // Set up args buffer for indirect rendering
            InitializeArgsBuffer();
        }
        
        /// <summary>
        /// Resizes buffers to accommodate new balloon count
        /// </summary>
        private void ResizeBuffers(int newCapacity)
        {
            // Dispose old buffers
            matricesBuffer?.Release();
            colorBuffer?.Release();
            
            currentCapacity = newCapacity;
            
            // Create new buffers
            matricesBuffer = new ComputeBuffer(currentCapacity, sizeof(float) * 16); // float4x4
            colorBuffer = new ComputeBuffer(currentCapacity, sizeof(float) * 4); // float4
        }
        
        /// <summary>
        /// Initializes the arguments buffer for DrawMeshInstancedIndirect
        /// </summary>
        private void InitializeArgsBuffer()
        {
            argsBuffer?.Release();
            
            // Arguments for DrawMeshInstancedIndirect
            uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
            
            // Index count per instance
            args[0] = balloonMesh.GetIndexCount(0);
            // Instance count (will be updated each frame)
            args[1] = 0;
            // Start index location
            args[2] = balloonMesh.GetIndexStart(0);
            // Base vertex location
            args[3] = balloonMesh.GetBaseVertex(0);
            // Start instance location
            args[4] = 0;
            
            argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            argsBuffer.SetData(args);
        }
        
        /// <summary>
        /// Updates GPU buffers with balloon data
        /// </summary>
        public void UpdateBuffers(NativeArray<BalloonData> balloons, int balloonCount)
        {
            // Resize buffers if needed
            if (balloonCount > currentCapacity)
            {
                ResizeBuffers(Mathf.NextPowerOfTwo(balloonCount));
            }
            
            // Prepare temporary arrays for GPU upload
            var matrices = new NativeArray<float4x4>(balloonCount, Allocator.Temp);
            var colors = new NativeArray<float4>(balloonCount, Allocator.Temp);
            
            // Extract rendering data from balloons
            for (int i = 0; i < balloonCount; i++)
            {
                matrices[i] = balloons[i].matrix;
                colors[i] = balloons[i].color;
            }
            
            // Upload to GPU
            matricesBuffer.SetData(matrices, 0, 0, balloonCount);
            colorBuffer.SetData(colors, 0, 0, balloonCount);
            
            // Update instance count in args buffer
            uint[] args = new uint[] { balloonMesh.GetIndexCount(0), (uint)balloonCount, 
                                      balloonMesh.GetIndexStart(0), balloonMesh.GetBaseVertex(0), 0 };
            argsBuffer.SetData(args);
            
            // Update material property block
            propertyBlock.SetBuffer(MatricesBufferId, matricesBuffer);
            propertyBlock.SetBuffer(ColorsBufferId, colorBuffer);
            
            // Clean up temporary arrays
            matrices.Dispose();
            colors.Dispose();
        }
        
        /// <summary>
        /// Updates rendering bounds for frustum culling
        /// </summary>
        public void UpdateBounds(Bounds worldBounds)
        {
            renderBounds = worldBounds;
        }
        
        /// <summary>
        /// Renders all balloons using GPU instancing
        /// </summary>
        public void Render()
        {
            if (balloonMesh == null || balloonMaterial == null || argsBuffer == null)
                return;
            
            // Draw all balloons in a single draw call
            Graphics.DrawMeshInstancedIndirect(
                balloonMesh,
                0,
                balloonMaterial,
                renderBounds,
                argsBuffer,
                0,
                propertyBlock,
                ShadowCastingMode.On,
                true,
                0,
                null,
                LightProbeUsage.BlendProbes
            );
        }
        
        /// <summary>
        /// Renders with custom camera
        /// </summary>
        public void Render(Camera camera)
        {
            if (balloonMesh == null || balloonMaterial == null || argsBuffer == null)
                return;
            
            Graphics.DrawMeshInstancedIndirect(
                balloonMesh,
                0,
                balloonMaterial,
                renderBounds,
                argsBuffer,
                0,
                propertyBlock,
                ShadowCastingMode.On,
                true,
                0,
                camera,
                LightProbeUsage.BlendProbes
            );
        }
        
        /// <summary>
        /// Clean up GPU resources
        /// </summary>
        public void Dispose()
        {
            argsBuffer?.Release();
            matricesBuffer?.Release();
            colorBuffer?.Release();
            
            argsBuffer = null;
            matricesBuffer = null;
            colorBuffer = null;
        }
    }
}