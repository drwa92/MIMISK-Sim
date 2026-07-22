using System;
using System.Collections.Generic;
using System.IO;
using NaughtyWaterBuoyancy;
using UnityEngine;

namespace ROS2
{
    public class SensorManager : MonoBehaviour
    {
        private const string SharedSerialPort = "/dev/unity_esp32";
        private static readonly string[] PortFallbacks = { SharedSerialPort, "/dev/unity_sensors", "/dev/unity_motors", "/dev/ttyACM0" };
        private const byte Sync = 0xBB;
        private const int FrameLen = 33;
        private const float StandardGravityMps2 = 9.80665f;
        private const float Icm20948AccelFullScaleG = 16f;
        private const float Icm20948GyroFullScaleDps = 2000f;
        private const float Icm20948MagMicroTeslaPerLsb = 0.15f;

        [Header("Serial Output")]
        [ReadOnly] public string portName = "/dev/unity_esp32";
        [ReadOnly] public int baudRate = 115200;
        [ReadOnly, Range(1, 200)] public int publishHz = 50;
        [ReadOnly] public bool autoOpenOnStart = true;

        [Header("Per-Sensor Emission Rates (Hz)")]
        [Min(0.1f)] public float baroHz = 10f;
        [Min(0.1f)] public float gyroHz = 50f;
        [Min(0.1f)] public float magnetoHz = 20f;
        [Min(0.1f)] public float accelHz = 50f;

        [Header("Ballast Source")]
        public FloatingObject ballastSource;

        [Header("Navigation State")]
        [ReadOnly] public Quaternion qNed = Quaternion.identity;
        [ReadOnly] public Vector3 accelNed;
        [ReadOnly] public Vector3 gyroNed;
        [ReadOnly] public Vector3 magnetoNed;
        [ReadOnly] public float depthMeters;

        [Header("Ballast Potentiometers")]
        [ReadOnly] public int ballast1;
        [ReadOnly] public int ballast2;

        private FileStream serialStream;
        private string openedPortName;
        private float publishTimer;
        private float reconnectTimer;

        private Vector3 rawAccelNed;
        private Vector3 rawGyroNed;
        private Vector3 rawMagnetoNed;
        private float rawDepthMeters;
        private int rawBallast1;
        private int rawBallast2;

        private float accelTimer;
        private float gyroTimer;
        private float magnetoTimer;
        private float baroTimer;

        public void UpdateImuData(Vector3 accel, Vector3 gyro, Quaternion quat)
        {
            rawAccelNed = accel;
            rawGyroNed = gyro;
            qNed = quat;
        }

        public void UpdateMagnetometerData(Vector3 magneto)
        {
            rawMagnetoNed = magneto;
        }

        public void UpdateDepthData(float depth)
        {
            rawDepthMeters = Mathf.Max(0f, depth);
        }

        public void UpdateBallastData(int ballast1Value, int ballast2Value)
        {
            rawBallast1 = Mathf.Clamp(ballast1Value, 0, 1000);
            rawBallast2 = Mathf.Clamp(ballast2Value, 0, 1000);
        }

        public byte[] BuildSensorFrameForSerial()
        {
            return BuildFrame();
        }

        private void Reset()
        {
            ApplyDefaultSerialConfig();
            CacheReferences();
        }

        private void OnValidate()
        {
            portName = NormalizeLegacyPortName(portName);
            ApplyDefaultSerialConfig();
            CacheReferences();
        }

        private void Start()
        {
            portName = NormalizeLegacyPortName(portName);
            CacheReferences();
            if (autoOpenOnStart)
            {
                TryOpenSerial();
            }
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            UpdateSensorOutputs(dt);

            publishTimer += dt;
            float period = 1f / Mathf.Max(1, publishHz);
            if (publishTimer < period)
            {
                return;
            }

            publishTimer -= period;

            if (!autoOpenOnStart)
            {
                return;
            }

            if (!IsSerialOpen())
            {
                reconnectTimer += dt;
                if (reconnectTimer >= 1f)
                {
                    reconnectTimer = 0f;
                    TryOpenSerial();
                }
                return;
            }

            byte[] frame = BuildFrame();
            try
            {
                serialStream.Write(frame, 0, frame.Length);
                serialStream.Flush();
            }
            catch (Exception e)
            {
                string activePort = string.IsNullOrEmpty(openedPortName) ? portName : openedPortName;
                Debug.LogWarning($"[SensorManager] Ecriture serie impossible sur {activePort}: {e.Message}");
                CloseSerial();
            }
        }

        private void OnDisable()
        {
            CloseSerial();
        }

        private void OnApplicationQuit()
        {
            CloseSerial();
        }

        private void CacheReferences()
        {
            if (ballastSource == null)
            {
                ballastSource = GetComponent<FloatingObject>();
            }
        }

        private bool IsSerialOpen()
        {
            return serialStream != null;
        }

        private void UpdateSensorOutputs(float dt)
        {
            accelTimer += dt;
            if (ShouldEmit(ref accelTimer, accelHz))
            {
                accelNed = rawAccelNed;
            }

            gyroTimer += dt;
            if (ShouldEmit(ref gyroTimer, gyroHz))
            {
                gyroNed = rawGyroNed;
            }

            magnetoTimer += dt;
            if (ShouldEmit(ref magnetoTimer, magnetoHz))
            {
                magnetoNed = rawMagnetoNed;
            }

            baroTimer += dt;
            if (ShouldEmit(ref baroTimer, baroHz))
            {
                depthMeters = rawDepthMeters;
            }

            if (ballastSource != null)
            {
                rawBallast1 = ballastSource.Ballast1PotentiometerValue;
                rawBallast2 = ballastSource.Ballast2PotentiometerValue;
            }

            ballast1 = Mathf.Clamp(rawBallast1, 0, 1000);
            ballast2 = Mathf.Clamp(rawBallast2, 0, 1000);
        }

