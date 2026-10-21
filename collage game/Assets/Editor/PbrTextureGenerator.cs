using System.IO;
using UnityEngine;
using UnityEditor;

public static class PbrTextureGenerator
{
    private const string TextureDir = "Assets/Textures";
    private const string MaterialDir = "Assets/Materials";

    [MenuItem("Tools/Generate Realistic PBR Textures & Materials")]
    public static void GenerateAll()
    {
        if (!Directory.Exists(TextureDir)) Directory.CreateDirectory(TextureDir);
        if (!Directory.Exists(MaterialDir)) Directory.CreateDirectory(MaterialDir);

        Debug.Log("[PbrTextureGenerator] Generating high-resolution procedural PBR textures...");

        // 1. Rusted Corroded Steel (Albedo, Normal, Metallic/Smoothness Mask)
        GenerateRustedSteelTextures();

        // 2. Realistic Moss & Overgrowth (Albedo, Normal)
        GenerateMossTextures();

        // 3. Cracked Concrete & Rubble (Albedo, Normal, Mask)
        GenerateConcreteTextures();

        // 4. Muddy Impact Soil (Albedo, Normal)
        GenerateSoilTextures();

        // 5. Braided Steel Cable (Albedo, Normal)
        GenerateCableTextures();

        // 6. Hazard Warning Stripes (Albedo, Normal)
        GenerateHazardTextures();

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // Configure texture importers
        ConfigureTextureImporter($"{TextureDir}/RustedSteel_Normal.png", true);
        ConfigureTextureImporter($"{TextureDir}/Moss_Normal.png", true);
        ConfigureTextureImporter($"{TextureDir}/Concrete_Normal.png", true);
        ConfigureTextureImporter($"{TextureDir}/Soil_Normal.png", true);
        ConfigureTextureImporter($"{TextureDir}/Cable_Normal.png", true);
        ConfigureTextureImporter($"{TextureDir}/Hazard_Normal.png", true);

        ConfigureTextureImporter($"{TextureDir}/RustedSteel_Albedo.png", false, true);
        ConfigureTextureImporter($"{TextureDir}/Moss_Albedo.png", false, true);
        ConfigureTextureImporter($"{TextureDir}/Concrete_Albedo.png", false, true);
        ConfigureTextureImporter($"{TextureDir}/Soil_Albedo.png", false, true);
        ConfigureTextureImporter($"{TextureDir}/Cable_Albedo.png", false, true);
        ConfigureTextureImporter($"{TextureDir}/Hazard_Albedo.png", false, true);

        ConfigureTextureImporter($"{TextureDir}/RustedSteel_Mask.png", false, false);
        ConfigureTextureImporter($"{TextureDir}/Concrete_Mask.png", false, false);

        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        // Create URP Materials using the textures
        CreatePbrMaterials();

        Debug.Log("[PbrTextureGenerator] All PBR Textures and Materials successfully generated!");
    }

    private static void ConfigureTextureImporter(string path, bool isNormal, bool isSrgb = true)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Bilinear;
            importer.anisoLevel = 8;

