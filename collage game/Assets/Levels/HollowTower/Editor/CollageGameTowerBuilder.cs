using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using HollowTower;

namespace HollowTower.EditorTools
{
    public static class CollageGameTowerBuilder
    {
        public const string BaseDir = "Assets/Levels/HollowTower";
        public const string TexDir = BaseDir + "/Textures";
        public const string UserTexDir = TexDir + "/UserTextures";
        public const string MatDir = BaseDir + "/Materials";
        public const string ScenePath = "Assets/Scenes/HollowTower_Remastered.unity";
        public const string TutorialScenePath = "Assets/Scenes/TutorialScene.unity";

        public const float Cell = 4.0f;
        public const float Story = 4.2f;
        public const float SlabThick = 0.20f;
        public const float WallH = Story - SlabThick; // 4.00m
        public const float WallThick = 0.25f;
        public const float DoorW = 1.8f;
        public const float DoorH = 2.6f;

        [MenuItem("FLIP//FALL/Tower/1. Generate URP Tower Level", priority = 10)]
        public static void GenerateURPTowerLevel()
        {
            ConfigureURPMaterials();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));

            var root = new GameObject("HollowTower_URP_Environment");
            root.isStatic = true;

            SetupURPAtmosphere(root);

            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
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

            var matSteel = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_Remastered_RustedSteel.mat");
            var matTrim = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_Remastered_HazardTrim.mat");
            var matLampCool = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_Remastered_LampCool.mat");
            var matLampWarm = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_Remastered_LampWarm.mat");
            var matLampRed = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_Remastered_LampRed.mat");
            var matLampDead = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_Remastered_LampDead.mat");
            var matBrass = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_Remastered_Brass.mat");
            var matMetalHousing = AssetDatabase.LoadAssetAtPath<Material>(MatDir + "/Mat_Remastered_MetalHousing.mat");

            var plans = LevelLayout.Plans;

