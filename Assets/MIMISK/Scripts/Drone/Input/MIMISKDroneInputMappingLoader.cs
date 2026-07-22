using System;
using System.IO;
using UnityEngine;

[RequireComponent(typeof(MIMISKDroneModelGamepadInput))]
public class MIMISKDroneInputMappingLoader : MonoBehaviour
{
    [Serializable]
    public class Mapping
    {
        public bool forceGenericAxisMapping = true;

        public string armButtonNames;
        public string takeoffButtonNames;
        public string altitudeHoldButtonNames;
        public string manualModeButtonNames;
        public string landButtonNames;
        public string disarmButtonNames;
        public string failsafeButtonNames;

        public string forwardAxisNames;
        public float forwardAxisSign = 1.0f;

        public string rightAxisNames;
        public float rightAxisSign = 1.0f;

        public string yawAxisNames;
        public float yawAxisSign = 1.0f;

        public string altitudeUpAxisNames;
        public float altitudeUpAxisSign = 1.0f;

        public string altitudeDownAxisNames;
        public float altitudeDownAxisSign = 1.0f;
    }

    public bool loadOnStart = true;
    public string mappingRelativePath = "Assets/MIMISK/Settings/InputMappings/gamesir_mapping.json";

    [Header("Debug")]
    public bool loaded;
    public string absolutePath;
    public string status = "not loaded";

    private MIMISKDroneModelGamepadInput input;

    private void Awake()
    {
        input = GetComponent<MIMISKDroneModelGamepadInput>();

        if (loadOnStart)
        {
            Load();
        }
    }

    [ContextMenu("Load Mapping Now")]
    public void Load()
    {
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        absolutePath = Path.Combine(projectRoot, mappingRelativePath);

        if (!File.Exists(absolutePath))
        {
            loaded = false;
            status = "mapping file not found: " + absolutePath;
            Debug.LogWarning("[MIMISKDroneInputMappingLoader] " + status);
            return;
        }

        Mapping m = JsonUtility.FromJson<Mapping>(File.ReadAllText(absolutePath));

        if (m == null)
        {
            loaded = false;
            status = "failed to parse mapping json";
            Debug.LogWarning("[MIMISKDroneInputMappingLoader] " + status);
            return;
        }

        input.forceGenericAxisMapping = m.forceGenericAxisMapping;

        ApplyIfNotEmpty(ref input.armButtonNames, m.armButtonNames);
        ApplyIfNotEmpty(ref input.takeoffButtonNames, m.takeoffButtonNames);
        ApplyIfNotEmpty(ref input.altitudeHoldButtonNames, m.altitudeHoldButtonNames);
        ApplyIfNotEmpty(ref input.manualModeButtonNames, m.manualModeButtonNames);
        ApplyIfNotEmpty(ref input.landButtonNames, m.landButtonNames);
        ApplyIfNotEmpty(ref input.disarmButtonNames, m.disarmButtonNames);
        ApplyIfNotEmpty(ref input.failsafeButtonNames, m.failsafeButtonNames);

        ApplyIfNotEmpty(ref input.forwardAxisNames, m.forwardAxisNames);
        input.forwardAxisSign = m.forwardAxisSign;

        ApplyIfNotEmpty(ref input.rightAxisNames, m.rightAxisNames);
        input.rightAxisSign = m.rightAxisSign;

        ApplyIfNotEmpty(ref input.yawAxisNames, m.yawAxisNames);
        input.yawAxisSign = m.yawAxisSign;

        ApplyIfNotEmpty(ref input.altitudeUpAxisNames, m.altitudeUpAxisNames);
        input.altitudeUpAxisSign = m.altitudeUpAxisSign;

        ApplyIfNotEmpty(ref input.altitudeDownAxisNames, m.altitudeDownAxisNames);
        input.altitudeDownAxisSign = m.altitudeDownAxisSign;

        loaded = true;
        status = "loaded mapping: " + absolutePath;

        Debug.Log("[MIMISKDroneInputMappingLoader] " + status);
    }

    private void ApplyIfNotEmpty(ref string target, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            target = value;
        }
    }
}
