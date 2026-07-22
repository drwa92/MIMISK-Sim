using UnityEngine;

[RequireComponent(typeof(Camera))]
public class MIMISKDroneRgbCameraDevice : MonoBehaviour
{
    public enum CameraModel
    {
        RaspberryPiCameraModule3,
        RaspberryPiCameraModule3Wide,
        GenericGlobalShutter
    }

    [Header("Device")]
    public CameraModel cameraModel = CameraModel.RaspberryPiCameraModule3Wide;
    public string deviceName = "Raspberry Pi Camera Module 3 Wide";
    public Camera unityCamera;

    [Header("Sensor Geometry")]
    public int imageWidth = 1280;
    public int imageHeight = 720;
    public float frameRateHz = 30.0f;
    public float horizontalFovDeg = 102.0f;

    [Header("Camera Rendering")]
    public bool renderOverlayInGameView = true;
    public Rect overlayViewport = new Rect(0.70f, 0.70f, 0.28f, 0.28f);
    public int cameraDepth = 80;
    public Color backgroundColor = Color.black;
    public bool useClearDepthOnly = false;

    [Header("Clipping")]
    public float nearClipM = 0.05f;
    public float farClipM = 500.0f;

    [Header("Sensor Effects")]
    public bool enableFrameRateLimit = true;
    public bool enableRollingShutterApproximation = false;
    public float rollingShutterReadoutMs = 12.0f;
    public bool enableExposureNoise = false;
    [Range(0.0f, 0.2f)] public float exposureNoiseStd = 0.02f;

    [Header("Outputs")]
    public bool hasNewFrame;
    public int frameIndex;
    public float lastFrameTime;
    public float measuredFrameRateHz;
    public Vector3 cameraPositionWorld;
    public Vector3 cameraEulerWorldDeg;
    public Vector3 cameraForwardWorld;

    [Header("Debug")]
    public float framePeriod;
    public float frameTimer;
    public int droppedFrames;

    private float previousFrameTime = -1.0f;

    private void Reset()
    {
        AutoFindCamera();
        ConfigureModelDefaults();
        ApplyCameraSettings();
    }

    private void Awake()
    {
        AutoFindCamera();
        ConfigureModelDefaults();
        ApplyCameraSettings();
    }

    private void OnValidate()
    {
        AutoFindCamera();
        ConfigureModelDefaults();
        ApplyCameraSettings();
    }

    private void Update()
    {
        hasNewFrame = false;

        UpdateCameraPoseOutputs();

        if (!enableFrameRateLimit)
        {
            MarkNewFrame();
            return;
        }

        framePeriod = 1.0f / Mathf.Max(1.0f, frameRateHz);
        frameTimer += Time.deltaTime;

        if (frameTimer >= framePeriod)
        {
            while (frameTimer >= framePeriod)
            {
                frameTimer -= framePeriod;
                droppedFrames++;
            }

            droppedFrames = Mathf.Max(0, droppedFrames - 1);
            MarkNewFrame();
        }
    }

    [ContextMenu("Auto Find Camera")]
    public void AutoFindCamera()
    {
        if (unityCamera == null)
        {
            unityCamera = GetComponent<Camera>();
        }
    }

    [ContextMenu("Configure Model Defaults")]
    public void ConfigureModelDefaults()
    {
        switch (cameraModel)
        {
            case CameraModel.RaspberryPiCameraModule3:
                deviceName = "Raspberry Pi Camera Module 3";
                imageWidth = 1280;
                imageHeight = 720;
                frameRateHz = 30.0f;
                horizontalFovDeg = 66.0f;
                break;

            case CameraModel.RaspberryPiCameraModule3Wide:
                deviceName = "Raspberry Pi Camera Module 3 Wide";
                imageWidth = 1280;
                imageHeight = 720;
                frameRateHz = 30.0f;
                horizontalFovDeg = 102.0f;
                break;

            case CameraModel.GenericGlobalShutter:
                deviceName = "Generic Global Shutter RGB Camera";
                imageWidth = 1280;
                imageHeight = 720;
                frameRateHz = 60.0f;
                horizontalFovDeg = 90.0f;
                break;
        }
    }

    [ContextMenu("Apply Camera Settings")]
    public void ApplyCameraSettings()
    {
        if (unityCamera == null)
        {
            return;
        }

        unityCamera.enabled = renderOverlayInGameView;
        unityCamera.depth = cameraDepth;
        unityCamera.nearClipPlane = nearClipM;
        unityCamera.farClipPlane = farClipM;
        unityCamera.fieldOfView = HorizontalToVerticalFov(horizontalFovDeg, imageWidth, imageHeight);
        unityCamera.backgroundColor = backgroundColor;

        if (useClearDepthOnly)
        {
            unityCamera.clearFlags = CameraClearFlags.Depth;
        }
        else
        {
            unityCamera.clearFlags = CameraClearFlags.Skybox;
        }

        if (renderOverlayInGameView)
        {
            unityCamera.rect = overlayViewport;
            unityCamera.targetTexture = null;
        }
    }

    private float HorizontalToVerticalFov(float hFovDeg, int width, int height)
    {
        float aspect = Mathf.Max(0.001f, (float)width / Mathf.Max(1, height));
        float hRad = hFovDeg * Mathf.Deg2Rad;
        float vRad = 2.0f * Mathf.Atan(Mathf.Tan(hRad * 0.5f) / aspect);

        return vRad * Mathf.Rad2Deg;
    }

    private void MarkNewFrame()
    {
        frameIndex++;
        hasNewFrame = true;
        lastFrameTime = Time.time;

        if (previousFrameTime > 0.0f)
        {
            measuredFrameRateHz = 1.0f / Mathf.Max(0.0001f, lastFrameTime - previousFrameTime);
        }

        previousFrameTime = lastFrameTime;
    }

    private void UpdateCameraPoseOutputs()
    {
        cameraPositionWorld = transform.position;
        cameraEulerWorldDeg = transform.eulerAngles;
        cameraForwardWorld = transform.forward;
    }
}
