using UnityEngine;
using System.Collections.Generic;

public class BalloonLODSystem : MonoBehaviour
{
    [Header("LOD Settings")]
    [SerializeField] private float[] lodDistances = new float[] { 10f, 25f, 50f };
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float updateInterval = 0.5f;
    
    [Header("Performance Settings")]
    [SerializeField] private bool disablePhysicsAtMaxDistance = true;
    [SerializeField] private bool reduceSolverIterationsWithDistance = true;
    
    private struct BalloonLOD
    {
        public BalloonController controller;
        public Rigidbody rigidbody;
        public Renderer renderer;
        public int currentLOD;
    }
    
    private List<BalloonLOD> balloons = new List<BalloonLOD>();
    private float nextUpdateTime;
    
    void Start()
    {
        if (cameraTransform == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
                cameraTransform = mainCamera.transform;
        }
        
        RegisterAllBalloons();
    }
    
    public void RegisterAllBalloons()
    {
        BalloonController[] allBalloons = FindObjectsOfType<BalloonController>();
        
        foreach (BalloonController balloon in allBalloons)
        {
            BalloonLOD lodData = new BalloonLOD
            {
                controller = balloon,
                rigidbody = balloon.GetComponent<Rigidbody>(),
                renderer = balloon.GetComponent<Renderer>(),
                currentLOD = 0
            };
            
            balloons.Add(lodData);
        }
        
        Debug.Log($"LOD System registered {balloons.Count} balloons");
    }
    
    void Update()
    {
        if (Time.time >= nextUpdateTime)
        {
            UpdateBalloonLODs();
            nextUpdateTime = Time.time + updateInterval;
        }
    }
    
    void UpdateBalloonLODs()
    {
        if (cameraTransform == null) return;
        
        for (int i = 0; i < balloons.Count; i++)
        {
            BalloonLOD balloon = balloons[i];
            
            if (balloon.controller == null) continue;
            
            float distance = Vector3.Distance(cameraTransform.position, balloon.controller.transform.position);
            int newLOD = CalculateLODLevel(distance);
            
            if (newLOD != balloon.currentLOD)
            {
                ApplyLODSettings(ref balloon, newLOD);
                balloons[i] = balloon;
            }
        }
    }
    
    int CalculateLODLevel(float distance)
    {
        for (int i = 0; i < lodDistances.Length; i++)
        {
            if (distance < lodDistances[i])
                return i;
        }
        return lodDistances.Length;
    }
    
    void ApplyLODSettings(ref BalloonLOD balloon, int lodLevel)
    {
        balloon.currentLOD = lodLevel;
        
        switch (lodLevel)
        {
            case 0: // Highest quality - full physics
                balloon.rigidbody.isKinematic = false;
                balloon.rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                if (reduceSolverIterationsWithDistance)
                {
                    balloon.rigidbody.solverIterations = 6;
                    balloon.rigidbody.solverVelocityIterations = 4;
                }
                balloon.renderer.enabled = true;
                break;
                
            case 1: // Medium quality - simplified physics
                balloon.rigidbody.isKinematic = false;
                balloon.rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
                if (reduceSolverIterationsWithDistance)
                {
                    balloon.rigidbody.solverIterations = 4;
                    balloon.rigidbody.solverVelocityIterations = 2;
                }
                balloon.renderer.enabled = true;
                break;
                
            case 2: // Low quality - minimal physics
                balloon.rigidbody.isKinematic = false;
                balloon.rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;
                if (reduceSolverIterationsWithDistance)
                {
                    balloon.rigidbody.solverIterations = 2;
                    balloon.rigidbody.solverVelocityIterations = 1;
                }
                balloon.renderer.enabled = true;
                break;
                
            default: // Beyond max distance - disable physics
                if (disablePhysicsAtMaxDistance)
                {
                    balloon.rigidbody.isKinematic = true;
                    balloon.controller.ResetVelocity();
                }
                balloon.renderer.enabled = true;
                break;
        }
    }
    
    public void RegisterBalloon(BalloonController balloon)
    {
        BalloonLOD lodData = new BalloonLOD
        {
            controller = balloon,
            rigidbody = balloon.GetComponent<Rigidbody>(),
            renderer = balloon.GetComponent<Renderer>(),
            currentLOD = 0
        };
        
        balloons.Add(lodData);
    }
    
    public void UnregisterBalloon(BalloonController balloon)
    {
        balloons.RemoveAll(b => b.controller == balloon);
    }
    
    void OnDrawGizmosSelected()
    {
        if (cameraTransform == null) return;
        
        Gizmos.color = Color.green;
        for (int i = 0; i < lodDistances.Length; i++)
        {
            Gizmos.DrawWireSphere(cameraTransform.position, lodDistances[i]);
        }
    }
}