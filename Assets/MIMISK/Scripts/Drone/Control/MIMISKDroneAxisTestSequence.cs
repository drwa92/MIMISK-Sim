using UnityEngine;

[RequireComponent(typeof(MIMISKDroneModelController))]
public class MIMISKDroneAxisTestSequence : MonoBehaviour
{
    [Header("References")]
    public MIMISKDroneModelController controller;

    [Header("Enable")]
    public bool autoRunAxisTest = false;

    [Header("Timing")]
    public float armAtSeconds = 0.5f;
    public float takeoffAtSeconds = 1.0f;
    public float manualAtSeconds = 5.0f;
    public float commandDurationSeconds = 1.5f;
    public float restDurationSeconds = 1.0f;
    public float landAfterSequenceSeconds = 1.5f;

    [Header("Command Amplitudes")]
    [Range(0f, 1f)] public float forwardAmplitude = 0.5f;
    [Range(0f, 1f)] public float lateralAmplitude = 0.5f;
    [Range(0f, 1f)] public float yawAmplitude = 0.4f;
    [Range(0f, 1f)] public float altitudeAmplitude = 0.5f;

    [Header("Debug")]
    public float elapsed;
    public string currentTestName = "none";
    public int currentStepIndex = -1;

    private bool didArm;
    private bool didTakeoff;
    private bool didManual;
    private bool didLand;

    private struct AxisStep
    {
        public string name;
        public Vector4 command;

        public AxisStep(string name, Vector4 command)
        {
            this.name = name;
            this.command = command;
        }
    }

    private AxisStep[] steps;

    private void Awake()
    {
        if (controller == null)
        {
            controller = GetComponent<MIMISKDroneModelController>();
        }

        BuildSteps();
    }

    private void OnEnable()
    {
        ResetSequence();
    }

    private void BuildSteps()
    {
        steps = new AxisStep[]
        {
            new AxisStep("forward",       new Vector4( forwardAmplitude, 0f, 0f, 0f)),
            new AxisStep("backward",      new Vector4(-forwardAmplitude, 0f, 0f, 0f)),
            new AxisStep("right",         new Vector4(0f,  lateralAmplitude, 0f, 0f)),
            new AxisStep("left",          new Vector4(0f, -lateralAmplitude, 0f, 0f)),
            new AxisStep("yaw_right",     new Vector4(0f, 0f,  yawAmplitude, 0f)),
            new AxisStep("yaw_left",      new Vector4(0f, 0f, -yawAmplitude, 0f)),
            new AxisStep("altitude_up",   new Vector4(0f, 0f, 0f,  altitudeAmplitude)),
            new AxisStep("altitude_down", new Vector4(0f, 0f, 0f, -altitudeAmplitude))
        };
    }

    private void ResetSequence()
    {
        elapsed = 0f;
        currentStepIndex = -1;
        currentTestName = "none";

        didArm = false;
        didTakeoff = false;
        didManual = false;
        didLand = false;
    }

    private void Update()
    {
        if (!autoRunAxisTest || controller == null)
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

        if (!didManual)
        {
            return;
        }

        float sequenceStart = manualAtSeconds + restDurationSeconds;
        float stepBlock = commandDurationSeconds + restDurationSeconds;
        float sequenceTime = elapsed - sequenceStart;

        if (sequenceTime < 0f)
        {
            controller.ClearExternalCommand();
            currentStepIndex = -1;
            currentTestName = "pre_sequence_rest";
            return;
        }

        int stepIndex = Mathf.FloorToInt(sequenceTime / stepBlock);

        if (stepIndex >= steps.Length)
        {
            controller.ClearExternalCommand();
            currentStepIndex = -1;
            currentTestName = "sequence_done";

            float landTime =
                sequenceStart + steps.Length * stepBlock + landAfterSequenceSeconds;

            if (!didLand && elapsed >= landTime)
            {
                controller.SetMode(MIMISKDroneModelController.DroneMode.LandingOnWater);
                didLand = true;
            }

            return;
        }

        float stepLocalTime = sequenceTime - stepIndex * stepBlock;

        currentStepIndex = stepIndex;
        currentTestName = steps[stepIndex].name;

        if (stepLocalTime <= commandDurationSeconds)
        {
            Vector4 c = steps[stepIndex].command;

            controller.SetExternalCommand(
                c.x,
                c.y,
                c.z,
                c.w
            );
        }
        else
        {
            controller.ClearExternalCommand();
            currentTestName = steps[stepIndex].name + "_rest";
        }
    }
}
