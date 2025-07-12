using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class BalloonController : MonoBehaviour
{
    [Header("Balloon Properties")]
    [SerializeField] private float balloonRadius = 0.5f;
    [SerializeField] private float mass = 0.01f;
    [SerializeField] private float drag = 2f;
    [SerializeField] private float angularDrag = 4f;
    
    [Header("Physics Settings")]
    [SerializeField] private float maxVelocity = 10f;
    [SerializeField] private bool useContinuousCollision = true;
    
    [Header("Static Object Detection")]
    [SerializeField] private float positionThreshold = 0.001f;
    [SerializeField] private float rotationThreshold = 0.1f;
    [SerializeField] private float scaleThreshold = 0.001f;
    [SerializeField] private float staticVelocityDamping = 0.1f;
    
    [Header("Predictive Movement")]
    [SerializeField] private float minimumMovementThreshold = 0.01f;
    [SerializeField] private bool enablePredictiveMovement = true;
    
    private Rigidbody rb;
    private SphereCollider sphereCollider;
    
    // Static object detection
    private struct CollisionObjectState
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public float timestamp;
    }
    
    private Dictionary<Collider, CollisionObjectState> previousCollisionStates = new Dictionary<Collider, CollisionObjectState>();
    private HashSet<Collider> staticObjects = new HashSet<Collider>();
    
    // Predictive movement
    private Vector3 lastPosition;
    private Vector3 predictedNextPosition;
    private bool isInCollision = false;
    
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        sphereCollider = GetComponent<SphereCollider>();
        
        ConfigurePhysics();
        lastPosition = transform.position;
    }
    
    void ConfigurePhysics()
    {
        // Configure Rigidbody for optimal balloon physics
        rb.mass = mass;
        rb.drag = drag;
        rb.angularDrag = angularDrag;
        rb.useGravity = true;
        
        // Use continuous collision detection for better accuracy
        rb.collisionDetectionMode = useContinuousCollision ? 
            CollisionDetectionMode.ContinuousDynamic : 
            CollisionDetectionMode.Discrete;
        
        // Configure SphereCollider
        sphereCollider.radius = balloonRadius;
        
        // Use low-friction physics material for balloon behavior
        PhysicMaterial balloonMaterial = new PhysicMaterial("BalloonMaterial");
        balloonMaterial.bounciness = 0.3f;
        balloonMaterial.dynamicFriction = 0.2f;
        balloonMaterial.staticFriction = 0.2f;
        balloonMaterial.frictionCombine = PhysicMaterialCombine.Minimum;
        balloonMaterial.bounceCombine = PhysicMaterialCombine.Average;
        sphereCollider.material = balloonMaterial;
    }
    
    void FixedUpdate()
    {
        // Predictive movement check
        if (enablePredictiveMovement && isInCollision)
        {
            PredictAndLimitMovement();
        }
        
        // Clamp velocity to prevent instability
        if (rb.velocity.magnitude > maxVelocity)
        {
            rb.velocity = rb.velocity.normalized * maxVelocity;
        }
        
        // Update position tracking
        lastPosition = transform.position;
    }
    
    public void SetPhysicsEnabled(bool enabled)
    {
        rb.isKinematic = !enabled;
    }
    
    public void ResetVelocity()
    {
        rb.velocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
    }
    
    void OnCollisionEnter(Collision collision)
    {
        isInCollision = true;
        CheckStaticObject(collision.collider);
    }
    
    void OnCollisionStay(Collision collision)
    {
        if (staticObjects.Contains(collision.collider))
        {
            ApplyStaticObjectDamping(collision);
        }
        else
        {
            CheckStaticObject(collision.collider);
        }
    }
    
    void OnCollisionExit(Collision collision)
    {
        // Clean up old collision data
        if (previousCollisionStates.ContainsKey(collision.collider))
        {
            previousCollisionStates.Remove(collision.collider);
        }
        staticObjects.Remove(collision.collider);
        
        // Check if we're still in collision with any objects
        if (staticObjects.Count == 0)
        {
            isInCollision = false;
        }
    }
    
    void CheckStaticObject(Collider collider)
    {
        Transform objTransform = collider.transform;
        CollisionObjectState currentState = new CollisionObjectState
        {
            position = objTransform.position,
            rotation = objTransform.rotation,
            scale = objTransform.lossyScale,
            timestamp = Time.time
        };
        
        if (previousCollisionStates.ContainsKey(collider))
        {
            CollisionObjectState previousState = previousCollisionStates[collider];
            
            // Check if object hasn't moved, rotated, or scaled
            bool positionStatic = Vector3.Distance(currentState.position, previousState.position) < positionThreshold;
            bool rotationStatic = Quaternion.Angle(currentState.rotation, previousState.rotation) < rotationThreshold;
            bool scaleStatic = Vector3.Distance(currentState.scale, previousState.scale) < scaleThreshold;
            
            if (positionStatic && rotationStatic && scaleStatic)
            {
                if (!staticObjects.Contains(collider))
                {
                    staticObjects.Add(collider);
                }
            }
            else
            {
                staticObjects.Remove(collider);
            }
        }
        
        previousCollisionStates[collider] = currentState;
    }
    
    void ApplyStaticObjectDamping(Collision collision)
    {
        // Apply strong damping when colliding with static objects
        Vector3 dampedVelocity = rb.velocity * staticVelocityDamping;
        Vector3 dampedAngularVelocity = rb.angularVelocity * staticVelocityDamping;
        
        // Calculate contact normal and apply additional stopping force
        Vector3 contactNormal = Vector3.zero;
        foreach (ContactPoint contact in collision.contacts)
        {
            contactNormal += contact.normal;
        }
        contactNormal.Normalize();
        
        // Remove velocity component along the contact normal
        Vector3 normalVelocity = Vector3.Project(rb.velocity, contactNormal);
        Vector3 tangentVelocity = rb.velocity - normalVelocity;
        
        // Apply heavier damping to normal velocity, lighter to tangent velocity
        rb.velocity = tangentVelocity * staticVelocityDamping + normalVelocity * (staticVelocityDamping * 0.1f);
        rb.angularVelocity = dampedAngularVelocity;
        
        // If velocity is very low, stop the balloon completely
        if (rb.velocity.magnitude < 0.1f)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }
    
    void PredictAndLimitMovement()
    {
        // Calculate predicted next position based on current velocity
        predictedNextPosition = transform.position + rb.velocity * Time.fixedDeltaTime;
        
        // Calculate how much we would move
        float predictedMovementDistance = Vector3.Distance(lastPosition, predictedNextPosition);
        
        // If predicted movement is below threshold, stop movement
        if (predictedMovementDistance < minimumMovementThreshold)
        {
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            
            // Optionally make the rigidbody sleep to save performance
            if (rb.velocity.magnitude < 0.01f && rb.angularVelocity.magnitude < 0.01f)
            {
                rb.Sleep();
            }
        }
        else
        {
            // Scale down velocity if movement is very small but above threshold
            float movementRatio = predictedMovementDistance / minimumMovementThreshold;
            if (movementRatio < 2f) // If movement is less than 2x threshold
            {
                float dampingFactor = Mathf.Lerp(0.1f, 1f, (movementRatio - 1f)); // Smooth transition
                rb.velocity *= dampingFactor;
                rb.angularVelocity *= dampingFactor;
            }
        }
    }
    
    void OnDestroy()
    {
        // Clean up collision tracking data
        previousCollisionStates.Clear();
        staticObjects.Clear();
    }
    
    // Public method to get predicted movement info for debugging
    public float GetPredictedMovementDistance()
    {
        if (enablePredictiveMovement)
        {
            Vector3 nextPos = transform.position + rb.velocity * Time.fixedDeltaTime;
            return Vector3.Distance(lastPosition, nextPos);
        }
        return 0f;
    }
}