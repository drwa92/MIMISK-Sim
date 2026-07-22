using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKPhase1CommonInterfaceSetup
{
    [MenuItem("MIMISK/Common Interface/Phase 1 - Setup Mission Interface")]
    public static void SetupPhase1CommonInterface()
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

        MIMISKMissionProfileLoader loader =
            common.GetComponent<MIMISKMissionProfileLoader>();

        if (loader == null)
        {
            loader = common.AddComponent<MIMISKMissionProfileLoader>();
        }

        MIMISKMissionProfile profile =
            AssetDatabase.LoadAssetAtPath<MIMISKMissionProfile>(
                "Assets/MIMISK/Missions/Profiles/MIMISK_Phase1_AutonomousMissionProfile.asset"
            );

        if (profile == null)
        {
            profile =
                ScriptableObject.CreateInstance<MIMISKMissionProfile>();

            profile.missionId = "mimisk_phase1_autonomous_profile";
            profile.description =
                "Phase 1 mission profile. Loads drone and MiniROV trajectories but does not execute them yet.";

            profile.droneTrajectory =
                AssetDatabase.LoadAssetAtPath<TextAsset>(
                    "Assets/MIMISK/Missions/Trajectories/drone_demo_placeholder.csv"
                );

            profile.miniRovTrajectory =
                AssetDatabase.LoadAssetAtPath<TextAsset>(
                    "Assets/MIMISK/Missions/Trajectories/minirov_kelp_inspection_demo.csv"
                );

            profile.deployLengthM = 0.90f;
            profile.payoutSpeedMS = 0.22f;
            profile.recoverySpeedMS = 0.25f;
            profile.miniRovBackend = MIMISKMiniROVBackendMode.UnityNative;

            AssetDatabase.CreateAsset(
                profile,
                "Assets/MIMISK/Missions/Profiles/MIMISK_Phase1_AutonomousMissionProfile.asset"
            );
        }

        loader.profile = profile;
        loader.loadOnStart = true;
        loader.LoadProfile();

        EditorUtility.SetDirty(loader);
        EditorUtility.SetDirty(profile);

        GameObject drone =
            GameObject.Find("Drone");

        if (drone != null)
        {
            MIMISKDroneModuleAdapter droneAdapter =
                drone.GetComponent<MIMISKDroneModuleAdapter>();

            if (droneAdapter == null)
            {
                droneAdapter = drone.AddComponent<MIMISKDroneModuleAdapter>();
            }

            droneAdapter.bus = bus;
            droneAdapter.AutoFindReferences();
            droneAdapter.adapterEnabled = true;
            droneAdapter.publishHz = 20.0f;

            EditorUtility.SetDirty(droneAdapter);

            MIMISKTetherModuleAdapter tetherAdapter =
                drone.GetComponent<MIMISKTetherModuleAdapter>();

            if (tetherAdapter == null)
            {
                tetherAdapter = drone.AddComponent<MIMISKTetherModuleAdapter>();
            }

            tetherAdapter.bus = bus;
            tetherAdapter.AutoFindReferences();
            tetherAdapter.adapterEnabled = true;
            tetherAdapter.publishHz = 20.0f;

            EditorUtility.SetDirty(tetherAdapter);
        }

        GameObject miniRov =
            GameObject.Find("MiniROV");

        if (miniRov != null)
        {
            MIMISKMiniROVModuleAdapter rovAdapter =
                miniRov.GetComponent<MIMISKMiniROVModuleAdapter>();

            if (rovAdapter == null)
            {
                rovAdapter = miniRov.AddComponent<MIMISKMiniROVModuleAdapter>();
            }

            rovAdapter.bus = bus;
            rovAdapter.AutoFindReferences();
            rovAdapter.adapterEnabled = true;
            rovAdapter.publishHz = 20.0f;

            EditorUtility.SetDirty(rovAdapter);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log(
            "[MIMISK] Phase 1 common mission interface configured. " +
            "This only loads profiles and publishes module states. It does not change the working final demo behavior."
        );
    }
}
