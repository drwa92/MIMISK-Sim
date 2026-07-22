using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MIMISKDroneSurfaceBuoyancy : MonoBehaviour
{
    [Header("Water")]
    public float waterLevel = 0.0f;

    [Header("Buoyancy Points")]
    public bool autoFindBuoyancyPoints = true;
    public Transform[] buoyancyPoints;

    [Tooltip("Approximate radius/depth scale of each buoyancy point in meters.")]
    public float floatRadius = 0.25f;

    [Tooltip("Maximum upward force per buoyancy point when fully submerged.")]
    public float buoyancyForcePerPointN = 14.0f;

    [Tooltip("Vertical damping at each buoyancy point.")]
    public float verticalDampingPerPoint = 7.0f;

    [Header("Water Drag")]
    public float surfaceLinearDrag = 2.0f;
    public float surfaceAngularDrag = 2.5f;

    [Header("Activation")]
    public bool activeOnlyNearWater = true;
    public float activationDistanceAboveWater = 1.5f;

    [Header("Debug")]
    public bool isNearWater;
    public int activePointCount;
    public float totalBuoyancyForceN;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        if (autoFindBuoyancyPoints)
        {
            AutoFindPoints();
        }
    }

    private void FixedUpdate()
    {
        isNearWater = transform.position.y < waterLevel + activationDistanceAboveWater;

        if (activeOnlyNearWater && !isNearWater)
        {
            return;
        }

        ApplySurfaceBuoyancy();
    }

    private void AutoFindPoints()
    {
        string[] names =
        {
            "Buoy_FL",
            "Buoy_FR",
            "Buoy_RL",
            "Buoy_RR"
        };

        buoyancyPoints = new Transform[names.Length];

        for (int i = 0; i < names.Length; i++)
        {
            buoyancyPoints[i] = FindDeepChild(transform, names[i]);
        }
    }

    private void ApplySurfaceBuoyancy()
    {
        activePointCount = 0;
        totalBuoyancyForceN = 0.0f;

        if (buoyancyPoints == null)
        {
            return;
        }

        foreach (Transform point in buoyancyPoints)
        {
            if (point == null)
            {
                continue;
            }

            float submergence =
                Mathf.Clamp01((waterLevel - point.position.y + floatRadius) / (2.0f * floatRadius));

            if (submergence <= 0.0f)
            {
                continue;
            }

            activePointCount++;

            Vector3 pointVelocity = rb.GetPointVelocity(point.position);

            float buoyancy = buoyancyForcePerPointN * submergence;
            float damping = -pointVelocity.y * verticalDampingPerPoint * submergence;

            float force = Mathf.Max(0.0f, buoyancy + damping);

            totalBuoyancyForceN += force;

            rb.AddForceAtPosition(Vector3.up * force, point.position, ForceMode.Force);
        }

        if (activePointCount > 0)
        {
            Vector3 horizontalVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(-horizontalVelocity * surfaceLinearDrag, ForceMode.Force);
            rb.AddTorque(-rb.angularVelocity * surfaceAngularDrag, ForceMode.Force);
        }
    }

    private Transform FindDeepChild(Transform parent, string name)
    {
        if (parent.name == name)
        {
            return parent;
        }

        foreach (Transform child in parent)
        {
            Transform result = FindDeepChild(child, name);

            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private void OnDrawGizmosSelected()
    {
        if (buoyancyPoints == null)
        {
            return;
        }

        Gizmos.color = Color.yellow;

        foreach (Transform p in buoyancyPoints)
        {
            if (p != null)
            {
                Gizmos.DrawWireSphere(p.position, floatRadius);
            }
        }
    }
}
