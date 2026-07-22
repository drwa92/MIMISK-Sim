using UnityEngine;

public class MIMISKPlantSway : MonoBehaviour
{
    public float swayAngleDeg = 5.0f;
    public float swaySpeed = 0.25f;
    public float phase = 0.0f;
    public Vector3 localAxis = Vector3.forward;

    private Quaternion initialRotation;

    private void Start()
    {
        initialRotation = transform.localRotation;

        if (phase == 0.0f)
        {
            phase = Random.Range(0.0f, 100.0f);
        }
    }

    private void Update()
    {
        float angle = Mathf.Sin(Time.time * swaySpeed + phase) * swayAngleDeg;
        transform.localRotation = initialRotation * Quaternion.AngleAxis(angle, localAxis.normalized);
    }
}
