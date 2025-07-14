using Unity.Mathematics;
using Unity.Collections;
using UnityEngine;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// Types of colliders supported in the simulation
    /// </summary>
    public enum ColliderType
    {
        Box = 0,
        Sphere = 1,
        Capsule = 2,
        Mesh = 3
    }
    
    /// <summary>
    /// Data structure for scene colliders that can be used in jobs
    /// </summary>
    [System.Serializable]
    public struct SceneColliderData
    {
        public ColliderType type;
        public float4x4 localToWorld;
        public float4x4 worldToLocal;
        public float3 center;
        public float3 size; // For box: half extents, For sphere: radius in x, For capsule: radius in x, height in y
        public int direction; // For capsule: 0=X, 1=Y, 2=Z
        
        /// <summary>
        /// Creates collider data from a Unity BoxCollider
        /// </summary>
        public static SceneColliderData FromBoxCollider(BoxCollider box)
        {
            Transform transform = box.transform;
            return new SceneColliderData
            {
                type = ColliderType.Box,
                localToWorld = float4x4.TRS(transform.position, transform.rotation, transform.lossyScale),
                worldToLocal = math.inverse(float4x4.TRS(transform.position, transform.rotation, transform.lossyScale)),
                center = box.center,
                size = box.size * 0.5f, // Store half extents
                direction = 0
            };
        }
        
        /// <summary>
        /// Creates collider data from a Unity SphereCollider
        /// </summary>
        public static SceneColliderData FromSphereCollider(SphereCollider sphere)
        {
            Transform transform = sphere.transform;
            float maxScale = math.max(math.max(transform.lossyScale.x, transform.lossyScale.y), transform.lossyScale.z);
            return new SceneColliderData
            {
                type = ColliderType.Sphere,
                localToWorld = float4x4.TRS(transform.position, transform.rotation, transform.lossyScale),
                worldToLocal = math.inverse(float4x4.TRS(transform.position, transform.rotation, transform.lossyScale)),
                center = sphere.center,
                size = new float3(sphere.radius * maxScale, 0, 0),
                direction = 0
            };
        }
        
        /// <summary>
        /// Creates collider data from a Unity CapsuleCollider
        /// </summary>
        public static SceneColliderData FromCapsuleCollider(CapsuleCollider capsule)
        {
            Transform transform = capsule.transform;
            return new SceneColliderData
            {
                type = ColliderType.Capsule,
                localToWorld = float4x4.TRS(transform.position, transform.rotation, transform.lossyScale),
                worldToLocal = math.inverse(float4x4.TRS(transform.position, transform.rotation, transform.lossyScale)),
                center = capsule.center,
                size = new float3(capsule.radius, capsule.height * 0.5f, 0),
                direction = capsule.direction
            };
        }
        
        /// <summary>
        /// Tests if a sphere collides with this collider
        /// </summary>
        public bool TestSphereCollision(float3 sphereWorldPos, float sphereRadius, out float3 pushDirection, out float penetrationDepth)
        {
            switch (type)
            {
                case ColliderType.Box:
                    return TestSphereVsBox(sphereWorldPos, sphereRadius, out pushDirection, out penetrationDepth);
                case ColliderType.Sphere:
                    return TestSphereVsSphere(sphereWorldPos, sphereRadius, out pushDirection, out penetrationDepth);
                case ColliderType.Capsule:
                    return TestSphereVsCapsule(sphereWorldPos, sphereRadius, out pushDirection, out penetrationDepth);
                default:
                    pushDirection = float3.zero;
                    penetrationDepth = 0;
                    return false;
            }
        }
        
        private bool TestSphereVsBox(float3 sphereWorldPos, float sphereRadius, out float3 pushDirection, out float penetrationDepth)
        {
            // Transform sphere position to box local space
            float4 localPos = math.mul(worldToLocal, new float4(sphereWorldPos, 1.0f));
            float3 sphereLocal = localPos.xyz;
            
            // Account for box center
            sphereLocal -= center;
            
            // Find closest point on box to sphere center
            float3 closestPoint = math.clamp(sphereLocal, -size, size);
            
            // Calculate distance from sphere center to closest point
            float3 delta = sphereLocal - closestPoint;
            float distanceSq = math.lengthsq(delta);
            
            if (distanceSq < sphereRadius * sphereRadius)
            {
                float distance = math.sqrt(distanceSq);
                penetrationDepth = sphereRadius - distance;
                
                // Calculate push direction in world space
                if (distance > 0.0001f)
                {
                    float3 localNormal = delta / distance;
                    pushDirection = math.normalize(math.mul(localToWorld, new float4(localNormal, 0.0f)).xyz);
                }
                else
                {
                    // Sphere center is inside box, push out along smallest axis
                    float3 absLocal = math.abs(sphereLocal);
                    float3 penetrations = size - absLocal + sphereRadius;
                    
                    if (penetrations.x < penetrations.y && penetrations.x < penetrations.z)
                    {
                        pushDirection = math.mul(localToWorld, new float4(math.sign(sphereLocal.x), 0, 0, 0)).xyz;
                        penetrationDepth = penetrations.x;
                    }
                    else if (penetrations.y < penetrations.z)
                    {
                        pushDirection = math.mul(localToWorld, new float4(0, math.sign(sphereLocal.y), 0, 0)).xyz;
                        penetrationDepth = penetrations.y;
                    }
                    else
                    {
                        pushDirection = math.mul(localToWorld, new float4(0, 0, math.sign(sphereLocal.z), 0)).xyz;
                        penetrationDepth = penetrations.z;
                    }
                }
                
                return true;
            }
            
            pushDirection = float3.zero;
            penetrationDepth = 0;
            return false;
        }
        
        private bool TestSphereVsSphere(float3 sphereWorldPos, float sphereRadius, out float3 pushDirection, out float penetrationDepth)
        {
            float3 worldCenter = math.mul(localToWorld, new float4(center, 1.0f)).xyz;
            float3 delta = sphereWorldPos - worldCenter;
            float distance = math.length(delta);
            float combinedRadius = sphereRadius + size.x;
            
            if (distance < combinedRadius)
            {
                penetrationDepth = combinedRadius - distance;
                pushDirection = distance > 0.0001f ? delta / distance : new float3(0, 1, 0);
                return true;
            }
            
            pushDirection = float3.zero;
            penetrationDepth = 0;
            return false;
        }
        
        private bool TestSphereVsCapsule(float3 sphereWorldPos, float sphereRadius, out float3 pushDirection, out float penetrationDepth)
        {
            // Transform sphere position to capsule local space
            float4 localPos = math.mul(worldToLocal, new float4(sphereWorldPos, 1.0f));
            float3 sphereLocal = localPos.xyz - center;
            
            // Get capsule axis
            float3 capsuleAxis = float3.zero;
            capsuleAxis[direction] = 1;
            
            // Project sphere position onto capsule axis
            float projection = math.dot(sphereLocal, capsuleAxis);
            projection = math.clamp(projection, -size.y, size.y);
            
            // Find closest point on capsule axis
            float3 closestPointOnAxis = capsuleAxis * projection;
            
            // Calculate distance from sphere to axis
            float3 delta = sphereLocal - closestPointOnAxis;
            float distance = math.length(delta);
            float combinedRadius = sphereRadius + size.x;
            
            if (distance < combinedRadius)
            {
                penetrationDepth = combinedRadius - distance;
                
                if (distance > 0.0001f)
                {
                    float3 localNormal = delta / distance;
                    pushDirection = math.normalize(math.mul(localToWorld, new float4(localNormal, 0.0f)).xyz);
                }
                else
                {
                    pushDirection = new float3(0, 1, 0);
                }
                
                return true;
            }
            
            pushDirection = float3.zero;
            penetrationDepth = 0;
            return false;
        }
    }
}