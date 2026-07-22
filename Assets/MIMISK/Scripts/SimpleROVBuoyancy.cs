using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SimpleROVBuoyancy : MonoBehaviour
{
    [Header("Water")]
    public float waterLevel = 0.0f;

    [Header("Buoyancy")]
    public bool useMassBasedBuoyancy = true;

    [Tooltip("Extra buoyancy in Newtons. Positive = rises, negative = sinks.")]
    public float trimBuoyancyN = 0.0f;

    [Header("Damping")]
    public float underwaterLinearDamping = 4.0f;
    public float underwaterAngularDamping = 6.0f;

    [Header("Startup")]
    public bool resetVelocityOnStart = true;

    [Header("Debug")]
    public bool underwater;
    public float neutralBuoyancyN;
    public float appliedBuoyancyN;
    public Vector3 velocityWorld;
    public Vector3 angularVelocityWorld;

    private Rigidbody rb;
    private float originalLinearDamping;
    private float originalAngularDamping;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();

        originalLinearDamping = rb.linearDamping;
        originalAngularDamping = rb.angularDamping;
    }

    private void Start()
    {
        if (resetVelocityOnStart)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private void FixedUpdate()
    {
        velocityWorld = rb.linearVelocity;
        angularVelocityWorld = rb.angularVelocity;

        underwater = transform.position.y < waterLevel;

        if (!underwater)
        {
            rb.linearDamping = originalLinearDamping;
            rb.angularDamping = originalAngularDamping;
            return;
        }

        rb.linearDamping = underwaterLinearDamping;
        rb.angularDamping = underwaterAngularDamping;

        if (useMassBasedBuoyancy)
        {
            neutralBuoyancyN = rb.mass * Mathf.Abs(Physics.gravity.y);
        }

        appliedBuoyancyN = neutralBuoyancyN + trimBuoyancyN;

        rb.AddForce(Vector3.up * appliedBuoyancyN, ForceMode.Force);
    }
}
