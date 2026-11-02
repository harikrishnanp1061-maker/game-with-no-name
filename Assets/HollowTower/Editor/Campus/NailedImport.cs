using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// Brings in the user's Monster_4 as the Nailed (level 3's smaller monster). The FBX comes from
    /// character_source/build_monster4.py (Blender: Generic rig, in-place clips NL_Idle/Walk/Run/Attack/Scream, fingers
    /// curled into claws). Here: import settings, URP materials from the user's texture set (the red pustules and eyes
    /// glow through the emission map), an animator with one state per clip, and a prefab the campus builder places.
    /// </summary>
    public static class NailedImport
    {
        public const string Dir = "Assets/Characters/Nailed";
        public const string Fbx = Dir + "/Nailed.fbx";
        public const string ControllerPath = Dir + "/Nailed.controller";
        public const string PrefabPath = Dir + "/Nailed.prefab";
        /// <summary>Ground speed of the walk / run clips at speed 1 (Nailed_ClipSpeeds.json from the Blender build).</summary>
        public const float WalkClipSpeed = 1.048f, RunClipSpeed = 3.952f;

        [MenuItem("Hollow Tower/Campus/Import Nailed Monster")]
        public static void Menu() { Debug.Log("[Nailed] " + Import()); }

        public static string Import()
        {
            var mats = Materials();
            var imp = (ModelImporter)AssetImporter.GetAtPath(Fbx);
            if (imp == null) return "no FBX at " + Fbx + " (run build_monster4.py)";
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (var kv in mats) imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.motionNodeName = "";
            imp.optimizeGameObjects = false;      // the brain turns the head bone
            imp.importBlendShapes = false;
            imp.resampleCurves = false;
            imp.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
            imp.SaveAndReimport();
            var clips = new List<ModelImporterClipAnimation>();
            foreach (var take in imp.importedTakeInfos)
            {
                string n = take.name.Substring(take.name.LastIndexOf('|') + 1);
                clips.Add(new ModelImporterClipAnimation
                {
                    name = n, takeName = take.name,
                    firstFrame = take.startTime * take.sampleRate, lastFrame = take.stopTime * take.sampleRate,
                    loopTime = n == "NL_Idle" || n == "NL_Walk" || n == "NL_Run", loopPose = false,
                });
            }
            imp.clipAnimations = clips.ToArray();
            imp.SaveAndReimport();
            var ctrl = Controller();
            var prefab = Prefab(ctrl, mats);
            return "clips " + clips.Count + ", controller " + (ctrl != null) + ", prefab " + (prefab != null);
        }

        static Texture2D Tex(string file, bool normal, bool linear = false)
        {
            string p = Dir + "/" + file + ".png";
            var ti = AssetImporter.GetAtPath(p) as TextureImporter;
            if (ti == null) return null;
            bool dirty = false;
            if (ti.maxTextureSize != 2048) { ti.maxTextureSize = 2048; dirty = true; }   // 4K source; it is never that close
            if (normal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; dirty = true; }
            if (linear && ti.sRGBTexture) { ti.sRGBTexture = false; dirty = true; }
            if (dirty) ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }

        static Dictionary<string, Material> Materials()
        {
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            var res = new Dictionary<string, Material>();
            foreach (var set in new[] { new[] { "NL_Monster_4", "t_body" }, new[] { "NL_Planks", "t_plank" } })
            {
                string path = Dir + "/" + set[0] + ".mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) { m = new Material(lit); AssetDatabase.CreateAsset(m, path); }
                m.shader = lit;
                m.SetTexture("_BaseMap", Tex(set[1] + "_Base_color", false)); m.SetColor("_BaseColor", Color.white);
                m.SetTexture("_BumpMap", Tex(set[1] + "_Normal_OpenGL", true)); m.EnableKeyword("_NORMALMAP");
                m.SetTexture("_MetallicGlossMap", Tex(set[1] + "_MetallicSmootness", false, true)); m.EnableKeyword("_METALLICSPECGLOSSMAP");
                m.SetFloat("_Smoothness", 0.85f);
                var em = set[1] == "t_body" ? Tex("t_body_Emissive", false) : null;
                if (em != null)
                {
                    m.SetTexture("_EmissionMap", em); m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", new Color(1f, 0.12f, 0.06f) * 0.7f);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
                }
                m.enableInstancing = true;
                EditorUtility.SetDirty(m);
                res[set[0]] = m;
            }
            AssetDatabase.SaveAssets();
            return res;
        }

        static AnimatorController Controller()
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var sm = ctrl.layers[0].stateMachine;
            var clips = new Dictionary<string, AnimationClip>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(Fbx))
            {
                var c = o as AnimationClip;
                if (c != null && !c.name.StartsWith("__preview")) clips[c.name] = c;
            }
            foreach (var st in new[] { "Idle", "Walk", "Run", "Attack", "Scream" })
            {
                AnimationClip c;
                clips.TryGetValue("NL_" + st, out c);
                var s = sm.AddState(st);
                s.motion = c;
                if (st == "Idle") sm.defaultState = s;
            }
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            return ctrl;
        }

        static GameObject Prefab(AnimatorController ctrl, Dictionary<string, Material> mats)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            var root = new GameObject("Nailed");
            var model = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
            model.name = "Model";
            PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var anim = model.GetComponent<Animator>() ?? model.AddComponent<Animator>();
            anim.runtimeAnimatorController = ctrl; anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                r.updateWhenOffscreen = false;
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++)
                    if (ms[i] != null && mats.ContainsKey(ms[i].name)) ms[i] = mats[ms[i].name];
                r.sharedMaterials = ms;
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }
    }
}
