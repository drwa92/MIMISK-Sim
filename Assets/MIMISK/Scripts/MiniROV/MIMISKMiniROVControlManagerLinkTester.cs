using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKMiniROVControlManagerLinkTester : MonoBehaviour
{
    public ControlManager controlManager;
    public Rigidbody rb;

    [Header("Test Command")]
    public short testLeft = 160;
    public short testRight = 160;
    public short testDc1;
    public short testDc2;

    [Header("Runtime")]
    public bool testEnabled;
    public float testDurationS = 2.0f;
    public float testTimerS;
    public int injectedFrames;
    public string lastEvent = "idle";

    private void Awake()
    {
        AutoFindReferences();
    }

    private void FixedUpdate()
    {
        if (!testEnabled)
        {
            return;
        }

        AutoFindReferences();

        testTimerS += Time.fixedDeltaTime;

        if (controlManager != null)
        {
            controlManager.InjectMotorFrame(testLeft, testRight, testDc1, testDc2);
            injectedFrames++;
            lastEvent = "injecting_test_frame";
        }

        if (testTimerS >= testDurationS)
        {
            StopTest();
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (controlManager == null)
        {
            controlManager = GetComponent<ControlManager>();
        }

        if (rb == null)
        {
            rb = GetComponent<Rigidbody>();
        }

        if (controlManager != null)
        {
            controlManager.enabled = true;
            controlManager.autoOpenOnStart = false;

            if (rb != null)
            {
                controlManager.rb = rb;
            }

            if (controlManager.leftThruster == null)
            {
                controlManager.leftThruster = FindDeepChild(transform, "propulseur_gauche");
            }

            if (controlManager.rightThruster == null)
            {
                controlManager.rightThruster = FindDeepChild(transform, "propulseur_droite");
            }

            controlManager.useThrusterTransformForward = false;
            controlManager.thrusterMaxForce = Mathf.Max(4.0f, controlManager.thrusterMaxForce);
            controlManager.thrusterDeadzonePwm = Mathf.Min(8, controlManager.thrusterDeadzonePwm);
        }
    }

    [ContextMenu("Start Forward Test")]
    public void StartForwardTest()
    {
        AutoFindReferences();

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.WakeUp();
        }

        testEnabled = true;
        testTimerS = 0.0f;
        injectedFrames = 0;
        lastEvent = "forward_test_started";
    }

    [ContextMenu("Stop Test")]
    public void StopTest()
    {
        testEnabled = false;

        if (controlManager != null)
        {
            controlManager.InjectMotorFrame(0, 0, 0, 0);
        }

        lastEvent = "test_stopped";
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
            Transform found = FindDeepChild(root.GetChild(i), childName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