        private static bool ShouldEmit(ref float timer, float hz)
        {
            float period = 1f / Mathf.Max(0.1f, hz);
            if (timer < period)
            {
                return false;
            }

            timer -= period;
            if (timer > period)
            {
                timer = 0f;
            }
            return true;
        }

        private void TryOpenSerial()
        {
            if (IsSerialOpen())
            {
                return;
            }

            foreach (string candidate in BuildPortCandidates())
            {
                try
                {
                    serialStream = new FileStream(
                        candidate,
                        FileMode.Open,
                        FileAccess.Write,
                        FileShare.ReadWrite
                    );
                    openedPortName = candidate;
                    if (portName != candidate)
                    {
                        portName = candidate;
                    }

                    Debug.Log($"[SensorManager] Port pseudo-serie ouvert: {candidate} (baud configure via socat/stty: {baudRate})");
                    return;
                }
                catch
                {
                    CloseSerial();
                }
            }

            Debug.LogWarning($"[SensorManager] Impossible d'ouvrir les ports series candidats: {string.Join(", ", BuildPortCandidates())}");
        }

        private string[] BuildPortCandidates()
        {
            List<string> candidates = new List<string>();

            string primary = NormalizeLegacyPortName(portName);
            if (!string.IsNullOrWhiteSpace(primary))
            {
                candidates.Add(primary);
            }

            for (int i = 0; i < PortFallbacks.Length; i++)
            {
                string candidate = PortFallbacks[i];
                if (!candidates.Contains(candidate))
                {
                    candidates.Add(candidate);
                }
            }

            return candidates.ToArray();
        }

        private static string NormalizeLegacyPortName(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "/dev/unity_sensors" || value == "/dev/unity_motors")
            {
                return SharedSerialPort;
            }

            return value;
        }

        private void CloseSerial()
        {
            if (serialStream == null)
            {
                return;
            }

            try
            {
                serialStream.Close();
            }
            catch
            {
            }

            serialStream.Dispose();
            serialStream = null;
            openedPortName = null;
        }

        private byte[] BuildFrame()
        {
            byte[] buf = new byte[FrameLen];
            buf[0] = Sync;

            PackI16(buf, 1, ToI16(qNed.w, 10000f));
            PackI16(buf, 3, ToI16(qNed.x, 10000f));
            PackI16(buf, 5, ToI16(qNed.y, 10000f));
            PackI16(buf, 7, ToI16(qNed.z, 10000f));

            PackI16(buf, 9, EncodeGyroToIcm20948(gyroNed.x));
            PackI16(buf, 11, EncodeGyroToIcm20948(gyroNed.y));
            PackI16(buf, 13, EncodeGyroToIcm20948(gyroNed.z));

            PackI16(buf, 15, EncodeAccelToIcm20948(accelNed.x));
            PackI16(buf, 17, EncodeAccelToIcm20948(accelNed.y));
            PackI16(buf, 19, EncodeAccelToIcm20948(accelNed.z));

            PackI16(buf, 21, EncodeMagToIcm20948(magnetoNed.x));
            PackI16(buf, 23, EncodeMagToIcm20948(magnetoNed.y));
            PackI16(buf, 25, EncodeMagToIcm20948(magnetoNed.z));

            PackI16(buf, 27, ToI16(depthMeters, 1000f));
            PackU16(buf, 29, (ushort)ballast1);
            PackU16(buf, 31, (ushort)ballast2);

            return buf;
        }

        private static int EncodeAccelToIcm20948(float accelMps2)
        {
            float accelG = accelMps2 / StandardGravityMps2;
            float lsbPerG = 32768f / Icm20948AccelFullScaleG;
            return ClampToI16(accelG * lsbPerG);
        }

        private static int EncodeGyroToIcm20948(float gyroRadPerSec)
        {
            float gyroDps = gyroRadPerSec * Mathf.Rad2Deg;
            float lsbPerDps = 32768f / Icm20948GyroFullScaleDps;
            return ClampToI16(gyroDps * lsbPerDps);
        }

        private static int EncodeMagToIcm20948(float magneticFieldMicroTesla)
        {
            return ClampToI16(magneticFieldMicroTesla / Icm20948MagMicroTeslaPerLsb);
        }

        private static int ToI16(float value, float scale)
        {
            return ClampToI16(value * scale);
        }

        private static int ClampToI16(float value)
        {
            if (value > 32767f) value = 32767f;
            if (value < -32768f) value = -32768f;
            return (short)Mathf.RoundToInt(value);
        }

        private static void PackI16(byte[] dst, int index, int value)
        {
            short v = (short)value;
            dst[index] = (byte)(v & 0xFF);
            dst[index + 1] = (byte)((v >> 8) & 0xFF);
        }

        private static void PackU16(byte[] dst, int index, ushort value)
        {
            dst[index] = (byte)(value & 0xFF);
            dst[index + 1] = (byte)((value >> 8) & 0xFF);
        }

        private void ApplyDefaultSerialConfig()
        {
            if (string.IsNullOrWhiteSpace(portName) || portName == "/tmp/ttyV0" || portName == "/dev/esp32_xiao")
            {
                portName = "/dev/unity_sensors";
            }
        }
    }
}
