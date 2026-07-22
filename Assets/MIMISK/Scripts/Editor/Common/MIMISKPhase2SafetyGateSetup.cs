using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKPhase2SafetyGateSetup
{
    [MenuItem("MIMISK/Common Interface/Phase 2A.5 - Setup Safety Gate and Manifest")]
    public static void SetupSafetyGateAndManifest()
    {
        GameObject common =
            GameObject.Find("MIMISK_CommonInterface");

        if (common == null)
        {
            common = new GameObject("MIMISK_CommonInterface");
        }

        MIMISKCommonBus bus =
            common.GetComponent<MIMISKCommonBus>();

        if (bus == null)
        {
            bus = common.AddComponent<MIMISKCommonBus>();
        }

        bus.busEnabled = true;
        bus.logCommands = true;
        bus.logStateTransitions = false;

        EditorUtility.SetDirty(bus);

        MIMISKCommonInterfaceReadOnlyProbe probe =
            common.GetComponent<MIMISKCommonInterfaceReadOnlyProbe>();

        if (probe == null)
        {
            probe = common.AddComponent<MIMISKCommonInterfaceReadOnlyProbe>();
        }

        probe.bus = bus;
        probe.probeEnabled = true;
        probe.publishToBus = true;
        probe.publishHz = 10.0f;
        probe.AutoFindReferences();

        EditorUtility.SetDirty(probe);

        MIMISKCommonInterfaceDashboard dashboard =
            common.GetComponent<MIMISKCommonInterfaceDashboard>();

        if (dashboard == null)
        {
            dashboard = common.AddComponent<MIMISKCommonInterfaceDashboard>();
        }

        dashboard.bus = bus;
        dashboard.dashboardEnabled = true;
        dashboard.AutoFindReferences();

        EditorUtility.SetDirty(dashboard);

        MIMISKCommonCommandFrontend frontend =
            common.GetComponent<MIMISKCommonCommandFrontend>();

        if (frontend == null)
        {
            frontend = common.AddComponent<MIMISKCommonCommandFrontend>();
        }

        frontend.bus = bus;
        frontend.frontendEnabled = true;
        frontend.usePhase2TestKeys = true;

        EditorUtility.SetDirty(frontend);

        MIMISKCommandSafetyGate gate =
            common.GetComponent<MIMISKCommandSafetyGate>();

        if (gate == null)
        {
            gate = common.AddComponent<MIMISKCommandSafetyGate>();
        }

        gate.bus = bus;
        gate.gateEnabled = true;
        gate.authorizedSourceName = "MIMISKCommandSafetyGate";
        gate.allowDeploymentFromPhysicalSurfaceState = true;
        gate.blockDeploymentWhileDroneMissionActive = true;
        gate.requireExternalMiniRovStackForControl = true;
        gate.AutoFindReferences();
        gate.RefreshReadiness();

        EditorUtility.SetDirty(gate);

        MIMISKCommandBridgeToExistingModules bridge =
            common.GetComponent<MIMISKCommandBridgeToExistingModules>();

        if (bridge == null)
        {
            bridge = common.AddComponent<MIMISKCommandBridgeToExistingModules>();
        }

        bridge.bus = bus;
        bridge.bridgeEnabled = true;
        bridge.requireSafetyGate = true;
        bridge.authorizedCommandSource = "MIMISKCommandSafetyGate";
        bridge.AutoFindReferences();

        EditorUtility.SetDirty(bridge);

        MIMISKRuntimeSceneManifest manifest =
            common.GetComponent<MIMISKRuntimeSceneManifest>();

        if (manifest == null)
        {
            manifest = common.AddComponent<MIMISKRuntimeSceneManifest>();
        }

        manifest.manifestEnabled = true;
        manifest.validateOnStart = true;
        manifest.logReport = true;
        manifest.ValidateScene();

        EditorUtility.SetDirty(manifest);

        Selection.activeGameObject = common;

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Phase 2A.5 Safety Gate and Runtime Manifest configured.");
        Debug.Log("[MIMISK] Test keys remain: 1=start, 2=deploy, 3=ROV control, 4=recover, 5=hold, F8=reset.");
        Debug.Log("[MIMISK] Commands now pass through safety gate before reaching existing modules.");
    }
}
