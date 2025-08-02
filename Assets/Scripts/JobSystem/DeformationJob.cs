using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// Job for calculating balloon deformation based on surface tension and external forces
    /// </summary>
    [BurstCompile]
    public struct DeformationJob : IJobParallelFor
    {
        // Balloon data
        [ReadOnly] public NativeArray<EnhancedBalloonData> balloonsIn;
        public NativeArray<EnhancedBalloonData> balloonsOut;
        [ReadOnly] public NativeArray<int> activeIndices;
        
        // Deformation data
        public NativeArray<DeformationData> deformationData;
        
        // Collision data
        [ReadOnly] public NativeArray<CollisionPair> collisionPairs;
        [ReadOnly] public NativeArray<int> collisionPairCount;
        
        // Spatial data
        [ReadOnly] public HierarchicalSpatialGrid spatialGrid;
        [ReadOnly] public NativeArray<int> balloonToCellMapping;
        
        // Simulation parameters
        [ReadOnly] public EnhancedSimulationParameters simParams;
        [ReadOnly] public float deltaTime;
        [ReadOnly] public float time;
        
        public void Execute(int index)
        {
            int balloonIndex = activeIndices[index];
            var balloon = balloonsIn[balloonIndex];
            var deformation = deformationData[balloonIndex];
            
            // Skip destroyed balloons
            if (balloon.state == BalloonState.Destroyed)
            {
                balloonsOut[balloonIndex] = balloon;
                return;
            }
            
            // 1. Calculate surface tension forces
            float3x3 surfaceTensionMatrix = CalculateSurfaceTensionMatrix(balloon, deformation);
            
            // 2. Apply collision deformations
            ApplyCollisionDeformations(ref balloon, ref deformation, balloonIndex);
            
            // 3. Apply velocity-based deformation (drag deformation)
            ApplyVelocityDeformation(ref balloon, ref deformation);
            
            // 4. Apply pressure-based deformation
            ApplyPressureDeformation(ref balloon, ref deformation);
            
            // 5. Calculate elastic recovery forces
            float3x3 recoveryMatrix = CalculateElasticRecovery(balloon, deformation);
            
            // 6. Combine all deformation effects
            float4x4 combinedDeformation = CombineDeformations(
                surfaceTensionMatrix,
                deformation.deformationMatrix,
                recoveryMatrix
            );
            
            // 7. Update deformation matrix with damping
            float t = deltaTime * 10f; // Deformation speed
            balloon.deformationMatrix = new float4x4(
                math.lerp(balloon.deformationMatrix.c0, combinedDeformation.c0, t),
                math.lerp(balloon.deformationMatrix.c1, combinedDeformation.c1, t),
                math.lerp(balloon.deformationMatrix.c2, combinedDeformation.c2, t),
                math.lerp(balloon.deformationMatrix.c3, combinedDeformation.c3, t)
            );
            
            // 8. Check for critical deformation (bursting)
            CheckBurstingCondition(ref balloon, deformation);
            
            // 9. Update deformation recovery
            balloon.UpdateDeformationRecovery(deltaTime);
            
            // 10. Update deformation data
            deformation.recoveryTimer -= deltaTime;
            if (deformation.recoveryTimer < 0f)
                deformation.recoveryTimer = 0f;
            
            deformation.deformationAmount = CalculateDeformationAmount(balloon.deformationMatrix);
            
            // Store updated data
            deformationData[balloonIndex] = deformation;
            balloonsOut[balloonIndex] = balloon;
        }
        
        private float3x3 CalculateSurfaceTensionMatrix(EnhancedBalloonData balloon, DeformationData deformation)
        {
            // Young-Laplace equation: ΔP = 2γ/r
            float pressureDiff = balloon.internalPressure - 101325f; // Atmospheric pressure
            float surfaceTensionPressure = 2f * balloon.surfaceTension / balloon.radius;
            
            // Surface wants to minimize area (return to sphere)
            float tensionFactor = surfaceTensionPressure / (pressureDiff + 0.001f);
            tensionFactor = math.clamp(tensionFactor, 0.1f, 2f);
            
            // Create restoration matrix based on current deformation
            float3x3 currentDeform = Extract3x3(balloon.deformationMatrix);
            float3x3 identity = float3x3.identity;
            
            float t = tensionFactor * 0.1f;
            return new float3x3(
                math.lerp(currentDeform.c0, identity.c0, t),
                math.lerp(currentDeform.c1, identity.c1, t),
                math.lerp(currentDeform.c2, identity.c2, t)
            );
        }
        
        private void ApplyCollisionDeformations(ref EnhancedBalloonData balloon, ref DeformationData deformation, int balloonIndex)
        {
            int collisionCount = collisionPairCount[0];
            float3 totalDeformAxis = float3.zero;
            float totalImpactForce = 0f;
            int relevantCollisions = 0;
            
            // Process collision pairs
            for (int i = 0; i < collisionCount && i < collisionPairs.Length; i++)
            {
                var collision = collisionPairs[i];
                
                if (collision.balloonA == balloonIndex || collision.balloonB == balloonIndex)
                {
                    float3 deformAxis = collision.contactNormal;
                    if (collision.balloonB == balloonIndex)
                        deformAxis = -deformAxis;
                    
                    totalDeformAxis += deformAxis * collision.impactForce;
                    totalImpactForce += collision.impactForce;
                    relevantCollisions++;
                    
                    // Apply immediate deformation for strong impacts
                    if (collision.impactForce > balloon.elasticity * 5f)
                    {
                        balloon.ApplyDeformation(deformAxis, collision.impactForce);
                        deformation.recoveryTimer = 1f / balloon.elasticity;
                    }
                }
            }
            
            // Apply accumulated deformation
            if (relevantCollisions > 0 && totalImpactForce > 0.01f)
            {
                float3 avgDeformAxis = math.normalize(totalDeformAxis);
                float avgImpactForce = totalImpactForce / relevantCollisions;
                
                // Update deformation velocity
                deformation.deformationVelocity = math.lerp(
                    deformation.deformationVelocity,
                    avgDeformAxis * avgImpactForce,
                    deltaTime * 5f
                );
                
                // Apply deformation based on material properties
                ApplyMaterialBasedDeformation(ref balloon, avgDeformAxis, avgImpactForce);
            }
        }
        
        private void ApplyVelocityDeformation(ref EnhancedBalloonData balloon, ref DeformationData deformation)
        {
            float speed = math.length(balloon.velocity);
            if (speed < 0.1f)
                return;
            
            float3 velocityDir = math.normalize(balloon.velocity);
            
            // Drag causes elongation in velocity direction
            float dragDeformation = speed * simParams.dragCoefficient * 0.01f;
            dragDeformation = math.clamp(dragDeformation, 0f, 0.3f);
            
            // Create stretch matrix
            float stretch = 1f + dragDeformation;
            float compress = 1f - dragDeformation * 0.5f;
            
            // Build deformation based on velocity direction
            float3x3 velocityDeform = CreateDirectionalDeformation(velocityDir, stretch, compress);
            
            // Apply to current deformation
            float3x3 current = Extract3x3(balloon.deformationMatrix);
            float3x3 combined = math.mul(current, velocityDeform);
            
            balloon.deformationMatrix = To4x4(combined);
        }
        
        private void ApplyPressureDeformation(ref EnhancedBalloonData balloon, ref DeformationData deformation)
        {
            // Internal pressure vs external pressure
            float pressureDiff = balloon.internalPressure - 101325f;
            float normalizedPressure = pressureDiff / 101325f;
            
            // Pressure causes uniform expansion/contraction
            float pressureScale = 1f + normalizedPressure * 0.1f;
            pressureScale = math.clamp(pressureScale, 0.8f, 1.3f);
            
            // Apply uniform scaling with elastic limit
            float maxDeformation = 1f / balloon.elasticity;
            float currentScale = GetUniformScale(balloon.deformationMatrix);
            
            if (math.abs(pressureScale - currentScale) > maxDeformation)
            {
                pressureScale = currentScale + math.sign(pressureScale - currentScale) * maxDeformation;
            }
            
            // Update deformation matrix
            float3x3 pressureDeform = float3x3.Scale(pressureScale);
            float3x3 current = Extract3x3(balloon.deformationMatrix);
            float3x3 lerpedMatrix = new float3x3(
                math.lerp(current.c0, pressureDeform.c0, deltaTime),
                math.lerp(current.c1, pressureDeform.c1, deltaTime),
                math.lerp(current.c2, pressureDeform.c2, deltaTime)
            );
            balloon.deformationMatrix = To4x4(lerpedMatrix);
        }
        
        private float3x3 CalculateElasticRecovery(EnhancedBalloonData balloon, DeformationData deformation)
        {
            // Hooke's law: F = -kx
            float3x3 current = Extract3x3(balloon.deformationMatrix);
            float3x3 identity = float3x3.identity;
            float3x3 deformationDelta = current - identity;
            
            // Spring constant based on elasticity
            float springConstant = balloon.elasticity * 10f;
            
            // Recovery force proportional to deformation
            float3x3 recoveryForce = -deformationDelta * springConstant;
            
            // Damping to prevent oscillation
            float damping = 0.9f;
            recoveryForce *= damping;
            
            // Apply recovery with material-specific speed
            float recoverySpeed = balloon.elasticity * 2f;
            return identity + recoveryForce * deltaTime * recoverySpeed;
        }
        
        private float4x4 CombineDeformations(float3x3 surfaceTension, float4x4 current, float3x3 recovery)
        {
            // Extract current 3x3
            float3x3 current3x3 = Extract3x3(current);
            
            // Weighted combination based on deformation state
            float deformAmount = CalculateDeformationAmount(current);
            float surfaceWeight = math.lerp(0.3f, 0.1f, deformAmount);
            float recoveryWeight = math.lerp(0.2f, 0.5f, deformAmount);
            float currentWeight = 1f - surfaceWeight - recoveryWeight;
            
            // Combine matrices
            float3x3 combined = current3x3 * currentWeight + 
                               surfaceTension * surfaceWeight + 
                               recovery * recoveryWeight;
            
            // Ensure no extreme deformations
            combined = ClampDeformation(combined, 0.5f, 1.5f);
            
            return To4x4(combined);
        }
        
        private void CheckBurstingCondition(ref EnhancedBalloonData balloon, DeformationData deformation)
        {
            float deformAmount = deformation.deformationAmount;
            
            // Check various bursting conditions
            bool overStretched = deformAmount > 1f / balloon.elasticity;
            bool overPressure = balloon.internalPressure > 150000f; // 1.5 atm
            bool criticalDamage = balloon.health < 0.1f;
            
            if (overStretched || overPressure || criticalDamage)
            {
                balloon.state = BalloonState.Bursting;
                balloon.health -= deltaTime * 2f; // Rapid health loss when bursting
                
                if (balloon.health <= 0f)
                {
                    balloon.state = BalloonState.Destroyed;
                }
            }
        }
        
        private void ApplyMaterialBasedDeformation(ref EnhancedBalloonData balloon, float3 axis, float force)
        {
            // Different materials deform differently
            float plasticThreshold = balloon.elasticity * 10f;
            
            if (force > plasticThreshold)
            {
                // Plastic deformation (permanent)
                float plasticAmount = (force - plasticThreshold) / plasticThreshold;
                plasticAmount = math.clamp(plasticAmount, 0f, 0.3f);
                
                float3x3 plasticDeform = CreateDirectionalDeformation(axis, 1f - plasticAmount, 1f + plasticAmount * 0.5f);
                float3x3 current = Extract3x3(balloon.deformationMatrix);
                balloon.deformationMatrix = To4x4(math.mul(current, plasticDeform));
                
                // Plastic deformation damages the balloon
                balloon.health -= plasticAmount * 0.1f;
            }
        }
        
        // Helper functions
        private float3x3 Extract3x3(float4x4 matrix)
        {
            return new float3x3(
                matrix.c0.xyz,
                matrix.c1.xyz,
                matrix.c2.xyz
            );
        }
        
        private float4x4 To4x4(float3x3 matrix)
        {
            return new float4x4(
                new float4(matrix.c0, 0f),
                new float4(matrix.c1, 0f),
                new float4(matrix.c2, 0f),
                new float4(0f, 0f, 0f, 1f)
            );
        }
        
        private float3x3 CreateDirectionalDeformation(float3 direction, float alongScale, float perpScale)
        {
            // Create a deformation matrix that scales along and perpendicular to a direction
            float3 d = math.normalize(direction);
            
            // Find two perpendicular vectors
            float3 perp1 = math.cross(d, new float3(0, 1, 0));
            if (math.lengthsq(perp1) < 0.001f)
                perp1 = math.cross(d, new float3(1, 0, 0));
            perp1 = math.normalize(perp1);
            
            float3 perp2 = math.normalize(math.cross(d, perp1));
            
            // Build transformation matrix
            float3x3 toBasis = new float3x3(d, perp1, perp2);
            float3x3 fromBasis = math.transpose(toBasis);
            float3x3 scale = float3x3.Scale(new float3(alongScale, perpScale, perpScale));
            
            return math.mul(math.mul(fromBasis, scale), toBasis);
        }
        
        private float GetUniformScale(float4x4 matrix)
        {
            float3 scale = new float3(
                math.length(matrix.c0.xyz),
                math.length(matrix.c1.xyz),
                math.length(matrix.c2.xyz)
            );
            return (scale.x + scale.y + scale.z) / 3f;
        }
        
        private float CalculateDeformationAmount(float4x4 matrix)
        {
            float3 scale = new float3(
                math.length(matrix.c0.xyz),
                math.length(matrix.c1.xyz),
                math.length(matrix.c2.xyz)
            );
            
            // Calculate deviation from unit scale
            float3 deviation = math.abs(scale - 1f);
            return math.length(deviation);
        }
        
        private float3x3 ClampDeformation(float3x3 matrix, float minScale, float maxScale)
        {
            // Decompose into rotation and scale
            float3 scale = new float3(
                math.length(matrix.c0),
                math.length(matrix.c1),
                math.length(matrix.c2)
            );
            
            // Clamp scales
            scale = math.clamp(scale, minScale, maxScale);
            
            // Rebuild matrix with clamped scale
            float3x3 rotation = new float3x3(
                matrix.c0 / math.length(matrix.c0),
                matrix.c1 / math.length(matrix.c1),
                matrix.c2 / math.length(matrix.c2)
            );
            
            return new float3x3(
                rotation.c0 * scale.x,
                rotation.c1 * scale.y,
                rotation.c2 * scale.z
            );
        }
    }
    
    /// <summary>
    /// Job to detect and prepare collision pairs for deformation calculation
    /// </summary>
    [BurstCompile]
    public struct CollisionDetectionForDeformationJob : IJob
    {
        [ReadOnly] public NativeArray<EnhancedBalloonData> balloons;
        [ReadOnly] public NativeArray<int> activeIndices;
        [ReadOnly] public HierarchicalSpatialGrid spatialGrid;
        
        public NativeArray<CollisionPair> collisionPairs;
        public NativeArray<int> collisionPairCount;
        
        public void Execute()
        {
            int pairCount = 0;
            int maxPairs = collisionPairs.Length;
            
            // Check all active balloon pairs
            for (int i = 0; i < activeIndices.Length; i++)
            {
                int balloonA = activeIndices[i];
                var dataA = balloons[balloonA];
                
                if (dataA.state == BalloonState.Destroyed)
                    continue;
                
                // Get nearby balloons from spatial grid
                var neighbors = new NativeList<int>(27, Allocator.Temp);
                spatialGrid.GetNeighboringCells(dataA.position, dataA.radius * 3f, 2, neighbors);
                
                for (int j = i + 1; j < activeIndices.Length && pairCount < maxPairs; j++)
                {
                    int balloonB = activeIndices[j];
                    var dataB = balloons[balloonB];
                    
                    if (dataB.state == BalloonState.Destroyed)
                        continue;
                    
                    // Check if B is in neighboring cells
                    int cellB = spatialGrid.GetCellIndex(dataB.position, 2);
                    bool isNeighbor = false;
                    for (int k = 0; k < neighbors.Length; k++)
                    {
                        if (neighbors[k] == cellB)
                        {
                            isNeighbor = true;
                            break;
                        }
                    }
                    
                    if (!isNeighbor)
                        continue;
                    
                    // Check collision
                    float3 delta = dataB.position - dataA.position;
                    float distance = math.length(delta);
                    float minDistance = dataA.radius + dataB.radius;
                    
                    if (distance < minDistance && distance > 0.001f)
                    {
                        float penetration = minDistance - distance;
                        float3 normal = delta / distance;
                        float3 contactPoint = dataA.position + normal * dataA.radius;
                        
                        // Calculate impact force based on relative velocity
                        float3 relativeVel = dataB.velocity - dataA.velocity;
                        float impactSpeed = math.dot(relativeVel, normal);
                        
                        if (impactSpeed < 0) // Moving towards each other
                        {
                            float reducedMass = (dataA.mass * dataB.mass) / (dataA.mass + dataB.mass);
                            float impactForce = math.abs(impactSpeed) * reducedMass * 10f;
                            
                            collisionPairs[pairCount] = new CollisionPair
                            {
                                balloonA = balloonA,
                                balloonB = balloonB,
                                contactPoint = contactPoint,
                                contactNormal = normal,
                                penetrationDepth = penetration,
                                impactForce = impactForce
                            };
                            
                            pairCount++;
                        }
                    }
                }
                
                neighbors.Dispose();
            }
            
            collisionPairCount[0] = pairCount;
        }
    }
}