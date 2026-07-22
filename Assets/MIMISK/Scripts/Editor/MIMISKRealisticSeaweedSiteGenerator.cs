using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;

public static class MIMISKRealisticSeaweedSiteGenerator
{
    private const float WaterLevel = 0.0f;
    private const float SeabedBaseY = -9.0f;
    private const float TerrainSize = 90.0f;
    private const int TerrainSubdivisions = 140;

    [MenuItem("MIMISK/Environment/Create Realistic MIMISK Seaweed Site")]
    public static void CreateScene()
    {
        Random.InitState(3600);

        DisableOldGeneratedEnvironments();
        ConfigureOceanSurface();

        GameObject oldRoot = GameObject.Find("MIMISK_RealisticSeaweedSite");
        if (oldRoot != null)
        {
            Object.DestroyImmediate(oldRoot);
        }

        GameObject root = new GameObject("MIMISK_RealisticSeaweedSite");

        CreateSeabed(root.transform);
        CreateRockyOutcrops(root.transform);
        CreateFarmLonglines(root.transform);
        CreateNaturalSeaweedPatches(root.transform);
        CreateInspectionCorridor(root.transform);
        CreateMarineDebris(root.transform);
        CreateWaypoints(root.transform);
        CreateMarineSnow(root.transform);
        PositionMiniROV();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Realistic seaweed monitoring site generated.");
    }

    private static void DisableOldGeneratedEnvironments()
    {
        DisableIfExists("heightmap");
        DisableIfExists("Seabed_Plane");
        DisableIfExists("Pool_Boundaries");
        DisableIfExists("SandFloor_PhysicalPool");
        DisableIfExists("OpenSea_Rocks");
        DisableIfExists("KelpFarm_Environment");
        DisableIfExists("NaturalKelpForest_Environment");
    }

    private static void DisableIfExists(string name)
    {
        GameObject obj = GameObject.Find(name);
        if (obj != null)
        {
            obj.SetActive(false);
        }
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
            Debug.LogWarning("[MIMISK] No Ocean_Surface or Pool found. Create HDRP water first.");
            return;
        }

        water.name = "Ocean_Surface";
        water.transform.position = new Vector3(0f, WaterLevel, 0f);
        water.transform.rotation = Quaternion.identity;
        water.transform.localScale = new Vector3(140f, 1f, 140f);

        BoxCollider col = water.GetComponent<BoxCollider>();
        if (col == null)
        {
            col = water.AddComponent<BoxCollider>();
        }

        col.isTrigger = true;
        col.center = new Vector3(0f, -25f, 0f);
        col.size = new Vector3(1f, 50f, 1f);

