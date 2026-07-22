using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKMiniROVCollisionIsolation : MonoBehaviour
{
    [Header("References")]
    public Transform miniRovRoot;
    public GameObject droneRoot;
    public Collider[] miniRovColliders;
    public Collider[] droneColliders;

    [Header("Isolation")]
    public bool isolationEnabled = true;
    public bool ignoreDroneCollisions = true;

    [Header("Runtime")]
    public int ignoredPairs;
    public string lastEvent = "idle";

    private void Awake()
    {
        AutoFindReferences();
    }

    private void Start()
    {
        ApplyIsolation();
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (miniRovRoot == null)
        {
            miniRovRoot = transform;
        }

        if (droneRoot == null)
        {
            droneRoot = GameObject.Find("Drone");
        }

        if (miniRovRoot != null)
        {
            miniRovColliders =
                miniRovRoot.GetComponentsInChildren<Collider>(true);
        }

        if (droneRoot != null)
        {
            droneColliders =
                droneRoot.GetComponentsInChildren<Collider>(true);
        }
    }

    [ContextMenu("Apply Isolation")]
    public void ApplyIsolation()
    {
        if (!isolationEnabled || !ignoreDroneCollisions)
        {
            return;
        }

        AutoFindReferences();

        ignoredPairs = 0;

        if (miniRovColliders == null || droneColliders == null)
        {
            return;
        }

        for (int i = 0; i < miniRovColliders.Length; i++)
        {
            Collider rovCol = miniRovColliders[i];

            if (rovCol == null)
            {
                continue;
            }

            for (int j = 0; j < droneColliders.Length; j++)
            {
                Collider droneCol = droneColliders[j];

                if (droneCol == null || droneCol == rovCol)
                {
                    continue;
                }

                Physics.IgnoreCollision(
                    rovCol,
                    droneCol,
                    true
                );

                ignoredPairs++;
            }
        }

        lastEvent = "ignored_pairs_" + ignoredPairs;
    }
}
