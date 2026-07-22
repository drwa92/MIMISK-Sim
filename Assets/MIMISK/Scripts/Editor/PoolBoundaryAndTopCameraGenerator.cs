using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class PoolBoundaryAndTopCameraGenerator
{
    private const float PoolLengthX = 8.0f;
    private const float PoolWidthZ = 4.0f;
    private const float PoolDepth = 3.0f;
    private const float WallThickness = 0.08f;
    private const float WaterLevelY = 0.0f;
    private const float FloorY = -3.0f;

    [MenuItem("MIMISK/Environment/Create Pool Boundaries And Top Camera")]
    public static void CreatePoolBoundariesAndTopCamera()
    {
        ConfigureHDRPPool();
        CreateWalls();
        CreateTopCamera();

        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[MIMISK] Created LABUST-style pool boundaries and TopCamera.");
    }

    private static void ConfigureHDRPPool()
    {
        GameObject pool = GameObject.Find("Pool");

        if (pool == null)
        {
            Debug.LogWarning("[MIMISK] No object named 'Pool' found. Skipping HDRP water pool configuration.");
            return;
        }

        pool.transform.position = new Vector3(0f, WaterLevelY, 0f);
        pool.transform.rotation = Quaternion.identity;
        pool.transform.localScale = new Vector3(PoolLengthX, 1f, PoolWidthZ);

        BoxCollider collider = pool.GetComponent<BoxCollider>();
        if (collider == null)
        {
            collider = pool.AddComponent<BoxCollider>();
        }

        collider.isTrigger = true;
        collider.center = new Vector3(0f, -PoolDepth * 0.5f, 0f);
        collider.size = new Vector3(1f, PoolDepth, 1f);

        try
        {
            pool.tag = "Water Volume";
        }
        catch
        {
            Debug.LogWarning("[MIMISK] Tag 'Water Volume' does not exist. Create it manually and assign it to Pool.");
        }
    }

    private static void CreateWalls()
    {
        GameObject environment = GameObject.Find("Environment");
        if (environment == null)
        {
            environment = new GameObject("Environment");
        }

        GameObject existingRoot = GameObject.Find("Pool_Boundaries");
        if (existingRoot != null)
        {
            Object.DestroyImmediate(existingRoot);
        }

        GameObject root = new GameObject("Pool_Boundaries");
        root.transform.SetParent(environment.transform);

        Material wallMaterial = GetOrCreateWallMaterial();

        float centerY = WaterLevelY - PoolDepth * 0.5f;

        CreateWall(
            "PoolWall_North",
            new Vector3(0f, centerY, PoolWidthZ * 0.5f + WallThickness * 0.5f),
            new Vector3(PoolLengthX + 2f * WallThickness, PoolDepth, WallThickness),
            root.transform,
            wallMaterial
        );

        CreateWall(
            "PoolWall_South",
            new Vector3(0f, centerY, -PoolWidthZ * 0.5f - WallThickness * 0.5f),
            new Vector3(PoolLengthX + 2f * WallThickness, PoolDepth, WallThickness),
            root.transform,
            wallMaterial
        );

        CreateWall(
            "PoolWall_East",
            new Vector3(PoolLengthX * 0.5f + WallThickness * 0.5f, centerY, 0f),
            new Vector3(WallThickness, PoolDepth, PoolWidthZ),
            root.transform,
            wallMaterial
        );

        CreateWall(
            "PoolWall_West",
            new Vector3(-PoolLengthX * 0.5f - WallThickness * 0.5f, centerY, 0f),
            new Vector3(WallThickness, PoolDepth, PoolWidthZ),
            root.transform,
            wallMaterial
        );
    }

    private static void CreateWall(string name, Vector3 position, Vector3 scale, Transform parent, Material material)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.position = position;
        wall.transform.rotation = Quaternion.identity;
        wall.transform.localScale = scale;
        wall.transform.SetParent(parent);

        MeshRenderer renderer = wall.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }

        BoxCollider collider = wall.GetComponent<BoxCollider>();
        if (collider != null)
        {
            collider.isTrigger = false;
        }
    }

    private static Material GetOrCreateWallMaterial()
    {
        string materialPath = "Assets/MIMISK/Materials/PoolWall_Concrete_Mat.mat";

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

        Color concreteColor = new Color(0.45f, 0.50f, 0.52f, 1.0f);

        if (mat.HasProperty("_BaseColor"))
        {
            mat.SetColor("_BaseColor", concreteColor);
        }

        if (mat.HasProperty("_Smoothness"))
        {
            mat.SetFloat("_Smoothness", 0.35f);
        }

        return mat;
    }

    private static void CreateTopCamera()
    {
        GameObject existing = GameObject.Find("TopCamera");
        if (existing != null)
        {
            Object.DestroyImmediate(existing);
        }

        GameObject cameraObject = new GameObject("TopCamera");
        Camera cam = cameraObject.AddComponent<Camera>();

        cameraObject.transform.position = new Vector3(0f, 5f, 0f);
        cameraObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        cam.orthographic = true;
        cam.orthographicSize = 4.5f;
        cam.nearClipPlane = 0.05f;
        cam.farClipPlane = 20f;

        // Small top-right overlay camera.
        cam.rect = new Rect(0.68f, 0.68f, 0.30f, 0.30f);
        cam.depth = 20f;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.12f, 0.15f, 1.0f);

        AudioListener listener = cameraObject.GetComponent<AudioListener>();
        if (listener != null)
        {
            Object.DestroyImmediate(listener);
        }

        Debug.Log("[MIMISK] TopCamera created as top-right overlay. For full-screen top view, set Camera Rect to X=0 Y=0 W=1 H=1.");
    }
}
