using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class KelpFarmInspectionSceneGenerator
{
    private const float FarmLength = 24.0f;
    private const float FarmWidth = 16.0f;
    private const float WaterLevel = 0.0f;
    private const float SeabedY = -8.0f;
    private const float LonglineY = -2.2f;
    private const int RowCount = 5;
    private const float RowSpacing = 3.0f;

    [MenuItem("MIMISK/Environment/Create Kelp Farm Inspection Scene")]
    public static void CreateKelpFarmInspectionScene()
    {
        GameObject rootOld = GameObject.Find("KelpFarm_Environment");
        if (rootOld != null)
        {
            Object.DestroyImmediate(rootOld);
        }

        GameObject root = new GameObject("KelpFarm_Environment");

        ConfigureOceanSurface();
        CreateSeabed(root.transform);
        CreateKelpRows(root.transform);
        CreateInspectionTargets(root.transform);
        CreateDebrisAndObstacles(root.transform);
        CreateWaypoints(root.transform);
        PositionMiniROV();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Kelp farm inspection environment created.");
    }

    private static void ConfigureOceanSurface()
    {
        GameObject water = GameObject.Find("Ocean_Surface");
        if (water == null)
        {
            water = GameObject.Find("Pool");
        }

        if (water == null)
        {
            Debug.LogWarning("[MIMISK] No Ocean_Surface or Pool object found. Create HDRP water manually if needed.");
            return;
        }

        water.name = "Ocean_Surface";
        water.transform.position = new Vector3(0f, WaterLevel, 0f);
        water.transform.localScale = new Vector3(80f, 1f, 80f);

        BoxCollider col = water.GetComponent<BoxCollider>();
        if (col == null)
        {
            col = water.AddComponent<BoxCollider>();
        }

        col.isTrigger = true;
        col.center = new Vector3(0f, -20f, 0f);
        col.size = new Vector3(1f, 40f, 1f);

        try
        {
            water.tag = "Water Volume";
        }
        catch
        {
            Debug.LogWarning("[MIMISK] Tag 'Water Volume' missing. Create it manually and assign to Ocean_Surface.");
        }
    }

    private static void CreateSeabed(Transform parent)
    {
        GameObject seabed = GameObject.CreatePrimitive(PrimitiveType.Plane);
        seabed.name = "KelpFarm_SandySeabed";
        seabed.transform.SetParent(parent);
        seabed.transform.position = new Vector3(0f, SeabedY, 0f);
        seabed.transform.localScale = new Vector3(8f, 1f, 8f);

        MeshRenderer renderer = seabed.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = GetOrCreateSandMaterial();

        MeshCollider collider = seabed.GetComponent<MeshCollider>();
        if (collider != null)
        {
            collider.convex = false;
        }
    }

    private static void CreateKelpRows(Transform parent)
    {
        GameObject rowsRoot = new GameObject("KelpRows");
        rowsRoot.transform.SetParent(parent);

        Material ropeMat = GetOrCreateMaterial(
            "Assets/MIMISK/Materials/KelpFarm_Rope_Mat.mat",
            new Color(0.05f, 0.04f, 0.03f, 1f),
            0.35f
        );

        Material kelpMat = GetOrCreateMaterial(
            "Assets/MIMISK/Materials/KelpFarm_Kelp_Mat.mat",
            new Color(0.10f, 0.32f, 0.10f, 1f),
            0.45f
        );

        Material floatMat = GetOrCreateMaterial(
            "Assets/MIMISK/Materials/KelpFarm_Float_Mat.mat",
            new Color(1.0f, 0.65f, 0.05f, 1f),
            0.35f
        );

        Material anchorMat = GetOrCreateMaterial(
            "Assets/MIMISK/Materials/KelpFarm_Anchor_Mat.mat",
            new Color(0.25f, 0.25f, 0.25f, 1f),
            0.45f
        );

        float startZ = -((RowCount - 1) * RowSpacing) * 0.5f;

        for (int r = 0; r < RowCount; r++)
        {
            float z = startZ + r * RowSpacing;

            GameObject row = new GameObject("KelpRow_" + r.ToString("00"));
            row.transform.SetParent(rowsRoot.transform);

            Vector3 ropeStart = new Vector3(-FarmLength * 0.5f, LonglineY, z);
            Vector3 ropeEnd = new Vector3(FarmLength * 0.5f, LonglineY, z);

            CreateCylinderBetween("Longline_Rope", ropeStart, ropeEnd, 0.025f, ropeMat, row.transform);

            CreateAnchorAndFloat(ropeStart, row.transform, anchorMat, floatMat);
            CreateAnchorAndFloat(ropeEnd, row.transform, anchorMat, floatMat);

            for (float x = -FarmLength * 0.45f; x <= FarmLength * 0.45f; x += 0.8f)
            {
                Vector3 kelpRoot = new Vector3(x, LonglineY - 0.1f, z);
                CreateKelpPlant(kelpRoot, row.transform, kelpMat);
            }
        }
    }

    private static void CreateAnchorAndFloat(Vector3 ropeEnd, Transform parent, Material anchorMat, Material floatMat)
    {
        Vector3 anchorPos = new Vector3(ropeEnd.x, SeabedY + 0.15f, ropeEnd.z);
        Vector3 floatPos = new Vector3(ropeEnd.x, -0.25f, ropeEnd.z);

        CreateCylinderBetween("Anchor_Line", anchorPos, ropeEnd, 0.01f, anchorMat, parent);

        GameObject anchor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        anchor.name = "Anchor_Block";
        anchor.transform.SetParent(parent);
        anchor.transform.position = anchorPos;
        anchor.transform.localScale = new Vector3(0.35f, 0.25f, 0.35f);
        anchor.GetComponent<MeshRenderer>().sharedMaterial = anchorMat;

        GameObject buoy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        buoy.name = "Surface_Float";
        buoy.transform.SetParent(parent);
        buoy.transform.position = floatPos;
        buoy.transform.localScale = new Vector3(0.35f, 0.22f, 0.35f);
        buoy.GetComponent<MeshRenderer>().sharedMaterial = floatMat;
    }

    private static void CreateKelpPlant(Vector3 rootPos, Transform parent, Material kelpMat)
    {
        int strandCount = Random.Range(3, 7);

        for (int i = 0; i < strandCount; i++)
        {
            float xOffset = Random.Range(-0.08f, 0.08f);
            float zOffset = Random.Range(-0.08f, 0.08f);
            float length = Random.Range(2.0f, 4.8f);

            Vector3 top = rootPos + new Vector3(xOffset, 0f, zOffset);
            Vector3 bottom = new Vector3(
                top.x + Random.Range(-0.15f, 0.15f),
                Mathf.Max(SeabedY + 0.3f, top.y - length),
                top.z + Random.Range(-0.15f, 0.15f)
            );

            GameObject strand = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            strand.name = "Kelp_Stipe";
            strand.transform.SetParent(parent);
            PlaceCylinderBetween(strand, top, bottom, 0.012f);
            strand.GetComponent<MeshRenderer>().sharedMaterial = kelpMat;

            // Make fronds as thin flattened cubes. They are visual only.
            int fronds = Random.Range(2, 5);
            for (int f = 0; f < fronds; f++)
            {
                float t = Random.Range(0.2f, 0.9f);
                Vector3 p = Vector3.Lerp(top, bottom, t);

                GameObject frond = GameObject.CreatePrimitive(PrimitiveType.Cube);
                frond.name = "Kelp_Frond";
                frond.transform.SetParent(parent);
                frond.transform.position = p;
                frond.transform.localScale = new Vector3(
                    Random.Range(0.05f, 0.10f),
                    Random.Range(0.25f, 0.60f),
                    0.006f
                );
                frond.transform.rotation = Quaternion.Euler(
                    Random.Range(-20f, 20f),
                    Random.Range(0f, 360f),
                    Random.Range(-20f, 20f)
                );
                frond.GetComponent<MeshRenderer>().sharedMaterial = kelpMat;

                Collider col = frond.GetComponent<Collider>();
                if (col != null)
                {
                    Object.DestroyImmediate(col);
                }
            }
        }
    }

    private static void CreateInspectionTargets(Transform parent)
    {
        GameObject targets = new GameObject("InspectionTargets");
        targets.transform.SetParent(parent);

        Material panelMat = GetOrCreateMaterial(
            "Assets/MIMISK/Materials/Inspection_Target_Mat.mat",
            new Color(0.9f, 0.9f, 0.15f, 1f),
            0.3f
        );

        for (int i = 0; i < 6; i++)
        {
            float x = -10f + i * 4f;
            float z = Random.Range(-5f, 5f);

            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "InspectionPanel_" + i.ToString("00");
            panel.transform.SetParent(targets.transform);
            panel.transform.position = new Vector3(x, -3.5f, z);
            panel.transform.localScale = new Vector3(0.05f, 0.7f, 0.7f);
            panel.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            panel.GetComponent<MeshRenderer>().sharedMaterial = panelMat;
        }
    }

    private static void CreateDebrisAndObstacles(Transform parent)
    {
        GameObject obstacles = new GameObject("Obstacles_And_Debris");
        obstacles.transform.SetParent(parent);

        Material rockMat = GetOrCreateMaterial(
            "Assets/MIMISK/Materials/KelpFarm_Rock_Mat.mat",
            new Color(0.25f, 0.25f, 0.22f, 1f),
            0.4f
        );

        Material debrisMat = GetOrCreateMaterial(
            "Assets/MIMISK/Materials/KelpFarm_Debris_Mat.mat",
            new Color(0.05f, 0.05f, 0.08f, 1f),
            0.5f
        );

        for (int i = 0; i < 25; i++)
        {
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Rock_" + i.ToString("00");
            rock.transform.SetParent(obstacles.transform);
            rock.transform.position = new Vector3(
                Random.Range(-18f, 18f),
                SeabedY + Random.Range(0.05f, 0.25f),
                Random.Range(-9f, 9f)
            );
            rock.transform.localScale = new Vector3(
                Random.Range(0.25f, 1.2f),
                Random.Range(0.10f, 0.45f),
                Random.Range(0.25f, 1.2f)
            );
            rock.GetComponent<MeshRenderer>().sharedMaterial = rockMat;
        }

        for (int i = 0; i < 8; i++)
        {
            GameObject debris = GameObject.CreatePrimitive(PrimitiveType.Cube);
            debris.name = "MarineDebris_" + i.ToString("00");
            debris.transform.SetParent(obstacles.transform);
            debris.transform.position = new Vector3(
                Random.Range(-15f, 15f),
                SeabedY + 0.05f,
                Random.Range(-7f, 7f)
            );
            debris.transform.localScale = new Vector3(
                Random.Range(0.15f, 0.45f),
                Random.Range(0.04f, 0.12f),
                Random.Range(0.15f, 0.7f)
            );
            debris.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            debris.GetComponent<MeshRenderer>().sharedMaterial = debrisMat;
        }
    }

    private static void CreateWaypoints(Transform parent)
    {
        GameObject waypoints = new GameObject("Autonomy_Waypoints");
        waypoints.transform.SetParent(parent);

        Material waypointMat = GetOrCreateMaterial(
            "Assets/MIMISK/Materials/Waypoint_Mat.mat",
            new Color(0.0f, 0.8f, 1.0f, 0.8f),
            0.2f
        );

        Vector3[] pts = new Vector3[]
        {
            new Vector3(-10f, -4f, -5.5f),
            new Vector3(-5f, -4f, -2.5f),
            new Vector3(0f, -4f, 0f),
            new Vector3(5f, -4f, 2.5f),
            new Vector3(10f, -4f, 5.5f),
        };

        for (int i = 0; i < pts.Length; i++)
        {
            GameObject wp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            wp.name = "Waypoint_" + i.ToString("00");
            wp.transform.SetParent(waypoints.transform);
            wp.transform.position = pts[i];
            wp.transform.localScale = Vector3.one * 0.25f;
            wp.GetComponent<MeshRenderer>().sharedMaterial = waypointMat;

            Collider col = wp.GetComponent<Collider>();
            if (col != null)
            {
                Object.DestroyImmediate(col);
            }
        }
    }

    private static void PositionMiniROV()
    {
        GameObject rov = GameObject.Find("MiniROV");
        if (rov == null)
        {
            return;
        }

        rov.transform.position = new Vector3(0f, -4f, -10f);
        rov.transform.rotation = Quaternion.identity;
    }

    private static void CreateCylinderBetween(string name, Vector3 start, Vector3 end, float radius, Material mat, Transform parent)
    {
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = name;
        cylinder.transform.SetParent(parent);
        PlaceCylinderBetween(cylinder, start, end, radius);
        cylinder.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    private static void PlaceCylinderBetween(GameObject cylinder, Vector3 start, Vector3 end, float radius)
    {
        Vector3 dir = end - start;
        float length = dir.magnitude;

        cylinder.transform.position = (start + end) * 0.5f;
        cylinder.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
        cylinder.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
    }

    private static Material GetOrCreateSandMaterial()
    {
        string materialPath = "Assets/MIMISK/Environment/KelpFarm/KelpFarm_Sand_Mat.mat";
        string texturePath = "Assets/MIMISK/Environment/Seabed/soil_sand_0045_01.jpg";

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        Shader shader = Shader.Find("HDRP/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, materialPath);
        }
        else
        {
            mat.shader = shader;
        }

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (tex != null && mat.HasProperty("_BaseColorMap"))
        {
            mat.SetTexture("_BaseColorMap", tex);
        }

        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", new Color(0.55f, 0.50f, 0.36f, 1f));
        }

        if (mat.HasProperty("_Smoothness"))
        {
            mat.SetFloat("_Smoothness", 0.25f);
        }

        return mat;
    }

    private static Material GetOrCreateMaterial(string path, Color color, float smoothness)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("HDRP/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = shader;
        }

        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }

        if (mat.HasProperty("_Smoothness"))
        {
            mat.SetFloat("_Smoothness", smoothness);
        }

        return mat;
    }
}
