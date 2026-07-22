using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKCommonInterfaceVerifySetup
{
    [MenuItem("MIMISK/Common Interface/Verify Existing Module Connections")]
    public static void VerifyExistingModuleConnections()
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
        probe.RefreshLiveState();
        probe.PublishAllStates();

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

        Selection.activeGameObject = common;

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Common interface verification setup complete.");
        Debug.Log("[MIMISK] Drone Mission found: " + probe.foundDroneMission);
        Debug.Log("[MIMISK] Drone FlightMode found: " + probe.foundDroneFlightMode);
        Debug.Log("[MIMISK] Tether Manager found: " + probe.foundTetherManager);
        Debug.Log("[MIMISK] MiniROV Module found: " + probe.foundMiniRovModule);
        Debug.Log("[MIMISK] Open MIMISK_CommonInterface in Inspector and watch the Probe/Dashboard while running P/U/I/R.");
    }
}
