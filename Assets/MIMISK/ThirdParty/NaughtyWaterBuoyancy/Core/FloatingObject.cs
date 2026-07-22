using System.Collections.Generic;
using UnityEngine;

namespace NaughtyWaterBuoyancy
{
    [RequireComponent(typeof(Collider))]
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(MeshFilter))]
    public class FloatingObject : MonoBehaviour
    {
        private enum MassConfigurationMode
        {
            UseRigidbodyMass,
            UseRelativeDensity
        }

        private enum WaterType
        {
            FreshWater,
            SeaWater
        }

        private const float FreshWaterDensityKgPerM3 = 1000f;
        private const float SeaWaterDensityKgPerM3 = 1025f;

        [Header("Mass / Density")]
        [SerializeField]
        private MassConfigurationMode massConfiguration = MassConfigurationMode.UseRelativeDensity;

        [SerializeField]
        [Min(0.1f)]
        private float dryRelativeDensity = 1.1f;

        [SerializeField]
        [InspectorReadOnly]
        private float dryMassKg;

        [SerializeField]
        [InspectorReadOnly]
        private float totalMassKg;

        [SerializeField]
        [InspectorReadOnly]
        private float dryDensityKgPerM3;

        [SerializeField]
        [InspectorReadOnly]
        private float realDensityKgPerM3;

        [SerializeField]
        [InspectorReadOnly]
        private float meshVolumeM3;

        [Header("Water")]
        [SerializeField]
        private WaterType waterType = WaterType.FreshWater;

        [SerializeField]
        [InspectorReadOnly]
        private float ballastWaterDensityKgPerM3;

        [SerializeField]
        [InspectorReadOnly]
        private float externalWaterDensityKgPerM3;

        [Header("Ballasts")]
        [SerializeField]
        [Range(0f, 1f)]
        private float forwardBallastFill;

        [SerializeField]
        [Range(0f, 1f)]
        private float aftBallastFill;

        [SerializeField]
        private bool useBallastVerticalOffsets;

        [SerializeField]
        [Min(0f)]
        private float ballastFarEndDistanceM = 0.0615f;

        [SerializeField]
        [Min(0f)]
        private float ballastMaxLengthM = 0.0515f;

        [SerializeField]
        [Min(0f)]
        private float ballastDiameterM = 0.0123f;

        [SerializeField]
        [Min(0f)]
        private float ballastVerticalOffsetM = 0.0063f;

        [SerializeField]
        [InspectorReadOnly]
        private float forwardBallastLengthM;

        [SerializeField]
        [InspectorReadOnly]
        private float aftBallastLengthM;

        [SerializeField]
        [InspectorReadOnly]
        private float forwardBallastVolumeM3;

        [SerializeField]
        [InspectorReadOnly]
        private float aftBallastVolumeM3;

        [SerializeField]
        [InspectorReadOnly]
        private float forwardBallastMassKg;

        [SerializeField]
        [InspectorReadOnly]
        private float aftBallastMassKg;

        [SerializeField]
        [InspectorReadOnly]
        private Vector3 forwardBallastCenterLocal;

        [SerializeField]
        [InspectorReadOnly]
        private Vector3 aftBallastCenterLocal;

        [Header("Center Of Mass")]
        [SerializeField]
        private float centerOfMassVerticalOffset = -0.01f;

        [SerializeField]
        private bool showCenterOfMass = true;

        [SerializeField]
        private bool showBallastCylinders = true;

        [SerializeField]
        private bool showBuoyancySamples;

        [SerializeField]
        [InspectorReadOnly]
        private Vector3 geometricCenterLocal;

        [SerializeField]
        [InspectorReadOnly]
        private Vector3 dryCenterOfMassLocal;

        [SerializeField]
        [InspectorReadOnly]
        private Vector3 centerOfMassLocal;

        [SerializeField]
        [InspectorReadOnly]
        private Vector3 centerOfMassWorld;

        [Header("Buoyancy Sampling")]
        [SerializeField]
        [Min(2)]
        private int sampleCountX = 3;

        [SerializeField]
        [Min(2)]
        private int sampleCountY = 3;

        [SerializeField]
        [Min(2)]
        private int sampleCountZ = 4;

        [SerializeField]
        [InspectorReadOnly]
        private int activeSampleCount;

        [SerializeField]
        [InspectorReadOnly]
        private float estimatedSubmergedFraction;

        [Header("Water Drag")]
        [SerializeField]
        [Min(0f)]
        private float localWaterDragMultiplier = 1.5f;

        [SerializeField]
        [Min(0f)]
        private float dragInWater = 10f;

        [SerializeField]
        [Min(0f)]
        private float angularDragInWater = 20f;

        [SerializeField]
        [HideInInspector]
        private float cachedDryMassKg = 0.3f;

        [SerializeField]
        [HideInInspector]
        private float lastAppliedTotalMassKg;

        [SerializeField]
        [HideInInspector]
        private bool ballast1EmptyWarningIssued;

        [SerializeField]
        [HideInInspector]
        private bool ballast1FullWarningIssued;

        [SerializeField]
        [HideInInspector]
        private bool ballast2EmptyWarningIssued;

        [SerializeField]
        [HideInInspector]
        private bool ballast2FullWarningIssued;

        private WaterVolume water;
        private new Collider collider;
        private new Rigidbody rigidbody;
        private MeshFilter meshFilter;
        private MeshCollider meshCollider;
        private float initialDrag;
        private float initialAngularDrag;
        private Vector3 sampleExtentsLocal;
        private float sampleHeightWorld;
        private Vector3[] buoyancySamples;
        private Bounds localBounds;

        public float ForwardBallastFill01 => forwardBallastFill;
        public float AftBallastFill01 => aftBallastFill;
        public float BallastMaxLengthMeters => ballastMaxLengthM;
        public ushort Ballast1PotentiometerValue => ConvertFillToPotentiometer(forwardBallastFill);
        public ushort Ballast2PotentiometerValue => ConvertFillToPotentiometer(aftBallastFill);

        protected virtual void Awake()
        {
            CacheComponents();
            initialDrag = rigidbody.linearDamping;
            initialAngularDrag = rigidbody.angularDamping;

            if (massConfiguration == MassConfigurationMode.UseRigidbodyMass && cachedDryMassKg <= 0f)
            {
                cachedDryMassKg = Mathf.Max(0.001f, rigidbody.mass);
            }

            RefreshGeometryData();
            ClampSettings();
            UpdateMassDistribution();
            BuildBuoyancySamples();
            ApplyCenterOfMass();
        }

        protected virtual void FixedUpdate()
        {
            UpdateMassDistribution();
            ApplyCenterOfMass();

            if (water == null || buoyancySamples == null || buoyancySamples.Length == 0)
            {
                estimatedSubmergedFraction = 0f;
                RestoreDefaultDrag();
                return;
            }

            ApplyBuoyancyForces();
        }

        protected virtual void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(WaterVolume.TAG))
            {
                water = other.GetComponent<WaterVolume>();
                if (buoyancySamples == null || buoyancySamples.Length == 0)
                {
                    BuildBuoyancySamples();
                }
            }
        }

        protected virtual void OnTriggerExit(Collider other)
        {
            if (other.CompareTag(WaterVolume.TAG))
            {
                water = null;
                RestoreDefaultDrag();
            }
        }

        protected virtual void OnValidate()
        {
            CacheComponents();
            if (rigidbody == null || collider == null)
            {
                return;
            }

            ClampSettings();
            RefreshGeometryData();
            CaptureDryMassFromRigidbodyIfNeeded();
            UpdateMassDistribution();
            BuildBuoyancySamples();
            ApplyCenterOfMass();
        }

        protected virtual void OnDrawGizmos()
        {
            if (showBuoyancySamples && buoyancySamples != null)
            {
                Gizmos.color = new Color(1f, 0f, 1f, 0.35f);
                for (int i = 0; i < buoyancySamples.Length; i++)
                {
                    Vector3 worldPoint = transform.TransformPoint(buoyancySamples[i]);
                    Gizmos.DrawSphere(worldPoint, Mathf.Max(0.01f, sampleHeightWorld * 0.15f));
                }
            }

            if (showBallastCylinders)
            {
                DrawBallastGizmo(true);
                DrawBallastGizmo(false);
            }

            if (showCenterOfMass)
            {
                Vector3 worldCom = transform.TransformPoint(centerOfMassLocal);
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(worldCom, 0.03f);
                Gizmos.DrawLine(worldCom - transform.right * 0.08f, worldCom + transform.right * 0.08f);
                Gizmos.DrawLine(worldCom - transform.forward * 0.08f, worldCom + transform.forward * 0.08f);
            }
        }

        private void CacheComponents()
        {
            if (collider == null)
            {
                collider = GetComponent<Collider>();
            }

            if (rigidbody == null)
            {
                rigidbody = GetComponent<Rigidbody>();
            }

            if (meshFilter == null)
            {
                meshFilter = GetComponent<MeshFilter>();
            }

            if (meshCollider == null)
            {
                meshCollider = GetComponent<MeshCollider>();
            }
        }

        private void ClampSettings()
        {
            sampleCountX = Mathf.Max(2, sampleCountX);
            sampleCountY = Mathf.Max(2, sampleCountY);
            sampleCountZ = Mathf.Max(2, sampleCountZ);
            dryRelativeDensity = Mathf.Max(0.1f, dryRelativeDensity);
            localWaterDragMultiplier = Mathf.Max(0f, localWaterDragMultiplier);
            dragInWater = Mathf.Max(0f, dragInWater);
            angularDragInWater = Mathf.Max(0f, angularDragInWater);
            ballastFarEndDistanceM = Mathf.Max(0f, ballastFarEndDistanceM);
            ballastMaxLengthM = Mathf.Max(0f, ballastMaxLengthM);
            ballastDiameterM = Mathf.Max(0f, ballastDiameterM);
            ballastVerticalOffsetM = Mathf.Max(0f, ballastVerticalOffsetM);
        }

        private void RefreshGeometryData()
        {
            Mesh referenceMesh = GetReferenceMesh();
            if (referenceMesh == null)
            {
                meshVolumeM3 = 0f;
                localBounds = new Bounds(Vector3.zero, Vector3.one);
                geometricCenterLocal = Vector3.zero;
                return;
            }

            localBounds = referenceMesh.bounds;
            geometricCenterLocal = localBounds.center;
            meshVolumeM3 = CalculateScaledMeshVolume(referenceMesh);
        }

        private Mesh GetReferenceMesh()
        {
            if (meshCollider != null && meshCollider.sharedMesh != null)
            {
                return meshCollider.sharedMesh;
            }

            if (meshFilter != null)
            {
                return meshFilter.sharedMesh;
            }

            return null;
        }

        private float CalculateScaledMeshVolume(Mesh mesh)
        {
            float baseVolume = 0f;
            Vector3[] vertices = mesh.vertices;
            int[] triangles = mesh.triangles;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                Vector3 p1 = vertices[triangles[i + 0]];
                Vector3 p2 = vertices[triangles[i + 1]];
                Vector3 p3 = vertices[triangles[i + 2]];
                baseVolume += MathfUtils.CalculateVolume_Tetrahedron(p1, p2, p3, Vector3.zero);
            }

            Vector3 scale = transform.lossyScale;
            float scaleFactor = Mathf.Abs(scale.x * scale.y * scale.z);
            return Mathf.Abs(baseVolume) * scaleFactor;
        }

        private void CaptureDryMassFromRigidbodyIfNeeded()
        {
            if (massConfiguration != MassConfigurationMode.UseRigidbodyMass || rigidbody == null)
            {
                return;
            }

            if (!Mathf.Approximately(rigidbody.mass, lastAppliedTotalMassKg))
            {
                cachedDryMassKg = Mathf.Max(0.001f, rigidbody.mass);
            }
        }

        private void UpdateMassDistribution()
        {
            externalWaterDensityKgPerM3 = GetSelectedWaterDensityKgPerM3();
            ballastWaterDensityKgPerM3 = externalWaterDensityKgPerM3;

            dryCenterOfMassLocal = geometricCenterLocal + Vector3.up * centerOfMassVerticalOffset;
            UpdateBallastState();

            if (meshVolumeM3 <= 0f)
            {
                dryMassKg = 0f;
                totalMassKg = forwardBallastMassKg + aftBallastMassKg;
                dryDensityKgPerM3 = 0f;
                realDensityKgPerM3 = 0f;
            }
            else if (massConfiguration == MassConfigurationMode.UseRelativeDensity)
            {
                dryMassKg = meshVolumeM3 * externalWaterDensityKgPerM3 * dryRelativeDensity;
                cachedDryMassKg = dryMassKg;
                totalMassKg = dryMassKg + forwardBallastMassKg + aftBallastMassKg;
                dryDensityKgPerM3 = dryMassKg / meshVolumeM3;
                realDensityKgPerM3 = totalMassKg / meshVolumeM3;
            }
            else
            {
                dryMassKg = Mathf.Max(0.001f, cachedDryMassKg);
                totalMassKg = dryMassKg + forwardBallastMassKg + aftBallastMassKg;
                dryDensityKgPerM3 = dryMassKg / meshVolumeM3;
                realDensityKgPerM3 = totalMassKg / meshVolumeM3;
                dryRelativeDensity = dryDensityKgPerM3 / Mathf.Max(externalWaterDensityKgPerM3, 0.001f);
            }

            if (rigidbody != null)
            {
                rigidbody.mass = totalMassKg;
                lastAppliedTotalMassKg = totalMassKg;
            }
        }

        private void UpdateBallastState()
        {
            float radius = ballastDiameterM * 0.5f;
            float sectionArea = Mathf.PI * radius * radius;

            forwardBallastLengthM = forwardBallastFill * ballastMaxLengthM;
            aftBallastLengthM = aftBallastFill * ballastMaxLengthM;

            forwardBallastVolumeM3 = sectionArea * forwardBallastLengthM;
            aftBallastVolumeM3 = sectionArea * aftBallastLengthM;

            forwardBallastMassKg = forwardBallastVolumeM3 * ballastWaterDensityKgPerM3;
            aftBallastMassKg = aftBallastVolumeM3 * ballastWaterDensityKgPerM3;

            forwardBallastCenterLocal = GetBallastCenterLocal(true, forwardBallastLengthM);
            aftBallastCenterLocal = GetBallastCenterLocal(false, aftBallastLengthM);
        }

        private Vector3 GetBallastCenterLocal(bool isForwardBallast, float ballastLength)
        {
            float yOffset = 0f;
            if (useBallastVerticalOffsets)
            {
                yOffset = isForwardBallast ? ballastVerticalOffsetM : -ballastVerticalOffsetM;
            }

            float zCenter = isForwardBallast
                ? ballastFarEndDistanceM - (ballastLength * 0.5f)
                : -ballastFarEndDistanceM + (ballastLength * 0.5f);

            Vector3 localCenter = dryCenterOfMassLocal;
            localCenter.y += yOffset;
            localCenter.z += zCenter;
            return localCenter;
        }

        private void BuildBuoyancySamples()
        {
            if (collider == null)
            {
                return;
            }

            List<Vector3> samples = new List<Vector3>(sampleCountX * sampleCountY * sampleCountZ);
            Bounds worldBounds = collider.bounds;
            sampleExtentsLocal = new Vector3(
                localBounds.size.x / sampleCountX,
                localBounds.size.y / sampleCountY,
                localBounds.size.z / sampleCountZ
            );
            sampleHeightWorld = Mathf.Abs(sampleExtentsLocal.y * transform.lossyScale.y);

            for (int ix = 0; ix < sampleCountX; ix++)
            {
                for (int iy = 0; iy < sampleCountY; iy++)
                {
                    for (int iz = 0; iz < sampleCountZ; iz++)
                    {
                        Vector3 localPoint = new Vector3(
                            localBounds.min.x + sampleExtentsLocal.x * (ix + 0.5f),
                            localBounds.min.y + sampleExtentsLocal.y * (iy + 0.5f),
                            localBounds.min.z + sampleExtentsLocal.z * (iz + 0.5f)
                        );

                        Vector3 worldPoint = transform.TransformPoint(localPoint);
                        if (ColliderUtils.IsPointInsideCollider(worldPoint, collider, ref worldBounds))
                        {
                            samples.Add(localPoint);
                        }
                    }
                }
            }

            buoyancySamples = samples.ToArray();
            activeSampleCount = buoyancySamples.Length;
        }

        private void ApplyCenterOfMass()
        {
            if (rigidbody == null)
            {
                return;
            }

            Vector3 weightedCenter = dryCenterOfMassLocal * dryMassKg;
            float combinedMass = dryMassKg;

            if (forwardBallastMassKg > 0f)
            {
                weightedCenter += forwardBallastCenterLocal * forwardBallastMassKg;
                combinedMass += forwardBallastMassKg;
            }

            if (aftBallastMassKg > 0f)
            {
                weightedCenter += aftBallastCenterLocal * aftBallastMassKg;
                combinedMass += aftBallastMassKg;
            }

            centerOfMassLocal = combinedMass > 0f ? weightedCenter / combinedMass : dryCenterOfMassLocal;
            rigidbody.centerOfMass = centerOfMassLocal;
            centerOfMassWorld = transform.TransformPoint(centerOfMassLocal);
        }

        public void ApplyBallastMotorCommands(int ballast1Pwm, int ballast2Pwm, int deadzonePwm, float maxTravelRateMmPerMin, float deltaTime)
        {
            forwardBallastFill = ApplySingleBallastMotorCommand(
                forwardBallastFill,
                ballast1Pwm,
                deadzonePwm,
                maxTravelRateMmPerMin,
                deltaTime,
                "Ballast 1",
                ref ballast1EmptyWarningIssued,
                ref ballast1FullWarningIssued
            );

            aftBallastFill = ApplySingleBallastMotorCommand(
                aftBallastFill,
                ballast2Pwm,
                deadzonePwm,
                maxTravelRateMmPerMin,
                deltaTime,
                "Ballast 2",
                ref ballast2EmptyWarningIssued,
                ref ballast2FullWarningIssued
            );
        }

        private void ApplyBuoyancyForces()
        {
            float sampleVolume = activeSampleCount > 0 ? meshVolumeM3 / activeSampleCount : 0f;
            Vector3 buoyancyDirection = -Physics.gravity.normalized;

            float submergedAccumulator = 0f;
            for (int i = 0; i < buoyancySamples.Length; i++)
            {
                Vector3 worldPoint = transform.TransformPoint(buoyancySamples[i]);
                float waterLevel = water.GetWaterLevel(worldPoint);
                float deepLevel = waterLevel - worldPoint.y + (sampleHeightWorld * 0.5f);
                float submergedFactor = Mathf.Clamp01(deepLevel / Mathf.Max(sampleHeightWorld, 0.0001f));

                if (submergedFactor <= 0f)
                {
                    continue;
                }

                submergedAccumulator += submergedFactor;

                Vector3 buoyancyForce = buoyancyDirection * externalWaterDensityKgPerM3 * Physics.gravity.magnitude * sampleVolume * submergedFactor;
                Vector3 pointVelocity = rigidbody.GetPointVelocity(worldPoint);
                Vector3 waterDragForce = -pointVelocity * externalWaterDensityKgPerM3 * sampleVolume * localWaterDragMultiplier * submergedFactor;

                rigidbody.AddForceAtPosition(buoyancyForce + waterDragForce, worldPoint, ForceMode.Force);
                Debug.DrawLine(worldPoint, worldPoint + buoyancyForce.normalized * 0.1f, Color.blue);
            }

            estimatedSubmergedFraction = activeSampleCount > 0 ? submergedAccumulator / activeSampleCount : 0f;
            rigidbody.linearDamping = Mathf.Lerp(initialDrag, dragInWater, estimatedSubmergedFraction);
            rigidbody.angularDamping = Mathf.Lerp(initialAngularDrag, angularDragInWater, estimatedSubmergedFraction);
        }

        private void RestoreDefaultDrag()
        {
            if (rigidbody == null)
            {
                return;
            }

            rigidbody.linearDamping = initialDrag;
            rigidbody.angularDamping = initialAngularDrag;
        }

        private float ApplySingleBallastMotorCommand(
            float currentFill,
            int signedPwm,
            int deadzonePwm,
            float maxTravelRateMmPerMin,
            float deltaTime,
            string ballastName,
            ref bool emptyWarningIssued,
            ref bool fullWarningIssued)
        {
            int clampedPwm = Mathf.Clamp(signedPwm, -255, 255);
            int absolutePwm = Mathf.Abs(clampedPwm);
            if (absolutePwm <= deadzonePwm || maxTravelRateMmPerMin <= 0f || ballastMaxLengthM <= 0f)
            {
                emptyWarningIssued = false;
                fullWarningIssued = false;
                return currentFill;
            }

            float normalizedCommand = (absolutePwm - deadzonePwm) / Mathf.Max(1f, 255f - deadzonePwm);
            float maxTravelRateMps = maxTravelRateMmPerMin / 1000f / 60f;
            float deltaLength = Mathf.Sign(clampedPwm) * maxTravelRateMps * normalizedCommand * deltaTime;
            float deltaFill = deltaLength / ballastMaxLengthM;

            float nextFill = currentFill + deltaFill;
            if (nextFill >= 1f)
            {
                nextFill = 1f;
                if (clampedPwm > 0)
                {
                    if (!fullWarningIssued)
                    {
                        Debug.LogWarning($"[FloatingObject] {ballastName} a atteint sa butee pleine. La commande est limitee.");
                    }
                    fullWarningIssued = true;
                    emptyWarningIssued = false;
                }
            }
            else if (nextFill <= 0f)
            {
                nextFill = 0f;
                if (clampedPwm < 0)
                {
                    if (!emptyWarningIssued)
                    {
                        Debug.LogWarning($"[FloatingObject] {ballastName} a atteint sa butee vide. La commande est limitee.");
                    }
                    emptyWarningIssued = true;
                    fullWarningIssued = false;
                }
            }
            else
            {
                emptyWarningIssued = false;
                fullWarningIssued = false;
            }

            return nextFill;
        }

        private static ushort ConvertFillToPotentiometer(float fill01)
        {
            return (ushort)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(fill01) * 1000f), 0, 1000);
        }

        private float GetSelectedWaterDensityKgPerM3()
        {
            return waterType == WaterType.SeaWater ? SeaWaterDensityKgPerM3 : FreshWaterDensityKgPerM3;
        }

        private void DrawBallastGizmo(bool isForwardBallast)
        {
            float radius = ballastDiameterM * 0.5f;
            Vector3 cavityStartLocal = GetBallastFarEndLocal(isForwardBallast);
            Vector3 cavityEndLocal = GetBallastNearEndLocal(isForwardBallast, ballastMaxLengthM);
            float currentLength = isForwardBallast ? forwardBallastLengthM : aftBallastLengthM;
            Vector3 fillEndLocal = GetBallastNearEndLocal(isForwardBallast, currentLength);

            DrawWireCylinder(cavityStartLocal, cavityEndLocal, radius, new Color(0.8f, 0.8f, 0.8f, 0.7f));
            if (currentLength > 0f)
            {
                Color fillColor = isForwardBallast ? new Color(0.2f, 0.8f, 1f, 0.9f) : new Color(1f, 0.5f, 0.2f, 0.9f);
                DrawWireCylinder(cavityStartLocal, fillEndLocal, radius, fillColor);
            }
        }

        private Vector3 GetBallastFarEndLocal(bool isForwardBallast)
        {
            float yOffset = 0f;
            if (useBallastVerticalOffsets)
            {
                yOffset = isForwardBallast ? ballastVerticalOffsetM : -ballastVerticalOffsetM;
            }

            Vector3 localPoint = dryCenterOfMassLocal;
            localPoint.y += yOffset;
            localPoint.z += isForwardBallast ? ballastFarEndDistanceM : -ballastFarEndDistanceM;
            return localPoint;
        }

        private Vector3 GetBallastNearEndLocal(bool isForwardBallast, float ballastLength)
        {
            Vector3 localPoint = GetBallastFarEndLocal(isForwardBallast);
            localPoint.z += isForwardBallast ? -ballastLength : ballastLength;
            return localPoint;
        }

        private void DrawWireCylinder(Vector3 startLocal, Vector3 endLocal, float radius, Color color)
        {
            if (radius <= 0f)
            {
                return;
            }

            const int segments = 16;
            Vector3 startWorld = transform.TransformPoint(startLocal);
            Vector3 endWorld = transform.TransformPoint(endLocal);
            Vector3 axisWorld = (endWorld - startWorld).normalized;
            if (axisWorld.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            Vector3 sideA = Vector3.Cross(axisWorld, transform.up);
            if (sideA.sqrMagnitude <= 0.000001f)
            {
                sideA = Vector3.Cross(axisWorld, transform.right);
            }

            sideA.Normalize();
            Vector3 sideB = Vector3.Cross(axisWorld, sideA).normalized;
            float worldRadius = radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);

            Gizmos.color = color;
            Vector3 previousStart = startWorld + sideA * worldRadius;
            Vector3 previousEnd = endWorld + sideA * worldRadius;
            for (int i = 1; i <= segments; i++)
            {
                float angle = (Mathf.PI * 2f * i) / segments;
                Vector3 radial = (sideA * Mathf.Cos(angle) + sideB * Mathf.Sin(angle)) * worldRadius;
                Vector3 nextStart = startWorld + radial;
                Vector3 nextEnd = endWorld + radial;

                Gizmos.DrawLine(previousStart, nextStart);
                Gizmos.DrawLine(previousEnd, nextEnd);
                Gizmos.DrawLine(previousStart, previousEnd);

                previousStart = nextStart;
                previousEnd = nextEnd;
            }
        }
    }
}
