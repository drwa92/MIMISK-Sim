using System;
using System.Reflection;
using UnityEngine;

using MimiskExternalReferenceBatch = MIMISK.Grpc.ExternalReferenceBatch;
using MimiskExternalReferenceCommand = MIMISK.Grpc.ExternalReferenceCommand;
using MimiskExternalReferenceRequest = MIMISK.Grpc.ExternalReferenceRequest;
using MimiskVec3 = MIMISK.Grpc.Vec3;

[DefaultExecutionOrder(2725)]
[DisallowMultipleComponent]
public class MIMISKGrpcExternalReferenceBridge : MonoBehaviour
{
    [Header("Connection")]
    public MIMISKGrpcConnection connection;
    public bool externalReferenceBridgeEnabled = true;
    public float pollHz = 20.0f;
    public string simId = "mimisk_unity_v2";

    [Header("Drone")]
    public bool enableDroneExternalReference = true;
    public bool autoEnterDroneExternalReferenceMode = true;
    public MonoBehaviour droneCoreController;
    public MonoBehaviour droneFlightModeManager;

    [Header("MiniROV")]
    public bool enableMiniRovExternalReference = true;
    public bool autoStartMiniRovGoToPoint = true;
    public MonoBehaviour miniRovController;
    public MonoBehaviour miniRovPathPlanner;
    public MonoBehaviour miniRovMissionManager;

    [Header("Tether")]
    public bool enableTetherExternalReference = true;
    public MonoBehaviour lowLevelTetherManager;
    public MonoBehaviour unifiedTetherManager;
    public MonoBehaviour smartWinchController;

    [Header("Runtime")]
    public bool autoFindReferences = true;
    public bool pollInProgress;
    public int referencesReceived;
    public int referencesApplied;
    public int referencesBlocked;
    public string lastDroneReference = "none";
    public string lastMiniRovReference = "none";
    public string lastTetherReference = "none";
    public string lastStatus = "idle";

    private float timerS;

    private void Awake()
    {
        if (autoFindReferences)
        {
            AutoFindReferences();
        }
    }

    private void Start()
    {
        if (connection == null)
        {
            connection = GetComponent<MIMISKGrpcConnection>();
        }

        if (connection == null)
        {
            connection = FindFirstObjectByType<MIMISKGrpcConnection>();
        }

        if (autoFindReferences)
        {
            AutoFindReferences();
        }
    }

    private void Update()
    {
        if (!externalReferenceBridgeEnabled)
        {
            return;
        }

        if (connection == null ||
            connection.Client == null ||
            !connection.isConnected)
        {
            return;
        }

        timerS += Time.deltaTime;

        float period =
            1.0f / Mathf.Max(1.0f, pollHz);

        if (timerS >= period)
        {
            timerS -= period;
            PollExternalReferences();
        }
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (connection == null)
        {
            connection = GetComponent<MIMISKGrpcConnection>();

            if (connection == null)
            {
                connection = FindFirstObjectByType<MIMISKGrpcConnection>();
            }
        }

        if (droneCoreController == null)
        {
            droneCoreController = FindBehaviourByTypeName("MIMISKDroneCoreRotorController");
        }

        if (droneFlightModeManager == null)
        {
            droneFlightModeManager = FindBehaviourByTypeName("MIMISKDroneCoreFlightModeManager");
        }

        if (miniRovController == null)
        {
            miniRovController = FindBehaviourByTypeName("MIMISKMiniROVPlantBasedController");
        }

        if (miniRovPathPlanner == null)
        {
            miniRovPathPlanner = FindBehaviourByTypeName("MIMISKMiniROVPathPlanner");
        }

        if (miniRovMissionManager == null)
        {
            miniRovMissionManager = FindBehaviourByTypeName("MIMISKMiniROVMissionManager");
        }

        if (lowLevelTetherManager == null)
        {
            lowLevelTetherManager = FindBehaviourByTypeName("MIMISKDroneCoreTetherManager");
        }

        if (unifiedTetherManager == null)
        {
            unifiedTetherManager = FindBehaviourByTypeName("MIMISKUnifiedTetherManager");
        }

        if (smartWinchController == null)
        {
            smartWinchController = FindBehaviourByTypeName("MIMISKTetherSmartWinchController");
        }
    }

