using UnityEngine;

[DefaultExecutionOrder(3500)]
[DisallowMultipleComponent]
public class MIMISKTetherCableModel : MonoBehaviour
{
    [Header("References")]
    public MIMISKUnifiedTetherManager unifiedTether;
    public MIMISKDroneCoreTetherManager tetherManager;

    [Tooltip("Optional. If assigned, this LineRenderer will be drawn with the same curved cable points.")]
    public LineRenderer debugLineRenderer;

    public MeshFilter cableMeshFilter;
    public MeshRenderer cableMeshRenderer;

    [Header("Cable Geometry")]
    public bool cableModelEnabled = true;

    [Tooltip("Number of samples along the cable curve.")]
    [Range(8, 96)]
    public int curveSegments = 36;

    [Tooltip("Number of radial samples for the tube mesh.")]
    [Range(4, 16)]
    public int radialSegments = 8;

    [Tooltip("Visual radius of the tether cable.")]
    public float cableRadiusM = 0.008f;

    [Tooltip("Additional sag gain applied to slack length.")]
    public float slackSagGain = 0.45f;

    [Tooltip("Maximum visual sag of the cable.")]
    public float maxSagM = 0.65f;

    [Tooltip("Small baseline sag even when almost semi-taut.")]
    public float minimumVisualSagM = 0.015f;

    [Header("Current / Wave Visual Deflection")]
    public bool enableCurrentDeflection = true;

    [Tooltip("World direction of water current. Only horizontal direction is used.")]
    public Vector3 currentDirectionWorld = new Vector3(1.0f, 0.0f, 0.0f);

    [Tooltip("How much current bends a slack cable.")]
    public float currentDeflectionGain = 0.18f;

    public bool enableSmallWaveMotion = true;
    public float waveAmplitudeM = 0.018f;
    public float waveSpatialFrequency = 2.0f;
    public float waveTemporalFrequency = 0.65f;

    [Header("Visual State")]
    public bool driveDebugLineRenderer = true;
    public bool colorByTetherState = true;

    public Color semiTautColor = new Color(1.0f, 0.82f, 0.05f, 1.0f);
    public Color slackColor = new Color(1.0f, 0.95f, 0.25f, 1.0f);
    public Color overTautColor = new Color(1.0f, 0.28f, 0.05f, 1.0f);

    [Header("Runtime Metrics")]
    public Vector3 startWorld;
    public Vector3 endWorld;
    public float deployedLengthM;
    public float straightDistanceM;
    public float curveLengthM;
    public float slackM;
    public float stretchM;
    public float tensionN;
    public float slackRatio;
    public float stretchRatio;
    public float sagAmplitudeM;
    public float currentDeflectionM;
    public string cableState = "unknown";

    private Vector3[] curvePoints;
    private Mesh cableMesh;

    private Vector3[] vertices;
    private Vector3[] normals;
    private Vector2[] uvs;
    private int[] triangles;

    private void Awake()
    {
        AutoFindReferences();
        EnsureMesh();
    }

    private void LateUpdate()
    {
        if (!cableModelEnabled)
        {
            return;
        }

        AutoFindReferences();

        if (tetherManager == null)
        {
            return;
        }

        EnsureMesh();
        UpdateMeasurements();
        BuildCurve();
        UpdateLineRenderer();
        UpdateTubeMesh();
        UpdateMaterialState();
    }

    [ContextMenu("Auto Find References")]
    public void AutoFindReferences()
    {
        if (unifiedTether == null)
        {
            unifiedTether =
                GetComponent<MIMISKUnifiedTetherManager>();
        }

        if (tetherManager == null)
        {
            tetherManager =
                GetComponent<MIMISKDroneCoreTetherManager>();
        }

        if (debugLineRenderer == null && tetherManager != null)
        {
            debugLineRenderer =
                tetherManager.tetherLineRenderer;
        }

        if (debugLineRenderer == null)
        {
            Transform line =
                FindDeepChild(transform, "MiniROV_ActiveYellowTetherLine");

            if (line == null)
            {
                line =
                    FindDeepChild(transform, "MIMISK_Final_ActiveYellowTetherLine");
            }

            if (line != null)
            {
                debugLineRenderer =
                    line.GetComponent<LineRenderer>();

                if (debugLineRenderer == null)
                {
                    debugLineRenderer =
                        line.gameObject.AddComponent<LineRenderer>();
                }
            }
        }

        if (cableMeshFilter == null)
        {
            Transform meshRoot =
                FindDeepChild(transform, "MIMISK_RealisticTetherCableMesh");

            if (meshRoot == null)
            {
                GameObject go =
                    new GameObject("MIMISK_RealisticTetherCableMesh");

                go.transform.SetParent(transform, false);
                meshRoot = go.transform;
            }

            cableMeshFilter =
                meshRoot.GetComponent<MeshFilter>();

            if (cableMeshFilter == null)
            {
                cableMeshFilter =
                    meshRoot.gameObject.AddComponent<MeshFilter>();
            }

            cableMeshRenderer =
                meshRoot.GetComponent<MeshRenderer>();

            if (cableMeshRenderer == null)
            {
                cableMeshRenderer =
                    meshRoot.gameObject.AddComponent<MeshRenderer>();
            }
        }

        if (cableMeshRenderer == null && cableMeshFilter != null)
        {
            cableMeshRenderer =
                cableMeshFilter.GetComponent<MeshRenderer>();
        }
    }

