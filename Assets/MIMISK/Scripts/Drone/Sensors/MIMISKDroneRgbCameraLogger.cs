using System;
using System.Globalization;
using System.IO;
using UnityEngine;

public class MIMISKDroneRgbCameraLogger : MonoBehaviour
{
    public MIMISKDroneRgbCameraDevice cameraDevice;

    [Header("Logging")]
    public bool enableLogging = true;
    public bool flushEveryLine = false;

    [Header("Runtime")]
    public string currentLogPath;
    public int linesWritten;

    private StreamWriter writer;
    private int lastLoggedFrame = -1;

    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    private void Awake()
    {
        if (cameraDevice == null)
        {
            cameraDevice = GetComponent<MIMISKDroneRgbCameraDevice>();
        }
    }

    private void OnEnable()
    {
        if (enableLogging)
        {
            OpenLog();
        }
    }

    private void Update()
    {
        if (!enableLogging || cameraDevice == null)
        {
            return;
        }

        if (writer == null)
        {
            OpenLog();
        }

        if (cameraDevice.frameIndex != lastLoggedFrame)
        {
            WriteLine();
            lastLoggedFrame = cameraDevice.frameIndex;
        }
    }

    private void OnDisable()
    {
        CloseLog();
    }

    private void OnApplicationQuit()
    {
        CloseLog();
    }

    private void OpenLog()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string logDir = Path.Combine(projectRoot, "Logs", "MIMISKDrone");

        Directory.CreateDirectory(logDir);

        string fileName = "drone_rgb_camera_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".csv";
        currentLogPath = Path.Combine(logDir, fileName);

        writer = new StreamWriter(currentLogPath);

        writer.WriteLine(
            "time,device,frame_index,width,height,target_fps,measured_fps," +
            "pos_x,pos_y,pos_z,rot_x,rot_y,rot_z," +
            "forward_x,forward_y,forward_z,horizontal_fov_deg,near_clip,far_clip"
        );

        writer.Flush();

        Debug.Log("[MIMISKDroneRgbCameraLogger] Logging to: " + currentLogPath);
    }

    private void WriteLine()
    {
        string line = string.Join(",",
            F(Time.time),
            cameraDevice.deviceName,
            cameraDevice.frameIndex.ToString(Culture),
            cameraDevice.imageWidth.ToString(Culture),
            cameraDevice.imageHeight.ToString(Culture),
            F(cameraDevice.frameRateHz),
            F(cameraDevice.measuredFrameRateHz),

            F(cameraDevice.cameraPositionWorld.x),
            F(cameraDevice.cameraPositionWorld.y),
            F(cameraDevice.cameraPositionWorld.z),

            F(cameraDevice.cameraEulerWorldDeg.x),
            F(cameraDevice.cameraEulerWorldDeg.y),
            F(cameraDevice.cameraEulerWorldDeg.z),

            F(cameraDevice.cameraForwardWorld.x),
            F(cameraDevice.cameraForwardWorld.y),
            F(cameraDevice.cameraForwardWorld.z),

            F(cameraDevice.horizontalFovDeg),
            F(cameraDevice.nearClipM),
            F(cameraDevice.farClipM)
        );

        writer.WriteLine(line);
        linesWritten++;

        if (flushEveryLine)
        {
            writer.Flush();
        }
    }

    private string F(float value)
    {
        return value.ToString("G9", Culture);
    }

    private void CloseLog()
    {
        if (writer == null)
        {
            return;
        }

        writer.Flush();
        writer.Close();
        writer.Dispose();
        writer = null;

        Debug.Log("[MIMISKDroneRgbCameraLogger] Closed log: " + currentLogPath);
    }
}
