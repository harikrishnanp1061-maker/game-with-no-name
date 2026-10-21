using System.IO;
using UnityEngine;
using UnityEditor;

public static class UserTextureMaterialSetup
{
    private const string UserTexDir = "Assets/Textures/UserTextures";
    private const string MatDir = "Assets/Materials";

    [MenuItem("Tools/FLIP//FALL/Setup User PBR Materials")]
    public static void SetupAll()
    {
        if (!Directory.Exists(MatDir)) Directory.CreateDirectory(MatDir);

        Debug.Log("[UserTextureMaterialSetup] Configuring TextureImporters and creating PBR Materials...");

        // 1. Configure texture importers
        ConfigureTexturesInFolder($"{UserTexDir}/Concrete044C_2K-PNG");
        ConfigureTexturesInFolder($"{UserTexDir}/Concrete042C_2K-PNG");
        ConfigureTexturesInFolder($"{UserTexDir}/DiamondPlate008C_2K-PNG");
        ConfigureTexturesInFolder($"{UserTexDir}/Metal062C_2K-PNG");
        ConfigureTexturesInFolder($"{UserTexDir}/PavingStones115C_2K-PNG");
        ConfigureTexturesInFolder($"{UserTexDir}/Sign004_2K-PNG");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Shader urpLit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");

        // 2. Create the 4 Master Materials for complete scene continuity:
        // Material 1: ONE FOR THE WALL (Architectural concrete for all walls, columns, shaft, concrete slabs)
        CreatePBRMat(
            $"{MatDir}/PBR_Master_Wall.mat",
            urpLit,
            $"{UserTexDir}/Concrete042C_2K-PNG/Concrete042C_2K-PNG_Color.png",
            $"{UserTexDir}/Concrete042C_2K-PNG/Concrete042C_2K-PNG_NormalGL.png",
            $"{UserTexDir}/Concrete042C_2K-PNG/Concrete042C_2K-PNG_AmbientOcclusion.png",
            null,
            new Color(0.92f, 0.92f, 0.92f),
            bumpScale: 1.0f,
            smoothness: 0.22f,
            metallic: 0.0f,
            tiling: new Vector2(3f, 2f)
        );

        // Material 2: ONE FOR ROOM FLOORS (Cast concrete slab floor for all room interiors)
        CreatePBRMat(
            $"{MatDir}/PBR_Master_RoomFloor.mat",
            urpLit,
            $"{UserTexDir}/Concrete044C_2K-PNG/Concrete044C_2K-PNG_Color.png",
            $"{UserTexDir}/Concrete044C_2K-PNG/Concrete044C_2K-PNG_NormalGL.png",
            $"{UserTexDir}/Concrete044C_2K-PNG/Concrete044C_2K-PNG_AmbientOcclusion.png",
            null,
            new Color(0.95f, 0.95f, 0.95f),
            bumpScale: 1.1f,
            smoothness: 0.30f,
            metallic: 0.0f,
            tiling: new Vector2(5f, 5f)
        );

        // Material 3: ONE FOR PATHWAY FLOORS (Paved stone slab walkway for all corridors, hallways and walkways)
        CreatePBRMat(
            $"{MatDir}/PBR_Master_PathwayFloor.mat",
            urpLit,
            $"{UserTexDir}/PavingStones115C_2K-PNG/PavingStones115C_2K-PNG_Color.png",
            $"{UserTexDir}/PavingStones115C_2K-PNG/PavingStones115C_2K-PNG_NormalGL.png",
            $"{UserTexDir}/PavingStones115C_2K-PNG/PavingStones115C_2K-PNG_AmbientOcclusion.png",
            null,
            new Color(0.90f, 0.90f, 0.90f),
            bumpScale: 1.2f,
            smoothness: 0.25f,
            metallic: 0.0f,
            tiling: new Vector2(2f, 14f)
        );

        // Material 4: ONE METALLIC STYLE FOR ALL METALLIC ELEMENTS (Beams, shaft ring braces, elevator cab, troffers, rebar, ductwork)
        CreatePBRMat(
            $"{MatDir}/PBR_Master_Metal.mat",
            urpLit,
            $"{UserTexDir}/Metal062C_2K-PNG/Metal062C_2K-PNG_Color.png",
            $"{UserTexDir}/Metal062C_2K-PNG/Metal062C_2K-PNG_NormalGL.png",
            null,
            $"{UserTexDir}/Metal062C_2K-PNG/Metal062C_2K-PNG_Metalness.png",
            new Color(0.90f, 0.92f, 0.95f),
            bumpScale: 1.0f,
            smoothness: 0.65f,
            metallic: 0.90f,
            tiling: new Vector2(2f, 2f)
        );

        // Hazard Warning Signage (Sign004)
        CreatePBRMat(
            $"{MatDir}/PBR_User_HazardSign.mat",
            urpLit,
            $"{UserTexDir}/Sign004_2K-PNG/Sign004_2K-PNG_Color.png",
            $"{UserTexDir}/Sign004_2K-PNG/Sign004_2K-PNG_NormalGL.png",
            null,
            null,
            Color.white,
            bumpScale: 1.0f,
            smoothness: 0.45f,
            metallic: 0.1f,
            tiling: new Vector2(1f, 1f)
        );

        // H. Fluorescent Tube Glow Material (Emissive 4500K bright white)
        string glowMatPath = $"{MatDir}/PBR_FluorescentTube_Glow.mat";
        Material glowMat = AssetDatabase.LoadAssetAtPath<Material>(glowMatPath);
        if (glowMat == null)
        {
            glowMat = new Material(urpLit);
            AssetDatabase.CreateAsset(glowMat, glowMatPath);
        }
        glowMat.SetColor("_BaseColor", new Color(0.92f, 0.96f, 1.0f));
        glowMat.EnableKeyword("_EMISSION");
        glowMat.SetColor("_EmissionColor", new Color(0.92f, 0.96f, 1.0f) * 3.8f);
        glowMat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        glowMat.SetFloat("_Smoothness", 0.95f);
        EditorUtility.SetDirty(glowMat);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[UserTextureMaterialSetup] All user PBR materials successfully created and linked!");
    }

