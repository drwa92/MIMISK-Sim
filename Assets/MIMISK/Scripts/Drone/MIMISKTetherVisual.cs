using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class MIMISKTetherVisual : MonoBehaviour
{
    public Transform droneAnchor;
    public Transform rovAnchor;

    [Header("Visual")]
    public float lineWidth = 0.01f;

    private LineRenderer line;

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
        line.positionCount = 2;
        line.widthMultiplier = lineWidth;
        line.useWorldSpace = true;
    }

    private void Update()
    {
        if (droneAnchor == null || rovAnchor == null)
        {
            return;
        }

        line.widthMultiplier = lineWidth;
        line.SetPosition(0, droneAnchor.position);
        line.SetPosition(1, rovAnchor.position);
    }
}
