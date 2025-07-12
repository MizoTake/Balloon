using UnityEngine;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

[System.Serializable]
public struct VerletPoint
{
    public float3 position;
    public float3 oldPosition;
    public bool isPinned;
    public float mass;
}

public class VerletRope : MonoBehaviour
{
    [Header("Rope Settings")]
    [SerializeField] private int pointCount = 20;
    [SerializeField] private float ropeLength = 2f;
    [SerializeField] private float pointMass = 0.01f;
    [SerializeField] private float damping = 0.99f;
    
    [Header("Constraints")]
    [SerializeField] private float stiffness = 1f;
    [SerializeField] private int constraintIterations = 3;
    [SerializeField] private bool enableCollisions = true;
    [SerializeField] private float collisionRadius = 0.02f;
    [SerializeField] private LayerMask collisionLayers = -1;
    
    [Header("Rendering")]
    [SerializeField] private LineRenderer lineRenderer;
    [SerializeField] private float ropeWidth = 0.05f;
    [SerializeField] private Material ropeMaterial;
    
    [Header("Performance")]
    [SerializeField] private bool useJobSystem = true;
    [SerializeField] private bool enableBurstCompilation = true;
    
    private NativeArray<VerletPoint> points;
    private NativeArray<float> restLengths;
    private Transform attachmentPoint;
    private BalloonController attachedBalloon;
    private float segmentLength;
    
    struct VerletIntegrationJob : IJobParallelFor
    {
        public NativeArray<VerletPoint> points;
        public float deltaTime;
        public float gravity;
        public float damping;
        
        public void Execute(int index)
        {
            VerletPoint point = points[index];
            
            if (!point.isPinned)
            {
                float3 velocity = (point.position - point.oldPosition) * damping;
                point.oldPosition = point.position;
                
                // Apply gravity
                velocity.y -= gravity * deltaTime * deltaTime;
                
                point.position += velocity;
            }
            
            points[index] = point;
        }
    }
    
    struct ConstraintSolverJob : IJob
    {
        public NativeArray<VerletPoint> points;
        [ReadOnly] public NativeArray<float> restLengths;
        public float stiffness;
        
        public void Execute()
        {
            for (int index = 0; index < points.Length - 1; index++)
            {
                VerletPoint pointA = points[index];
                VerletPoint pointB = points[index + 1];
            
            float3 delta = pointB.position - pointA.position;
            float distance = math.length(delta);
            
            if (distance > 0.001f)
            {
                float restLength = restLengths[index];
                float difference = (restLength - distance) / distance;
                float3 correction = delta * difference * stiffness;
                
                if (!pointA.isPinned && !pointB.isPinned)
                {
                    pointA.position -= correction * 0.5f;
                    pointB.position += correction * 0.5f;
                }
                else if (!pointA.isPinned)
                {
                    pointA.position -= correction;
                }
                else if (!pointB.isPinned)
                {
                    pointB.position += correction;
                }
            }
                
                points[index] = pointA;
                points[index + 1] = pointB;
            }
        }
    }
    
    void Start()
    {
        InitializeRope();
        SetupLineRenderer();
    }
    
    void InitializeRope()
    {
        segmentLength = ropeLength / (pointCount - 1);
        
        points = new NativeArray<VerletPoint>(pointCount, Allocator.Persistent);
        restLengths = new NativeArray<float>(pointCount - 1, Allocator.Persistent);
        
        for (int i = 0; i < pointCount; i++)
        {
            VerletPoint point = new VerletPoint
            {
                position = transform.position + Vector3.down * (i * segmentLength),
                oldPosition = transform.position + Vector3.down * (i * segmentLength),
                isPinned = i == 0, // Pin the top point
                mass = pointMass
            };
            
            points[i] = point;
            
            if (i < pointCount - 1)
            {
                restLengths[i] = segmentLength;
            }
        }
        
        // Find attached balloon
        attachedBalloon = GetComponentInParent<BalloonController>();
        if (attachedBalloon != null)
        {
            attachmentPoint = attachedBalloon.transform;
        }
    }
    
    void SetupLineRenderer()
    {
        if (lineRenderer == null)
        {
            lineRenderer = GetComponent<LineRenderer>();
            if (lineRenderer == null)
            {
                lineRenderer = gameObject.AddComponent<LineRenderer>();
            }
        }
        
        lineRenderer.positionCount = pointCount;
        lineRenderer.startWidth = ropeWidth;
        lineRenderer.endWidth = ropeWidth;
        lineRenderer.useWorldSpace = true;
        
        if (ropeMaterial != null)
        {
            lineRenderer.material = ropeMaterial;
        }
    }
    
