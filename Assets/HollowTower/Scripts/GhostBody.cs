using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace HollowTower
{
    /// <summary>
    /// The ghost's body: the "DOE Ghost" model (Resources/Models/Ghost) with a walk driven here.
    /// The model's own clips are floating poses (feet held 20-35 cm off the ground, one lurch per 5 s), so:
    ///  - the standing "Stalking 1" clip runs underneath for the idle sway of the whole body,
    ///  - while hunting, the "Chasing" clip drives the spine, arms and head only (its reaching, hunched upper body),
    ///  - the legs, hips, arm swing and head are posed here every frame from the distance actually walked, so the
    ///    stride matches the speed, and the body is then lowered so the standing foot rests exactly on the floor.
    /// It renders with solid, lit materials (base colour, normal and roughness/metal masks from the zip) and only
    /// switches to see-through copies while it fades (vanishing, scattering, or as the smoke decoy).
    /// </summary>
    public class GhostBody : MonoBehaviour
    {
        public const float Height = 1.7f;      // head to toe
        public const string ModelName = "GhostModel";

        // ---- driven by the owner every frame ----
        /// <summary>Speed along the body's forward axis (m/s, negative = backing off).</summary>
        public float ForwardSpeed;
        public bool Hunting;
        /// <summary>World point the head turns to (null: look ahead).</summary>
        public Vector3? LookAt;
        /// <summary>The height of the floor the feet stand on (NaN: no floor contact, e.g. the decoy hovers).</summary>
        public float FloorY = float.NaN;
        /// <summary>Always use the see-through materials (the smoke decoy).</summary>
        public bool AlwaysSeeThrough;

        // ---- read by the play test ----
        public float LeftThighAngle { get; private set; }
        /// <summary>How far the lowest foot was from the floor before the body was settled onto it (m).</summary>
        public float FootGap { get; private set; }
        public Animation Anim { get; private set; }

        Transform owner, hips, spine2, neck, head;
        readonly Transform[] thigh = new Transform[2], shin = new Transform[2], foot = new Transform[2], toeEnd = new Transform[2], arm = new Transform[2], fore = new Transform[2];
        float legLen, phase, headYaw, headPitch, twitchUntil, nextTwitch, twitchRoll;
        // Heel and toe-tip points on the sole, in the foot / toe bones' own space (from the rest pose).
        readonly Vector3[] heelLocal = new Vector3[2], toeLocal = new Vector3[2];
        Vector3 hipsRest;
        string clipIdle, clipHunt;
        AnimationState huntState;

        Renderer[] renderers;
        int[] part;
        Material[] solid, seeThrough;
        bool usingSeeThrough;

        // ---------------- assets ----------------

        static GameObject prefab;
        static bool tried;
        static Texture2D[] baseMaps, normalMaps, masks;   // body, cloth, hair

        static bool Load()
        {
            if (tried) return prefab != null;
            tried = true;
            prefab = Resources.Load<GameObject>("Models/Ghost/Ghost");
            string[] parts = { "Body", "Cloth", "Hair" };
            baseMaps = new Texture2D[3]; normalMaps = new Texture2D[3]; masks = new Texture2D[3];
            for (int i = 0; i < 3; i++)
            {
                baseMaps[i] = Resources.Load<Texture2D>("Models/Ghost/Ghost_" + parts[i] + "_BaseColor");
                normalMaps[i] = Resources.Load<Texture2D>("Models/Ghost/Ghost_" + parts[i] + "_Normal");
                masks[i] = Resources.Load<Texture2D>("Models/Ghost/Ghost_" + parts[i] + "_Mask");
            }
            if (prefab == null) Debug.LogWarning("Hollow Tower: ghost model missing - using the primitive ghost.");
            return prefab != null;
        }

        static Material MakeMaterial(int p, bool transparent)
        {
            var m = transparent ? Gfx.Transparent(new Color(0.9f, 0.93f, 1f, 0.8f), Color.black) : Gfx.Lit(Color.white, 0.4f);
            if (baseMaps[p] != null)
            {
                m.SetTexture("_BaseColorMap", baseMaps[p]);
                m.SetTexture("_EmissiveColorMap", baseMaps[p]);   // the faint glow follows the texture
            }
            if (normalMaps[p] != null) { m.SetTexture("_NormalMap", normalMaps[p]); m.SetFloat("_NormalScale", 1.2f); }
            if (masks[p] != null)
            {
                m.SetTexture("_MaskMap", masks[p]);
                m.SetFloat("_SmoothnessRemapMin", 0f);
                m.SetFloat("_SmoothnessRemapMax", 0.8f);
                m.SetFloat("_AORemapMin", 0f);
                m.SetFloat("_AORemapMax", 1f);
            }
            m.SetFloat("_DoubleSidedEnable", p == 0 ? 0f : 1f);   // the dress and hair are single sheets
            if (transparent) m.SetFloat("_TransparentDepthPrepassEnable", 1f);
            if (p == 2)
            {
                m.SetFloat("_AlphaCutoffEnable", 1f);
                m.SetFloat("_AlphaCutoff", 0.35f);
            }
            HDMaterial.ValidateMaterial(m);
            return m;
        }

        /// <summary>Builds the body under <paramref name="parent"/>, feet <paramref name="feetBelow"/> below it. Null if the model is missing.</summary>
        public static GhostBody Create(Transform parent, Transform owner, float feetBelow)
        {
            if (!Load()) return null;
            var holder = new GameObject(ModelName).transform;
            holder.SetParent(parent, false);
            var model = Instantiate(prefab, holder, false);
            model.name = prefab.name;
            var b = holder.gameObject.AddComponent<GhostBody>();
            b.owner = owner;
            b.Build(model.transform, feetBelow);
            return b;
        }

        void Build(Transform model, float feetBelow)
        {
            var bones = new Dictionary<string, Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) bones[t.name] = t;
            System.Func<string, Transform> B = n => { Transform t; return bones.TryGetValue(n, out t) ? t : null; };
            hips = B("Hips"); spine2 = B("Spine2"); neck = B("Neck"); head = B("Head");
            string[] side = { "_L", "_R" };
            for (int s = 0; s < 2; s++)
            {
                thigh[s] = B("LegUp" + side[s]); shin[s] = B("LegLow" + side[s]); foot[s] = B("Foot" + side[s]);
                toeEnd[s] = B("Toe" + side[s] + "_end"); arm[s] = B("Arm" + side[s]); fore[s] = B("ForeArm" + side[s]);
            }

            // Fit the height in the rest pose (toe to the top of the head).
            var top = B("Head_end");
            float h = top != null && toeEnd[0] != null ? transform.InverseTransformPoint(top.position).y - transform.InverseTransformPoint(toeEnd[0].position).y : 1.25f;
            float scale = Height / Mathf.Max(0.1f, h);
            transform.localScale = Vector3.one * scale;
            transform.localPosition = new Vector3(0f, -feetBelow, 0f);
            if (thigh[0] != null && foot[0] != null)
            {
                legLen = Vector3.Distance(thigh[0].position, foot[0].position);
                float ground = transform.position.y;
                for (int s = 0; s < 2; s++)
                {
                    if (foot[s] != null) heelLocal[s] = foot[s].InverseTransformPoint(new Vector3(foot[s].position.x, ground, foot[s].position.z) - owner.forward * 0.03f);
                    if (toeEnd[s] != null) toeLocal[s] = toeEnd[s].InverseTransformPoint(new Vector3(toeEnd[s].position.x, ground, toeEnd[s].position.z));
                }
            }
            if (hips != null) hipsRest = hips.localPosition;
            FindFootVertices(model);

            // Materials: a solid set and a see-through set per ghost (alpha and glow are animated per ghost).
            solid = new Material[3];
            seeThrough = new Material[3];
            for (int i = 0; i < 3; i++) { solid[i] = MakeMaterial(i, false); seeThrough[i] = MakeMaterial(i, true); }
            renderers = model.GetComponentsInChildren<Renderer>(true);
            part = new int[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                var r = renderers[i];
                part[i] = r.name.StartsWith("Dress") ? 1 : r.name.StartsWith("Hair") ? 2 : 0;
                r.rayTracingMode = UnityEngine.Experimental.Rendering.RayTracingMode.Off;
                var smr = r as SkinnedMeshRenderer;
                if (smr != null) smr.updateWhenOffscreen = true;   // its imported bounds are ~30x too big
            }
            usingSeeThrough = true;
            ApplyMaterials(false);

            Anim = model.GetComponentInChildren<Animation>();
            if (Anim != null)
            {
                Anim.cullingType = AnimationCullingType.AlwaysAnimate;
                foreach (AnimationState st in Anim)
                {
                    st.wrapMode = WrapMode.Loop;
                    if (st.name.Contains("Stalking 1")) clipIdle = st.name;
                    if (st.name.Contains("Chasing") && !st.name.StartsWith("__")) clipHunt = st.name;
                }
                if (clipIdle != null)
                {
                    Anim.Play(clipIdle);
                    Anim[clipIdle].time = Random.value * Anim[clipIdle].length;
                }
                if (clipHunt != null && B("Spine1") != null)
                {
                    huntState = Anim[clipHunt];
                    huntState.layer = 1;
                    huntState.AddMixingTransform(B("Spine1"), true);   // upper body only: the legs walk
                    huntState.weight = 0f;
                    huntState.enabled = true;
                    huntState.speed = 1.6f;
                }
            }
            phase = Random.value;
            nextTwitch = Time.time + Random.Range(2f, 5f);
        }

        void ApplyMaterials(bool transparent)
        {
            if (transparent == usingSeeThrough) return;
            usingSeeThrough = transparent;
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].sharedMaterial = transparent ? seeThrough[part[i]] : solid[part[i]];
                renderers[i].shadowCastingMode = transparent ? ShadowCastingMode.Off : ShadowCastingMode.On;
            }
        }

        void OnDestroy()
        {
            for (int i = 0; i < 3; i++) { if (solid != null && solid[i] != null) Destroy(solid[i]); if (seeThrough != null && seeThrough[i] != null) Destroy(seeThrough[i]); }
            if (baked != null) Destroy(baked);
        }

        public void SetRenderersEnabled(bool on)
        {
            foreach (var r in renderers) if (r != null) r.enabled = on;
            if (Anim != null) Anim.enabled = on;
            enabled = on;
        }

        /// <summary>Visibility 0..1 (1 = solid) and glow (old 0..1 emission scale).</summary>
        public void SetLook(float alpha, Color glow)
        {
            bool fade = AlwaysSeeThrough || alpha < 0.985f;
            ApplyMaterials(fade);
            var mats = fade ? seeThrough : solid;
            for (int i = 0; i < 3; i++)
            {
                if (fade)
                {
                    Color c = mats[i].GetColor(Gfx.BaseColorId);
                    c.a = 0.8f * Mathf.Clamp01(alpha);
                    mats[i].SetColor(Gfx.BaseColorId, c);
                }
                Gfx.SetEmission(mats[i], glow * (fade ? Mathf.Clamp01(alpha) : 1f));
            }
        }

        // ---------------- the walk ----------------

        readonly List<Transform> posed = new List<Transform>();
        readonly List<Quaternion> posedRot = new List<Quaternion>();

        void RememberRest()
        {
            foreach (var t in new[] { hips, spine2, neck, head, thigh[0], thigh[1], shin[0], shin[1], foot[0], foot[1], arm[0], arm[1], fore[0], fore[1] })
                if (t != null) { posed.Add(t); posedRot.Add(t.localRotation); }
        }

        /// <summary>
        /// Before the clip is applied, put every bone we pose back to rest: bones the clip doesn't animate would
        /// otherwise pile up this frame's offsets on top of last frame's.
        /// </summary>
        void Update()
        {
            if (posed.Count == 0) RememberRest();
            for (int i = 0; i < posed.Count; i++) posed[i].localRotation = posedRot[i];
            if (hips != null) hips.localPosition = hipsRest;
        }

        void LateUpdate()
        {
            if (thigh[0] == null || thigh[1] == null || owner == null) return;
            float dt = Time.deltaTime;
            Vector3 right = owner.right, up = owner.up;

            // Upper body: the reaching Chasing pose fades in while hunting.
            if (huntState != null) huntState.weight = Mathf.MoveTowards(huntState.weight, Hunting ? 1f : 0f, dt * 2.5f);
            float huntW = huntState != null ? huntState.weight : 0f;

            // Gait: one stride (two steps) per 'stride' metres, so the feet don't skate.
            float speed = Mathf.Abs(ForwardSpeed);
            float run = Mathf.InverseLerp(1.6f, 4f, speed);
            float ampMax = Mathf.Lerp(24f, 36f, run);
            float amp = ampMax * Mathf.Clamp01(speed / 0.5f);
            float stride = Mathf.Max(0.4f, 4f * legLen * Mathf.Sin(ampMax * Mathf.Deg2Rad));
            phase += ForwardSpeed * dt / stride;
            if (speed < 0.05f) phase = Mathf.MoveTowards(phase, Mathf.Round(phase * 2f) * 0.5f, dt * 0.6f);   // settle to standing
            float kneeMax = Mathf.Lerp(40f, 70f, run);

            for (int s = 0; s < 2; s++)
            {
                float ph = (phase + s * 0.5f) * Mathf.PI * 2f;
                float swing = amp * Mathf.Sin(ph);                                      // + = leg forward
                float lift = Mathf.Max(0f, Mathf.Cos(ph));                              // swing phase: foot travelling forward
                float knee = 6f * Mathf.Clamp01(speed) + kneeMax * Mathf.Clamp01(speed / 0.5f) * Mathf.Pow(lift, 1.5f);
                thigh[s].rotation = Quaternion.AngleAxis(-swing, right) * thigh[s].rotation;
                shin[s].rotation = Quaternion.AngleAxis(knee, right) * shin[s].rotation;
                // Keep the sole flat while it carries weight; point the toes down a little as it swings through.
                float footPitch = (knee - swing) * (1f - 0.15f * lift) - 8f * lift * Mathf.Clamp01(speed);
                if (foot[s] != null) foot[s].rotation = Quaternion.AngleAxis(-footPitch, right) * foot[s].rotation;
                if (s == 0) LeftThighAngle = swing;

                // Arms swing against the legs (the left arm with the right leg); the reach takes over while hunting.
                int o = 1 - s;
                float armSwing = 0.55f * swing * (1f - 0.8f * huntW);
                if (arm[o] != null) arm[o].rotation = Quaternion.AngleAxis(-armSwing, right) * arm[o].rotation;
                if (fore[o] != null) fore[o].rotation = Quaternion.AngleAxis(-(8f + Mathf.Max(0f, armSwing) * 0.6f) * (1f - huntW) * Mathf.Clamp01(speed), right) * fore[o].rotation;
            }

            // Hips sway over the standing foot and twist with the stride; the chest twists back.
            float ph0 = phase * Mathf.PI * 2f;
            float amt = amp / 36f;
            if (hips != null)
            {
                hips.position += right * (0.03f * Mathf.Sin(ph0) * amt);
                hips.rotation = Quaternion.AngleAxis(5f * Mathf.Sin(ph0) * amt, up) * Quaternion.AngleAxis(4f * run * Mathf.Clamp01(speed), right) * hips.rotation;
            }
            if (spine2 != null) spine2.rotation = Quaternion.AngleAxis(-7f * Mathf.Sin(ph0) * amt, up) * spine2.rotation;

            // Head: turns to what it watches (neck takes a share), with the odd sharp twitch.
            float wantYaw = 0f, wantPitch = 0f;
            if (LookAt.HasValue && head != null)
            {
                Vector3 d = LookAt.Value - head.position;
                Vector3 flat = Vector3.ProjectOnPlane(d, up);
                wantYaw = Mathf.Clamp(Vector3.SignedAngle(Vector3.ProjectOnPlane(owner.forward, up), flat, up), -65f, 65f);
                wantPitch = Mathf.Clamp(-Mathf.Atan2(Vector3.Dot(d, up), flat.magnitude) * Mathf.Rad2Deg, -30f, 35f);
            }
            headYaw = Mathf.MoveTowards(headYaw, wantYaw, dt * 160f);
            headPitch = Mathf.MoveTowards(headPitch, wantPitch, dt * 90f);
            if (Time.time > nextTwitch)
            {
                nextTwitch = Time.time + Random.Range(2.5f, 6f);
                twitchUntil = Time.time + 0.18f;
                twitchRoll = Random.Range(-22f, 22f);
            }
            float roll = Time.time < twitchUntil ? twitchRoll : 0f;
            if (neck != null) neck.rotation = Quaternion.AngleAxis(headYaw * 0.35f, up) * neck.rotation;
            if (head != null)
                head.rotation = Quaternion.AngleAxis(roll, owner.forward) * Quaternion.AngleAxis(headYaw * 0.65f, up) * Quaternion.AngleAxis(headPitch, right) * head.rotation;

            // Plant the feet: lower / raise the whole body so the lowest sole sits exactly on the floor.
            if (!float.IsNaN(FloorY))
            {
                float sole = float.MaxValue;
                for (int s = 0; s < 2; s++)
                {
                    if (foot[s] != null) sole = Mathf.Min(sole, foot[s].TransformPoint(heelLocal[s]).y);
                    if (toeEnd[s] != null) sole = Mathf.Min(sole, toeEnd[s].TransformPoint(toeLocal[s]).y);
                }
                float meshSole = MeshSole();
                if (!float.IsNaN(meshSole)) sole = meshSole;
                FootGap = sole - FloorY;
                Vector3 lp = transform.localPosition;
                lp.y += -FootGap;
                transform.localPosition = lp;
            }
        }

        // ---------------- exact foot contact ----------------

        SkinnedMeshRenderer bodyMesh;
        Mesh baked;
        readonly List<Vector3> bakedVerts = new List<Vector3>();
        int[] footVerts;

        /// <summary>The vertices of the feet and ankles (the lowest 20 cm of the body mesh in the rest pose).</summary>
        void FindFootVertices(Transform model)
        {
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.name == "Body") bodyMesh = smr;
            if (bodyMesh == null) return;
            baked = new Mesh();
            bodyMesh.BakeMesh(baked, true);
            baked.GetVertices(bakedVerts);
            var t = bodyMesh.transform;
            float low = float.MaxValue;
            foreach (var v in bakedVerts) low = Mathf.Min(low, (t.position + t.rotation * v).y);
            var list = new List<int>();
            for (int i = 0; i < bakedVerts.Count; i++)
                if ((t.position + t.rotation * bakedVerts[i]).y - low < 0.2f) list.Add(i);
            footVerts = list.ToArray();
        }

        /// <summary>
        /// The lowest point of the skinned feet as posed right now (the bone-based sole points miss how the mesh
        /// bends at the ankle and toes by up to ~10 cm). Only ghosts on screen run this.
        /// </summary>
        float MeshSole()
        {
            if (bodyMesh == null || footVerts == null || footVerts.Length == 0 || !bodyMesh.enabled) return float.NaN;
            bodyMesh.BakeMesh(baked, true);
            baked.GetVertices(bakedVerts);
            var t = bodyMesh.transform;
            Vector3 p = t.position;
            Quaternion r = t.rotation;
            float low = float.MaxValue;
            foreach (int i in footVerts) low = Mathf.Min(low, (p + r * bakedVerts[i]).y);
            return low;
        }
    }
}
