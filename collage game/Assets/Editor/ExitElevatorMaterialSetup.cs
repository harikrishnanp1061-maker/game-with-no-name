using System.IO;
using UnityEngine;
using UnityEditor;

public static class ExitElevatorMaterialSetup
{
    private const string TextureDir = "Assets/Textures/Elevator";
    private const string MaterialDir = "Assets/Materials/Elevator";

    [MenuItem("Tools/FLIP//FALL/Generate Exit Elevator PBR Materials")]
    public static void GenerateAll()
    {
        if (!Directory.Exists(TextureDir)) Directory.CreateDirectory(TextureDir);
        if (!Directory.Exists(MaterialDir)) Directory.CreateDirectory(MaterialDir);

        Debug.Log("[ExitElevatorMaterialSetup] Starting synthesis of Exit Elevator PBR textures...");

        // 1. Chipped Yellow Paint
        GenerateChippedYellow();

        // 2. 45-degree Diagonal Hazard Stripes
        GenerateHazardStripes();

        // 3. Brushed Weathered Stainless Steel (Sliding Doors)
        GenerateDoorWeathered();

        // 4. Heavy Rusted Gunmetal (Outer Walls & Frame)
        GenerateRustedGunmetal();

        // 5. Dark Iron (Columns, Girders & Brackets)
        GenerateDarkIron();

        // 6. Diamond Tread Plate Floor
        GenerateFloorTread();

        // 7. Clean Stainless Steel Interior
        GenerateInteriorSteel();

        // 8. Worn Power Module Housing
        GeneratePowerModuleWorn();

        // 9. Warning Badge Decal
        GenerateWarningBadge();

        AssetDatabase.Refresh();

        // Build & configure URP Lit Materials
        BuildMaterials();

        // Also setup FuseCell materials
        SetupFuseCellMaterials();

        AssetDatabase.SaveAssets();
        Debug.Log("[ExitElevatorMaterialSetup] All Exit Elevator PBR materials and textures generated successfully!");
    }

    // =========================================================================
    // 1. CHIPPED INDUSTRIAL YELLOW
    // =========================================================================
    private static void GenerateChippedYellow()
    {
        int size = 1024;
        Texture2D albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D normal = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D mask = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float[,] height = new float[size, size];

        Color yellowBase = new Color(0.96f, 0.72f, 0.05f);
        Color yellowDark = new Color(0.82f, 0.55f, 0.04f);
        Color rustDark = new Color(0.12f, 0.10f, 0.09f);
        Color rustOrange = new Color(0.50f, 0.22f, 0.05f);
        Color steelBare = new Color(0.35f, 0.36f, 0.38f);

        for (int y = 0; y < size; y++)
        {
            float ny = (float)y / size;
            for (int x = 0; x < size; x++)
            {
                float nx = (float)x / size;

                // Organic noise for rust chipping (matches Blender Noise Texture scale 3.2, detail 12)
                float n1 = Mathf.PerlinNoise(nx * 4.5f, ny * 4.5f);
                float n2 = Mathf.PerlinNoise(nx * 12.0f + 17.3f, ny * 12.0f + 31.7f);
                float n3 = Mathf.PerlinNoise(nx * 28.0f + 8.1f, ny * 28.0f + 14.5f);
                float chipNoise = n1 * 0.55f + n2 * 0.30f + n3 * 0.15f;

                // Fine scratch noise
                float scratch = Mathf.PerlinNoise(nx * 80.0f, ny * 2.0f);
                float isScratch = scratch > 0.82f ? (scratch - 0.82f) / 0.18f : 0f;

                Color c;
                float met, smooth, h;

                // Chipping threshold
                if (chipNoise < 0.42f)
                {
                    // Chipped bare metal & rust
                    float rustBlend = Mathf.Clamp01((chipNoise - 0.25f) / 0.17f);
                    Color r = Color.Lerp(rustDark, rustOrange, rustBlend);
                    c = Color.Lerp(steelBare, r, Mathf.PerlinNoise(nx * 16f, ny * 16f));
                    met = 0.88f;
                    smooth = 0.38f;
                    h = 0.25f;
                }
                else if (chipNoise < 0.48f)
                {
                    // Chipping edge transition (dark rust ring & peeling paint lip)
                    float edge = (chipNoise - 0.42f) / 0.06f;
                    c = Color.Lerp(rustDark, yellowDark, edge);
                    met = Mathf.Lerp(0.85f, 0.20f, edge);
                    smooth = Mathf.Lerp(0.35f, 0.45f, edge);
                    h = Mathf.Lerp(0.28f, 0.55f, edge);
                }
                else
                {
                    // Yellow painted metal with grime
                    float grime = Mathf.PerlinNoise(nx * 8.0f + 5.0f, ny * 8.0f + 5.0f);
                    c = Color.Lerp(yellowDark, yellowBase, grime);
                    if (isScratch > 0f)
                    {
                        c = Color.Lerp(c, steelBare, isScratch * 0.8f);
                        met = Mathf.Lerp(0.20f, 0.85f, isScratch);
                        smooth = Mathf.Lerp(0.48f, 0.60f, isScratch);
                        h = Mathf.Lerp(0.55f, 0.35f, isScratch);
                    }
                    else
                    {
                        met = 0.20f;
                        smooth = 0.46f;
                        h = 0.55f;
                    }
                }

                albedo.SetPixel(x, y, c);
                mask.SetPixel(x, y, new Color(met, 1.0f, 0f, smooth)); // R=Metallic, G=AO, A=Smoothness
                height[x, y] = h;
            }
        }

        ComputeNormalMap(normal, height, size, 4.0f);

        SaveTexture(albedo, Path.Combine(TextureDir, "Elevator_ChippedYellow_Albedo.png"));
        SaveTexture(normal, Path.Combine(TextureDir, "Elevator_ChippedYellow_Normal.png"));
        SaveTexture(mask, Path.Combine(TextureDir, "Elevator_ChippedYellow_Mask.png"));
    }

