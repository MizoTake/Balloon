using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace BalloonSimulation.JobSystem
{
    /// <summary>
    /// System to handle balloon teleportation when colliding with specific objects
    /// </summary>
    public class BalloonTeleportSystem : MonoBehaviour
    {
        [Header("Teleport Settings")]
        [SerializeField] private Transform teleportTarget; // Position B to teleport to
        [SerializeField] private LayerMask triggerLayers = 1; // Layers for object A
        [SerializeField] private float teleportRadius = 2f; // Detection radius around object A
        [SerializeField] private bool enableEffects = true;
        
        [Header("Effect Settings")]
        [SerializeField] private ParticleSystem teleportEffect;
        [SerializeField] private AudioSource teleportSound;
        
        private BalloonManager balloonManager;
        private Camera mainCamera;
        
        // Cached data for performance
        private NativeArray<BalloonData> balloonData;
        private Transform[] triggerObjects;
        
        private void Awake()
        {
            balloonManager = FindObjectOfType<BalloonManager>();
            mainCamera = Camera.main;
            
            if (balloonManager == null)
            {
                Debug.LogError("BalloonTeleportSystem requires BalloonManager in scene");
                enabled = false;
                return;
            }
            
            if (teleportTarget == null)
            {
                Debug.LogWarning("No teleport target assigned. Creating default target at origin.");
                GameObject target = new GameObject("TeleportTarget");
                teleportTarget = target.transform;
            }
            
            // Find all trigger objects in specified layers
            RefreshTriggerObjects();
        }
        
        private void RefreshTriggerObjects()
        {
            var foundObjects = new System.Collections.Generic.List<Transform>();
            
            // Find all objects in trigger layers
            for (int layer = 0; layer < 32; layer++)
            {
                if ((triggerLayers.value & (1 << layer)) != 0)
                {
                    var objectsInLayer = FindObjectsOfType<Collider>();
                    foreach (var collider in objectsInLayer)
                    {
                        if (collider.gameObject.layer == layer)
                        {
                            foundObjects.Add(collider.transform);
                        }
                    }
                }
            }
            
            triggerObjects = foundObjects.ToArray();
            Debug.Log($"BalloonTeleportSystem found {triggerObjects.Length} trigger objects");
        }
        
        private void Update()
        {
            if (balloonManager == null || triggerObjects.Length == 0)
                return;
                
            // Get current balloon data from manager
            if (balloonManager.TryGetBalloonData(out balloonData))
            {
                CheckBalloonCollisions();
            }
        }
        
        private void CheckBalloonCollisions()
        {
            var updatedBalloons = new NativeArray<BalloonData>(balloonData.Length, Allocator.Temp);
            balloonData.CopyTo(updatedBalloons);
            
            bool anyTeleported = false;
            
            for (int balloonIndex = 0; balloonIndex < updatedBalloons.Length; balloonIndex++)
            {
                BalloonData balloon = updatedBalloons[balloonIndex];
                
                // Check against all trigger objects
                foreach (var triggerObject in triggerObjects)
                {
                    if (triggerObject == null) continue;
                    
                    float3 triggerPos = triggerObject.position;
                    float3 balloonPos = balloon.position;
                    
                    float distance = math.distance(balloonPos, triggerPos);
                    
                    // Check if balloon is within teleport radius of trigger object
                    if (distance <= teleportRadius + balloon.radius)
                    {
                        // Teleport balloon to target position
                        TeleportBalloon(ref balloon, balloonIndex);
                        updatedBalloons[balloonIndex] = balloon;
                        anyTeleported = true;
                        break; // Only teleport once per frame per balloon
                    }
                }
            }
            
            // Update balloon manager with new positions if any teleported
            if (anyTeleported)
            {
                balloonManager.UpdateBalloonData(updatedBalloons);
            }
            
            updatedBalloons.Dispose();
        }
        
        private void TeleportBalloon(ref BalloonData balloon, int balloonIndex)
        {
            float3 oldPosition = balloon.position;
            
            // Set new position with some random offset to avoid overlapping
            float3 baseTargetPos = teleportTarget.position;
            float3 randomOffset = new float3(
                UnityEngine.Random.Range(-1f, 1f),
                UnityEngine.Random.Range(-1f, 1f),
                UnityEngine.Random.Range(-1f, 1f)
            ) * 0.5f;
            
            balloon.position = baseTargetPos + randomOffset;
            
            // Reset velocity to prevent sudden movements
            balloon.velocity = float3.zero;
            
            // Update transform matrix
            balloon.UpdateMatrix();
            
            // Play effects
            if (enableEffects)
            {
                PlayTeleportEffects(oldPosition, balloon.position);
            }
            
            Debug.Log($"Balloon {balloonIndex} teleported from {oldPosition} to {balloon.position}");
        }
        
        private void PlayTeleportEffects(float3 fromPos, float3 toPos)
        {
            // Play particle effect at both positions
            if (teleportEffect != null)
            {
                // Effect at departure
                var departureEffect = Instantiate(teleportEffect, fromPos, Quaternion.identity);
                departureEffect.Play();
                Destroy(departureEffect.gameObject, 2f);
                
                // Effect at arrival
                var arrivalEffect = Instantiate(teleportEffect, toPos, Quaternion.identity);
                arrivalEffect.Play();
                Destroy(arrivalEffect.gameObject, 2f);
            }
            
            // Play sound effect
            if (teleportSound != null)
            {
                teleportSound.PlayOneShot(teleportSound.clip);
            }
        }
        
        private void OnDrawGizmosSelected()
        {
            // Draw teleport target
            if (teleportTarget != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(teleportTarget.position, 1f);
                Gizmos.DrawIcon(teleportTarget.position, "sv_icon_dot3_pix16_gizmo", true);
            }
            
            // Draw trigger objects and their detection radius
            if (triggerObjects != null)
            {
                Gizmos.color = Color.red;
                foreach (var triggerObj in triggerObjects)
                {
                    if (triggerObj != null)
                    {
                        Gizmos.DrawWireSphere(triggerObj.position, teleportRadius);
                    }
                }
            }
        }
        
        private void OnDestroy()
        {
            if (balloonData.IsCreated)
            {
                balloonData.Dispose();
            }
        }
        
        /// <summary>
        /// Public method to manually refresh trigger objects
        /// </summary>
        public void RefreshTriggers()
        {
            RefreshTriggerObjects();
        }
        
        /// <summary>
        /// Set new teleport target at runtime
        /// </summary>
        public void SetTeleportTarget(Transform newTarget)
        {
            teleportTarget = newTarget;
        }
    }
}