            for (int f = 0; f < plans.Length; f++)
            {
                var p = plans[f];
                var floorRoot = new GameObject($"Floor_{f + 1}_{p.theme}");
                floorRoot.transform.SetParent(root.transform, false);
                floorRoot.isStatic = true;

                float baseY = f * Story;

                Material wallForFloor = (f == 0) ? matF1Wall : (f == 1 ? matF2Wall : (f == 2 ? matF3Wall : matF4Wall));
                Material floorForFloor = (f == 0) ? matF1Floor : (f == 1 ? matF2Floor : (f == 2 ? matF3Floor : matF4Floor));
                Material ceilForFloor = (f == 0) ? matF1Ceil : (f == 1 ? matF2Ceil : (f == 2 ? matF3Ceil : matF4Ceil));

                // 1. Floor Slabs, Pit Holes & Ceilings
                var slabsRoot = new GameObject("Slabs_And_Ceilings");
                slabsRoot.transform.SetParent(floorRoot.transform, false);
                slabsRoot.isStatic = true;

                for (int r = 0; r < LevelLayout.Rows; r++)
                {
                    for (int c = 0; c < LevelLayout.Cols; c++)
                    {
                        if (p.zone[r, c] < 0) continue;

                        Vector3 cellCenter = new Vector3((c + 0.5f) * Cell, baseY, (LevelLayout.Rows - r - 0.5f) * Cell);

                        if (!p.shaft[r, c])
                        {
                            if (p.pit[r, c])
                            {
                                float ledgeW = 0.6f;
                                float holeSize = Cell - ledgeW * 2f;

                                CreateBox("PitLedge_N", slabsRoot.transform, cellCenter + new Vector3(0f, -SlabThick * 0.5f, (Cell - ledgeW) * 0.5f), new Vector3(Cell, SlabThick, ledgeW), floorForFloor).isStatic = true;
                                CreateBox("PitLedge_S", slabsRoot.transform, cellCenter + new Vector3(0f, -SlabThick * 0.5f, -(Cell - ledgeW) * 0.5f), new Vector3(Cell, SlabThick, ledgeW), floorForFloor).isStatic = true;
                                CreateBox("PitLedge_W", slabsRoot.transform, cellCenter + new Vector3(-(Cell - ledgeW) * 0.5f, -SlabThick * 0.5f, 0f), new Vector3(ledgeW, SlabThick, holeSize), floorForFloor).isStatic = true;
                                CreateBox("PitLedge_E", slabsRoot.transform, cellCenter + new Vector3((Cell - ledgeW) * 0.5f, -SlabThick * 0.5f, 0f), new Vector3(ledgeW, SlabThick, holeSize), floorForFloor).isStatic = true;

                                var pitBottom = CreateBox("PitBottom", slabsRoot.transform, cellCenter + new Vector3(0f, -2.5f, 0f), new Vector3(holeSize, 0.2f, holeSize), matSteel);
                                pitBottom.isStatic = true;

                                var hazardGo = new GameObject("PitHazardTrigger");
                                hazardGo.transform.SetParent(slabsRoot.transform, false);
                                hazardGo.transform.position = cellCenter + new Vector3(0f, -1.0f, 0f);
                                var boxCol = hazardGo.AddComponent<BoxCollider>();
                                boxCol.size = new Vector3(holeSize, 2.0f, holeSize);
                                boxCol.isTrigger = true;

                                var hazardType = System.Type.GetType("HollowTower.TowerPitHazard, Assembly-CSharp");
                                if (hazardType != null)
                                {
                                    var hazard = hazardGo.AddComponent(hazardType);
                                    hazardType.GetField("safeRespawnPosition")?.SetValue(hazard, cellCenter + new Vector3(0f, 0.05f, 2.5f));
                                }
                            }
                            else
                            {
                                var fl = CreateBox("FloorSlab", slabsRoot.transform, cellCenter + Vector3.down * (SlabThick * 0.5f), new Vector3(Cell, SlabThick, Cell), floorForFloor);
                                fl.isStatic = true;
                            }
                        }

                        bool hasCeiling = true;
                        if (f < plans.Length - 1 && plans[f + 1].shaft[r, c]) hasCeiling = false;
                        if (p.ramp[r, c]) hasCeiling = false;

                        if (hasCeiling)
                        {
                            var ceil = CreateBox("CeilingSlab", slabsRoot.transform, cellCenter + Vector3.up * (WallH - 0.025f), new Vector3(Cell, 0.05f, Cell), ceilForFloor);
                            ceil.isStatic = true;

                            if (f == plans.Length - 1)
                            {
                                var roof = CreateBox("RoofSlab", slabsRoot.transform, cellCenter + Vector3.up * (WallH + SlabThick * 0.5f), new Vector3(Cell, SlabThick, Cell), matTrim);
                                roof.isStatic = true;
                            }
                        }
                    }
                }

                // 2. Walls and Interactive Doors
                var wallsRoot = new GameObject("Walls_And_Partitions");
                wallsRoot.transform.SetParent(floorRoot.transform, false);
                wallsRoot.isStatic = true;

                // Horizontal edges (row boundaries)
                for (int r = 0; r < LevelLayout.Rows - 1; r++)
                {
                    for (int c = 0; c < LevelLayout.Cols; c++)
                    {
                        int z0 = p.zone[r, c];
                        int z1 = p.zone[r + 1, c];
                        if (z0 == z1) continue;

                        int edgeKey = LevelLayout.EdgeS(r, c);
                        bool isDoor = p.doors.Contains(edgeKey);
                        Vector3 pos = new Vector3((c + 0.5f) * Cell, baseY + WallH * 0.5f, (LevelLayout.Rows - 1 - r) * Cell);

                        if (isDoor)
                        {
                            BuildDoorwaySection(wallsRoot.transform, pos, true, wallForFloor, matSteel, matTrim);
                        }
                        else
                        {
                            var w = CreateBox("Wall_H", wallsRoot.transform, pos, new Vector3(Cell, WallH, WallThick), wallForFloor);
                            w.isStatic = true;
                            CreateBox("Baseboard_H", wallsRoot.transform, pos - new Vector3(0f, WallH * 0.5f - 0.09f, 0f), new Vector3(Cell, 0.18f, WallThick + 0.04f), matTrim).isStatic = true;
                        }
                    }
                }

                // Vertical edges (column boundaries)
                for (int r = 0; r < LevelLayout.Rows; r++)
                {
                    for (int c = 0; c < LevelLayout.Cols - 1; c++)
                    {
                        int z0 = p.zone[r, c];
                        int z1 = p.zone[r, c + 1];
                        if (z0 == z1) continue;

                        int edgeKey = LevelLayout.EdgeE(r, c);
                        bool isDoor = p.doors.Contains(edgeKey);
                        Vector3 pos = new Vector3((c + 1) * Cell, baseY + WallH * 0.5f, (LevelLayout.Rows - r - 0.5f) * Cell);

                        if (isDoor)
                        {
                            BuildDoorwaySection(wallsRoot.transform, pos, false, wallForFloor, matSteel, matTrim);
                        }
                        else
                        {
                            var w = CreateBox("Wall_V", wallsRoot.transform, pos, new Vector3(WallThick, WallH, Cell), wallForFloor);
                            w.isStatic = true;
                            CreateBox("Baseboard_V", wallsRoot.transform, pos - new Vector3(0f, WallH * 0.5f - 0.09f, 0f), new Vector3(WallThick + 0.04f, 0.18f, Cell), matTrim).isStatic = true;
                        }
                    }
                }

                // 3. URP Lighting Fixtures
                var lightsRoot = new GameObject("Lighting_Fixtures");
                lightsRoot.transform.SetParent(floorRoot.transform, false);
                PlaceURPFloorLighting(lightsRoot.transform, p, baseY, matLampCool, matLampWarm, matLampRed, matLampDead, matBrass, matMetalHousing);
            }