    // =========================================================================
    // 2. 45-DEGREE DIAGONAL HAZARD STRIPES
    // =========================================================================
    private static void GenerateHazardStripes()
    {
        int size = 1024;
        Texture2D albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D normal = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D mask = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float[,] height = new float[size, size];

        Color yellowStripe = new Color(1.0f, 0.78f, 0.0f);   // Bold Saturated Industrial Safety Yellow matching Blender
        Color blackStripe = new Color(0.06f, 0.06f, 0.07f);   // Rich Carbon Dark Iron
        Color rustChipping = new Color(0.28f, 0.16f, 0.06f);
        Color steelBare = new Color(0.38f, 0.40f, 0.44f);

        // 16.0 frequency with ascending diagonal (/): produces bold, punchy, high-contrast hazard bars matching Blender render
        float stripeFreq = 16.0f;

        for (int y = 0; y < size; y++)
        {
            float ny = (float)y / size;
            for (int x = 0; x < size; x++)
            {
                float nx = (float)x / size;

                // 45 degree ascending diagonal coordinate matching Blender render (/)
                float diag = (nx - ny + 10.0f) * stripeFreq;
                float stripePhase = diag - Mathf.Floor(diag); // 0 to 1

                // Sharp industrial stripe transition with clean antialiasing
                float stripeFactor = Mathf.SmoothStep(0.46f, 0.54f, stripePhase);

                // Subtle edge weathering noise
                float n1 = Mathf.PerlinNoise(nx * 18f, ny * 18f);
                float n2 = Mathf.PerlinNoise(nx * 45f + 5.2f, ny * 45f + 9.7f);
                float wear = n1 * 0.7f + n2 * 0.3f;

                Color c = Color.Lerp(blackStripe, yellowStripe, stripeFactor);
                float met = Mathf.Lerp(0.35f, 0.15f, stripeFactor);
                float smooth = Mathf.Lerp(0.38f, 0.48f, stripeFactor);
                float h = Mathf.Lerp(0.48f, 0.52f, stripeFactor);

                // Realistic paint chipping at wear points
                if (wear < 0.14f)
                {
                    c = Color.Lerp(rustChipping, steelBare, wear / 0.14f);
                    met = 0.85f;
                    smooth = 0.35f;
                    h = 0.30f;
                }

                albedo.SetPixel(x, y, c);
                mask.SetPixel(x, y, new Color(met, 1.0f, 0f, smooth));
                height[x, y] = h;
            }
        }

        ComputeNormalMap(normal, height, size, 2.5f);

        SaveTexture(albedo, Path.Combine(TextureDir, "Elevator_HazardStripes_Albedo.png"));
        SaveTexture(normal, Path.Combine(TextureDir, "Elevator_HazardStripes_Normal.png"));
        SaveTexture(mask, Path.Combine(TextureDir, "Elevator_HazardStripes_Mask.png"));
    }

