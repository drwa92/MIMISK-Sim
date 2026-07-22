using System;
using System.Collections.Generic;
using Google.Protobuf;
using UnityEngine;

using MimiskCameraFrame = MIMISK.Grpc.CameraFrame;
using MimiskHeader = MIMISK.Grpc.Header;
using MimiskTelemetryAck = MIMISK.Grpc.TelemetryAck;

[DefaultExecutionOrder(2550)]
[DisallowMultipleComponent]
public class MIMISKGrpcCameraBridge : MonoBehaviour
{
    [Header("Connection")]
    public MIMISKGrpcConnection connection;
    public bool cameraStreamingEnabled = true;

    [Header("Cameras")]
    public bool streamDroneCamera = true;
    public bool streamMiniRovFrontCamera = true;

    public string droneCameraName = "DroneCamera";
    public string miniRovFrontCameraName = "FrontCamera";

    public Camera droneCamera;
    public Camera miniRovFrontCamera;

    [Header("Image Settings")]
    [Range(160, 1920)]
    public int imageWidth = 640;

    [Range(120, 1080)]
    public int imageHeight = 360;

    [Range(1, 100)]
    public int jpegQuality = 70;

    [Range(1.0f, 30.0f)]
    public float publishHz = 5.0f;

    [Header("Runtime")]
    public bool autoFindReferences = true;
    public bool sendInProgress;
    public ulong sequence;
    public int framesCaptured;
    public int framesSent;
    public int framesFailed;
    public string lastStatus = "idle";

    private float timerS;
    private Texture2D captureTexture;

    private void Awake()
    {
        if (autoFindReferences)
        {
            AutoFindReferences();
        }
    }

    private void Start()
    {
        if (connection == null)
        {
            connection =
                GetComponent<MIMISKGrpcConnection>();
        }

        if (connection == null)
        {
            connection =
                FindFirstObjectByType<MIMISKGrpcConnection>();
        }

        if (autoFindReferences)
        {
            AutoFindReferences();
        }
    }

