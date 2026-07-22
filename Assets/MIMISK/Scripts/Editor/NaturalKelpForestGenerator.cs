using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;

public static class NaturalKelpForestGenerator
{
    private const float WaterLevel = 0.0f;
    private const float SeabedBaseY = -10.0f;
    private const float TerrainSize = 80.0f;
    private const int TerrainSubdivisions = 150;

    [MenuItem("MIMISK/Environment/Create Natural Kelp Forest")]
    public static void CreateNaturalKelpForest()
    {
        Random.InitState(2026);

        DisableOldEnvironmentObjects();
        ConfigureOceanSurface();

        GameObject oldRoot = GameObject.Find("NaturalKelpForest_Environment");
        if (oldRoot != null)
        {
            Object.DestroyImmediate(oldRoot);
        }

        GameObject root = new GameObject("NaturalKelpForest_Environment");

        CreateSeabed(root.transform);
        CreateRockField(root.transform);
        CreateKelpPatches(root.transform);
        CreateInspectionTargets(root.transform);
        CreateAutonomyWaypoints(root.transform);
        CreateMarineSnow(root.transform);
        PositionMiniROV();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Natural kelp forest scene generated.");
    }

    private static void DisableOldEnvironmentObjects()
    {
        DisableIfExists("heightmap");
        DisableIfExists("Seabed_Plane");
        DisableIfExists("Pool_Boundaries");
        DisableIfExists("SandFloor_PhysicalPool");
        DisableIfExists("KelpFarm_Environment");
        DisableIfExists("OpenSea_Rocks");
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
            Debug.LogWarning("[MIMISK] No Ocean_Surface or Pool found. Create HDRP water manually if needed.");
            return;
        }

