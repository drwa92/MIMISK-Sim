using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ROVPhysicsDebug : MonoBehaviour
{
    public Vector3 position;
    public Vector3 velocity;
    public Vector3 angularVelocity;
    public int collisionContactsThisFrame;
    public string lastCollisionObject = "none";

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void FixedUpdate()
    {
        position = transform.position;
        velocity = rb.linearVelocity;
        angularVelocity = rb.angularVelocity;
        collisionContactsThisFrame = 0;
    }

    private void OnCollisionStay(Collision collision)
    {
        collisionContactsThisFrame += collision.contactCount;
        lastCollisionObject = collision.gameObject.name;
    }
}