    // =========================================================================
    // 3. BRUSHED WEATHERED STAINLESS STEEL (Sliding Doors)
    // =========================================================================
    private static void GenerateDoorWeathered()
    {
        int size = 1024;
        Texture2D albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D normal = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D mask = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float[,] height = new float[size, size];

        Color baseSteel = new Color(0.24f, 0.25f, 0.28f);
        Color brightBrush = new Color(0.48f, 0.50f, 0.54f);
        Color darkScuff = new Color(0.12f, 0.13f, 0.15f);
        Color rustSpeck = new Color(0.45f, 0.22f, 0.08f);

        for (int y = 0; y < size; y++)
        {
            float ny = (float)y / size;
            for (int x = 0; x < size; x++)
            {
                float nx = (float)x / size;

                // Strong vertical sliding friction streaks matching Blender (Scale 1.5, 1.0, 30.0)
                float brush1 = Mathf.PerlinNoise(nx * 8.0f, ny * 180.0f);
                float brush2 = Mathf.PerlinNoise(nx * 24.0f + 13f, ny * 350.0f + 29f);
                float brush = brush1 * 0.65f + brush2 * 0.35f;

                // Large mottled scuffs
                float scuff = Mathf.PerlinNoise(nx * 4.0f + 7f, ny * 4.0f + 11f);

                // Rust flecks near edges and lower third
                float rustNoise = Mathf.PerlinNoise(nx * 20.0f + 55f, ny * 20.0f + 88f);

                Color c = Color.Lerp(darkScuff, brightBrush, brush);
                c = Color.Lerp(c, darkScuff, (1.0f - scuff) * 0.55f);

                float met = 0.92f;
                float smooth = Mathf.Lerp(0.50f, 0.68f, brush);
                float h = brush * 0.5f;

                if (rustNoise < 0.18f)
                {
                    c = Color.Lerp(rustSpeck, c, rustNoise / 0.18f);
                    met = 0.65f;
                    smooth = 0.32f;
                    h = 0.25f;
                }

                albedo.SetPixel(x, y, c);
                mask.SetPixel(x, y, new Color(met, 1.0f, 0f, smooth));
                height[x, y] = h;
            }
        }

        ComputeNormalMap(normal, height, size, 2.5f);

        SaveTexture(albedo, Path.Combine(TextureDir, "Elevator_Door_Weathered_Albedo.png"));
        SaveTexture(normal, Path.Combine(TextureDir, "Elevator_Door_Weathered_Normal.png"));
        SaveTexture(mask, Path.Combine(TextureDir, "Elevator_Door_Weathered_Mask.png"));
    }

