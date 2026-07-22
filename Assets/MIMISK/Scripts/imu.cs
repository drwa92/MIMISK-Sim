using UnityEngine;
 
namespace ROS2
{
    
    public class IMU : MonoBehaviour
    {
        private Rigidbody rb;
        private Vector3 lastVelocity;
        
        [Header("Inspector Values (Unity frame)")]
        [ReadOnly] public Vector3 accelerationLocal;
        [ReadOnly] public Vector3 gyroLocal;
        [ReadOnly] public Vector3 magneticFieldLocal;
        private Quaternion quatLocal;

        [Header("Inspector Values (NED frame)")]
        [ReadOnly] public Vector3 accelNED;
        [ReadOnly] public Vector3 gyroNED;
        [ReadOnly] public Vector3 magnetoNED;
        [ReadOnly] public Quaternion quatNED;

        [Header("Manager")]
        public SensorManager sensorManager;

        private System.Random rand = new System.Random();

        [Header("ICM-20948-like Magnetic Field")]
        public float earthMagneticFieldStrengthMicroTesla = 48f;
        [Range(0f, 90f)] public float magneticInclinationDeg = 60f;

        public float accelNoiseStd = 0.02f;  // amplitude du bruit sur l'accélération
        public float gyroNoiseStd = 0.001f;
        public float quatNoiseStd = 0.005f;

        [Header("Enable Noise")]
        public bool enableNoise = true;


        void Start()
        {
            rb = GetComponentInParent<Rigidbody>();
            if (rb == null)
            {
                Debug.LogError("[IMU] Aucun Rigidbody trouve dans le parent.");
                enabled = false;
                return;
            }

            lastVelocity = rb.linearVelocity;

            if (sensorManager == null)
            {
                sensorManager = FindAnyObjectByType<SensorManager>();
            }

            if (sensorManager == null)
            {
                Debug.LogWarning("[IMU] SensorManager non assigne: les donnees IMU ne seront pas publiees sur le port serie.");
            }
        }

        void FixedUpdate() //50 hz by default
        {
            // --- Accélération dans le monde ---
            Vector3 velocity_world = rb.linearVelocity;
            Vector3 accel_world = (velocity_world - lastVelocity) / Time.fixedDeltaTime;
            lastVelocity = velocity_world;

            // An IMU measures specific force, so gravity appears when the vehicle is at rest.
            Vector3 specificForceWorld = accel_world - Physics.gravity;


            // ---Conversion dans le repère local du capteur ---
            accelerationLocal = transform.InverseTransformDirection(specificForceWorld);

            // --- Gyroscope ---
            // Unity donne angularVelocity en repère monde :
            gyroLocal = transform.InverseTransformDirection(rb.angularVelocity);

            // --- Champ magnétique ---
            // Unity world convention here assumes north is +Z and magnetic field dips downward.
            float inclinationRad = magneticInclinationDeg * Mathf.Deg2Rad;
            Vector3 worldMagneticField = new Vector3(
                0f,
                -Mathf.Sin(inclinationRad),
                Mathf.Cos(inclinationRad)
            ) * earthMagneticFieldStrengthMicroTesla;
            magneticFieldLocal = transform.InverseTransformDirection(worldMagneticField);

            //Quaternion for ROS
            Quaternion quatLocal = transform.rotation;


            accelNED = Unity2NED.VectorUnityToNED(accelerationLocal);
            gyroNED  = Unity2NED.VectorUnityToNED(gyroLocal);
            magnetoNED = Unity2NED.VectorUnityToNED(magneticFieldLocal);
            quatNED = Unity2NED.QuaternionUnityToNED(quatLocal);

            // --- Affichage pour debug ---
            // Debug.Log($"Quat : {quatLocal:F3} | Speed: {velocity_world:F3} | Accel: {accelerationLocal:F3} | Gyro: {gyroLocal:F3} | Mag: {magneticFieldLocal:F3}");
            // Debug.Log($"NED Quat : {quatNED:F3} | Accel: {accelNED:F3} | Gyro: {gyroNED:F3}");


            if (enableNoise)
            {
                quatNED = AddGaussianNoiseQuat(quatNED, quatNoiseStd); // std en rad
                accelNED = AddGaussianNoiseVector(accelNED, accelNoiseStd); // std en rad
                gyroNED = AddGaussianNoiseVector(gyroNED, gyroNoiseStd); // std en rad
            }

            if (sensorManager != null)
            {
                sensorManager.UpdateImuData(accelNED, gyroNED, quatNED);
                sensorManager.UpdateMagnetometerData(magnetoNED);
            }


        }

        Vector3 AddGaussianNoiseVector(Vector3 v, float std)
        {
            return new Vector3(
                v.x + NextGaussian() * std,
                v.y + NextGaussian() * std,
                v.z + NextGaussian() * std
            );
        }

        float NextGaussian()
        {
            // Box-Muller transform
            float u1 = 1.0f - (float)rand.NextDouble(); // uniform(0,1] random doubles
            float u2 = 1.0f - (float)rand.NextDouble();
            float randStdNormal = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) *
                                Mathf.Sin(2.0f * Mathf.PI * u2);
            return randStdNormal;
        }

        Quaternion AddGaussianNoiseQuat(Quaternion q, float stdRad)
        {
            // Générer un petit bruit autour de chaque axe (roll, pitch, yaw)
            Vector3 deltaEuler = new Vector3(
                NextGaussian() * stdRad,
                NextGaussian() * stdRad,
                NextGaussian() * stdRad
            );

            // Convertir le bruit en quaternion
            Quaternion noiseQuat = Quaternion.Euler(deltaEuler * Mathf.Rad2Deg);

            // Appliquer le bruit sur le quaternion original
            return noiseQuat * q;
        }



    }
}
