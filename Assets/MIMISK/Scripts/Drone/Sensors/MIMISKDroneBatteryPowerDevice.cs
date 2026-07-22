using UnityEngine;

public class MIMISKDroneBatteryPowerDevice : MonoBehaviour
{
    public enum BatteryModel
    {
        FourSLiPo,
        SixSLiPo,
        FourSLiIon
    }

    [Header("Device")]
    public BatteryModel batteryModel = BatteryModel.FourSLiPo;
    public string deviceName = "4S LiPo Power Module";

    [Header("References")]
    public MIMISKDroneModelController controller;

    [Header("Battery Pack")]
    public int cellCount = 4;
    public float capacityMah = 5000.0f;
    [Range(0.0f, 1.0f)] public float initialStateOfCharge = 1.0f;

    public float fullCellVoltage = 4.20f;
    public float nominalCellVoltage = 3.70f;
    public float emptyCellVoltage = 3.30f;

    [Tooltip("Total pack internal resistance in ohms.")]
    public float internalResistanceOhm = 0.035f;

    [Header("Current Model")]
    public float avionicsCurrentA = 1.2f;
    public float motorIdleCurrentA = 0.15f;
    public float motorMaxCurrentA = 12.0f;
    public float motorCurrentExponent = 1.35f;

    [Header("Failsafe Thresholds")]
    public float lowCellVoltage = 3.50f;
    public float criticalCellVoltage = 3.30f;
    public bool enableFailsafeOnCritical = false;

    [Header("Outputs")]
    public float stateOfCharge;
    public float consumedMah;
    public float packOpenCircuitVoltage;
    public float packVoltage;
    public float cellVoltage;
    public float currentA;
    public float powerW;
    public float consumedWh;
    public float estimatedRemainingMinutes;
    public bool lowBattery;
    public bool criticalBattery;

    [Header("Motor Current Debug")]
    public float motorFLCurrentA;
    public float motorFRCurrentA;
    public float motorRLCurrentA;
    public float motorRRCurrentA;

    public Vector4 motorFractionsFL_FR_RL_RR;

    private void Reset()
    {
        AutoFindReferences();
        ConfigureModelDefaults();
    }

    private void Awake()
    {
        AutoFindReferences();
        ConfigureModelDefaults();
    }

    private void Start()
    {
        stateOfCharge = Mathf.Clamp01(initialStateOfCharge);
        consumedMah = capacityMah * (1.0f - stateOfCharge);
        UpdateElectricalState(0.0f);
    }

    private void FixedUpdate()
    {
        UpdateElectricalState(Time.fixedDeltaTime);
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (controller == null)
        {
            controller = GetComponentInParent<MIMISKDroneModelController>();
        }
    }

    [ContextMenu("Configure Model Defaults")]
    public void ConfigureModelDefaults()
    {
        switch (batteryModel)
        {
            case BatteryModel.FourSLiPo:
                deviceName = "4S LiPo Power Module";
                cellCount = 4;
                capacityMah = 5000.0f;
                fullCellVoltage = 4.20f;
                nominalCellVoltage = 3.70f;
                emptyCellVoltage = 3.30f;
                internalResistanceOhm = 0.035f;
                break;

            case BatteryModel.SixSLiPo:
                deviceName = "6S LiPo Power Module";
                cellCount = 6;
                capacityMah = 5000.0f;
                fullCellVoltage = 4.20f;
                nominalCellVoltage = 3.70f;
                emptyCellVoltage = 3.30f;
                internalResistanceOhm = 0.050f;
                break;

            case BatteryModel.FourSLiIon:
                deviceName = "4S Li-ion Power Module";
                cellCount = 4;
                capacityMah = 6000.0f;
                fullCellVoltage = 4.20f;
                nominalCellVoltage = 3.60f;
                emptyCellVoltage = 3.00f;
                internalResistanceOhm = 0.080f;
                break;
        }
    }

    private void UpdateElectricalState(float dt)
    {
        motorFractionsFL_FR_RL_RR = Vector4.zero;

        if (controller != null)
        {
            motorFractionsFL_FR_RL_RR = controller.motorFractionsFL_FR_RL_RR;
        }

        motorFLCurrentA = MotorCurrentFromFraction(motorFractionsFL_FR_RL_RR.x);
        motorFRCurrentA = MotorCurrentFromFraction(motorFractionsFL_FR_RL_RR.y);
        motorRLCurrentA = MotorCurrentFromFraction(motorFractionsFL_FR_RL_RR.z);
        motorRRCurrentA = MotorCurrentFromFraction(motorFractionsFL_FR_RL_RR.w);

        currentA =
            avionicsCurrentA +
            motorFLCurrentA +
            motorFRCurrentA +
            motorRLCurrentA +
            motorRRCurrentA;

        if (dt > 0.0f)
        {
            consumedMah += currentA * dt * 1000.0f / 3600.0f;
            consumedMah = Mathf.Clamp(consumedMah, 0.0f, capacityMah);
        }

        stateOfCharge = 1.0f - consumedMah / Mathf.Max(1.0f, capacityMah);
        stateOfCharge = Mathf.Clamp01(stateOfCharge);

        packOpenCircuitVoltage = cellCount * EstimateOpenCircuitCellVoltage(stateOfCharge);
        packVoltage = Mathf.Max(0.0f, packOpenCircuitVoltage - currentA * internalResistanceOhm);
        cellVoltage = packVoltage / Mathf.Max(1, cellCount);

        powerW = packVoltage * currentA;

        if (dt > 0.0f)
        {
            consumedWh += powerW * dt / 3600.0f;
        }

        if (currentA > 0.1f)
        {
            float remainingAh = capacityMah * stateOfCharge / 1000.0f;
            estimatedRemainingMinutes = remainingAh / currentA * 60.0f;
        }
        else
        {
            estimatedRemainingMinutes = 999.0f;
        }

        lowBattery = cellVoltage <= lowCellVoltage;
        criticalBattery = cellVoltage <= criticalCellVoltage;

        if (enableFailsafeOnCritical && criticalBattery && controller != null)
        {
            controller.SetMode(MIMISKDroneModelController.DroneMode.Failsafe);
        }
    }

    private float MotorCurrentFromFraction(float fraction)
    {
        fraction = Mathf.Clamp01(fraction);

        if (fraction <= 0.001f)
        {
            return 0.0f;
        }

        return motorIdleCurrentA + motorMaxCurrentA * Mathf.Pow(fraction, motorCurrentExponent);
    }

    private float EstimateOpenCircuitCellVoltage(float soc)
    {
        soc = Mathf.Clamp01(soc);

        // Simple LiPo/Li-ion open-circuit curve.
        // It is intentionally smooth and configurable rather than a hard lookup table.
        float curved = Mathf.Pow(soc, 0.72f);
        return Mathf.Lerp(emptyCellVoltage, fullCellVoltage, curved);
    }

    [ContextMenu("Reset Battery To Full")]
    public void ResetBatteryToFull()
    {
        initialStateOfCharge = 1.0f;
        stateOfCharge = 1.0f;
        consumedMah = 0.0f;
        consumedWh = 0.0f;
        UpdateElectricalState(0.0f);
    }
}
