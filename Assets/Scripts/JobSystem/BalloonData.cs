using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// Structure representing a single balloon's data for JobSystem processing
    /// </summary>
    [System.Serializable]
    public struct BalloonData
    {
        public float3 position;
        public float3 velocity;
        public float3 acceleration;
        public float radius;
        public float mass;
        public float buoyancy;
        public float4 color;
        public float4x4 matrix; // Transform Matrix for Indirect Rendering
        
        /// <summary>
        /// Creates a new balloon with default parameters
        /// </summary>
        public static BalloonData CreateDefault(float3 position, float radius = 0.5f)
        {
            return new BalloonData
            {
                position = position,
                velocity = float3.zero,
                acceleration = float3.zero,
                radius = radius,
                mass = 0.005f, // 5 grams (typical balloon mass)
                buoyancy = 0.015f, // Helium buoyancy factor
                color = new float4(1f, 1f, 1f, 1f),
                matrix = float4x4.TRS(position, quaternion.identity, new float3(radius * 2f))
            };
        }
        
        /// <summary>
        /// Updates the transform matrix based on current position and radius
        /// </summary>
        public void UpdateMatrix()
        {
            matrix = float4x4.TRS(position, quaternion.identity, new float3(radius * 2f));
        }
    }
    
    /// <summary>
    /// Simulation parameters shared across all jobs
    /// </summary>
    [System.Serializable]
    public struct SimulationParameters
    {
        public float gravity;
        public float airDensity;
        public float windStrength;
        public float3 windDirection;
        public float damping;
        public float collisionElasticity;
        public Bounds worldBounds;
        
        /// <summary>
        /// Creates default simulation parameters
        /// </summary>
        public static SimulationParameters CreateDefault()
        {
            return new SimulationParameters
            {
                gravity = 9.81f,
                airDensity = 1.225f, // kg/m³ at sea level
                windStrength = 2.0f,
                windDirection = new float3(1f, 0f, 0f),
                damping = 0.98f,
                collisionElasticity = 0.8f,
                worldBounds = new Bounds(UnityEngine.Vector3.zero, UnityEngine.Vector3.one * 100f)
            };
        }
    }
}