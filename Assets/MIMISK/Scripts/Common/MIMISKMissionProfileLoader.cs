using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKMissionProfileLoader : MonoBehaviour
{
    [Header("Profile")]
    public MIMISKMissionProfile profile;

    [Header("Loaded Trajectories")]
    public MIMISKTrajectory droneTrajectory;
    public MIMISKTrajectory miniRovTrajectory;

    [Header("Runtime Preview")]
    public bool loadOnStart = true;
    public bool profileLoaded;
    public string loadStatus = "not_loaded";

    public int dronePointCount;
    public float droneDurationS;

    public int miniRovPointCount;
    public float miniRovDurationS;

    private void Start()
    {
        if (loadOnStart)
        {
            LoadProfile();
        }
    }

    [ContextMenu("Load Profile")]
    public void LoadProfile()
    {
        profileLoaded = false;
        loadStatus = "loading";

        if (profile == null)
        {
            loadStatus = "missing_profile";
            Debug.LogWarning("[MIMISK] Mission profile loader: missing profile.");
            return;
        }

        droneTrajectory =
            profile.droneTrajectory != null
                ? MIMISKTrajectory.FromText(profile.droneTrajectory.text, profile.droneTrajectory.name)
                : new MIMISKTrajectory();

        miniRovTrajectory =
            profile.miniRovTrajectory != null
                ? MIMISKTrajectory.FromText(profile.miniRovTrajectory.text, profile.miniRovTrajectory.name)
                : new MIMISKTrajectory();

        dronePointCount =
            droneTrajectory != null && droneTrajectory.points != null
                ? droneTrajectory.points.Count
                : 0;

        droneDurationS =
            droneTrajectory != null
                ? droneTrajectory.Duration
                : 0.0f;

        miniRovPointCount =
            miniRovTrajectory != null && miniRovTrajectory.points != null
                ? miniRovTrajectory.points.Count
                : 0;

        miniRovDurationS =
            miniRovTrajectory != null
                ? miniRovTrajectory.Duration
                : 0.0f;

        profileLoaded =
            miniRovPointCount > 0 ||
            dronePointCount > 0;

        loadStatus =
            "loaded mission=" + profile.missionId +
            " drone_points=" + dronePointCount +
            " rov_points=" + miniRovPointCount;

        Debug.Log("[MIMISK] Mission profile loaded: " + loadStatus);
    }
}
