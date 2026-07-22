// using System;
// using System.Collections.Generic;
// using System.IO;
// using System.Threading;
// using NaughtyWaterBuoyancy;
// using UnityEngine;

// public class ControlManager : MonoBehaviour
// {
//     private const string SharedSerialPort = "/dev/unity_esp32";
//     private static readonly string[] PortFallbacks = { SharedSerialPort, "/dev/unity_motors", "/dev/unity_sensors", "/dev/ttyACM0" };
//     private const byte Sync = 0xAA;
//     private const int FrameLen = 7;

//     [Serializable]
//     private struct MotorFrame
//     {
//         public byte leftThrusterByte;
//         public byte rightThrusterByte;
//         public short dc1;
//         public short dc2;
//         public double receivedAtSeconds;
//     }

//     [Header("Serial Input")]
//     [ReadOnly] public string portName = "/dev/unity_esp32";
//     [ReadOnly] public int baudRate = 115200;
//     public bool autoOpenOnStart = false;
//     [ReadOnly, Min(0.01f)] public float commandTimeoutSeconds = 0.25f;

//     [Header("References")]
//     public Rigidbody rb;
//     public Transform leftThruster;
//     public Transform rightThruster;
//     public FloatingObject floatingObject;
//     public bool useThrusterTransformForward;

//     [Header("Thrusters")]
//     [Min(0f)] public float thrusterMinForce = 0f;
//     [Min(0f)] public float thrusterMaxForce = 2f;
//     [Range(0, 255)] public int thrusterDeadzonePwm = 10;

//     [Header("Ballast Motors")]
//     [Min(0f)] public float ballastMaxTravelRateMmPerMin = 250f;
//     [Range(0, 255)] public int dcMotorDeadzonePwm = 10;

//     [Header("Diagnostics")]
//     [ReadOnly] public bool connected;
//     [ReadOnly] public float measuredHz;
//     [ReadOnly] public byte leftThrusterByte;
//     [ReadOnly] public byte rightThrusterByte;
//     [ReadOnly] public int leftThrusterCommand;
//     [ReadOnly] public int rightThrusterCommand;
//     [ReadOnly] public int ballast1MotorCommand;
//     [ReadOnly] public int ballast2MotorCommand;
//     [ReadOnly] public float leftThrusterForce;
//     [ReadOnly] public float rightThrusterForce;
//     [ReadOnly] public float propulsionForce;
//     [ReadOnly] public float yawDifferentialForce;

//     private readonly object frameLock = new object();
//     private FileStream serialStream;
//     private Thread readThread;
//     private volatile bool running;
//     private volatile bool connectedState;
//     private volatile float measuredHzState;
//     private MotorFrame latestFrame;
//     private string openedPortName;

//     private void Reset()
//     {
//         CacheReferences();
//     }

//     private void OnValidate()
//     {
//         portName = NormalizeLegacyPortName(portName);
//         CacheReferences();
//         thrusterMaxForce = Mathf.Max(thrusterMinForce, thrusterMaxForce);
//         ballastMaxTravelRateMmPerMin = Mathf.Max(0f, ballastMaxTravelRateMmPerMin);
//     }

//     private void Start()
//     {
//         portName = NormalizeLegacyPortName(portName);
//         CacheReferences();
//         if (autoOpenOnStart)
//         {
//             StartReader();
//         }
//     }

//     private void FixedUpdate()
//     {
//         connected = connectedState;
//         measuredHz = measuredHzState;

//         MotorFrame frame;
//         lock (frameLock)
//         {
//             frame = latestFrame;
//         }

//         bool commandIsFresh = frame.receivedAtSeconds > 0d && (GetNowSeconds() - frame.receivedAtSeconds) <= commandTimeoutSeconds;

//         leftThrusterByte = frame.leftThrusterByte;
//         rightThrusterByte = frame.rightThrusterByte;

//         if (commandIsFresh)
//         {
//             leftThrusterCommand = DecodeThrusterByte(frame.leftThrusterByte);
//             rightThrusterCommand = DecodeThrusterByte(frame.rightThrusterByte);
//             ballast1MotorCommand = Mathf.Clamp(frame.dc1, -255, 255);
//             ballast2MotorCommand = Mathf.Clamp(frame.dc2, -255, 255);
//         }
//         else
//         {
//             leftThrusterCommand = 0;
//             rightThrusterCommand = 0;
//             ballast1MotorCommand = 0;
//             ballast2MotorCommand = 0;
//         }

