using UnityEngine;

[RequireComponent(typeof(MIMISKDroneModelController))]
public class MIMISKDroneCommandStepTester : MonoBehaviour
{
    [Header("References")]
    public MIMISKDroneModelController controller;

    [Header("Enable")]
    public bool autoRunStepTest = false;

    [Header("Timing")]
    public float armAtSeconds = 0.5f;
    public float takeoffAtSeconds = 1.0f;
    public float manualAtSeconds = 5.0f;
    public float commandStartSeconds = 6.0f;
    public float commandDurationSeconds = 2.0f;
    public float landAtSeconds = 11.0f;

    [Header("Command")]
    [Range(-1f, 1f)] public float forwardCommand = 0.5f;
    [Range(-1f, 1f)] public float rightCommand = 0.0f;
    [Range(-1f, 1f)] public float yawCommand = 0.0f;
    [Range(-1f, 1f)] public float altitudeCommand = 0.0f;

    [Header("Debug")]
    public float elapsed;
    public bool commandActive;

    private bool didArm;
    private bool didTakeoff;
    private bool didManual;
    private bool didLand;

    private void Awake()
    {
        if (controller == null)
        {
            controller = GetComponent<MIMISKDroneModelController>();
        }
    }

    private void OnEnable()
    {
        elapsed = 0.0f;
        didArm = false;
        didTakeoff = false;
        didManual = false;
        didLand = false;
        commandActive = false;
    }

    private void Update()
    {
        if (!autoRunStepTest || controller == null)
        {
            return;
        }

        elapsed += Time.deltaTime;

        if (!didArm && elapsed >= armAtSeconds)
        {
            controller.SetMode(MIMISKDroneModelController.DroneMode.ArmedIdle);
            didArm = true;
        }

        if (!didTakeoff && elapsed >= takeoffAtSeconds)
        {
            controller.SetMode(MIMISKDroneModelController.DroneMode.Takeoff);
            didTakeoff = true;
        }

        if (!didManual && elapsed >= manualAtSeconds)
        {
            controller.SetMode(MIMISKDroneModelController.DroneMode.ManualAttitude);
            didManual = true;
        }

        bool shouldCommand =
            elapsed >= commandStartSeconds &&
            elapsed <= commandStartSeconds + commandDurationSeconds;

        if (shouldCommand)
        {
            commandActive = true;
            controller.SetExternalCommand(
                forwardCommand,
                rightCommand,
                yawCommand,
                altitudeCommand
            );
        }
        else
        {
            commandActive = false;
            controller.ClearExternalCommand();
        }

        if (!didLand && elapsed >= landAtSeconds)
        {
            controller.SetMode(MIMISKDroneModelController.DroneMode.LandingOnWater);
            didLand = true;
        }
    }
}
