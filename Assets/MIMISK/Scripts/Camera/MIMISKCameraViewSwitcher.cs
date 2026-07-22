using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class MIMISKCameraViewSwitcher : MonoBehaviour
{
    public enum ViewMode
    {
        DroneFollow,
        MiniROVFollow,
        MiniROVFront
    }

    [Header("Cameras")]
    public Camera droneFollowCamera;
    public Camera miniRovFollowCamera;
    public Camera miniRovFrontCamera;

    [Header("Targets")]
    public Transform droneTarget;
    public Transform miniRovTarget;

    [Header("Keyboard")]
    public Key cycleKey = Key.C;
    public Key droneViewKey = Key.Digit1;
    public Key miniRovFollowKey = Key.Digit2;
    public Key miniRovFrontKey = Key.Digit3;

    [Header("Runtime")]
    public ViewMode currentView = ViewMode.DroneFollow;
    public string currentViewName = "DroneFollow";

    private void Start()
    {
        AutoFindReferences();
        ApplyView(currentView);
    }

    private void Update()
    {
        if (Keyboard.current == null)
        {
            return;
        }

        if (Keyboard.current[cycleKey].wasPressedThisFrame)
        {
            CycleView();
        }

        if (Keyboard.current[droneViewKey].wasPressedThisFrame)
        {
            ApplyView(ViewMode.DroneFollow);
        }

        if (Keyboard.current[miniRovFollowKey].wasPressedThisFrame)
        {
            ApplyView(ViewMode.MiniROVFollow);
        }

        if (Keyboard.current[miniRovFrontKey].wasPressedThisFrame)
        {
            ApplyView(ViewMode.MiniROVFront);
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (droneTarget == null)
        {
            GameObject drone = GameObject.Find("Drone");

            if (drone != null)
            {
                droneTarget = drone.transform;
            }
        }

        if (miniRovTarget == null)
        {
            GameObject rov = GameObject.Find("MiniROV");

            if (rov != null)
            {
                miniRovTarget = rov.transform;
            }
        }

        if (droneFollowCamera == null)
        {
            GameObject go = GameObject.Find("DroneFollowCamera");

            if (go != null)
            {
                droneFollowCamera = go.GetComponent<Camera>();
            }
        }

        if (miniRovFollowCamera == null)
        {
            GameObject go = GameObject.Find("MiniROVFollowCamera");

            if (go != null)
            {
                miniRovFollowCamera = go.GetComponent<Camera>();
            }
        }

        if (miniRovFrontCamera == null && miniRovTarget != null)
        {
            Transform front =
                FindDeepChild(miniRovTarget, "FrontCamera");

            if (front != null)
            {
                miniRovFrontCamera = front.GetComponent<Camera>();
            }
        }
    }

    [ContextMenu("Cycle View")]
    public void CycleView()
    {
        if (currentView == ViewMode.DroneFollow)
        {
            ApplyView(ViewMode.MiniROVFollow);
        }
        else if (currentView == ViewMode.MiniROVFollow)
        {
            ApplyView(ViewMode.MiniROVFront);
        }
        else
        {
            ApplyView(ViewMode.DroneFollow);
        }
    }

    public void ApplyView(ViewMode mode)
    {
        currentView = mode;
        currentViewName = mode.ToString();

        SetCameraEnabled(droneFollowCamera, mode == ViewMode.DroneFollow);
        SetCameraEnabled(miniRovFollowCamera, mode == ViewMode.MiniROVFollow);
        SetCameraEnabled(miniRovFrontCamera, mode == ViewMode.MiniROVFront);

        Debug.Log("[MIMISK] Camera view switched to " + currentViewName);
    }

    private void SetCameraEnabled(Camera cam, bool enabled)
    {
        if (cam == null)
        {
            return;
        }

        cam.enabled = enabled;

        AudioListener[] listeners =
            cam.GetComponents<AudioListener>();

        for (int i = 0; i < listeners.Length; i++)
        {
            listeners[i].enabled = enabled;
        }
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
}
