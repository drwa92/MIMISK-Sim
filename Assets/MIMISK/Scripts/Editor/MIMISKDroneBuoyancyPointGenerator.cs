using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKDroneBuoyancyPointGenerator
{
    [MenuItem("MIMISK/Drone/Create Buoyancy Points From Selected Ring")]
    public static void CreateBuoyancyPointsFromSelectedRing()
    {
        GameObject selected = Selection.activeGameObject;

        if (selected == null)
        {
            Debug.LogWarning("[MIMISK] Select the yellow buoyancy ring or lowest_water_contact object first.");
            return;
        }

        MIMISKDroneSurfaceBuoyancy buoyancy =
            selected.GetComponentInParent<MIMISKDroneSurfaceBuoyancy>();

        if (buoyancy == null)
        {
            buoyancy = Object.FindFirstObjectByType<MIMISKDroneSurfaceBuoyancy>();
        }

        if (buoyancy == null)
        {
            Debug.LogError("[MIMISK] Could not find MIMISKDroneSurfaceBuoyancy in the scene. Add it to the Drone root first.");
            return;
        }

        Transform droneRoot = buoyancy.transform;

        Bounds bounds = CalculateBounds(selected);

        if (bounds.size == Vector3.zero)
        {
            Debug.LogError("[MIMISK] Selected object has no renderer bounds. Select the actual yellow ring mesh or its parent.");
            return;
        }

        Transform buoyancyRoot = GetOrCreateChildPath(
            droneRoot,
            "SurfaceLandingSystem/BuoyancyPoints"
        );

        float radius = Mathf.Max(0.10f, buoyancy.floatRadius);

        float xOffset = Mathf.Max(0.45f, bounds.extents.x * 0.65f);
        float zOffset = Mathf.Max(0.45f, bounds.extents.z * 0.65f);

        // Place points close to the lower side of the float ring.
        // If they are too low/high, adjust local Y manually afterward.
        float pointY = bounds.min.y + radius * 0.35f;

        Vector3 center = bounds.center;

        Vector3[] worldPositions =
        {
            new Vector3(center.x - xOffset, pointY, center.z + zOffset), // FL
            new Vector3(center.x + xOffset, pointY, center.z + zOffset), // FR
            new Vector3(center.x - xOffset, pointY, center.z - zOffset), // RL
            new Vector3(center.x + xOffset, pointY, center.z - zOffset)  // RR
        };

        string[] names =
        {
            "Buoy_FL",
            "Buoy_FR",
            "Buoy_RL",
            "Buoy_RR"
        };

        Transform[] points = new Transform[4];

        for (int i = 0; i < 4; i++)
        {
            Transform existing = buoyancyRoot.Find(names[i]);

            GameObject pointObject;

            if (existing == null)
            {
                pointObject = new GameObject(names[i]);
                pointObject.transform.SetParent(buoyancyRoot, true);
            }
            else
            {
                pointObject = existing.gameObject;
            }

            pointObject.transform.position = worldPositions[i];
            pointObject.transform.rotation = Quaternion.identity;
            pointObject.transform.localScale = Vector3.one;

            points[i] = pointObject.transform;
        }

        buoyancy.buoyancyPoints = points;
        buoyancy.autoFindBuoyancyPoints = false;

        EditorUtility.SetDirty(buoyancy);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Selection.activeGameObject = buoyancyRoot.gameObject;

        Debug.Log("[MIMISK] Created and assigned 4 buoyancy points from selected ring: " + selected.name);
    }

    private static Bounds CalculateBounds(GameObject obj)
    {
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);

        if (renderers.Length == 0)
        {
            return new Bounds(obj.transform.position, Vector3.zero);
        }

        Bounds bounds = renderers[0].bounds;

        for (int i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        return bounds;
    }

    private static Transform GetOrCreateChildPath(Transform root, string path)
    {
        string[] parts = path.Split('/');
        Transform current = root;

        foreach (string part in parts)
        {
            Transform child = current.Find(part);

            if (child == null)
            {
                GameObject obj = new GameObject(part);
                obj.transform.SetParent(current, false);
                obj.transform.localPosition = Vector3.zero;
                obj.transform.localRotation = Quaternion.identity;
                obj.transform.localScale = Vector3.one;
                child = obj.transform;
            }

            current = child;
        }

        return current;
    }
}