    // =========================================================================
    // 4. HEAVY RUSTED GUNMETAL (Outer Walls & Frame)
    // =========================================================================
    private static void GenerateRustedGunmetal()
    {
        int size = 1024;
        Texture2D albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D normal = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D mask = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float[,] height = new float[size, size];

        Color gunmetalDark = new Color(0.16f, 0.17f, 0.19f);
        Color gunmetalMid = new Color(0.25f, 0.27f, 0.30f);
        Color rustDark = new Color(0.32f, 0.14f, 0.05f);
        Color rustOrange = new Color(0.55f, 0.26f, 0.07f);

        for (int y = 0; y < size; y++)
        {
            float ny = (float)y / size;
            for (int x = 0; x < size; x++)
            {
                float nx = (float)x / size;

                float n1 = Mathf.PerlinNoise(nx * 6.0f, ny * 6.0f);
                float n2 = Mathf.PerlinNoise(nx * 18.0f + 12f, ny * 18.0f + 34f);
                float n3 = Mathf.PerlinNoise(nx * 45.0f + 5f, ny * 45.0f + 8f);
                float rustFac = n1 * 0.50f + n2 * 0.35f + n3 * 0.15f;

                Color c;
                float met, smooth, h;

                if (rustFac < 0.40f)
                {
                    // Heavy pitted oxidation
                    float blend = rustFac / 0.40f;
                    c = Color.Lerp(rustDark, rustOrange, blend);
                    met = 0.65f;
                    smooth = 0.30f;
                    h = blend * 0.35f;
                }
                else
                {
                    // Dark gunmetal base
                    float metalBlend = (rustFac - 0.40f) / 0.60f;
                    c = Color.Lerp(gunmetalDark, gunmetalMid, metalBlend);
                    met = 0.92f;
                    smooth = Mathf.Lerp(0.38f, 0.52f, metalBlend);
                    h = 0.45f + metalBlend * 0.15f;
                }

                albedo.SetPixel(x, y, c);
                mask.SetPixel(x, y, new Color(met, 1.0f, 0f, smooth));
                height[x, y] = h;
            }
        }

        ComputeNormalMap(normal, height, size, 5.0f);

        SaveTexture(albedo, Path.Combine(TextureDir, "Elevator_RustedGunmetal_Albedo.png"));
        SaveTexture(normal, Path.Combine(TextureDir, "Elevator_RustedGunmetal_Normal.png"));
        SaveTexture(mask, Path.Combine(TextureDir, "Elevator_RustedGunmetal_Mask.png"));
    }

    // =========================================================================
    // 5. DARK IRON (Columns, Girders & Beams)
    // =========================================================================
    private static void GenerateDarkIron()
    {
        int size = 1024;
        Texture2D albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D normal = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D mask = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float[,] height = new float[size, size];

        Color darkIronBase = new Color(0.11f, 0.11f, 0.13f);
        Color darkIronHighlight = new Color(0.22f, 0.23f, 0.26f);

        for (int y = 0; y < size; y++)
        {
            float ny = (float)y / size;
            for (int x = 0; x < size; x++)
            {
                float nx = (float)x / size;

                float pebble = Mathf.PerlinNoise(nx * 50f, ny * 50f);
                float wave = Mathf.PerlinNoise(nx * 8f + 11f, ny * 8f + 22f);
                float val = pebble * 0.4f + wave * 0.6f;

                Color c = Color.Lerp(darkIronBase, darkIronHighlight, val);
                albedo.SetPixel(x, y, c);
                mask.SetPixel(x, y, new Color(0.95f, 1.0f, 0f, 0.45f));
                height[x, y] = val;
            }
        }

        ComputeNormalMap(normal, height, size, 3.0f);

        SaveTexture(albedo, Path.Combine(TextureDir, "Elevator_DarkIron_Albedo.png"));
        SaveTexture(normal, Path.Combine(TextureDir, "Elevator_DarkIron_Normal.png"));
        SaveTexture(mask, Path.Combine(TextureDir, "Elevator_DarkIron_Mask.png"));
    }