        water.name = "Ocean_Surface";
        water.transform.position = new Vector3(0f, WaterLevel, 0f);
        water.transform.rotation = Quaternion.identity;
        water.transform.localScale = new Vector3(120f, 1f, 120f);

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
            Debug.LogWarning("[MIMISK] Tag 'Water Volume' missing. Create it and assign to Ocean_Surface.");
        }
    }

    private static void CreateSeabed(Transform parent)
    {
        GameObject existing = GameObject.Find("NaturalKelpForest_Seabed");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

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

                float y = GetSeabedHeight(wx, wz);

                int index = z * vertsPerSide + x;
                vertices[index] = new Vector3(wx, y, wz);
                uvs[index] = new Vector2(u * 24f, v * 24f);
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
        mesh.name = "NaturalKelpForest_Seabed_Mesh";
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject seabed = new GameObject("NaturalKelpForest_Seabed");
        seabed.transform.SetParent(parent);

        MeshFilter mf = seabed.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        MeshRenderer mr = seabed.AddComponent<MeshRenderer>();
        mr.sharedMaterial = GetOrCreateSandMaterial();

        MeshCollider mc = seabed.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;
    }

    private static float GetSeabedHeight(float x, float z)
    {
        float large =
            Mathf.PerlinNoise(x * 0.035f + 12.0f, z * 0.035f + 9.0f) * 2.2f;

        float medium =
            Mathf.PerlinNoise(x * 0.12f + 30.0f, z * 0.12f + 40.0f) * 0.45f;

        float ripples =
            Mathf.Sin(x * 1.2f + z * 0.25f) * 0.04f +
            Mathf.Sin(z * 1.5f) * 0.03f;

        return SeabedBaseY + large + medium + ripples;
    }

    private static void CreateRockField(Transform parent)
    {
        GameObject root = new GameObject("Rocks_And_Holdfasts");
        root.transform.SetParent(parent);

        Material rockMat = GetOrCreateMaterial(
            "Assets/MIMISK/Materials/NaturalKelp_Rock_Mat.mat",
            new Color(0.22f, 0.23f, 0.21f, 1f),
            0.45f
        );

        for (int i = 0; i < 70; i++)
        {
            float x = Random.Range(-36f, 36f);
            float z = Random.Range(-36f, 36f);
            float y = GetSeabedHeight(x, z) + 0.08f;

            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Rock_" + i.ToString("00");
            rock.transform.SetParent(root.transform);
            rock.transform.position = new Vector3(x, y, z);
            rock.transform.rotation = Quaternion.Euler(
                Random.Range(-10f, 10f),
                Random.Range(0f, 360f),
                Random.Range(-10f, 10f)
            );

            rock.transform.localScale = new Vector3(
                Random.Range(0.25f, 1.4f),
                Random.Range(0.08f, 0.55f),
                Random.Range(0.25f, 1.4f)
            );

            rock.GetComponent<MeshRenderer>().sharedMaterial = rockMat;
        }
    }

    private static void CreateKelpPatches(Transform parent)
    {
        GameObject root = new GameObject("KelpPatches");
        root.transform.SetParent(parent);

        Material stalkMat = GetOrCreateMaterial(
            "Assets/MIMISK/Materials/NaturalKelp_Stalk_Mat.mat",
            new Color(0.07f, 0.24f, 0.08f, 1f),
            0.45f
        );

        Material frondMat = GetOrCreateMaterial(
            "Assets/MIMISK/Materials/NaturalKelp_Frond_Mat.mat",
            new Color(0.11f, 0.38f, 0.12f, 1f),
            0.35f
        );

        Vector2[] patchCenters = new Vector2[]
        {
            new Vector2(-18f, -14f),
            new Vector2(-8f, -10f),
            new Vector2(8f, -12f),
            new Vector2(18f, -6f),
            new Vector2(-20f, 6f),
            new Vector2(-5f, 5f),
            new Vector2(10f, 8f),
            new Vector2(24f, 14f)
        };

        int plantIndex = 0;

        foreach (Vector2 center in patchCenters)
        {
            GameObject patch = new GameObject("KelpPatch_" + plantIndex.ToString("00"));
            patch.transform.SetParent(root.transform);

            int plantsInPatch = Random.Range(18, 34);

            for (int i = 0; i < plantsInPatch; i++)
            {
                Vector2 p = center + Random.insideUnitCircle * Random.Range(0.5f, 4.0f);

                CreateKelpPlant(
                    new Vector3(p.x, GetSeabedHeight(p.x, p.y), p.y),
                    patch.transform,
                    stalkMat,
                    frondMat,
                    plantIndex
                );

                plantIndex++;
            }
        }
    }

    private static void CreateKelpPlant(
        Vector3 seabedPoint,
        Transform parent,
        Material stalkMat,
        Material frondMat,
        int index)
    {
        GameObject plant = new GameObject("KelpPlant_" + index.ToString("000"));
        plant.transform.SetParent(parent);
        plant.transform.position = seabedPoint;

        KelpSway sway = plant.AddComponent<KelpSway>();
        sway.swayAngleDeg = Random.Range(3f, 9f);
        sway.swaySpeed = Random.Range(0.18f, 0.45f);
        sway.phase = Random.Range(0f, 100f);
        sway.localSwayAxis = Random.value > 0.5f ? Vector3.forward : Vector3.right;

        float height = Random.Range(3.5f, 8.5f);
        float topY = Mathf.Min(-0.5f, seabedPoint.y + height);
        Vector3 top = new Vector3(
            Random.Range(-0.25f, 0.25f),
            topY - seabedPoint.y,
            Random.Range(-0.25f, 0.25f)
        );

        GameObject stalk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stalk.name = "Kelp_Stalk";
        stalk.transform.SetParent(plant.transform);
        PlaceCylinderBetweenLocal(stalk, Vector3.zero, top, Random.Range(0.012f, 0.025f));
        stalk.GetComponent<MeshRenderer>().sharedMaterial = stalkMat;

        Collider stalkCollider = stalk.GetComponent<Collider>();
        if (stalkCollider != null)
        {
            stalkCollider.isTrigger = true;
        }

        int frondCount = Random.Range(4, 9);

        for (int i = 0; i < frondCount; i++)
        {
            float t = Random.Range(0.2f, 0.95f);
            Vector3 pos = Vector3.Lerp(Vector3.zero, top, t);

            GameObject frond = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frond.name = "Kelp_Frond";
            frond.transform.SetParent(plant.transform);
            frond.transform.localPosition = pos;

            frond.transform.localScale = new Vector3(
                Random.Range(0.04f, 0.09f),
                Random.Range(0.35f, 1.0f),
                0.008f
            );

            frond.transform.localRotation = Quaternion.Euler(
                Random.Range(-35f, 35f),
                Random.Range(0f, 360f),
                Random.Range(-35f, 35f)
            );

            frond.GetComponent<MeshRenderer>().sharedMaterial = frondMat;

            Collider col = frond.GetComponent<Collider>();
            if (col != null)
            {
                Object.DestroyImmediate(col);
            }
        }

        // Holdfast-like base
        GameObject holdfast = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        holdfast.name = "Holdfast";
        holdfast.transform.SetParent(plant.transform);
        holdfast.transform.localPosition = Vector3.zero;
        holdfast.transform.localScale = new Vector3(0.18f, 0.06f, 0.18f);
        holdfast.GetComponent<MeshRenderer>().sharedMaterial = stalkMat;

        Collider holdfastCollider = holdfast.GetComponent<Collider>();
        if (holdfastCollider != null)
        {
            holdfastCollider.isTrigger = true;
        }
    }

    private static void PlaceCylinderBetweenLocal(GameObject cylinder, Vector3 start, Vector3 end, float radius)
    {
        Vector3 dir = end - start;
        float length = dir.magnitude;

        cylinder.transform.localPosition = (start + end) * 0.5f;
        cylinder.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
        cylinder.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
    }

    private static void CreateInspectionTargets(Transform parent)
    {
        GameObject root = new GameObject("InspectionTargets");
        root.transform.SetParent(parent);

        Material targetMat = GetOrCreateMaterial(
            "Assets/MIMISK/Materials/NaturalKelp_InspectionTarget_Mat.mat",
            new Color(0.95f, 0.85f, 0.10f, 1f),
            0.25f
        );

        for (int i = 0; i < 8; i++)
        {
            float x = Random.Range(-25f, 25f);
            float z = Random.Range(-25f, 25f);
            float y = GetSeabedHeight(x, z) + Random.Range(1.0f, 3.0f);

            GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = "InspectionPanel_" + i.ToString("00");
            target.transform.SetParent(root.transform);
            target.transform.position = new Vector3(x, y, z);
            target.transform.localScale = new Vector3(0.05f, 0.6f, 0.6f);
            target.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            target.GetComponent<MeshRenderer>().sharedMaterial = targetMat;
        }
    }

    private static void CreateAutonomyWaypoints(Transform parent)
    {
        GameObject root = new GameObject("Autonomy_Waypoints");
        root.transform.SetParent(parent);

        Material waypointMat = GetOrCreateMaterial(
            "Assets/MIMISK/Materials/NaturalKelp_Waypoint_Mat.mat",
            new Color(0f, 0.8f, 1f, 0.8f),
            0.2f
        );

        Vector3[] points =
        {
            new Vector3(-25f, -5f, -25f),
            new Vector3(-15f, -5f, -12f),
            new Vector3(0f, -5f, -5f),
            new Vector3(12f, -5f, 5f),
            new Vector3(22f, -5f, 18f)
        };

        for (int i = 0; i < points.Length; i++)
        {
            GameObject wp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            wp.name = "Waypoint_" + i.ToString("00");
            wp.transform.SetParent(root.transform);
            wp.transform.position = points[i];
            wp.transform.localScale = Vector3.one * 0.35f;
            wp.GetComponent<MeshRenderer>().sharedMaterial = waypointMat;

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
        snow.transform.position = new Vector3(0f, -5f, 0f);

        ParticleSystem ps = snow.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ps.main;
        main.startLifetime = 25f;
        main.startSpeed = 0.04f;
        main.startSize = 0.035f;
        main.maxParticles = 1200;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.rateOverTime = 45f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(70f, 18f, 70f);

        ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.y = new ParticleSystem.MinMaxCurve(-0.015f, -0.005f);

        ParticleSystemRenderer renderer = snow.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = GetOrCreateParticleMaterial();
    }

    private static void PositionMiniROV()
    {
        GameObject rov = GameObject.Find("MiniROV");
        if (rov == null)
        {
            return;
        }

        rov.transform.position = new Vector3(0f, -5f, -18f);
        rov.transform.rotation = Quaternion.identity;
    }

    private static Material GetOrCreateSandMaterial()
    {
        string matPath = "Assets/MIMISK/Environment/NaturalKelpForest/NaturalKelp_Sand_Mat.mat";
        string texPath = "Assets/MIMISK/Environment/Seabed/soil_sand_0045_01.jpg";

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        Shader shader = Shader.Find("HDRP/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, matPath);
        }
        else
        {
            mat.shader = shader;
        }

        Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

        if (tex != null)
        {
            TextureImporter importer = AssetImporter.GetAtPath(texPath) as TextureImporter;
            if (importer != null)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.SaveAndReimport();
            }

            if (mat.HasProperty("_BaseColorMap"))
            {
                mat.SetTexture("_BaseColorMap", tex);
            }
            else if (mat.HasProperty("_MainTex"))
            {
                mat.SetTexture("_MainTex", tex);
            }
        }

        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", new Color(0.55f, 0.51f, 0.36f, 1f));
        }

        if (mat.HasProperty("_Smoothness"))
        {
            mat.SetFloat("_Smoothness", 0.25f);
        }

        return mat;
    }

    private static Material GetOrCreateParticleMaterial()
    {
        string path = "Assets/MIMISK/Materials/MarineSnow_Mat.mat";
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);

        Shader shader = Shader.Find("HDRP/Unlit");
        if (shader == null) shader = Shader.Find("Sprites/Default");

        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        else
        {
            mat.shader = shader;
        }

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
