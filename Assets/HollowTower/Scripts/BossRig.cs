using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace HollowTower
{
    /// <summary>
    /// The brute's real body: one of two boss models, animated procedurally (run, menace, overhead smash).
    ///  0 - "monster juggernaut" by sanyaork (Sketchfab, CC-BY-4.0): Resources/Models/Bosses/Juggernaut.glb, no clips.
    ///  1 - "boss 2": Resources/Models/Bosses/Boss2/Boss2.fbx, a Mixamo rig with a breathing idle clip.
    /// Neither ships a run or attack clip, so the legs, arms and spine are driven here on top of the
    /// rest pose (or the idle clip), rotating bones about the zombie's own axes so either rig works.
    /// The chasers use the same rig:
    ///  2 - "scary zombie" (Mixamo): Resources/Models/Zombies/Scary, a walker.
    ///  3 - "Zombie Hunter" parasite zombie (Mixamo): Resources/Models/Zombies/Parasite, a walker.
    ///  4 - "the human deer" (Maya rig): Resources/Models/Zombies/Deer, the runner - hunched, arms to the floor.
    /// </summary>
    public class BossRig
    {
        public const int Variants = 5, BossVariants = 2;
        public const int Scary = 2, Parasite = 3, Deer = 4;
        static readonly string[] Paths = { "Models/Bosses/Juggernaut", "Models/Bosses/Boss2/Boss2",
            "Models/Zombies/Scary/Scary", "Models/Zombies/Parasite/Parasite", "Models/Zombies/Deer/Deer" };
        const string Boss2Tex = "Models/Bosses/Boss2/Boss2_";
        bool IsBoss { get { return Variant < BossVariants; } }

        public Transform Head { get; private set; }
        public int Variant { get; private set; }

        Transform owner, holder;
        Transform hips, neck, lThigh, lShin, rThigh, rShin, lUpper, lFore, rUpper, rFore;
        readonly List<Transform> spine = new List<Transform>();
        readonly List<Transform> posed = new List<Transform>();
        readonly Dictionary<Transform, Quaternion> restRot = new Dictionary<Transform, Quaternion>();
        readonly Dictionary<Transform, Vector3> restPos = new Dictionary<Transform, Vector3>();
        Animation anim;
        float height, phase, walkBlend, runBlend;

        // The juggernaut's hands and feet hang off IK bones, not off the arm / leg chain: they follow their limb.
        struct Follow { public Transform t, leader; public Vector3 pos; public Quaternion rot; }
        readonly List<Follow> follows = new List<Follow>();

        public static BossRig Create(Transform owner, int variant, float targetHeight)
        {
            if (variant < 0 || variant >= Variants) variant = ((variant % BossVariants) + BossVariants) % BossVariants;
            var prefab = Resources.Load<GameObject>(Paths[variant]);
            if (prefab == null && variant < BossVariants) { variant = 1 - variant; prefab = Resources.Load<GameObject>(Paths[variant]); }
            if (prefab == null) return null;
            var rig = new BossRig();
            if (!rig.Build(owner, prefab, variant, targetHeight))
            {
                if (rig.holder != null) Object.Destroy(rig.holder.gameObject);
                return null;
            }
            return rig;
        }

        bool Build(Transform owner, GameObject prefab, int variant, float targetHeight)
        {
            this.owner = owner;
            Variant = variant;
            holder = new GameObject("BossModel").transform;
            holder.SetParent(owner, false);
            var model = Object.Instantiate(prefab, holder, false);
            model.name = prefab.name;

            // Only boss 2's breathing idle is worth playing: the scary zombie's clip is an agonized writhe,
            // the parasite's a single frame and the deer's a showcase take.
            anim = variant == 1 ? model.GetComponentInChildren<Animation>() : null;
            if (variant >= Scary) ResetToBindPose(model);
            if (variant == Deer) TidyDeer(model);
            if (anim != null && anim.clip != null)
            {
                anim.wrapMode = WrapMode.Loop;
                anim.cullingType = AnimationCullingType.AlwaysAnimate;
                anim.Play();
                anim[anim.clip.name].time = Random.value * anim.clip.length;
                anim.Sample();
            }
            else anim = null;
            if (Benchmark.BossShots)
                Debug.Log("BossRig " + prefab.name + " anim=" + (anim != null) + " animations=" + model.GetComponentsInChildren<Animation>(true).Length +
                          " animators=" + model.GetComponentsInChildren<Animator>(true).Length);
            // Anything that would animate the bones besides us (and the clip we play) goes.
            foreach (var a in model.GetComponentsInChildren<Animation>(true)) if (a != anim) { a.enabled = false; Object.Destroy(a); }
            foreach (var a in model.GetComponentsInChildren<Animator>(true)) Object.Destroy(a);

            // Bone names: "mixamorig:LeftArm" or "arm1.L_058" -> "LeftArm" / "arm1.L".
            var exact = new Dictionary<string, Transform>();
            var loose = new Dictionary<string, Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                int colon = n.IndexOf(':');
                if (colon >= 0) n = n.Substring(colon + 1);
                if (!exact.ContainsKey(n)) exact[n] = t;
                int us = n.LastIndexOf('_');
                if (us > 0)
                {
                    string s = n.Substring(0, us);
                    if (!loose.ContainsKey(s)) loose[s] = t;
                }
            }
            System.Func<string[], Transform> B = names =>
            {
                Transform t;
                foreach (var k in names) if (exact.TryGetValue(k, out t)) return t;
                foreach (var k in names) if (loose.TryGetValue(k, out t)) return t;
                return null;
            };

            // Mixamo / juggernaut / deer ("C_Hip_J", "L_upper_leg_J" ...) names.
            hips = B(new[] { "Hips", "spine1", "C_Hip_J" });
            foreach (var k in new[] { "Spine", "Spine1", "Spine2", "spine2", "spine3", "spine4", "C_spine_01_J", "C_spine_02_J", "C_spine_03_J", "C_spine_04_J" })
            {
                var t = B(new[] { k });
                if (t != null && t != hips && !spine.Contains(t)) spine.Add(t);
            }
            neck = B(new[] { "Neck", "neck", "C_Neck_J" });
            Head = B(new[] { "Head", "head", "C_Head_J" });
            lThigh = B(new[] { "LeftUpLeg", "leg1.L", "L_upper_leg_J" }); lShin = B(new[] { "LeftLeg", "leg2.L", "L_knee_J" });
            rThigh = B(new[] { "RightUpLeg", "leg1.R", "R_upper_leg_J" }); rShin = B(new[] { "RightLeg", "leg2.R", "R_knee_J" });
            lUpper = B(new[] { "LeftArm", "arm1.L", "L_shoulder_J" }); lFore = B(new[] { "LeftForeArm", "arm2.L", "L_elbow_J" });
            rUpper = B(new[] { "RightArm", "arm1.R", "R_shoulder_J" }); rFore = B(new[] { "RightForeArm", "arm2.R", "R_elbow_J" });
            Transform lFoot = B(new[] { "LeftFoot", "leg3.L", "L_feet_J" }), rFoot = B(new[] { "RightFoot", "leg3.R", "R_feet_J" });
            Transform lHand = B(new[] { "LeftHand", "arm3.L", "L_wrist_J" }), rHand = B(new[] { "RightHand", "arm3.R", "R_wrist_J" });
            if (hips == null || Head == null || lThigh == null || lShin == null || rThigh == null || rShin == null ||
                lUpper == null || lFore == null || rUpper == null || rFore == null || lFoot == null || rFoot == null)
            {
                Debug.LogWarning("Hollow Tower: boss rig " + prefab.name + " not recognised - using the block brute.");
                return false;
            }

            if (Benchmark.BossShots)
            {
                string sk = "";
                foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    sk += " [" + smr.name + " bones=" + smr.bones.Length + " hasThigh=" + System.Array.IndexOf(smr.bones, lThigh) + " hasArm=" + System.Array.IndexOf(smr.bones, lUpper) + "]";
                Debug.Log("BossRig " + prefab.name + " hips=" + hips.name + " thigh=" + lThigh.name + " arm=" + lUpper.name + " path=" + lThigh.parent.name + sk);
            }

            // IK-driven hands / feet follow the end of their limb.
            AddFollow(B(new[] { "arm_IK.L" }), lFore);
            AddFollow(B(new[] { "arm_IK.R" }), rFore);
            AddFollow(B(new[] { "leg_IK.L" }), lFoot);
            AddFollow(B(new[] { "leg_IK.R" }), rFoot);

            // Materials.
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterials = Materials(variant, r);
                r.shadowCastingMode = ShadowCastingMode.On;
                var smr = r as SkinnedMeshRenderer;
                if (smr != null) smr.updateWhenOffscreen = true; // true bounds while fitting (chasers switch off below)
                if (!IsBoss)
                {
                    // Dozens of skinned chasers would mean rebuilding their ray-tracing geometry every frame;
                    // and the deer's teeth, eyes and fingers are too small to throw a visible shadow.
                    r.rayTracingMode = UnityEngine.Experimental.Rendering.RayTracingMode.Off;
                    var mf = r.GetComponent<MeshFilter>();
                    var mesh = smr != null ? smr.sharedMesh : mf != null ? mf.sharedMesh : null;
                    if (mesh != null && mesh.vertexCount < 1500) r.shadowCastingMode = ShadowCastingMode.Off;
                }
            }

            // Face +Z: the mouth is in front of the head, the toes in front of the feet (the deer: head ahead of the hips).
            Transform faceFrom = Head, faceTo = B(new[] { "uppermouth", "lowermouth" });
            if (variant == Deer) { faceFrom = hips; faceTo = Head; }
            if (faceTo == null) { faceFrom = lFoot; faceTo = B(new[] { "LeftToeBase", "LeftToe_End" }); }
            if (faceTo != null)
            {
                Vector3 f = Vector3.ProjectOnPlane(faceTo.position - faceFrom.position, owner.up);
                if (f.sqrMagnitude > 1e-8f)
                    holder.rotation = Quaternion.FromToRotation(f.normalized, owner.forward) * holder.rotation;
            }

            // Fit the height and stand on the floor.
            Bounds b;
            if (!WorldBounds(model, out b)) return false;
            if (Benchmark.BossShots)
            {
                Vector3 bmin = Vector3.one * 1e9f, bmax = -bmin;
                foreach (var t in model.GetComponentsInChildren<Transform>()) { bmin = Vector3.Min(bmin, t.position); bmax = Vector3.Max(bmax, t.position); }
                string rs = "";
                foreach (var r in model.GetComponentsInChildren<Renderer>()) rs += " [" + r.name + " " + r.bounds.center.ToString("F2") + " " + r.bounds.size.ToString("F2") + "]";
                Debug.Log("BossRig " + prefab.name + " owner=" + owner.position.ToString("F2") + " bounds=" + b.center.ToString("F2") + " size=" + b.size.ToString("F2") +
                          " bones " + bmin.ToString("F2") + ".." + bmax.ToString("F2") + " head=" + Head.position.ToString("F2") + " lfoot=" + lFoot.position.ToString("F2") +
                          " larm=" + (lFore.position - lUpper.position).ToString("F2") + rs);
            }
            float h = b.size.y / Mathf.Max(1e-4f, owner.lossyScale.y);
            holder.localScale *= targetHeight / Mathf.Max(1e-4f, h);
            WorldBounds(model, out b);
            holder.position += owner.up * (owner.position.y - b.min.y);
            Vector3 c = Vector3.ProjectOnPlane(b.center - owner.position, owner.up);
            holder.position -= c;
            height = targetHeight * owner.lossyScale.y;
            if (Benchmark.BossShots)
            {
                WorldBounds(model, out b);
                Debug.Log("BossRig fitted " + prefab.name + " scale=" + holder.localScale.ToString("F4") + " bounds=" + b.center.ToString("F2") + " size=" + b.size.ToString("F2") + " head=" + Head.position.ToString("F2"));
            }

            // Without a clip the arms may be in a T-pose: let them hang, ready to swing.
            if (anim == null)
            {
                HangArm(lUpper, lFore, lHand);
                HangArm(rUpper, rFore, rHand);
                StandLeg(lThigh, lFoot);
                StandLeg(rThigh, rFoot);
                ApplyFollows();
                // Plant the feet on the floor.
                float foot = Mathf.Min(lFoot.position.y, rFoot.position.y);
                WorldBounds(model, out b);
                holder.position += owner.up * (owner.position.y - Mathf.Max(b.min.y, foot - 0.12f * height));
                if (Benchmark.BossShots) Debug.Log("BossRig planted " + prefab.name + " foot=" + (foot - owner.position.y).ToString("F2") + " min=" + (b.min.y - owner.position.y).ToString("F2"));
            }

            foreach (var t in new[] { hips, neck, Head, lThigh, lShin, rThigh, rShin, lUpper, lFore, rUpper, rFore })
                AddPosed(t);
            // Bosses are few; a floor full of chasers can't all skin off-screen, so they cull on padded bounds.
            if (!IsBoss)
                foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    smr.updateWhenOffscreen = false;
                    var lb = smr.localBounds;
                    lb.Expand(lb.size.magnitude * 0.5f);
                    smr.localBounds = lb;
                }

            foreach (var t in spine) AddPosed(t);
            foreach (var f in follows) AddPosed(f.t);
            phase = Random.value * 10f;
            return true;
        }

        void AddPosed(Transform t)
        {
            if (t == null || restRot.ContainsKey(t)) return;
            posed.Add(t);
            restRot[t] = t.localRotation;
            restPos[t] = t.localPosition;
        }

        void AddFollow(Transform t, Transform leader)
        {
            if (t == null || leader == null) return;
            follows.Add(new Follow { t = t, leader = leader, pos = leader.InverseTransformPoint(t.position), rot = Quaternion.Inverse(leader.rotation) * t.rotation });
        }

        void ApplyFollows()
        {
            foreach (var f in follows)
            {
                f.t.position = f.leader.TransformPoint(f.pos);
                f.t.rotation = f.leader.rotation * f.rot;
            }
        }

        void HangArm(Transform upper, Transform fore, Transform hand)
        {
            Vector3 R = owner.right, U = owner.up, F = owner.forward;
            Vector3 dir = fore.position - upper.position;
            float side = Mathf.Sign(Vector3.Dot(dir, R));
            Vector3 want = (side * 0.32f * R - 0.92f * U + 0.12f * F).normalized;
            if (Vector3.Angle(dir, want) > 25f) upper.rotation = Quaternion.FromToRotation(dir, want) * upper.rotation;
            Vector3 end = hand != null ? hand.position : fore.position + fore.rotation * Vector3.up * dir.magnitude;
            Vector3 d2 = end - fore.position;
            Vector3 want2 = (want + 0.35f * F).normalized;
            if (hand != null && Vector3.Angle(d2, want2) > 25f) fore.rotation = Quaternion.FromToRotation(d2, want2) * fore.rotation;
        }

        // Swing the whole leg (keeping the knee) so the foot sits under the hip.
        void StandLeg(Transform thigh, Transform foot)
        {
            Vector3 d = foot.position - thigh.position;
            Vector3 want = -owner.up * d.magnitude + owner.forward * 0.05f * d.magnitude;
            if (Vector3.Angle(d, want) > 10f) thigh.rotation = Quaternion.FromToRotation(d, want) * thigh.rotation;
        }

        bool WorldBounds(GameObject model, out Bounds b)
        {
            // Only meshes around the hips count: the juggernaut's eye / teeth meshes report bounds
            // in their source units, far from where they're actually skinned.
            b = new Bounds();
            bool any = false;
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                var rb = r.bounds;
                rb.Expand(rb.size.magnitude * 0.05f);
                if (!rb.Contains(hips.position)) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (any) return true;
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            return any;
        }

        static readonly Dictionary<string, Material> texMats = new Dictionary<string, Material>();

        /// <summary>An HDRP Lit material from "prefix" + BaseColor / Normal / Emissive textures (cached).</summary>
        static Material TexMat(string prefix, float smoothness, Color emission)
        {
            Material m;
            if (texMats.TryGetValue(prefix, out m)) return m;
            m = Gfx.Lit(Color.white, smoothness, 0f, Resources.Load<Texture2D>(prefix + "BaseColor"));
            var n = Resources.Load<Texture2D>(prefix + "Normal");
            if (n != null) { m.SetTexture("_NormalMap", n); m.SetFloat("_NormalScale", 1f); }
            var e = Resources.Load<Texture2D>(prefix + "Emissive");
            if (e != null)
            {
                m.SetTexture("_EmissiveColorMap", e);
                Gfx.SetEmission(m, emission);
            }
            HDMaterial.ValidateMaterial(m);
            texMats[prefix] = m;
            return m;
        }

        static Material[] Materials(int variant, Renderer r)
        {
            int count = Mathf.Max(1, r.sharedMaterials.Length);
            if (variant == 0) return Gfx.FromGltf(r.sharedMaterials);
            var a = new Material[count];
            for (int i = 0; i < count; i++)
            {
                Material m;
                switch (variant)
                {
                    case 1: m = TexMat(Boss2Tex, 0.32f, new Color(0.08f, 0.08f, 0.08f)); break;
                    case Scary: m = TexMat("Models/Zombies/Scary/Scary_", 0.3f, Color.black); break;
                    case Parasite: m = TexMat(i == 0 ? "Models/Zombies/Parasite/Parasite_" : "Models/Zombies/Parasite/ParasiteBody_", 0.3f, Color.black); break;
                    default:
                        // Body and bones share one texture set; the head, antlers, mouths and hands the other.
                        bool body = r.name.Contains("Body") || r.name.Contains("bone");
                        m = TexMat(body ? "Models/Zombies/Deer/DeerBody_" : "Models/Zombies/Deer/DeerHead_", 0.35f, new Color(0.25f, 0.25f, 0.25f));
                        break;
                }
                a[i] = m;
            }
            return a;
        }

        /// <summary>Mixamo exports sit in their clip's first frame: put every bone back into the T-pose it was skinned in.</summary>
        static void ResetToBindPose(GameObject model)
        {
            SkinnedMeshRenderer best = null;
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr.sharedMesh != null && (best == null || smr.bones.Length > best.bones.Length)) best = smr;
            if (best == null) return;
            var bones = best.bones;
            var bind = best.sharedMesh.bindposes;
            if (bind.Length != bones.Length) return;
            Matrix4x4 root = best.transform.localToWorldMatrix;
            var order = new List<int>();
            for (int i = 0; i < bones.Length; i++) if (bones[i] != null) order.Add(i);
            order.Sort((x, y) => Depth(bones[x]).CompareTo(Depth(bones[y])));
            foreach (int i in order)
            {
                Matrix4x4 w = root * bind[i].inverse;
                bones[i].SetPositionAndRotation(w.GetColumn(3), w.rotation);
            }
        }

        static int Depth(Transform t) { int d = 0; while (t.parent != null) { t = t.parent; d++; } return d; }

        /// <summary>
        /// The deer ships ~60 meshes: drop the alpha hair cards (they'd render as solid planes), hang the
        /// one loose leg-bone mesh on its knee, and turn the single-bone skinned meshes (antlers, teeth,
        /// hands, bones) into plain meshes riding their bone, so a floor full of deer stays cheap to skin.
        /// </summary>
        static void TidyDeer(GameObject model)
        {
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
                if (t != null && t.name.StartsWith("pPlane")) Object.DestroyImmediate(t.gameObject);
            Transform knee = null;
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) if (t.name == "R_knee_J") knee = t;
            foreach (var mr in model.GetComponentsInChildren<MeshRenderer>(true))
                if (knee != null && mr.name.StartsWith("R_leg_exposed")) mr.transform.SetParent(knee, true);
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = smr.sharedMesh;
                if (mesh == null || smr.bones.Length != 1 || smr.bones[0] == null || mesh.bindposes.Length != 1) continue;
                Matrix4x4 bp = mesh.bindposes[0];
                var go = new GameObject(smr.name);
                go.transform.SetParent(smr.bones[0], false);
                go.transform.localPosition = bp.GetColumn(3);
                go.transform.localRotation = bp.rotation;
                go.transform.localScale = bp.lossyScale;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials = smr.sharedMaterials;
                Object.DestroyImmediate(smr.gameObject.GetComponentsInChildren<Transform>(true).Length > 1 ? (Object)smr : smr.gameObject);
            }
        }

        public void Stop() { if (anim != null) anim.Stop(); }

        /// <summary>
        /// Call from LateUpdate. speed in m/s; raise 0..1 = arms going up for the smash; slam 0..1 = the smash
        /// landing (1 on impact, fading); flinch 0..1 = just got shot.
        /// </summary>
        public void Pose(float dt, float speed, bool chasing, float raise, float slam, float flinch)
        {
            if (anim == null)
                for (int i = 0; i < posed.Count; i++) { posed[i].localRotation = restRot[posed[i]]; posed[i].localPosition = restPos[posed[i]]; }

            Vector3 R = owner.right, U = owner.up;
            walkBlend = Mathf.MoveTowards(walkBlend, Mathf.Clamp01(speed / 1.6f), dt * 4f);
            runBlend = Mathf.MoveTowards(runBlend, Mathf.Clamp01((speed - 2f) / 3.5f), dt * 3f);
            float stride = height * (0.42f + 0.28f * runBlend);
            phase += dt * (speed / Mathf.Max(0.3f, stride) * Mathf.PI + 1.2f * (1f - walkBlend));
            float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
            float attack = Mathf.Max(raise, slam);
            float idle = Time.time * 1.7f;

            // Hips: heavy bob and stomp, twist with the stride, drop into the smash.
            hips.position += U * (height * (Mathf.Abs(s) * (0.018f * walkBlend + 0.03f * runBlend) - 0.035f * runBlend - 0.07f * slam));
            hips.rotation = Quaternion.AngleAxis(s * (6f * walkBlend + 4f * runBlend) * (1f - attack), U) * hips.rotation;

            // Spine: hunched when hunting, leaning hard into a charge, rearing back then crunching on the smash.
            float hunch = Variant == 0 || Variant == Deer ? 0.3f : 1f; // the juggernaut and the deer are already bent double
            float lean = hunch * ((chasing ? 12f : 5f) + 20f * runBlend + 40f * slam) - 22f * raise - 12f * flinch + Mathf.Sin(idle) * 1.5f;
            float twist = -s * 8f * runBlend * (1f - attack);
            if (spine.Count > 0)
                foreach (var t in spine)
                    t.rotation = Quaternion.AngleAxis(twist / spine.Count, U) * Quaternion.AngleAxis(lean / spine.Count, R) * t.rotation;
            if (neck != null) neck.rotation = Quaternion.AngleAxis(-lean * 0.35f + Mathf.Sin(idle * 0.6f) * 4f, R) * neck.rotation;

            // Legs: big strides, knees snapping up when running.
            float legAmp = (22f * walkBlend + 26f * runBlend) * (Variant == 0 ? 0.6f : Variant == Deer ? 0.8f : 1f); // stubby juggernaut legs
            Leg(lThigh, lShin, s, c, legAmp, R);
            Leg(rThigh, rShin, -s, -c, legAmp, R);

            // Arms: pump against the legs; reach out when hunting; overhead, then slammed down, on attack.
            float armAmp = (18f * walkBlend + 30f * runBlend) * (1f - attack);
            float reach = chasing ? -25f * (1f - runBlend) : 0f;
            float up = -165f * raise - 70f * slam;
            Arm(lUpper, lFore, s * armAmp + reach + up, raise, slam, R);
            Arm(rUpper, rFore, -s * armAmp + reach + up, raise, slam, R);

            ApplyFollows();
        }

        static void Leg(Transform thigh, Transform shin, float s, float c, float amp, Vector3 R)
        {
            thigh.rotation = Quaternion.AngleAxis(-s * amp, R) * thigh.rotation;
            float bend = amp * (0.25f + 1.2f * Mathf.Max(0f, c));
            shin.rotation = Quaternion.AngleAxis(bend, R) * shin.rotation;
        }

        static void Arm(Transform upper, Transform fore, float swing, float raise, float slam, Vector3 R)
        {
            upper.rotation = Quaternion.AngleAxis(swing, R) * upper.rotation;
            fore.rotation = Quaternion.AngleAxis(-25f - 35f * raise + 20f * slam, R) * fore.rotation;
        }
    }
}