    void FixedUpdate()
    {
        UpdateAttachmentPoint();
        
        if (useJobSystem && enableBurstCompilation)
        {
            UpdateRopeWithJobs();
        }
        else
        {
            UpdateRopeClassic();
        }
        
        if (enableCollisions)
        {
            HandleCollisions();
        }
        
        UpdateLineRenderer();
    }
    
    void UpdateAttachmentPoint()
    {
        if (attachmentPoint != null && points.IsCreated)
        {
            VerletPoint firstPoint = points[0];
            firstPoint.position = attachmentPoint.position;
            firstPoint.oldPosition = attachmentPoint.position;
            points[0] = firstPoint;
        }
    }
    
    void UpdateRopeWithJobs()
    {
        // Verlet integration
        VerletIntegrationJob integrationJob = new VerletIntegrationJob
        {
            points = points,
            deltaTime = Time.fixedDeltaTime,
            gravity = 9.81f,
            damping = damping
        };
        
        JobHandle integrationHandle = integrationJob.Schedule(pointCount, 32);
        integrationHandle.Complete();
        
        // Constraint solving
        for (int iteration = 0; iteration < constraintIterations; iteration++)
        {
            ConstraintSolverJob constraintJob = new ConstraintSolverJob
            {
                points = points,
                restLengths = restLengths,
                stiffness = stiffness
            };
            
            JobHandle constraintHandle = constraintJob.Schedule();
            constraintHandle.Complete();
        }
    }
    
    void UpdateRopeClassic()
    {
        float deltaTime = Time.fixedDeltaTime;
        
        // Verlet integration
        for (int i = 0; i < pointCount; i++)
        {
            VerletPoint point = points[i];
            
            if (!point.isPinned)
            {
                float3 velocity = (point.position - point.oldPosition) * damping;
                point.oldPosition = point.position;
                
                velocity.y -= 9.81f * deltaTime * deltaTime;
                point.position += velocity;
            }
            
            points[i] = point;
        }
        
        // Constraint solving
        for (int iteration = 0; iteration < constraintIterations; iteration++)
        {
            for (int i = 0; i < pointCount - 1; i++)
            {
                VerletPoint pointA = points[i];
                VerletPoint pointB = points[i + 1];
                
                float3 delta = pointB.position - pointA.position;
                float distance = math.length(delta);
                
                if (distance > 0.001f)
                {
                    float restLength = restLengths[i];
                    float difference = (restLength - distance) / distance;
                    float3 correction = delta * difference * stiffness;
                    
                    if (!pointA.isPinned && !pointB.isPinned)
                    {
                        pointA.position -= correction * 0.5f;
                        pointB.position += correction * 0.5f;
                    }
                    else if (!pointA.isPinned)
                    {
                        pointA.position -= correction;
                    }
                    else if (!pointB.isPinned)
                    {
                        pointB.position += correction;
                    }
                }
                
                points[i] = pointA;
                points[i + 1] = pointB;
            }
        }
    }
    
    void HandleCollisions()
    {
        for (int i = 1; i < pointCount; i++) // Skip pinned point
        {
            VerletPoint point = points[i];
            
            if (Physics.CheckSphere(point.position, collisionRadius, collisionLayers))
            {
                // Simple collision response - move point away from collision
                float3 direction = math.normalize(point.position - point.oldPosition);
                if (math.length(direction) < 0.1f) direction = new float3(0, 1, 0);
                
                point.position = point.oldPosition + direction * collisionRadius;
            }
            
            points[i] = point;
        }
    }
    
    void UpdateLineRenderer()
    {
        if (lineRenderer != null && points.IsCreated)
        {
            Vector3[] positions = new Vector3[pointCount];
            for (int i = 0; i < pointCount; i++)
            {
                positions[i] = points[i].position;
            }
            lineRenderer.SetPositions(positions);
        }
    }
    
    public Vector3 GetBottomPosition()
    {
        if (points.IsCreated && pointCount > 0)
        {
            return points[pointCount - 1].position;
        }
        return transform.position;
    }
    
    void OnDestroy()
    {
        if (points.IsCreated) points.Dispose();
        if (restLengths.IsCreated) restLengths.Dispose();
    }
    
    void OnDrawGizmosSelected()
    {
        if (points.IsCreated)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < pointCount; i++)
            {
                Gizmos.DrawWireSphere(points[i].position, collisionRadius);
            }
        }
    }
}