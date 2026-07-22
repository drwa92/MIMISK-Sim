using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKPhase2CommandRoutingSetup
{
    [MenuItem("MIMISK/Common Interface/Phase 2A - Setup Command Routing Test")]
    public static void SetupPhase2ACommandRoutingTest()
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

        MIMISKCommandBridgeToExistingModules bridge =
            common.GetComponent<MIMISKCommandBridgeToExistingModules>();

        if (bridge == null)
        {
            bridge = common.AddComponent<MIMISKCommandBridgeToExistingModules>();
        }

        bridge.bus = bus;
        bridge.bridgeEnabled = true;
        bridge.AutoFindReferences();

        EditorUtility.SetDirty(bridge);

        // Disable older common command adapters if they exist, to avoid double execution.
        GameObject drone =
            GameObject.Find("Drone");

        if (drone != null)
        {
            DisableByClassName(drone, "MIMISKFinalPlannerAdapter");
        }

        // Keep old direct P/U/I/R owners enabled.
        // Phase 2A uses 1/2/3/4/5/F8 to avoid conflicts.

        Selection.activeGameObject = common;

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Phase 2A command routing test configured.");
        Debug.Log("[MIMISK] Test keys: 1=StartMission, 2=DeployTether, 3=EnableMiniROVControl, 4=RecoverMiniROV, 5=HoldTether, F8=Reset.");
        Debug.Log("[MIMISK] Existing P/U/I/R controls remain unchanged.");
    }

    private static void DisableByClassName(GameObject root, string className)
    {
        if (root == null)
        {
            return;
        }

        Type t =
            FindTypeByName(className);

        if (t == null)
        {
            return;
        }

        Component[] components =
            root.GetComponentsInChildren(t, true);

        if (components == null)
        {
            return;
        }

        for (int i = 0; i < components.Length; i++)
        {
            Behaviour b =
                components[i] as Behaviour;

            if (b != null)
            {
                b.enabled = false;
                EditorUtility.SetDirty(b);
            }
        }
    }

    private static Type FindTypeByName(string className)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            Type t =
                assembly.GetType(className);

            if (t != null)
            {
                return t;
            }

            Type[] types;

            try
            {
                types = assembly.GetTypes();
            }
            catch
            {
                continue;
            }

            for (int i = 0; i < types.Length; i++)
            {
                if (types[i].Name == className)
                {
                    return types[i];
                }
            }
        }

        return null;
    }
}
