using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKDroneCoreRotorVisualAnimator : MonoBehaviour
{
    [Header("References")]
    public MIMISKDroneCoreRotorController coreController;

    [Header("Rotor Visual Transforms: FL, FR, RL, RR")]
    public Transform rotorFL;
    public Transform rotorFR;
    public Transform rotorRL;
    public Transform rotorRR;

    [Header("Animation")]
    public bool animateRotors = true;

    [Tooltip("Local axis used by the visual propeller mesh. Try (0,1,0). If the spin axis is wrong, change to (1,0,0) or (0,0,1).")]
    public Vector3 localSpinAxis = Vector3.up;

    public float idleRpm = 900.0f;
    public float maxRpm = 6500.0f;
    public float rpmResponseHz = 14.0f;

    [Header("Debug")]
    public Vector4 visualRpmFL_FR_RL_RR;

    private Vector4 rpmState;

    private void Awake()
    {
        if (coreController == null)
        {
            coreController = GetComponent<MIMISKDroneCoreRotorController>();
        }

        if (rotorFL == null || rotorFR == null || rotorRL == null || rotorRR == null)
        {
            AutoFindPropellerTransforms();
        }
    }

    private void Update()
    {
        if (!animateRotors || coreController == null)
        {
            return;
        }

        float dt = Time.deltaTime;

        Vector4 thrust = coreController.motorThrustActualN;
        float maxThrust = Mathf.Max(0.001f, coreController.maxThrustPerRotorN);

        Vector4 targetRpm = new Vector4(
            RpmFromThrust(thrust.x, maxThrust),
            RpmFromThrust(thrust.y, maxThrust),
            RpmFromThrust(thrust.z, maxThrust),
            RpmFromThrust(thrust.w, maxThrust)
        );

        float alpha =
            1.0f - Mathf.Exp(-Mathf.Max(0.001f, rpmResponseHz) * dt);

        rpmState = Vector4.Lerp(rpmState, targetRpm, alpha);
        visualRpmFL_FR_RL_RR = rpmState;

        Vector4 spin = coreController.rotorSpinSigns;

        SpinRotor(rotorFL, rpmState.x, spin.x, dt);
        SpinRotor(rotorFR, rpmState.y, spin.y, dt);
        SpinRotor(rotorRL, rpmState.z, spin.z, dt);
        SpinRotor(rotorRR, rpmState.w, spin.w, dt);
    }

    private float RpmFromThrust(float thrustN, float maxThrustN)
    {
        float u = Mathf.Clamp01(thrustN / maxThrustN);
        return Mathf.Lerp(idleRpm, maxRpm, Mathf.Sqrt(u));
    }

    private void SpinRotor(Transform rotor, float rpm, float spinSign, float dt)
    {
        if (rotor == null)
        {
            return;
        }

        float degrees =
            spinSign * rpm * 360.0f * dt / 60.0f;

        rotor.Rotate(localSpinAxis.normalized, degrees, Space.Self);
    }

    [ContextMenu("Auto Find Propeller Transforms")]
    public void AutoFindPropellerTransforms()
    {
        Transform[] all = GetComponentsInChildren<Transform>(true);

        rotorFL = rotorFL != null ? rotorFL : FindFirst(all, "fl", "frontleft", "front_left", "front-left", "m4", "motor4", "rotor4");
        rotorFR = rotorFR != null ? rotorFR : FindFirst(all, "fr", "frontright", "front_right", "front-right", "m2", "motor2", "rotor2");
        rotorRL = rotorRL != null ? rotorRL : FindFirst(all, "rl", "rearleft", "rear_left", "rear-left", "backleft", "back_left", "m3", "motor3", "rotor3");
        rotorRR = rotorRR != null ? rotorRR : FindFirst(all, "rr", "rearright", "rear_right", "rear-right", "backright", "back_right", "m1", "motor1", "rotor1");
    }

    private Transform FindFirst(Transform[] all, params string[] keys)
    {
        for (int i = 0; i < all.Length; i++)
        {
            string n = Normalize(all[i].name);

            for (int k = 0; k < keys.Length; k++)
            {
                if (n.Contains(Normalize(keys[k])))
                {
                    return all[i];
                }
            }
        }

        return null;
    }

    private string Normalize(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return "";
        }

        return s.ToLowerInvariant()
            .Replace(" ", "")
            .Replace("_", "")
            .Replace("-", "");
    }
}