//         leftThrusterForce = ComputeThrusterForce(leftThrusterCommand);
//         rightThrusterForce = ComputeThrusterForce(rightThrusterCommand);

//         propulsionForce = leftThrusterForce + rightThrusterForce;
//         yawDifferentialForce = 0.5f * (leftThrusterForce - rightThrusterForce);

//         ApplyThrusterForce(leftThruster, leftThrusterForce);
//         ApplyThrusterForce(rightThruster, rightThrusterForce);

//         if (floatingObject != null)
//         {
//             floatingObject.ApplyBallastMotorCommands(
//                 ballast1MotorCommand,
//                 ballast2MotorCommand,
//                 dcMotorDeadzonePwm,
//                 ballastMaxTravelRateMmPerMin,
//                 Time.fixedDeltaTime
//             );
//         }
//     }

//     private void OnDisable()
//     {
//         StopReader();
//     }

//     private void OnApplicationQuit()
//     {
//         StopReader();
//     }

//     private void CacheReferences()
//     {
//         if (rb == null)
//         {
//             rb = GetComponent<Rigidbody>();
//         }

//         if (floatingObject == null)
//         {
//             floatingObject = GetComponent<FloatingObject>();
//         }

//         if (leftThruster == null)
//         {
//             Transform child = transform.Find("propulseur_gauche");
//             if (child != null)
//             {
//                 leftThruster = child;
//             }
//         }

//         if (rightThruster == null)
//         {
//             Transform child = transform.Find("propulseur_droite");
//             if (child != null)
//             {
//                 rightThruster = child;
//             }
//         }
//     }

//     private void StartReader()
//     {
//         if (running)
//         {
//             return;
//         }

//         running = true;
//         readThread = new Thread(ReadLoop)
//         {
//             IsBackground = true,
//             Name = "UnityMotorSerialReader"
//         };
//         readThread.Start();
//     }

//     private void StopReader()
//     {
//         running = false;
//         CloseSerial();

//         if (readThread != null && readThread.IsAlive)
//         {
//             readThread.Join(500);
//         }

//         readThread = null;
//         connectedState = false;
//         measuredHzState = 0f;
//     }

//     private void ReadLoop()
//     {
//         byte[] payload = new byte[FrameLen - 1];
//         double windowStart = GetNowSeconds();
//         int framesInWindow = 0;

//         while (running)
//         {
//             if (serialStream == null)
//             {
//                 if (!TryOpenSerial())
//                 {
//                     connectedState = false;
//                     measuredHzState = 0f;
//                     Thread.Sleep(1000);
//                     continue;
//                 }

//                 windowStart = GetNowSeconds();
//                 framesInWindow = 0;
//             }

//             try
//             {
//                 int sync = serialStream.ReadByte();
//                 if (sync < 0)
//                 {
//                     Thread.Sleep(1);
//                     continue;
//                 }

//                 if (sync != Sync)
//                 {
//                     continue;
//                 }

//                 if (!ReadExact(payload, payload.Length))
//                 {
//                     CloseSerial();
//                     connectedState = false;
//                     measuredHzState = 0f;
//                     Thread.Sleep(200);
//                     continue;
//                 }

//                 MotorFrame frame = new MotorFrame
//                 {
//                     leftThrusterByte = payload[0],
//                     rightThrusterByte = payload[1],
//                     dc1 = (short)(payload[2] | (payload[3] << 8)),
//                     dc2 = (short)(payload[4] | (payload[5] << 8)),
//                     receivedAtSeconds = GetNowSeconds()
//                 };

//                 lock (frameLock)
//                 {
//                     latestFrame = frame;
//                 }

//                 connectedState = true;
//                 framesInWindow += 1;

