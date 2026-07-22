using UnityEngine;
namespace ROS2 
{
public class BarometerSimulator : MonoBehaviour
{
    public float referenceDepth = 0f; //(m)
    public bool addNoise = true;
    public float noiseMean = 0f; // Moyenne du bruit (Pa)
    public float noiseStdDev = 500f; // ecart-type du bruit (Pa)
    
    // Constantes physiques
    private const float ATMOSPHERIC_PRESSURE = 101325f;
    private const float WATER_DENSITY = 1000f;
    private const float GRAVITY = 9.81f;
    
    [Header("Inspector Values")]
    [ReadOnly] public float truePressurePa;
    [ReadOnly] public float noisyPressurePa;
    [ReadOnly] public float depthMeters;

    [Header("Manager")]
    public SensorManager sensorManager;
    
    void Start()
    {
        Debug.Log("Position de départ Y: " + transform.position.y);

        if (sensorManager == null)
        {
            sensorManager = FindAnyObjectByType<SensorManager>();
        }

        if (sensorManager == null)
        {
            Debug.LogWarning("[BarometerSimulator] SensorManager non assigne: la profondeur ne sera pas publiee sur le port serie.");
        }
    }
    
    float CalculatePressure()
    {
        float yPosition = transform.position.y;
        float depth = referenceDepth - yPosition;
        if (depth < 0f)
            return ATMOSPHERIC_PRESSURE;
        
        float pressure = ATMOSPHERIC_PRESSURE + (WATER_DENSITY * GRAVITY * depth);
        return pressure;
    }
    
    float GenerateGaussianNoise(float mean, float stdDev)
    {
        float u1 = Random.value;
        float u2 = Random.value;
        // eviter log(0)
        if (u1 <= Mathf.Epsilon)
            u1 = Mathf.Epsilon;
        float randStdNormal = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2);
        return mean + stdDev * randStdNormal;
    }
    
    float GetNoisyPressure(float truePressure)
    {
        if (addNoise)
        {
            float noise = GenerateGaussianNoise(noiseMean, noiseStdDev);
            float noisyValue = truePressure + noise;
            // S'assurer que la pression reste positive
            return Mathf.Max(0f, noisyValue);
        }
        return truePressure;
    }
    
    float ComputeDepthMeters()
    {
        return Mathf.Max(0f, referenceDepth - transform.position.y);
    }
    
    void Update()
    {
        truePressurePa = CalculatePressure();
        noisyPressurePa = GetNoisyPressure(truePressurePa);
        depthMeters = ComputeDepthMeters();

        if (sensorManager != null)
        {
            sensorManager.UpdateDepthData(depthMeters);
        }
    }
}
} //namespace ROS2