    // =========================================================================
    // 6. DIAMOND TREAD PLATE FLOOR
    // =========================================================================
    private static void GenerateFloorTread()
    {
        int size = 1024;
        Texture2D albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D normal = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D mask = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float[,] height = new float[size, size];

        Color floorBase = new Color(0.24f, 0.26f, 0.28f);
        Color diamondRaised = new Color(0.44f, 0.46f, 0.50f);

        int cells = 16;
        float cellSize = (float)size / cells;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float cx = (x % cellSize) / cellSize; // 0 to 1
                float cy = (y % cellSize) / cellSize; // 0 to 1
                int cellX = (int)(x / cellSize);
                int cellY = (int)(y / cellSize);

                // Alternate rotated diamonds
                bool rot = (cellX + cellY) % 2 == 0;
                float dx = cx - 0.5f;
                float dy = cy - 0.5f;

                if (rot) { float tmp = dx; dx = dy; dy = -tmp; }

                // Diamond lozenge distance
                float dist = Mathf.Abs(dx * 2.2f) + Mathf.Abs(dy * 1.0f);
                float isDiamond = Mathf.Clamp01(1.0f - Mathf.SmoothStep(0.28f, 0.40f, dist));

                Color c = Color.Lerp(floorBase, diamondRaised, isDiamond);
                float h = 0.2f + isDiamond * 0.6f;

                albedo.SetPixel(x, y, c);
                mask.SetPixel(x, y, new Color(0.92f, 1.0f, 0f, 0.48f + isDiamond * 0.15f));
                height[x, y] = h;
            }
        }

        ComputeNormalMap(normal, height, size, 6.0f);

        SaveTexture(albedo, Path.Combine(TextureDir, "Elevator_Floor_Albedo.png"));
        SaveTexture(normal, Path.Combine(TextureDir, "Elevator_Floor_Normal.png"));
        SaveTexture(mask, Path.Combine(TextureDir, "Elevator_Floor_Mask.png"));
    }

    // =========================================================================
    // 7. CLEAN STAINLESS STEEL INTERIOR
    // =========================================================================
    private static void GenerateInteriorSteel()
    {
        int size = 1024;
        Texture2D albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D normal = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D mask = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float[,] height = new float[size, size];

        Color steelLight = new Color(0.68f, 0.70f, 0.73f);
        Color steelDark = new Color(0.55f, 0.57f, 0.60f);

        for (int y = 0; y < size; y++)
        {
            float ny = (float)y / size;
            for (int x = 0; x < size; x++)
            {
                float nx = (float)x / size;
                float grain = Mathf.PerlinNoise(nx * 150f, ny * 3f);
                Color c = Color.Lerp(steelDark, steelLight, grain);

                albedo.SetPixel(x, y, c);
                mask.SetPixel(x, y, new Color(0.92f, 1.0f, 0f, 0.65f));
                height[x, y] = grain * 0.2f;
            }
        }

        ComputeNormalMap(normal, height, size, 1.5f);

        SaveTexture(albedo, Path.Combine(TextureDir, "Elevator_Interior_CleanSteel_Albedo.png"));
        SaveTexture(normal, Path.Combine(TextureDir, "Elevator_Interior_CleanSteel_Normal.png"));
        SaveTexture(mask, Path.Combine(TextureDir, "Elevator_Interior_CleanSteel_Mask.png"));
    }

    // =========================================================================
    // 8. WORN POWER MODULE HOUSING
    // =========================================================================
    private static void GeneratePowerModuleWorn()
    {
        int size = 1024;
        Texture2D albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D normal = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D mask = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float[,] height = new float[size, size];

        Color yellowBase = new Color(0.88f, 0.66f, 0.12f);
        Color sootDark = new Color(0.18f, 0.17f, 0.16f);
        Color steelBare = new Color(0.40f, 0.42f, 0.45f);

        for (int y = 0; y < size; y++)
        {
            float ny = (float)y / size;
            for (int x = 0; x < size; x++)
            {
                float nx = (float)x / size;
                float n = Mathf.PerlinNoise(nx * 12f, ny * 12f);
                float soot = Mathf.PerlinNoise(nx * 4f + 3f, ny * 4f + 7f);

                Color c = yellowBase;
                if (n < 0.35f) c = Color.Lerp(steelBare, c, n / 0.35f);
                c = Color.Lerp(c, sootDark, (1.0f - soot) * 0.5f);

                albedo.SetPixel(x, y, c);
                mask.SetPixel(x, y, new Color(0.35f, 1.0f, 0f, 0.42f));
                height[x, y] = n * 0.4f;
            }
        }

        ComputeNormalMap(normal, height, size, 3.0f);

        SaveTexture(albedo, Path.Combine(TextureDir, "Elevator_PowerModule_Worn_Albedo.png"));
        SaveTexture(normal, Path.Combine(TextureDir, "Elevator_PowerModule_Worn_Normal.png"));
        SaveTexture(mask, Path.Combine(TextureDir, "Elevator_PowerModule_Worn_Mask.png"));
    }

    // =========================================================================
    // 9. WARNING BADGE
    // =========================================================================
    private static void GenerateWarningBadge()
    {
        int size = 512;
        Texture2D albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color yellow = new Color(0.95f, 0.72f, 0.05f);
        Color black = new Color(0.12f, 0.12f, 0.13f);

        for (int y = 0; y < size; y++)
        {
            float ny = (float)y / size;
            for (int x = 0; x < size; x++)
            {
                float nx = (float)x / size;

                // Triangle warning symbol
                float tx = Mathf.Abs(nx - 0.5f) * 2.0f; // 0 to 1
                float ty = ny; // 0 to 1

                bool insideTriangle = ty >= 0.15f && ty <= 0.85f && tx <= (ty - 0.15f) / 0.70f;
                bool innerTriangle = ty >= 0.22f && ty <= 0.80f && tx <= (ty - 0.22f) / 0.58f;

                // Exclamation point in center
                bool isExclamation = innerTriangle && (
                    (tx < 0.08f && ty >= 0.40f && ty <= 0.70f) ||
                    (tx < 0.08f && ty >= 0.28f && ty <= 0.34f)
                );

                Color c;
                if (insideTriangle && !innerTriangle) c = black;
                else if (isExclamation) c = black;
                else if (innerTriangle) c = yellow;
                else c = yellow * 0.85f;

                albedo.SetPixel(x, y, c);
            }
        }

        SaveTexture(albedo, Path.Combine(TextureDir, "Elevator_WarningBadge_Albedo.png"));
    }

    // =========================================================================
    // BUILD URP LIT MATERIALS
    // =========================================================================
    public static void BuildMaterials()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) urpLit = Shader.Find("Standard");

        // 1. M_Elevator_ChippedYellow
        CreateOrUpdateMaterial("M_Elevator_ChippedYellow", urpLit,
            "Elevator_ChippedYellow_Albedo", "Elevator_ChippedYellow_Normal", "Elevator_ChippedYellow_Mask",
            new Color(0.92f, 0.70f, 0.08f), 0.20f, 0.46f);

        // 2. M_Elevator_HazardStripes
        CreateOrUpdateMaterial("M_Elevator_HazardStripes", urpLit,
            "Elevator_HazardStripes_Albedo", "Elevator_HazardStripes_Normal", "Elevator_HazardStripes_Mask",
            Color.white, 0.25f, 0.42f);

        // 3. M_Elevator_Door_Weathered
        CreateOrUpdateMaterial("M_Elevator_Door_Weathered", urpLit,
            "Elevator_Door_Weathered_Albedo", "Elevator_Door_Weathered_Normal", "Elevator_Door_Weathered_Mask",
            new Color(0.38f, 0.40f, 0.44f), 0.94f, 0.65f);

        // 4. M_Elevator_RustedGunmetal
        CreateOrUpdateMaterial("M_Elevator_RustedGunmetal", urpLit,
            "Elevator_RustedGunmetal_Albedo", "Elevator_RustedGunmetal_Normal", "Elevator_RustedGunmetal_Mask",
            new Color(0.20f, 0.21f, 0.24f), 0.90f, 0.40f);

        // 5. M_Elevator_DarkIron
        CreateOrUpdateMaterial("M_Elevator_DarkIron", urpLit,
            "Elevator_DarkIron_Albedo", "Elevator_DarkIron_Normal", "Elevator_DarkIron_Mask",
            new Color(0.14f, 0.14f, 0.16f), 0.95f, 0.45f);

        // 6. M_Elevator_Floor
        CreateOrUpdateMaterial("M_Elevator_Floor", urpLit,
            "Elevator_Floor_Albedo", "Elevator_Floor_Normal", "Elevator_Floor_Mask",
            new Color(0.28f, 0.30f, 0.33f), 0.92f, 0.52f);

        // 7. M_Elevator_Interior_CleanSteel
        CreateOrUpdateMaterial("M_Elevator_Interior_CleanSteel", urpLit,
            "Elevator_Interior_CleanSteel_Albedo", "Elevator_Interior_CleanSteel_Normal", "Elevator_Interior_CleanSteel_Mask",
            new Color(0.68f, 0.70f, 0.73f), 0.92f, 0.65f);

        // 8. M_Elevator_PowerModule_Worn
        CreateOrUpdateMaterial("M_Elevator_PowerModule_Worn", urpLit,
            "Elevator_PowerModule_Worn_Albedo", "Elevator_PowerModule_Worn_Normal", "Elevator_PowerModule_Worn_Mask",
            new Color(0.85f, 0.65f, 0.12f), 0.35f, 0.42f);

        // 9. M_Warning_Badge
        CreateOrUpdateMaterial("M_Warning_Badge", urpLit,
            "Elevator_WarningBadge_Albedo", null, null,
            new Color(0.92f, 0.70f, 0.08f), 0.15f, 0.50f);

        // 10. M_Elevator_AmberLamp (Glowing Marquee Chevrons & Fixture Lenses)
        Material amberLamp = GetOrCreateMaterial("M_Elevator_AmberLamp", urpLit);
        amberLamp.SetColor("_BaseColor", new Color(1.0f, 0.80f, 0.25f, 1.0f));
        amberLamp.SetFloat("_Smoothness", 0.85f);
        amberLamp.SetFloat("_Metallic", 0.0f);
        amberLamp.EnableKeyword("_EMISSION");
        amberLamp.SetColor("_EmissionColor", new Color(1.0f, 0.72f, 0.12f) * 5.0f);
        EditorUtility.SetDirty(amberLamp);

        // 11. M_Elevator_GreenLED (Power Module Socket Indicator LEDs)
        Material greenLED = GetOrCreateMaterial("M_Elevator_GreenLED", urpLit);
        greenLED.SetColor("_BaseColor", new Color(0.15f, 1.0f, 0.35f, 1.0f));
        greenLED.SetFloat("_Smoothness", 0.90f);
        greenLED.SetFloat("_Metallic", 0.0f);
        greenLED.EnableKeyword("_EMISSION");
        greenLED.SetColor("_EmissionColor", new Color(0.15f, 1.0f, 0.35f) * 6.0f);
        EditorUtility.SetDirty(greenLED);
    }

    private static void CreateOrUpdateMaterial(string matName, Shader shader, string albedoName, string normalName, string maskName, Color fallbackCol, float fallbackMet, float fallbackSmooth)
    {
        Material m = GetOrCreateMaterial(matName, shader);
        m.SetColor("_BaseColor", fallbackCol);
        m.SetFloat("_Metallic", fallbackMet);
        m.SetFloat("_Smoothness", fallbackSmooth);

        if (!string.IsNullOrEmpty(albedoName))
        {
            Texture2D albedoTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Path.Combine(TextureDir, albedoName + ".png"));
            if (albedoTex != null)
            {
                m.SetTexture("_BaseMap", albedoTex);
                m.SetColor("_BaseColor", Color.white);
            }
        }

        if (!string.IsNullOrEmpty(normalName))
        {
            Texture2D normTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Path.Combine(TextureDir, normalName + ".png"));
            if (normTex != null)
            {
                m.EnableKeyword("_NORMALMAP");
                m.SetTexture("_BumpMap", normTex);
                m.SetFloat("_BumpScale", 1.0f);
            }
        }

        if (!string.IsNullOrEmpty(maskName))
        {
            Texture2D maskTex = AssetDatabase.LoadAssetAtPath<Texture2D>(Path.Combine(TextureDir, maskName + ".png"));
            if (maskTex != null)
            {
                m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetTexture("_MetallicGlossMap", maskTex);
            }
        }

        EditorUtility.SetDirty(m);
    }

    private static Material GetOrCreateMaterial(string name, Shader shader)
    {
        string path = Path.Combine(MaterialDir, name + ".mat");
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(shader);
            AssetDatabase.CreateAsset(m, path);
        }
        else
        {
            m.shader = shader;
        }
        return m;
    }

    // =========================================================================
    // FUSE CELL MATERIALS SETUP
    // =========================================================================
    public static void SetupFuseCellMaterials()
    {
        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
        if (urpLit == null) urpLit = Shader.Find("Standard");

        string fusePath = "Assets/ReadyMadeAssets/FuseCell/SM_FuseCell.fbx";
        Object[] fuseAssets = AssetDatabase.LoadAllAssetsAtPath(fusePath);
        for (int i = 0; i < fuseAssets.Length; i++)
        {
            Material m = fuseAssets[i] as Material;
            if (m == null) continue;

            if (m.name == "M_FuseCell_EnergyCore")
            {
                m.SetColor("_BaseColor", new Color(0.2f, 1.0f, 0.4f, 1f));
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(0.15f, 1.0f, 0.35f) * 6.5f);
            }
            else if (m.name == "M_FuseCell_Indicator")
            {
                m.SetColor("_BaseColor", new Color(0.2f, 1.0f, 0.4f, 1f));
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(0.15f, 1.0f, 0.35f) * 5.0f);
            }
            else if (m.name == "M_FuseCell_HazardYellow")
            {
                m.SetColor("_BaseColor", new Color(0.92f, 0.70f, 0.08f, 1f));
                m.SetFloat("_Metallic", 0.20f);
                m.SetFloat("_Smoothness", 0.45f);
            }
            else if (m.name == "M_FuseCell_Gunmetal")
            {
                m.SetColor("_BaseColor", new Color(0.16f, 0.17f, 0.19f, 1f));
                m.SetFloat("_Metallic", 0.95f);
                m.SetFloat("_Smoothness", 0.50f);
            }
            else if (m.name == "M_FuseCell_Glass" || m.name == "M_FuseCell_Liquid" || m.name == "M_FuseCell_Bubble")
            {
                m.SetColor("_BaseColor", new Color(0.15f, 0.95f, 0.40f, 0.75f));
                m.SetFloat("_Smoothness", 0.95f);
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", new Color(0.15f, 0.85f, 0.30f) * 1.5f);
            }
            EditorUtility.SetDirty(m);
        }
    }

    // =========================================================================
    // APPLY MATERIALS TO ELEVATOR GAMEOBJECT IN SCENE
    // =========================================================================
    public static void ApplyMaterialsToElevator(GameObject elevatorRoot)
    {
        if (elevatorRoot == null) return;

        MeshRenderer[] renderers = elevatorRoot.GetComponentsInChildren<MeshRenderer>(true);
        foreach (MeshRenderer r in renderers)
        {
            Material[] mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (mats[i] == null) continue;
                string cleanName = mats[i].name.Replace(" (Instance)", "");

                // Check for vertical door lights or special fixtures
                if (r.name.Contains("LightLens") || r.name.Contains("LightFixture"))
                {
                    Material lensMat = AssetDatabase.LoadAssetAtPath<Material>(Path.Combine(MaterialDir, "M_Elevator_AmberLamp.mat"));
                    if (lensMat != null) { mats[i] = lensMat; changed = true; }
                    continue;
                }

                Material pbrMat = AssetDatabase.LoadAssetAtPath<Material>(Path.Combine(MaterialDir, cleanName + ".mat"));
                if (pbrMat != null)
                {
                    mats[i] = pbrMat;
                    changed = true;
                }
            }
            if (changed)
            {
                r.sharedMaterials = mats;
                EditorUtility.SetDirty(r);
            }
        }
    }

    // =========================================================================
    // UTILITIES
    // =========================================================================
    private static void ComputeNormalMap(Texture2D normal, float[,] height, int size, float strength)
    {
        for (int y = 0; y < size; y++)
        {
            int yPrev = (y - 1 + size) % size;
            int yNext = (y + 1) % size;
            for (int x = 0; x < size; x++)
            {
                int xPrev = (x - 1 + size) % size;
                int xNext = (x + 1) % size;

                // Sobel filter
                float dX = (height[xNext, y] - height[xPrev, y]) * strength;
                float dY = (height[x, yNext] - height[x, yPrev]) * strength;

                Vector3 norm = new Vector3(-dX, -dY, 1.0f).normalized;
                Color col = new Color(norm.x * 0.5f + 0.5f, norm.y * 0.5f + 0.5f, norm.z * 0.5f + 0.5f, 1.0f);
                normal.SetPixel(x, y, col);
            }
        }
    }

    private static void SaveTexture(Texture2D tex, string path)
    {
        tex.Apply();
        byte[] bytes = tex.EncodeToPNG();
        File.WriteAllBytes(path, bytes);

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null)
        {
            if (path.Contains("Normal"))
            {
                importer.textureType = TextureImporterType.NormalMap;
            }
            else if (path.Contains("Mask"))
            {
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.FromInput;
            }
            importer.SaveAndReimport();
        }
    }
}