            // 4. Inter-Floor Staircases
            var stairsRoot = new GameObject("Staircases");
            stairsRoot.transform.SetParent(root.transform, false);
            stairsRoot.isStatic = true;
            for (int i = 0; i < LevelLayout.Stairs.Length; i++)
            {
                var s = LevelLayout.Stairs[i];
                Material stMat = (i == 0) ? matF1Floor : (i == 1 ? matF2Floor : matF3Floor);
                BuildStaircase(stairsRoot.transform, s, stMat, matSteel, matTrim);
            }

            // 5. Central 4-Floor Grand Elevator
            BuildTowerElevator(root.transform, matSteel, matTrim, matBrass, matLampWarm);

            // 6. Bake NavMesh
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

            // Spawn Player Character
            SpawnControlledPlayer();

            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.Refresh();
            Debug.Log($"<color=green>[CollageGame]</color> Successfully built URP Remastered Tower Scene at: {ScenePath}");
        }

        [MenuItem("FLIP//FALL/Tower/2. Open Tower Scene", priority = 11)]
        public static void OpenTowerScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(ScenePath);
            }
        }

        [MenuItem("FLIP//FALL/Tower/3. Open Tutorial Scene", priority = 12)]
        public static void OpenTutorialScene()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(TutorialScenePath);
            }
        }

        static void ConfigureURPMaterials()
        {
            Directory.CreateDirectory(MatDir);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");

            CreateURPMaterial("Mat_F1_Wall", lit, UserTexDir + "/walls/PaintedBricks001_2K-PNG/PaintedBricks001_2K-PNG_Color.png", UserTexDir + "/walls/PaintedBricks001_2K-PNG/PaintedBricks001_2K-PNG_NormalGL.png", new Vector2(2f, 2f), 0.25f, 0f);
            CreateURPMaterial("Mat_F1_Floor", lit, UserTexDir + "/floors/Bricks066_2K-PNG/Bricks066_2K-PNG_Color.png", UserTexDir + "/floors/Bricks066_2K-PNG/Bricks066_2K-PNG_NormalGL.png", new Vector2(2f, 2f), 0.35f, 0f);
            CreateURPMaterial("Mat_F1_Ceil", lit, UserTexDir + "/ceilings/OfficeCeiling005_2K-PNG/OfficeCeiling005_2K-PNG_Color.png", UserTexDir + "/ceilings/OfficeCeiling005_2K-PNG/OfficeCeiling005_2K-PNG_NormalGL.png", new Vector2(1.5f, 1.5f), 0.25f, 0f);

            CreateURPMaterial("Mat_F2_Wall", lit, UserTexDir + "/walls/Tiles011_2K-PNG/Tiles011_2K-PNG_Color.png", UserTexDir + "/walls/Tiles011_2K-PNG/Tiles011_2K-PNG_NormalGL.png", new Vector2(2.5f, 2.5f), 0.65f, 0f);
            CreateURPMaterial("Mat_F2_Floor", lit, UserTexDir + "/floors/Tiles143_2K-PNG/Tiles143_2K-PNG_Color.png", UserTexDir + "/floors/Tiles143_2K-PNG/Tiles143_2K-PNG_NormalGL.png", new Vector2(2f, 2f), 0.45f, 0f);
            CreateURPMaterial("Mat_F2_Ceil", lit, UserTexDir + "/ceilings/OfficeCeiling001_2K-PNG/OfficeCeiling001_2K-PNG_Color.png", UserTexDir + "/ceilings/OfficeCeiling001_2K-PNG/OfficeCeiling001_2K-PNG_NormalGL.png", new Vector2(1.5f, 1.5f), 0.2f, 0f);

            CreateURPMaterial("Mat_F3_Wall", lit, UserTexDir + "/walls/WoodSiding008_2K-PNG/WoodSiding008_2K-PNG_Color.png", UserTexDir + "/walls/WoodSiding008_2K-PNG/WoodSiding008_2K-PNG_NormalGL.png", new Vector2(1.5f, 1.5f), 0.35f, 0f);
            CreateURPMaterial("Mat_F3_Floor", lit, UserTexDir + "/floors/WoodFloor043_2K-PNG/WoodFloor043_2K-PNG_Color.png", UserTexDir + "/floors/WoodFloor043_2K-PNG/WoodFloor043_2K-PNG_NormalGL.png", new Vector2(2f, 2f), 0.55f, 0f);
            CreateURPMaterial("Mat_F3_Ceil", lit, UserTexDir + "/ceilings/OfficeCeiling005_2K-PNG/OfficeCeiling005_2K-PNG_Color.png", UserTexDir + "/ceilings/OfficeCeiling005_2K-PNG/OfficeCeiling005_2K-PNG_NormalGL.png", new Vector2(1.5f, 1.5f), 0.25f, 0f);

            CreateURPMaterial("Mat_F4_Wall", lit, UserTexDir + "/walls/PaintedBricks003_2K-PNG/PaintedBricks003_2K-PNG_Color.png", UserTexDir + "/walls/PaintedBricks003_2K-PNG/PaintedBricks003_2K-PNG_NormalGL.png", new Vector2(2f, 2f), 0.25f, 0f);
            CreateURPMaterial("Mat_F4_Floor", lit, UserTexDir + "/floors/Planks021_2K-PNG/Planks021_2K-PNG_Color.png", UserTexDir + "/floors/Planks021_2K-PNG/Planks021_2K-PNG_NormalGL.png", new Vector2(2f, 2f), 0.4f, 0f);
            CreateURPMaterial("Mat_F4_Ceil", lit, UserTexDir + "/ceilings/OfficeCeiling001_2K-PNG/OfficeCeiling001_2K-PNG_Color.png", UserTexDir + "/ceilings/OfficeCeiling001_2K-PNG/OfficeCeiling001_2K-PNG_NormalGL.png", new Vector2(1.5f, 1.5f), 0.2f, 0f);

            CreateURPMaterial("Mat_Remastered_HazardTrim", lit, TexDir + "/Hazard_Albedo.png", TexDir + "/Hazard_Normal.png", new Vector2(2f, 1f), 0.5f, 0.4f);
            CreateURPMaterial("Mat_Remastered_RustedSteel", lit, TexDir + "/RustedSteel_Albedo.png", TexDir + "/RustedSteel_Normal.png", new Vector2(1f, 1f), 0.35f, 0.6f);

            CreateURPEmissive("Mat_Remastered_LampCool", lit, new Color(0.92f, 0.96f, 1f), new Color(0.9f, 0.95f, 1f) * 2.5f);
            CreateURPEmissive("Mat_Remastered_LampWarm", lit, new Color(1f, 0.9f, 0.75f), new Color(1f, 0.85f, 0.6f) * 2.5f);
            CreateURPEmissive("Mat_Remastered_LampRed", lit, new Color(1f, 0.2f, 0.2f), new Color(1f, 0.1f, 0.05f) * 3f);
            CreateURPMaterial("Mat_Remastered_LampDead", lit, null, null, Vector2.one, 0.2f, 0.05f, new Color(0.22f, 0.22f, 0.25f));
            CreateURPMaterial("Mat_Remastered_Brass", lit, null, null, Vector2.one, 0.72f, 0.85f, new Color(0.78f, 0.65f, 0.35f));
            CreateURPMaterial("Mat_Remastered_MetalHousing", lit, null, null, Vector2.one, 0.55f, 0.75f, new Color(0.32f, 0.34f, 0.38f));

            AssetDatabase.SaveAssets();
        }

        static Material CreateURPMaterial(string name, Shader shader, string albedoPath, string normalPath, Vector2 tiling, float smoothness, float metallic, Color? baseColor = null)
        {
            string path = MatDir + "/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else mat.shader = shader;

            if (baseColor.HasValue) mat.SetColor("_BaseColor", baseColor.Value);
            if (!string.IsNullOrEmpty(albedoPath))
            {
                Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
                if (albedo != null) { mat.SetTexture("_BaseMap", albedo); mat.SetTextureScale("_BaseMap", tiling); }
            }
            if (!string.IsNullOrEmpty(normalPath))
            {
                Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
                if (normal != null) { mat.SetTexture("_BumpMap", normal); mat.SetTextureScale("_BumpMap", tiling); mat.SetFloat("_BumpScale", 1.0f); }
            }
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Material CreateURPEmissive(string name, Shader shader, Color baseColor, Color emissiveColor)
        {
            string path = MatDir + "/" + name + ".mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader) { name = name };
                AssetDatabase.CreateAsset(mat, path);
            }
            else mat.shader = shader;

            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_BaseColor", baseColor);
            mat.SetColor("_EmissionColor", emissiveColor);
            mat.SetFloat("_Smoothness", 0.5f);
            mat.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static void SetupURPAtmosphere(GameObject root)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.015f;
            RenderSettings.fogColor = new Color(0.04f, 0.06f, 0.09f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.06f, 0.08f, 0.12f);
        }

        static void BuildDoorwaySection(Transform parent, Vector3 center, bool horizontal, Material wallMat, Material frameMat, Material trimMat)
        {
            var doorGroup = new GameObject(horizontal ? "Doorway_H" : "Doorway_V");
            doorGroup.transform.SetParent(parent, false);
            doorGroup.isStatic = true;

            float sideW = (Cell - DoorW) * 0.5f;
            Vector3 leftOffset = horizontal ? new Vector3(-Cell * 0.5f + sideW * 0.5f, 0f, 0f) : new Vector3(0f, 0f, -Cell * 0.5f + sideW * 0.5f);
            Vector3 sideScale = horizontal ? new Vector3(sideW, WallH, WallThick) : new Vector3(WallThick, WallH, sideW);
            CreateBox("Wall_Post_A", doorGroup.transform, center + leftOffset, sideScale, wallMat).isStatic = true;
            CreateBox("Wall_Post_B", doorGroup.transform, center - leftOffset, sideScale, wallMat).isStatic = true;

            float lintelH = WallH - DoorH;
            Vector3 lintelPos = new Vector3(center.x, center.y + (WallH * 0.5f) - (lintelH * 0.5f), center.z);
            Vector3 lintelScale = horizontal ? new Vector3(DoorW, lintelH, WallThick) : new Vector3(WallThick, lintelH, DoorW);
            CreateBox("Door_Lintel", doorGroup.transform, lintelPos, lintelScale, wallMat).isStatic = true;

            Vector3 frameTopScale = horizontal ? new Vector3(DoorW + 0.15f, 0.12f, WallThick * 1.35f) : new Vector3(WallThick * 1.35f, 0.12f, DoorW + 0.15f);
            CreateBox("Frame_Header", doorGroup.transform, new Vector3(center.x, center.y - (WallH * 0.5f) + DoorH, center.z), frameTopScale, frameMat).isStatic = true;

            Vector3 jambScale = horizontal ? new Vector3(0.1f, DoorH, WallThick * 1.3f) : new Vector3(WallThick * 1.3f, DoorH, 0.1f);
            Vector3 jambLeftPos = center + (horizontal ? new Vector3(-DoorW * 0.5f + 0.05f, -WallH * 0.5f + DoorH * 0.5f, 0f) : new Vector3(0f, -WallH * 0.5f + DoorH * 0.5f, -DoorW * 0.5f + 0.05f));
            Vector3 jambRightPos = center + (horizontal ? new Vector3(DoorW * 0.5f - 0.05f, -WallH * 0.5f + DoorH * 0.5f, 0f) : new Vector3(0f, -WallH * 0.5f + DoorH * 0.5f, -DoorW * 0.5f - 0.05f));
            CreateBox("Frame_Jamb_L", doorGroup.transform, jambLeftPos, jambScale, frameMat).isStatic = true;
            CreateBox("Frame_Jamb_R", doorGroup.transform, jambRightPos, jambScale, frameMat).isStatic = true;

            // Interactive Door Leaf & Hinge
            float leafW = DoorW - 0.12f;
            float leafH = DoorH - 0.06f;
            float leafT = 0.06f;
            var hingeGo = new GameObject("Door_Hinge");
            hingeGo.transform.SetParent(doorGroup.transform, false);
            hingeGo.transform.position = jambLeftPos;

            Vector3 leafOffset = horizontal ? new Vector3(leafW * 0.5f, 0f, 0f) : new Vector3(0f, 0f, leafW * 0.5f);
            Vector3 leafScale = horizontal ? new Vector3(leafW, leafH, leafT) : new Vector3(leafT, leafH, leafW);
            CreateBox("Door_Leaf", hingeGo.transform, jambLeftPos + leafOffset, leafScale, frameMat);

            Vector3 handleOffset = horizontal ? new Vector3(leafW - 0.12f, 0f, 0.05f) : new Vector3(0.05f, 0f, leafW - 0.12f);
            CreateBox("Door_Handle", hingeGo.transform, jambLeftPos + handleOffset, new Vector3(0.04f, 0.18f, 0.08f), trimMat);

            var doorType = System.Type.GetType("HollowTower.InteractiveDoor, Assembly-CSharp");
            if (doorType != null)
            {
                var doorComp = hingeGo.AddComponent(doorType);
                doorType.GetField("hingeTransform")?.SetValue(doorComp, hingeGo.transform);
                doorType.GetField("openAngle")?.SetValue(doorComp, 90f);
            }
        }

        static void PlaceURPFloorLighting(Transform parent, FloorPlan p, float baseY,
            Material matLampCool, Material matLampWarm, Material matLampRed, Material matLampDead, Material matBrass, Material matMetalHousing)
        {
            float fixtureCeilH = WallH - 0.05f;

            if (p.corridor != null)
            {
                int n = 0;
                foreach (var cell in p.corridor.cells)
                {
                    int c = cell.x, r = cell.y;
                    if (p.pit[r, c]) continue;

                    bool lamp = (r == 6 || r == 17 || (p.floor == 2 && r == 2)) ? c % 3 == 1 : r % 3 == 1;
                    if (lamp)
                    {
                        n++;
                        bool em = n % 9 == 4;
                        bool warm = p.floor == 2;
                        Vector3 floorPos = new Vector3((c + 0.5f) * Cell, baseY, (LevelLayout.Rows - r - 0.5f) * Cell);
                        CreateURPLampFixture(parent, p.floor, floorPos, em ? 2 : (warm ? 1 : 0), 500 + r * LevelLayout.Cols + c, fixtureCeilH,
                            matLampCool, matLampWarm, matLampRed, matLampDead, matBrass, matMetalHousing);
                    }
                }
            }

            foreach (var room in p.rooms)
            {
                if (room == p.corridor) continue;
                int style = (room.floor == 2) ? 1 : 0;
                var R = room.rect;
                int nx = Mathf.Max(1, Mathf.RoundToInt(R.width / 2.6f));
                int nz = Mathf.Max(1, Mathf.RoundToInt(R.height / 2.6f));
                if (room.type == RoomType.Stair) { nx = 1; nz = 1; }

                for (int i = 0; i < nx; i++)
                {
                    for (int j = 0; j < nz; j++)
                    {
                        float cx = (R.x + (i + 0.5f) * ((float)R.width / nx)) * Cell;
                        float cz = (LevelLayout.Rows - R.y - (j + 0.5f) * ((float)R.height / nz)) * Cell;
                        Vector3 floorPos = new Vector3(cx, baseY, cz);
                        CreateURPLampFixture(parent, p.floor, floorPos, style, room.id * 31 + i * 7 + j, fixtureCeilH,
                            matLampCool, matLampWarm, matLampRed, matLampDead, matBrass, matMetalHousing);
                    }
                }
            }
        }

        static void CreateURPLampFixture(Transform parent, int floor, Vector3 floorPos, int style, int seed, float height,
            Material matLampCool, Material matLampWarm, Material matLampRed, Material matLampDead, Material matBrass, Material matMetalHousing)
        {
            var rnd = new System.Random(seed * 13 + floor * 1001);
            bool dead = rnd.NextDouble() < 0.16 && style != 2;

            float ceilY = floorPos.y + height;
            var holder = new GameObject("Lamp_Fixture").transform;
            holder.SetParent(parent, false);
            holder.position = new Vector3(floorPos.x, ceilY, floorPos.z);
            holder.gameObject.isStatic = true;

            float drop = 0f;
            Color col = new Color(0.88f, 0.94f, 1f);

            if (style == 1) // Warm
            {
                drop = 0.55f;
                CreatePrimitive(PrimitiveType.Cylinder, "Brass_Rod", holder, new Vector3(0f, -drop * 0.5f, 0f), new Vector3(0.025f, drop * 0.5f, 0.025f), matBrass).isStatic = true;
                CreatePrimitive(PrimitiveType.Sphere, "Warm_Globe", holder, new Vector3(0f, -drop, 0f), new Vector3(0.42f, 0.38f, 0.42f), dead ? matLampDead : matLampWarm).isStatic = true;
                col = new Color(1f, 0.85f, 0.65f);
            }
            else if (style == 2) // Emergency
            {
                CreateBox("Emergency_Housing", holder, new Vector3(0f, -0.16f, 0f), new Vector3(0.42f, 0.22f, 0.42f), matLampRed).isStatic = true;
                col = new Color(1f, 0.15f, 0.1f);
            }
            else // Fluorescent
            {
                CreatePrimitive(PrimitiveType.Cylinder, "Cable_L", holder, new Vector3(-0.45f, -0.07f, 0f), new Vector3(0.015f, 0.07f, 0.015f), matMetalHousing).isStatic = true;
                CreatePrimitive(PrimitiveType.Cylinder, "Cable_R", holder, new Vector3(0.45f, -0.07f, 0f), new Vector3(0.015f, 0.07f, 0.015f), matMetalHousing).isStatic = true;
                CreateBox("Metal_Housing", holder, new Vector3(0f, -0.15f, 0f), new Vector3(1.4f, 0.10f, 0.42f), matMetalHousing).isStatic = true;
                CreateBox("Fluorescent_Tube", holder, new Vector3(0f, -0.22f, 0f), new Vector3(1.3f, 0.06f, 0.32f), dead ? matLampDead : matLampCool).isStatic = true;
            }

            if (dead) return;

            var lightGo = new GameObject("SpotLight_Downward");
            lightGo.transform.SetParent(holder, false);
            lightGo.transform.localPosition = new Vector3(0f, -0.28f - Mathf.Max(0f, drop - 0.45f), 0f);
            lightGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Spot;
            l.color = col;
            l.intensity = (style == 2) ? 1.8f : 2.8f;
            l.range = 10.5f;
            l.spotAngle = 150f;
            l.innerSpotAngle = 70f;
            l.shadows = LightShadows.None;
        }

        static void BuildStaircase(Transform parent, StairSpec s, Material rampMat, Material steelMat, Material trimMat)
        {
            var stairGo = new GameObject($"Staircase_Floor_{s.lowerFloor + 1}_to_{s.lowerFloor + 2}");
            stairGo.transform.SetParent(parent, false);
            stairGo.isStatic = true;

            float baseY = s.lowerFloor * Story;
            bool climbWest = s.climbWest;

            float xWest = s.col0 * Cell;
            float xEast = (s.col0 + 3) * Cell;
            float lowX = climbWest ? xEast : xWest;
            float highX = climbWest ? xWest : xEast;
            float run = Mathf.Abs(highX - lowX);

            float zNorth = (LevelLayout.Rows - s.row0) * Cell;
            float zSouth = (LevelLayout.Rows - (s.row0 + 2)) * Cell;
            float zCenter = (zNorth + zSouth) * 0.5f;
            float totalZ = 2f * Cell;
            float stairWidth = totalZ - 0.4f;

            float angle = Mathf.Atan2(Story, run) * Mathf.Rad2Deg;
            float length = Mathf.Sqrt(run * run + Story * Story) + 0.2f;
            Vector3 mid = new Vector3((lowX + highX) * 0.5f, baseY + Story * 0.5f, zCenter);
            Quaternion rot = Quaternion.Euler(0f, 0f, highX > lowX ? angle : -angle);
            Vector3 normal = rot * Vector3.up;

            var rampSlab = CreateBox("Ramp_Collision_Slab", stairGo.transform, mid - normal * 0.15f, new Vector3(length, 0.3f, stairWidth), rampMat);
            rampSlab.transform.localRotation = rot;
            rampSlab.isStatic = true;

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
                var col = step.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.DestroyImmediate(col);

                float edgeX = climbWest ? (sx - stepRun * 0.5f + 0.025f) : (sx + stepRun * 0.5f - 0.025f);
                var edge = CreateBox("Nosing_Trim", step.transform, new Vector3(edgeX, sy, zCenter), new Vector3(0.06f, 0.02f, stairWidth), trimMat);
                edge.isStatic = true;
                var edgeCol = edge.GetComponent<Collider>();
                if (edgeCol != null) UnityEngine.Object.DestroyImmediate(edgeCol);
            }

            // Handrails
            var railsGo = new GameObject("Handrails");
            railsGo.transform.SetParent(stairGo.transform, false);
            railsGo.isStatic = true;

            for (int side = -1; side <= 1; side += 2)
            {
                float railZ = zCenter + side * (stairWidth * 0.5f - 0.12f);
                Vector3 aTop = new Vector3(lowX, baseY + 1.0f, railZ);
                Vector3 bTop = new Vector3(highX, baseY + Story + 1.0f, railZ);
                var topRail = CreateBox(side > 0 ? "TopRail_North" : "TopRail_South", railsGo.transform, (aTop + bTop) * 0.5f, new Vector3(length, 0.07f, 0.07f), steelMat);
                topRail.transform.localRotation = rot;
                topRail.isStatic = true;

                Vector3 aMid = new Vector3(lowX, baseY + 0.5f, railZ);
                Vector3 bMid = new Vector3(highX, baseY + Story + 0.5f, railZ);
                var midRail = CreateBox(side > 0 ? "MidRail_North" : "MidRail_South", railsGo.transform, (aMid + bMid) * 0.5f, new Vector3(length, 0.04f, 0.04f), steelMat);
                midRail.transform.localRotation = rot;
                midRail.isStatic = true;

                int numPosts = 6;
                for (int p = 0; p < numPosts; p++)
                {
                    float pt = (float)p / (numPosts - 1);
                    float px = Mathf.Lerp(lowX, highX, pt);
                    float py = baseY + pt * Story + 0.5f;
                    CreateBox($"Post_{p}", railsGo.transform, new Vector3(px, py, railZ), new Vector3(0.07f, 1.0f, 0.07f), steelMat).isStatic = true;
                }
            }
        }

        static void BuildTowerElevator(Transform parent, Material steelMat, Material trimMat, Material brassMat, Material lampMat)
        {
            var elevRoot = new GameObject("Tower_Grand_Elevator");
            elevRoot.transform.SetParent(parent, false);

            float elevX = (17 + 0.5f) * Cell;
            float elevZ = (LevelLayout.Rows - 6 - 0.5f) * Cell;
            Vector3 baseElevPos = new Vector3(elevX, 0f, elevZ);

            float totalShaftH = 4 * Story;
            float shaftSize = 3.6f;

            Vector3[] corners = new Vector3[]
            {
                new Vector3(-shaftSize * 0.5f, 0f, -shaftSize * 0.5f),
                new Vector3(shaftSize * 0.5f, 0f, -shaftSize * 0.5f),
                new Vector3(-shaftSize * 0.5f, 0f, shaftSize * 0.5f),
                new Vector3(shaftSize * 0.5f, 0f, shaftSize * 0.5f),
            };

            foreach (var c in corners)
            {
                CreateBox("Shaft_Steel_Column", elevRoot.transform, baseElevPos + c + Vector3.up * (totalShaftH * 0.5f), new Vector3(0.2f, totalShaftH, 0.2f), steelMat).isStatic = true;
            }

            for (int f = 0; f < 4; f++)
            {
                float floorY = f * Story;
                Vector3 landingPos = new Vector3(elevX, floorY, elevZ - shaftSize * 0.5f);
                CreateBox($"Elevator_Call_Button_F{f + 1}", elevRoot.transform, landingPos + new Vector3(1.3f, 1.4f, -0.05f), new Vector3(0.2f, 0.32f, 0.08f), brassMat).isStatic = true;
                CreateBox($"Elevator_Floor_Indicator_F{f + 1}", elevRoot.transform, landingPos + new Vector3(0f, 2.7f, -0.05f), new Vector3(0.8f, 0.25f, 0.06f), lampMat).isStatic = true;
            }

            var cabinGo = new GameObject("Elevator_Cabin");
            cabinGo.transform.SetParent(elevRoot.transform, false);
            cabinGo.transform.position = new Vector3(elevX, 0.05f, elevZ);

            float cabW = 3.0f, cabH = 2.8f, cabD = 3.0f;
            float wallT = 0.08f;

            CreateBox("Cabin_Floor", cabinGo.transform, cabinGo.transform.position + Vector3.up * 0.04f, new Vector3(cabW, 0.08f, cabD), steelMat);
            CreateBox("Cabin_Ceiling", cabinGo.transform, cabinGo.transform.position + Vector3.up * (cabH - 0.04f), new Vector3(cabW, 0.08f, cabD), trimMat);
            CreateBox("Cabin_Wall_Back", cabinGo.transform, cabinGo.transform.position + new Vector3(0f, cabH * 0.5f, cabD * 0.5f - wallT * 0.5f), new Vector3(cabW, cabH, wallT), trimMat);
            CreateBox("Cabin_Wall_Left", cabinGo.transform, cabinGo.transform.position + new Vector3(-cabW * 0.5f + wallT * 0.5f, cabH * 0.5f, 0f), new Vector3(wallT, cabH, cabD), trimMat);
            CreateBox("Cabin_Wall_Right", cabinGo.transform, cabinGo.transform.position + new Vector3(cabW * 0.5f - wallT * 0.5f, cabH * 0.5f, 0f), new Vector3(wallT, cabH, cabD), trimMat);

            float doorW = 1.0f;
            var doorL = CreateBox("Cabin_Door_L", cabinGo.transform, cabinGo.transform.position + new Vector3(-doorW * 0.5f, cabH * 0.5f, -cabD * 0.5f + 0.04f), new Vector3(doorW, cabH - 0.1f, 0.06f), steelMat);
            var doorR = CreateBox("Cabin_Door_R", cabinGo.transform, cabinGo.transform.position + new Vector3(doorW * 0.5f, cabH * 0.5f, -cabD * 0.5f + 0.04f), new Vector3(doorW, cabH - 0.1f, 0.06f), steelMat);

            var lightGo = new GameObject("Cabin_Interior_Light");
            lightGo.transform.SetParent(cabinGo.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, cabH - 0.25f, 0f);
            var cabinLight = lightGo.AddComponent<Light>();
            cabinLight.type = LightType.Spot;
            cabinLight.spotAngle = 140f;
            cabinLight.color = new Color(1.0f, 0.92f, 0.8f);
            cabinLight.intensity = 2.4f;
            cabinLight.range = 5.0f;

            var elevType = System.Type.GetType("HollowTower.TowerElevator, Assembly-CSharp");
            if (elevType != null)
            {
                var elevComp = elevRoot.AddComponent(elevType);
                elevType.GetField("cabinTransform")?.SetValue(elevComp, cabinGo.transform);
                elevType.GetField("cabinDoorL")?.SetValue(elevComp, doorL.transform);
                elevType.GetField("cabinDoorR")?.SetValue(elevComp, doorR.transform);
                elevType.GetField("cabinInteriorLight")?.SetValue(elevComp, cabinLight);
                elevType.GetField("floorHeights")?.SetValue(elevComp, new float[] { 0.05f, 4.25f, 8.45f, 12.65f });
            }
        }

        static GameObject SpawnControlledPlayer()
        {
            var oldCam = GameObject.Find("Player_Camera");
            if (oldCam != null) Object.DestroyImmediate(oldCam);

            var oldPlayer = GameObject.Find("Player_Character_Controlled");
            if (oldPlayer != null) Object.DestroyImmediate(oldPlayer);

            var playerGo = new GameObject("Player_Character_Controlled");
            playerGo.tag = "Player";
            playerGo.transform.position = new Vector3(10f * Cell, 0.05f, (LevelLayout.Rows - 3f) * Cell);
            playerGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            var cc = playerGo.AddComponent<CharacterController>();
            cc.height = 2.0f;
            cc.radius = 0.38f;
            cc.center = new Vector3(0f, 1.0f, 0f);

            var camHolder = new GameObject("Camera_Holder");
            camHolder.transform.SetParent(playerGo.transform, false);
            camHolder.transform.localPosition = new Vector3(0f, 1.75f, 0f);

            var camGo = new GameObject("Player_Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(camHolder.transform, false);
            camGo.transform.localPosition = Vector3.zero;

            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 250f;
            cam.fieldOfView = 75f;
            camGo.AddComponent<AudioListener>();

            var rpcType = System.Type.GetType("HollowTower.RemasteredPlayerController, Assembly-CSharp");
            if (rpcType != null)
            {
                var rpc = playerGo.AddComponent(rpcType);
                rpcType.GetField("cameraHolder")?.SetValue(rpc, camHolder.transform);
                rpcType.GetField("playerCamera")?.SetValue(rpc, cam);
            }

            var hudType = System.Type.GetType("HollowTower.RemasteredMapHUD, Assembly-CSharp");
            if (hudType != null) playerGo.AddComponent(hudType);

            var optType = System.Type.GetType("HollowTower.RemasteredLightOptimizer, Assembly-CSharp");
            if (optType != null)
            {
                var opt = playerGo.AddComponent(optType);
                optType.GetField("storyHeight")?.SetValue(opt, Story);
            }

            return playerGo;
        }

        static GameObject CreateBox(string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            var r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
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
            if (c != null) Object.DestroyImmediate(c);
            return go;
        }
    }
}
