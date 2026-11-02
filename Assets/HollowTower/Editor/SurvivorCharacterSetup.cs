using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// The user's Survival_Character as all four players. Survivor.fbx is built in Blender by
    /// character_source/build_survivor.py: real mocap (Bandai Namco Research Motion Dataset, CC BY-NC 4.0) retargeted
    /// onto the character's own five-bone spine, plus hand-authored idle / crouch idle / fall / death / held-item clips.
    /// It imports as a Generic rig so every bone plays exactly as authored (a Humanoid avatar drives only three of the
    /// five spine bones, which kinked the torso). The controller blends with 2D directional trees fed by
    /// CharacterAnimDriver, so changing speed, direction or stance crossfades instead of popping. "Replace Players In
    /// Open Scene" swaps the model under every RemasteredPlayerController and moves each spray can / compass into the
    /// new hands. Safe to run again.
    /// </summary>
    public static class SurvivorCharacterSetup
    {
        const string Dir = "Assets/Characters/Survivor";
        const string Fbx = Dir + "/Survivor.fbx";
        const string SpeedsJson = Dir + "/Survivor_ClipSpeeds.json";
        const string TexDir = Dir + "/Textures/";
        const string MatDir = Dir + "/Materials/";
        const string ControllerPath = Dir + "/Survivor_Locomotion.controller";
        const string MaskPath = Dir + "/Survivor_LeftArm.mask";
        const float EyeHeight = 1.80f;            // matches RemasteredPlayerController.standingCamY (1.82)

        [MenuItem("Hollow Tower/Players/Survivor/1. Import Character + Animations")]
        public static void ImportMenu() { Debug.Log("[Survivor] " + Import()); }

        [MenuItem("Hollow Tower/Players/Survivor/2. Replace Players In Open Scene")]
        public static void ReplaceMenu() { Debug.Log("[Survivor] " + ReplacePlayers()); }

        // ------------------------------------------------------------------ import
        public static string Import()
        {
            var log = new System.Text.StringBuilder();
            var mats = BuildMaterials();
            var imp = (ModelImporter)AssetImporter.GetAtPath(Fbx);
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            foreach (var kv in mats) imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), kv.Key), kv.Value);
            imp.animationType = ModelImporterAnimationType.Generic;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            imp.motionNodeName = "";                 // cycles are already in place; no root motion
            imp.optimizeGameObjects = false;         // props hang off the hand bones
            imp.importBlendShapes = false;
            imp.resampleCurves = false;              // keep the per-frame keys exactly as baked
            imp.animationCompression = ModelImporterAnimationCompression.Off;
            imp.SaveAndReimport();

            var clips = new List<ModelImporterClipAnimation>();
            foreach (var take in imp.importedTakeInfos)
            {
                string n = take.name.Substring(take.name.LastIndexOf('|') + 1);
                clips.Add(new ModelImporterClipAnimation
                {
                    name = n, takeName = take.name,
                    firstFrame = take.startTime * take.sampleRate, lastFrame = take.stopTime * take.sampleRate,
                    loopTime = n != "SV_Death", loopPose = false,
                });
            }
            imp.clipAnimations = clips.ToArray();
            imp.SaveAndReimport();
            log.Append("clips " + clips.Count + "; ");
            BuildController(log);
            return log.ToString();
        }

        /// <summary>The file's default pose is whatever Blender last showed; the meshes' bind poses are the real rest.</summary>
        static void ToBindPose(GameObject inst)
        {
            var world = new Dictionary<Transform, Matrix4x4>();
            foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var bp = smr.sharedMesh.bindposes;
                for (int i = 0; i < smr.bones.Length && i < bp.Length; i++)
                    if (smr.bones[i] != null && !world.ContainsKey(smr.bones[i])) world[smr.bones[i]] = smr.transform.localToWorldMatrix * bp[i].inverse;
            }
            foreach (var tr in inst.GetComponentsInChildren<Transform>(true))   // parents before children
            {
                Matrix4x4 m;
                if (world.TryGetValue(tr, out m)) tr.SetPositionAndRotation(m.GetColumn(3), m.rotation);
            }
        }

        static Transform Bone(GameObject root, string name)
        {
            return root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);
        }

        static AnimationClip Clip(string name)
        {
            return AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<AnimationClip>().FirstOrDefault(c => c.name == name && !c.name.StartsWith("__preview"));
        }

        /// <summary>Clip speeds (m/s at model scale 1) written by build_survivor.py next to the FBX.</summary>
        static Dictionary<string, float> Speeds()
        {
            var d = new Dictionary<string, float>();
            var text = System.IO.File.ReadAllText(SpeedsJson);
            foreach (Match m in Regex.Matches(text, "\"(\\w+)\"\\s*:\\s*([0-9.]+)"))
                d[m.Groups[1].Value] = float.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
            return d;
        }

        // ------------------------------------------------------------------ materials
        static Dictionary<string, Material> BuildMaterials()
        {
            if (!AssetDatabase.IsValidFolder(MatDir.TrimEnd('/'))) AssetDatabase.CreateFolder(Dir, "Materials");
            foreach (var path in System.IO.Directory.GetFiles(TexDir, "*.png"))
            {
                var ti = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                string n = System.IO.Path.GetFileNameWithoutExtension(path);
                bool linear = n.EndsWith("_MetallicSmoothness") || n.EndsWith("_Occlusion");
                bool normal = n.EndsWith("_Normal");
                bool changed = ti.maxTextureSize != 2048 || ti.sRGBTexture == linear || (normal != (ti.textureType == TextureImporterType.NormalMap));
                ti.maxTextureSize = 2048;
                ti.sRGBTexture = !linear;
                ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
                ti.alphaIsTransparency = n == "SV_Hair_BaseColor" || n == "SV_Brows_BaseColor";
                ti.mipMapsPreserveCoverage = ti.alphaIsTransparency;
                if (ti.mipMapsPreserveCoverage) ti.alphaTestReferenceValue = 0.4f;
                if (changed || ti.alphaIsTransparency) ti.SaveAndReimport();
            }
            var m = new Dictionary<string, Material>();
            foreach (var part in new[] { "Backpack", "Gloves", "Jacket", "Jeans", "Shoes", "Body_1001", "Body_1002", "Body_1003" })
                m["SV_" + part] = Lit("SV_" + part, 1f, true);
            m["SV_Hair"] = Lit("SV_Hair", 0.35f, false, true);
            m["SV_Brows"] = Lit("SV_Brows", 0.25f, false, true);
            m["SV_Eye"] = Lit("SV_Eye", 0.85f, false);
            m["SV_Teeth"] = Lit("SV_Teeth", 0.6f, false);
            AssetDatabase.SaveAssets();
            return m;
        }

        static Texture2D Tex(string n) { return AssetDatabase.LoadAssetAtPath<Texture2D>(TexDir + n + ".png"); }

        static Material Lit(string name, float smoothness, bool packed, bool cutout = false)
        {
            string path = MatDir + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
            mat.shader = Shader.Find("Universal Render Pipeline/Lit");
            mat.SetTexture("_BaseMap", Tex(name + "_BaseColor"));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", smoothness);
            var nrm = Tex(name + "_Normal");
            mat.SetTexture("_BumpMap", nrm);
            if (nrm != null) mat.EnableKeyword("_NORMALMAP"); else mat.DisableKeyword("_NORMALMAP");
            if (packed)
            {
                mat.SetTexture("_MetallicGlossMap", Tex(name + "_MetallicSmoothness"));
                mat.SetFloat("_Metallic", 1f);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetTexture("_OcclusionMap", Tex(name + "_Occlusion"));
                mat.SetFloat("_OcclusionStrength", 1f);
                mat.EnableKeyword("_OCCLUSIONMAP");
            }
            else mat.SetFloat("_Metallic", 0f);
            if (cutout)
            {
                mat.SetFloat("_AlphaClip", 1f);
                mat.SetFloat("_Cutoff", 0.4f);
                mat.SetFloat("_Cull", 0f);
                mat.EnableKeyword("_ALPHATEST_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
                mat.SetOverrideTag("RenderType", "TransparentCutout");
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------ animator
        static void BuildController(System.Text.StringBuilder log)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            ctrl.AddParameter("VelX", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("VelZ", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter(new AnimatorControllerParameter { name = "LocoMul", type = AnimatorControllerParameterType.Float, defaultFloat = 1f });
            ctrl.AddParameter(new AnimatorControllerParameter { name = "IsGrounded", type = AnimatorControllerParameterType.Bool, defaultBool = true });
            ctrl.AddParameter("IsCrouching", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("HasTorch", AnimatorControllerParameterType.Bool);
            ctrl.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Die", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("CrouchW", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("AirW", AnimatorControllerParameterType.Float);

            var v = Speeds();
            log.Append("speeds " + string.Join(", ", v.Select(kv => kv.Key.Substring(3) + " " + kv.Value.ToString("F2"))) + "; ");

            // One state for everything alive: AirW blends ground <-> fall, CrouchW blends standing <-> crouched. The driver
            // eases both weights, so changing stance never has the velocity kink of a linear state crossfade.
            var sm = ctrl.layers[0].stateMachine;
            BlendTree root;
            var loco = ctrl.CreateBlendTreeInController("Locomotion", out root, 0);
            root.blendType = BlendTreeType.Simple1D; root.blendParameter = "AirW"; root.useAutomaticThresholds = false;
            var ground = root.CreateBlendTreeChild(0f);
            ground.name = "Ground";
            ground.blendType = BlendTreeType.Simple1D; ground.blendParameter = "CrouchW"; ground.useAutomaticThresholds = false;
            root.AddChild(Clip("SV_Fall"), 1f);

            // positions are each clip's real speed, so the blend lands on the cycle that matches the ground speed
            var stand = ground.CreateBlendTreeChild(0f);
            stand.name = "Stand";
            stand.blendType = BlendTreeType.FreeformDirectional2D;
            stand.blendParameter = "VelX"; stand.blendParameterY = "VelZ";
            stand.AddChild(Clip("SV_Idle"), Vector2.zero);
            stand.AddChild(Clip("SV_Walk"), new Vector2(0f, v["SV_Walk"]));
            stand.AddChild(Clip("SV_Run"), new Vector2(0f, v["SV_Run"]));
            stand.AddChild(Clip("SV_Sprint"), new Vector2(0f, v["SV_Sprint"]));
            stand.AddChild(Clip("SV_WalkBack"), new Vector2(0f, -v["SV_WalkBack"]));
            stand.AddChild(Clip("SV_WalkLeft"), new Vector2(-v["SV_WalkLeft"], 0f));
            stand.AddChild(Clip("SV_WalkRight"), new Vector2(v["SV_WalkRight"], 0f));

            var crouchTree = ground.CreateBlendTreeChild(1f);
            crouchTree.name = "Crouch";
            crouchTree.blendType = BlendTreeType.FreeformDirectional2D;
            crouchTree.blendParameter = "VelX"; crouchTree.blendParameterY = "VelZ";
            crouchTree.AddChild(Clip("SV_CrouchIdle"), Vector2.zero);
            crouchTree.AddChild(Clip("SV_CrouchWalkF"), new Vector2(0f, v["SV_CrouchWalkF"]));
            crouchTree.AddChild(Clip("SV_CrouchWalkF"), new Vector2(0f, -v["SV_CrouchWalkF"]));
            crouchTree.AddChild(Clip("SV_CrouchWalkL"), new Vector2(-v["SV_CrouchWalkL"], 0f));
            crouchTree.AddChild(Clip("SV_CrouchWalkR"), new Vector2(v["SV_CrouchWalkR"], 0f));
            var ch = crouchTree.children; ch[2].timeScale = -1f; crouchTree.children = ch;

            loco.speedParameter = "LocoMul"; loco.speedParameterActive = true;
            var death = sm.AddState("Death"); death.motion = Clip("SV_Death");
            sm.defaultState = loco;
            var die = sm.AddAnyStateTransition(death);
            die.duration = 0.3f; die.hasExitTime = false; die.canTransitionToSelf = false;
            die.AddCondition(AnimatorConditionMode.If, 0, "Die");

            // arm-only layer: P4's raised compass / torch hand, faded in by CharacterAnimDriver
            var mask = new AvatarMask();
            var inst = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Fbx));
            try
            {
                var all = inst.GetComponentsInChildren<Transform>(true).Where(t => t != inst.transform).ToArray();
                mask.transformCount = all.Length;
                for (int i = 0; i < all.Length; i++)
                {
                    string path = AnimationUtility.CalculateTransformPath(all[i], inst.transform);
                    mask.SetTransformPath(i, path);
                    mask.SetTransformActive(i, path.Contains("/clavicle_l"));
                }
            }
            finally { Object.DestroyImmediate(inst); }
            AssetDatabase.DeleteAsset(MaskPath);
            AssetDatabase.CreateAsset(mask, MaskPath);
            ctrl.AddLayer("HoldItem");
            var layers = ctrl.layers;
            layers[1].avatarMask = mask;
            layers[1].defaultWeight = 0f;
            layers[1].blendingMode = AnimatorLayerBlendingMode.Override;
            var hold = layers[1].stateMachine.AddState("Hold");
            hold.motion = Clip("SV_HoldItem");
            ctrl.layers = layers;
            EditorUtility.SetDirty(ctrl);
            AssetDatabase.SaveAssets();
            log.Append("controller built; ");
        }

        static float ModelScale()
        {
            var inst = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Fbx));
            try
            {
                ToBindPose(inst);
                float eye = Bone(inst, "head").position.y + 0.1f;   // eyes sit ~10 cm above the head joint
                return EyeHeight / eye;
            }
            finally { Object.DestroyImmediate(inst); }
        }

        // ------------------------------------------------------------------ players
        public static string ReplacePlayers()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ControllerPath);
            if (prefab == null || ctrl == null) return "run step 1 first";
            float scale = ModelScale();
            var v = Speeds();

            int n = 0;
            foreach (var p in Object.FindObjectsByType<RemasteredPlayerController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var old = p.characterModel;
                Transform can = null, compass = null;
                int layer = p.gameObject.layer;
                if (old != null)
                {
                    foreach (var t in old.GetComponentsInChildren<Transform>(true))
                    {
                        if (t.name == "SprayCan") can = t;
                        else if (t.name == "Compass") compass = t;
                    }
                    var r0 = old.GetComponentInChildren<Renderer>(true);
                    if (r0 != null) layer = r0.gameObject.layer;
                }

                var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, p.transform);
                model.name = "Model_Survivor";
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;
                model.transform.localScale = Vector3.one * scale;
                foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
                ToBindPose(model);
                var anim = model.GetComponent<Animator>();
                anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var drv = model.GetComponent<CharacterAnimDriver>() ?? model.AddComponent<CharacterAnimDriver>();
                drv.runClipSpeed = v["SV_Sprint"]; drv.backClipSpeed = v["SV_WalkBack"];
                drv.strafeClipSpeed = (v["SV_WalkLeft"] + v["SV_WalkRight"]) * 0.5f;
                drv.crouchClipSpeed = v["SV_CrouchWalkF"];
                drv.crouchSideClipSpeed = (v["SV_CrouchWalkL"] + v["SV_CrouchWalkR"]) * 0.5f;
                foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    smr.updateWhenOffscreen = true;
                    smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                }

                if (can != null) PlaceInPalm(can, Bone(model, "hand_r"), false);
                if (compass != null) PlaceInPalm(compass, Bone(model, "hand_l"), true);
                if (old != null) Object.DestroyImmediate(old);

                p.characterModel = model;
                p.animator = anim;
                EditorUtility.SetDirty(p);
                n++;
            }
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            return n + " players now use the survivor (scale " + scale.ToString("F3") + ")";
        }

        /// <summary>
        /// Puts a held prop in the palm of a hand (hand_l / hand_r), measured from the fingers rather than the bone axes:
        /// a can lies across the palm, the compass lies flat in it, face up out of the hand.
        /// </summary>
        public static void PlaceInPalm(Transform prop, Transform hand, bool flat)
        {
            if (prop == null || hand == null) return;
            string s = hand.name.EndsWith("_l") ? "_l" : "_r";
            var root = hand.GetComponentInParent<Animator>().gameObject;
            Transform middle = Bone(root, "middle_01" + s), index = Bone(root, "index_01" + s);
            Transform little = Bone(root, "pinky_01" + s), thumb = Bone(root, "thumb_02" + s);
            Vector3 fingers = (middle.position - hand.position).normalized;
            Vector3 across = Vector3.ProjectOnPlane(index.position - little.position, fingers).normalized;
            // the palm faces the side the thumb curls toward
            Vector3 palm = Vector3.Cross(fingers, across);
            if (Vector3.Dot(palm, thumb.position - (hand.position + middle.position) * 0.5f) < 0f) palm = -palm;
            Vector3 centre = Vector3.Lerp(hand.position, middle.position, 0.75f);
            prop.SetParent(hand, true);
            if (flat)
            {
                prop.position = centre + palm * 0.025f;
                prop.rotation = Quaternion.LookRotation(fingers, palm);
            }
            else
            {
                prop.position = centre + palm * 0.04f;
                prop.rotation = Quaternion.LookRotation(palm, across * (s == "_l" ? -1f : 1f));
            }
        }
    }
}
