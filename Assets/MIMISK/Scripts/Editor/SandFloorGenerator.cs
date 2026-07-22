using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class SandFloorGenerator
{
    [MenuItem("MIMISK/Environment/Create Physical Scale Sand Floor")]
    public static void CreatePhysicalScaleSandFloor()
    {
        // Disable old terrain objects, but do not delete assets from project.
        GameObject oldHeightmap = GameObject.Find("heightmap");
        if (oldHeightmap != null)
        {
            oldHeightmap.SetActive(false);
        }

        GameObject oldPlane = GameObject.Find("Seabed_Plane");
        if (oldPlane != null)
        {
            oldPlane.SetActive(false);
        }

        GameObject oldSand = GameObject.Find("SandFloor_PhysicalPool");
        if (oldSand != null)
        {
            Object.DestroyImmediate(oldSand);
        }

        // Pool-scale floor: LABUST-like 8 m x 4 m x 3 m pool.
        float sizeX = 8.0f;
        float sizeZ = 4.0f;
        float depthY = -3.0f;

        int subdivisionsX = 120;
        int subdivisionsZ = 60;

        float sandRippleAmplitude = 0.025f; // 2.5 cm small sand undulations
        float longWaveAmplitude = 0.035f;   // 3.5 cm slow terrain variation

        int vertsX = subdivisionsX + 1;
        int vertsZ = subdivisionsZ + 1;

        Vector3[] vertices = new Vector3[vertsX * vertsZ];
        Vector2[] uvs = new Vector2[vertsX * vertsZ];
        int[] triangles = new int[subdivisionsX * subdivisionsZ * 6];

        for (int z = 0; z < vertsZ; z++)
        {
            for (int x = 0; x < vertsX; x++)
            {
                float u = (float)x / subdivisionsX;
                float v = (float)z / subdivisionsZ;

                float worldX = (u - 0.5f) * sizeX;
                float worldZ = (v - 0.5f) * sizeZ;

                float ripple =
                    Mathf.Sin(worldX * 8.0f + worldZ * 1.7f) * sandRippleAmplitude +
                    Mathf.Sin(worldZ * 9.0f) * sandRippleAmplitude * 0.4f;

                float longWave =
                    Mathf.PerlinNoise(worldX * 0.35f + 13.1f, worldZ * 0.35f + 7.2f) * longWaveAmplitude;

                float y = depthY + ripple + longWave;

                int index = z * vertsX + x;
                vertices[index] = new Vector3(worldX, y, worldZ);

                // Texture tiling: repeat sand texture several times across the floor.
                uvs[index] = new Vector2(u * 8.0f, v * 4.0f);
            }
        }

        int t = 0;
        for (int z = 0; z < subdivisionsZ; z++)
        {
            for (int x = 0; x < subdivisionsX; x++)
            {
                int i = z * vertsX + x;

                triangles[t++] = i;
                triangles[t++] = i + vertsX;
                triangles[t++] = i + 1;

                triangles[t++] = i + 1;
                triangles[t++] = i + vertsX;
                triangles[t++] = i + vertsX + 1;
            }
        }

        Mesh mesh = new Mesh();
        mesh.name = "SandFloor_PhysicalPool_Mesh";
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        GameObject sandFloor = new GameObject("SandFloor_PhysicalPool");
        sandFloor.transform.position = Vector3.zero;
        sandFloor.transform.rotation = Quaternion.identity;
        sandFloor.transform.localScale = Vector3.one;

        MeshFilter meshFilter = sandFloor.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        MeshRenderer meshRenderer = sandFloor.AddComponent<MeshRenderer>();
        MeshCollider meshCollider = sandFloor.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = mesh;

        Material sandMaterial = GetOrCreateSandMaterial();
        meshRenderer.sharedMaterial = sandMaterial;

        GameObject environment = GameObject.Find("Environment");
        if (environment == null)
        {
            environment = new GameObject("Environment");
        }

        sandFloor.transform.SetParent(environment.transform);

        Selection.activeGameObject = sandFloor;

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        Debug.Log("[MIMISK] Created physical-scale underwater sand floor: 8 m x 4 m at Y = -3 m.");
    }

    private static Material GetOrCreateSandMaterial()
    {
        string materialFolder = "Assets/MIMISK/Environment/SandMap";
        string materialPath = materialFolder + "/SandFloor_Mat.mat";
        string texturePath = "Assets/MIMISK/Environment/Seabed/soil_sand_0045_01.jpg";

        if (!AssetDatabase.IsValidFolder(materialFolder))
        {
            AssetDatabase.CreateFolder("Assets/MIMISK/Environment", "SandMap");
        }

        Texture2D sandTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);

        TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
        if (importer != null)
        {
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

        Shader shader = Shader.Find("HDRP/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, materialPath);
        }
        else
        {
            mat.shader = shader;
        }

        Color sandColor = new Color(0.72f, 0.64f, 0.48f, 1.0f);

        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", sandColor);
        }

        if (sandTexture != null)
        {
            if (mat.HasProperty("_BaseColorMap"))
            {
                mat.SetTexture("_BaseColorMap", sandTexture);
                mat.SetTextureScale("_BaseColorMap", new Vector2(1, 1));
            }
            else if (mat.HasProperty("_MainTex"))
            {
                mat.SetTexture("_MainTex", sandTexture);
                mat.SetTextureScale("_MainTex", new Vector2(1, 1));
            }
        }

        if (mat.HasProperty("_Smoothness"))
        {
            mat.SetFloat("_Smoothness", 0.25f);
        }

        AssetDatabase.SaveAssets();
        return mat;
    }
}