//                 double now = GetNowSeconds();
//                 double windowDuration = now - windowStart;
//                 if (windowDuration >= 0.5d)
//                 {
//                     measuredHzState = (float)(framesInWindow / windowDuration);
//                     windowStart = now;
//                     framesInWindow = 0;
//                 }
//             }
//             catch (Exception e)
//             {
//                 if (running)
//                 {
//                     string activePort = string.IsNullOrEmpty(openedPortName) ? portName : openedPortName;
//                     Debug.LogWarning($"[ControlManager] Lecture serie impossible sur {activePort}: {e.Message}");
//                 }
//                 CloseSerial();
//                 connectedState = false;
//                 measuredHzState = 0f;
//                 Thread.Sleep(200);
//             }
//         }
//     }

//     private bool TryOpenSerial()
//     {
//         foreach (string candidate in BuildPortCandidates())
//         {
//             try
//             {
//                 serialStream = new FileStream(
//                     candidate,
//                     FileMode.Open,
//                     FileAccess.Read,
//                     FileShare.ReadWrite
//                 );
//                 openedPortName = candidate;
//                 if (portName != candidate)
//                 {
//                     portName = candidate;
//                 }

//                 Debug.Log($"[ControlManager] Port pseudo-serie ouvert: {candidate} (baud configure via socat/stty: {baudRate})");
//                 connectedState = true;
//                 return true;
//             }
//             catch
//             {
//                 CloseSerial();
//             }
//         }

//         Debug.LogWarning($"[ControlManager] Impossible d'ouvrir les ports series candidats: {string.Join(", ", BuildPortCandidates())}");
//         return false;
//     }

//     private string[] BuildPortCandidates()
//     {
//         List<string> candidates = new List<string>();

//         string primary = NormalizeLegacyPortName(portName);
//         if (!string.IsNullOrWhiteSpace(primary))
//         {
//             candidates.Add(primary);
//         }

//         for (int i = 0; i < PortFallbacks.Length; i++)
//         {
//             string candidate = PortFallbacks[i];
//             if (!candidates.Contains(candidate))
//             {
//                 candidates.Add(candidate);
//             }
//         }

//         return candidates.ToArray();
//     }

//     private static string NormalizeLegacyPortName(string value)
//     {
//         if (string.IsNullOrWhiteSpace(value) || value == "/dev/unity_motors" || value == "/dev/unity_sensors")
//         {
//             return SharedSerialPort;
//         }

//         return value;
//     }

//     private bool ReadExact(byte[] buffer, int count)
//     {
//         int offset = 0;
//         while (offset < count && running)
//         {
//             int read = serialStream.Read(buffer, offset, count - offset);
//             if (read <= 0)
//             {
//                 return false;
//             }
//             offset += read;
//         }

//         return offset == count;
//     }

//     private void CloseSerial()
//     {
//         if (serialStream == null)
//         {
//             return;
//         }

//         try
//         {
//             serialStream.Close();
//         }
//         catch
//         {
//         }

//         serialStream.Dispose();
//         serialStream = null;
//         openedPortName = null;
//     }

//     private void ApplyThrusterForce(Transform thruster, float force)
//     {
//         if (rb == null || thruster == null || Mathf.Approximately(force, 0f))
//         {
//             return;
//         }

//         Vector3 direction = GetThrustDirection();
//         rb.AddForceAtPosition(direction * force, ProjectToCenterOfMassPlane(thruster.position), ForceMode.Force);
//     }

//     private Vector3 GetThrustDirection()
//     {
//         if (!useThrusterTransformForward)
//         {
//             return transform.forward;
//         }

//         Vector3 leftDirection = leftThruster != null ? Vector3.ProjectOnPlane(leftThruster.forward, transform.up) : Vector3.zero;
//         Vector3 rightDirection = rightThruster != null ? Vector3.ProjectOnPlane(rightThruster.forward, transform.up) : Vector3.zero;
//         Vector3 combinedDirection = leftDirection + rightDirection;

//         if (combinedDirection.sqrMagnitude <= 0.000001f)
//         {
//             return transform.forward;
//         }

//         return combinedDirection.normalized;
//     }

//     private Vector3 ProjectToCenterOfMassPlane(Vector3 worldPosition)
//     {
//         Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
//         Vector3 localCenterOfMass = transform.InverseTransformPoint(rb.worldCenterOfMass);
//         localPosition.y = localCenterOfMass.y;
//         return transform.TransformPoint(localPosition);
//     }

