using HollowTower.Campus;
using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEditor;
using UnityEngine;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// Puts the giant into the campus (CampusBuilder): the tutorial's pit monster model at 13x - about 48 m tall - with
    /// the pit monster's own materials, two glowing red eyes on its face, the red gaze light that follows where its head
    /// looks, the dust its footfalls kick up, its route round the open grounds, and the network parts (host-driven
    /// transform + GiantNet) so it walks the same on every machine. It sits at the scene root, not in the static
    /// environment.
    /// </summary>
    public static class CampusGiantBuilder
    {
        // the pit monster's mesh subdivided once in Blender (character_source/subdivide_giant.py): smooth at 48 m tall
        const string ModelPath = "Assets/Campus/Models/giant/Monster8_HD.fbx";
        const string Dir = "Assets/Campus/Models/giant/";
        const string PitPrefab = "Assets/Tutorial/Characters/Monster8/Monster8_PitStalker.prefab";
        public const float Scale = 13f;

        // its walk round the open grounds (body centre): the sports field, the car park, the east lawns, past the
        // cafeteria and the quad, along the library to the gardens and back
        static readonly Vector3[] Route =
        {
            new Vector3(-90f, 0f, 195f), new Vector3(-30f, 0f, 185f), new Vector3(50f, 0f, 195f), new Vector3(115f, 0f, 175f),
            new Vector3(135f, 0f, 95f), new Vector3(140f, 0f, 52f), new Vector3(85f, 0f, 22f), new Vector3(30f, 0f, 75f),
            new Vector3(-30f, 0f, 95f), new Vector3(-80f, 0f, 32f), new Vector3(-135f, 0f, 60f), new Vector3(-130f, 0f, 140f),
        };

        public static CampusGiant Build()
        {
            var mi = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            if (mi != null && (Mathf.Abs(mi.globalScale - 0.025f) > 1e-4f || mi.animationType != ModelImporterAnimationType.Generic || mi.importAnimation))
            {
                mi.globalScale = 0.025f;   // Blender wrote it 40x the original size
                mi.animationType = ModelImporterAnimationType.Generic; mi.importAnimation = false;
                mi.SaveAndReimport();
            }
            var root = new GameObject("Campus_Giant");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath), root.transform);
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
            var anim = model.GetComponent<Animator>(); if (anim != null) anim.enabled = false;
            var smr = model.GetComponentInChildren<SkinnedMeshRenderer>();
            smr.sharedMaterial = SkinMaterial();
            smr.updateWhenOffscreen = true;   // the IK moves it far from its bind-pose bounds

            // the eyes, on the face (found on the mesh: the two bulges at the front of the head), parented to the head
            Transform head = null;
            foreach (var t in model.GetComponentsInChildren<Transform>()) if (t.name == "Bone_006") head = t;
            var eyeMat = CampusKit.Glow("CM_GiantEye", new Color(1f, 0.08f, 0.04f), 3f);
            var eyes = new Renderer[2];
            for (int i = 0; i < 2; i++)
            {
                var e = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                e.name = i == 0 ? "Eye_L" : "Eye_R";
                Object.DestroyImmediate(e.GetComponent<Collider>());
                e.transform.position = new Vector3(0.011f + (i == 0 ? -0.115f : 0.115f), 1.30f, 2.50f);
                e.transform.localScale = Vector3.one * 0.16f;
                e.transform.SetParent(head, true);
                var r = e.GetComponent<Renderer>(); r.sharedMaterial = eyeMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                eyes[i] = r;
            }

            // a red glow on its face from the eyes
            var el = new GameObject("Eye_Light"); el.transform.SetParent(head, false);
            el.transform.position = new Vector3(0.011f, 1.32f, 2.75f);
            var eyeLight = el.AddComponent<Light>(); eyeLight.type = LightType.Point; eyeLight.range = 30f; eyeLight.intensity = 1400f;
            eyeLight.color = new Color(1f, 0.12f, 0.06f); eyeLight.shadows = LightShadows.None;
            el.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();

            // a pale fill in front of the face (cool, like moonlight caught on it), so its features read from afar
            var fl = new GameObject("Face_Light"); fl.transform.SetParent(head, false);
            fl.transform.position = new Vector3(0.011f, 1.55f, 3.4f);
            var faceLight = fl.AddComponent<Light>(); faceLight.type = LightType.Point; faceLight.range = 32f; faceLight.intensity = 1800f;
            faceLight.color = new Color(0.72f, 0.78f, 1f); faceLight.shadows = LightShadows.None;
            fl.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();

            // the gaze: a dim red spot from its head along where it looks
            var gazeGo = new GameObject("Giant_Gaze"); gazeGo.transform.SetParent(root.transform, false);
            var gaze = gazeGo.AddComponent<Light>();
            gaze.type = LightType.Spot; gaze.range = 120f; gaze.spotAngle = 30f; gaze.innerSpotAngle = 12f;
            gaze.intensity = 26f; gaze.color = new Color(1f, 0.42f, 0.36f); gaze.shadows = LightShadows.None;
            gazeGo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalLightData>();

            root.transform.position = Route[0];
            root.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            root.transform.localScale = Vector3.one * Scale;

            var giant = root.AddComponent<CampusGiant>();
            giant.waypoints = Route;
            giant.gaze = gaze;
            giant.eyes = eyes;
            giant.eyeLight = eyeLight;
            giant.faceLight = faceLight;
            giant.dustMaterial = DustMaterial();

            root.AddComponent<NetworkObject>();
            var nt = root.AddComponent<NetworkTransform>();
            nt.SyncRotAngleX = false; nt.SyncRotAngleZ = false;
            nt.SyncScaleX = nt.SyncScaleY = nt.SyncScaleZ = false;
            nt.Interpolate = true;
            root.AddComponent<GiantNet>();
            return giant;
        }

        static Texture2D Tex(string path, bool normal, int max)
        {
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            bool dirty = false;
            if (normal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; dirty = true; }
            if (ti.maxTextureSize != max) { ti.maxTextureSize = max; dirty = true; }
            if (ti.anisoLevel != 8) { ti.anisoLevel = 8; dirty = true; }
            if (ti.textureCompression != TextureImporterCompression.CompressedHQ) { ti.textureCompression = TextureImporterCompression.CompressedHQ; dirty = true; }
            if (dirty) ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>The giant's pale, corpse-white skin: the body texture remapped pale at full 4K (giant_skin.py), its
        /// own normal map, a fine wrinkle-and-pore detail normal tiled over it so it stays sharp up close, and a soft damp
        /// sheen instead of the pit model's plastic gloss.</summary>
        static Material SkinMaterial()
        {
            string path = CampusKit.MatDir + "/CM_GiantSkin.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
            m.shader = lit;
            m.SetTexture("_BaseMap", Tex(Dir + "giant_skin.png", false, 4096));
            m.SetColor("_BaseColor", new Color(0.95f, 0.94f, 0.93f));
            m.SetTexture("_BumpMap", Tex(Dir + "giant_skin_n.png", true, 4096)); m.SetFloat("_BumpScale", 1.2f); m.EnableKeyword("_NORMALMAP");
            m.SetTexture("_DetailNormalMap", Tex(Dir + "giant_detail_n.png", true, 1024)); m.SetFloat("_DetailNormalMapScale", 0.9f);
            m.SetTextureScale("_DetailAlbedoMap", new Vector2(14f, 14f)); m.EnableKeyword("_DETAIL_MULX2");
            m.SetFloat("_Smoothness", 0.38f); m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>A soft grey puff for the dust clouds (URP particles, alpha-blended).</summary>
        static Material DustMaterial()
        {
            string texPath = CampusKit.GenDir + "/GiantDust.png";
            const int n = 128;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - r); a = a * a * (0.75f + 0.25f * Mathf.PerlinNoise(x * 0.09f, y * 0.09f));
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            tex.SetPixels(px); tex.Apply();
            System.IO.File.WriteAllBytes(texPath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath);
            var ti = (TextureImporter)AssetImporter.GetAtPath(texPath);
            ti.alphaIsTransparency = true; ti.wrapMode = TextureWrapMode.Clamp; ti.SaveAndReimport();

            string matPath = CampusKit.MatDir + "/CM_GiantDust.mat";
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, matPath); }
            m.shader = shader;
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