    [ContextMenu("Poll External References Once")]
    public async void PollExternalReferences()
    {
        if (pollInProgress)
        {
            return;
        }

        if (connection == null ||
            connection.Client == null ||
            !connection.isConnected)
        {
            lastStatus = "not_connected";
            return;
        }

        pollInProgress = true;

        try
        {
            MimiskExternalReferenceBatch batch =
                await connection.Client.GetExternalReferencesAsync(
                    new MimiskExternalReferenceRequest
                    {
                        SimId = simId
                    }
                );

            if (batch == null || batch.References == null)
            {
                return;
            }

            referencesReceived += batch.References.Count;

            for (int i = 0; i < batch.References.Count; i++)
            {
                ApplyReference(batch.References[i]);
            }
        }
        catch (Exception ex)
        {
            lastStatus =
                ex.GetType().Name + ": " + ex.Message;

            Debug.LogWarning(
                "[MIMISK gRPC] External reference polling failed: " +
                lastStatus
            );

            if (connection != null)
            {
                connection.isConnected = false;
            }
        }
        finally
        {
            pollInProgress = false;
        }
    }

    private void ApplyReference(MimiskExternalReferenceCommand referenceCommand)
    {
        if (referenceCommand == null || !referenceCommand.Enabled)
        {
            return;
        }

        if (autoFindReferences)
        {
            AutoFindReferences();
        }

        string target =
            Normalize(referenceCommand.TargetAgent);

        if (target == "drone")
        {
            ApplyDroneReference(referenceCommand);
            return;
        }

        if (target == "minirov" || target == "rov")
        {
            ApplyMiniRovReference(referenceCommand);
            return;
        }

        if (target == "tether")
        {
            ApplyTetherReference(referenceCommand);
            return;
        }

        referencesBlocked++;
        lastStatus = "unknown_external_reference_target_" + target;
    }

    private void ApplyDroneReference(MimiskExternalReferenceCommand r)
    {
        if (!enableDroneExternalReference || droneCoreController == null)
        {
            referencesBlocked++;
            lastStatus = "drone_external_reference_disabled_or_missing_core";
            return;
        }

        Vector3 position =
            r.PositionValid
                ? RosToUnity(r.Position)
                : ReadVector3(droneCoreController, "referencePositionWorld", Vector3.zero);

        Vector3 velocity =
            r.VelocityValid
                ? RosToUnity(r.Velocity)
                : Vector3.zero;

        Vector3 acceleration =
            r.AccelerationValid
                ? RosToUnity(r.Acceleration)
                : Vector3.zero;

        float yawDeg =
            r.YawValid
                ? (float)r.YawDeg
                : ReadFloat(droneCoreController, "referenceYawDeg", 0.0f);

        if (autoEnterDroneExternalReferenceMode)
        {
            SetEnumFieldOrProperty(droneFlightModeManager, "flightMode", "ExternalReference");
            SetEnumFieldOrProperty(droneCoreController, "controlMode", "ExternalReference");
        }

        // Keep the flight manager reference fields aligned too, so it does not fight the core.
        SetVector3Field(droneFlightModeManager, "referencePositionWorld", position);
        SetVector3Field(droneFlightModeManager, "referenceVelocityWorld", velocity);
        SetVector3Field(droneFlightModeManager, "referenceAccelerationWorld", acceleration);
        SetFloatField(droneFlightModeManager, "referenceYawDeg", yawDeg);

        bool methodOk =
            CallMethod4(
                droneCoreController,
                "SetExternalReference",
                position,
                velocity,
                acceleration,
                yawDeg
            );

        if (!methodOk)
        {
            SetVector3Field(droneCoreController, "referencePositionWorld", position);
            SetVector3Field(droneCoreController, "referenceVelocityWorld", velocity);
            SetVector3Field(droneCoreController, "referenceAccelerationWorld", acceleration);
            SetFloatField(droneCoreController, "referenceYawDeg", yawDeg);
            SetEnumFieldOrProperty(droneCoreController, "controlMode", "ExternalReference");
        }

        referencesApplied++;

        lastDroneReference =
            "pos=" + position.ToString("F2") +
            " vel=" + velocity.ToString("F2") +
            " yaw=" + yawDeg.ToString("F1");

        lastStatus = "drone_external_reference_applied";
    }