//     private float ComputeThrusterForce(int signedPwm)
//     {
//         int clamped = Mathf.Clamp(signedPwm, -255, 255);
//         int absolutePwm = Mathf.Abs(clamped);
//         if (absolutePwm <= thrusterDeadzonePwm)
//         {
//             return 0f;
//         }

//         float normalized = (absolutePwm - thrusterDeadzonePwm) / Mathf.Max(1f, 255f - thrusterDeadzonePwm);
//         float magnitude = Mathf.Lerp(thrusterMinForce, thrusterMaxForce, normalized);
//         return Mathf.Sign(clamped) * magnitude;
//     }

//     private static int DecodeThrusterByte(byte value)
//     {
//         float normalized = value / 255f;
//         return Mathf.Clamp(Mathf.RoundToInt((normalized * 510f) - 255f), -255, 255);
//     }

//     private static double GetNowSeconds()
//     {
//         return (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
//     }
// }







using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using NaughtyWaterBuoyancy;
using UnityEngine;

public class ControlManager : MonoBehaviour
{
    private const string SharedSerialPort = "/dev/unity_esp32";
    private static readonly string[] PortFallbacks = { SharedSerialPort, "/dev/unity_motors", "/dev/unity_sensors", "/dev/ttyACM0" };
    private const byte Sync = 0xAA;

    // Frame format from rasp:
    // 0xAA + int16 thruster_port + int16 thruster_stbd + int16 dc_port + int16 dc_stbd
    private const int FrameLen = 9;

    [Serializable]
    private struct MotorFrame
    {
        public short leftThruster;
        public short rightThruster;
        public short dc1;
        public short dc2;
        public double receivedAtSeconds;
    }

    [Header("Serial Input")]
    [ReadOnly] public string portName = "/dev/unity_esp32";
    [ReadOnly] public int baudRate = 115200;
    public bool autoOpenOnStart = false;
    [ReadOnly, Min(0.01f)] public float commandTimeoutSeconds = 0.25f;

    [Header("References")]
    public Rigidbody rb;
    public Transform leftThruster;
    public Transform rightThruster;
    public FloatingObject floatingObject;
    public bool useThrusterTransformForward;

    [Header("Thrusters")]
    [Min(0f)] public float thrusterMinForce = 0f;
    [Min(0f)] public float thrusterMaxForce = 2f;
    [Range(0, 255)] public int thrusterDeadzonePwm = 10;

    [Header("Ballast Motors")]
    [Min(0f)] public float ballastMaxTravelRateMmPerMin = 250f;
    [Range(0, 255)] public int dcMotorDeadzonePwm = 10;

    [Header("Diagnostics")]
    [ReadOnly] public bool connected;
    [ReadOnly] public float measuredHz;
    [ReadOnly] public int leftThrusterRaw;
    [ReadOnly] public int rightThrusterRaw;
    [ReadOnly] public int leftThrusterCommand;
    [ReadOnly] public int rightThrusterCommand;
    [ReadOnly] public int ballast1MotorCommand;
    [ReadOnly] public int ballast2MotorCommand;
    [ReadOnly] public float leftThrusterForce;
    [ReadOnly] public float rightThrusterForce;
    [ReadOnly] public float propulsionForce;
    [ReadOnly] public float yawDifferentialForce;

    private readonly object frameLock = new object();
    private FileStream serialStream;
    private Thread readThread;
    private volatile bool running;
    private volatile bool connectedState;
    private volatile float measuredHzState;
    private MotorFrame latestFrame;
    private string openedPortName;

    private void Reset()
    {
        CacheReferences();
    }

    private void OnValidate()
    {
        portName = NormalizeLegacyPortName(portName);
        CacheReferences();
        thrusterMaxForce = Mathf.Max(thrusterMinForce, thrusterMaxForce);
        ballastMaxTravelRateMmPerMin = Mathf.Max(0f, ballastMaxTravelRateMmPerMin);
    }

    private void Start()
    {
        portName = NormalizeLegacyPortName(portName);
        CacheReferences();

        if (autoOpenOnStart)
        {
            StartReader();
        }
    }

