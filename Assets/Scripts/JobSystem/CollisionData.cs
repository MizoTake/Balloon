using Unity.Mathematics;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// Represents a collision between two balloons
    /// </summary>
    [System.Serializable]
    public struct CollisionPair
    {
        // Basic collision indices
        public int indexA;
        public int indexB;
        public int balloonA;
        public int balloonB;
        
        // Distance information
        public float distance;
        public float minDistance;
        
        // Detailed collision information
        public float3 contactPoint;
        public float3 contactNormal;
        public float penetrationDepth;
        public float impactForce;
        
        /// <summary>
        /// Creates a simple collision pair from indices and distance
        /// </summary>
        public static CollisionPair Create(int indexA, int indexB, float distance, float minDistance)
        {
            return new CollisionPair
            {
                indexA = indexA,
                indexB = indexB,
                balloonA = indexA,
                balloonB = indexB,
                distance = distance,
                minDistance = minDistance,
                contactPoint = float3.zero,
                contactNormal = float3.zero,
                penetrationDepth = 0f,
                impactForce = 0f
            };
        }
        
        /// <summary>
        /// Creates a detailed collision pair with contact information
        /// </summary>
        public static CollisionPair CreateDetailed(int balloonA, int balloonB, float3 contactPoint, 
            float3 contactNormal, float penetrationDepth, float impactForce)
        {
            return new CollisionPair
            {
                indexA = balloonA,
                indexB = balloonB,
                balloonA = balloonA,
                balloonB = balloonB,
                distance = penetrationDepth,
                minDistance = 0f,
                contactPoint = contactPoint,
                contactNormal = contactNormal,
                penetrationDepth = penetrationDepth,
                impactForce = impactForce
            };
        }
    }
}