    private void ApplyMiniRovReference(MimiskExternalReferenceCommand r)
    {
        if (!enableMiniRovExternalReference || miniRovController == null)
        {
            referencesBlocked++;
            lastStatus = "minirov_external_reference_disabled_or_missing_controller";
            return;
        }

        Vector3 pointWorld =
            r.PositionValid
                ? RosToUnity(r.Position)
                : ReadVector3(miniRovController, "targetPointWorld", Vector3.zero);

        float depthM =
            r.DepthValid
                ? (float)r.DepthM
                : ReadFloat(miniRovController, "targetPointDepthM", 0.0f);

        float yawDeg =
            r.YawValid
                ? (float)r.YawDeg
                : ReadFloat(miniRovController, "fixedYawDeg", 0.0f);

        SetVector3Field(miniRovController, "targetPointWorld", pointWorld);
        SetFloatField(miniRovController, "targetPointDepthM", depthM);

        SetVector3Field(miniRovPathPlanner, "goToPointWorld", pointWorld);
        SetFloatField(miniRovPathPlanner, "goToPointDepthM", depthM);

        if (r.YawValid)
        {
            SetFloatField(miniRovController, "fixedYawDeg", yawDeg);
            SetBoolField(miniRovController, "hasFinalYawReference", true);
        }

        if (autoStartMiniRovGoToPoint)
        {
            string currentMode =
                ReadString(miniRovController, "controlMode", "");

            if (!string.Equals(currentMode, "GoToPoint", StringComparison.OrdinalIgnoreCase))
            {
                bool started =
                    CallMethodEnumArg(
                        miniRovController,
                        "StartController",
                        "GoToPoint"
                    );

                if (!started)
                {
                    SetEnumFieldOrProperty(miniRovController, "controlMode", "GoToPoint");
                }
            }
        }

        referencesApplied++;

        lastMiniRovReference =
            "point=" + pointWorld.ToString("F2") +
            " depth=" + depthM.ToString("F2") +
            " yaw=" + yawDeg.ToString("F1");

        lastStatus = "minirov_external_reference_applied";
    }

    private void ApplyTetherReference(MimiskExternalReferenceCommand r)
    {
        if (!enableTetherExternalReference)
        {
            referencesBlocked++;
            lastStatus = "tether_external_reference_disabled";
            return;
        }

        float targetLength =
            (float)r.TargetLengthM;

        if (targetLength <= 0.0f)
        {
            referencesBlocked++;
            lastStatus = "tether_reference_missing_positive_target_length";
            return;
        }

        SetFloatField(lowLevelTetherManager, "targetLengthM", targetLength);
        SetFloatField(lowLevelTetherManager, "targetDeployLengthM", targetLength);

        SetFloatField(unifiedTetherManager, "targetDeployLengthM", targetLength);
        SetFloatField(smartWinchController, "targetLengthM", targetLength);
        SetFloatField(smartWinchController, "desiredLengthM", targetLength);

        referencesApplied++;

        lastTetherReference =
            "target_length=" + targetLength.ToString("F2") + " m";

        lastStatus = "tether_external_reference_applied";
    }

    private Vector3 RosToUnity(MimiskVec3 v)
    {
        return
            new Vector3(
                (float)v.X,
                (float)v.Z,
                (float)v.Y
            );
    }

    private MonoBehaviour FindBehaviourByTypeName(string typeName)
    {
        MonoBehaviour[] behaviours =
            Resources.FindObjectsOfTypeAll<MonoBehaviour>();

        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour b = behaviours[i];

            if (b == null ||
                b.gameObject == null ||
                !b.gameObject.scene.IsValid())
            {
                continue;
            }

            if (b.GetType().Name == typeName)
            {
                return b;
            }
        }

