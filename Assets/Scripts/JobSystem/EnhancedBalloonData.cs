using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// Enhanced balloon data structure for advanced physics simulation
    /// Supports deformation, material properties, and visual effects
    /// </summary>
    [System.Serializable]
    public struct EnhancedBalloonData
    {
        // Basic physics data
        public float3 position;
        public float3 velocity;
        public float3 acceleration;
        public quaternion rotation;
        public float3 angularVelocity;
        
        // Shape and material data
        public float radius;
        public float mass;
        public float buoyancy;
        public float elasticity;
        public float viscosity;
        
        // Deformation data
        public float4x4 deformationMatrix;
        public float surfaceTension;
        public float internalPressure;
        
        // Visual data
        public float4 baseColor;
        public float metallic;
        public float roughness;
        public float transparency;
        public float4x4 transformMatrix;
        
        // State data
        public BalloonState state;
        public float health;
        public float age;
        
        // LOD and optimization data
        public int lodLevel;
        public float distanceToCamera;
        
        /// <summary>
        /// Creates a new enhanced balloon with default parameters
        /// </summary>
        public static EnhancedBalloonData CreateDefault(float3 position, float radius = 0.5f)
        {
            return new EnhancedBalloonData
            {
                position = position,
                velocity = float3.zero,
                acceleration = float3.zero,
                rotation = quaternion.identity,
                angularVelocity = float3.zero,
                
                radius = radius,
                mass = 0.005f, // 5 grams
                buoyancy = 0.015f, // Helium buoyancy
                elasticity = 0.8f,
                viscosity = 0.001f,
                
                deformationMatrix = float4x4.identity,
                surfaceTension = 0.0728f, // N/m for rubber
                internalPressure = 101325f, // 1 atm in Pa
                
                baseColor = new float4(1f, 1f, 1f, 1f),
                metallic = 0.1f,
                roughness = 0.5f,
                transparency = 0.8f,
                transformMatrix = float4x4.TRS(position, quaternion.identity, new float3(radius * 2f)),
                
                state = BalloonState.Normal,
                health = 1.0f,
                age = 0.0f,
                
                lodLevel = 0,
                distanceToCamera = 0f
            };
        }
        
        /// <summary>
        /// Creates from existing BalloonData (migration helper)
        /// </summary>
        public static EnhancedBalloonData FromBalloonData(BalloonData oldData)
        {
            var enhanced = CreateDefault(oldData.position, oldData.radius);
            enhanced.velocity = oldData.velocity;
            enhanced.acceleration = oldData.acceleration;
            enhanced.mass = oldData.mass;
            enhanced.buoyancy = oldData.buoyancy;
            enhanced.baseColor = oldData.color;
            enhanced.transformMatrix = oldData.matrix;
            return enhanced;
        }
        
        /// <summary>
        /// Updates the transform matrix with position, rotation, scale, and deformation
        /// </summary>
        public void UpdateMatrix()
        {
            var baseTransform = float4x4.TRS(position, rotation, new float3(radius * 2f));
            transformMatrix = math.mul(baseTransform, deformationMatrix);
        }
        
        /// <summary>
        /// Applies deformation based on forces and collisions
        /// </summary>
        public void ApplyDeformation(float3 contactNormal, float impactForce)
        {
            float deformationStrength = math.saturate(impactForce / (elasticity * 10f));
            float3 deformAxis = math.normalize(contactNormal);
            
            // Simple squash deformation along impact axis
            float squashFactor = 1f - deformationStrength * 0.3f;
            float stretchFactor = 1f + deformationStrength * 0.15f;
            
            float3 scale = new float3(
                math.lerp(1f, contactNormal.x > 0.5f ? squashFactor : stretchFactor, math.abs(contactNormal.x)),
                math.lerp(1f, contactNormal.y > 0.5f ? squashFactor : stretchFactor, math.abs(contactNormal.y)),
                math.lerp(1f, contactNormal.z > 0.5f ? squashFactor : stretchFactor, math.abs(contactNormal.z))
            );
            
            deformationMatrix = float4x4.Scale(scale);
        }
        
        /// <summary>
        /// Updates deformation recovery based on elasticity
        /// </summary>
        public void UpdateDeformationRecovery(float deltaTime)
        {
            // Gradually return to identity matrix
            float recoverySpeed = elasticity * 5f;
            float t = deltaTime * recoverySpeed;
            deformationMatrix = new float4x4(
                math.lerp(deformationMatrix.c0, float4x4.identity.c0, t),
                math.lerp(deformationMatrix.c1, float4x4.identity.c1, t),
                math.lerp(deformationMatrix.c2, float4x4.identity.c2, t),
                math.lerp(deformationMatrix.c3, float4x4.identity.c3, t)
            );
        }
        
        /// <summary>
        /// Calculates effective buoyancy considering temperature and altitude
        /// </summary>
        public float CalculateEffectiveBuoyancy(float temperature, float altitude)
        {
            // Air density decreases with altitude
            float altitudeFactor = math.exp(-altitude * 0.00012f);
            // Ideal gas law: density inversely proportional to temperature
            float temperatureFactor = 288.15f / (temperature + 273.15f);
            
            return buoyancy * altitudeFactor * temperatureFactor;
        }
        
        /// <summary>
        /// Updates balloon state based on health and age
        /// </summary>
        public void UpdateState()
        {
            if (health <= 0f)
            {
                state = BalloonState.Destroyed;
            }
            else if (health < 0.3f)
            {
                state = BalloonState.Bursting;
            }
            else if (math.length(deformationMatrix[0].xyz) < 0.8f || 
                     math.length(deformationMatrix[1].xyz) < 0.8f || 
                     math.length(deformationMatrix[2].xyz) < 0.8f)
            {
                state = BalloonState.Deforming;
            }
            else
            {
                state = BalloonState.Normal;
            }
        }
    }
    
    /// <summary>
    /// Balloon state enumeration
    /// </summary>
    public enum BalloonState
    {
        Normal,
        Deforming,
        Bursting,
        Destroyed
    }
    
    /// <summary>
    /// Enhanced simulation parameters with fluid dynamics
    /// </summary>
    [System.Serializable]
    public struct EnhancedSimulationParameters
    {
        // Basic physics
        public float gravity;
        public float airDensity;
        public float windStrength;
        public float3 windDirection;
        public float damping;
        public float collisionElasticity;
        public Bounds worldBounds;
        
        // Fluid dynamics
        public float viscosityCoefficient;
        public float dragCoefficient;
        public float turbulenceStrength;
        public float vortexStrength;
        
        // Environmental
        public float temperature; // Celsius
        public float altitude; // Meters
        public float humidity; // Percentage
        
        // Performance
        public int maxBalloonCount;
        public float physicsTimeStep;
        public float lodDistanceThreshold;
        
        /// <summary>
        /// Creates enhanced default parameters
        /// </summary>
        public static EnhancedSimulationParameters CreateDefault()
        {
            return new EnhancedSimulationParameters
            {
                gravity = 9.81f,
                airDensity = 1.225f,
                windStrength = 2.0f,
                windDirection = new float3(1f, 0f, 0f),
                damping = 0.98f,
                collisionElasticity = 0.8f,
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 100f),
                
                viscosityCoefficient = 0.00001f,
                dragCoefficient = 0.47f, // Sphere drag coefficient
                turbulenceStrength = 0.1f,
                vortexStrength = 0.05f,
                
                temperature = 20f,
                altitude = 0f,
                humidity = 50f,
                
                maxBalloonCount = 50000,
                physicsTimeStep = 0.025f,
                lodDistanceThreshold = 50f
            };
        }
        
        /// <summary>
        /// Creates from old parameters (migration helper)
        /// </summary>
        public static EnhancedSimulationParameters FromSimulationParameters(SimulationParameters oldParams)
        {
            var enhanced = CreateDefault();
            enhanced.gravity = oldParams.gravity;
            enhanced.airDensity = oldParams.airDensity;
            enhanced.windStrength = oldParams.windStrength;
            enhanced.windDirection = oldParams.windDirection;
            enhanced.damping = oldParams.damping;
            enhanced.collisionElasticity = oldParams.collisionElasticity;
            enhanced.worldBounds = oldParams.worldBounds;
            return enhanced;
        }
    }
}