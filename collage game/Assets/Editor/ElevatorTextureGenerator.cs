using System.IO;
using UnityEngine;
using UnityEditor;

public static class ElevatorTextureGenerator
{
    private const string TextureDir = "Assets/Textures";
    private const string MaterialDir = "Assets/Materials";

    [MenuItem("Tools/FLIP//FALL/Generate Elevator PBR Textures")]
    public static void GenerateAllElevatorTextures()
    {
        if (!Directory.Exists(TextureDir)) Directory.CreateDirectory(TextureDir);
        if (!Directory.Exists(MaterialDir)) Directory.CreateDirectory(MaterialDir);

        Debug.Log("[ElevatorTextureGenerator] Starting generation of high-detail Elevator PBR textures...");

        // 1. Scratched Battered Industrial Steel (Cab exterior, uprights, frame)
        GenerateElevatorSteel();

        // 2. Elevator Interior Wall Paneling (Ribbed institutional stainless steel)
        GenerateElevatorInterior();

        // 3. Diamond Tread-Plate Steel (Elevator floor & hatch)
        GenerateTreadPlate();

        // 4. Elevator Sliding Doors (Brushed steel with rubber safety seals & kickplate)
        GenerateElevatorDoors();

        // 5. Car Operating Panel (COP) with illuminated buttons & speaker grille
        GenerateElevatorCOP();

        // 6. Ribbed Concrete Rebar (Ceiling reinforcement rods)
        GenerateRebar();

        AssetDatabase.Refresh();

        // Configure Texture Importers & Build Materials
        ConfigureAndCreateMaterials();

        Debug.Log("[ElevatorTextureGenerator] All Elevator PBR textures and materials successfully generated!");
    }

    // =========================================================================
    // 1. ELEVATOR STEEL (Scratched, Dented, Battered Industrial Steel)
    // =========================================================================
    private static void GenerateElevatorSteel()
    {
        int size = 1024;
        Texture2D albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D normal = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D mask = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float[,] height = new float[size, size];

        Color baseSteel = new Color(0.44f, 0.46f, 0.49f);
        Color darkScuff = new Color(0.24f, 0.25f, 0.27f);
        Color brightScratch = new Color(0.72f, 0.74f, 0.78f);
        Color rivetCol = new Color(0.55f, 0.57f, 0.60f);

        // Precompute random scratches (start, end, width)
        System.Random rand = new System.Random(4242);
        int numScratches = 40;
        float[,] scratches = new float[numScratches, 5]; // x1, y1, x2, y2, intensity
        for (int i = 0; i < numScratches; i++)
        {
            scratches[i, 0] = (float)rand.NextDouble();
            scratches[i, 1] = (float)rand.NextDouble();
            float len = 0.08f + (float)rand.NextDouble() * 0.22f;
            float angle = (float)rand.NextDouble() * Mathf.PI * 2f;
            scratches[i, 2] = scratches[i, 0] + Mathf.Cos(angle) * len;
            scratches[i, 3] = scratches[i, 1] + Mathf.Sin(angle) * len;
            scratches[i, 4] = 0.5f + (float)rand.NextDouble() * 0.5f;
        }

        for (int y = 0; y < size; y++)
        {
            float v = y / (float)size;
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;

                // Fine vertical brushed metal grain
                float grain = Mathf.Sin(u * 1200f) * 0.035f + Mathf.Sin(u * 3200f) * 0.015f;
                float broadWear = Mathf.PerlinNoise(u * 4f, v * 4f) * 0.12f - 0.06f;
                float grime = Mathf.PerlinNoise(u * 8f + 10f, v * 8f + 10f);

                // Beveled panel border seam (inset 0.04 from edges)
                float edgeDistX = Mathf.Min(u, 1f - u);
                float edgeDistY = Mathf.Min(v, 1f - v);
                float borderSeam = 1f;
                if (edgeDistX < 0.02f || edgeDistY < 0.02f) borderSeam = 0.65f;
                else if (edgeDistX < 0.035f || edgeDistY < 0.035f) borderSeam = 0.85f;

                // Corner rivets (at 0.05, 0.95)
                float rivet = 0f;
                float[] rivetPos = { 0.05f, 0.95f };
                foreach (float rx in rivetPos)
                {
                    foreach (float ry in rivetPos)
                    {
                        float d = Mathf.Sqrt((u - rx) * (u - rx) + (v - ry) * (v - ry));
                        if (d < 0.022f)
                        {
                            rivet = Mathf.Cos((d / 0.022f) * (Mathf.PI * 0.5f));
                        }
                    }
                }

                // Scratch distance
                float scratchEffect = 0f;
                for (int i = 0; i < numScratches; i++)
                {
                    float d = DistToSegment(u, v, scratches[i, 0], scratches[i, 1], scratches[i, 2], scratches[i, 3]);
                    if (d < 0.0035f)
                    {
                        float f = 1f - (d / 0.0035f);
                        scratchEffect = Mathf.Max(scratchEffect, f * scratches[i, 4]);
                    }
                }

                // Calculate albedo
                Color c = Color.Lerp(baseSteel + new Color(broadWear, broadWear, broadWear) + new Color(grain, grain, grain), darkScuff, grime * 0.25f);
                c *= borderSeam;
                if (rivet > 0.01f) c = Color.Lerp(c, rivetCol, rivet);
                if (scratchEffect > 0.01f) c = Color.Lerp(c, brightScratch, scratchEffect * 0.85f);

                albedo.SetPixel(x, y, c);

                // Height map for normal calculation
                float h = 0.5f + grain * 0.5f + rivet * 0.45f - (1f - borderSeam) * 0.35f + scratchEffect * 0.2f;
                height[x, y] = h;

                // Mask: R = Metallic (0.88), G = Occlusion, B = 0, A = Smoothness
                float metallic = 0.88f;
                float occ = borderSeam * (1f - grime * 0.2f);
                float smoothness = Mathf.Clamp01(0.60f + grain * 2f - scratchEffect * 0.25f + (rivet > 0.1f ? 0.15f : 0f));
                mask.SetPixel(x, y, new Color(metallic, occ, 0f, smoothness));
            }
        }