    private void FixedUpdate()
    {
        connected = connectedState;
        measuredHz = measuredHzState;

        MotorFrame frame;
        lock (frameLock)
        {
            frame = latestFrame;
        }

        bool commandIsFresh =
            frame.receivedAtSeconds > 0d &&
            (GetNowSeconds() - frame.receivedAtSeconds) <= commandTimeoutSeconds;

        leftThrusterRaw = frame.leftThruster;
        rightThrusterRaw = frame.rightThruster;

        if (commandIsFresh)
        {
            leftThrusterCommand = Mathf.Clamp(frame.leftThruster, -255, 255);
            rightThrusterCommand = Mathf.Clamp(frame.rightThruster, -255, 255);
            ballast1MotorCommand = Mathf.Clamp(frame.dc1, -255, 255);
            ballast2MotorCommand = Mathf.Clamp(frame.dc2, -255, 255);
        }
        else
        {
            leftThrusterCommand = 0;
            rightThrusterCommand = 0;
            ballast1MotorCommand = 0;
            ballast2MotorCommand = 0;
        }

        leftThrusterForce = ComputeThrusterForce(leftThrusterCommand);
        rightThrusterForce = ComputeThrusterForce(rightThrusterCommand);

        propulsionForce = leftThrusterForce + rightThrusterForce;
        yawDifferentialForce = 0.5f * (leftThrusterForce - rightThrusterForce);

        ApplyThrusterForce(leftThruster, leftThrusterForce);
        ApplyThrusterForce(rightThruster, rightThrusterForce);

        if (floatingObject != null)
        {
            floatingObject.ApplyBallastMotorCommands(
                ballast1MotorCommand,
                ballast2MotorCommand,
                dcMotorDeadzonePwm,
                ballastMaxTravelRateMmPerMin,
                Time.fixedDeltaTime
            );
        }
    }

    public void InjectMotorFrame(short leftThruster, short rightThruster, short dc1, short dc2)
    {
        MotorFrame frame = new MotorFrame
        {
            leftThruster = leftThruster,
            rightThruster = rightThruster,
            dc1 = dc1,
            dc2 = dc2,
            receivedAtSeconds = GetNowSeconds()
        };

        lock (frameLock)
        {
            latestFrame = frame;
        }

        connectedState = true;
    }

    private void OnDisable()
    {
        StopReader();
    }

    private void OnApplicationQuit()
    {
        StopReader();
    }

    private void CacheReferences()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (floatingObject == null)
        {
            floatingObject = GetComponent<FloatingObject>();
        }

        if (leftThruster == null)
        {
            Transform child = transform.Find("propulseur_gauche");
            if (child != null)
            {
                leftThruster = child;
            }
        }