        try
        {
            water.tag = "Water Volume";
        }
        catch
        {
            Debug.LogWarning("[MIMISK] Tag 'Water Volume' missing. Add it manually and assign it to Ocean_Surface.");
        }
    }

    private static void CreateSeabed(Transform parent)
    {
        int vertsPerSide = TerrainSubdivisions + 1;

        Vector3[] vertices = new Vector3[vertsPerSide * vertsPerSide];
        Vector2[] uvs = new Vector2[vertsPerSide * vertsPerSide];
        int[] triangles = new int[TerrainSubdivisions * TerrainSubdivisions * 6];

        for (int z = 0; z < vertsPerSide; z++)
        {
            for (int x = 0; x < vertsPerSide; x++)
            {
                float u = (float)x / TerrainSubdivisions;
                float v = (float)z / TerrainSubdivisions;

                float wx = (u - 0.5f) * TerrainSize;
                float wz = (v - 0.5f) * TerrainSize;
                float y = SeabedHeight(wx, wz);

                int index = z * vertsPerSide + x;

                vertices[index] = new Vector3(wx, y, wz);
                uvs[index] = new Vector2(u * 26f, v * 26f);
            }
        }

        int t = 0;

        for (int z = 0; z < TerrainSubdivisions; z++)
        {
            for (int x = 0; x < TerrainSubdivisions; x++)
            {
                int i = z * vertsPerSide + x;

                triangles[t++] = i;
                triangles[t++] = i + vertsPerSide;
                triangles[t++] = i + 1;

                triangles[t++] = i + 1;
                triangles[t++] = i + vertsPerSide;
                triangles[t++] = i + vertsPerSide + 1;
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = "MIMISK_SeaweedSite_Seabed_Mesh";
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject seabed = new GameObject("MIMISK_SandyRockySeabed");
        seabed.transform.SetParent(parent);

        MeshFilter mf = seabed.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        MeshRenderer mr = seabed.AddComponent<MeshRenderer>();
        mr.sharedMaterial = GetSandMaterial();

        MeshCollider mc = seabed.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;
    }

    private static float SeabedHeight(float x, float z)
    {
        float large = Mathf.PerlinNoise(x * 0.035f + 10.0f, z * 0.035f + 25.0f) * 2.0f;
        float medium = Mathf.PerlinNoise(x * 0.12f + 31.0f, z * 0.12f + 14.0f) * 0.45f;
        float ripples = Mathf.Sin(x * 1.1f + z * 0.35f) * 0.035f + Mathf.Sin(z * 1.7f) * 0.025f;

        return SeabedBaseY + large + medium + ripples;
    }

    private static void CreateRockyOutcrops(Transform parent)
    {
        GameObject root = new GameObject("RockyOutcrops_And_Holdfasts");
        root.transform.SetParent(parent);

        Material rockMat = GetMaterial("Assets/MIMISK/Materials/MIMISK_Rock_Mat.mat", new Color(0.23f, 0.24f, 0.22f, 1f), 0.45f);

        for (int i = 0; i < 90; i++)
        {
            float x = Random.Range(-42f, 42f);
            float z = Random.Range(-42f, 42f);
            float y = SeabedHeight(x, z) + 0.08f;

            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Rock_" + i.ToString("00");
            rock.transform.SetParent(root.transform);
            rock.transform.position = new Vector3(x, y, z);
            rock.transform.rotation = Quaternion.Euler(Random.Range(-12f, 12f), Random.Range(0f, 360f), Random.Range(-12f, 12f));
            rock.transform.localScale = new Vector3(Random.Range(0.25f, 1.8f), Random.Range(0.08f, 0.7f), Random.Range(0.25f, 1.8f));

            rock.GetComponent<MeshRenderer>().sharedMaterial = rockMat;
        }
    }

    private static void CreateFarmLonglines(Transform parent)
    {
        GameObject root = new GameObject("StructuredSeaweedFarm_Longlines");
        root.transform.SetParent(parent);

        Material ropeMat = GetMaterial("Assets/MIMISK/Materials/MIMISK_Rope_Mat.mat", new Color(0.03f, 0.025f, 0.02f, 1f), 0.35f);
        Material floatMat = GetMaterial("Assets/MIMISK/Materials/MIMISK_Float_Mat.mat", new Color(0.95f, 0.55f, 0.08f, 1f), 0.32f);
        Material anchorMat = GetMaterial("Assets/MIMISK/Materials/MIMISK_Anchor_Mat.mat", new Color(0.18f, 0.18f, 0.17f, 1f), 0.45f);
        Material kelpMat = GetKelpMaterial();

        float[] rowX = new float[] { -20f, -16f, -12f };

        foreach (float x in rowX)
        {
            GameObject row = new GameObject("Farm_Longline_X_" + x.ToString("0"));
            row.transform.SetParent(root.transform);

            Vector3 start = new Vector3(x, -2.0f, -32f);
            Vector3 end = new Vector3(x, -2.0f, 32f);

            CreateCylinderBetween("Longline_Rope", start, end, 0.025f, ropeMat, row.transform);

            CreateAnchorFloatSystem(start, row.transform, anchorMat, floatMat, ropeMat);
            CreateAnchorFloatSystem(end, row.transform, anchorMat, floatMat, ropeMat);

            for (float z = -29f; z <= 29f; z += 1.0f)
            {
                Vector3 attach = new Vector3(x, -2.05f, z);
                CreateHangingSeaweedCurtain(attach, row.transform, kelpMat);
            }
        }
    }

    private static void CreateAnchorFloatSystem(Vector3 ropePoint, Transform parent, Material anchorMat, Material floatMat, Material ropeMat)
    {
        Vector3 anchorPos = new Vector3(ropePoint.x, SeabedHeight(ropePoint.x, ropePoint.z) + 0.12f, ropePoint.z);
        Vector3 floatPos = new Vector3(ropePoint.x, -0.2f, ropePoint.z);

        CreateCylinderBetween("Anchor_Line", anchorPos, ropePoint, 0.01f, ropeMat, parent);
        CreateCylinderBetween("Float_Line", ropePoint, floatPos, 0.008f, ropeMat, parent);

        GameObject anchor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        anchor.name = "Anchor_Block";
        anchor.transform.SetParent(parent);
        anchor.transform.position = anchorPos;
        anchor.transform.localScale = new Vector3(0.45f, 0.25f, 0.45f);
        anchor.GetComponent<MeshRenderer>().sharedMaterial = anchorMat;

        GameObject buoy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        buoy.name = "Surface_Float";
        buoy.transform.SetParent(parent);
        buoy.transform.position = floatPos;
        buoy.transform.localScale = new Vector3(0.45f, 0.25f, 0.45f);
        buoy.GetComponent<MeshRenderer>().sharedMaterial = floatMat;
    }

    private static void CreateHangingSeaweedCurtain(Vector3 attachPoint, Transform parent, Material kelpMat)
    {
        GameObject curtain = new GameObject("HangingSeaweed");
        curtain.transform.SetParent(parent);
        curtain.transform.position = attachPoint;

        MIMISKPlantSway sway = curtain.AddComponent<MIMISKPlantSway>();
        sway.swayAngleDeg = Random.Range(2.5f, 5.5f);
        sway.swaySpeed = Random.Range(0.15f, 0.35f);
        sway.phase = Random.Range(0f, 100f);
        sway.localAxis = Vector3.right;

        int bladeCount = Random.Range(5, 10);

        for (int i = 0; i < bladeCount; i++)
        {
            float length = Random.Range(2.0f, 5.8f);
            float width = Random.Range(0.05f, 0.11f);

            GameObject blade = CreateBladeObject("Seaweed_Blade", length, width, Random.Range(0.08f, 0.22f), kelpMat);
            blade.transform.SetParent(curtain.transform);
            blade.transform.localPosition = new Vector3(Random.Range(-0.12f, 0.12f), 0f, Random.Range(-0.12f, 0.12f));
            blade.transform.localRotation = Quaternion.Euler(Random.Range(-8f, 8f), Random.Range(0f, 360f), Random.Range(-8f, 8f));
        }
    }

    private static void CreateNaturalSeaweedPatches(Transform parent)
    {
        GameObject root = new GameObject("NaturalSeaweedPatches");
        root.transform.SetParent(parent);

        Material kelpMat = GetKelpMaterial();
        Material stalkMat = GetMaterial("Assets/MIMISK/Materials/MIMISK_Stipe_Mat.mat", new Color(0.06f, 0.18f, 0.06f, 1f), 0.4f);

        Vector2[] centers = new Vector2[]
        {
            new Vector2(8f, -24f),
            new Vector2(18f, -15f),
            new Vector2(25f, -2f),
            new Vector2(18f, 14f),
            new Vector2(8f, 22f),
            new Vector2(-4f, 18f)
        };

        int plantIndex = 0;

        foreach (Vector2 c in centers)
        {
            GameObject patch = new GameObject("NaturalPatch_" + plantIndex.ToString("00"));
            patch.transform.SetParent(root.transform);

            int plants = Random.Range(28, 48);

            for (int i = 0; i < plants; i++)
            {
                Vector2 p = c + Random.insideUnitCircle * Random.Range(0.5f, 5.0f);
                CreateRootedSeaweedPlant(p.x, p.y, patch.transform, kelpMat, stalkMat, plantIndex);
                plantIndex++;
            }
        }
    }

    private static void CreateRootedSeaweedPlant(float x, float z, Transform parent, Material kelpMat, Material stalkMat, int id)
    {
        float y = SeabedHeight(x, z);

        GameObject plant = new GameObject("RootedSeaweed_" + id.ToString("000"));
        plant.transform.SetParent(parent);
        plant.transform.position = new Vector3(x, y, z);

        MIMISKPlantSway sway = plant.AddComponent<MIMISKPlantSway>();
        sway.swayAngleDeg = Random.Range(3.0f, 8.0f);
        sway.swaySpeed = Random.Range(0.12f, 0.38f);
        sway.phase = Random.Range(0f, 100f);
        sway.localAxis = Random.value > 0.5f ? Vector3.forward : Vector3.right;

        float stipeHeight = Random.Range(2.5f, 7.5f);
        float topY = Mathf.Min(-0.4f, stipeHeight);

        GameObject stipe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stipe.name = "Flexible_Stipe";
        stipe.transform.SetParent(plant.transform);
        PlaceCylinderBetweenLocal(stipe, Vector3.zero, new Vector3(Random.Range(-0.15f, 0.15f), topY, Random.Range(-0.15f, 0.15f)), Random.Range(0.012f, 0.025f));
        stipe.GetComponent<MeshRenderer>().sharedMaterial = stalkMat;

        Collider stipeCol = stipe.GetComponent<Collider>();
        if (stipeCol != null)
        {
            stipeCol.isTrigger = true;
        }

        int blades = Random.Range(5, 12);

        for (int i = 0; i < blades; i++)
        {
            float bladeLen = Random.Range(0.8f, 2.4f);
            float bladeWidth = Random.Range(0.05f, 0.12f);

            GameObject blade = CreateBladeObject("Kelp_Frond", bladeLen, bladeWidth, Random.Range(0.05f, 0.18f), kelpMat);
            blade.transform.SetParent(plant.transform);
            blade.transform.localPosition = new Vector3(Random.Range(-0.10f, 0.10f), Random.Range(topY * 0.75f, topY * 0.25f), Random.Range(-0.10f, 0.10f));
            blade.transform.localRotation = Quaternion.Euler(Random.Range(50f, 110f), Random.Range(0f, 360f), Random.Range(-30f, 30f));
        }

        GameObject holdfast = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        holdfast.name = "Holdfast";
        holdfast.transform.SetParent(plant.transform);
        holdfast.transform.localPosition = Vector3.zero;
        holdfast.transform.localScale = new Vector3(0.25f, 0.07f, 0.25f);
        holdfast.GetComponent<MeshRenderer>().sharedMaterial = stalkMat;

        Collider holdCol = holdfast.GetComponent<Collider>();
        if (holdCol != null)
        {
            holdCol.isTrigger = true;
        }
    }

    private static GameObject CreateBladeObject(string name, float length, float width, float curve, Material mat)
    {
        GameObject obj = new GameObject(name);

        Mesh mesh = CreateCurvedBladeMesh(length, width, curve);

        MeshFilter mf = obj.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        MeshRenderer mr = obj.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;

        return obj;
    }

    private static Mesh CreateCurvedBladeMesh(float length, float width, float curve)
    {
        int segments = 8;
        Vector3[] verts = new Vector3[(segments + 1) * 2];
        Vector2[] uvs = new Vector2[(segments + 1) * 2];
        int[] tris = new int[segments * 6];

        for (int i = 0; i <= segments; i++)
        {
            float t = (float)i / segments;
            float taper = Mathf.Lerp(1.0f, 0.18f, t);
            float y = -length * t;
            float z = Mathf.Sin(t * Mathf.PI) * curve;

            verts[i * 2] = new Vector3(-width * taper, y, z);
            verts[i * 2 + 1] = new Vector3(width * taper, y, z);

            uvs[i * 2] = new Vector2(0f, t);
            uvs[i * 2 + 1] = new Vector2(1f, t);
        }

        int k = 0;
        for (int i = 0; i < segments; i++)
        {
            int a = i * 2;
            int b = a + 1;
            int c = a + 2;
            int d = a + 3;

            tris[k++] = a;
            tris[k++] = c;
            tris[k++] = b;

            tris[k++] = b;
            tris[k++] = c;
            tris[k++] = d;
        }

        Mesh mesh = new Mesh();
        mesh.name = "CurvedSeaweedBlade";
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        return mesh;
    }

    private static void CreateInspectionCorridor(Transform parent)
    {
        GameObject root = new GameObject("InspectionCorridor");
        root.transform.SetParent(parent);

        Material targetMat = GetMaterial("Assets/MIMISK/Materials/MIMISK_InspectionPanel_Mat.mat", new Color(0.95f, 0.82f, 0.10f, 1f), 0.25f);
        Material pipeMat = GetMaterial("Assets/MIMISK/Materials/MIMISK_Pipe_Mat.mat", new Color(0.08f, 0.08f, 0.07f, 1f), 0.45f);

        for (int i = 0; i < 8; i++)
        {
            float z = -28f + i * 8f;
            float x = Random.value > 0.5f ? -3.5f : 3.5f;
            float y = -4.5f + Random.Range(-0.5f, 0.5f);

            GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = "InspectionPanel_" + i.ToString("00");
            panel.transform.SetParent(root.transform);
            panel.transform.position = new Vector3(x, y, z);
            panel.transform.localScale = new Vector3(0.05f, 0.55f, 0.75f);
            panel.transform.rotation = Quaternion.Euler(0f, x > 0 ? -90f : 90f, 0f);
            panel.GetComponent<MeshRenderer>().sharedMaterial = targetMat;
        }

        for (int i = 0; i < 3; i++)
        {
            Vector3 start = new Vector3(-5f + i * 5f, -7.0f, -30f);
            Vector3 end = new Vector3(-5f + i * 5f, -7.0f, 30f);
            CreateCylinderBetween("Seabed_Reference_Line", start, end, 0.035f, pipeMat, root.transform);
        }
    }

    private static void CreateMarineDebris(Transform parent)
    {
        GameObject root = new GameObject("MarineDebris_And_Obstacles");
        root.transform.SetParent(parent);

        Material debrisMat = GetMaterial("Assets/MIMISK/Materials/MIMISK_Debris_Mat.mat", new Color(0.05f, 0.05f, 0.06f, 1f), 0.35f);

        for (int i = 0; i < 18; i++)
        {
            float x = Random.Range(-30f, 30f);
            float z = Random.Range(-30f, 30f);
            float y = SeabedHeight(x, z) + 0.08f;

            GameObject debris = GameObject.CreatePrimitive(PrimitiveType.Cube);
            debris.name = "MarineDebris_" + i.ToString("00");
            debris.transform.SetParent(root.transform);
            debris.transform.position = new Vector3(x, y, z);
            debris.transform.rotation = Quaternion.Euler(Random.Range(-5f, 5f), Random.Range(0f, 360f), Random.Range(-5f, 5f));
            debris.transform.localScale = new Vector3(Random.Range(0.15f, 0.55f), Random.Range(0.04f, 0.16f), Random.Range(0.2f, 0.85f));
            debris.GetComponent<MeshRenderer>().sharedMaterial = debrisMat;
        }
    }

    private static void CreateWaypoints(Transform parent)
    {
        GameObject root = new GameObject("Autonomy_Waypoints");
        root.transform.SetParent(parent);

        Material mat = GetMaterial("Assets/MIMISK/Materials/MIMISK_Waypoint_Mat.mat", new Color(0.0f, 0.75f, 1.0f, 1f), 0.25f);

        Vector3[] points =
        {
            new Vector3(0f, -5f, -30f),
            new Vector3(0f, -5f, -18f),
            new Vector3(0f, -5f, -6f),
            new Vector3(0f, -5f, 6f),
            new Vector3(0f, -5f, 18f),
            new Vector3(0f, -5f, 30f)
        };

        for (int i = 0; i < points.Length; i++)
        {
            GameObject wp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            wp.name = "Waypoint_" + i.ToString("00");
            wp.transform.SetParent(root.transform);
            wp.transform.position = points[i];
            wp.transform.localScale = Vector3.one * 0.35f;
            wp.GetComponent<MeshRenderer>().sharedMaterial = mat;

            Collider col = wp.GetComponent<Collider>();
            if (col != null)
            {
                Object.DestroyImmediate(col);
            }
        }
    }

    private static void CreateMarineSnow(Transform parent)
    {
        GameObject snow = new GameObject("MarineSnow_Particles");
        snow.transform.SetParent(parent);
        snow.transform.position = new Vector3(0f, -6f, 0f);

        ParticleSystem ps = snow.AddComponent<ParticleSystem>();

        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = 30f;
        main.startSpeed = 0.025f;
        main.startSize = 0.025f;
        main.maxParticles = 1800;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 55f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(80f, 20f, 80f);

        ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.x = new ParticleSystem.MinMaxCurve(-0.005f, 0.01f);
        vel.y = new ParticleSystem.MinMaxCurve(-0.012f, -0.004f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.005f, 0.01f);

        ParticleSystemRenderer renderer = snow.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = GetParticleMaterial();
    }

    private static void PositionMiniROV()
    {
        GameObject rov = GameObject.Find("MiniROV");

        if (rov == null)
        {
            return;
        }

        rov.transform.position = new Vector3(0f, -5f, -34f);
        rov.transform.rotation = Quaternion.identity;
    }

    private static void CreateCylinderBetween(string name, Vector3 start, Vector3 end, float radius, Material mat, Transform parent)
    {
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = name;
        cylinder.transform.SetParent(parent);

        Vector3 dir = end - start;
        float length = dir.magnitude;

        cylinder.transform.position = (start + end) * 0.5f;
        cylinder.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
        cylinder.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);

        cylinder.GetComponent<MeshRenderer>().sharedMaterial = mat;
    }

    private static void PlaceCylinderBetweenLocal(GameObject cylinder, Vector3 start, Vector3 end, float radius)
    {
        Vector3 dir = end - start;
        float length = dir.magnitude;

        cylinder.transform.localPosition = (start + end) * 0.5f;
        cylinder.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
        cylinder.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
    }

    private static Material GetSandMaterial()
    {
        string path = "Assets/MIMISK/Environment/RealisticSeaweedSite/MIMISK_SeaweedSite_Sand.mat";
        string texturePath = "Assets/MIMISK/Environment/Seabed/soil_sand_0045_01.jpg";

        Material mat = LoadOrCreateMat(path, "HDRP/Lit");

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (tex != null)
        {
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.SaveAndReimport();
            }

            if (mat.HasProperty("_BaseColorMap"))
            {
                mat.SetTexture("_BaseColorMap", tex);
            }
        }

        SetColor(mat, new Color(0.48f, 0.45f, 0.31f, 1f));
        SetSmoothness(mat, 0.25f);

        return mat;
    }

    private static Material GetKelpMaterial()
    {
        Material mat = GetMaterial("Assets/MIMISK/Materials/MIMISK_KelpBlade_Mat.mat", new Color(0.08f, 0.32f, 0.08f, 1f), 0.35f);

        if (mat.HasProperty("_DoubleSidedEnable"))
        {
            mat.SetFloat("_DoubleSidedEnable", 1.0f);
        }

        return mat;
    }

    private static Material GetParticleMaterial()
    {
        string path = "Assets/MIMISK/Materials/MIMISK_MarineSnow_Mat.mat";
        Material mat = LoadOrCreateMat(path, "HDRP/Unlit");

        if (mat.HasProperty("_UnlitColor"))
        {
            mat.SetColor("_UnlitColor", new Color(0.85f, 0.95f, 1f, 0.35f));
        }

        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", new Color(0.85f, 0.95f, 1f, 0.35f));
        }

        return mat;
    }

    private static Material GetMaterial(string path, Color color, float smoothness)
    {
        Material mat = LoadOrCreateMat(path, "HDRP/Lit");
        SetColor(mat, color);
        SetSmoothness(mat, smoothness);
        return mat;
    }

    private static Material LoadOrCreateMat(string path, string shaderName)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

        Shader shader = Shader.Find(shaderName);
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = shader;
        }

        return mat;
    }

    private static void SetColor(Material mat, Color color)
    {
        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", color);
        }
        else if (mat.HasProperty("_Color"))
        {
            mat.SetColor("_Color", color);
        }
    }

    private static void SetSmoothness(Material mat, float value)
    {
        if (mat.HasProperty("_Smoothness"))
        {
            mat.SetFloat("_Smoothness", value);
        }
    }
}
