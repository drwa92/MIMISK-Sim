using UnityEngine;

public class KelpSway : MonoBehaviour
{
    public float swayAngleDeg = 6.0f;
    public float swaySpeed = 0.35f;
    public float phase = 0.0f;
    public Vector3 localSwayAxis = Vector3.forward;

    private Quaternion initialLocalRotation;

    private void Start()
    {
        initialLocalRotation = transform.localRotation;

        if (phase == 0.0f)
        {
            phase = Random.Range(0.0f, 100.0f);
        }
    }

    private void Update()
    {
        float angle = Mathf.Sin(Time.time * swaySpeed + phase) * swayAngleDeg;
        transform.localRotation =
            initialLocalRotation *
            Quaternion.AngleAxis(angle, localSwayAxis.normalized);
    }
}
