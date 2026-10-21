using System.IO;
using UnityEngine;
using UnityEditor;

public static class ConcreteTextureGenerator
{
    private const string TextureDir = "Assets/Textures";
    private const string MaterialDir = "Assets/Materials";

    [MenuItem("Tools/FLIP//FALL/Generate Old Concrete PBR Textures")]
    public static void GenerateOldConcrete()
    {
        if (!Directory.Exists(TextureDir)) Directory.CreateDirectory(TextureDir);
        if (!Directory.Exists(MaterialDir)) Directory.CreateDirectory(MaterialDir);

        Debug.Log("[ConcreteTextureGenerator] Generating natural weathered Old Concrete PBR textures...");

        int size = 1024;
        Texture2D albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D normal = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D mask = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float[,] heightMap = new float[size, size];

        // Authentic aged concrete palette
        Color colDeep = new Color(0.36f, 0.37f, 0.38f);
        Color colBase = new Color(0.48f, 0.49f, 0.50f);
        Color colLight = new Color(0.59f, 0.60f, 0.61f);
        Color colGrime = new Color(0.30f, 0.29f, 0.27f); // subtle damp grime
        Color colPebble = new Color(0.26f, 0.26f, 0.27f); // fine dark gravel specks
        Color colSand = new Color(0.55f, 0.54f, 0.51f); // lighter sand specks

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;
                float v = y / (float)size;

                // Subtle organic domain warping (fluid cement cure / weathering)
                float warpX = SeamlessNoise(u, v, 2.5f, 15f) * 0.08f;
                float warpY = SeamlessNoise(u, v, 2.5f, 45f) * 0.08f;

                float uW = u + warpX;
                float vW = v + warpY;

                // Multi-octave natural concrete FBM
                float broadTone = SeamlessNoise(uW, vW, 2.0f, 10.0f);
                float midTone = SeamlessNoise(uW, vW, 5.0f, 25.0f) * 0.5f;
                float aggregateFine = SeamlessNoise(u, v, 16.0f, 50.0f) * 0.25f;
                float microPores = SeamlessNoise(u, v, 42.0f, 85.0f) * 0.12f;
                float sandGrains = SeamlessNoise(u, v, 90.0f, 110.0f) * 0.08f;

                float fbm = (broadTone + midTone + aggregateFine + microPores + sandGrains) / (1f + 0.5f + 0.25f + 0.12f + 0.08f);

                // Water dampness & mineral leaching (soft organic patches)
                float moisture = SeamlessNoise(uW * 1.5f, vW * 1.2f, 3.5f, 80.0f);

                // Scattered aggregate pebbles
                float pebbleNoise = SeamlessNoise(u, v, 55f, 30.0f);
                bool isPebble = pebbleNoise > 0.82f;
                bool isSand = pebbleNoise < 0.16f;

                // Color calculation: rich tonal depth without grid lines
                Color c = Color.Lerp(colDeep, colBase, fbm);
                if (fbm > 0.54f)
                {
                    c = Color.Lerp(c, colLight, (fbm - 0.54f) * 1.4f);
                }

                // Apply organic mineral / moisture darkening
                if (moisture > 0.56f)
                {
                    float mFactor = Mathf.Clamp01((moisture - 0.56f) * 2.0f);
                    c = Color.Lerp(c, colGrime, mFactor * 0.50f);
                }

                // Fine natural sand and pebble flecks
                if (isPebble)
                {
                    c = Color.Lerp(c, colPebble, 0.45f);
                }
                else if (isSand)
                {
                    c = Color.Lerp(c, colSand, 0.40f);
                }

                // Height calculation for micro-relief
                float h = fbm * 0.65f + microPores * 0.20f + sandGrains * 0.15f;
                if (isPebble) h += 0.06f;
                heightMap[x, y] = h;

                // Smoothness: matte weathered concrete (0.22 - 0.38)
                float smoothness = Mathf.Lerp(0.20f, 0.35f, fbm);
                if (moisture > 0.60f) smoothness += 0.06f;

                float occlusion = Mathf.Clamp01(0.72f + fbm * 0.28f);

                albedo.SetPixel(x, y, c);
                mask.SetPixel(x, y, new Color(0.02f, occlusion, 0f, smoothness));
            }
        }

        // Sobel filter for realistic concrete normal map
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int xL = (x - 1 + size) % size;
                int xR = (x + 1) % size;
                int yD = (y - 1 + size) % size;
                int yU = (y + 1) % size;

                float dX = ((heightMap[xR, yU] + 2f * heightMap[xR, y] + heightMap[xR, yD]) -
                            (heightMap[xL, yU] + 2f * heightMap[xL, y] + heightMap[xL, yD])) * 2.0f;

                float dY = ((heightMap[xL, yU] + 2f * heightMap[x, yU] + heightMap[xR, yU]) -
                            (heightMap[xL, yD] + 2f * heightMap[x, yD] + heightMap[xR, yD])) * 2.0f;

                Vector3 n = new Vector3(-dX, -dY, 1.0f).normalized;
                normal.SetPixel(x, y, new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f));
            }
        }

        SaveAndImport(albedo, $"{TextureDir}/OldConcrete_Albedo.png", false, true);
        SaveAndImport(normal, $"{TextureDir}/OldConcrete_Normal.png", true, false);
        SaveAndImport(mask, $"{TextureDir}/OldConcrete_Mask.png", false, false);

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        CreateOrUpdateMaterial();
        Debug.Log("[ConcreteTextureGenerator] Natural weathered Old Concrete PBR textures updated!");
    }

    private static float SeamlessNoise(float u, float v, float scale, float offset)
    {
        u = Mathf.Repeat(u, 1f);
        v = Mathf.Repeat(v, 1f);

        float s = scale;
        float o = offset;

        float n00 = Mathf.PerlinNoise(u * s + o, v * s + o);
        float n10 = Mathf.PerlinNoise((u - 1f) * s + o, v * s + o);
        float n01 = Mathf.PerlinNoise(u * s + o, (v - 1f) * s + o);
        float n11 = Mathf.PerlinNoise((u - 1f) * s + o, (v - 1f) * s + o);

        float wX = Mathf.SmoothStep(0f, 1f, u);
        float wY = Mathf.SmoothStep(0f, 1f, v);

        float bot = Mathf.Lerp(n00, n10, wX);
        float top = Mathf.Lerp(n01, n11, wX);
        return Mathf.Lerp(bot, top, wY);
    }

    private static void SaveAndImport(Texture2D tex, string path, bool isNormal, bool issRGB)
    {
        byte[] bytes = tex.EncodeToPNG();
        File.WriteAllBytes(path, bytes);
        Object.DestroyImmediate(tex);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            if (isNormal)
            {
                importer.textureType = TextureImporterType.NormalMap;
            }
            else
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = issRGB;
            }
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.SaveAndReimport();
        }
    }

    private static void CreateOrUpdateMaterial()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        string matPath = $"{MaterialDir}/PBR_OldConcrete_Seamless.mat";

        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(urpLit);
            AssetDatabase.CreateAsset(mat, matPath);
        }

        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/OldConcrete_Albedo.png");
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/OldConcrete_Normal.png");
        Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/OldConcrete_Mask.png");

        if (albedo != null)
        {
            mat.SetTexture("_BaseMap", albedo);
            mat.SetTexture("_MainTex", albedo);
        }
        if (normal != null)
        {
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_BumpScale", 1.1f);
        }
        if (mask != null)
        {
            mat.SetTexture("_MetallicGlossMap", mask);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        mat.SetFloat("_Smoothness", 0.30f);
        mat.SetFloat("_Metallic", 0.02f);
        mat.SetTextureScale("_BaseMap", new Vector2(2f, 2f));
        mat.SetTextureScale("_BumpMap", new Vector2(2f, 2f));

        EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();
    }
}
