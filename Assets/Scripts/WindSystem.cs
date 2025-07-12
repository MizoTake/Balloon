using UnityEngine;

public class WindSystem : MonoBehaviour
{
    [Header("Wind Settings")]
    [SerializeField] private Vector3 baseWindDirection = Vector3.right;
    [SerializeField] private float baseWindStrength = 2f;
    [SerializeField] private float gustStrength = 5f;
    
    [Header("Perlin Noise Configuration")]
    [SerializeField] private float turbulenceScale = 0.1f;
    [SerializeField] private float turbulenceSpeed = 1f;
    [SerializeField] private Vector3 noiseOffset = Vector3.zero;
    
    [Header("Wind Zones")]
    [SerializeField] private Vector3 windZoneSize = new Vector3(50f, 20f, 50f);
    [SerializeField] private AnimationCurve windFalloff = AnimationCurve.Linear(0f, 1f, 1f, 0f);
    
    [Header("Performance")]
    [SerializeField] private bool useWorldSpaceNoise = true;
    [SerializeField] private float updateInterval = 0.1f;
    [SerializeField] private int maxBalloonsPerFrame = 10;
    
    [Header("Wind Patterns")]
    [SerializeField] private bool enableVerticalTurbulence = true;
    [SerializeField] private float verticalTurbulenceStrength = 1f;
    [SerializeField] private bool enableWindGusts = true;
    [SerializeField] private float gustFrequency = 0.2f;
    
    private static WindSystem instance;
    private float nextUpdateTime;
    private int balloonUpdateIndex;
    private BalloonController[] allBalloons;
    
    public static WindSystem Instance => instance;
    
    void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    void Start()
    {
        RefreshBalloonList();
    }
    
    void Update()
    {
        if (Time.time >= nextUpdateTime)
        {
            UpdateBalloonWindForces();
            nextUpdateTime = Time.time + updateInterval;
        }
    }
    
    void UpdateBalloonWindForces()
    {
        if (allBalloons == null || allBalloons.Length == 0)
        {
            RefreshBalloonList();
            return;
        }
        
        int balloonsToUpdate = Mathf.Min(maxBalloonsPerFrame, allBalloons.Length);
        
        for (int i = 0; i < balloonsToUpdate; i++)
        {
            int index = (balloonUpdateIndex + i) % allBalloons.Length;
            BalloonController balloon = allBalloons[index];
            
            if (balloon != null && balloon.gameObject.activeInHierarchy)
            {
                ApplyWindForce(balloon);
            }
        }
        
        balloonUpdateIndex = (balloonUpdateIndex + balloonsToUpdate) % allBalloons.Length;
    }
    
    void ApplyWindForce(BalloonController balloon)
    {
        Vector3 position = balloon.transform.position;
        Vector3 windForce = CalculateWindForce(position);
        
        // Apply wind zone falloff
        float distanceFromCenter = Vector3.Distance(position, transform.position);
        float maxDistance = Mathf.Max(windZoneSize.x, windZoneSize.y, windZoneSize.z) * 0.5f;
        float falloffFactor = windFalloff.Evaluate(distanceFromCenter / maxDistance);
        
        windForce *= falloffFactor;
        
        // Apply force to rigidbody
        Rigidbody rb = balloon.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.AddForce(windForce, ForceMode.Force);
        }
    }
    
    public Vector3 CalculateWindForce(Vector3 position)
    {
        float time = Time.time * turbulenceSpeed;
        Vector3 noisePos = useWorldSpaceNoise ? position * turbulenceScale + noiseOffset : noiseOffset;
        
        // Calculate base wind with Perlin noise turbulence
        float noiseX = Mathf.PerlinNoise(noisePos.x + time, noisePos.z + time) * 2f - 1f;
        float noiseZ = Mathf.PerlinNoise(noisePos.x + time + 100f, noisePos.z + time + 100f) * 2f - 1f;
        
        Vector3 turbulence = new Vector3(noiseX, 0f, noiseZ);
        
        // Add vertical turbulence if enabled
        if (enableVerticalTurbulence)
        {
            float noiseY = Mathf.PerlinNoise(noisePos.y + time, noisePos.x + time) * 2f - 1f;
            turbulence.y = noiseY * verticalTurbulenceStrength;
        }
        
        // Combine base wind with turbulence
        Vector3 windForce = baseWindDirection.normalized * baseWindStrength + turbulence;
        
        // Add wind gusts
        if (enableWindGusts)
        {
            float gustNoise = Mathf.PerlinNoise(time * gustFrequency, 0f);
            if (gustNoise > 0.7f) // Gusts occur 30% of the time
            {
                float gustMultiplier = (gustNoise - 0.7f) / 0.3f; // Normalize to 0-1
                windForce += baseWindDirection.normalized * gustStrength * gustMultiplier;
            }
        }
        
        return windForce;
    }
    
    public void RefreshBalloonList()
    {
        allBalloons = FindObjectsOfType<BalloonController>();
        balloonUpdateIndex = 0;
    }
    
    public void SetWindParameters(Vector3 direction, float strength)
    {
        baseWindDirection = direction.normalized;
        baseWindStrength = strength;
    }
    
    public void SetTurbulenceParameters(float scale, float speed)
    {
        turbulenceScale = scale;
        turbulenceSpeed = speed;
    }
    
    void OnDrawGizmosSelected()
    {
        // Draw wind zone
        Gizmos.color = new Color(0f, 1f, 1f, 0.3f);
        Gizmos.DrawCube(transform.position, windZoneSize);
        
        // Draw wind direction
        Gizmos.color = Color.cyan;
        Vector3 windDirection = baseWindDirection.normalized * 3f;
        Gizmos.DrawRay(transform.position, windDirection);
        
        // Draw wind strength indicator
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position + windDirection, baseWindStrength * 0.2f);
    }
}