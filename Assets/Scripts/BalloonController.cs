using UnityEngine;

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
    
    private Rigidbody rb;
    private SphereCollider sphereCollider;
    
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        sphereCollider = GetComponent<SphereCollider>();
        
        ConfigurePhysics();
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
        // Clamp velocity to prevent instability
        if (rb.velocity.magnitude > maxVelocity)
        {
            rb.velocity = rb.velocity.normalized * maxVelocity;
        }
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
}