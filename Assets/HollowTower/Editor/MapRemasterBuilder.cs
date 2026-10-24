using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if HOLLOWTOWER_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif
using UnityEditor;
using UnityEditor.SceneManagement;
using HollowTower;

namespace HollowTower.EditorTools
{
    public enum LampStyle { Fluorescent, Warm, Dim, Industrial, Emergency }

    public static class MapRemasterBuilder
    {
        public const string BaseDir = "Assets/HollowTowerRemastered";
        public const string TexDir = BaseDir + "/Textures";
        public const string UserTexDir = TexDir + "/UserTextures";
        public const string MatDir = BaseDir + "/Materials";
        public const string ScenePath = BaseDir + "/Scenes/HollowTower_Remastered.unity";

        // Architectural scale:
        // Corridors: 4.0m wide (human-proportioned institutional scale)
        // Storey height: 4.2m
        // Floor slab thickness: 0.20m
        // Wall height / room vertical clearance: 4.00m
        // Doorway: 1.8m wide x 2.6m high
        public const float Cell = 4.0f;
        public const float Story = 4.2f;
        public const float SlabThick = 0.20f;
        public const float WallH = Story - SlabThick; // 4.00m
        public const float WallThick = 0.25f;
        public const float DoorW = 1.8f;
        public const float DoorH = 2.6f;

        public const float Lumens = 240f;

        [MenuItem("Hollow Tower/Open Remastered Scene", priority = 1)]
        public static void SwitchToRemasteredScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.playModeStartScene = null;
                EditorSceneManager.OpenScene(ScenePath);
                Debug.Log("<color=green>[SceneSwitcher]</color> Switched to Remastered Map.");
            }
        }