            if (isNormal)
            {
                importer.textureType = TextureImporterType.NormalMap;
            }
            else
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = isSrgb;
            }
            importer.SaveAndReimport();
        }
    }

    // 1. RUSTED CORRODED STEEL
    private static void GenerateRustedSteelTextures()
    {
        int size = 512;
        Texture2D albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D normal = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D mask = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float[,] heightMap = new float[size, size];

        Color basePaint = new Color(0.23f, 0.31f, 0.32f); 
        Color baseRustDark = new Color(0.18f, 0.08f, 0.04f); 
        Color baseRustBright = new Color(0.58f, 0.26f, 0.08f); 
        Color steelBare = new Color(0.52f, 0.54f, 0.56f); 

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;
                float v = y / (float)size;

                float n1 = Mathf.PerlinNoise(u * 5f, v * 5f);
                float n2 = Mathf.PerlinNoise(u * 14f + 20f, v * 14f + 20f) * 0.5f;
                float n3 = Mathf.PerlinNoise(u * 36f + 40f, v * 36f + 40f) * 0.25f;
                float rustFactor = Mathf.Clamp01((n1 + n2 + n3 - 0.45f) * 2.8f);

                float microNoise = Mathf.PerlinNoise(u * 80f, v * 80f);
                float scratch = Mathf.Abs(Mathf.Sin(u * 140f + Mathf.PerlinNoise(u * 10f, v * 10f) * 12f)) > 0.97f ? 0.4f : 0f;

                // Rivet dots pattern along edges
                bool isRivet = false;
                if ((x % 64 >= 28 && x % 64 <= 36) && (y % 64 >= 28 && y % 64 <= 36))
                {
                    float dx = (x % 64) - 32;
                    float dy = (y % 64) - 32;
                    if (dx * dx + dy * dy < 16) isRivet = true;
                }

                Color c;
                float metallic;
                float smoothness;
                float h;

                if (isRivet)
                {
                    c = steelBare * 0.9f;
                    metallic = 0.9f;
                    smoothness = 0.5f;
                    h = 0.95f;
                }
                else if (rustFactor > 0.55f)
                {
                    float rustSub = Mathf.PerlinNoise(u * 25f, v * 25f);
                    c = Color.Lerp(baseRustDark, baseRustBright, rustSub);
                    metallic = 0.04f;
                    smoothness = 0.12f;
                    h = 0.32f + microNoise * 0.22f;
                }
                else if (rustFactor > 0.32f)
                {
                    c = Color.Lerp(baseRustBright, basePaint, (rustFactor - 0.32f) / 0.23f);
                    metallic = 0.15f;
                    smoothness = 0.25f;
                    h = 0.62f + microNoise * 0.16f;
                }
                else
                {
                    c = (scratch > 0.1f) ? steelBare : (basePaint * (0.85f + microNoise * 0.3f));
                    metallic = (scratch > 0.1f) ? 0.9f : 0.35f;
                    smoothness = (scratch > 0.1f) ? 0.65f : 0.48f;
                    h = 0.76f + scratch * 0.2f;
                }

                heightMap[x, y] = h;
                albedo.SetPixel(x, y, c);
                mask.SetPixel(x, y, new Color(metallic, 1f, 0f, smoothness));
            }
        }

        ComputeNormalMap(heightMap, normal, size, 5.0f);
        SaveTexture(albedo, $"{TextureDir}/RustedSteel_Albedo.png");
        SaveTexture(normal, $"{TextureDir}/RustedSteel_Normal.png");
        SaveTexture(mask, $"{TextureDir}/RustedSteel_Mask.png");
    }

    // 2. REALISTIC MOSS & OVERGROWTH
    private static void GenerateMossTextures()
    {
        int size = 512;
        Texture2D albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D normal = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float[,] heightMap = new float[size, size];

        Color darkVelvet = new Color(0.09f, 0.22f, 0.05f);
        Color lushGreen = new Color(0.22f, 0.46f, 0.11f);
        Color brightMoss = new Color(0.44f, 0.66f, 0.16f);
        Color lichenGold = new Color(0.56f, 0.60f, 0.20f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;
                float v = y / (float)size;

                float clump = Mathf.PerlinNoise(u * 7f, v * 7f);
                float fibers1 = Mathf.PerlinNoise(u * 32f + 5f, v * 32f + 5f);
                float fibers2 = Mathf.PerlinNoise(u * 85f + 14f, v * 85f + 14f);
                float totalNoise = clump * 0.5f + fibers1 * 0.35f + fibers2 * 0.15f;

                Color c = Color.Lerp(darkVelvet, lushGreen, totalNoise);
                if (totalNoise > 0.60f)
                {
                    c = Color.Lerp(c, brightMoss, (totalNoise - 0.60f) / 0.25f);
                }
                if (totalNoise > 0.80f)
                {
                    c = Color.Lerp(c, lichenGold, (totalNoise - 0.80f) / 0.20f);
                }

                heightMap[x, y] = totalNoise;
                albedo.SetPixel(x, y, c);
            }
        }

        ComputeNormalMap(heightMap, normal, size, 5.5f);
        SaveTexture(albedo, $"{TextureDir}/Moss_Albedo.png");
        SaveTexture(normal, $"{TextureDir}/Moss_Normal.png");
    }

    // 3. CAST ARCHITECTURAL & INDUSTRIAL CONCRETE
    private static void GenerateConcreteTextures()
    {
        int size = 512;
        Texture2D albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D normal = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D mask = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float[,] heightMap = new float[size, size];

        Color concreteBase = new Color(0.52f, 0.53f, 0.54f);
        Color concreteDark = new Color(0.42f, 0.43f, 0.44f);
        Color concreteLight = new Color(0.62f, 0.63f, 0.64f);
        Color stainColor = new Color(0.36f, 0.35f, 0.32f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;
                float v = y / (float)size;

                // Multi-octave fine aggregate and porosity
                float fineGrain = Mathf.PerlinNoise(u * 60f, v * 60f) * 0.5f + Mathf.PerlinNoise(u * 140f + 7f, v * 140f + 7f) * 0.3f;
                float broadTone = Mathf.PerlinNoise(u * 5f, v * 5f);
                float stain = Mathf.PerlinNoise(u * 12f + 3f, v * 12f + 3f);

                // Subtle cast panel seams (formwork joints)
                float seamX = Mathf.Min(Mathf.Abs(u - 0.5f), Mathf.Min(u, 1f - u));
                float seamY = Mathf.Min(Mathf.Abs(v - 0.5f), Mathf.Min(v, 1f - v));
                float seamDist = Mathf.Min(seamX, seamY);
                float seamIndent = (seamDist < 0.008f) ? Mathf.SmoothStep(0f, 1f, seamDist / 0.008f) : 1f;

                Color c = Color.Lerp(concreteDark, concreteBase, broadTone);
                c = Color.Lerp(c, concreteLight, fineGrain * 0.45f);
                if (stain > 0.62f)
                {
                    c = Color.Lerp(c, stainColor, (stain - 0.62f) * 0.7f);
                }
                c *= (0.75f + seamIndent * 0.25f);

                float h = 0.5f + (fineGrain - 0.4f) * 0.12f;
                h *= (0.6f + seamIndent * 0.4f);
                float smoothness = Mathf.Lerp(0.28f, 0.42f, fineGrain);

                heightMap[x, y] = h;
                albedo.SetPixel(x, y, c);
                mask.SetPixel(x, y, new Color(0.02f, 1f, 0f, smoothness));
            }
        }

        ComputeNormalMap(heightMap, normal, size, 2.2f);
        SaveTexture(albedo, $"{TextureDir}/Concrete_Albedo.png");
        SaveTexture(normal, $"{TextureDir}/Concrete_Normal.png");
        SaveTexture(mask, $"{TextureDir}/Concrete_Mask.png");
    }

    // 4. MUDDY IMPACT SOIL & PEBBLES
    private static void GenerateSoilTextures()
    {
        int size = 512;
        Texture2D albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D normal = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float[,] heightMap = new float[size, size];

        Color dampDirt = new Color(0.16f, 0.13f, 0.09f);
        Color wetMud = new Color(0.11f, 0.09f, 0.06f);
        Color mossyDirt = new Color(0.18f, 0.22f, 0.10f);
        Color pebble = new Color(0.36f, 0.34f, 0.30f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;
                float v = y / (float)size;

                float nBase = Mathf.PerlinNoise(u * 9f, v * 9f);
                float nDetail = Mathf.PerlinNoise(u * 55f, v * 55f);
                float pebbleNoise = Mathf.PerlinNoise(u * 95f + 35f, v * 95f + 35f);

                Color c = Color.Lerp(dampDirt, mossyDirt, nBase);
                float h = nBase * 0.45f + nDetail * 0.3f;

                if (nBase < 0.35f)
                {
                    // Wet puddle depression
                    c = wetMud;
                    h *= 0.6f;
                }
                else if (pebbleNoise > 0.80f)
                {
                    c = Color.Lerp(c, pebble, (pebbleNoise - 0.80f) / 0.20f);
                    h += 0.4f;
                }

                heightMap[x, y] = h;
                albedo.SetPixel(x, y, c);
            }
        }

        ComputeNormalMap(heightMap, normal, size, 6.0f);
        SaveTexture(albedo, $"{TextureDir}/Soil_Albedo.png");
        SaveTexture(normal, $"{TextureDir}/Soil_Normal.png");
    }

    // 5. BRAIDED STEEL CABLE
    private static void GenerateCableTextures()
    {
        int size = 512;
        Texture2D albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D normal = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float[,] heightMap = new float[size, size];

        Color darkSteel = new Color(0.14f, 0.15f, 0.17f);
        Color metallicHighlight = new Color(0.48f, 0.51f, 0.55f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;
                float v = y / (float)size;

                float spiral = Mathf.Sin((v * 36f + u * 10f) * Mathf.PI * 2f);
                float ridge = (spiral + 1f) * 0.5f;
                float wireNoise = Mathf.PerlinNoise(u * 90f, v * 90f) * 0.2f;

                Color c = Color.Lerp(darkSteel, metallicHighlight, ridge * 0.7f + wireNoise);
                heightMap[x, y] = ridge * 0.85f + wireNoise;
                albedo.SetPixel(x, y, c);
            }
        }

        ComputeNormalMap(heightMap, normal, size, 7.5f);
        SaveTexture(albedo, $"{TextureDir}/Cable_Albedo.png");
        SaveTexture(normal, $"{TextureDir}/Cable_Normal.png");
    }

    // 6. HAZARD WARNING STRIPES
    private static void GenerateHazardTextures()
    {
        int size = 512;
        Texture2D albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D normal = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float[,] heightMap = new float[size, size];

        Color warningYellow = new Color(0.88f, 0.72f, 0.10f);
        Color warningBlack = new Color(0.12f, 0.12f, 0.13f);
        Color chippedEdge = new Color(0.28f, 0.20f, 0.14f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;
                float v = y / (float)size;

                // 45-degree diagonal stripe
                float stripeVal = Mathf.Sin((u + v) * 24f * Mathf.PI);
                bool isYellow = stripeVal > 0f;

                float wear = Mathf.PerlinNoise(u * 30f, v * 30f);
                Color c = isYellow ? warningYellow : warningBlack;
                float h = 0.7f;

                if (wear > 0.65f)
                {
                    c = Color.Lerp(c, chippedEdge, (wear - 0.65f) / 0.25f);
                    h = 0.45f;
                }

                heightMap[x, y] = h;
                albedo.SetPixel(x, y, c);
            }
        }

        ComputeNormalMap(heightMap, normal, size, 5.0f);
        SaveTexture(albedo, $"{TextureDir}/Hazard_Albedo.png");
        SaveTexture(normal, $"{TextureDir}/Hazard_Normal.png");
    }

    // SOBEL FILTER NORMAL MAP
    private static void ComputeNormalMap(float[,] heightMap, Texture2D target, int size, float strength)
    {
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int xPrev = (x - 1 + size) % size;
                int xNext = (x + 1) % size;
                int yPrev = (y - 1 + size) % size;
                int yNext = (y + 1) % size;

                float dX = (heightMap[xNext, yPrev] + 2f * heightMap[xNext, y] + heightMap[xNext, yNext]) -
                           (heightMap[xPrev, yPrev] + 2f * heightMap[xPrev, y] + heightMap[xPrev, yNext]);

                float dY = (heightMap[xPrev, yNext] + 2f * heightMap[x, yNext] + heightMap[xNext, yNext]) -
                           (heightMap[xPrev, yPrev] + 2f * heightMap[x, yPrev] + heightMap[xNext, yPrev]);

                Vector3 n = new Vector3(-dX * strength, -dY * strength, 1.0f).normalized;
                Color nc = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1.0f);
                target.SetPixel(x, y, nc);
            }
        }
        target.Apply();
    }

    private static void SaveTexture(Texture2D tex, string path)
    {
        byte[] bytes = tex.EncodeToPNG();
        File.WriteAllBytes(path, bytes);
        Object.DestroyImmediate(tex);
    }

    // CREATE URP MATERIALS
    private static void CreatePbrMaterials()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) urpLit = Shader.Find("Standard");

        // 1. Rusted Steel Material
        Material matSteel = new Material(urpLit);
        matSteel.name = "PBR_RustedSteel";
        Texture2D steelAlb = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/RustedSteel_Albedo.png");
        Texture2D steelNorm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/RustedSteel_Normal.png");
        Texture2D steelMask = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/RustedSteel_Mask.png");
        if (steelAlb != null) matSteel.SetTexture("_BaseMap", steelAlb);
        if (steelNorm != null)
        {
            matSteel.SetTexture("_BumpMap", steelNorm);
            matSteel.EnableKeyword("_NORMALMAP");
            matSteel.SetFloat("_BumpScale", 1.4f);
        }
        if (steelMask != null)
        {
            matSteel.SetTexture("_MetallicGlossMap", steelMask);
            matSteel.EnableKeyword("_METALLICSPECGLOSSMAP");
        }
        matSteel.SetFloat("_Smoothness", 0.65f);
        matSteel.SetFloat("_Metallic", 0.8f);
        AssetDatabase.CreateAsset(matSteel, $"{MaterialDir}/PBR_RustedSteel.mat");

        // 2. Realistic Moss Material
        Material matMoss = new Material(urpLit);
        matMoss.name = "PBR_Moss";
        Texture2D mossAlb = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/Moss_Albedo.png");
        Texture2D mossNorm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/Moss_Normal.png");
        if (mossAlb != null) matMoss.SetTexture("_BaseMap", mossAlb);
        if (mossNorm != null)
        {
            matMoss.SetTexture("_BumpMap", mossNorm);
            matMoss.EnableKeyword("_NORMALMAP");
            matMoss.SetFloat("_BumpScale", 2.0f);
        }
        matMoss.SetFloat("_Smoothness", 0.05f);
        matMoss.SetFloat("_Metallic", 0.0f);
        AssetDatabase.CreateAsset(matMoss, $"{MaterialDir}/PBR_Moss.mat");

        // 3. Cracked Concrete Material
        Material matConcrete = new Material(urpLit);
        matConcrete.name = "PBR_CrackedConcrete";
        Texture2D concAlb = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/Concrete_Albedo.png");
        Texture2D concNorm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/Concrete_Normal.png");
        Texture2D concMask = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/Concrete_Mask.png");
        if (concAlb != null) matConcrete.SetTexture("_BaseMap", concAlb);
        if (concNorm != null)
        {
            matConcrete.SetTexture("_BumpMap", concNorm);
            matConcrete.EnableKeyword("_NORMALMAP");
            matConcrete.SetFloat("_BumpScale", 0.6f);
        }
        if (concMask != null) matConcrete.SetTexture("_MetallicGlossMap", concMask);
        matConcrete.SetFloat("_Smoothness", 0.35f);
        matConcrete.SetFloat("_Metallic", 0.04f);
        AssetDatabase.CreateAsset(matConcrete, $"{MaterialDir}/PBR_CrackedConcrete.mat");

        // 4. Mud Soil Material
        Material matSoil = new Material(urpLit);
        matSoil.name = "PBR_MudSoil";
        Texture2D soilAlb = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/Soil_Albedo.png");
        Texture2D soilNorm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/Soil_Normal.png");
        if (soilAlb != null) matSoil.SetTexture("_BaseMap", soilAlb);
        if (soilNorm != null)
        {
            matSoil.SetTexture("_BumpMap", soilNorm);
            matSoil.EnableKeyword("_NORMALMAP");
            matSoil.SetFloat("_BumpScale", 1.7f);
        }
        matSoil.SetFloat("_Smoothness", 0.28f);
        matSoil.SetFloat("_Metallic", 0.02f);
        AssetDatabase.CreateAsset(matSoil, $"{MaterialDir}/PBR_MudSoil.mat");

        // 5. Twisted Cable Material
        Material matCable = new Material(urpLit);
        matCable.name = "PBR_SteelCable";
        Texture2D cableAlb = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/Cable_Albedo.png");
        Texture2D cableNorm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/Cable_Normal.png");
        if (cableAlb != null) matCable.SetTexture("_BaseMap", cableAlb);
        if (cableNorm != null)
        {
            matCable.SetTexture("_BumpMap", cableNorm);
            matCable.EnableKeyword("_NORMALMAP");
            matCable.SetFloat("_BumpScale", 2.2f);
        }
        matCable.SetFloat("_Smoothness", 0.6f);
        matCable.SetFloat("_Metallic", 0.9f);
        AssetDatabase.CreateAsset(matCable, $"{MaterialDir}/PBR_SteelCable.mat");

        // 6. Hazard Stripes Material
        Material matHazard = new Material(urpLit);
        matHazard.name = "PBR_HazardWarning";
        Texture2D hazAlb = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/Hazard_Albedo.png");
        Texture2D hazNorm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/Hazard_Normal.png");
        if (hazAlb != null) matHazard.SetTexture("_BaseMap", hazAlb);
        if (hazNorm != null)
        {
            matHazard.SetTexture("_BumpMap", hazNorm);
            matHazard.EnableKeyword("_NORMALMAP");
            matHazard.SetFloat("_BumpScale", 1.2f);
        }
        matHazard.SetFloat("_Smoothness", 0.35f);
        matHazard.SetFloat("_Metallic", 0.3f);
        AssetDatabase.CreateAsset(matHazard, $"{MaterialDir}/PBR_HazardWarning.mat");

        // 7. Glowing Emergency Light Material
        Material matLight = new Material(urpLit);
        matLight.name = "PBR_EmergencyLamp";
        matLight.SetColor("_BaseColor", new Color(1.0f, 0.75f, 0.35f));
        matLight.EnableKeyword("_EMISSION");
        matLight.SetColor("_EmissionColor", new Color(1.0f, 0.65f, 0.2f) * 3.5f);
        matLight.SetFloat("_Smoothness", 0.9f);
        AssetDatabase.CreateAsset(matLight, $"{MaterialDir}/PBR_EmergencyLamp.mat");

        // 8. Fluorescent Tube Light Material (Emissive Cool White)
        Material matFluor = new Material(urpLit);
        matFluor.name = "PBR_FluorescentLamp";
        matFluor.SetColor("_BaseColor", new Color(0.9f, 0.95f, 1.0f));
        matFluor.EnableKeyword("_EMISSION");
        matFluor.SetColor("_EmissionColor", new Color(0.9f, 0.95f, 1.0f) * 4.0f);
        matFluor.SetFloat("_Smoothness", 0.95f);
        AssetDatabase.CreateAsset(matFluor, $"{MaterialDir}/PBR_FluorescentLamp.mat");

        // 9. Emergency Alarm Beacon Material (Emissive Red/Amber)
        Material matBeacon = new Material(urpLit);
        matBeacon.name = "PBR_EmergencyBeacon";
        matBeacon.SetColor("_BaseColor", new Color(1.0f, 0.25f, 0.1f));
        matBeacon.EnableKeyword("_EMISSION");
        matBeacon.SetColor("_EmissionColor", new Color(1.0f, 0.3f, 0.1f) * 4.5f);
        matBeacon.SetFloat("_Smoothness", 0.95f);
        AssetDatabase.CreateAsset(matBeacon, $"{MaterialDir}/PBR_EmergencyBeacon.mat");

        // 10. Dark Industrial Painted Metal (Girders, Ducting, Frames)
        Material matDarkMetal = new Material(urpLit);
        matDarkMetal.name = "PBR_IndustrialMetal";
        matDarkMetal.SetColor("_BaseColor", new Color(0.18f, 0.21f, 0.24f));
        if (concNorm != null)
        {
            matDarkMetal.SetTexture("_BumpMap", concNorm);
            matDarkMetal.EnableKeyword("_NORMALMAP");
            matDarkMetal.SetFloat("_BumpScale", 0.6f);
        }
        matDarkMetal.SetFloat("_Smoothness", 0.45f);
        matDarkMetal.SetFloat("_Metallic", 0.85f);
        AssetDatabase.CreateAsset(matDarkMetal, $"{MaterialDir}/PBR_IndustrialMetal.mat");

        // 11. Concrete Wall Material (Tiled high-detail)
        Material matWall = new Material(matConcrete);
        matWall.name = "PBR_ConcreteWall";
        matWall.SetTextureScale("_BaseMap", new Vector2(4f, 2.5f));
        if (matWall.HasProperty("_BumpMap")) matWall.SetTextureScale("_BumpMap", new Vector2(4f, 2.5f));
        matWall.SetFloat("_Smoothness", 0.18f);
        AssetDatabase.CreateAsset(matWall, $"{MaterialDir}/PBR_ConcreteWall.mat");

        AssetDatabase.SaveAssets();
    }
}