    private void EnsureMesh()
    {
        if (cableMeshFilter == null)
        {
            return;
        }

        if (cableMesh == null)
        {
            cableMesh =
                new Mesh();

            cableMesh.name =
                "MIMISK_TetherCableRuntimeMesh";

            cableMesh.MarkDynamic();

            cableMeshFilter.sharedMesh =
                cableMesh;
        }

        int pointCount =
            Mathf.Max(2, curveSegments + 1);

        int radial =
            Mathf.Max(4, radialSegments);

        if (curvePoints == null || curvePoints.Length != pointCount)
        {
            curvePoints =
                new Vector3[pointCount];
        }

        int vertexCount =
            pointCount * radial;

        int triangleCount =
            (pointCount - 1) * radial * 6;

        if (vertices == null || vertices.Length != vertexCount)
        {
            vertices =
                new Vector3[vertexCount];

            normals =
                new Vector3[vertexCount];

            uvs =
                new Vector2[vertexCount];

            triangles =
                new int[triangleCount];
        }
    }

    private void UpdateMeasurements()
    {
        startWorld =
            tetherManager.tetherStartWorld;

        endWorld =
            tetherManager.tetherEndWorld;

        deployedLengthM =
            Mathf.Max(0.001f, tetherManager.deployedLengthM);

        straightDistanceM =
            Mathf.Max(0.001f, Vector3.Distance(startWorld, endWorld));

        slackM =
            Mathf.Max(
                tetherManager.slackM,
                deployedLengthM - straightDistanceM
            );

        stretchM =
            Mathf.Max(
                tetherManager.stretchM,
                straightDistanceM - deployedLengthM
            );

        tensionN =
            Mathf.Max(0.0f, tetherManager.tensionN);

        slackRatio =
            slackM / Mathf.Max(0.001f, straightDistanceM);

        stretchRatio =
            stretchM / Mathf.Max(0.001f, deployedLengthM);

        if (stretchM > 0.02f || tensionN > 1.0f)
        {
            cableState = "over_taut";
        }
        else if (slackRatio > 0.12f)
        {
            cableState = "slack";
        }
        else
        {
            cableState = "semi_taut";
        }

        sagAmplitudeM =
            Mathf.Clamp(
                minimumVisualSagM + slackM * slackSagGain,
                0.0f,
                maxSagM
            );

        if (enableCurrentDeflection)
        {
            Vector3 currentDir =
                currentDirectionWorld;

            currentDir.y = 0.0f;

            if (currentDir.sqrMagnitude > 0.0001f)
            {
                currentDir.Normalize();
            }

            currentDeflectionM =
                Mathf.Clamp(
                    slackM * currentDeflectionGain,
                    0.0f,
                    maxSagM * 0.75f
                );
        }
        else
        {
            currentDeflectionM = 0.0f;
        }
    }

    private void BuildCurve()
    {
        int n =
            curvePoints.Length;

        Vector3 chord =
            endWorld - startWorld;

        Vector3 horizontal =
            new Vector3(chord.x, 0.0f, chord.z);

        Vector3 side =
            Vector3.zero;

        if (horizontal.sqrMagnitude > 0.0001f)
        {
            side =
                Vector3.Cross(Vector3.up, horizontal.normalized);
        }
        else
        {
            side =
                Vector3.right;
        }

        Vector3 currentDir =
            currentDirectionWorld;

        currentDir.y = 0.0f;

        if (currentDir.sqrMagnitude > 0.0001f)
        {
            currentDir.Normalize();
        }
        else
        {
            currentDir = side;
        }

        float time =
            Application.isPlaying
                ? Time.time
                : 0.0f;

        for (int i = 0; i < n; i++)
        {
            float u =
                n <= 1
                    ? 1.0f
                    : (float)i / (float)(n - 1);

            Vector3 p =
                Vector3.Lerp(startWorld, endWorld, u);

            float shape =
                Mathf.Sin(Mathf.PI * u);

            // Sag is largest at the midpoint.
            p +=
                Vector3.down *
                sagAmplitudeM *
                shape;

            // Current deflection bends the cable sideways only when there is slack.
            p +=
                currentDir *
                currentDeflectionM *
                shape;

            if (enableSmallWaveMotion)
            {
                float wave =
                    Mathf.Sin(
                        2.0f * Mathf.PI *
                        (u * waveSpatialFrequency + time * waveTemporalFrequency)
                    );

                p +=
                    side *
                    wave *
                    waveAmplitudeM *
                    shape *
                    Mathf.Clamp01(0.2f + slackRatio * 4.0f);
            }

            curvePoints[i] =
                p;
        }

        curveLengthM =
            0.0f;

        for (int i = 1; i < n; i++)
        {
            curveLengthM +=
                Vector3.Distance(curvePoints[i - 1], curvePoints[i]);
        }
    }

