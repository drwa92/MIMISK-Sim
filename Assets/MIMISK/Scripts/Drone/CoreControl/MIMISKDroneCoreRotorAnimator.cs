using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKDroneCoreRotorAnimator : MonoBehaviour
{
    [System.Serializable]
    public class VisualRotor
    {
        public string name;
        public Transform rotorTransform;
        public Vector3 localSpinAxis = Vector3.forward;
        public float spinSign = 1.0f;

        [Range(0.0f, 1.0f)]
        public float targetOutput;

        [Range(0.0f, 1.0f)]
        public float currentOutput;

        public float currentRpm;
    }

    [Header("References")]
    public MIMISKDroneCoreRotorController core;
    public MIMISKDroneCoreFlightModeManager modeManager;

    [Tooltip("Optional. Used only as fallback reference source. Keep old MIMISKDroneRotorModel disabled.")]
    public MIMISKDroneRotorModel legacyRotorModel;

    [Header("Rotor Visuals in Core Order: FL, FR, RL, RR")]
    public VisualRotor rotorFL = new VisualRotor { name = "FL", spinSign = 1.0f, localSpinAxis = Vector3.forward };
    public VisualRotor rotorFR = new VisualRotor { name = "FR", spinSign = -1.0f, localSpinAxis = Vector3.forward };
    public VisualRotor rotorRL = new VisualRotor { name = "RL", spinSign = -1.0f, localSpinAxis = Vector3.forward };
    public VisualRotor rotorRR = new VisualRotor { name = "RR", spinSign = 1.0f, localSpinAxis = Vector3.forward };

    [Header("Exact FBX Pivot Names")]
    public string pivotNameFL = "Rotor_FL_spin_pivot_Unity_rotate_local_Z";
    public string pivotNameFR = "Rotor_FR_spin_pivot_Unity_rotate_local_Z";
    public string pivotNameRL = "Rotor_RL_spin_pivot_Unity_rotate_local_Z";
    public string pivotNameRR = "Rotor_RR_spin_pivot_Unity_rotate_local_Z";

    [Header("Animation")]
    public bool animate = true;

    [Tooltip("Idle visual output used in TakeoffIdle / ArmedIdle.")]
    [Range(0.0f, 0.25f)]
    public float idleOutput = 0.06f;

    public float idleRpm = 600.0f;
    public float maxRpm = 6800.0f;

    [Tooltip("How quickly visual rotor output follows target output.")]
    public float visualResponseRate = 10.0f;

    [Tooltip("How quickly propellers visually spin down after motor cut.")]
    public float spinDownRpmPerSecond = 4500.0f;

    [Header("Debug")]
    public string rotorAnimationState = "unknown";
    public Vector4 targetOutputFL_FR_RL_RR;
    public Vector4 outputFL_FR_RL_RR;
    public Vector4 rpmFL_FR_RL_RR;

    public string boundFL;
    public string boundFR;
    public string boundRL;
    public string boundRR;

    private void Awake()
    {
        AutoFindReferences();
        AutoBindRotorVisuals();
    }

    private void Update()
    {
        if (!animate || core == null)
        {
            return;
        }

        UpdateTargetOutputs();

        float dt = Time.deltaTime;

        AnimateOne(rotorFL, dt);
        AnimateOne(rotorFR, dt);
        AnimateOne(rotorRL, dt);
        AnimateOne(rotorRR, dt);

        targetOutputFL_FR_RL_RR = new Vector4(
            rotorFL.targetOutput,
            rotorFR.targetOutput,
            rotorRL.targetOutput,
            rotorRR.targetOutput
        );

        outputFL_FR_RL_RR = new Vector4(
            rotorFL.currentOutput,
            rotorFR.currentOutput,
            rotorRL.currentOutput,
            rotorRR.currentOutput
        );

        rpmFL_FR_RL_RR = new Vector4(
            rotorFL.currentRpm,
            rotorFR.currentRpm,
            rotorRL.currentRpm,
            rotorRR.currentRpm
        );
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

        if (legacyRotorModel == null)
        {
            MIMISKDroneRotorModel[] models =
                GetComponentsInChildren<MIMISKDroneRotorModel>(true);

            if (models != null && models.Length > 0)
            {
                legacyRotorModel = models[0];
            }
        }
    }

    [ContextMenu("Auto Bind Rotor Visuals")]
    public void AutoBindRotorVisuals()
    {
        AutoFindReferences();

        bool exactBound = BindFromExactFbxPivotNames();

        if (!exactBound)
        {
            bool legacyBound = BindFromLegacyRotorModel();

            if (!legacyBound)
            {
                AutoBindByNameAndPosition();
            }
        }

        UpdateBoundNames();

        Debug.Log(
            "[MIMISKCoreRotorAnimator] Rotor visual binding: " +
            "FL=" + boundFL + ", FR=" + boundFR +
            ", RL=" + boundRL + ", RR=" + boundRR
        );
    }

    private bool BindFromExactFbxPivotNames()
    {
        bool ok = true;

        ok &= BindExact(rotorFL, pivotNameFL, "FL", Vector3.forward, 1.0f);
        ok &= BindExact(rotorFR, pivotNameFR, "FR", Vector3.forward, -1.0f);
        ok &= BindExact(rotorRL, pivotNameRL, "RL", Vector3.forward, -1.0f);
        ok &= BindExact(rotorRR, pivotNameRR, "RR", Vector3.forward, 1.0f);

        return ok;
    }

    private bool BindExact(
        VisualRotor rotor,
        string exactName,
        string label,
        Vector3 axis,
        float spinSign)
    {
        Transform t = FindDeepChild(transform, exactName);

        if (t == null)
        {
            return false;
        }

        rotor.name = label;
        rotor.rotorTransform = t;
        rotor.localSpinAxis = axis;
        rotor.spinSign = spinSign;

        return true;
    }

    private bool BindFromLegacyRotorModel()
    {
        if (legacyRotorModel == null)
        {
            return false;
        }

        bool any = false;

        if (legacyRotorModel.motor4_FL != null &&
            legacyRotorModel.motor4_FL.rotorTransform != null)
        {
            CopyLegacyRotor(legacyRotorModel.motor4_FL, rotorFL, "FL", 1.0f);
            any = true;
        }

        if (legacyRotorModel.motor2_FR != null &&
            legacyRotorModel.motor2_FR.rotorTransform != null)
        {
            CopyLegacyRotor(legacyRotorModel.motor2_FR, rotorFR, "FR", -1.0f);
            any = true;
        }

        if (legacyRotorModel.motor3_RL != null &&
            legacyRotorModel.motor3_RL.rotorTransform != null)
        {
            CopyLegacyRotor(legacyRotorModel.motor3_RL, rotorRL, "RL", -1.0f);
            any = true;
        }

        if (legacyRotorModel.motor1_RR != null &&
            legacyRotorModel.motor1_RR.rotorTransform != null)
        {
            CopyLegacyRotor(legacyRotorModel.motor1_RR, rotorRR, "RR", 1.0f);
            any = true;
        }

        return any;
    }

    private void CopyLegacyRotor(
        MIMISKDroneRotorModel.Rotor legacy,
        VisualRotor visual,
        string label,
        float fallbackSpinSign)
    {
        visual.name = label;
        visual.rotorTransform = legacy.rotorTransform;

        visual.localSpinAxis =
            legacy.localSpinAxis.sqrMagnitude > 0.0001f
                ? legacy.localSpinAxis
                : Vector3.forward;

        visual.spinSign =
            Mathf.Abs(legacy.yawTorqueSign) > 0.0001f
                ? Mathf.Sign(legacy.yawTorqueSign)
                : fallbackSpinSign;
    }

    private void AutoBindByNameAndPosition()
    {
        Transform[] all = GetComponentsInChildren<Transform>(true);
        List<Transform> candidates = new List<Transform>();

        for (int i = 0; i < all.Length; i++)
        {
            string n = Normalize(all[i].name);

            if (n.Contains("spinpivot") ||
                n.Contains("propeller") ||
                n.Contains("prop") ||
                n.Contains("blade") ||
                n.Contains("rotorvisual") ||
                n.Contains("rotormesh") ||
                n.Contains("helice") ||
                n.Contains("fan"))
            {
                candidates.Add(all[i]);
            }
        }

        HashSet<Transform> used = new HashSet<Transform>();

        if (rotorFL.rotorTransform == null)
        {
            rotorFL.rotorTransform = BestCandidate(candidates, used, -1.0f, -1.0f);
        }

        if (rotorFR.rotorTransform == null)
        {
            rotorFR.rotorTransform = BestCandidate(candidates, used, 1.0f, -1.0f);
        }

        if (rotorRL.rotorTransform == null)
        {
            rotorRL.rotorTransform = BestCandidate(candidates, used, -1.0f, 1.0f);
        }

        if (rotorRR.rotorTransform == null)
        {
            rotorRR.rotorTransform = BestCandidate(candidates, used, 1.0f, 1.0f);
        }

        rotorFL.localSpinAxis = Vector3.forward;
        rotorFR.localSpinAxis = Vector3.forward;
        rotorRL.localSpinAxis = Vector3.forward;
        rotorRR.localSpinAxis = Vector3.forward;
    }

    private Transform BestCandidate(
        List<Transform> candidates,
        HashSet<Transform> used,
        float signX,
        float signZ)
    {
        Transform best = null;
        float bestScore = float.PositiveInfinity;

        for (int i = 0; i < candidates.Count; i++)
        {
            Transform candidate = candidates[i];

            if (candidate == null || used.Contains(candidate))
            {
                continue;
            }

            Vector3 local =
                transform.InverseTransformPoint(candidate.position);

            float sx = signX < 0.0f ? -local.x : local.x;
            float sz = signZ < 0.0f ? -local.z : local.z;

            float penalty = 0.0f;

            if (sx < 0.0f) penalty += 100.0f;
            if (sz < 0.0f) penalty += 100.0f;

            float armX =
                core != null ? Mathf.Max(0.01f, core.armX_M) : 0.58f;

            float armZ =
                core != null ? Mathf.Max(0.01f, core.armZ_M) : 0.50f;

            float score =
                penalty +
                Mathf.Abs(Mathf.Abs(local.x) - armX) +
                Mathf.Abs(Mathf.Abs(local.z) - armZ);

            if (score < bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        if (best != null)
        {
            used.Add(best);
        }

        return best;
    }

    private void UpdateTargetOutputs()
    {
        string mode =
            modeManager != null
                ? modeManager.flightMode.ToString()
                : "";

        if (mode == "Disabled" ||
            mode == "Disarmed" ||
            mode == "SurfaceStable" ||
            mode == "SurfaceHold")
        {
            rotorAnimationState = "stopped";

            rotorFL.targetOutput = 0.0f;
            rotorFR.targetOutput = 0.0f;
            rotorRL.targetOutput = 0.0f;
            rotorRR.targetOutput = 0.0f;

            return;
        }

        if (mode == "TakeoffIdle" ||
            mode == "ArmedIdle")
        {
            rotorAnimationState = "idle";

            rotorFL.targetOutput = idleOutput;
            rotorFR.targetOutput = idleOutput;
            rotorRL.targetOutput = idleOutput;
            rotorRR.targetOutput = idleOutput;

            return;
        }

        rotorAnimationState = "thrust_based";

        float maxThrust =
            core != null
                ? Mathf.Max(0.001f, core.maxThrustPerRotorN)
                : 18.0f;

        Vector4 thrust =
            core != null
                ? core.motorThrustActualN
                : Vector4.zero;

        rotorFL.targetOutput = Mathf.Clamp01(thrust.x / maxThrust);
        rotorFR.targetOutput = Mathf.Clamp01(thrust.y / maxThrust);
        rotorRL.targetOutput = Mathf.Clamp01(thrust.z / maxThrust);
        rotorRR.targetOutput = Mathf.Clamp01(thrust.w / maxThrust);
    }

    private void AnimateOne(VisualRotor rotor, float dt)
    {
        if (rotor == null || rotor.rotorTransform == null)
        {
            return;
        }

        rotor.currentOutput =
            Mathf.MoveTowards(
                rotor.currentOutput,
                rotor.targetOutput,
                Mathf.Max(0.001f, visualResponseRate) * dt
            );

        float targetRpm;

        if (rotor.currentOutput <= 0.001f &&
            rotor.targetOutput <= 0.001f)
        {
            targetRpm = 0.0f;
        }
        else
        {
            // Square-root mapping makes hover look realistically fast.
            targetRpm =
                Mathf.Lerp(
                    idleRpm,
                    maxRpm,
                    Mathf.Sqrt(Mathf.Clamp01(rotor.currentOutput))
                );
        }

        if (targetRpm <= 0.001f)
        {
            rotor.currentRpm =
                Mathf.MoveTowards(
                    rotor.currentRpm,
                    0.0f,
                    spinDownRpmPerSecond * dt
                );
        }
        else
        {
            rotor.currentRpm = targetRpm;
        }

        Vector3 axis =
            rotor.localSpinAxis.sqrMagnitude > 0.0001f
                ? rotor.localSpinAxis.normalized
                : Vector3.forward;

        float direction =
            rotor.spinSign >= 0.0f ? 1.0f : -1.0f;

        float degPerSec =
            rotor.currentRpm * 360.0f / 60.0f;

        rotor.rotorTransform.Rotate(
            axis,
            degPerSec * direction * dt,
            Space.Self
        );
    }

    private Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null)
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found =
                FindDeepChild(root.GetChild(i), childName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private void UpdateBoundNames()
    {
        boundFL =
            rotorFL.rotorTransform != null
                ? GetPath(rotorFL.rotorTransform)
                : "NOT_BOUND";

        boundFR =
            rotorFR.rotorTransform != null
                ? GetPath(rotorFR.rotorTransform)
                : "NOT_BOUND";

        boundRL =
            rotorRL.rotorTransform != null
                ? GetPath(rotorRL.rotorTransform)
                : "NOT_BOUND";

        boundRR =
            rotorRR.rotorTransform != null
                ? GetPath(rotorRR.rotorTransform)
                : "NOT_BOUND";
    }

    private string GetPath(Transform t)
    {
        if (t == null)
        {
            return "null";
        }

        string path = t.name;
        Transform parent = t.parent;

        while (parent != null && parent != transform)
        {
            path = parent.name + "/" + path;
            parent = parent.parent;
        }

        return path;
    }

    private string Normalize(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        return value.ToLowerInvariant()
            .Replace(" ", "")
            .Replace("_", "")
            .Replace("-", "")
            .Replace(".", "")
            .Replace("é", "e");
    }
}
