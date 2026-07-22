using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKDroneCorePropellerAnimationBridge : MonoBehaviour
{
    [Header("References")]
    public MIMISKDroneCoreRotorController core;
    public MIMISKDroneCoreFlightModeManager modeManager;
    public MIMISKDronePropellerAnimator propellerAnimator;

    [Header("Exact Drone V1 Propeller Pivot Names")]
    public string rotorFLName = "Rotor_FL_spin_pivot_Unity_rotate_local_Z";
    public string rotorFRName = "Rotor_FR_spin_pivot_Unity_rotate_local_Z";
    public string rotorRLName = "Rotor_RL_spin_pivot_Unity_rotate_local_Z";
    public string rotorRRName = "Rotor_RR_spin_pivot_Unity_rotate_local_Z";

    [Header("Visual RPM")]
    public float idleRpm = 600.0f;
    public float minFlyingRpm = 2400.0f;
    public float maxFlyingRpm = 6800.0f;

    public float spinUpRate = 3500.0f;
    public float spinDownRate = 2500.0f;

    [Header("Debug")]
    public string currentVisualState = "unknown";
    public float averageMotorOutput;
    public float commandedFlyingRpm;
    public int boundRotorCount;

    public string boundFL;
    public string boundFR;
    public string boundRL;
    public string boundRR;

    private void Awake()
    {
        AutoFindReferences();
        BindDroneV1Propellers();
    }

    private void Update()
    {
        if (propellerAnimator == null)
        {
            return;
        }

        UpdatePropellerStateFromCore();
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (core == null)
        {
            core = GetComponent<MIMISKDroneCoreRotorController>();
        }

        if (modeManager == null)
        {
            modeManager = GetComponent<MIMISKDroneCoreFlightModeManager>();
        }

        if (propellerAnimator == null)
        {
            propellerAnimator = GetComponent<MIMISKDronePropellerAnimator>();
        }

        if (propellerAnimator == null)
        {
            propellerAnimator = gameObject.AddComponent<MIMISKDronePropellerAnimator>();
        }

        propellerAnimator.enabled = true;
        propellerAnimator.enableKeyboardDebug = false;
    }

    [ContextMenu("Bind Drone V1 Propeller Pivots")]
    public void BindDroneV1Propellers()
    {
        AutoFindReferences();

        string[] rotorNames =
        {
            rotorFLName,
            rotorFRName,
            rotorRLName,
            rotorRRName
        };

        bool[] clockwise =
        {
            true,   // FL
            false,  // FR
            false,  // RL
            true    // RR
        };

        propellerAnimator.rotors =
            new MIMISKDronePropellerAnimator.Rotor[rotorNames.Length];

        boundRotorCount = 0;

        for (int i = 0; i < rotorNames.Length; i++)
        {
            Transform rotor =
                FindDeepChild(transform, rotorNames[i]);

            propellerAnimator.rotors[i] =
                new MIMISKDronePropellerAnimator.Rotor();

            propellerAnimator.rotors[i].name = rotorNames[i];
            propellerAnimator.rotors[i].rotorTransform = rotor;
            propellerAnimator.rotors[i].localSpinAxis = Vector3.forward;
            propellerAnimator.rotors[i].clockwise = clockwise[i];

            if (rotor != null)
            {
                boundRotorCount++;
            }
            else
            {
                Debug.LogWarning("[MIMISK] Core propeller bridge could not find: " + rotorNames[i]);
            }
        }

        propellerAnimator.disarmedRpm = 0.0f;
        propellerAnimator.armedIdleRpm = idleRpm;
        propellerAnimator.flyingRpm = minFlyingRpm;
        propellerAnimator.waterSurfaceStableRpm = 0.0f;
        propellerAnimator.spinUpRate = spinUpRate;
        propellerAnimator.spinDownRate = spinDownRate;

        UpdateBoundNames();

        Debug.Log(
            "[MIMISK] Core propeller bridge bound " + boundRotorCount +
            "/4 rotors. FL=" + boundFL +
            ", FR=" + boundFR +
            ", RL=" + boundRL +
            ", RR=" + boundRR
        );
    }

    private void UpdatePropellerStateFromCore()
    {
        string mode =
            modeManager != null
                ? modeManager.flightMode.ToString()
                : "unknown";

        if (mode == "Disabled" ||
            mode == "Disarmed")
        {
            currentVisualState = "Disarmed";
            propellerAnimator.SetState(MIMISKDronePropellerAnimator.DroneRotorState.Disarmed);
            return;
        }

        if (mode == "SurfaceStable" ||
            mode == "SurfaceHold")
        {
            currentVisualState = "WaterSurfaceStable";
            propellerAnimator.SetState(MIMISKDronePropellerAnimator.DroneRotorState.WaterSurfaceStable);
            return;
        }

        if (mode == "TakeoffIdle" ||
            mode == "ArmedIdle")
        {
            currentVisualState = "ArmedIdle";
            propellerAnimator.armedIdleRpm = idleRpm;
            propellerAnimator.SetState(MIMISKDronePropellerAnimator.DroneRotorState.ArmedIdle);
            return;
        }

        currentVisualState = "Flying";

        averageMotorOutput = ComputeAverageMotorOutput();
        commandedFlyingRpm =
            Mathf.Lerp(
                minFlyingRpm,
                maxFlyingRpm,
                Mathf.Sqrt(Mathf.Clamp01(averageMotorOutput))
            );

        propellerAnimator.SetFlyingRpm(commandedFlyingRpm);
        propellerAnimator.SetState(MIMISKDronePropellerAnimator.DroneRotorState.Flying);
    }

    private float ComputeAverageMotorOutput()
    {
        if (core == null)
        {
            return 0.0f;
        }

        float maxThrust =
            Mathf.Max(0.001f, core.maxThrustPerRotorN);

        Vector4 thrust =
            core.motorThrustActualN;

        float output =
            (
                Mathf.Clamp01(thrust.x / maxThrust) +
                Mathf.Clamp01(thrust.y / maxThrust) +
                Mathf.Clamp01(thrust.z / maxThrust) +
                Mathf.Clamp01(thrust.w / maxThrust)
            ) * 0.25f;

        return output;
    }

    private Transform FindDeepChild(Transform parent, string name)
    {
        if (parent == null)
        {
            return null;
        }

        if (parent.name == name)
        {
            return parent;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform result =
                FindDeepChild(parent.GetChild(i), name);

            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private void UpdateBoundNames()
    {
        boundFL = GetRotorName(0);
        boundFR = GetRotorName(1);
        boundRL = GetRotorName(2);
        boundRR = GetRotorName(3);
    }

    private string GetRotorName(int index)
    {
        if (propellerAnimator == null ||
            propellerAnimator.rotors == null ||
            index < 0 ||
            index >= propellerAnimator.rotors.Length ||
            propellerAnimator.rotors[index] == null ||
            propellerAnimator.rotors[index].rotorTransform == null)
        {
            return "NOT_BOUND";
        }

        return GetPath(propellerAnimator.rotors[index].rotorTransform);
    }

    private string GetPath(Transform t)
    {
        if (t == null)
        {
            return "null";
        }

        string path = t.name;
        Transform p = t.parent;

        while (p != null && p != transform)
        {
            path = p.name + "/" + path;
            p = p.parent;
        }

        return path;
    }
}