    private static void ConfigureTexturesInFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath)) return;

        foreach (string file in Directory.GetFiles(folderPath, "*.png"))
        {
            string unityPath = file.Replace('\\', '/');
            TextureImporter importer = AssetImporter.GetAtPath(unityPath) as TextureImporter;
            if (importer == null) continue;

            bool isNormal = unityPath.Contains("Normal");
            bool isColor = unityPath.Contains("Color") || unityPath.EndsWith(".png") && !unityPath.Contains("_");
            bool isData = unityPath.Contains("Roughness") || unityPath.Contains("Metalness") || unityPath.Contains("AmbientOcclusion") || unityPath.Contains("Displacement");

            bool modified = false;

            if (isNormal && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.wrapMode = TextureWrapMode.Repeat;
                modified = true;
            }
            else if (isData && importer.sRGBTexture)
            {
                importer.sRGBTexture = false;
                importer.wrapMode = TextureWrapMode.Repeat;
                modified = true;
            }
            else if (isColor && (!importer.sRGBTexture || importer.wrapMode != TextureWrapMode.Repeat))
            {
                importer.sRGBTexture = true;
                importer.wrapMode = TextureWrapMode.Repeat;
                modified = true;
            }

            if (modified)
            {
                importer.SaveAndReimport();
            }
        }
    }

    private static void CreatePBRMat(
        string matPath,
        Shader shader,
        string albedoPath,
        string normalPath,
        string aoPath,
        string metallicPath,
        Color tint,
        float bumpScale,
        float smoothness,
        float metallic,
        Vector2 tiling)
    {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, matPath);
        }

        Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
        Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
        Texture2D ao = !string.IsNullOrEmpty(aoPath) ? AssetDatabase.LoadAssetAtPath<Texture2D>(aoPath) : null;
        Texture2D metal = !string.IsNullOrEmpty(metallicPath) ? AssetDatabase.LoadAssetAtPath<Texture2D>(metallicPath) : null;

        if (albedo != null)
        {
            mat.SetTexture("_BaseMap", albedo);
            mat.SetColor("_BaseColor", tint);
            mat.SetTextureScale("_BaseMap", tiling);
        }
        if (normal != null)
        {
            mat.SetTexture("_BumpMap", normal);
            mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_BumpScale", bumpScale);
            mat.SetTextureScale("_BumpMap", tiling);
        }
        if (ao != null)
        {
            mat.SetTexture("_OcclusionMap", ao);
            mat.SetFloat("_OcclusionStrength", 1.0f);
        }
        if (metal != null)
        {
            mat.SetTexture("_MetallicGlossMap", metal);
            mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        }

        mat.SetFloat("_Smoothness", smoothness);
        mat.SetFloat("_Metallic", metallic);

        EditorUtility.SetDirty(mat);
    }
}
