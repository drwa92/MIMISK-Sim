#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKValidationBatch
{
    private const string ConfigPath = "Assets/MIMISK/Validation/run_config.json";

    [MenuItem("MIMISK/Validation/Install Validation Harness In Active Scene")]
    public static void InstallHarnessMenu()
    {
        InstallHarnessInActiveScene(false);
    }


    [MenuItem("MIMISK/Validation/Install Inspector Test Runner In Active Scene")]
    public static void InstallInspectorRunnerMenu()
    {
        MIMISKValidationHarness harness = InstallHarnessInActiveScene(true);
        harness.loadConfigOnStart = false;
        harness.autoStart = false;
        harness.quitOnComplete = false;
        harness.config.quit_on_complete = false;
        harness.config.write_done_flag = false;
        Selection.activeObject = harness.gameObject;
        EditorUtility.SetDirty(harness);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("MIMISK validation inspector runner installed. Press Play, select MIMISK_ValidationHarness, then run tests from the Inspector.");
    }

    [MenuItem("MIMISK/Validation/Run From Config In Editor")]
    public static void RunFromConfigMenu()
    {
        RunFromConfigInternal(false);
    }

    public static void RunFromConfig()
    {
        RunFromConfigInternal(true);
    }

    public static void RunFullMission()
    {
        WriteSimpleConfig("full_mission", "mimisk_full_mission", "trial_001", false);
        RunFromConfigInternal(true);
    }

    public static void RunDroneTrajectory()
    {
        WriteSimpleConfig("drone_trajectory", "mimisk_drone_trajectory", "trial_001", false);
        RunFromConfigInternal(true);
    }

    public static void RunMiniRovNavigation()
    {
        WriteSimpleConfig("minirov_navigation", "mimisk_minirov_navigation", "trial_001", false);
        RunFromConfigInternal(true);
    }

    public static void RunTetherV8()
    {
        WriteSimpleConfig("tether_v8", "mimisk_tether_v8", "trial_001", false);
        RunFromConfigInternal(true);
    }

    public static void RunRosGrpc()
    {
        WriteSimpleConfig("ros_grpc", "mimisk_ros_grpc", "trial_001", true);
        RunFromConfigInternal(true);
    }

    private static void RunFromConfigInternal(bool batchMode)
    {
        MIMISKValidationHarness.RunConfig cfg = ReadConfig();
        if (!string.IsNullOrEmpty(cfg.scene))
        {
            EditorSceneManager.OpenScene(cfg.scene, OpenSceneMode.Single);
        }

        MIMISKValidationHarness harness = InstallHarnessInActiveScene(false);
        harness.loadConfigOnStart = true;
        harness.autoStart = true;
        harness.quitOnComplete = false;
        harness.config.quit_on_complete = false;
        harness.config.write_done_flag = batchMode;
        harness.config.done_flag_path = string.IsNullOrEmpty(cfg.done_flag_path)
            ? "Logs/MIMISKValidation/validation_done.flag"
            : cfg.done_flag_path;

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());

        if (batchMode)
        {
            DeleteDoneFlag(harness.config.done_flag_path);
            EditorApplication.update += WatchDoneFlagAndExit;
        }

        EditorApplication.isPlaying = true;
    }

    private static MIMISKValidationHarness InstallHarnessInActiveScene(bool saveScene)
    {
        MIMISKValidationHarness existing = UnityEngine.Object.FindObjectOfType<MIMISKValidationHarness>();
        if (existing != null)
        {
            Selection.activeObject = existing.gameObject;
            return existing;
        }

        GameObject go = GameObject.Find("MIMISK_ValidationHarness");
        if (go == null)
        {
            go = new GameObject("MIMISK_ValidationHarness");
        }

        MIMISKValidationHarness harness = go.GetComponent<MIMISKValidationHarness>();
        if (harness == null)
        {
            harness = go.AddComponent<MIMISKValidationHarness>();
        }

        Selection.activeObject = go;
        if (saveScene)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }
        return harness;
    }

    private static MIMISKValidationHarness.RunConfig ReadConfig()
    {
        if (!File.Exists(ConfigPath))
        {
            WriteSimpleConfig("full_mission", "mimisk_full_mission", "trial_001", false);
        }
        string json = File.ReadAllText(ConfigPath);
        return JsonUtility.FromJson<MIMISKValidationHarness.RunConfig>(json);
    }

    private static void WriteSimpleConfig(string test, string runId, string trialId, bool grpc)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath));
        MIMISKValidationHarness.RunConfig cfg = new MIMISKValidationHarness.RunConfig();
        cfg.test = test;
        cfg.run_id = runId;
        cfg.trial_id = trialId;
        cfg.enable_ros_grpc = grpc;
        cfg.write_done_flag = true;
        cfg.done_flag_path = "Logs/MIMISKValidation/validation_done.flag";
        File.WriteAllText(ConfigPath, JsonUtility.ToJson(cfg, true));
        AssetDatabase.Refresh();
    }

    private static void DeleteDoneFlag(string flagPath)
    {
        string full = flagPath;
        if (!Path.IsPathRooted(full)) full = Path.Combine(Application.dataPath, "..", full);
        if (File.Exists(full)) File.Delete(full);
    }

    private static void WatchDoneFlagAndExit()
    {
        string configJson = File.Exists(ConfigPath) ? File.ReadAllText(ConfigPath) : "{}";
        MIMISKValidationHarness.RunConfig cfg = JsonUtility.FromJson<MIMISKValidationHarness.RunConfig>(configJson);
        string flag = string.IsNullOrEmpty(cfg.done_flag_path) ? "Logs/MIMISKValidation/validation_done.flag" : cfg.done_flag_path;
        if (!Path.IsPathRooted(flag)) flag = Path.Combine(Application.dataPath, "..", flag);
        if (!File.Exists(flag)) return;

        EditorApplication.update -= WatchDoneFlagAndExit;
        string text = File.ReadAllText(flag);
        bool failed = text.Contains("timeout_") || text.Contains("missing_") || text.Contains("failed");
        EditorApplication.isPlaying = false;
        EditorApplication.Exit(failed ? 2 : 0);
    }
}
#endif
