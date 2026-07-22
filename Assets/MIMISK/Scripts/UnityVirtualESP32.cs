using System;
using System.IO;
using System.Threading;
using UnityEngine;

public class UnityVirtualESP32 : MonoBehaviour
{
    private const byte MotorSync = 0xAA;
    private const byte SensorSync = 0xBB;

    private const int MotorFrameLen = 9;
    private const int SensorFrameLen = 33;

    private const float QuaternionScale = 10000.0f;
    private const float DepthScale = 1000.0f;
    private const float StandardGravity = 9.80665f;
    private const float AccelFullScaleG = 16.0f;
    private const float GyroFullScaleDps = 2000.0f;
    private const float MagMicroTeslaPerLsb = 0.15f;

    [Header("Serial")]
    public string portName = "/dev/unity_esp32";
    public int baudRate = 115200;
    public bool autoOpenOnStart = true;
    [Range(1, 200)] public int sensorPublishHz = 50;

    [Header("References")]
    public Rigidbody rb;
    public ControlManager controlManager;

    [Header("Sensor Emulation")]
    public float waterLevel = 0.0f;
    public Vector3 magneticFieldBodyMicroTesla = new Vector3(30.0f, 0.0f, 40.0f);
    [Range(0, 500)] public int ballastPortRaw = 0;
    [Range(0, 500)] public int ballastStarboardRaw = 0;
    public bool yawOnlyImu = true;
    public bool invertYawSign = false;

    [Header("Diagnostics")]
    public bool motorRxConnected;
    public bool sensorTxConnected;
    public float motorRxHz;
    public float sensorTxHz;
    public int lastLeftThruster;
    public int lastRightThruster;
    public int lastDcPort;
    public int lastDcStarboard;
    public float depthMeters;
    public float yawDeg;
    public int sensorFramesSent;

    private volatile bool running;

    private FileStream readStream;
    private FileStream writeStream;

    private Thread readThread;
    private Thread writeThread;

    private readonly object sensorFrameLock = new object();
    private byte[] latestSensorFrame;

    private Vector3 lastLinearVelocity;
    private bool hasLastVelocity;

    private void Awake()
    {
        CacheReferences();

        if (controlManager != null)
        {
            controlManager.autoOpenOnStart = false;
        }
    }

    private void Start()
    {
        CacheReferences();

        if (rb != null)
        {
            lastLinearVelocity = rb.linearVelocity;
            hasLastVelocity = true;
        }

        if (autoOpenOnStart)
        {
            StartBridge();
        }
    }

    private void FixedUpdate()
    {
        BuildLatestSensorFrame(Time.fixedDeltaTime);
    }

    private void OnDisable()
    {
        StopBridge();
    }

    private void OnApplicationQuit()
    {
        StopBridge();
    }

    private void CacheReferences()
    {
        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (controlManager == null)
        {
            controlManager = GetComponent<ControlManager>();
        }
    }

    public void StartBridge()
    {
        if (running)
        {
            return;
        }

        running = true;

        readThread = new Thread(ReadLoop)
        {
            IsBackground = true,
            Name = "UnityVirtualESP32_MotorRx"
        };

        writeThread = new Thread(WriteLoop)
        {
            IsBackground = true,
            Name = "UnityVirtualESP32_SensorTx"
        };

        readThread.Start();
        writeThread.Start();
    }

    public void StopBridge()
    {
        running = false;

        CloseReadStream();
        CloseWriteStream();

        if (readThread != null && readThread.IsAlive)
        {
            readThread.Join(500);
        }

        if (writeThread != null && writeThread.IsAlive)
        {
            writeThread.Join(500);
        }

        readThread = null;
        writeThread = null;

        motorRxConnected = false;
        sensorTxConnected = false;
        motorRxHz = 0.0f;
        sensorTxHz = 0.0f;
    }

