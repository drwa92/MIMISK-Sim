using UnityEngine;

public class MIMISKDroneFollowCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Follow Settings")]
    public Vector3 localOffset = new Vector3(0f, 1.2f, -4.0f);
    public Vector3 lookAtLocalOffset = new Vector3(0f, 0.2f, 0f);

    public float positionSmoothness = 5.0f;
    public float rotationSmoothness = 8.0f;

    [Header("Debug")]
    public bool autoFindDrone = true;

    private void Start()
    {
        if (target == null && autoFindDrone)
        {
            TryFindDrone();
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 desiredPosition = target.TransformPoint(localOffset);
        transform.position = Vector3.Lerp(
            transform.position,
            desiredPosition,
            positionSmoothness * Time.deltaTime
        );

        Vector3 lookAtPoint = target.TransformPoint(lookAtLocalOffset);
        Vector3 lookDirection = lookAtPoint - transform.position;

        if (lookDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(lookDirection, Vector3.up);

            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                desiredRotation,
                rotationSmoothness * Time.deltaTime
            );
        }
    }

    private void TryFindDrone()
    {
        GameObject drone = GameObject.Find("Drone");

        if (drone == null)
        {
            drone = GameObject.Find("MIMISK_Drone_Final");
        }

        if (drone == null)
        {
            drone = GameObject.Find("MIMISK_Drone");
        }

        if (drone != null)
        {
            target = drone.transform;
            Debug.Log("[MIMISKDroneFollowCamera] Target found: " + drone.name);
        }
        else
        {
            Debug.LogWarning("[MIMISKDroneFollowCamera] No drone target found.");
        }
    }
}
