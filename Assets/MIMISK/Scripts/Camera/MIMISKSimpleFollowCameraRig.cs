using UnityEngine;

[DisallowMultipleComponent]
public class MIMISKSimpleFollowCameraRig : MonoBehaviour
{
    public Transform target;

    [Header("Follow")]
    public Vector3 offsetWorld = new Vector3(0.0f, 2.5f, -5.0f);
    public Vector3 lookAtOffset = new Vector3(0.0f, 0.3f, 0.0f);

    public bool rotateOffsetWithTargetYaw = true;
    public float positionResponseHz = 6.0f;
    public float rotationResponseHz = 8.0f;

    [Header("Runtime")]
    public Vector3 desiredPosition;
    public Quaternion desiredRotation;

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Quaternion yawRotation =
            rotateOffsetWithTargetYaw
                ? Quaternion.Euler(0.0f, target.eulerAngles.y, 0.0f)
                : Quaternion.identity;

        desiredPosition =
            target.position + yawRotation * offsetWorld;

        Vector3 lookPoint =
            target.position + lookAtOffset;

        Vector3 lookDirection =
            lookPoint - desiredPosition;

        if (lookDirection.sqrMagnitude < 0.0001f)
        {
            lookDirection = target.forward;
        }

        desiredRotation =
            Quaternion.LookRotation(lookDirection.normalized, Vector3.up);

        float pAlpha =
            1.0f - Mathf.Exp(-Mathf.Max(0.001f, positionResponseHz) * Time.deltaTime);

        float rAlpha =
            1.0f - Mathf.Exp(-Mathf.Max(0.001f, rotationResponseHz) * Time.deltaTime);

        transform.position =
            Vector3.Lerp(transform.position, desiredPosition, pAlpha);

        transform.rotation =
            Quaternion.Slerp(transform.rotation, desiredRotation, rAlpha);
    }

    [ContextMenu("Snap To Target")]
    public void SnapToTarget()
    {
        if (target == null)
        {
            return;
        }

        Quaternion yawRotation =
            rotateOffsetWithTargetYaw
                ? Quaternion.Euler(0.0f, target.eulerAngles.y, 0.0f)
                : Quaternion.identity;

        transform.position =
            target.position + yawRotation * offsetWorld;

        Vector3 lookPoint =
            target.position + lookAtOffset;

        Vector3 lookDirection =
            lookPoint - transform.position;

        if (lookDirection.sqrMagnitude < 0.0001f)
        {
            lookDirection = target.forward;
        }

        transform.rotation =
            Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
    }
}
