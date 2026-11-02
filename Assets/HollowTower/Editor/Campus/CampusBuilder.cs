using System.Collections.Generic;
using System.Linq;
using HollowTower.Campus;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// Builds the final level, Campus_Final: a copy of the tutorial scene keeps the party, its four players and the
    /// network objects (so it works in a LAN session like the other levels); the tutorial's rooms are replaced by the
    /// administration building and the campus around it, lit for night - a low moon, deep blue ambient, thin fog, a star
    /// sky - with its own post-processing. Floor 2's exit elevator loads it (NetLobby.CampusLevel).
    /// </summary>
    public static class CampusBuilder
    {
        public const string ScenePath = CampusKit.Dir + "/Scenes/Campus_Final.unity";
        const string Template = "Assets/Tutorial/Scenes/Tutorial.unity";
        const string TowerScene = "Assets/HollowTowerRemastered/Scenes/HollowTower_Remastered.unity";
        static readonly string[] Keep = { "Player_1", "Player_2", "Player_3", "Player_4", "Player_Party", "NetGame" };

        static readonly Vector3 MoonDir = new Vector3(0.42f, 0.53f, 0.73f);   // towards the moon: north-east, low

        [MenuItem("Hollow Tower/Campus/Build Final Level", priority = 90)]
        public static void BuildMenu()
        {
            if (EditorApplication.isPlaying) { Debug.LogWarning("[Campus] stop play mode first"); return; }
            Debug.Log(Build());
        }

        public static string Build()
        {
            var t0 = System.DateTime.Now;
            CampusKit.Folder(CampusKit.Dir, "Scenes");
            CampusKit.Begin();
            CampusKit.FixModelMaterials();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null) AssetDatabase.DeleteAsset(ScenePath);
            AssetDatabase.CopyAsset(Template, ScenePath);
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var go in scene.GetRootGameObjects())
                if (!Keep.Contains(go.name)) Object.DestroyImmediate(go);

            // the spawn elevator the party rides in the other levels, copied out of the tower scene
            var tower = EditorSceneManager.OpenScene(TowerScene, OpenSceneMode.Additive);
            CampusAdmin.ElevatorUnit = null;
            foreach (var go in tower.GetRootGameObjects())
            {
                var src = go.transform.Find("Floor_1_Ground/Floor_1_SpawnElevator");
                if (src == null) continue;
                var copy = Object.Instantiate(src.gameObject);
                copy.name = "Spawn_Elevator";
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(copy, scene);
                CampusAdmin.ElevatorUnit = copy;
            }
            EditorSceneManager.CloseScene(tower, true);

            CampusMats.Load();
            var env = new GameObject("Campus_Final_Environment").transform;
            CampusAdmin.Build(env);
            CampusGrounds.Build(env);
            CampusSecurity.Build(env);
            CampusGiantBuilder.Build();   // the giant outside (scene root: it moves)
            Lighting(env);

            // the party starts in the elevator cab, facing its doors
            var party = Object.FindAnyObjectByType<PlayerParty>();
            for (int i = 0; i < 4; i++)
            {
                var p = GameObject.Find("Player_" + (i + 1));
                if (p == null) continue;
                p.transform.position = CampusAdmin.Spawns[i];
                p.transform.rotation = Quaternion.identity;
                foreach (var cam in p.GetComponentsInChildren<Camera>(true))
                {
                    cam.farClipPlane = 320f; cam.nearClipPlane = 0.05f;
                    var data = cam.GetComponent<UniversalAdditionalCameraData>() ?? cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                    data.renderPostProcessing = true;
                    cam.clearFlags = CameraClearFlags.Skybox;
                }
            }
            if (party != null && party.GetComponent<CampusLightCuller>() == null) party.gameObject.AddComponent<CampusLightCuller>();
            if (party != null && party.GetComponent<CampusMapHUD>() == null) party.gameObject.AddComponent<CampusMapHUD>();   // dev map

            var obj = new GameObject("Campus_Objective").AddComponent<CampusObjective>();
            obj.key = CampusAdmin.Key; obj.mainDoor = CampusAdmin.MainDoor;
            obj.generator = CampusGrounds.Generator; obj.gateControl = CampusGrounds.GateControl; obj.gate = CampusGrounds.Gate;
            obj.serviceGate = CampusGrounds.Service;
            if (CampusSecurity.Console != null) { obj.controlRoom = CampusSecurity.Console.transform; CampusSecurity.Console.serviceGate = CampusGrounds.Service; }
            CampusSecurity.RadioAndNarration();
            if (CampusGrounds.Clock != null) CampusGrounds.Clock.shiftBoard = CampusSecurity.ShiftBoard;
            obj.clock = CampusGrounds.Clock; obj.statues = CampusGrounds.Statues;

            CampusKit.MarkStatic(env.gameObject);
            string nav = CampusSecurity.BakeNavMesh(env);
            int nailed = CampusSecurity.SpawnNailed();
            Debug.Log("[Campus] " + nav + ", Nailed " + nailed);
            CampusKit.End();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AddToBuild();

            int lamps = env.GetComponentsInChildren<CampusLamp>(true).Length, rs = env.GetComponentsInChildren<Renderer>(true).Length;
            return "[Campus] built " + ScenePath + " in " + (System.DateTime.Now - t0).TotalSeconds.ToString("F1") + " s: " + CampusKit.BoxCount + " boxes, "
                   + rs + " renderers, " + lamps + " lamps" + (CampusKit.Missing.Count > 0 ? "; MISSING: " + string.Join(", ", CampusKit.Missing.Distinct()) : "");
        }

        static void AddToBuild()
        {
            var list = EditorBuildSettings.scenes.ToList();
            if (list.Any(s => s.path == ScenePath)) return;
            list.Add(new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = list.ToArray();
        }

        // ------------------------------------------------------------------ night
        static void Lighting(Transform env)
        {
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) Object.DestroyImmediate(l.gameObject);
            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.transform.SetParent(env, false);
            moon.type = LightType.Directional;
            moon.transform.rotation = Quaternion.LookRotation(-MoonDir.normalized);
            moon.color = new Color(0.64f, 0.74f, 1f);
            moon.intensity = 1.05f;
            moon.shadows = LightShadows.Soft; moon.shadowStrength = 0.7f;
            moon.gameObject.AddComponent<UniversalAdditionalLightData>();

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.2f, 0.235f, 0.36f);
            RenderSettings.ambientEquatorColor = new Color(0.13f, 0.145f, 0.2f);
            RenderSettings.ambientGroundColor = new Color(0.06f, 0.062f, 0.075f);
            RenderSettings.reflectionIntensity = 0.45f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.068f, 0.084f, 0.124f);
            RenderSettings.fogDensity = 0.0072f;
            RenderSettings.skybox = CampusSky.Build(MoonDir, RenderSettings.fogColor);
            RenderSettings.sun = moon;
            Lightmapping.lightingSettings = LightingAsset();

            var vgo = new GameObject("Campus_Night_Volume"); vgo.transform.SetParent(env, false);
            var vol = vgo.AddComponent<Volume>();
            vol.isGlobal = true; vol.priority = 10f;
            vol.sharedProfile = Profile();
        }

        static LightingSettings LightingAsset()
        {
            string path = CampusKit.GenDir + "/CampusLighting.lighting";
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(path);
            if (ls == null) { ls = new LightingSettings(); AssetDatabase.CreateAsset(ls, path); }
            ls.bakedGI = false; ls.realtimeGI = false;   // all lights are realtime and culled; nothing to bake
            ls.autoGenerate = false;
            EditorUtility.SetDirty(ls);
            return ls;
        }

        static VolumeProfile Profile()
        {
            string path = CampusKit.GenDir + "/CampusNight_Profile.asset";
            var old = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (old != null) AssetDatabase.DeleteAsset(path);
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, path);
            var bloom = p.Add<Bloom>(true); bloom.threshold.Override(0.95f); bloom.intensity.Override(0.7f); bloom.scatter.Override(0.68f);
            var tone = p.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
            var vig = p.Add<Vignette>(true); vig.intensity.Override(0.3f); vig.smoothness.Override(0.45f);
            var grade = p.Add<ColorAdjustments>(true); grade.postExposure.Override(1.0f); grade.contrast.Override(14f); grade.saturation.Override(-10f);
            grade.colorFilter.Override(new Color(0.93f, 0.96f, 1.04f));
            var wb = p.Add<WhiteBalance>(true); wb.temperature.Override(-12f);
            var grain = p.Add<FilmGrain>(true); grain.type.Override(FilmGrainLookup.Medium1); grain.intensity.Override(0.1f);
            foreach (var c in p.components) { c.name = c.GetType().Name; AssetDatabase.AddObjectToAsset(c, p); }
            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();
            return p;
        }

        /// <summary>A night sky panorama: deep blue fading into the fog's colour at the horizon, thin moonlit cloud,
        /// stars thickening towards the zenith, and the moon where the moonlight comes from.</summary>
        static Material Sky()
        {
            const int W = 2048, H = 1024;
            string texPath = CampusKit.GenDir + "/NightSky.png";
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            var px = new Color[W * H];
            var rnd = new System.Random(77);
            Color horizon = new Color(0.058f, 0.072f, 0.105f), zenith = new Color(0.012f, 0.017f, 0.04f);
            Vector3 md = MoonDir.normalized;
            for (int y = 0; y < H; y++)
            {
                float v = (y + 0.5f) / H;
                float elev = (v - 0.5f) * Mathf.PI;   // -90..90 degrees
                float k = Mathf.Clamp01(Mathf.Sin(Mathf.Max(0f, elev)));
                Color baseC = elev < 0 ? horizon : Color.Lerp(horizon, zenith, Mathf.Pow(k, 0.6f));
                for (int x = 0; x < W; x++)
                {
                    float u = (x + 0.5f) / W;
                    float lon = (0.5f - u) * 2f * Mathf.PI, lat = (1f - v) * Mathf.PI;
                    var d = new Vector3(Mathf.Cos(lon) * Mathf.Sin(lat), Mathf.Cos(lat), Mathf.Sin(lon) * Mathf.Sin(lat));
                    Color c = baseC;
                    if (elev > 0.02f)
                    {
                        float cloud = Mathf.PerlinNoise(u * 18f, v * 9f) * 0.6f + Mathf.PerlinNoise(u * 47f, v * 23f) * 0.4f;
                        cloud = Mathf.Clamp01((cloud - 0.5f) * 2.2f) * (1f - k * 0.5f);
                        float glow = Mathf.Pow(Mathf.Max(0f, Vector3.Dot(d, md)), 24f);
                        c += new Color(0.05f, 0.06f, 0.085f) * cloud * (0.35f + glow * 2f);
                        c += new Color(0.06f, 0.07f, 0.1f) * glow;
                    }
                    px[y * W + x] = c;
                }
            }
            // stars
            for (int i = 0; i < 9000; i++)
            {
                int x = rnd.Next(W), y = H / 2 + 6 + (int)(Mathf.Sqrt((float)rnd.NextDouble()) * (H / 2 - 7));
                float b = Mathf.Pow((float)rnd.NextDouble(), 3f) * 0.9f + 0.08f;
                float elev = ((y + 0.5f) / H - 0.5f) * 2f;
                b *= Mathf.Clamp01(elev * 4f);
                var tint = rnd.NextDouble() < 0.2 ? new Color(1f, 0.85f, 0.7f) : rnd.NextDouble() < 0.3 ? new Color(0.75f, 0.85f, 1f) : Color.white;
                px[y * W + x] += tint * b;
                if (b > 0.6f) { px[y * W + (x + 1) % W] += tint * b * 0.35f; if (y + 1 < H) px[(y + 1) * W + x] += tint * b * 0.35f; }
            }
            // the moon
            float mu = 0.5f - Mathf.Atan2(md.z, md.x) / (2f * Mathf.PI), mv = 1f - Mathf.Acos(md.y) / Mathf.PI;
            int cx = Mathf.RoundToInt(mu * W), cy = Mathf.RoundToInt(mv * H);
            const int rad = 13;
            for (int dy = -rad * 4; dy <= rad * 4; dy++)
                for (int dx = -rad * 6; dx <= rad * 6; dx++)
                {
                    int x = (cx + dx + W) % W, y = cy + dy;
                    if (y < 0 || y >= H) continue;
                    float sx = dx * Mathf.Cos((mv - 0.5f) * Mathf.PI);   // longitude pixels shrink with elevation: keep it round
                    float r = Mathf.Sqrt(sx * sx + dy * dy) / rad;
                    if (r <= 1f)
                    {
                        float crater = Mathf.PerlinNoise(dx * 0.25f + 10f, dy * 0.25f) * 0.25f;
                        px[y * W + x] = new Color(0.86f, 0.88f, 0.92f) * (0.85f - crater);
                    }
                    else px[y * W + x] += new Color(0.12f, 0.14f, 0.2f) * Mathf.Exp(-(r - 1f) * 2.2f);
                }
            tex.SetPixels(px); tex.Apply();
            System.IO.File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath);
            var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
            ti.mipmapEnabled = false; ti.wrapModeU = TextureWrapMode.Repeat; ti.wrapModeV = TextureWrapMode.Clamp;
            ti.maxTextureSize = 2048; ti.textureCompression = TextureImporterCompression.CompressedHQ; ti.sRGBTexture = true;
            ti.SaveAndReimport();
            string matPath = CampusKit.MatDir + "/CM_NightSky.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (m == null) { m = new Material(Shader.Find("Skybox/Panoramic")); AssetDatabase.CreateAsset(m, matPath); }
            m.shader = Shader.Find("Skybox/Panoramic");
            m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            m.SetFloat("_Mapping", 1f); m.SetFloat("_ImageType", 0f); m.SetFloat("_Exposure", 1f);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
