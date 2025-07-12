using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class BalloonPhysics : MonoBehaviour
{
    [Header("Buoyancy Settings")]
    [SerializeField] private float balloonVolume = 0.5236f; // Volume of sphere with radius 0.5m (4/3 * π * r³)
    [SerializeField] private float heliumDensity = 0.1785f; // kg/m³ at 20°C
    [SerializeField] private float airDensity = 1.225f; // kg/m³ at sea level, 15°C
    [SerializeField] private float gravity = 9.81f;
    
    [Header("Environmental Factors")]
    [SerializeField] private float temperature = 20f; // Celsius
    [SerializeField] private float altitude = 0f; // meters above sea level
    [SerializeField] private bool simulateTemperatureEffect = true;
    [SerializeField] private bool simulateAltitudeEffect = true;
    
    [Header("Balloon Properties")]
    [SerializeField] private float rubberMass = 0.005f; // kg
    [SerializeField] private float stringMass = 0.001f; // kg
    [SerializeField] private float leakRate = 0.0001f; // volume loss per second
    
    [Header("Performance")]
    [SerializeField] private bool useOptimizedCalculations = true;
    [SerializeField] private float calculationInterval = 0.1f; // seconds between buoyancy updates
    
    private Rigidbody rb;
    private float currentVolume;
    private float nextCalculationTime;
    private float cachedBuoyancyForce;
    private float effectiveAirDensity;
    
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        currentVolume = balloonVolume;
        CalculateEffectiveAirDensity();
    }
    
    void Start()
    {
        // Set total mass (helium + rubber + string)
        float heliumMass = heliumDensity * currentVolume;
        rb.mass = heliumMass + rubberMass + stringMass;
    }
    
    void FixedUpdate()
    {
        // Apply buoyancy force
        if (useOptimizedCalculations && Time.time < nextCalculationTime)
        {
            // Use cached buoyancy force
            rb.AddForce(Vector3.up * cachedBuoyancyForce, ForceMode.Force);
        }
        else
        {
            // Recalculate buoyancy
            CalculateBuoyancy();
            nextCalculationTime = Time.time + calculationInterval;
        }
        
        // Simulate helium leak
        if (leakRate > 0f)
        {
            currentVolume = Mathf.Max(0f, currentVolume - leakRate * Time.fixedDeltaTime);
            UpdateBalloonMass();
        }
    }
    
    void CalculateBuoyancy()
    {
        // Update air density if environmental factors changed
        if (simulateTemperatureEffect || simulateAltitudeEffect)
        {
            CalculateEffectiveAirDensity();
        }
        
        // Archimedes' principle: F_buoyancy = ρ_air * g * V_displaced
        float displacedAirMass = effectiveAirDensity * currentVolume;
        cachedBuoyancyForce = displacedAirMass * gravity;
        
        // Apply the force
        rb.AddForce(Vector3.up * cachedBuoyancyForce, ForceMode.Force);
    }
    
    void CalculateEffectiveAirDensity()
    {
        effectiveAirDensity = airDensity;
        
        // Temperature effect: density decreases with temperature
        if (simulateTemperatureEffect)
        {
            // Ideal gas law approximation
            float kelvin = temperature + 273.15f;
            float referenceKelvin = 288.15f; // 15°C reference
            effectiveAirDensity *= referenceKelvin / kelvin;
        }
        
        // Altitude effect: exponential decrease
        if (simulateAltitudeEffect)
        {
            // Barometric formula approximation
            float scaleHeight = 8500f; // meters
            effectiveAirDensity *= Mathf.Exp(-altitude / scaleHeight);
        }
    }
    
    void UpdateBalloonMass()
    {
        float heliumMass = heliumDensity * currentVolume;
        rb.mass = heliumMass + rubberMass + stringMass;
    }
    
    public void SetEnvironmentalConditions(float temp, float alt)
    {
        temperature = temp;
        altitude = alt;
        CalculateEffectiveAirDensity();
    }
    
    public float GetCurrentLift()
    {
        return cachedBuoyancyForce - (rb.mass * gravity);
    }
    
    public float GetVolumePercentage()
    {
        return currentVolume / balloonVolume;
    }
    
    public void RefillBalloon()
    {
        currentVolume = balloonVolume;
        UpdateBalloonMass();
    }
    
    void OnDrawGizmosSelected()
    {
        // Visualize buoyancy force
        if (Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            Vector3 forceVector = Vector3.up * (cachedBuoyancyForce * 0.1f);
            Gizmos.DrawRay(transform.position, forceVector);
            
            // Show lift direction
            float lift = GetCurrentLift();
            Gizmos.color = lift > 0 ? Color.green : Color.red;
            Gizmos.DrawWireSphere(transform.position, 0.1f);
        }
    }
}