#if HOLLOWTOWER_HDRP
        // Remaster steps 1-4, 6 and 7 build the map with HDRP materials, lights and volume. They are retired with the
        // move to URP (the saved scene is now the source of truth, converted by URPConversion); re-running them would
        // regenerate HDRP content. They only compile with the HDRP package installed and HOLLOWTOWER_HDRP defined.
        [MenuItem("Hollow Tower/Remaster/1. Configure Textures and Materials", priority = 50)]
        public static void ConfigureTexturesAndMaterials()
        {
            AssetDatabase.Refresh();

            // Configure Normal Maps
            string[] normalTextures = new[]
            {
                UserTexDir + "/ceilings/OfficeCeiling001_2K-PNG/OfficeCeiling001_2K-PNG_NormalGL.png",
                UserTexDir + "/ceilings/OfficeCeiling005_2K-PNG/OfficeCeiling005_2K-PNG_NormalGL.png",
                UserTexDir + "/floors/Bricks066_2K-PNG/Bricks066_2K-PNG_NormalGL.png",
                UserTexDir + "/floors/Planks021_2K-PNG/Planks021_2K-PNG_NormalGL.png",
                UserTexDir + "/floors/Tiles007_2K-JPG/Tiles007_2K-JPG_NormalGL.jpg",
                UserTexDir + "/floors/Tiles143_2K-PNG/Tiles143_2K-PNG_NormalGL.png",
                UserTexDir + "/floors/WoodFloor043_2K-PNG/WoodFloor043_2K-PNG_NormalGL.png",
                UserTexDir + "/walls/PaintedBricks001_2K-PNG/PaintedBricks001_2K-PNG_NormalGL.png",
                UserTexDir + "/walls/PaintedBricks003_2K-PNG/PaintedBricks003_2K-PNG_NormalGL.png",
                UserTexDir + "/walls/Tiles011_2K-PNG/Tiles011_2K-PNG_NormalGL.png",
                UserTexDir + "/walls/WoodSiding008_2K-PNG/WoodSiding008_2K-PNG_NormalGL.png",
                TexDir + "/Hazard_Normal.png",
                TexDir + "/RustedSteel_Normal.png"
            };

            foreach (var path in normalTextures)
            {
                if (File.Exists(path))
                {
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer != null && importer.textureType != TextureImporterType.NormalMap)
                    {
                        importer.textureType = TextureImporterType.NormalMap;
                        importer.SaveAndReimport();
                    }
                }
            }

            // Configure Linear Masks
            string[] maskTextures = new[]
            {
                UserTexDir + "/ceilings/OfficeCeiling001_2K-PNG/OfficeCeiling001_2K-PNG_Mask.png",
                UserTexDir + "/ceilings/OfficeCeiling005_2K-PNG/OfficeCeiling005_2K-PNG_Mask.png",
                UserTexDir + "/floors/Bricks066_2K-PNG/Bricks066_2K-PNG_Mask.png",
                UserTexDir + "/floors/Planks021_2K-PNG/Planks021_2K-PNG_Mask.png",
                UserTexDir + "/floors/Tiles007_2K-JPG/Tiles007_2K-JPG_Mask.png",
                UserTexDir + "/floors/Tiles143_2K-PNG/Tiles143_2K-PNG_Mask.png",
                UserTexDir + "/floors/WoodFloor043_2K-PNG/WoodFloor043_2K-PNG_Mask.png",
                UserTexDir + "/walls/PaintedBricks001_2K-PNG/PaintedBricks001_2K-PNG_Mask.png",
                UserTexDir + "/walls/PaintedBricks003_2K-PNG/PaintedBricks003_2K-PNG_Mask.png",
                UserTexDir + "/walls/Tiles011_2K-PNG/Tiles011_2K-PNG_Mask.png",
                UserTexDir + "/walls/WoodSiding008_2K-PNG/WoodSiding008_2K-PNG_Mask.png",
                TexDir + "/RustedSteel_Mask.png"
            };

            foreach (var path in maskTextures)
            {
                if (File.Exists(path))
                {
                    var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                    if (importer != null && importer.sRGBTexture)
                    {
                        importer.sRGBTexture = false;
                        importer.SaveAndReimport();
                    }
                }
            }

            Shader litShader = Shader.Find("HDRP/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");

            Directory.CreateDirectory(MatDir);

            // -------------------------------------------------------------
            // FLOOR 1 (Ground / Industrial Lobby):
            // Wall:   PaintedBricks001 (Dark industrial brickwork)
            // Floor:  Bricks066 (Cobblestone / industrial brick pavers)
            // Ceil:   OfficeCeiling005 (Recessed industrial grid ceiling)
            // -------------------------------------------------------------
            CreateOrUpdateMaterial("Mat_F1_Wall", litShader,
                UserTexDir + "/walls/PaintedBricks001_2K-PNG/PaintedBricks001_2K-PNG_Color.png",
                UserTexDir + "/walls/PaintedBricks001_2K-PNG/PaintedBricks001_2K-PNG_NormalGL.png",
                UserTexDir + "/walls/PaintedBricks001_2K-PNG/PaintedBricks001_2K-PNG_Mask.png",
                new Vector2(2f, 2f), 0.2f, 0f);

            CreateOrUpdateMaterial("Mat_F1_Floor", litShader,
                UserTexDir + "/floors/Bricks066_2K-PNG/Bricks066_2K-PNG_Color.png",
                UserTexDir + "/floors/Bricks066_2K-PNG/Bricks066_2K-PNG_NormalGL.png",
                UserTexDir + "/floors/Bricks066_2K-PNG/Bricks066_2K-PNG_Mask.png",
                new Vector2(2f, 2f), 0.3f, 0f);

            CreateOrUpdateMaterial("Mat_F1_Ceil", litShader,
                UserTexDir + "/ceilings/OfficeCeiling005_2K-PNG/OfficeCeiling005_2K-PNG_Color.png",
                UserTexDir + "/ceilings/OfficeCeiling005_2K-PNG/OfficeCeiling005_2K-PNG_NormalGL.png",
                UserTexDir + "/ceilings/OfficeCeiling005_2K-PNG/OfficeCeiling005_2K-PNG_Mask.png",
                new Vector2(0.5f, 0.5f), 0.25f, 0f, null,
                UserTexDir + "/ceilings/OfficeCeiling005_2K-PNG/OfficeCeiling005_Sparse_Emission.png",
                new Color(0.72f, 0.88f, 1.0f), 75f);

            // -------------------------------------------------------------
            // FLOOR 2 (School / Institutional):
            // Wall:   Tiles011 (Institutional ceramic wall tiles)
            // Floor:  Tiles143 (Checkered / ceramic school floor tiles)
            // Ceil:   OfficeCeiling001 (Classroom acoustic drop ceiling)
            // -------------------------------------------------------------
            CreateOrUpdateMaterial("Mat_F2_Wall", litShader,
                UserTexDir + "/walls/Tiles011_2K-PNG/Tiles011_2K-PNG_Color.png",
                UserTexDir + "/walls/Tiles011_2K-PNG/Tiles011_2K-PNG_NormalGL.png",
                UserTexDir + "/walls/Tiles011_2K-PNG/Tiles011_2K-PNG_Mask.png",
                new Vector2(2.5f, 2.5f), 0.65f, 0f);

            CreateOrUpdateMaterial("Mat_F2_Floor", litShader,
                UserTexDir + "/floors/Tiles143_2K-PNG/Tiles143_2K-PNG_Color.png",
                UserTexDir + "/floors/Tiles143_2K-PNG/Tiles143_2K-PNG_NormalGL.png",
                UserTexDir + "/floors/Tiles143_2K-PNG/Tiles143_2K-PNG_Mask.png",
                new Vector2(2f, 2f), 0.45f, 0f);

            CreateOrUpdateMaterial("Mat_F2_Ceil", litShader,
                UserTexDir + "/ceilings/OfficeCeiling001_2K-PNG/OfficeCeiling001_2K-PNG_Color.png",
                UserTexDir + "/ceilings/OfficeCeiling001_2K-PNG/OfficeCeiling001_2K-PNG_NormalGL.png",
                UserTexDir + "/ceilings/OfficeCeiling001_2K-PNG/OfficeCeiling001_2K-PNG_Mask.png",
                new Vector2(1.5f, 1.5f), 0.2f, 0f);

            // -------------------------------------------------------------
            // FLOOR 3 (Hotel / Vintage Hospitality):
            // Wall:   WoodSiding008 (Warm vintage wood siding / panelling)
            // Floor:  WoodFloor043 (Polished parquet hotel hardwood floor)
            // Ceil:   OfficeCeiling005 (Warm panelled ceiling)
            // -------------------------------------------------------------
            CreateOrUpdateMaterial("Mat_F3_Wall", litShader,
                UserTexDir + "/walls/WoodSiding008_2K-PNG/WoodSiding008_2K-PNG_Color.png",
                UserTexDir + "/walls/WoodSiding008_2K-PNG/WoodSiding008_2K-PNG_NormalGL.png",
                UserTexDir + "/walls/WoodSiding008_2K-PNG/WoodSiding008_2K-PNG_Mask.png",
                new Vector2(1.5f, 1.5f), 0.35f, 0f);

            CreateOrUpdateMaterial("Mat_F3_Floor", litShader,
                UserTexDir + "/floors/WoodFloor043_2K-PNG/WoodFloor043_2K-PNG_Color.png",
                UserTexDir + "/floors/WoodFloor043_2K-PNG/WoodFloor043_2K-PNG_NormalGL.png",
                UserTexDir + "/floors/WoodFloor043_2K-PNG/WoodFloor043_2K-PNG_Mask.png",
                new Vector2(2f, 2f), 0.55f, 0f);

            CreateOrUpdateMaterial("Mat_F3_Ceil", litShader,
                UserTexDir + "/ceilings/OfficeCeiling005_2K-PNG/OfficeCeiling005_2K-PNG_Color.png",
                UserTexDir + "/ceilings/OfficeCeiling005_2K-PNG/OfficeCeiling005_2K-PNG_NormalGL.png",
                UserTexDir + "/ceilings/OfficeCeiling005_2K-PNG/OfficeCeiling005_2K-PNG_Mask.png",
                new Vector2(0.5f, 0.5f), 0.25f, 0f, null,
                UserTexDir + "/ceilings/OfficeCeiling005_2K-PNG/OfficeCeiling005_Sparse_Emission.png",
                new Color(1.0f, 0.90f, 0.75f), 70f);

            // -------------------------------------------------------------
            // FLOOR 4 (Offices / Corporate Headquarters):
            // Wall:   PaintedBricks003 (Clean painted brick / corporate drywall)
            // Floor:  Planks021 (Long executive wooden planks)
            // Ceil:   OfficeCeiling001 (Corporate acoustic grid drop ceiling)
            // -------------------------------------------------------------
            CreateOrUpdateMaterial("Mat_F4_Wall", litShader,
                UserTexDir + "/walls/PaintedBricks003_2K-PNG/PaintedBricks003_2K-PNG_Color.png",
                UserTexDir + "/walls/PaintedBricks003_2K-PNG/PaintedBricks003_2K-PNG_NormalGL.png",
                UserTexDir + "/walls/PaintedBricks003_2K-PNG/PaintedBricks003_2K-PNG_Mask.png",
                new Vector2(2f, 2f), 0.25f, 0f);

            CreateOrUpdateMaterial("Mat_F4_Floor", litShader,
                UserTexDir + "/floors/Planks021_2K-PNG/Planks021_2K-PNG_Color.png",
                UserTexDir + "/floors/Planks021_2K-PNG/Planks021_2K-PNG_NormalGL.png",
                UserTexDir + "/floors/Planks021_2K-PNG/Planks021_2K-PNG_Mask.png",
                new Vector2(2f, 2f), 0.4f, 0f);

            CreateOrUpdateMaterial("Mat_F4_Ceil", litShader,
                UserTexDir + "/ceilings/OfficeCeiling001_2K-PNG/OfficeCeiling001_2K-PNG_Color.png",
                UserTexDir + "/ceilings/OfficeCeiling001_2K-PNG/OfficeCeiling001_2K-PNG_NormalGL.png",
                UserTexDir + "/ceilings/OfficeCeiling001_2K-PNG/OfficeCeiling001_2K-PNG_Mask.png",
                new Vector2(1.5f, 1.5f), 0.2f, 0f, null,
                UserTexDir + "/ceilings/OfficeCeiling001_2K-PNG/OfficeCeiling001_2K-PNG_Emission.png",
                new Color(0.82f, 0.90f, 1.0f), 95f);

            // Architectural Trims & Structural Metals
            CreateOrUpdateMaterial("Mat_Remastered_HazardTrim", litShader,
                TexDir + "/Hazard_Albedo.png",
                TexDir + "/Hazard_Normal.png",
                null,
                new Vector2(2f, 1f), 0.5f, 0.4f);

            CreateOrUpdateMaterial("Mat_Remastered_RustedSteel", litShader,
                TexDir + "/RustedSteel_Albedo.png",
                TexDir + "/RustedSteel_Normal.png",
                TexDir + "/RustedSteel_Mask.png",
                new Vector2(1f, 1f), 0.35f, 0.6f);

            // Emissive Lamp Materials (Calibrated for subtle, non-glaring glow)
            CreateOrUpdateEmissiveMaterial("Mat_Remastered_LampCool", litShader,
                new Color(0.92f, 0.96f, 1.0f), new Color(0.88f, 0.94f, 1.0f), 350f);

            CreateOrUpdateEmissiveMaterial("Mat_Remastered_LampWarm", litShader,
                new Color(1.0f, 0.90f, 0.75f), new Color(1.0f, 0.82f, 0.60f), 300f);

            CreateOrUpdateEmissiveMaterial("Mat_Remastered_LampRed", litShader,
                new Color(1.0f, 0.2f, 0.2f), new Color(1.0f, 0.12f, 0.08f), 450f);

            CreateOrUpdateMaterial("Mat_Remastered_LampDead", litShader, null, null, null, Vector2.one, 0.2f, 0.05f, new Color(0.22f, 0.22f, 0.25f));
            CreateOrUpdateMaterial("Mat_Remastered_Brass", litShader, null, null, null, Vector2.one, 0.72f, 0.85f, new Color(0.78f, 0.65f, 0.35f));
            CreateOrUpdateMaterial("Mat_Remastered_MetalHousing", litShader, null, null, null, Vector2.one, 0.55f, 0.75f, new Color(0.32f, 0.34f, 0.38f));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=green>[MapRemaster]</color> Configured 4-Floor Harmonious PBR Materials successfully.");
        }

        static Material CreateOrUpdateMaterial(string name, Shader shader, string albedoPath, string normalPath, string maskPath, Vector2 tiling, float smoothness, float metallic, Color? baseColor = null, string emissionPath = null, Color? emissiveColor = null, float emissiveNits = 0f)
        {
            string path = MatDir + "/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            if (baseColor.HasValue) mat.SetColor("_BaseColor", baseColor.Value);

            if (!string.IsNullOrEmpty(albedoPath))
            {
                Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
                if (albedo != null)
                {
                    mat.SetTexture("_BaseColorMap", albedo);
                    mat.SetTextureScale("_BaseColorMap", tiling);
                }
            }

            if (!string.IsNullOrEmpty(normalPath))
            {
                Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                if (normal != null)
                {
                    mat.SetTexture("_NormalMap", normal);
                    mat.SetTextureScale("_NormalMap", tiling);
                    mat.SetFloat("_NormalScale", 1.0f);
                }
            }

            if (!string.IsNullOrEmpty(maskPath))
            {
                Texture2D mask = AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath);
                if (mask != null)
                {
                    mat.SetTexture("_MaskMap", mask);
                    mat.SetTextureScale("_MaskMap", tiling);
                }
            }

            if (!string.IsNullOrEmpty(emissionPath))
            {
                Texture2D emTex = AssetDatabase.LoadAssetAtPath<Texture2D>(emissionPath);
                if (emTex != null)
                {
                    mat.EnableKeyword("_EMISSIVE_COLOR_MAP");
                    mat.SetTexture("_EmissiveColorMap", emTex);
                    mat.SetTextureScale("_EmissiveColorMap", tiling);
                    Color emCol = emissiveColor ?? new Color(0.82f, 0.92f, 1.0f);
                    mat.SetColor("_EmissiveColor", emCol * emissiveNits);
                    mat.SetColor("_EmissiveColorLDR", emCol);
                    mat.SetFloat("_UseEmissiveIntensity", 1f);
                    mat.SetFloat("_EmissiveIntensityUnit", 0f); // Nits
                    mat.SetFloat("_EmissiveIntensity", emissiveNits);
                    mat.SetFloat("_EmissiveExposureWeight", 0.5f);
                }
            }

            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            HDMaterial.ValidateMaterial(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material CreateOrUpdateEmissiveMaterial(string name, Shader shader, Color baseColor, Color emissiveColor, float nits)
        {
            string path = MatDir + "/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            mat.EnableKeyword("_EMISSIVE_COLOR_MAP");
            mat.SetColor("_BaseColor", baseColor);
            mat.SetColor("_EmissiveColor", emissiveColor);
            mat.SetColor("_EmissiveColorLDR", baseColor);
            mat.SetFloat("_UseEmissiveIntensity", 1f);
            mat.SetFloat("_EmissiveIntensityUnit", 0f); // 0 = Nits
            mat.SetFloat("_EmissiveIntensity", nits);
            mat.SetFloat("_EmissiveExposureWeight", 0f); // Prevents eye adaptation from extinguishing lamp bulb
            mat.SetFloat("_Smoothness", 0.5f);
            mat.SetFloat("_Metallic", 0f);
            HDMaterial.ValidateMaterial(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        [MenuItem("Hollow Tower/Remaster/2. Generate Remastered 3D Scene", priority = 51)]
        public static void GenerateRemasteredScene()
        {
            ConfigureTexturesAndMaterials();

            // Create new single scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));

            // Load Harmonious Materials for All 4 Floors
            var matF1Wall = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_F1_Wall.mat");
            var matF1Floor = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_F1_Floor.mat");
            var matF1Ceil = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_F1_Ceil.mat");

            var matF2Wall = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_F2_Wall.mat");
            var matF2Floor = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_F2_Floor.mat");
            var matF2Ceil = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_F2_Ceil.mat");

            var matF3Wall = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_F3_Wall.mat");
            var matF3Floor = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_F3_Floor.mat");
            var matF3Ceil = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_F3_Ceil.mat");

            var matF4Wall = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_F4_Wall.mat");
            var matF4Floor = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_F4_Floor.mat");
            var matF4Ceil = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_F4_Ceil.mat");

            var matTrim = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_Remastered_HazardTrim.mat");
            var matSteel = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_Remastered_RustedSteel.mat");
            var matLampCool = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_Remastered_LampCool.mat");
            var matLampWarm = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_Remastered_LampWarm.mat");
            var matLampRed = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_Remastered_LampRed.mat");
            var matLampDead = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_Remastered_LampDead.mat");
            var matBrass = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_Remastered_Brass.mat");
            var matMetalHousing = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_Remastered_MetalHousing.mat");

            var matDoorTactical = AssetDatabase.LoadAssetAtPath<Material>("Assets/HollowTowerRemastered/Materials/Doors/Mat_Door_TacticalSteel.mat");
            var matDoorCleanLab = AssetDatabase.LoadAssetAtPath<Material>("Assets/HollowTowerRemastered/Materials/Doors/Mat_Door_CleanLab.mat");
            var matDoorExecutive = AssetDatabase.LoadAssetAtPath<Material>("Assets/HollowTowerRemastered/Materials/Doors/Mat_Door_ExecutiveWood.mat");
            var matDoorFrame = AssetDatabase.LoadAssetAtPath<Material>("Assets/HollowTowerRemastered/Materials/Doors/Mat_Door_FrameDarkSteel.mat");
            var doorLeafMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/HollowTowerRemastered/Meshes/Door_Leaf_Mesh.asset");

            var root = new GameObject("HollowTower_Remastered_Environment");
            root.isStatic = true;

            // Global Directional & Volume Setup
            SetupAtmosphere(root);

            var plans = LevelLayout.Plans;
            var floorRoots = new GameObject[plans.Length];

            for (int f = 0; f < plans.Length; f++)
            {
                var p = plans[f];
                string floorName = $"Floor_{f + 1}_{p.theme}";
                var floorRoot = new GameObject(floorName);
                floorRoots[f] = floorRoot;
                floorRoot.transform.SetParent(root.transform, false);
                floorRoot.isStatic = true;

                float baseY = f * Story;

                // Select Harmonious 3-piece combo and dedicated clean door for this specific floor
                Material wallForFloor = matF1Wall;
                Material floorForFloor = matF1Floor;
                Material ceilForFloor = matF1Ceil;
                Material doorForFloor = matDoorTactical;

                if (f == 0) { wallForFloor = matF1Wall; floorForFloor = matF1Floor; ceilForFloor = matF1Ceil; doorForFloor = matDoorTactical; }
                else if (f == 1) { wallForFloor = matF2Wall; floorForFloor = matF2Floor; ceilForFloor = matF2Ceil; doorForFloor = matDoorCleanLab; }
                else if (f == 2) { wallForFloor = matF3Wall; floorForFloor = matF3Floor; ceilForFloor = matF3Ceil; doorForFloor = matDoorExecutive; }
                else { wallForFloor = matF4Wall; floorForFloor = matF4Floor; ceilForFloor = matF4Ceil; doorForFloor = matDoorTactical; }

                // 1. Build Floor Slabs and Ceilings
                var slabsRoot = new GameObject("Slabs_And_Ceilings");
                slabsRoot.transform.SetParent(floorRoot.transform, false);
                slabsRoot.isStatic = true;

                for (int r = 0; r < LevelLayout.Rows; r++)
                {
                    for (int c = 0; c < LevelLayout.Cols; c++)
                    {
                        if (p.zone[r, c] < 0) continue; // outside / solid

                        Vector3 cellCenter = new Vector3((c + 0.5f) * Cell, baseY, (LevelLayout.Rows - r - 0.5f) * Cell);

                        // Floor slab: top of slab is at Y = baseY (completely flat, unobstructed walking surface)
                        if (!p.shaft[r, c])
                        {
                            var fl = CreateBox("FloorSlab", slabsRoot.transform, cellCenter + Vector3.down * (SlabThick * 0.5f), new Vector3(Cell, SlabThick, Cell), floorForFloor);
                            fl.isStatic = true;
                        }

                        // Ceiling slab: skin at Y = baseY + WallH - 0.025f (bottom is at baseY + WallH - 0.05f = 3.95m)
                        bool hasCeiling = true;
                        if (f < plans.Length - 1 && plans[f + 1].shaft[r, c]) hasCeiling = false;
                        if (p.ramp[r, c]) hasCeiling = false;

                        if (hasCeiling)
                        {
                            var ceil = CreateBox("CeilingSlab", slabsRoot.transform, cellCenter + Vector3.up * (WallH - 0.025f), new Vector3(Cell, 0.05f, Cell), ceilForFloor);
                            ceil.isStatic = true;

                            // Sealed roof slab for the top floor (Floor 4)
                            if (f == plans.Length - 1)
                            {
                                var roof = CreateBox("RoofSlab", slabsRoot.transform, cellCenter + Vector3.up * (WallH + SlabThick * 0.5f), new Vector3(Cell, SlabThick, Cell), matTrim);
                                roof.isStatic = true;
                            }
                        }
                    }
                }

                // 2. Build Walls & Doorways
                var wallsRoot = new GameObject("Walls_And_Doors");
                wallsRoot.transform.SetParent(floorRoot.transform, false);
                wallsRoot.isStatic = true;

                // Horizontal edges (constant Z)
                for (int b = 0; b <= LevelLayout.Rows; b++)
                {
                    float z = (LevelLayout.Rows - b) * Cell;
                    for (int c = 0; c < LevelLayout.Cols; c++)
                    {
                        int zn = b > 0 ? p.zone[b - 1, c] : -1;
                        int zs = b < LevelLayout.Rows ? p.zone[b, c] : -1;

                        if (zn == zs) continue; // same room or both outside

                        float xCenter = (c + 0.5f) * Cell;
                        Vector3 pos = new Vector3(xCenter, baseY + WallH * 0.5f, z);

                        int edgeKey = LevelLayout.EdgeS(Mathf.Clamp(b - 1, 0, LevelLayout.Rows - 1), c);
                        bool isDoor = p.doors.Contains(edgeKey);

                        if (isDoor)
                        {
                            BuildDoorwaySection(wallsRoot.transform, pos, true, wallForFloor, doorForFloor, matDoorFrame, matTrim, doorLeafMesh);
                        }
                        else
                        {
                            var w = CreateBox("Wall_H", wallsRoot.transform, pos, new Vector3(Cell, WallH, WallThick), wallForFloor);
                            w.isStatic = true;

                            // Baseboard
                            Vector3 baseScale = new Vector3(Cell, 0.18f, WallThick + 0.04f);
                            var bb = CreateBox("Baseboard_H", wallsRoot.transform, pos - new Vector3(0f, WallH * 0.5f - 0.09f, 0f), baseScale, matTrim);
                            bb.isStatic = true;
                        }
                    }
                }

                // Vertical edges (constant X)
                for (int a = 0; a <= LevelLayout.Cols; a++)
                {
                    float x = a * Cell;
                    for (int r = 0; r < LevelLayout.Rows; r++)
                    {
                        int zw = a > 0 ? p.zone[r, a - 1] : -1;
                        int ze = a < LevelLayout.Cols ? p.zone[r, a] : -1;

                        if (zw == ze) continue;

                        float zCenter = (LevelLayout.Rows - r - 0.5f) * Cell;
                        Vector3 pos = new Vector3(x, baseY + WallH * 0.5f, zCenter);

                        int edgeKey = LevelLayout.EdgeE(r, Mathf.Clamp(a - 1, 0, LevelLayout.Cols - 1));
                        bool isDoor = p.doors.Contains(edgeKey);

                        if (isDoor)
                        {
                            BuildDoorwaySection(wallsRoot.transform, pos, false, wallForFloor, doorForFloor, matDoorFrame, matTrim, doorLeafMesh);
                        }
                        else
                        {
                            var w = CreateBox("Wall_V", wallsRoot.transform, pos, new Vector3(WallThick, WallH, Cell), wallForFloor);
                            w.isStatic = true;

                            // Baseboard
                            Vector3 baseScale = new Vector3(WallThick + 0.04f, 0.18f, Cell);
                            var bb = CreateBox("Baseboard_V", wallsRoot.transform, pos - new Vector3(0f, WallH * 0.5f - 0.09f, 0f), baseScale, matTrim);
                            bb.isStatic = true;
                        }
                    }
                }

                // 3. Place Atmospheric Horror Lighting & Physical Fixtures (Matching GitHub Placements)
                var lightsRoot = new GameObject("Lighting_Fixtures");
                lightsRoot.transform.SetParent(floorRoot.transform, false);
                PlaceFloorLighting(lightsRoot.transform, p, baseY, matLampCool, matLampWarm, matLampRed, matLampDead, matBrass, matMetalHousing);
            }

            // 4. Build Inter-Floor Staircases (Floors 1->2, 2->3, 3->4)
            var stairsRoot = new GameObject("Staircases");
            stairsRoot.transform.SetParent(root.transform, false);
            stairsRoot.isStatic = true;
            for (int i = 0; i < LevelLayout.Stairs.Length; i++)
            {
                var s = LevelLayout.Stairs[i];
                Material stairFloorMat = (i == 0) ? matF1Floor : (i == 1 ? matF2Floor : matF3Floor);
                BuildStaircase(stairsRoot.transform, s, stairFloorMat, matSteel, matTrim, matLampWarm);
            }

            // 5. Elevators at user-specified positions:
            // Spawn Elevator on Floor 1 in "Parking access" (SS 1 position)
            BuildSpawnElevator(floorRoots[0].transform, matSteel, matTrim, matBrass, matLampWarm);
            // Exit Elevator on Floor 4 in "Storage 2" (SS 2 position)
            BuildExitElevator(floorRoots[3].transform, matSteel, matTrim, matBrass, matLampWarm);

            // 6. Bake NavMesh across all 4 floors, rooms, and staircases
            BakeTowerNavMeshInternal();

            // Spawn Controlled Player Character from Tutorial Map
            SpawnControlledPlayer();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log($"<color=green>[MapRemaster]</color> Successfully built and saved remastered scene at: {ScenePath}");
        }

        [MenuItem("Hollow Tower/Remaster/3. Add Controlled Player Character", priority = 52)]
        public static GameObject SpawnControlledPlayer()
        {
            var oldCam = GameObject.Find("Preview_Camera");
            if (oldCam != null) UnityEngine.Object.DestroyImmediate(oldCam);

            var oldPlayer = GameObject.Find("Player_Character_Controlled");
            if (oldPlayer != null) UnityEngine.Object.DestroyImmediate(oldPlayer);

            var playerGo = new GameObject("Player_Character_Controlled");
            playerGo.tag = "Player";
            // Spawn inside Floor 1 Blender Elevator cabin in "Parking access" (facing West into the room)
            playerGo.transform.position = new Vector3(141.60f, 0.20f, 12.00f);
            playerGo.transform.rotation = Quaternion.Euler(0f, 270f, 0f);

            var cc = playerGo.AddComponent<CharacterController>();
            cc.height = 2.0f;
            cc.radius = 0.38f;
            cc.center = new Vector3(0f, 1.0f, 0f);
            cc.stepOffset = 0.45f;
            cc.slopeLimit = 45f;

            var camHolder = new GameObject("Camera_Holder");
            camHolder.transform.SetParent(playerGo.transform, false);
            camHolder.transform.localPosition = new Vector3(0f, 1.75f, 0.25f);

            var camGo = new GameObject("Player_Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(camHolder.transform, false);
            camGo.transform.localPosition = Vector3.zero;
            camGo.transform.localRotation = Quaternion.identity;

            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 45f; // High-performance indoor corridor render distance
            cam.fieldOfView = 75f;
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<HDAdditionalCameraData>();

            // Tactical Flashlight
            var torchGo = new GameObject("Tactical_Flashlight");
            torchGo.transform.SetParent(camHolder.transform, false);
            torchGo.transform.localPosition = new Vector3(0.18f, -0.15f, 0.25f);
            torchGo.transform.localRotation = Quaternion.identity;

            var torchLight = torchGo.AddComponent<Light>();
            torchLight.type = LightType.Spot;
            torchLight.lightUnit = LightUnit.Lumen;
            torchLight.enableSpotReflector = true;
            torchLight.spotAngle = 54f;
            torchLight.innerSpotAngle = 24f;
            torchLight.range = 35f;
            torchLight.color = new Color(1.0f, 0.96f, 0.90f);
            torchLight.intensity = 2600f;
            torchLight.shadows = LightShadows.Soft;

            var hdTorch = torchGo.AddComponent<HDAdditionalLightData>();
            hdTorch.affectsVolumetric = true;
            hdTorch.volumetricDimmer = 0.4f;

            // Player 3D Model
            // collage game's tutorial mannequin (Player 1 colours), first-person shadows-only body
            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Characters/SK_Web_Player_Animated.fbx");
            GameObject modelInst = null;
            if (modelPrefab != null)
            {
                modelInst = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, playerGo.transform);
                modelInst.name = "Model_Animated";
                modelInst.transform.localPosition = Vector3.zero;
                modelInst.transform.localRotation = Quaternion.identity;
                modelInst.transform.localScale = Vector3.one * 1.3f;
                var bodyMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Character_Body.mat");
                var jointMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Player1_Joints.mat");
                foreach (var smr in modelInst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    smr.sharedMaterial = smr.name.Contains("Joints") ? jointMat : bodyMat;
                    smr.updateWhenOffscreen = true;
                    smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;
                }
            }

            var anim = playerGo.GetComponentInChildren<Animator>();
            var animCtrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Characters/Player/Player_LocomotionController.controller");
            if (anim != null && animCtrl != null)
            {
                anim.runtimeAnimatorController = animCtrl;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }

            var rpcType = System.Type.GetType("HollowTower.RemasteredPlayerController, Assembly-CSharp");
            if (rpcType != null)
            {
                var rpc = playerGo.AddComponent(rpcType);
                rpcType.GetField("cameraHolder")?.SetValue(rpc, camHolder.transform);
                rpcType.GetField("playerCamera")?.SetValue(rpc, cam);
                rpcType.GetField("flashlightLight")?.SetValue(rpc, torchLight);
                rpcType.GetField("animator")?.SetValue(rpc, anim);
                rpcType.GetField("characterModel")?.SetValue(rpc, modelInst);
            }

            // Minimap HUD
            var hudType = System.Type.GetType("HollowTower.RemasteredMapHUD, Assembly-CSharp");
            if (hudType != null)
            {
                playerGo.AddComponent(hudType);
            }

            // Dynamic Light and Shadow Optimizer (Eliminates lag completely)
            var optType = System.Type.GetType("HollowTower.RemasteredLightOptimizer, Assembly-CSharp");
            if (optType != null)
            {
                var opt = playerGo.AddComponent(optType);
                optType.GetField("storyHeight")?.SetValue(opt, Story);
            }

            // Dynamic Floor Culling & Render Distance Optimizer
            var cullType = System.Type.GetType("HollowTower.TowerFloorCulling, Assembly-CSharp");
            if (cullType != null)
            {
                playerGo.AddComponent(cullType);
            }

            EditorUtility.SetDirty(playerGo);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();

            Debug.Log($"<color=green>[MapRemaster]</color> Spawned controlled Player Character with Light Optimizer at: {playerGo.transform.position}");
            return playerGo;
        }

        static void BuildDoorwaySection(Transform parent, Vector3 center, bool horizontal, Material wallMat, Material doorMat, Material frameMat, Material trimMat, Mesh doorLeafMesh)
        {
            var doorGroup = new GameObject(horizontal ? "Doorway_H" : "Doorway_V");
            doorGroup.transform.SetParent(parent, false);
            doorGroup.isStatic = true;

            // Left / West post
            float sideW = (Cell - DoorW) * 0.5f;
            Vector3 leftOffset = horizontal ? new Vector3(-Cell * 0.5f + sideW * 0.5f, 0f, 0f) : new Vector3(0f, 0f, -Cell * 0.5f + sideW * 0.5f);
            Vector3 sideScale = horizontal ? new Vector3(sideW, WallH, WallThick) : new Vector3(WallThick, WallH, sideW);
            var leftPost = CreateBox("Wall_Post_A", doorGroup.transform, center + leftOffset, sideScale, wallMat);
            leftPost.isStatic = true;

            // Right / East post
            Vector3 rightOffset = -leftOffset;
            var rightPost = CreateBox("Wall_Post_B", doorGroup.transform, center + rightOffset, sideScale, wallMat);
            rightPost.isStatic = true;

            // Header Lintel
            float lintelH = WallH - DoorH;
            Vector3 lintelPos = new Vector3(center.x, center.y + (WallH * 0.5f) - (lintelH * 0.5f), center.z);
            Vector3 lintelScale = horizontal ? new Vector3(DoorW, lintelH, WallThick) : new Vector3(WallThick, lintelH, DoorW);
            var lintel = CreateBox("Door_Lintel", doorGroup.transform, lintelPos, lintelScale, wallMat);
            lintel.isStatic = true;

            // Modeled Clean Steel Frame Header Trim
            Vector3 frameTopScale = horizontal ? new Vector3(DoorW + 0.15f, 0.12f, WallThick * 1.35f) : new Vector3(WallThick * 1.35f, 0.12f, DoorW + 0.15f);
            var frameTop = CreateBox("Frame_Header", doorGroup.transform, new Vector3(center.x, center.y - (WallH * 0.5f) + DoorH, center.z), frameTopScale, frameMat);
            frameTop.isStatic = true;

            // Side jamb trims
            Vector3 jambScale = horizontal ? new Vector3(0.1f, DoorH, WallThick * 1.3f) : new Vector3(WallThick * 1.3f, DoorH, 0.1f);
            Vector3 jambLeftPos = center + (horizontal ? new Vector3(-DoorW * 0.5f + 0.05f, -WallH * 0.5f + DoorH * 0.5f, 0f) : new Vector3(0f, -WallH * 0.5f + DoorH * 0.5f, -DoorW * 0.5f + 0.05f));
            Vector3 jambRightPos = center + (horizontal ? new Vector3(DoorW * 0.5f - 0.05f, -WallH * 0.5f + DoorH * 0.5f, 0f) : new Vector3(0f, -WallH * 0.5f + DoorH * 0.5f, -DoorW * 0.5f - 0.05f));
            var jambL = CreateBox("Frame_Jamb_L", doorGroup.transform, jambLeftPos, jambScale, frameMat);
            jambL.isStatic = true;
            var jambR = CreateBox("Frame_Jamb_R", doorGroup.transform, jambRightPos, jambScale, frameMat);
            jambR.isStatic = true;

            // Interactive Door Leaf & Hinge
            float leafW = 1.70f;
            float leafH = 2.54f;
            var hingeGo = new GameObject("Door_Hinge");
            hingeGo.transform.SetParent(doorGroup.transform, false);
            hingeGo.transform.position = jambLeftPos;
            hingeGo.transform.rotation = horizontal ? Quaternion.identity : Quaternion.Euler(0f, -90f, 0f);

            var leafGo = new GameObject("Door_Leaf");
            leafGo.transform.SetParent(hingeGo.transform, false);
            leafGo.transform.localPosition = new Vector3(0f, -leafH * 0.5f, 0f);
            leafGo.transform.localRotation = Quaternion.identity;
            leafGo.transform.localScale = Vector3.one;

            if (doorLeafMesh != null)
            {
                var mf = leafGo.AddComponent<MeshFilter>();
                mf.sharedMesh = doorLeafMesh;
                var mr = leafGo.AddComponent<MeshRenderer>();
                mr.sharedMaterial = doorMat;
            }
            else
            {
                var fallbackBox = CreateLocalBox("Door_Leaf_Fallback", leafGo.transform, new Vector3(leafW * 0.5f, leafH * 0.5f, 0f), new Vector3(leafW, leafH, 0.06f), doorMat);
            }

            var boxCol = leafGo.AddComponent<BoxCollider>();
            boxCol.center = new Vector3(leafW * 0.5f, leafH * 0.5f, 0f);
            boxCol.size = new Vector3(leafW, leafH, 0.08f);

            var doorType = System.Type.GetType("HollowTower.InteractiveDoor, Assembly-CSharp");
            if (doorType != null)
            {
                var doorComp = hingeGo.AddComponent(doorType);
                doorType.GetField("hingeTransform")?.SetValue(doorComp, hingeGo.transform);
                doorType.GetField("openAngle")?.SetValue(doorComp, 90f);
            }
        }

        static void PlaceFloorLighting(Transform parent, FloorPlan p, float baseY,
            Material matLampCool, Material matLampWarm, Material matLampRed, Material matLampDead, Material matBrass, Material matMetalHousing)
        {
            float fixtureCeilH = WallH - 0.05f;

            // 1. Corridor lamps - Placed identically to GitHub version:
            // (r == 6 || r == 17 || (f == 2 && r == 2)) ? c % 3 == 1 : r % 3 == 1
            if (p.corridor != null)
            {
                int n = 0;
                foreach (var cell in p.corridor.cells)
                {
                    int c = cell.x, r = cell.y;
                    if (p.pit[r, c]) continue;

                    bool lamp = (r == 6 || r == 17 || (p.floor == 2 && r == 2)) ? c % 6 == 1 : r % 6 == 1;
                    if (lamp)
                    {
                        n++;
                        LampStyle style = n % 9 == 4 ? LampStyle.Emergency : (p.floor == 2 ? LampStyle.Warm : LampStyle.Fluorescent);
                        Vector3 floorPos = new Vector3((c + 0.5f) * Cell, baseY, (LevelLayout.Rows - r - 0.5f) * Cell);
                        CreateLampFixture(parent, p.floor, floorPos, style, 500 + r * LevelLayout.Cols + c, fixtureCeilH,
                            matLampCool, matLampWarm, matLampRed, matLampDead, matBrass, matMetalHousing);
                    }
                }
            }

            // 2. Room lighting - Spaced on grid per room for clean performance
            foreach (var room in p.rooms)
            {
                if (room == p.corridor) continue;

                LampStyle style = DetermineRoomStyle(room);
                var R = room.rect;
                int nx = Mathf.Max(1, Mathf.RoundToInt(R.width / 4.8f));
                int nz = Mathf.Max(1, Mathf.RoundToInt(R.height / 4.8f));
                if (room.type == RoomType.Stair) { nx = 1; nz = 1; }

                for (int i = 0; i < nx; i++)
                {
                    for (int j = 0; j < nz; j++)
                    {
                        float cx = (R.x + (i + 0.5f) * ((float)R.width / nx)) * Cell;
                        float cz = (LevelLayout.Rows - R.y - (j + 0.5f) * ((float)R.height / nz)) * Cell;
                        Vector3 floorPos = new Vector3(cx, baseY, cz);
                        CreateLampFixture(parent, p.floor, floorPos, style, room.id * 31 + i * 7 + j, fixtureCeilH,
                            matLampCool, matLampWarm, matLampRed, matLampDead, matBrass, matMetalHousing);
                    }
                }
            }
        }

        static LampStyle DetermineRoomStyle(Room room)
        {
            switch (room.type)
            {
                case RoomType.Boiler:
                case RoomType.Generator:
                case RoomType.Maintenance:
                case RoomType.Loading:
                    return LampStyle.Industrial;
                case RoomType.Nurse:
                case RoomType.Lab:
                case RoomType.ComputerLab:
                    return LampStyle.Fluorescent;
                case RoomType.HotelRoom:
                case RoomType.Suite:
                case RoomType.Lounge:
                    return LampStyle.Warm;
                default:
                    return room.floor == 2 && room.type != RoomType.Washroom && room.type != RoomType.Laundry ? LampStyle.Warm : LampStyle.Fluorescent;
            }
        }

        static void CreateLampFixture(Transform parent, int floor, Vector3 floorPos, LampStyle style, int seed, float height,
            Material matLampCool, Material matLampWarm, Material matLampRed, Material matLampDead, Material matBrass, Material matMetalHousing)
        {
            var rnd = new System.Random(seed * 13 + floor * 1001);
            double roll = rnd.NextDouble();
            bool dead = roll < 0.16 && style != LampStyle.Emergency;
            bool flicker = !dead && roll < 0.32;

            float ceilY = floorPos.y + height;
            var holder = new GameObject("Lamp_Fixture").transform;
            holder.SetParent(parent, false);
            holder.position = new Vector3(floorPos.x, ceilY, floorPos.z);
            holder.gameObject.isStatic = true;

            Renderer fixtureR = null;
            float drop = 0f;

            if (style == LampStyle.Warm)
            {
                // High ceiling chain / rod + warm sphere globe
                drop = height > 4.5f ? height - 3.8f : 0.55f;
                var rod = CreatePrimitive(PrimitiveType.Cylinder, "Brass_Rod", holder, new Vector3(0f, -drop * 0.5f, 0f), new Vector3(0.025f, drop * 0.5f, 0.025f), matBrass);
                rod.isStatic = true;

                var globe = CreatePrimitive(PrimitiveType.Sphere, "Warm_Globe", holder, new Vector3(0f, -drop, 0f), new Vector3(0.42f, 0.38f, 0.42f), dead ? matLampDead : matLampWarm);
                globe.isStatic = true;
                fixtureR = globe.GetComponent<Renderer>();
            }
            else if (style == LampStyle.Emergency)
            {
                // Red emergency bulkhead fixture
                var emBox = CreateLocalBox("Emergency_Housing", holder, new Vector3(0f, -0.16f, 0f), new Vector3(0.42f, 0.22f, 0.42f), matLampRed);
                emBox.isStatic = true;
                fixtureR = emBox.GetComponent<Renderer>();
            }
            else
            {
                // Hanging metal cables
                CreatePrimitive(PrimitiveType.Cylinder, "Cable_L", holder, new Vector3(-0.45f, -0.07f, 0f), new Vector3(0.015f, 0.07f, 0.015f), matMetalHousing);
                CreatePrimitive(PrimitiveType.Cylinder, "Cable_R", holder, new Vector3(0.45f, -0.07f, 0f), new Vector3(0.015f, 0.07f, 0.015f), matMetalHousing);

                // Institutional Fluorescent Tube with sheet metal housing
                var housing = CreateLocalBox("Metal_Housing", holder, new Vector3(0f, -0.15f, 0f), new Vector3(1.4f, 0.10f, 0.42f), matMetalHousing);
                housing.isStatic = true;

                var tube = CreateLocalBox("Fluorescent_Tube", holder, new Vector3(0f, -0.22f, 0f), new Vector3(1.3f, 0.06f, 0.32f), dead ? matLampDead : matLampCool);
                tube.isStatic = true;
                fixtureR = tube.GetComponent<Renderer>();

                // 12% hung askew for decaying horror tone
                if (rnd.NextDouble() < 0.12)
                {
                    holder.localRotation = Quaternion.Euler(0f, 0f, 18f);
                }
            }

            if (fixtureR != null) fixtureR.shadowCastingMode = ShadowCastingMode.Off;

            // If lamp is dead, do NOT spawn light component (pure static prop)
            if (dead) return;

            // Light specifications
            Color col;
            float inten;
            float range;
            float spotAngle = 150f;

            switch (style)
            {
                case LampStyle.Warm:
                    col = new Color(1f, 0.8f, 0.58f);
                    inten = 1.2f;
                    range = Mathf.Max(8f, height + 2f);
                    spotAngle = 140f;
                    break;
                case LampStyle.Dim:
                    col = new Color(1f, 0.85f, 0.7f);
                    inten = 0.8f;
                    range = 6.5f;
                    spotAngle = 135f;
                    break;
                case LampStyle.Industrial:
                    col = new Color(1f, 0.75f, 0.5f);
                    inten = 1.1f;
                    range = Mathf.Max(8f, height + 2f);
                    spotAngle = 140f;
                    break;
                case LampStyle.Emergency:
                    col = new Color(1f, 0.1f, 0.05f);
                    inten = 1.0f;
                    range = 7f;
                    spotAngle = 135f;
                    break;
                default: // Fluorescent
                    col = new Color(0.86f, 0.93f, 1f);
                    inten = 1.3f;
                    range = 8.5f;
                    spotAngle = 140f;
                    break;
            }

            // Downward SpotLight: 1 shadow projection instead of 6 faces
            var lightGo = new GameObject("SpotLight_Downward");
            lightGo.transform.SetParent(holder, false);
            lightGo.transform.localPosition = new Vector3(0f, -0.28f - Mathf.Max(0f, drop - 0.45f), 0f);
            lightGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Spot;
            l.lightUnit = LightUnit.Lumen;
            l.enableSpotReflector = true;
            l.color = col;
            l.intensity = inten * Lumens;
            l.range = range;
            l.spotAngle = spotAngle;
            l.innerSpotAngle = spotAngle * 0.45f;
            l.shadows = LightShadows.None; // Optimized: LightOptimizer allocates shadows strictly to the 2 closest lights
            l.shapeRadius = 0.04f;

            var hdLight = lightGo.AddComponent<HDAdditionalLightData>();
            if (hdLight != null)
            {
                hdLight.SetShadowResolutionLevel(1);
                hdLight.affectsVolumetric = true;
                hdLight.volumetricDimmer = 0.25f;
                hdLight.shadowUpdateMode = ShadowUpdateMode.OnEnable;
                hdLight.alwaysDrawDynamicShadows = true;
            }

            if (flicker)
            {
                var flk = lightGo.AddComponent<RemasteredFlicker>();
                flk.Setup(l, fixtureR);
            }
        }

        static void SetupAtmosphere(GameObject root)
        {
            // Dark moonlight for atmosphere
            var dirLightGo = new GameObject("Directional_Moonlight");
            dirLightGo.transform.SetParent(root.transform, false);
            dirLightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            var dirLight = dirLightGo.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = new Color(0.12f, 0.18f, 0.28f);
            dirLight.intensity = 0.5f;
            dirLight.shadows = LightShadows.Soft;

            var hdDir = dirLightGo.AddComponent<HDAdditionalLightData>();
            if (hdDir != null)
            {
                hdDir.volumetricDimmer = 0.2f;
            }

            // Sky and Fog Volume
            var volGo = new GameObject("Global_Horror_Volume");
            volGo.transform.SetParent(root.transform, false);
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.priority = 1;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();

            // Atmospheric Volumetric Fog
            var fog = profile.Add<Fog>(true);
            fog.enabled.value = true;
            fog.albedo.value = new Color(0.04f, 0.06f, 0.09f);
            fog.meanFreePath.value = 70f;
            fog.baseHeight.value = 0f;
            fog.maximumHeight.value = 25f;
            fog.enableVolumetricFog.value = true;
            fog.anisotropy.value = 0.35f;

            // Adaptive Histogram Exposure matching tutorial horror look
            var exposure = profile.Add<Exposure>(true);
            exposure.mode.value = ExposureMode.AutomaticHistogram;
            exposure.meteringMode.value = MeteringMode.CenterWeighted;
            exposure.limitMin.value = 1.0f;
            exposure.limitMax.value = 11.5f;
            exposure.compensation.value = 0.0f;
            exposure.adaptationMode.value = AdaptationMode.Progressive;
            exposure.adaptationSpeedDarkToLight.value = 1.5f;
            exposure.adaptationSpeedLightToDark.value = 1.0f;
            exposure.histogramPercentages.value = new Vector2(40f, 92f);

            // Bloom (Tamed: gentle cinematic halo without burning white circles)
            var bloom = profile.Add<Bloom>(true);
            bloom.intensity.value = 0.08f;
            bloom.threshold.value = 1.8f;

            // Disable MotionBlur completely so camera looks sharp at all times
            var mb = profile.Add<MotionBlur>(true);
            mb.intensity.value = 0f;

            // Ambient Occlusion
            var ao = profile.Add<ScreenSpaceAmbientOcclusion>(true);
            ao.intensity.value = 0.6f;
            ao.directLightingStrength.value = 0.25f;

            // Tonemapping ACES
            var tone = profile.Add<Tonemapping>(true);
            tone.mode.value = TonemappingMode.ACES;

            vol.sharedProfile = profile;
            AssetDatabase.CreateAsset(profile, MatDir + "/HorrorVolumeProfile.asset");
            AssetDatabase.AddObjectToAsset(fog, profile);
            AssetDatabase.AddObjectToAsset(exposure, profile);
            AssetDatabase.AddObjectToAsset(bloom, profile);
            AssetDatabase.AddObjectToAsset(mb, profile);
            AssetDatabase.AddObjectToAsset(ao, profile);
            AssetDatabase.AddObjectToAsset(tone, profile);
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Hollow Tower/Remaster/4. Rebuild Staircases Only", priority = 53)]
        public static void RebuildStaircasesOnly()
        {
            var oldStairs = GameObject.Find("Staircases");
            if (oldStairs != null) UnityEngine.Object.DestroyImmediate(oldStairs);

            var root = GameObject.Find("HollowTower_Remastered_Environment");
            var stairsRoot = new GameObject("Staircases");
            if (root != null) stairsRoot.transform.SetParent(root.transform, false);
            stairsRoot.isStatic = true;

            var matFloor1 = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_F1_Floor.mat");
            var matFloor2 = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_F2_Floor.mat");
            var matFloor3 = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_F3_Floor.mat");
            var matSteel = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_Remastered_RustedSteel.mat");
            var matTrim = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_Remastered_HazardTrim.mat");
            var matLamp = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_Remastered_LampWarm.mat");

            for (int i = 0; i < LevelLayout.Stairs.Length; i++)
            {
                var s = LevelLayout.Stairs[i];
                Material stMat = (i == 0) ? matFloor1 : (i == 1 ? matFloor2 : matFloor3);
                BuildStaircase(stairsRoot.transform, s, stMat, matSteel, matTrim, matLamp);
            }

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("<color=green>[MapRemaster]</color> Rebuilt all 3 inter-floor staircases successfully.");
        }

        static void BuildStaircase(Transform parent, StairSpec s, Material rampMat, Material steelMat, Material trimMat, Material lampMat)
        {
            string stairName = $"Staircase_Floor_{s.lowerFloor + 1}_to_{s.lowerFloor + 2}";
            var stairGo = new GameObject(stairName);
            stairGo.transform.SetParent(parent, false);
            stairGo.isStatic = true;

            float baseY = s.lowerFloor * Story;
            bool climbWest = s.climbWest;

            // Ramp occupies 3 columns: s.col0, s.col0 + 1, s.col0 + 2 (12m run)
            float xWest = s.col0 * Cell;
            float xEast = (s.col0 + 3) * Cell;
            float lowX = climbWest ? xEast : xWest;
            float highX = climbWest ? xWest : xEast;
            float run = Mathf.Abs(highX - lowX); // 12m

            // Ramp occupies 2 rows: s.row0, s.row0 + 1
            float zNorth = (LevelLayout.Rows - s.row0) * Cell;
            float zSouth = (LevelLayout.Rows - (s.row0 + 2)) * Cell;
            float zCenter = (zNorth + zSouth) * 0.5f;
            float totalZ = 2f * Cell; // 8m
            float stairWidth = totalZ - 0.4f; // 7.6m wide

            float angle = Mathf.Atan2(Story, run) * Mathf.Rad2Deg; // approx 19.3 degrees
            float length = Mathf.Sqrt(run * run + Story * Story) + 0.2f;
            Vector3 mid = new Vector3((lowX + highX) * 0.5f, baseY + Story * 0.5f, zCenter);
            Quaternion rot = Quaternion.Euler(0f, 0f, highX > lowX ? angle : -angle);
            Vector3 normal = rot * Vector3.up;

            // 1. Solid Ramp Slab (Smooth Collider for flawless CharacterController navigation)
            var rampSlab = CreateBox("Ramp_Collision_Slab", stairGo.transform, mid - normal * 0.15f, new Vector3(length, 0.3f, stairWidth), rampMat);
            rampSlab.transform.localRotation = rot;
            rampSlab.isStatic = true;

            // 2. Individual Architectural Step Treads
            int numSteps = 21;
            float stepRun = run / numSteps;
            float stepRise = Story / numSteps;
            var stepsGo = new GameObject("Steps");
            stepsGo.transform.SetParent(stairGo.transform, false);
            stepsGo.isStatic = true;

            for (int i = 0; i < numSteps; i++)
            {
                float t = (i + 0.5f) / numSteps;
                float sx = Mathf.Lerp(lowX, highX, t);
                float sy = baseY + (i + 0.5f) * stepRise;
                Vector3 stepPos = new Vector3(sx, sy - stepRise * 0.5f, zCenter);
                var step = CreateBox($"Step_{i:00}", stepsGo.transform, stepPos, new Vector3(stepRun + 0.04f, stepRise, stairWidth), rampMat);
                step.isStatic = true;
                // Remove collider from step box so CharacterController walks on the smooth inclined ramp collider without snagging
                var col = step.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.DestroyImmediate(col);
            }

            // 3. Handrails and Balustrades (Left and Right along the ramp slope)
            var railsGo = new GameObject("Handrails");
            railsGo.transform.SetParent(stairGo.transform, false);
            railsGo.isStatic = true;

            for (int side = -1; side <= 1; side += 2)
            {
                float railZ = zCenter + side * (stairWidth * 0.5f - 0.12f);

                // Top Rail (1.0m height)
                Vector3 aTop = new Vector3(lowX, baseY + 1.0f, railZ);
                Vector3 bTop = new Vector3(highX, baseY + Story + 1.0f, railZ);
                var topRail = CreateBox(side > 0 ? "TopRail_North" : "TopRail_South", railsGo.transform, (aTop + bTop) * 0.5f, new Vector3(length, 0.07f, 0.07f), steelMat);
                topRail.transform.localRotation = rot;
                topRail.isStatic = true;

                // Mid Rail (0.5m height)
                Vector3 aMid = new Vector3(lowX, baseY + 0.5f, railZ);
                Vector3 bMid = new Vector3(highX, baseY + Story + 0.5f, railZ);
                var midRail = CreateBox(side > 0 ? "MidRail_North" : "MidRail_South", railsGo.transform, (aMid + bMid) * 0.5f, new Vector3(length, 0.04f, 0.04f), steelMat);
                midRail.transform.localRotation = rot;
                midRail.isStatic = true;

                // Vertical Support Posts (every ~2.5m)
                int numPosts = 6;
                for (int p = 0; p < numPosts; p++)
                {
                    float pt = (float)p / (numPosts - 1);
                    float px = Mathf.Lerp(lowX, highX, pt);
                    float py = baseY + pt * Story + 0.5f;
                    var post = CreateBox($"Post_{p}", railsGo.transform, new Vector3(px, py, railZ), new Vector3(0.07f, 1.0f, 0.07f), steelMat);
                    post.isStatic = true;
                }
            }

            // 4. Safety Guardrails around Open Shaft on the Upper Floor
            float upperY = baseY + Story;
            var guardGo = new GameObject("Upper_Floor_Safety_Guardrails");
            guardGo.transform.SetParent(stairGo.transform, false);
            guardGo.isStatic = true;

            // Drop end (the side opposite to highX landing)
            float dropX = climbWest ? (xEast - 0.12f) : (xWest + 0.12f);
            var endGuardTop = CreateBox("Guard_DropEnd_Top", guardGo.transform, new Vector3(dropX, upperY + 1.0f, zCenter), new Vector3(0.08f, 0.07f, stairWidth), steelMat);
            endGuardTop.isStatic = true;
            var endGuardMid = CreateBox("Guard_DropEnd_Mid", guardGo.transform, new Vector3(dropX, upperY + 0.5f, zCenter), new Vector3(0.08f, 0.04f, stairWidth), steelMat);
            endGuardMid.isStatic = true;

            // 5. Atmospheric Stairwell Lighting
            var lightsGo = new GameObject("Stairwell_Lighting");
            lightsGo.transform.SetParent(stairGo.transform, false);

            Vector3 lightPos1 = new Vector3(Mathf.Lerp(lowX, highX, 0.25f), baseY, zCenter);
            CreateLampFixture(lightsGo.transform, s.lowerFloor, lightPos1, LampStyle.Warm, 9001 + s.lowerFloor, WallH - 0.05f,
                lampMat, lampMat, lampMat, lampMat, steelMat, steelMat);

            Vector3 lightPos2 = new Vector3(Mathf.Lerp(lowX, highX, 0.75f), baseY + Story, zCenter);
            CreateLampFixture(lightsGo.transform, s.lowerFloor + 1, lightPos2, LampStyle.Warm, 9002 + s.lowerFloor, WallH - 0.05f,
                lampMat, lampMat, lampMat, lampMat, steelMat, steelMat);
        }

        static GameObject CreateBox(string name, Transform parent, Vector3 pos, Vector3 size, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = size;
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
            return go;
        }

        static GameObject CreateLocalBox(string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
            var c = go.GetComponent<Collider>();
            if (c != null) UnityEngine.Object.DestroyImmediate(c);
            return go;
        }

        static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
            var c = go.GetComponent<Collider>();
            if (c != null) UnityEngine.Object.DestroyImmediate(c);
            return go;
        }

        static GameObject CreateBlenderElevatorInstance(Transform parentRoot, string elevatorName, Vector3 position, Quaternion rotation)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ReadyMadeAssets/Elevator/Elevator_Fixed.fbx")
                      ?? AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ReadyMadeAssets/Elevator/Elevator.fbx");
            if (prefab == null)
            {
                Debug.LogError("[MapRemasterBuilder] Could not find Elevator_Fixed.fbx or Elevator.fbx in Assets/ReadyMadeAssets/Elevator/!");
                return null;
            }

            GameObject elevObj = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            elevObj.name = elevatorName;
            elevObj.transform.SetParent(parentRoot, false);
            elevObj.transform.position = position;
            elevObj.transform.rotation = rotation;

            // Strip legacy FBX animation so root position tracks don't snap elevator to (0,0,0)
            var anim = elevObj.GetComponent<Animation>();
            if (anim != null) UnityEngine.Object.DestroyImmediate(anim);

            // Bind authentic HDRP Lit materials
            ApplyElevatorMaterials(elevObj);

            // Align double sliding doors seamlessly (compensate FBX export root origin)
            var ld = elevObj.transform.Find("Exterior_Front_Assembly/LeftSlidingDoor");
            var rd = elevObj.transform.Find("Exterior_Front_Assembly/RightSlidingDoor");
            if (ld != null && rd != null)
            {
                rd.localPosition = ld.localPosition - new Vector3(0.0067f, 0f, 0f);
            }

            // Add colliders for floors, walls, roof, and sliding doors
            AddElevatorColliders(elevObj);

            return elevObj;
        }

        static void ApplyElevatorMaterials(GameObject elevatorRoot)
        {
            if (elevatorRoot == null) return;
            const string matDir = "Assets/Materials/Elevator";
            var renderers = elevatorRoot.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    string cleanName = mats[i].name.Replace(" (Instance)", "");
                    var pbrMat = AssetDatabase.LoadAssetAtPath<Material>($"{matDir}/{cleanName}.mat");
                    if (pbrMat != null && mats[i] != pbrMat)
                    {
                        mats[i] = pbrMat;
                        changed = true;
                    }
                }
                if (changed) r.sharedMaterials = mats;
            }
        }

        static void AddElevatorColliders(GameObject elevatorRoot)
        {
            if (elevatorRoot == null) return;

            string[] meshParts = new string[]
            {
                "Base_SkidChassis/Base_FloorThreshold",
                "Interior_Assembly/Interior_Floor_Treadplate",
                "Interior_Assembly/Interior_Wall_Left",
                "Interior_Assembly/Interior_Wall_Rear",
                "Interior_Assembly/Interior_Wall_Right",
                "Interior_Assembly/Interior_Ceiling_Panel",
                "Exterior_Left_Assembly",
                "Exterior_Rear_Assembly",
                "Exterior_Right_Assembly",
                "Top_RoofSlab"
            };

            foreach (string path in meshParts)
            {
                var t = elevatorRoot.transform.Find(path);
                if (t != null)
                {
                    var mfs = t.GetComponentsInChildren<MeshFilter>(true);
                    foreach (var mf in mfs)
                    {
                        if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
                        {
                            var mc = mf.gameObject.AddComponent<MeshCollider>();
                            mc.sharedMesh = mf.sharedMesh;
                        }
                    }
                }
            }

            // Door colliders (convex for dynamic movement)
            var ld = elevatorRoot.transform.Find("Exterior_Front_Assembly/LeftSlidingDoor");
            if (ld != null)
            {
                var mf = ld.GetComponentInChildren<MeshFilter>(true);
                if (mf != null && mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
                {
                    var mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    mc.convex = true;
                }
            }

            var rd = elevatorRoot.transform.Find("Exterior_Front_Assembly/RightSlidingDoor");
            if (rd != null)
            {
                var mf = rd.GetComponentInChildren<MeshFilter>(true);
                if (mf != null && mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
                {
                    var mc = mf.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    mc.convex = true;
                }
            }

            // Solid cabin boundary box colliders (parented to elevatorRoot's parent so scale is strictly 1.0)
            var cabinColObj = new GameObject(elevatorRoot.name + "_Cabin_Colliders");
            cabinColObj.transform.SetParent(elevatorRoot.transform.parent, false);
            cabinColObj.transform.position = elevatorRoot.transform.position;
            cabinColObj.transform.rotation = Quaternion.identity;

            // Solid floor slab
            var floorBox = cabinColObj.AddComponent<BoxCollider>();
            floorBox.center = new Vector3(0f, 0.06f, 0f);
            floorBox.size = new Vector3(2.6f, 0.12f, 2.6f);

            // Rear wall (East, +X)
            var rearWall = new GameObject("Rear_Wall_Col");
            rearWall.transform.SetParent(cabinColObj.transform, false);
            var rCol = rearWall.AddComponent<BoxCollider>();
            rCol.center = new Vector3(1.15f, 1.4f, 0f);
            rCol.size = new Vector3(0.2f, 2.8f, 2.6f);

            // Left wall (North, +Z)
            var leftWall = new GameObject("Left_Wall_Col");
            leftWall.transform.SetParent(cabinColObj.transform, false);
            var lCol = leftWall.AddComponent<BoxCollider>();
            lCol.center = new Vector3(0f, 1.4f, 1.25f);
            lCol.size = new Vector3(2.4f, 2.8f, 0.2f);

            // Right wall (South, -Z)
            var rightWall = new GameObject("Right_Wall_Col");
            rightWall.transform.SetParent(cabinColObj.transform, false);
            var rtCol = rightWall.AddComponent<BoxCollider>();
            rtCol.center = new Vector3(0f, 1.4f, -1.25f);
            rtCol.size = new Vector3(2.4f, 2.8f, 0.2f);

            // Roof slab (Y = 2.45)
            var roofWall = new GameObject("Roof_Col");
            roofWall.transform.SetParent(cabinColObj.transform, false);
            var rfCol = roofWall.AddComponent<BoxCollider>();
            rfCol.center = new Vector3(0f, 2.5f, 0f);
            rfCol.size = new Vector3(2.6f, 0.2f, 2.6f);
        }

        static void BuildSpawnElevator(Transform floor1Root, Material steelMat, Material trimMat, Material brassMat, Material lampMat)
        {
            Transform oldSpawn = floor1Root.Find("Floor_1_SpawnElevator");
            if (oldSpawn != null) UnityEngine.Object.DestroyImmediate(oldSpawn.gameObject);
            Transform oldSpawnCols = floor1Root.Find("Floor_1_SpawnElevator_Cabin_Colliders");
            if (oldSpawnCols != null) UnityEngine.Object.DestroyImmediate(oldSpawnCols.gameObject);
            Transform oldBlender = floor1Root.Find("Floor_1_Blender_Elevator");
            if (oldBlender != null) UnityEngine.Object.DestroyImmediate(oldBlender.gameObject);

            // Floor 1 "Parking access" room (X: 120-144, Z: 0-24, Y: 0.05)
            // East wall is at X = 144m. Elevator cabin is placed against East wall at Z = 12m.
            Vector3 elevPos = new Vector3(141.60f, 0.05f, 12.00f);
            Quaternion elevRot = Quaternion.Euler(270f, 270f, 0f);

            var elevObj = CreateBlenderElevatorInstance(floor1Root, "Floor_1_SpawnElevator", elevPos, elevRot);
            if (elevObj == null) return;

            // Find doors
            var doorL = elevObj.transform.Find("Exterior_Front_Assembly/LeftSlidingDoor");
            var doorR = elevObj.transform.Find("Exterior_Front_Assembly/RightSlidingDoor");

            // Interior Warm Spotlight
            var lightGo = new GameObject("Spawn_Interior_Light");
            lightGo.transform.SetParent(elevObj.transform, false);
            lightGo.transform.position = new Vector3(elevPos.x, elevPos.y + 2.2f, elevPos.z);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = 130f;
            light.innerSpotAngle = 80f;
            light.color = new Color(1.0f, 0.95f, 0.85f);
            light.intensity = 1800f;
            light.range = 5.0f;
            light.shadows = LightShadows.None;

            // Elevator local fixture lighting (warm fluorescents and amber marquee)
            var lightsParent = new GameObject("Elevator_Local_Lights");
            lightsParent.transform.SetParent(elevObj.transform, false);

            var lDoorLeft = new GameObject("Light_Door_Left").AddComponent<Light>();
            lDoorLeft.transform.SetParent(lightsParent.transform, false);
            lDoorLeft.transform.position = new Vector3(elevPos.x - 1.1f, elevPos.y + 1.4f, elevPos.z + 0.7f);
            lDoorLeft.type = LightType.Point;
            lDoorLeft.color = new Color(1.0f, 0.95f, 0.85f);
            lDoorLeft.intensity = 450f;
            lDoorLeft.range = 3.5f;

            var lDoorRight = new GameObject("Light_Door_Right").AddComponent<Light>();
            lDoorRight.transform.SetParent(lightsParent.transform, false);
            lDoorRight.transform.position = new Vector3(elevPos.x - 1.1f, elevPos.y + 1.4f, elevPos.z - 0.7f);
            lDoorRight.type = LightType.Point;
            lDoorRight.color = new Color(1.0f, 0.95f, 0.85f);
            lDoorRight.intensity = 450f;
            lDoorRight.range = 3.5f;

            var lMarquee = new GameObject("Light_Marquee_Amber").AddComponent<Light>();
            lMarquee.transform.SetParent(lightsParent.transform, false);
            lMarquee.transform.position = new Vector3(elevPos.x - 1.15f, elevPos.y + 2.25f, elevPos.z);
            lMarquee.type = LightType.Point;
            lMarquee.color = new Color(1.0f, 0.75f, 0.20f);
            lMarquee.intensity = 600f;
            lMarquee.range = 3.0f;

            // Spawn Elevator Controller
            var spawnElevComp = elevObj.AddComponent<TowerSpawnElevator>();
            spawnElevComp.doorLeft = doorL;
            spawnElevComp.doorRight = doorR;
            spawnElevComp.doorSlideVector = new Vector3(0.008f, 0f, 0f);
            spawnElevComp.interiorLight = light;
            if (doorL != null && doorR != null)
            {
                spawnElevComp.leftClosedPos = doorL.localPosition;
                spawnElevComp.rightClosedPos = doorR.localPosition;
            }
        }

        static void BuildExitElevator(Transform floor4Root, Material steelMat, Material trimMat, Material brassMat, Material lampMat)
        {
            Transform oldExit = floor4Root.Find("Floor_4_ExitElevator");
            if (oldExit != null) UnityEngine.Object.DestroyImmediate(oldExit.gameObject);
            Transform oldExitCols = floor4Root.Find("Floor_4_ExitElevator_Cabin_Colliders");
            if (oldExitCols != null) UnityEngine.Object.DestroyImmediate(oldExitCols.gameObject);
            Transform oldBlender = floor4Root.Find("Floor_4_Blender_Elevator");
            if (oldBlender != null) UnityEngine.Object.DestroyImmediate(oldBlender.gameObject);

            // Floor 4 "Storage 2" room (X: 120-144, Z: 0-12, Y: 12.65)
            // East wall is at X = 144m. Elevator cabin is placed against East wall at Z = 6.0m.
            Vector3 elevPos = new Vector3(141.60f, 12.65f, 6.00f);
            Quaternion elevRot = Quaternion.Euler(270f, 270f, 0f);

            var elevObj = CreateBlenderElevatorInstance(floor4Root, "Floor_4_ExitElevator", elevPos, elevRot);
            if (elevObj == null) return;

            // Find doors
            var doorL = elevObj.transform.Find("Exterior_Front_Assembly/LeftSlidingDoor");
            var doorR = elevObj.transform.Find("Exterior_Front_Assembly/RightSlidingDoor");

            // Interior Light
            var lightGo = new GameObject("Exit_Interior_Light");
            lightGo.transform.SetParent(elevObj.transform, false);
            lightGo.transform.position = new Vector3(elevPos.x, elevPos.y + 2.2f, elevPos.z);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = 130f;
            light.innerSpotAngle = 80f;
            light.color = new Color(0.85f, 1.0f, 0.9f);
            light.intensity = 1800f;
            light.range = 5.0f;
            light.shadows = LightShadows.None;

            // Elevator local fixture lighting
            var lightsParent = new GameObject("Elevator_Local_Lights");
            lightsParent.transform.SetParent(elevObj.transform, false);

            var lDoorLeft = new GameObject("Light_Door_Left").AddComponent<Light>();
            lDoorLeft.transform.SetParent(lightsParent.transform, false);
            lDoorLeft.transform.position = new Vector3(elevPos.x - 1.1f, elevPos.y + 1.4f, elevPos.z + 0.7f);
            lDoorLeft.type = LightType.Point;
            lDoorLeft.color = new Color(1.0f, 0.95f, 0.85f);
            lDoorLeft.intensity = 450f;
            lDoorLeft.range = 3.5f;

            var lDoorRight = new GameObject("Light_Door_Right").AddComponent<Light>();
            lDoorRight.transform.SetParent(lightsParent.transform, false);
            lDoorRight.transform.position = new Vector3(elevPos.x - 1.1f, elevPos.y + 1.4f, elevPos.z - 0.7f);
            lDoorRight.type = LightType.Point;
            lDoorRight.color = new Color(1.0f, 0.95f, 0.85f);
            lDoorRight.intensity = 450f;
            lDoorRight.range = 3.5f;

            var lMarquee = new GameObject("Light_Marquee_Amber").AddComponent<Light>();
            lMarquee.transform.SetParent(lightsParent.transform, false);
            lMarquee.transform.position = new Vector3(elevPos.x - 1.15f, elevPos.y + 2.25f, elevPos.z);
            lMarquee.type = LightType.Point;
            lMarquee.color = new Color(1.0f, 0.75f, 0.20f);
            lMarquee.intensity = 600f;
            lMarquee.range = 3.0f;

            Transform oldTrigger = floor4Root.Find("Exit_Trigger_Zone");
            if (oldTrigger != null) UnityEngine.Object.DestroyImmediate(oldTrigger.gameObject);

            // Trigger zone inside cabin for player detection (parented to floor4Root so scale is 1.0)
            var triggerGo = new GameObject("Exit_Trigger_Zone");
            triggerGo.transform.SetParent(floor4Root, false);
            triggerGo.transform.position = new Vector3(elevPos.x, elevPos.y + 1.2f, elevPos.z);
            var boxCol = triggerGo.AddComponent<BoxCollider>();
            boxCol.isTrigger = true;
            boxCol.size = new Vector3(1.6f, 2.2f, 1.6f);

            var rb = triggerGo.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // Exit Elevator Controller
            var exitElevComp = triggerGo.AddComponent<TowerExitElevator>();
            exitElevComp.doorLeft = doorL;
            exitElevComp.doorRight = doorR;
            exitElevComp.doorSlideVector = new Vector3(0.008f, 0f, 0f);
            exitElevComp.interiorLight = light;
            if (doorL != null && doorR != null)
            {
                exitElevComp.leftClosedPos = doorL.localPosition;
                exitElevComp.rightClosedPos = doorR.localPosition;
                doorL.localPosition = exitElevComp.leftClosedPos + exitElevComp.doorSlideVector;
                doorR.localPosition = exitElevComp.rightClosedPos - exitElevComp.doorSlideVector;
            }
        }

        [MenuItem("Hollow Tower/Remaster/6. Rebuild Elevators Only", priority = 55)]
        public static void RebuildElevatorsOnly()
        {
            var f1 = GameObject.Find("Floor_1_Ground");
            var f4 = GameObject.Find("Floor_4_Offices");
            if (f1 != null) BuildSpawnElevator(f1.transform, null, null, null, null);
            if (f4 != null) BuildExitElevator(f4.transform, null, null, null, null);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("<color=green>[Elevators]</color> Successfully rebuilt Floor 1 and Floor 4 Blender Elevators!");
        }

        [MenuItem("Hollow Tower/Remaster/7. Rebuild Doorways Only", priority = 56)]
        public static void RebuildDoorwaysOnly()
        {
            var matF1Wall = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_F1_Wall.mat");
            var matF2Wall = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_F2_Wall.mat");
            var matF3Wall = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_F3_Wall.mat");
            var matF4Wall = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_F4_Wall.mat");
            var matTrim = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_Remastered_HazardTrim.mat");

            var matDoorTactical = AssetDatabase.LoadAssetAtPath<Material>("Assets/HollowTowerRemastered/Materials/Doors/Mat_Door_TacticalSteel.mat");
            var matDoorCleanLab = AssetDatabase.LoadAssetAtPath<Material>("Assets/HollowTowerRemastered/Materials/Doors/Mat_Door_CleanLab.mat");
            var matDoorExecutive = AssetDatabase.LoadAssetAtPath<Material>("Assets/HollowTowerRemastered/Materials/Doors/Mat_Door_ExecutiveWood.mat");
            var matDoorFrame = AssetDatabase.LoadAssetAtPath<Material>("Assets/HollowTowerRemastered/Materials/Doors/Mat_Door_FrameDarkSteel.mat");
            var doorLeafMesh = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/HollowTowerRemastered/Meshes/Door_Leaf_Mesh.asset");

            var plans = LevelLayout.Plans;

            for (int f = 0; f < plans.Length; f++)
            {
                var p = plans[f];
                string floorName = $"Floor_{f + 1}_{p.theme}";
                var floorRoot = GameObject.Find(floorName);
                if (floorRoot == null) continue;

                var wallsRoot = floorRoot.transform.Find("Walls_And_Doors");
                if (wallsRoot == null) continue;

                // Remove existing Doorway_H and Doorway_V children
                var toDestroy = new System.Collections.Generic.List<GameObject>();
                foreach (Transform child in wallsRoot)
                {
                    if (child.name.StartsWith("Doorway"))
                    {
                        toDestroy.Add(child.gameObject);
                    }
                }
                foreach (var go in toDestroy)
                {
                    Undo.DestroyObjectImmediate(go);
                }

                float baseY = f * Story;
                Material wallForFloor = f == 0 ? matF1Wall : (f == 1 ? matF2Wall : (f == 2 ? matF3Wall : matF4Wall));
                Material doorForFloor = f == 0 ? matDoorTactical : (f == 1 ? matDoorCleanLab : (f == 2 ? matDoorExecutive : matDoorTactical));

                // Horizontal door edges
                for (int b = 0; b <= LevelLayout.Rows; b++)
                {
                    float z = (LevelLayout.Rows - b) * Cell;
                    for (int c = 0; c < LevelLayout.Cols; c++)
                    {
                        int zn = b > 0 ? p.zone[b - 1, c] : -1;
                        int zs = b < LevelLayout.Rows ? p.zone[b, c] : -1;
                        if (zn == zs) continue;

                        float xCenter = (c + 0.5f) * Cell;
                        Vector3 pos = new Vector3(xCenter, baseY + WallH * 0.5f, z);
                        int edgeKey = LevelLayout.EdgeS(Mathf.Clamp(b - 1, 0, LevelLayout.Rows - 1), c);
                        if (p.doors.Contains(edgeKey))
                        {
                            BuildDoorwaySection(wallsRoot, pos, true, wallForFloor, doorForFloor, matDoorFrame, matTrim, doorLeafMesh);
                        }
                    }
                }

                // Vertical door edges
                for (int a = 0; a <= LevelLayout.Cols; a++)
                {
                    float x = a * Cell;
                    for (int r = 0; r < LevelLayout.Rows; r++)
                    {
                        int zw = a > 0 ? p.zone[r, a - 1] : -1;
                        int ze = a < LevelLayout.Cols ? p.zone[r, a] : -1;
                        if (zw == ze) continue;

                        float zCenter = (LevelLayout.Rows - r - 0.5f) * Cell;
                        Vector3 pos = new Vector3(x, baseY + WallH * 0.5f, zCenter);
                        int edgeKey = LevelLayout.EdgeE(r, Mathf.Clamp(a - 1, 0, LevelLayout.Cols - 1));
                        if (p.doors.Contains(edgeKey))
                        {
                            BuildDoorwaySection(wallsRoot, pos, false, wallForFloor, doorForFloor, matDoorFrame, matTrim, doorLeafMesh);
                        }
                    }
                }
            }

            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("<color=green>[Doorways]</color> Successfully rebuilt all facility doorways with Clean PBR Doors and Frames!");
        }

#endif

        [MenuItem("Hollow Tower/Remaster/5. Bake Tower NavMesh", priority = 54)]
        public static void BakeTowerNavMesh()
        {
            BakeTowerNavMeshInternal();
            Debug.Log("<color=green>[NavMesh]</color> Successfully baked 4-floor NavMesh for all rooms, corridors, and staircases!");
        }

        static void BakeTowerNavMeshInternal()
        {
            try
            {
#pragma warning disable CS0618
                UnityEditor.AI.NavMeshBuilder.ClearAllNavMeshes();
                UnityEditor.AI.NavMeshBuilder.BuildNavMesh();
#pragma warning restore CS0618
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[NavMesh] Notice during NavMesh build: " + ex.Message);
            }
        }
    }
}
