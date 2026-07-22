using UnityEngine;
using UnityEngine.InputSystem;

public class MIMISKInputDeviceProbe : MonoBehaviour
{
    public bool printOnStart = true;
    public bool printEveryTwoSeconds = false;

    [Header("Runtime")]
    public int deviceCount;
    public int gamepadCount;
    public string deviceSummary = "";

    private float timer;

    private void Start()
    {
        if (printOnStart)
        {
            PrintDevices();
        }
    }

    private void Update()
    {
        deviceCount = InputSystem.devices.Count;
        gamepadCount = Gamepad.all.Count;

        if (!printEveryTwoSeconds)
        {
            return;
        }

        timer += Time.deltaTime;

        if (timer >= 2.0f)
        {
            timer = 0.0f;
            PrintDevices();
        }
    }

    [ContextMenu("Print Input Devices")]
    public void PrintDevices()
    {
        deviceSummary = "";

        foreach (InputDevice device in InputSystem.devices)
        {
            deviceSummary += device.displayName + " | layout=" + device.layout + " | path=" + device.path + "\\n";
        }

        Debug.Log("[MIMISKInputDeviceProbe]\\n" + deviceSummary);
    }
}