        return null;
    }

    private bool CallMethod4(
        object target,
        string methodName,
        Vector3 a,
        Vector3 b,
        Vector3 c,
        float d)
    {
        if (target == null)
        {
            return false;
        }

        MethodInfo method =
            target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic
            );

        if (method == null || method.GetParameters().Length != 4)
        {
            return false;
        }

        try
        {
            method.Invoke(target, new object[] { a, b, c, d });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool CallMethodEnumArg(object target, string methodName, string enumValue)
    {
        if (target == null)
        {
            return false;
        }

        MethodInfo[] methods =
            target.GetType().GetMethods(
                BindingFlags.Instance |
                BindingFlags.Public |
                BindingFlags.NonPublic
            );

        for (int i = 0; i < methods.Length; i++)
        {
            MethodInfo method = methods[i];

            if (method.Name != methodName)
            {
                continue;
            }

            ParameterInfo[] parameters = method.GetParameters();

            if (parameters.Length != 1 || !parameters[0].ParameterType.IsEnum)
            {
                continue;
            }

            try
            {
                object enumObj =
                    Enum.Parse(parameters[0].ParameterType, enumValue, true);

                method.Invoke(target, new object[] { enumObj });
                return true;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    private bool SetEnumFieldOrProperty(object target, string memberName, string enumName)
    {
        if (target == null)
        {
            return false;
        }

        BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        Type type = target.GetType();

        FieldInfo field = type.GetField(memberName, flags);

        if (field != null && field.FieldType.IsEnum)
        {
            object value = Enum.Parse(field.FieldType, enumName, true);
            field.SetValue(target, value);
            return true;
        }

        PropertyInfo prop = type.GetProperty(memberName, flags);

        if (prop != null && prop.CanWrite && prop.PropertyType.IsEnum)
        {
            object value = Enum.Parse(prop.PropertyType, enumName, true);
            prop.SetValue(target, value, null);
            return true;
        }

        return false;
    }

    private void SetVector3Field(object target, string name, Vector3 value)
    {
        SetMember(target, name, value, typeof(Vector3));
    }

    private void SetFloatField(object target, string name, float value)
    {
        SetMember(target, name, value, typeof(float));
    }

    private void SetBoolField(object target, string name, bool value)
    {
        SetMember(target, name, value, typeof(bool));
    }

    private void SetMember(object target, string name, object value, Type expectedType)
    {
        if (target == null || string.IsNullOrEmpty(name))
        {
            return;
        }

        BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        FieldInfo field =
            target.GetType().GetField(name, flags);

        if (field != null && field.FieldType == expectedType)
        {
            field.SetValue(target, value);
            return;
        }

        PropertyInfo prop =
            target.GetType().GetProperty(name, flags);

        if (prop != null && prop.CanWrite && prop.PropertyType == expectedType)
        {
            prop.SetValue(target, value, null);
        }
    }

    private object GetMember(object target, string name)
    {
        if (target == null || string.IsNullOrEmpty(name))
        {
            return null;
        }

        BindingFlags flags =
            BindingFlags.Instance |
            BindingFlags.Public |
            BindingFlags.NonPublic;

        FieldInfo field =
            target.GetType().GetField(name, flags);

        if (field != null)
        {
            return field.GetValue(target);
        }

        PropertyInfo prop =
            target.GetType().GetProperty(name, flags);

        if (prop != null && prop.CanRead)
        {
            return prop.GetValue(target, null);
        }

        return null;
    }

    private string ReadString(object target, string name, string fallback)
    {
        object value = GetMember(target, name);
        return value != null ? value.ToString() : fallback;
    }

    private float ReadFloat(object target, string name, float fallback)
    {
        object value = GetMember(target, name);

        if (value is float)
        {
            return (float)value;
        }

        if (value is double)
        {
            return (float)(double)value;
        }

        if (value is int)
        {
            return (int)value;
        }

        return fallback;
    }

    private Vector3 ReadVector3(object target, string name, Vector3 fallback)
    {
        object value = GetMember(target, name);

        if (value is Vector3)
        {
            return (Vector3)value;
        }

        return fallback;
    }

    private string Normalize(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        return
            value.Trim()
                 .Replace("-", "_")
                 .Replace(" ", "_")
                 .ToLowerInvariant();
    }
}
