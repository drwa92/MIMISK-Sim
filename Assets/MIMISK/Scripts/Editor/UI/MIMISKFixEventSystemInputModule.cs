using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

public static class MIMISKFixEventSystemInputModule
{
    [MenuItem("MIMISK/UI/Fix EventSystem For Input System")]
    public static void FixEventSystemForInputSystem()
    {
        EventSystem eventSystem =
            Object.FindFirstObjectByType<EventSystem>();

        if (eventSystem == null)
        {
            GameObject go =
                new GameObject("EventSystem");

            eventSystem =
                go.AddComponent<EventSystem>();
        }

#if ENABLE_INPUT_SYSTEM
        StandaloneInputModule[] oldModules =
            eventSystem.GetComponents<StandaloneInputModule>();

        for (int i = oldModules.Length - 1; i >= 0; i--)
        {
            if (oldModules[i] != null)
            {
                Object.DestroyImmediate(oldModules[i]);
            }
        }

        InputSystemUIInputModule inputSystemModule =
            eventSystem.GetComponent<InputSystemUIInputModule>();

        if (inputSystemModule == null)
        {
            inputSystemModule =
                eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        TryAssignDefaultUiActions(inputSystemModule);
#else
        if (eventSystem.GetComponent<StandaloneInputModule>() == null)
        {
            eventSystem.gameObject.AddComponent<StandaloneInputModule>();
        }
#endif

        EditorUtility.SetDirty(eventSystem);

        EditorSceneManager.MarkSceneDirty(
            EditorSceneManager.GetActiveScene()
        );

        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] EventSystem fixed for active input handling.");
    }

#if ENABLE_INPUT_SYSTEM
    private static void TryAssignDefaultUiActions(InputSystemUIInputModule module)
    {
        if (module == null)
        {
            return;
        }

        try
        {
            System.Reflection.MethodInfo assignDefaults =
                typeof(InputSystemUIInputModule).GetMethod(
                    "AssignDefaultActions",
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic
                );

            if (assignDefaults != null)
            {
                assignDefaults.Invoke(module, null);
            }
        }
        catch
        {
        }
    }
#endif
}