    private void BuildLatestSensorFrame(float dt)
    {
        if (rb == null)
        {
            return;
        }

        byte[] frame = new byte[SensorFrameLen];
        frame[0] = SensorSync;

        depthMeters = Mathf.Max(0.0f, waterLevel - transform.position.y);

        float yawRad = Mathf.Atan2(transform.forward.x, transform.forward.z);
        if (invertYawSign)
        {
            yawRad = -yawRad;
        }

        yawDeg = yawRad * Mathf.Rad2Deg;

        float halfYaw = yawRad * 0.5f;

        float qw = Mathf.Cos(halfYaw);
        float qx = 0.0f;
        float qy = 0.0f;
        float qz = Mathf.Sin(halfYaw);

        if (!yawOnlyImu)
        {
            // Keep this disabled until roll/pitch convention is validated.
            // For now the real platform validation phase should use yaw-only IMU
            // because Rigidbody roll/pitch are intentionally frozen.
        }

        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 worldAccel = Vector3.zero;

        if (hasLastVelocity && dt > 0.0001f)
        {
            worldAccel = (currentVelocity - lastLinearVelocity) / dt;
        }

        lastLinearVelocity = currentVelocity;
        hasLastVelocity = true;

        Vector3 localAccel = transform.InverseTransformDirection(worldAccel);

        float surgeAccel = localAccel.z;
        float swayAccel = localAccel.x;
        float heaveAccel = -localAccel.y;

        float rollRate = 0.0f;
        float pitchRate = 0.0f;
        float yawRate = rb.angularVelocity.y;

        PackI16(frame, 1, ToI16(qw, QuaternionScale));
        PackI16(frame, 3, ToI16(qx, QuaternionScale));
        PackI16(frame, 5, ToI16(qy, QuaternionScale));
        PackI16(frame, 7, ToI16(qz, QuaternionScale));

        PackI16(frame, 9, EncodeGyro(rollRate));
        PackI16(frame, 11, EncodeGyro(pitchRate));
        PackI16(frame, 13, EncodeGyro(yawRate));

        PackI16(frame, 15, EncodeAccel(surgeAccel));
        PackI16(frame, 17, EncodeAccel(swayAccel));
        PackI16(frame, 19, EncodeAccel(heaveAccel));

        PackI16(frame, 21, EncodeMag(magneticFieldBodyMicroTesla.x));
        PackI16(frame, 23, EncodeMag(magneticFieldBodyMicroTesla.y));
        PackI16(frame, 25, EncodeMag(magneticFieldBodyMicroTesla.z));

        PackI16(frame, 27, ToI16(depthMeters, DepthScale));
        PackU16(frame, 29, (ushort)Mathf.Clamp(ballastPortRaw, 0, 500));
        PackU16(frame, 31, (ushort)Mathf.Clamp(ballastStarboardRaw, 0, 500));

        lock (sensorFrameLock)
        {
            latestSensorFrame = frame;
        }
    }

    private void ReadLoop()
    {
        byte[] payload = new byte[MotorFrameLen - 1];

        double windowStart = NowSeconds();
        int framesInWindow = 0;

        while (running)
        {
            if (readStream == null)
            {
                if (!TryOpenReadStream())
                {
                    motorRxConnected = false;
                    motorRxHz = 0.0f;
                    Thread.Sleep(1000);
                    continue;
                }

                windowStart = NowSeconds();
                framesInWindow = 0;
            }

            try
            {
                int sync = readStream.ReadByte();

                if (sync < 0)
                {
                    Thread.Sleep(1);
                    continue;
                }

                if (sync != MotorSync)
                {
                    continue;
                }

                if (!ReadExact(readStream, payload, payload.Length))
                {
                    CloseReadStream();
                    motorRxConnected = false;
                    motorRxHz = 0.0f;
                    Thread.Sleep(200);
                    continue;
                }

                short left = ReadInt16LE(payload, 0);
                short right = ReadInt16LE(payload, 2);
                short dc1 = ReadInt16LE(payload, 4);
                short dc2 = ReadInt16LE(payload, 6);

                lastLeftThruster = left;
                lastRightThruster = right;
                lastDcPort = dc1;
                lastDcStarboard = dc2;

                if (controlManager != null)
                {
                    controlManager.InjectMotorFrame(left, right, dc1, dc2);
                }

                motorRxConnected = true;

                framesInWindow++;

                double now = NowSeconds();
                double duration = now - windowStart;

                if (duration >= 0.5)
                {
                    motorRxHz = (float)(framesInWindow / duration);
                    windowStart = now;
                    framesInWindow = 0;
                }
            }
            catch
            {
                CloseReadStream();
                motorRxConnected = false;
                motorRxHz = 0.0f;
                Thread.Sleep(200);
            }
        }
    }