    private void Update()
    {
        if (!cameraStreamingEnabled)
        {
            return;
        }

        if (connection == null ||
            connection.Client == null ||
            !connection.isConnected)
        {
            return;
        }

        timerS += Time.deltaTime;

        float period =
            1.0f / Mathf.Max(1.0f, publishHz);

        if (timerS >= period)
        {
            timerS -= period;
            SendCameraFrames();
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (connection == null)
        {
            connection =
                GetComponent<MIMISKGrpcConnection>();

            if (connection == null)
            {
                connection =
                    FindFirstObjectByType<MIMISKGrpcConnection>();
            }
        }

        if (droneCamera == null)
        {
            droneCamera =
                FindCameraByName(droneCameraName);
        }

        if (miniRovFrontCamera == null)
        {
            miniRovFrontCamera =
                FindCameraByName(miniRovFrontCameraName);
        }
    }

    [ContextMenu("Send One Camera Frame Set")]
    public async void SendCameraFrames()
    {
        if (sendInProgress)
        {
            return;
        }

        if (connection == null ||
            connection.Client == null ||
            !connection.isConnected)
        {
            lastStatus =
                "not_connected";

            return;
        }

        sendInProgress =
            true;

        List<MimiskCameraFrame> frames =
            new List<MimiskCameraFrame>();

        try
        {
            if (autoFindReferences &&
                ((streamDroneCamera && droneCamera == null) ||
                 (streamMiniRovFrontCamera && miniRovFrontCamera == null)))
            {
                AutoFindReferences();
            }

            if (streamDroneCamera && droneCamera != null)
            {
                MimiskCameraFrame frame =
                    CaptureCameraFrame(
                        droneCamera,
                        "DroneCamera",
                        "drone_camera_optical"
                    );

                if (frame != null)
                {
                    frames.Add(frame);
                }
            }

            if (streamMiniRovFrontCamera && miniRovFrontCamera != null)
            {
                MimiskCameraFrame frame =
                    CaptureCameraFrame(
                        miniRovFrontCamera,
                        "FrontCamera",
                        "minirov_front_camera_optical"
                    );

                if (frame != null)
                {
                    frames.Add(frame);
                }
            }
        }
        catch (Exception ex)
        {
            framesFailed++;
            lastStatus =
                "capture_failed_" + ex.GetType().Name + ": " + ex.Message;

            Debug.LogWarning(
                "[MIMISK gRPC] Camera capture failed: " +
                lastStatus
            );

            sendInProgress =
                false;

            return;
        }

        try
        {
            for (int i = 0; i < frames.Count; i++)
            {
                MimiskTelemetryAck ack =
                    await connection.Client.SendCameraFrameAsync(frames[i]);

                if (ack.Accepted)
                {
                    framesSent++;
                    lastStatus =
                        ack.Message;
                }
                else
                {
                    framesFailed++;
                    lastStatus =
                        "camera_rejected_" + ack.Message;
                }
            }
        }
        catch (Exception ex)
        {
            framesFailed++;

            if (connection != null)
            {
                connection.isConnected =
                    false;
            }

            lastStatus =
                "send_failed_" + ex.GetType().Name + ": " + ex.Message;

            Debug.LogWarning(
                "[MIMISK gRPC] Camera send failed: " +
                lastStatus
            );
        }
        finally
        {
            sendInProgress =
                false;
        }
    }

    private MimiskCameraFrame CaptureCameraFrame(
        Camera camera,
        string cameraNameForRos,
        string frameId)
    {
        if (camera == null)
        {
            return null;
        }

        int w =
            Mathf.Max(16, imageWidth);

        int h =
            Mathf.Max(16, imageHeight);

        if (captureTexture == null ||
            captureTexture.width != w ||
            captureTexture.height != h)
        {
            if (captureTexture != null)
            {
                Destroy(captureTexture);
            }

            captureTexture =
                new Texture2D(
                    w,
                    h,
                    TextureFormat.RGB24,
                    false
                );
        }

        RenderTexture oldTarget =
            camera.targetTexture;

        RenderTexture oldActive =
            RenderTexture.active;

        RenderTexture rt =
            RenderTexture.GetTemporary(
                w,
                h,
                24,
                RenderTextureFormat.ARGB32
            );

        try
        {
            camera.targetTexture =
                rt;

            RenderTexture.active =
                rt;

            camera.Render();

            captureTexture.ReadPixels(
                new Rect(0, 0, w, h),
                0,
                0,
                false
            );

            captureTexture.Apply(false, false);

            byte[] jpg =
                captureTexture.EncodeToJPG(
                    Mathf.Clamp(jpegQuality, 1, 100)
                );

            framesCaptured++;

            double simTime =
                Time.time;

            return
                new MimiskCameraFrame
                {
                    Header =
                        new MimiskHeader
                        {
                            SimTimeSec = simTime,
                            Seq = sequence++,
                            FrameId = frameId
                        },
                    CameraName = cameraNameForRos,
                    FrameId = frameId,
                    Encoding = "jpeg",
                    Width = (uint)w,
                    Height = (uint)h,
                    Data = ByteString.CopyFrom(jpg),
                    FovYDeg = camera.fieldOfView
                };
        }
        finally
        {
            camera.targetTexture =
                oldTarget;

            RenderTexture.active =
                oldActive;

            RenderTexture.ReleaseTemporary(rt);
        }
    }

    private Camera FindCameraByName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return null;
        }

        Camera[] cameras =
            Resources.FindObjectsOfTypeAll<Camera>();

        for (int i = 0; i < cameras.Length; i++)
        {
            Camera c =
                cameras[i];

            if (c == null ||
                c.gameObject == null ||
                !c.gameObject.scene.IsValid())
            {
                continue;
            }

            if (c.gameObject.name == objectName)
            {
                return c;
            }
        }

        return null;
    }

    private void OnDestroy()
    {
        if (captureTexture != null)
        {
            Destroy(captureTexture);
            captureTexture = null;
        }
    }
}
