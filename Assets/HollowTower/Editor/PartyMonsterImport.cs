using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// The floor 2 party guest (Zoonomaly monster 17: a pale big-eyed, big-grinned imp). Its FBX carries eight takes
    /// named "...skeleton|idle" and so on; this names them plainly (Idle, Walk, Run, Attack1-3, Jumpscare, Hit), loops the
    /// three locomotion ones, gives it a URP material from its body and normal maps, and builds the Animator controller
    /// PartyMonster drives by state name. Safe to run again.
    /// </summary>
    public static class PartyMonsterImport
    {
        public const string Folder = "Assets/Characters/PartyMonster/";
        public const string Fbx = Folder + "PartyMonster.fbx";
        public const string ControllerPath = Folder + "PartyMonster.controller";
        public const string MaterialPath = Folder + "Mat_PartyMonster.mat";

        [MenuItem("Hollow Tower/Floor 2/Import Party Monster")]
        public static void BuildMenu() { Debug.Log("[PartyMonster] " + Build()); }

        public static string Build()
        {
            var mi = AssetImporter.GetAtPath(Fbx) as ModelImporter;
            if (mi == null) return "model missing at " + Fbx;
            bool changed = false;
            var clips = mi.clipAnimations;
            if (clips == null || clips.Length == 0) clips = mi.defaultClipAnimations;
            foreach (var c in clips)
            {
                string n = c.name.Contains("|") ? c.name.Substring(c.name.LastIndexOf('|') + 1) : c.name;
                string want = Plain(n);
                bool loop = want == "Idle" || want == "Walk" || want == "Run";
                if (c.name != want || c.loopTime != loop) { c.name = want; c.loopTime = loop; changed = true; }
                c.lockRootPositionXZ = true;   // the walk and run move on the spot; the NavMeshAgent moves the body
                c.keepOriginalPositionXZ = true;
            }
            if (changed || mi.clipAnimations.Length == 0) { mi.clipAnimations = clips; mi.SaveAndReimport(); }

            Texture(Folder + "PartyMonster_Body.png", false);
            Texture(Folder + "PartyMonster_Normal.png", true);

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, MaterialPath); }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "PartyMonster_Body.png"));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "PartyMonster_Normal.png"));
            mat.EnableKeyword("_NORMALMAP");
            mat.SetFloat("_BumpScale", 1f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.38f);   // clammy skin
            EditorUtility.SetDirty(mat);

            AssetDatabase.DeleteAsset(ControllerPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var sm = ctrl.layers[0].stateMachine;
            int states = 0;
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(Fbx))
            {
                var clip = o as AnimationClip;
                if (clip == null || clip.name.StartsWith("__preview__")) continue;
                var s = sm.AddState(clip.name);
                s.motion = clip;
                if (clip.name == "Idle") sm.defaultState = s;
                states++;
            }
            AssetDatabase.SaveAssets();
            return states + " animator states, material " + mat.name;
        }

        static string Plain(string take)
        {
            string t = take.Trim().ToLower();
            switch (t)
            {
                case "idle": return "Idle";
                case "walk": return "Walk";
                case "run": return "Run";
                case "hit": return "Hit";
                case "jumpscare": return "Jumpscare";
                case "attack 1": return "Attack1";
                case "attack 2": return "Attack2";
                case "attack 3": return "Attack3";
                default: return take.Trim().Replace(" ", "");
            }
        }

        static void Texture(string path, bool normal)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null) return;
            bool dirty = false;
            if (normal && ti.textureType != TextureImporterType.NormalMap) { ti.textureType = TextureImporterType.NormalMap; dirty = true; }
            if (ti.maxTextureSize != 2048) { ti.maxTextureSize = 2048; dirty = true; }
            if (dirty) ti.SaveAndReimport();
        }
    }
}
