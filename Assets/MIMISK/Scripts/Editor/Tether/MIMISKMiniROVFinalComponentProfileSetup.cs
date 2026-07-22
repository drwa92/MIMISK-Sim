using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MIMISKMiniROVFinalComponentProfileSetup
{
    [MenuItem("MIMISK/Drone/Tether/Apply MiniROV Final Component Profile")]
    public static void ApplyMiniRovFinalComponentProfile()
    {
        GameObject miniRov =
            GameObject.Find("MiniROV");

        if (miniRov == null)
        {
            Debug.LogError("[MIMISK] Could not find MiniROV.");
            return;
        }

        Behaviour[] behaviours =
            miniRov.GetComponentsInChildren<Behaviour>(true);

        int enabledCount = 0;
        int disabledCount = 0;

        for (int i = 0; i < behaviours.Length; i++)
        {
            Behaviour b = behaviours[i];

            if (b == null)
            {
                continue;
            }

            string typeName =
                b.GetType().Name;

            bool isFinalAllowed =
                ContainsIgnoreCase(typeName, "UnityVirtualESP32") ||
                ContainsIgnoreCase(typeName, "ControlManager") ||
                ContainsIgnoreCase(typeName, "MIMISKWaterInteraction");

            bool forceDisabled =
                ContainsIgnoreCase(typeName, "SensorManager") ||
                ContainsIgnoreCase(typeName, "SimpleROVBuoyancy");

            if (isFinalAllowed || forceDisabled)
            {
                // Initial deployment state: everything off.
                // Deployment manager enables MIMISKWaterInteraction at O
                // and UnityVirtualESP32 + ControlManager at I.
                b.enabled = false;
                disabledCount++;
                EditorUtility.SetDirty(b);
            }
        }

        Rigidbody rb =
            miniRov.GetComponent<Rigidbody>();

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            EditorUtility.SetDirty(rb);
        }

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log(
            "[MIMISK] MiniROV final component profile applied. " +
            "Initially disabled: UnityVirtualESP32, ControlManager, MIMISKWaterInteraction, SensorManager, SimpleROVBuoyancy. " +
            "Only the deployment manager will enable the final three at O/I."
        );
    }

    private static bool ContainsIgnoreCase(string text, string pattern)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(pattern))
        {
            return false;
        }

        return text.IndexOf(
            pattern,
            System.StringComparison.OrdinalIgnoreCase
        ) >= 0;
    }
}
