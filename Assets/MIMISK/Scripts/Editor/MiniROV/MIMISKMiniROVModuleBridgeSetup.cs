using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKMiniROVModuleBridgeSetup
{
    [MenuItem("MIMISK/MiniROV/Setup MiniROV Module For Tether Scene")]
    public static void SetupMiniROVModuleForTetherScene()
    {
        GameObject miniRov = GameObject.Find("MiniROV");

        if (miniRov == null)
        {
            Debug.LogError("[MIMISK] Could not find MiniROV.");
            return;
        }

        GameObject drone = GameObject.Find("Drone");

        Rigidbody rb = miniRov.GetComponent<Rigidbody>();

        if (rb == null)
        {
            rb = miniRov.AddComponent<Rigidbody>();
        }

        rb.mass = 0.60f;
        rb.isKinematic = true;
        rb.useGravity = false;

        // Leave damping values as in the working manual setup.
        rb.linearDamping = 4.0f;
        rb.angularDamping = 6.0f;

        EditorUtility.SetDirty(rb);

        MIMISKMiniROVModule module = miniRov.GetComponent<MIMISKMiniROVModule>();

        if (module == null)
        {
            module = miniRov.AddComponent<MIMISKMiniROVModule>();
        }

        module.miniRovRoot = miniRov.transform;
        module.miniRovRigidbody = rb;

        module.rovTetherAnchor =
            FindDeepChild(miniRov.transform, "ROV_TetherAnchor");

        if (module.rovTetherAnchor == null)
        {
            module.rovTetherAnchor =
                FindDeepChild(miniRov.transform, "MiniROV_TetherPoint");
        }

        if (module.rovTetherAnchor == null)
        {
            module.rovTetherAnchor =
                FindDeepChild(miniRov.transform, "TetherPoint");
        }

        module.miniRovColliders =
            miniRov.GetComponentsInChildren<Collider>(true);

        module.tetherManager =
            drone != null ? drone.GetComponent<MIMISKDroneCoreTetherManager>() : null;

        module.miniRovMassKg = 0.60f;
        module.requireExternalSerialBeforeControl = true;
        module.unityEsp32SerialDevice = "/dev/unity_esp32";

        // Final working configuration from your latest test:
        // gravity + SimpleROVBuoyancy.
        module.useGravityInControl = true;
        module.enableSimpleRovBuoyancyInFinalStack = true;
        module.keepSensorManagerDisabled = true;

        module.enableCollidersDuringControl = true;

        module.disableDroneDeploymentOwnersOnExternalControl = true;
        module.detachFromCableRootOnExternalControl = true;
        module.configureTetherAsVisualOnlyOnExternalControl = true;

        module.correctOrientationOnHandoff = true;
        module.freeSwimWorldEuler = Vector3.zero;
        module.preserveCurrentYaw = false;
        module.keepTetherAnchorLockedDuringOrientationFix = true;

        module.enableFinalStackStaged = true;
        module.serialWarmupSeconds = 0.15f;
        module.controlWarmupSeconds = 0.10f;

        module.keyboardTestEnabled = true;
        module.externalControlKey = UnityEngine.InputSystem.Key.I;

        module.AutoFindReferences();

        MIMISKMiniROVTetherVisualBridge visualBridge = null;

        if (drone != null)
        {
            visualBridge = drone.GetComponent<MIMISKMiniROVTetherVisualBridge>();

            if (visualBridge == null)
            {
                visualBridge = drone.AddComponent<MIMISKMiniROVTetherVisualBridge>();
            }

            visualBridge.miniRovRoot = miniRov.transform;
            visualBridge.rovTetherAnchor = module.rovTetherAnchor;
            visualBridge.tetherStart =
                FindDeepChild(drone.transform, "WinchFairlead_for_Unity_LineRenderer_Start");

            if (visualBridge.tetherStart == null)
            {
                visualBridge.tetherStart =
                    FindDeepChild(drone.transform, "TetherAnchor");
            }

            if (visualBridge.tetherStart == null)
            {
                visualBridge.tetherStart =
                    FindDeepChild(drone.transform, "WinchPoint");
            }

            visualBridge.hookVisual =
                FindDeepChild(drone.transform, "small_dark_open_deployment_hook_for_miniROV");

            visualBridge.lineWidthM = 0.018f;
            visualBridge.lineSegments = 16;
            visualBridge.slackSagM = 0.08f;
            visualBridge.disableOtherTetherLineRenderers = true;
            visualBridge.active = false;
            visualBridge.AutoFindReferences();

            EditorUtility.SetDirty(visualBridge);
        }

        module.tetherVisualBridge = visualBridge;

        DisableFinalMiniROVStack(miniRov);

        EditorUtility.SetDirty(module);

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log(
            "[MIMISK] MiniROV module configured for tether scene. " +
            "U deploys with tether. I corrects orientation, detaches MiniROV, enables final stack, and attaches yellow tether to ROV_TetherAnchor."
        );
    }

    private static void DisableFinalMiniROVStack(GameObject miniRov)
    {
        Behaviour[] behaviours =
            miniRov.GetComponentsInChildren<Behaviour>(true);

        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour b = behaviours[i];

            if (b == null)
            {
                continue;
            }

            string n = b.GetType().Name;

            bool managed =
                ContainsIgnoreCase(n, "UnityVirtualESP32") ||
                ContainsIgnoreCase(n, "ControlManager") ||
                ContainsIgnoreCase(n, "MIMISKWaterInteraction") ||
                ContainsIgnoreCase(n, "SensorManager") ||
                ContainsIgnoreCase(n, "SimpleROVBuoyancy");

            if (managed)
            {
                b.enabled = false;
                EditorUtility.SetDirty(b);
            }
        }
    }

    private static Transform FindDeepChild(Transform root, string childName)
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

    private static bool ContainsIgnoreCase(string text, string pattern)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        return text.IndexOf(
            pattern,
            System.StringComparison.OrdinalIgnoreCase
        ) >= 0;
    }
}