    private void WriteLoop()
    {
        int hz = Mathf.Max(1, sensorPublishHz);
        int sleepMs = Mathf.Max(1, Mathf.RoundToInt(1000.0f / hz));

        double windowStart = NowSeconds();
        int framesInWindow = 0;

        while (running)
        {
            if (writeStream == null)
            {
                if (!TryOpenWriteStream())
                {
                    sensorTxConnected = false;
                    sensorTxHz = 0.0f;
                    Thread.Sleep(1000);
                    continue;
                }

                windowStart = NowSeconds();
                framesInWindow = 0;
            }

            byte[] frame = null;

            lock (sensorFrameLock)
            {
                if (latestSensorFrame != null)
                {
                    frame = (byte[])latestSensorFrame.Clone();
                }
            }

            if (frame != null)
            {
                try
                {
                    writeStream.Write(frame, 0, frame.Length);
                    writeStream.Flush();

                    sensorFramesSent++;
                    sensorTxConnected = true;
                    framesInWindow++;

                    double now = NowSeconds();
                    double duration = now - windowStart;

                    if (duration >= 0.5)
                    {
                        sensorTxHz = (float)(framesInWindow / duration);
                        windowStart = now;
                        framesInWindow = 0;
                    }
                }
                catch
                {
                    CloseWriteStream();
                    sensorTxConnected = false;
                    sensorTxHz = 0.0f;
                    Thread.Sleep(200);
                }
            }

            Thread.Sleep(sleepMs);
        }
    }

    private bool TryOpenReadStream()
    {
        try
        {
            readStream = new FileStream(portName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            Debug.Log("[UnityVirtualESP32] Motor RX opened: " + portName);
            motorRxConnected = true;
            return true;
        }
        catch
        {
            CloseReadStream();
            return false;
        }
    }

    private bool TryOpenWriteStream()
    {
        try
        {
            writeStream = new FileStream(portName, FileMode.Open, FileAccess.Write, FileShare.ReadWrite);
            Debug.Log("[UnityVirtualESP32] Sensor TX opened: " + portName);
            sensorTxConnected = true;
            return true;
        }
        catch
        {
            CloseWriteStream();
            return false;
        }
    }

    private void CloseReadStream()
    {
        if (readStream == null)
        {
            return;
        }

        try { readStream.Close(); } catch { }
        readStream.Dispose();
        readStream = null;
    }

    private void CloseWriteStream()
    {
        if (writeStream == null)
        {
            return;
        }

        try { writeStream.Close(); } catch { }
        writeStream.Dispose();
        writeStream = null;
    }

    private bool ReadExact(FileStream stream, byte[] buffer, int count)
    {
        int offset = 0;

        while (running && offset < count)
        {
            int n = stream.Read(buffer, offset, count - offset);

            if (n <= 0)
            {
                return false;
            }

            offset += n;
        }

        return offset == count;
    }

    private static short ReadInt16LE(byte[] buffer, int offset)
    {
        return unchecked((short)(buffer[offset] | (buffer[offset + 1] << 8)));
    }

    private static int EncodeAccel(float accelMps2)
    {
        float accelG = accelMps2 / StandardGravity;
        float lsbPerG = 32768.0f / AccelFullScaleG;
        return ClampToI16(accelG * lsbPerG);
    }

    private static int EncodeGyro(float gyroRadPerSec)
    {
        float gyroDps = gyroRadPerSec * Mathf.Rad2Deg;
        float lsbPerDps = 32768.0f / GyroFullScaleDps;
        return ClampToI16(gyroDps * lsbPerDps);
    }

    private static int EncodeMag(float microTesla)
    {
        return ClampToI16(microTesla / MagMicroTeslaPerLsb);
    }

    private static int ToI16(float value, float scale)
    {
        return ClampToI16(value * scale);
    }

    private static int ClampToI16(float value)
    {
        if (value > 32767.0f) value = 32767.0f;
        if (value < -32768.0f) value = -32768.0f;
        return Mathf.RoundToInt(value);
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

    private static double NowSeconds()
    {
        return (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
    }
}
