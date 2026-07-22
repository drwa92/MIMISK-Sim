using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKFinalTetherEndpointRigSetup
{
    [MenuItem("MIMISK/Tether/Setup Final Tether Endpoint Rig")]
    public static void SetupFinalTetherEndpointRig()
    {
        GameObject root =
            Selection.activeGameObject;

        if (root == null)
        {
            MIMISKDroneCoreTetherManager tether =
                Object.FindFirstObjectByType<MIMISKDroneCoreTetherManager>();

            if (tether != null)
            {
                root = tether.gameObject;
            }
        }

        if (root == null)
        {
            Debug.LogError("[MIMISK] Select the drone/tether root first.");
            return;
        }

        MIMISKFinalTetherEndpointRig rig =
            root.GetComponent<MIMISKFinalTetherEndpointRig>();

        if (rig == null)
        {
            rig =
                root.AddComponent<MIMISKFinalTetherEndpointRig>();
        }

        rig.tetherManager =
            root.GetComponent<MIMISKDroneCoreTetherManager>();

        rig.deployment =
            root.GetComponent<MIMISKMiniROVRealisticDeploymentManager>();

        rig.rigEnabled = true;
        rig.doNotReparentPrefabChildren = true;
        rig.followCableEndpointByWorldOffset = true;
        rig.driveActiveYellowLine = true;
        rig.lineSegments = 16;
        rig.sagScale = 0.12f;
        rig.maxSagM = 0.35f;

        rig.AutoFindReferences();
        rig.ConfigureEndpointRig();

        if (rig.tetherManager != null)
        {
            rig.tetherManager.fairleadLineStart =
                rig.fairleadStart;

            rig.tetherManager.tetherLineRenderer =
                rig.activeYellowLine;

            rig.tetherManager.movingTetherEndVisual =
                rig.shortYellowCableMesh != null
                    ? rig.shortYellowCableMesh
                    : (
                        rig.hookVisual != null
                            ? rig.hookVisual
                            : rig.hookAttachPoint
                      );

            rig.tetherManager.useVirtualEndpointWhenNoMiniRov = true;
            rig.tetherManager.miniRovRigidbody = null;
            rig.tetherManager.miniRovTetherPoint = null;

            rig.tetherManager.hideStaticShortCableMeshWhenDynamic = false;
            rig.tetherManager.staticShortDeploymentCableMesh = null;

            rig.tetherManager.minimumLengthM = 0.05f;
            rig.tetherManager.maximumLengthM = 12.0f;
            rig.tetherManager.targetDeployLengthM = 1.25f;
            rig.tetherManager.payoutSpeedMS = 0.22f;
            rig.tetherManager.recoverySpeedMS = 0.25f;

            rig.tetherManager.enableTetherForceWhenMiniRovAttached = false;
            rig.tetherManager.tetherStiffnessNPerM = 0.0f;
            rig.tetherManager.tetherDampingNPerMS = 0.0f;
            rig.tetherManager.maximumSafeTensionN = 999999.0f;

            EditorUtility.SetDirty(rig.tetherManager);
        }

        if (rig.deployment != null)
        {
            rig.deployment.yellowCableEndPoint =
                rig.shortYellowCableMesh != null
                    ? rig.shortYellowCableMesh
                    : rig.hookAttachPoint;

            rig.deployment.hookVisual =
                rig.hookVisual != null
                    ? rig.hookVisual
                    : rig.hookAttachPoint;

            rig.deployment.cableEndFollowRoot =
                rig.miniRovCableFollowRoot;

            rig.deployment.miniRovRoot =
                rig.miniRovRoot;

            rig.deployment.miniRovRigidbody =
                rig.miniRovRigidbody;

            rig.deployment.miniRovTetherAnchor =
                rig.miniRovTetherAnchor;

            rig.deployment.targetDeployLengthM = 1.25f;
            rig.deployment.releaseDepthBelowSurfaceM = 0.08f;
            rig.deployment.minimumPayoutBeforeReleaseM = 0.15f;

            rig.deployment.autoReleaseToDynamicAtWaterDepth = true;
            rig.deployment.stopAndHoldKinematicAtReleaseDepth = false;

            EditorUtility.SetDirty(rig.deployment);
        }

        EditorUtility.SetDirty(rig);

        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager.GetActiveScene()
        );

        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Final tether endpoint rig configured without prefab re-parenting on " + root.name + ".");
    }
}