        ComputeNormalMapSobel(height, normal, 2.5f);

        SaveTexturePNG(albedo, Path.Combine(TextureDir, "ElevatorSteel_Albedo.png"));
        SaveTexturePNG(normal, Path.Combine(TextureDir, "ElevatorSteel_Normal.png"));
        SaveTexturePNG(mask, Path.Combine(TextureDir, "ElevatorSteel_Mask.png"));
    }

    // =========================================================================
    // 2. ELEVATOR INTERIOR (Ribbed Institutional Paneling)
    // =========================================================================
    private static void GenerateElevatorInterior()
    {
        int size = 1024;
        Texture2D albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D normal = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D mask = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float[,] height = new float[size, size];

        Color bandA = new Color(0.36f, 0.38f, 0.41f);
        Color bandB = new Color(0.31f, 0.33f, 0.36f);
        Color grooveCol = new Color(0.18f, 0.19f, 0.21f);

        for (int y = 0; y < size; y++)
        {
            float v = y / (float)size;
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;

                // Horizontal repeating architectural panel bands (every 0.125 v)
                float bandV = (v * 8f) % 1.0f;
                float groove = 1f;
                if (bandV < 0.08f) groove = bandV / 0.08f;
                else if (bandV > 0.92f) groove = (1f - bandV) / 0.08f;

                // Horizontal brushed streaks
                float grain = Mathf.Sin(v * 1600f) * 0.025f;
                float fineGrain = Mathf.Sin(v * 3800f) * 0.015f;

                // Alternating panel tones
                int bandIndex = Mathf.FloorToInt(v * 8f);
                Color baseCol = (bandIndex % 2 == 0) ? bandA : bandB;

                // Vertical divider line in center
                float centerSeam = Mathf.Abs(u - 0.5f);
                float seamFactor = centerSeam < 0.008f ? (centerSeam / 0.008f) : 1f;

                Color c = (baseCol + new Color(grain + fineGrain, grain + fineGrain, grain + fineGrain)) * groove * seamFactor;
                albedo.SetPixel(x, y, c);

                float h = 0.5f + (bandIndex % 2 == 0 ? 0.05f : -0.05f) - (1f - groove) * 0.4f - (1f - seamFactor) * 0.3f;
                height[x, y] = h;

                float metallic = 0.78f;
                float occ = groove * seamFactor;
                float smoothness = Mathf.Clamp01(0.55f + grain * 3f) * groove;
                mask.SetPixel(x, y, new Color(metallic, occ, 0f, smoothness));
            }
        }

        ComputeNormalMapSobel(height, normal, 2.0f);

        SaveTexturePNG(albedo, Path.Combine(TextureDir, "ElevatorInterior_Albedo.png"));
        SaveTexturePNG(normal, Path.Combine(TextureDir, "ElevatorInterior_Normal.png"));
        SaveTexturePNG(mask, Path.Combine(TextureDir, "ElevatorInterior_Mask.png"));
    }

    // =========================================================================
    // 3. DIAMOND TREAD-PLATE (Elevator Floor & Hatch Plate)
    // =========================================================================
    private static void GenerateTreadPlate()
    {
        int size = 1024;
        Texture2D albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D normal = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D mask = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float[,] height = new float[size, size];

        Color plateBase = new Color(0.28f, 0.30f, 0.33f);
        Color diamondTop = new Color(0.68f, 0.70f, 0.74f); // Worn bright specular steel
        Color valleyDark = new Color(0.18f, 0.19f, 0.20f);

        int cells = 16; // 16x16 grid of diamond pairs
        for (int y = 0; y < size; y++)
        {
            float v = y / (float)size;
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;

                float cellU = (u * cells) % 1.0f - 0.5f;
                float cellV = (v * cells) % 1.0f - 0.5f;
                int ix = Mathf.FloorToInt(u * cells);
                int iy = Mathf.FloorToInt(v * cells);

                // Alternating orientation: even cells rotated +45 deg, odd cells -45 deg
                bool flip = (ix + iy) % 2 == 0;
                float rotU = flip ? (cellU + cellV) * 0.7071f : (cellU - cellV) * 0.7071f;
                float rotV = flip ? (-cellU + cellV) * 0.7071f : (cellU + cellV) * 0.7071f;

                // Elliptical diamond profile
                float d = (rotU * rotU) / (0.28f * 0.28f) + (rotV * rotV) / (0.10f * 0.10f);
                float diamondHeight = 0f;
                if (d < 1f)
                {
                    diamondHeight = Mathf.Cos(Mathf.Sqrt(d) * (Mathf.PI * 0.5f));
                }

                Color c = Color.Lerp(valleyDark, plateBase, 0.6f);
                c = Color.Lerp(c, diamondTop, diamondHeight * 0.9f);

                albedo.SetPixel(x, y, c);
                height[x, y] = diamondHeight;

                float metallic = 0.92f;
                float occ = Mathf.Lerp(0.5f, 1f, diamondHeight);
                float smoothness = Mathf.Lerp(0.38f, 0.82f, diamondHeight);
                mask.SetPixel(x, y, new Color(metallic, occ, 0f, smoothness));
            }
        }

        ComputeNormalMapSobel(height, normal, 4.0f);

        SaveTexturePNG(albedo, Path.Combine(TextureDir, "ElevatorTreadPlate_Albedo.png"));
        SaveTexturePNG(normal, Path.Combine(TextureDir, "ElevatorTreadPlate_Normal.png"));
        SaveTexturePNG(mask, Path.Combine(TextureDir, "ElevatorTreadPlate_Mask.png"));
    }

    // =========================================================================
    // 4. ELEVATOR DOORS (Sliding Stainless Doors with Safety Edge & Kickplate)
    // =========================================================================
    private static void GenerateElevatorDoors()
    {
        int size = 1024;
        Texture2D albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D normal = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D mask = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float[,] height = new float[size, size];

        Color doorSteel = new Color(0.48f, 0.50f, 0.53f);
        Color rubberSeal = new Color(0.12f, 0.12f, 0.13f);
        Color kickplateCol = new Color(0.38f, 0.40f, 0.43f);
        Color hazardYellow = new Color(0.92f, 0.72f, 0.12f);
        Color hazardBlack = new Color(0.14f, 0.14f, 0.16f);

        for (int y = 0; y < size; y++)
        {
            float v = y / (float)size;
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;

                // Vertical brushed grain
                float grain = Mathf.Sin(u * 1400f) * 0.03f;

                // Central Rubber safety bumper (left edge of right door, or right edge of left door: u < 0.06)
                bool isRubber = (u < 0.05f);

                // Lower kickplate (v < 0.22)
                bool isKickplate = (v < 0.22f);

                // Hazard Warning Stripe band on kickplate header (v between 0.20 and 0.23)
                bool isHazard = (v >= 0.20f && v <= 0.23f) && !isRubber;

                // Center recessed decorative groove (u between 0.48 and 0.52)
                float groove = 1f;
                if (!isRubber && Mathf.Abs(u - 0.5f) < 0.02f)
                {
                    groove = Mathf.Abs(u - 0.5f) / 0.02f;
                }

                Color c;
                float h = 0.5f;
                float metallic = 0.88f;
                float smoothness = 0.65f;

                if (isRubber)
                {
                    c = rubberSeal;
                    h = 0.65f; // Raised rounded bumper profile
                    float rDist = u / 0.05f;
                    h += Mathf.Sin(rDist * Mathf.PI) * 0.2f;
                    metallic = 0.02f;
                    smoothness = 0.25f;
                }
                else if (isHazard)
                {
                    float stripe = Mathf.Sin((u + v) * 55f);
                    c = stripe > 0f ? hazardYellow : hazardBlack;
                    metallic = stripe > 0f ? 0.1f : 0.05f;
                    smoothness = 0.45f;
                }
                else if (isKickplate)
                {
                    float rib = Mathf.Sin(v * 400f) * 0.04f;
                    c = kickplateCol + new Color(rib, rib, rib);
                    h = 0.54f + rib;
                    metallic = 0.90f;
                    smoothness = 0.50f;
                }
                else
                {
                    c = (doorSteel + new Color(grain, grain, grain)) * groove;
                    h = 0.5f - (1f - groove) * 0.35f;
                    metallic = 0.88f;
                    smoothness = 0.68f + grain * 2f;
                }

                albedo.SetPixel(x, y, c);
                height[x, y] = h;
                mask.SetPixel(x, y, new Color(metallic, groove, 0f, smoothness));
            }
        }

        ComputeNormalMapSobel(height, normal, 3.0f);

        SaveTexturePNG(albedo, Path.Combine(TextureDir, "ElevatorDoors_Albedo.png"));
        SaveTexturePNG(normal, Path.Combine(TextureDir, "ElevatorDoors_Normal.png"));
        SaveTexturePNG(mask, Path.Combine(TextureDir, "ElevatorDoors_Mask.png"));
    }

    // =========================================================================
    // 5. CAR OPERATING PANEL (COP with Buttons, Indicators & Grille)
    // =========================================================================
    private static void GenerateElevatorCOP()
    {
        int size = 1024;
        Texture2D albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D normal = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D mask = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D emission = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float[,] height = new float[size, size];

        Color plateSteel = new Color(0.50f, 0.52f, 0.55f);
        Color buttonRim = new Color(0.78f, 0.80f, 0.84f);
        Color buttonCenter = new Color(0.20f, 0.22f, 0.25f);
        Color displayBack = new Color(0.06f, 0.08f, 0.10f);
        Color glowAmber = new Color(1.0f, 0.60f, 0.10f);
        Color glowRed = new Color(0.95f, 0.15f, 0.10f);

        for (int y = 0; y < size; y++)
        {
            float v = y / (float)size;
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;

                // Base brushed plate
                float grain = Mathf.Sin(v * 1800f) * 0.02f;
                Color c = plateSteel + new Color(grain, grain, grain);
                float h = 0.5f;
                float metallic = 0.88f;
                float smoothness = 0.72f;
                Color em = Color.black;

                // Digital Floor Indicator Display at top (v: 0.80 to 0.94, u: 0.20 to 0.80)
                if (v >= 0.80f && v <= 0.94f && u >= 0.20f && u <= 0.80f)
                {
                    c = displayBack;
                    h = 0.45f;
                    metallic = 0.1f;
                    smoothness = 0.85f;
                    // Draw digital numeral "1" or warning "!"
                    if (Mathf.Abs(u - 0.50f) < 0.025f && v >= 0.83f && v <= 0.91f)
                    {
                        c = glowAmber;
                        em = glowAmber * 2.5f;
                    }
                }

                // Push Button Matrix (Rows from v = 0.35 to 0.75, 2 columns at u = 0.38, 0.62)
                float[] colX = { 0.38f, 0.62f };
                for (int r = 0; r < 5; r++)
                {
                    float rowY = 0.35f + r * 0.08f;
                    foreach (float bx in colX)
                    {
                        float d = Mathf.Sqrt((u - bx) * (u - bx) + (v - rowY) * (v - rowY));
                        if (d < 0.032f) // Outer bezel ring
                        {
                            if (d > 0.025f)
                            {
                                c = buttonRim;
                                h = 0.62f;
                                metallic = 0.95f;
                                smoothness = 0.88f;
                            }
                            else // Button surface
                            {
                                c = buttonCenter;
                                h = 0.58f;
                                metallic = 0.20f;
                                smoothness = 0.60f;
                                // Glowing selected button (e.g. Ground floor or Alarm)
                                if ((r == 0 && bx > 0.5f) || (r == 4 && bx < 0.5f))
                                {
                                    c = (r == 0) ? glowAmber : glowRed;
                                    em = c * 3.0f;
                                }
                            }
                        }
                    }
                }

                // Speaker phone grille slots (v: 0.12 to 0.24, u: 0.30 to 0.70)
                if (v >= 0.12f && v <= 0.24f && u >= 0.30f && u <= 0.70f)
                {
                    float slotX = (u * 30f) % 1.0f;
                    float slotY = (v * 20f) % 1.0f;
                    if (slotX < 0.35f && slotY < 0.35f)
                    {
                        c = Color.black;
                        h = 0.30f;
                        smoothness = 0.1f;
                    }
                }

                albedo.SetPixel(x, y, c);
                height[x, y] = h;
                mask.SetPixel(x, y, new Color(metallic, 1f, 0f, smoothness));
                emission.SetPixel(x, y, em);
            }
        }

        ComputeNormalMapSobel(height, normal, 3.5f);

        SaveTexturePNG(albedo, Path.Combine(TextureDir, "ElevatorCOP_Albedo.png"));
        SaveTexturePNG(normal, Path.Combine(TextureDir, "ElevatorCOP_Normal.png"));
        SaveTexturePNG(mask, Path.Combine(TextureDir, "ElevatorCOP_Mask.png"));
        SaveTexturePNG(emission, Path.Combine(TextureDir, "ElevatorCOP_Emission.png"));
    }

    // =========================================================================
    // 6. RIBBED CONCRETE REBAR
    // =========================================================================
    private static void GenerateRebar()
    {
        int size = 512;
        Texture2D albedo = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D normal = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Texture2D mask = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float[,] height = new float[size, size];

        Color darkSteel = new Color(0.24f, 0.23f, 0.22f);
        Color ribHighlight = new Color(0.38f, 0.37f, 0.36f);
        Color concreteDust = new Color(0.55f, 0.54f, 0.52f);

        for (int y = 0; y < size; y++)
        {
            float v = y / (float)size;
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size;

                // Helical / annular rebar ridges across length (v)
                float ribPattern = Mathf.Sin((v * 24f + u * 2f) * Mathf.PI * 2f);
                float ribProfile = Mathf.Clamp01(ribPattern * 1.5f);

                // Longitudinal spine ribs (left & right)
                float spine = Mathf.Max(Mathf.Clamp01(1f - Mathf.Abs(u - 0.25f) * 12f), Mathf.Clamp01(1f - Mathf.Abs(u - 0.75f) * 12f));

                float totalRib = Mathf.Clamp01(ribProfile + spine);

                Color c = Color.Lerp(darkSteel, ribHighlight, totalRib);
                // Concrete dust deposit in grooves
                if (totalRib < 0.2f) c = Color.Lerp(c, concreteDust, 0.4f);

                albedo.SetPixel(x, y, c);
                height[x, y] = totalRib * 0.6f;

                float metallic = 0.85f;
                float occ = Mathf.Lerp(0.5f, 1f, totalRib);
                float smoothness = Mathf.Lerp(0.25f, 0.60f, totalRib);
                mask.SetPixel(x, y, new Color(metallic, occ, 0f, smoothness));
            }
        }

        ComputeNormalMapSobel(height, normal, 4.0f);

        SaveTexturePNG(albedo, Path.Combine(TextureDir, "RebarRibbed_Albedo.png"));
        SaveTexturePNG(normal, Path.Combine(TextureDir, "RebarRibbed_Normal.png"));
        SaveTexturePNG(mask, Path.Combine(TextureDir, "RebarRibbed_Mask.png"));
    }

    // =========================================================================
    // MATERIAL COMPILATION & SETUP
    // =========================================================================
    private static void ConfigureAndCreateMaterials()
    {
        string[] normals = {
            "ElevatorSteel_Normal.png",
            "ElevatorInterior_Normal.png",
            "ElevatorTreadPlate_Normal.png",
            "ElevatorDoors_Normal.png",
            "ElevatorCOP_Normal.png",
            "RebarRibbed_Normal.png"
        };

        foreach (string n in normals)
        {
            string path = Path.Combine(TextureDir, n).Replace("\\", "/");
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.SaveAndReimport();
            }
        }

        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        // 1. PBR_ElevatorSteel
        CreatePBRMat(urpLit, "PBR_ElevatorSteel",
            "ElevatorSteel_Albedo.png", "ElevatorSteel_Normal.png", "ElevatorSteel_Mask.png", null);

        // 2. PBR_ElevatorInterior
        CreatePBRMat(urpLit, "PBR_ElevatorInterior",
            "ElevatorInterior_Albedo.png", "ElevatorInterior_Normal.png", "ElevatorInterior_Mask.png", null);

        // 3. PBR_ElevatorTreadPlate
        CreatePBRMat(urpLit, "PBR_ElevatorTreadPlate",
            "ElevatorTreadPlate_Albedo.png", "ElevatorTreadPlate_Normal.png", "ElevatorTreadPlate_Mask.png", null);

        // 4. PBR_ElevatorDoors
        CreatePBRMat(urpLit, "PBR_ElevatorDoors",
            "ElevatorDoors_Albedo.png", "ElevatorDoors_Normal.png", "ElevatorDoors_Mask.png", null);

        // 5. PBR_ElevatorCOP
        CreatePBRMat(urpLit, "PBR_ElevatorCOP",
            "ElevatorCOP_Albedo.png", "ElevatorCOP_Normal.png", "ElevatorCOP_Mask.png", "ElevatorCOP_Emission.png");

        // 6. PBR_RebarRibbed
        CreatePBRMat(urpLit, "PBR_RebarRibbed",
            "RebarRibbed_Albedo.png", "RebarRibbed_Normal.png", "RebarRibbed_Mask.png", null);

        AssetDatabase.SaveAssets();
    }

    private static void CreatePBRMat(Shader shader, string matName, string albedoName, string normalName, string maskName, string emissionName)
    {
        string matPath = Path.Combine(MaterialDir, matName + ".mat").Replace("\\", "/");
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(shader) { name = matName };
            AssetDatabase.CreateAsset(mat, matPath);
        }

        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(Path.Combine(TextureDir, albedoName).Replace("\\", "/"));
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Path.Combine(TextureDir, normalName).Replace("\\", "/"));
        Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(Path.Combine(TextureDir, maskName).Replace("\\", "/"));

        if (albedo != null)
        {
            mat.SetTexture("_BaseMap", albedo);
            mat.SetTexture("_MainTex", albedo);
        }
        if (normal != null)
        {
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
        }
        if (mask != null)
        {
            mat.SetTexture("_MetallicGlossMap", mask);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        if (!string.IsNullOrEmpty(emissionName))
        {
            Texture2D emission = AssetDatabase.LoadAssetAtPath<Texture2D>(Path.Combine(TextureDir, emissionName).Replace("\\", "/"));
            if (emission != null)
            {
                mat.SetTexture("_EmissionMap", emission);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.white * 2.2f);
            }
        }

        EditorUtility.SetDirty(mat);
    }

    // =========================================================================
    // UTILITIES
    // =========================================================================
    private static float DistToSegment(float px, float py, float x1, float y1, float x2, float y2)
    {
        float dx = x2 - x1;
        float dy = y2 - y1;
        float l2 = dx * dx + dy * dy;
        if (l2 < 0.00001f) return Mathf.Sqrt((px - x1) * (px - x1) + (py - y1) * (py - y1));
        float t = Mathf.Clamp01(((px - x1) * dx + (py - y1) * dy) / l2);
        float projX = x1 + t * dx;
        float projY = y1 + t * dy;
        return Mathf.Sqrt((px - projX) * (px - projX) + (py - projY) * (py - projY));
    }

    private static void ComputeNormalMapSobel(float[,] height, Texture2D normalTex, float strength)
    {
        int size = height.GetLength(0);
        for (int y = 0; y < size; y++)
        {
            int y0 = (y - 1 + size) % size;
            int y1 = (y + 1) % size;
            for (int x = 0; x < size; x++)
            {
                int x0 = (x - 1 + size) % size;
                int x1 = (x + 1) % size;

                float dX = (height[x1, y0] + 2f * height[x1, y] + height[x1, y1]) -
                           (height[x0, y0] + 2f * height[x0, y] + height[x0, y1]);
                float dY = (height[x0, y1] + 2f * height[x, y1] + height[x1, y1]) -
                           (height[x0, y0] + 2f * height[x, y0] + height[x1, y0]);

                Vector3 n = new Vector3(-dX * strength, -dY * strength, 1.0f).normalized;
                Color normCol = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1.0f);
                normalTex.SetPixel(x, y, normCol);
            }
        }
        normalTex.Apply();
    }

    private static void SaveTexturePNG(Texture2D tex, string path)
    {
        tex.Apply();
        byte[] bytes = tex.EncodeToPNG();
        File.WriteAllBytes(path, bytes);
    }
}
