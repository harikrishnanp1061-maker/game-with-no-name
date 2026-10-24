using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace HollowTower.EditorTools
{
    /// <summary>
    /// Animates the Human Deer (it came with a skeleton but no animation): builds its materials, five clips and an
    /// animator controller. The clips are made in code from per-bone swing functions (pitch, yaw, roll in degrees) that
    /// are applied about axes measured from the rest pose: "pitch" turns about the creature's left-right axis, "yaw"
    /// about the vertical, "roll" about front-back, so the same numbers mean the same thing on every bone whatever its
    /// local orientation. The model faces -Z at rest: positive pitch swings a hanging limb forward and lifts the spine.
    ///   Deer_Idle    hunched breathing, slow head turns with twitches, jaw working, tail curling
    ///   Deer_Walk    a long, stalking, hunched gait (digitigrade legs, dragging arms)
    ///   Deer_Run     a low, fast lope with arms reaching forward
    ///   Deer_Attack  rear back, then lunge and rake both arms down, jaw wide
    ///   Deer_Scream  head thrown up, jaw gaping, arms flung out, shaking
    /// Menu: Hollow Tower > Monsters > Build Human Deer Animations.
    /// </summary>
    public static class HumanDeerAnimator
    {
        public const string Dir = "Assets/Characters/HumanDeer";
        public const string Fbx = Dir + "/HumanDeer.fbx";
        public const string ControllerPath = Dir + "/HumanDeer_Animator.controller";
        const float Fps = 30f;

        class Bone
        {
            public Transform t;
            public string path;
            public Quaternion rest;
            public Vector3 lat, up, fwd;   // world X, Y, Z in the bone's rest local space
        }

        delegate Vector3 Pose(string bone, float t);   // degrees: pitch, yaw, roll

        [MenuItem("Hollow Tower/Monsters/Build Human Deer Animations")]
        public static void BuildMenu() { Debug.Log("[HumanDeer] " + Build()); }

        public static string Build()
        {
            FixTextures();
            var src = AssetDatabase.LoadAssetAtPath<GameObject>(Fbx);
            var go = (GameObject)UnityEngine.Object.Instantiate(src);
            try
            {
                var bones = new Dictionary<string, Bone>();
                foreach (var t in go.GetComponentsInChildren<Transform>())
                {
                    if (!t.name.EndsWith("_J")) continue;
                    bones[t.name] = new Bone
                    {
                        t = t, path = AnimationUtility.CalculateTransformPath(t, go.transform), rest = t.localRotation,
                        lat = t.InverseTransformDirection(Vector3.right).normalized,
                        up = t.InverseTransformDirection(Vector3.up).normalized,
                        fwd = t.InverseTransformDirection(Vector3.forward).normalized
                    };
                }
                var hip = bones.ContainsKey("C_Hip_J") ? bones["C_Hip_J"].t : null;
                Vector3 hipRest = hip != null ? hip.localPosition : Vector3.zero;

                var clips = new Dictionary<string, AnimationClip>
                {
                    { "Deer_Idle", Make("Deer_Idle", 4f, true, bones, hip, hipRest, Idle, t => new Vector3(0f, Mathf.Sin(t * Mathf.PI * 2f / 4f) * 1.2f, 0f)) },
                    { "Deer_Walk", Make("Deer_Walk", 1.5f, true, bones, hip, hipRest, (b, t) => Gait(b, t, 1.5f, 1f), t => new Vector3(0f, -Mathf.Cos(t * Mathf.PI * 4f / 1.5f) * 3f, 0f)) },
                    { "Deer_Run", Make("Deer_Run", 0.62f, true, bones, hip, hipRest, (b, t) => Gait(b, t, 0.62f, 1.7f), t => new Vector3(0f, -Mathf.Cos(t * Mathf.PI * 4f / 0.62f) * 5f - 6f, 0f)) },
                    { "Deer_Attack", Make("Deer_Attack", 1.2f, false, bones, hip, hipRest, Attack, t => new Vector3(0f, -Lunge(t) * 6f, 0f)) },
                    { "Deer_Scream", Make("Deer_Scream", 1.2f, false, bones, hip, hipRest, Scream, t => new Vector3(0f, Bell(t, 0.15f, 1.05f) * 4f, 0f)) },
                };
                BuildController(clips);
                return clips.Count + " clips over " + bones.Count + " bones, controller " + ControllerPath;
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }

        // ------------------------------------------------------------------ motion
        static float Ph(float t, float period) { return t / period * Mathf.PI * 2f; }
        static float Bell(float t, float a, float b) { return t < a || t > b ? 0f : Mathf.Sin((t - a) / (b - a) * Mathf.PI); }
        static float Lunge(float t) { return Bell(t, 0.3f, 0.75f); }
        static float WindUp(float t) { return Bell(t, 0f, 0.45f); }
        static float Twitch(float t, float seed) { float x = Mathf.PerlinNoise(t * 3f, seed); return x > 0.78f ? (x - 0.78f) * 140f : 0f; }

        static int TailIndex(string b)
        {
            int i = b.IndexOf("tail_");
            int n;
            return i >= 0 && b.Length >= i + 7 && int.TryParse(b.Substring(i + 5, 2), out n) ? n : 0;
        }

        static Vector3 Idle(string b, float t)
        {
            float br = Mathf.Sin(Ph(t, 4f));            // breathing
            float look = Mathf.Sin(Ph(t, 8f) + 1f);     // slow head turn
            switch (b)
            {
                case "C_spine_02_J": case "C_spine_03_J": return new Vector3(2.5f * br, 0f, 0f);
                case "C_spine_04_J": return new Vector3(3f * br, 3f * look, 0f);
                case "C_Neck_J": return new Vector3(-2f * br, 10f * look, 0f);
                case "C_Head_J": return new Vector3(-2f * br + Twitch(t, 1.3f) * 0.3f, 14f * look + Twitch(t, 7.1f), 6f * Mathf.Sin(Ph(t, 2.7f)) + Twitch(t, 3.3f) * 0.5f);
                case "C_jaw__J": return new Vector3(4f + 4f * Mathf.Abs(Mathf.Sin(Ph(t, 1.9f))), 0f, 0f);
                case "L_shoulder_J": case "R_shoulder_J": return new Vector3(3f * br, 0f, 0f);
                case "L_elbow_J": case "R_elbow_J": return new Vector3(4f + 2f * br, 0f, 0f);
                case "L_hand_J": case "R_hand_J": return new Vector3(0f, 0f, Twitch(t, b[0] == 'L' ? 5.5f : 9.2f) * 0.4f);
            }
            int ti = TailIndex(b);
            if (ti > 0) return new Vector3(4f * Mathf.Sin(Ph(t, 4f) - ti * 0.35f), 7f * Mathf.Sin(Ph(t, 4f) * 0.5f - ti * 0.45f), 0f);
            return Vector3.zero;
        }

        /// <summary>Walk (power 1) and run (power ~1.7): the legs alternate, arms swing against them, body counter-twists.</summary>
        static Vector3 Gait(string b, float t, float period, float power)
        {
            float p = Ph(t, period), s = Mathf.Sin(p), c = Mathf.Cos(p);
            bool run = power > 1.2f;
            float side = b[0] == 'L' ? 1f : -1f;
            float legPh = b[0] == 'L' ? p : p + Mathf.PI;
            float ls = Mathf.Sin(legPh), lc = Mathf.Cos(legPh);
            switch (b)
            {
                case "C_Hip_J": return new Vector3(run ? -6f : -2f, 6f * s * power, 4f * s);
                case "C_spine_01_J": return new Vector3(run ? -6f : 0f, -3f * s * power, 0f);
                case "C_spine_02_J": return new Vector3(2f * Mathf.Sin(2f * p), -3f * s * power, 0f);
                case "C_spine_03_J": return new Vector3(2f * Mathf.Sin(2f * p) + (run ? -4f : 0f), -2f * s, 0f);
                case "C_spine_04_J": return new Vector3(run ? -5f : 0f, -2f * s, -2f * s);
                case "C_Neck_J": return new Vector3((run ? 10f : 4f) - 4f * Mathf.Sin(2f * p + 0.6f), 4f * s, 0f);
                case "C_Head_J": return new Vector3((run ? 6f : 2f) - 5f * Mathf.Sin(2f * p + 0.9f), 3f * s + Twitch(t * 2f, 4.4f) * 0.6f, 0f);
                case "C_jaw__J": return new Vector3(run ? 14f + 6f * Mathf.Abs(s) : 5f, 0f, 0f);
                case "L_upper_leg_J": case "R_upper_leg_J": return new Vector3(26f * ls * power + (run ? 8f : 0f), 0f, 0f);
                case "L_knee_J": case "R_knee_J": return new Vector3(-(18f + 30f * Mathf.Max(0f, lc) * power), 0f, 0f);
                case "L_ankle_J": case "R_ankle_J": return new Vector3(14f + 20f * Mathf.Max(0f, lc) * power - 8f * ls, 0f, 0f);
                case "L_feet_J": case "R_feet_J": return new Vector3(-10f * Mathf.Max(0f, -ls), 0f, 0f);
                case "L_shoulder_J": case "R_shoulder_J": return new Vector3(-22f * ls * power + (run ? 35f : 6f), 0f, side * (run ? 6f : 3f));
                case "L_elbow_J": case "R_elbow_J": return new Vector3((run ? 30f : 12f) + 12f * Mathf.Max(0f, -ls) * power, 0f, 0f);
                case "L_hand_J": case "R_hand_J": return new Vector3(8f * ls, 0f, 0f);
            }
            int ti = TailIndex(b);
            if (ti > 0) return new Vector3((run ? 6f : 3f) * Mathf.Sin(2f * p - ti * 0.4f), 9f * power * Mathf.Sin(p - ti * 0.5f), 0f);
            return Vector3.zero;
        }

        static Vector3 Attack(string b, float t)
        {
            float w = WindUp(t), l = Lunge(t), shake = Mathf.Sin(t * 70f) * w * 3f;
            switch (b)
            {
                case "C_Hip_J": return new Vector3(-8f * l + 5f * w, 0f, 0f);
                case "C_spine_02_J": case "C_spine_03_J": return new Vector3(12f * w - 16f * l, 0f, 0f);
                case "C_spine_04_J": return new Vector3(10f * w - 14f * l, 0f, shake);
                case "C_Neck_J": return new Vector3(8f * w - 14f * l, 0f, 0f);
                case "C_Head_J": return new Vector3(10f * w - 10f * l, shake, 0f);
                case "C_jaw__J": return new Vector3(8f + 22f * Mathf.Max(w, l), 0f, 0f);
                case "L_shoulder_J": case "R_shoulder_J": return new Vector3(85f * w - 35f * l + 20f * l * Mathf.Sin(t * 20f), 0f, (b[0] == 'L' ? 1f : -1f) * 20f * w);
                case "L_elbow_J": case "R_elbow_J": return new Vector3(55f * w - 20f * l, 0f, 0f);
                case "L_hand_J": case "R_hand_J": return new Vector3(-25f * w + 30f * l, 0f, 0f);
                case "L_upper_leg_J": case "R_upper_leg_J": return new Vector3((b[0] == 'L' ? 22f : -8f) * l, 0f, 0f);
                case "L_knee_J": case "R_knee_J": return new Vector3(-12f * w - 18f * l, 0f, 0f);
            }
            int ti = TailIndex(b);
            if (ti > 0) return new Vector3(10f * (w - l) * Mathf.Sin(ti * 0.6f), 8f * Mathf.Sin(t * 12f - ti * 0.5f) * (w + l), 0f);
            return Vector3.zero;
        }

        static Vector3 Scream(string b, float t)
        {
            float k = Bell(t, 0.1f, 1.15f), shake = Mathf.Sin(t * 85f) * k;
            switch (b)
            {
                case "C_spine_03_J": case "C_spine_04_J": return new Vector3(12f * k, 0f, shake * 2f);
                case "C_Neck_J": return new Vector3(22f * k, shake * 3f, 0f);
                case "C_Head_J": return new Vector3(25f * k, shake * 6f, shake * 4f);
                case "C_jaw__J": return new Vector3(6f + 34f * k, 0f, 0f);
                case "C_Tongue_01_J": case "C_Tongue_02_J": return new Vector3(-10f * k, shake * 8f, 0f);
                case "L_shoulder_J": case "R_shoulder_J": return new Vector3(30f * k, 0f, (b[0] == 'L' ? 1f : -1f) * 55f * k);
                case "L_elbow_J": case "R_elbow_J": return new Vector3(25f * k, 0f, 0f);
                case "L_hand_J": case "R_hand_J": return new Vector3(0f, 0f, shake * 10f);
            }
            int ti = TailIndex(b);
            if (ti > 0) return new Vector3(14f * k * Mathf.Sin(ti * 0.5f), shake * 6f, 0f);
            return Vector3.zero;
        }

        // ------------------------------------------------------------------ baking
        static AnimationClip Make(string name, float length, bool loop, Dictionary<string, Bone> bones, Transform hip, Vector3 hipRest, Pose pose, Func<float, Vector3> hipOffset)
        {
            string path = Dir + "/" + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip == null) { clip = new AnimationClip { name = name }; AssetDatabase.CreateAsset(clip, path); }
            clip.ClearCurves();
            clip.frameRate = Fps;
            int frames = Mathf.RoundToInt(length * Fps);
            foreach (var kv in bones)
            {
                var b = kv.Value;
                var curves = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
                bool moves = false;
                Quaternion prev = b.rest;
                for (int f = 0; f <= frames; f++)
                {
                    float t = f / Fps;
                    Vector3 a = pose(kv.Key, loop ? t % length : t);
                    if (a != Vector3.zero) moves = true;
                    var q = b.rest * Quaternion.AngleAxis(a.x, b.lat) * Quaternion.AngleAxis(a.y, b.up) * Quaternion.AngleAxis(a.z, b.fwd);
                    if (Quaternion.Dot(prev, q) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);   // keep the curves continuous
                    prev = q;
                    curves[0].AddKey(t, q.x); curves[1].AddKey(t, q.y); curves[2].AddKey(t, q.z); curves[3].AddKey(t, q.w);
                }
                // every bone gets keys (even at rest) so a crossfade from another clip settles it back
                string[] props = { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
                for (int i = 0; i < 4; i++)
                {
                    if (!moves) curves[i] = new AnimationCurve(curves[i].keys[0], curves[i].keys[curves[i].length - 1]);
                    AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(b.path, typeof(Transform), props[i]), curves[i]);
                }
            }
            if (hip != null)
            {
                string hp = bones["C_Hip_J"].path;
                var cx = new AnimationCurve(); var cy = new AnimationCurve(); var cz = new AnimationCurve();
                for (int f = 0; f <= frames; f++)
                {
                    float t = f / Fps;
                    Vector3 o = hipRest + hipOffset(loop ? t % length : t);
                    cx.AddKey(t, o.x); cy.AddKey(t, o.y); cz.AddKey(t, o.z);
                }
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(hp, typeof(Transform), "m_LocalPosition.x"), cx);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(hp, typeof(Transform), "m_LocalPosition.y"), cy);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(hp, typeof(Transform), "m_LocalPosition.z"), cz);
            }
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        static void BuildController(Dictionary<string, AnimationClip> clips)
        {
            AssetDatabase.DeleteAsset(ControllerPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            var sm = ctrl.layers[0].stateMachine;
            foreach (var kv in clips)
            {
                var st = sm.AddState(kv.Key);
                st.motion = kv.Value;
                if (kv.Key == "Deer_Idle") sm.defaultState = st;
            }
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ materials
        static void FixTextures()
        {
            foreach (var n in new[] { "deer_body_12_Deer_body_Normal.png", "deer_head_12.6_Deer_head_Normal.png" })
            {
                var imp = AssetImporter.GetAtPath(Dir + "/Textures/" + n) as TextureImporter;
                if (imp != null && imp.textureType != TextureImporterType.NormalMap) { imp.textureType = TextureImporterType.NormalMap; imp.SaveAndReimport(); }
            }
        }

        public static Material BodyMaterial(bool head)
        {
            string part = head ? "head_12.6_Deer_head" : "body_12_Deer_body";
            string path = Dir + "/Mat_HumanDeer_" + (head ? "Head" : "Body") + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            var tex = Dir + "/Textures/deer_" + part;
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "_BaseColor.png"));
            m.SetColor("_BaseColor", new Color(0.82f, 0.8f, 0.78f));
            var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "_Normal.png");
            if (nrm != null) { m.SetTexture("_BumpMap", nrm); m.EnableKeyword("_NORMALMAP"); }
            var em = AssetDatabase.LoadAssetAtPath<Texture2D>(tex + "_Emissive.png");
            if (em != null)
            {
                // the emissive map lights the eyes and wounds faintly: something to see in the dark
                m.SetTexture("_EmissionMap", em); m.SetColor("_EmissionColor", new Color(0.9f, 0.35f, 0.25f) * 0.8f);
                m.EnableKeyword("_EMISSION"); m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            m.SetFloat("_Smoothness", 0.28f);
            m.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(m);
            return m;
        }
    }
}