        if (rightThruster == null)
        {
            Transform child = transform.Find("propulseur_droite");
            if (child != null)
            {
                rightThruster = child;
            }
        }
    }

    private void StartReader()
    {
        if (running)
        {
            return;
        }

        running = true;
        readThread = new Thread(ReadLoop)
        {
            IsBackground = true,
            Name = "UnityMotorSerialReader"
        };
        readThread.Start();
    }

    private void StopReader()
    {
        running = false;
        CloseSerial();

        if (readThread != null && readThread.IsAlive)
        {
            readThread.Join(500);
        }

        readThread = null;
        connectedState = false;
        measuredHzState = 0f;
    }

    private void ReadLoop()
    {
        byte[] payload = new byte[FrameLen - 1];
        double windowStart = GetNowSeconds();
        int framesInWindow = 0;

        while (running)
        {
            if (serialStream == null)
            {
                if (!TryOpenSerial())
                {
                    connectedState = false;
                    measuredHzState = 0f;
                    Thread.Sleep(1000);
                    continue;
                }

                windowStart = GetNowSeconds();
                framesInWindow = 0;
            }

            try
            {
                int sync = serialStream.ReadByte();
                if (sync < 0)
                {
                    Thread.Sleep(1);
                    continue;
                }

                if (sync != Sync)
                {
                    continue;
                }

                if (!ReadExact(payload, payload.Length))
                {
                    CloseSerial();
                    connectedState = false;
                    measuredHzState = 0f;
                    Thread.Sleep(200);
                    continue;
                }

                MotorFrame frame = new MotorFrame
                {
                    leftThruster = ReadInt16LE(payload, 0),
                    rightThruster = ReadInt16LE(payload, 2),
                    dc1 = ReadInt16LE(payload, 4),
                    dc2 = ReadInt16LE(payload, 6),
                    receivedAtSeconds = GetNowSeconds()
                };

                lock (frameLock)
                {
                    latestFrame = frame;
                }

                connectedState = true;
                framesInWindow += 1;

                double now = GetNowSeconds();
                double windowDuration = now - windowStart;
                if (windowDuration >= 0.5d)
                {
                    measuredHzState = (float)(framesInWindow / windowDuration);
                    windowStart = now;
                    framesInWindow = 0;
                }
            }
            catch (Exception e)
            {
                if (running)
                {
                    string activePort = string.IsNullOrEmpty(openedPortName) ? portName : openedPortName;
                    Debug.LogWarning($"[ControlManager] Lecture serie impossible sur {activePort}: {e.Message}");
                }

                CloseSerial();
                connectedState = false;
                measuredHzState = 0f;
                Thread.Sleep(200);
            }
        }
    }

    private bool TryOpenSerial()
    {
        foreach (string candidate in BuildPortCandidates())
        {
            try
            {
                serialStream = new FileStream(
                    candidate,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.ReadWrite
                );

                openedPortName = candidate;
                if (portName != candidate)
                {
                    portName = candidate;
                }

                Debug.Log($"[ControlManager] Port pseudo-serie ouvert: {candidate} (baud configure via socat/stty: {baudRate})");
                connectedState = true;
                return true;
            }
            catch
            {
                CloseSerial();
            }
        }

        Debug.LogWarning($"[ControlManager] Impossible d'ouvrir les ports series candidats: {string.Join(", ", BuildPortCandidates())}");
        return false;
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
        if (string.IsNullOrWhiteSpace(value) || value == "/dev/unity_motors" || value == "/dev/unity_sensors")
        {
            return SharedSerialPort;
        }

        return value;
    }

    private bool ReadExact(byte[] buffer, int count)
    {
        int offset = 0;
        while (offset < count && running)
        {
            int read = serialStream.Read(buffer, offset, count - offset);
            if (read <= 0)
            {
                return false;
            }

            offset += read;
        }

        return offset == count;
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

    private void ApplyThrusterForce(Transform thruster, float force)
    {
        if (rb == null || thruster == null || Mathf.Approximately(force, 0f))
        {
            return;
        }

        Vector3 direction = GetThrustDirection();
        rb.AddForceAtPosition(direction * force, ProjectToCenterOfMassPlane(thruster.position), ForceMode.Force);
    }

    private Vector3 GetThrustDirection()
    {
        if (!useThrusterTransformForward)
        {
            return transform.forward;
        }

        Vector3 leftDirection = leftThruster != null
            ? Vector3.ProjectOnPlane(leftThruster.forward, transform.up)
            : Vector3.zero;

        Vector3 rightDirection = rightThruster != null
            ? Vector3.ProjectOnPlane(rightThruster.forward, transform.up)
            : Vector3.zero;

        Vector3 combinedDirection = leftDirection + rightDirection;

        if (combinedDirection.sqrMagnitude <= 0.000001f)
        {
            return transform.forward;
        }

        return combinedDirection.normalized;
    }

    private Vector3 ProjectToCenterOfMassPlane(Vector3 worldPosition)
    {
        Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
        Vector3 localCenterOfMass = transform.InverseTransformPoint(rb.worldCenterOfMass);
        localPosition.y = localCenterOfMass.y;
        return transform.TransformPoint(localPosition);
    }

    private float ComputeThrusterForce(int signedPwm)
    {
        int clamped = Mathf.Clamp(signedPwm, -255, 255);
        int absolutePwm = Mathf.Abs(clamped);

        if (absolutePwm <= thrusterDeadzonePwm)
        {
            return 0f;
        }

        float normalized = (absolutePwm - thrusterDeadzonePwm) / Mathf.Max(1f, 255f - thrusterDeadzonePwm);
        float magnitude = Mathf.Lerp(thrusterMinForce, thrusterMaxForce, normalized);

        return Mathf.Sign(clamped) * magnitude;
    }

    private static short ReadInt16LE(byte[] buffer, int offset)
    {
        return unchecked((short)(buffer[offset] | (buffer[offset + 1] << 8)));
    }

    private static double GetNowSeconds()
    {
        return (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
    }
}