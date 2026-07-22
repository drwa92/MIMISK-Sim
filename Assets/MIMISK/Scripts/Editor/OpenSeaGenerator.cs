using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class OpenSeaGenerator
{
    private const float OceanSize = 200f;
    private const float SeaDepth = -20f;
    private const float TerrainSize = 120f;
    private const int Subdivisions = 160;

    [MenuItem("MIMISK/Environment/Create Open Sea Setup")]
    public static void CreateOpenSeaSetup()
    {
        DisablePoolOnlyObjects();
        ConfigureWaterSurface();
        CreateOpenSeaSeabed();
        CreateRocks();
        PositionMiniROV();
        CreateOpenSeaOverviewCamera();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Open sea setup generated.");
    }

    private static void DisablePoolOnlyObjects()
    {
        DisableIfExists("Pool_Boundaries");
        DisableIfExists("SandFloor_PhysicalPool");
        DisableIfExists("Seabed_Plane");
        DisableIfExists("heightmap");
    }

    private static void DisableIfExists(string objectName)
    {
        GameObject obj = GameObject.Find(objectName);
        if (obj != null)
        {
            obj.SetActive(false);
        }
    }

    private static void ConfigureWaterSurface()
    {
        GameObject water = GameObject.Find("Ocean_Surface");

        if (water == null)
        {
            water = GameObject.Find("Pool");
        }

        if (water == null)
        {
            Debug.LogWarning("[MIMISK] No Pool/Ocean_Surface object found. Create one manually using GameObject > Water > Ocean.");
            return;
        }

        water.name = "Ocean_Surface";
        water.transform.position = new Vector3(0f, 0f, 0f);
        water.transform.rotation = Quaternion.identity;
        water.transform.localScale = new Vector3(OceanSize, 1f, OceanSize);

        BoxCollider col = water.GetComponent<BoxCollider>();
        if (col == null)
        {
            col = water.AddComponent<BoxCollider>();
        }

        col.isTrigger = true;
        col.center = new Vector3(0f, SeaDepth * 0.5f, 0f);
        col.size = new Vector3(1f, Mathf.Abs(SeaDepth), 1f);

        try
        {
            water.tag = "Water Volume";
        }
        catch
        {
            Debug.LogWarning("[MIMISK] Tag 'Water Volume' missing. Add it manually and assign it to Ocean_Surface.");
        }
    }

    private static void CreateOpenSeaSeabed()
    {
        GameObject existing = GameObject.Find("OpenSea_Seabed");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        GameObject root = GetOrCreateRoot("OpenSea_Environment");

        int vertsPerSide = Subdivisions + 1;
        Vector3[] vertices = new Vector3[vertsPerSide * vertsPerSide];
        Vector2[] uvs = new Vector2[vertsPerSide * vertsPerSide];
        int[] triangles = new int[Subdivisions * Subdivisions * 6];

        for (int z = 0; z < vertsPerSide; z++)
        {
            for (int x = 0; x < vertsPerSide; x++)
            {
                float u = (float)x / Subdivisions;
                float v = (float)z / Subdivisions;

                float wx = (u - 0.5f) * TerrainSize;
                float wz = (v - 0.5f) * TerrainSize;
                float y = SeabedHeight(wx, wz);

                int index = z * vertsPerSide + x;
                vertices[index] = new Vector3(wx, y, wz);
                uvs[index] = new Vector2(u * 30f, v * 30f);
            }
        }

        int t = 0;
        for (int z = 0; z < Subdivisions; z++)
        {
            for (int x = 0; x < Subdivisions; x++)
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
        mesh.name = "OpenSea_Seabed_Mesh";
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject seabed = new GameObject("OpenSea_Seabed");
        seabed.transform.SetParent(root.transform);
        seabed.transform.position = Vector3.zero;

        MeshFilter mf = seabed.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;

        MeshRenderer mr = seabed.AddComponent<MeshRenderer>();
        mr.sharedMaterial = GetOrCreateSandMaterial();

        MeshCollider mc = seabed.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;

        Selection.activeGameObject = seabed;
    }

    private static float SeabedHeight(float x, float z)
    {
        float baseDepth = SeaDepth;

        float largeNoise =
            Mathf.PerlinNoise(x * 0.035f + 12.3f, z * 0.035f + 8.7f) * 3.0f;

        float smallRipples =
            Mathf.Sin(x * 0.7f + z * 0.2f) * 0.08f +
            Mathf.Sin(z * 0.9f) * 0.04f;

        return baseDepth + largeNoise + smallRipples;
    }

    private static void CreateRocks()
    {
        GameObject root = GetOrCreateRoot("OpenSea_Environment");

        GameObject oldRocks = GameObject.Find("OpenSea_Rocks");
        if (oldRocks != null)
        {
            Object.DestroyImmediate(oldRocks);
        }

        GameObject rocksRoot = new GameObject("OpenSea_Rocks");
        rocksRoot.transform.SetParent(root.transform);

        Material rockMat = GetOrCreateRockMaterial();

        System.Random rng = new System.Random(42);

        for (int i = 0; i < 35; i++)
        {
            float x = Mathf.Lerp(-45f, 45f, (float)rng.NextDouble());
            float z = Mathf.Lerp(-45f, 45f, (float)rng.NextDouble());
            float y = SeabedHeight(x, z) + 0.15f;

            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            rock.name = "Rock_" + i.ToString("00");
            rock.transform.SetParent(rocksRoot.transform);
            rock.transform.position = new Vector3(x, y, z);

            float sx = Mathf.Lerp(0.4f, 1.8f, (float)rng.NextDouble());
            float sy = Mathf.Lerp(0.15f, 0.8f, (float)rng.NextDouble());
            float sz = Mathf.Lerp(0.4f, 1.8f, (float)rng.NextDouble());

            rock.transform.localScale = new Vector3(sx, sy, sz);
            rock.transform.rotation = Quaternion.Euler(
                Mathf.Lerp(-10f, 10f, (float)rng.NextDouble()),
                Mathf.Lerp(0f, 360f, (float)rng.NextDouble()),
                Mathf.Lerp(-10f, 10f, (float)rng.NextDouble())
            );

            MeshRenderer mr = rock.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                mr.sharedMaterial = rockMat;
            }
        }
    }

    private static void PositionMiniROV()
    {
        GameObject rov = GameObject.Find("MiniROV");
        if (rov == null)
        {
            Debug.LogWarning("[MIMISK] MiniROV not found. Skipping ROV positioning.");
            return;
        }

        rov.transform.position = new Vector3(0f, -5f, 0f);
        rov.transform.rotation = Quaternion.identity;

        Component buoyancy = rov.GetComponent("SimpleROVBuoyancy");
        if (buoyancy != null)
        {
            SerializedObject so = new SerializedObject(buoyancy);

            SerializedProperty waterLevel = so.FindProperty("waterLevel");
            if (waterLevel != null) waterLevel.floatValue = 0f;

            SerializedProperty buoyancyForce = so.FindProperty("buoyancyForce");
            if (buoyancyForce != null) buoyancyForce.floatValue = 5.88f;

            SerializedProperty waterDrag = so.FindProperty("waterDrag");
            if (waterDrag != null) waterDrag.floatValue = 5f;

            SerializedProperty waterAngularDrag = so.FindProperty("waterAngularDrag");
            if (waterAngularDrag != null) waterAngularDrag.floatValue = 4f;

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void CreateOpenSeaOverviewCamera()
    {
        GameObject existing = GameObject.Find("OpenSeaOverviewCamera");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        GameObject camObj = new GameObject("OpenSeaOverviewCamera");
        Camera cam = camObj.AddComponent<Camera>();

        camObj.transform.position = new Vector3(0f, 60f, 0f);
        camObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        cam.orthographic = true;
        cam.orthographicSize = 65f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 150f;
        cam.depth = 10;

        cam.rect = new Rect(0.70f, 0.70f, 0.28f, 0.28f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.08f, 0.10f, 1f);
    }

    private static GameObject GetOrCreateRoot(string name)
    {
        GameObject root = GameObject.Find(name);
        if (root == null)
        {
            root = new GameObject(name);
        }

        return root;
    }

    private static Material GetOrCreateSandMaterial()
    {
        string materialPath = "Assets/MIMISK/Environment/OpenSea/OpenSea_Sand_Mat.mat";
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
            else if (mat.HasProperty("_MainTex"))
            {
                mat.SetTexture("_MainTex", tex);
            }
        }

        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", new Color(0.68f, 0.60f, 0.45f, 1f));
        }

        if (mat.HasProperty("_Smoothness"))
        {
            mat.SetFloat("_Smoothness", 0.25f);
        }

        return mat;
    }

    private static Material GetOrCreateRockMaterial()
    {
        string materialPath = "Assets/MIMISK/Materials/OpenSea_Rock_Mat.mat";

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

        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", new Color(0.32f, 0.32f, 0.30f, 1f));
        }

        if (mat.HasProperty("_Smoothness"))
        {
            mat.SetFloat("_Smoothness", 0.45f);
        }

        return mat;
    }
}