    private void UpdateLineRenderer()
    {
        if (!driveDebugLineRenderer || debugLineRenderer == null)
        {
            return;
        }

        debugLineRenderer.positionCount =
            curvePoints.Length;

        for (int i = 0; i < curvePoints.Length; i++)
        {
            debugLineRenderer.SetPosition(i, curvePoints[i]);
        }

        debugLineRenderer.widthMultiplier =
            Mathf.Max(0.002f, cableRadiusM * 2.0f);

        debugLineRenderer.useWorldSpace =
            true;
    }

    private void UpdateTubeMesh()
    {
        if (cableMesh == null ||
            vertices == null ||
            curvePoints == null)
        {
            return;
        }

        int pointCount =
            curvePoints.Length;

        int radial =
            Mathf.Max(4, radialSegments);

        float cumulativeLength =
            0.0f;

        int triIndex =
            0;

        for (int i = 0; i < pointCount; i++)
        {
            Vector3 tangent;

            if (i == 0)
            {
                tangent =
                    (curvePoints[1] - curvePoints[0]).normalized;
            }
            else if (i == pointCount - 1)
            {
                tangent =
                    (curvePoints[i] - curvePoints[i - 1]).normalized;
            }
            else
            {
                tangent =
                    (curvePoints[i + 1] - curvePoints[i - 1]).normalized;
            }

            if (tangent.sqrMagnitude < 0.0001f)
            {
                tangent = Vector3.forward;
            }

            Vector3 binormal =
                Vector3.Cross(tangent, Vector3.up);

            if (binormal.sqrMagnitude < 0.0001f)
            {
                binormal =
                    Vector3.Cross(tangent, Vector3.right);
            }

            binormal.Normalize();

            Vector3 normal =
                Vector3.Cross(binormal, tangent).normalized;

            if (i > 0)
            {
                cumulativeLength +=
                    Vector3.Distance(curvePoints[i - 1], curvePoints[i]);
            }

            for (int j = 0; j < radial; j++)
            {
                float a =
                    2.0f * Mathf.PI * (float)j / (float)radial;

                Vector3 radialDir =
                    Mathf.Cos(a) * normal +
                    Mathf.Sin(a) * binormal;

                int index =
                    i * radial + j;

                vertices[index] =
                    transform.InverseTransformPoint(
                        curvePoints[i] + radialDir * cableRadiusM
                    );

                normals[index] =
                    transform.InverseTransformDirection(radialDir);

                uvs[index] =
                    new Vector2(
                        cumulativeLength / Mathf.Max(0.001f, cableRadiusM * 10.0f),
                        (float)j / (float)radial
                    );
            }
        }

        for (int i = 0; i < pointCount - 1; i++)
        {
            for (int j = 0; j < radial; j++)
            {
                int jNext =
                    (j + 1) % radial;

                int a =
                    i * radial + j;

                int b =
                    i * radial + jNext;

                int c =
                    (i + 1) * radial + j;

                int d =
                    (i + 1) * radial + jNext;

                triangles[triIndex++] = a;
                triangles[triIndex++] = c;
                triangles[triIndex++] = b;

                triangles[triIndex++] = b;
                triangles[triIndex++] = c;
                triangles[triIndex++] = d;
            }
        }

        cableMesh.Clear();
        cableMesh.vertices = vertices;
        cableMesh.normals = normals;
        cableMesh.uv = uvs;
        cableMesh.triangles = triangles;
        cableMesh.RecalculateBounds();
    }

    private void UpdateMaterialState()
    {
        if (!colorByTetherState || cableMeshRenderer == null)
        {
            return;
        }

        Material mat =
            cableMeshRenderer.material;

        if (mat == null)
        {
            return;
        }

        Color c =
            semiTautColor;

        if (cableState == "slack")
        {
            c = slackColor;
        }
        else if (cableState == "over_taut")
        {
            c = overTautColor;
        }

        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", c);
        }

        if (mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", c);
        }
    }

    private Transform FindDeepChild(Transform root, string childName)
    {
        if (root == null || string.IsNullOrEmpty(childName))
        {
            return null;
        }

        if (root.name == childName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found =
                FindDeepChild(root.GetChild(i), childName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
