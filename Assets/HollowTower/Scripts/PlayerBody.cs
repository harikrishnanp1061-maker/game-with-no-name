using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HollowTower
{
    /// <summary>
    /// Full first-person body: the SWAT operator model (Resources/Models/SWAT.glb) under the player.
    /// The head is collapsed so the camera can sit in it, both hands hold the MP5 with two-bone IK,
    /// the legs walk / run / crouch procedurally with foot IK, and the whole body casts shadows.
    /// </summary>
    public class PlayerBody : MonoBehaviour
    {
        const string ModelPath = "Models/SWAT";
        const float BodyScale = 0.975f;          // ~1.8 m tall
        const float NeckBehindCamera = 0.13f;     // how far the neck sits behind the eye
        const float ArmStretch = 1.3f;            // max arm stretch so hands reach the gun

        PlayerController owner;
        Transform root;
        readonly List<Renderer> renderers = new List<Renderer>();
        Transform shadowHead;

        Transform hips, spine, spine1, spine2, neck, head;
        Transform lShoulder, lArm, lFore, lHand, rShoulder, rArm, rFore, rHand;
        Transform lUpLeg, lLeg, lFoot, rUpLeg, rLeg, rFoot;
        readonly List<Transform> posed = new List<Transform>();
        readonly Dictionary<Transform, Quaternion> restRot = new Dictionary<Transform, Quaternion>();
        readonly Dictionary<Transform, Vector3> restPos = new Dictionary<Transform, Vector3>();

        // Grips learned from the model's own rifle pose, expressed in the gun's frame.
        Quaternion rGripRot, lGripRot;
        Vector3 rGripOfs, lGripOfs;

        // Feet / hands relative to the player when standing at rest.
        Vector3 lFootRest, rFootRest, lHandRelaxed, rHandRelaxed;
        Quaternion lFootRotRest, rFootRotRest, lHandRotRelaxed, rHandRotRelaxed;

        float walkPhase, moveBlend, crouchBlend, deathT, airBlend;
        Vector3 smoothVel;

        public bool Ready { get { return root != null; } }

        public static PlayerBody Create(PlayerController owner)
        {
            var prefab = Resources.Load<GameObject>(ModelPath);
            if (prefab == null)
            {
                Debug.LogWarning("Hollow Tower: player model not found at Resources/" + ModelPath + " - playing without a body.");
                return null;
            }
            var body = owner.gameObject.AddComponent<PlayerBody>();
            body.owner = owner;
            body.Build(prefab);
            return body.Ready ? body : null;
        }

        // ---------------- setup ----------------

        void Build(GameObject prefab)
        {
            root = new GameObject("Body").transform;
            root.SetParent(owner.transform, false);
            root.localScale = Vector3.one * BodyScale;
            var model = Instantiate(prefab, root, false); // keeps the glTF root's own axis conversion
            model.name = "SWAT";

            var bones = new Dictionary<string, Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name;
                if (!n.StartsWith("mixamorig:")) continue;
                n = n.Substring("mixamorig:".Length);
                int us = n.LastIndexOf('_');
                if (us > 0) n = n.Substring(0, us);
                if (!bones.ContainsKey(n)) bones[n] = t;
            }
            System.Func<string, Transform> B = k => { Transform t; bones.TryGetValue(k, out t); return t; };
            hips = B("Hips"); spine = B("Spine"); spine1 = B("Spine1"); spine2 = B("Spine2"); neck = B("Neck"); head = B("Head");
            lShoulder = B("LeftShoulder"); lArm = B("LeftArm"); lFore = B("LeftForeArm"); lHand = B("LeftHand");
            rShoulder = B("RightShoulder"); rArm = B("RightArm"); rFore = B("RightForeArm"); rHand = B("RightHand");
            lUpLeg = B("LeftUpLeg"); lLeg = B("LeftLeg"); lFoot = B("LeftFoot");
            rUpLeg = B("RightUpLeg"); rLeg = B("RightLeg"); rFoot = B("RightFoot");
            if (hips == null || neck == null || head == null || lArm == null || lFore == null || lHand == null || rArm == null || rFore == null ||
                rHand == null || lUpLeg == null || lLeg == null || lFoot == null || rUpLeg == null || rLeg == null || rFoot == null)
            {
                Debug.LogWarning("Hollow Tower: player model rig not recognised - playing without a body.");
                Destroy(root.gameObject);
                root = null;
                return;
            }
            if (spine2 == null) spine2 = spine1;

            foreach (var t in new[] { hips, spine, spine1, spine2, neck, head, lShoulder, lArm, lFore, lHand, rShoulder, rArm, rFore, rHand,
                                      lUpLeg, lLeg, lFoot, rUpLeg, rLeg, rFoot })
            {
                if (t == null || restRot.ContainsKey(t)) continue;
                posed.Add(t);
                restRot[t] = t.localRotation;
                restPos[t] = t.localPosition;
            }

            LearnGrips(model);

            // Rest data relative to the player.
            Transform p = owner.transform;
            lFootRest = p.InverseTransformPoint(lFoot.position);
            rFootRest = p.InverseTransformPoint(rFoot.position);
            lFootRotRest = Quaternion.Inverse(p.rotation) * lFoot.rotation;
            rFootRotRest = Quaternion.Inverse(p.rotation) * rFoot.rotation;
            lHandRelaxed = p.InverseTransformPoint(lArm.position) + new Vector3(-0.06f, -0.5f, 0.06f);
            rHandRelaxed = p.InverseTransformPoint(rArm.position) + new Vector3(0.06f, -0.5f, 0.06f);
            lHandRotRelaxed = Quaternion.Inverse(p.rotation) * lHand.rotation;
            rHandRotRelaxed = Quaternion.Inverse(p.rotation) * rHand.rotation;

            // Renderers: hide the model's own rifle and eyelashes; the body casts real shadows.
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                string mesh = MeshName(r);
                if (mesh.Contains("KSVR")) { r.gameObject.SetActive(false); continue; }
                if (mesh.Contains("Eyelashes")) { r.enabled = false; continue; }
                r.sharedMaterials = Gfx.FromGltf(r.sharedMaterials);
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
                var smr = r as SkinnedMeshRenderer;
                if (smr != null) smr.updateWhenOffscreen = true;
                renderers.Add(r);
            }
            Gfx.SetLayerRecursive(root.gameObject, Gfx.PlayerLayer);

            // The head shrinks to nothing so the camera sits inside it without seeing the face;
            // a shadow-only stand-in keeps the head in your shadow.
            head.localScale = Vector3.one * 0.001f;
            var proxy = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(proxy.GetComponent<Collider>());
            proxy.name = "HeadShadow";
            proxy.layer = Gfx.PlayerLayer;
            proxy.transform.SetParent(root, false);
            proxy.transform.localScale = new Vector3(0.24f, 0.28f, 0.27f);
            var pr = proxy.GetComponent<Renderer>();
            pr.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            pr.sharedMaterial = Gfx.Lit(Color.black);
            shadowHead = proxy.transform;
            renderers.Add(pr);
        }

        static string MeshName(Renderer r)
        {
            var smr = r as SkinnedMeshRenderer;
            if (smr != null && smr.sharedMesh != null) return smr.sharedMesh.name;
            var mf = r.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) return mf.sharedMesh.name;
            return r.name;
        }

        /// <summary>
        /// The model is posed holding its KRISS Vector. Record where each hand sits relative to that
        /// rifle (trigger for the right hand, fore-grip for the left) so the same grip can go on the MP5.
        /// </summary>
        void LearnGrips(GameObject model)
        {
            Bounds? trigger = null, front = null, barrel = null, stock = null, mag = null, top = null;
            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                string n = MeshName(r);
                if (!n.Contains("KSVR")) continue;
                if (n.StartsWith("trigger")) trigger = r.bounds;
                else if (n.StartsWith("frontGrip")) front = r.bounds;
                else if (n.StartsWith("barrel")) barrel = r.bounds;
                else if (n.StartsWith("stockBack")) stock = r.bounds;
                else if (n.StartsWith("mag")) mag = r.bounds;
                else if (n.StartsWith("topGun")) top = r.bounds;
            }

            Quaternion gunRot;
            Vector3 kTrigger, kFront;
            if (trigger.HasValue && front.HasValue && barrel.HasValue && stock.HasValue && mag.HasValue && top.HasValue)
            {
                Vector3 fwd = (barrel.Value.center - stock.Value.center).normalized;
                Vector3 up = top.Value.center - mag.Value.center;
                up = Vector3.ProjectOnPlane(up, fwd).normalized;
                gunRot = Quaternion.LookRotation(fwd, up);
                kTrigger = trigger.Value.center;
                kFront = front.Value.center;
            }
            else
            {
                // Fallback: treat the hands' own midpoint as the gun.
                gunRot = Quaternion.LookRotation(lHand.position - rHand.position, Vector3.up);
                kTrigger = rHand.position;
                kFront = lHand.position;
            }
            Quaternion inv = Quaternion.Inverse(gunRot);
            rGripRot = inv * rHand.rotation;
            lGripRot = inv * lHand.rotation;
            rGripOfs = inv * (rHand.position - kTrigger);
            lGripOfs = inv * (lHand.position - kFront);
        }

        // ---------------- per frame ----------------

        void LateUpdate()
        {
            if (root == null) return;
            var p = owner;

            bool hidden = p.IsHidden;
            // Dead: the death cam lies inside the body, so it only casts its shadow from then on.
            var shadowMode = p.IsDead ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            for (int i = 0; i < renderers.Count; i++)
            {
                var r = renderers[i];
                if (r == null) continue;
                r.enabled = !hidden;
                if (r.transform != shadowHead) r.shadowCastingMode = shadowMode;
            }
            if (hidden) return;

            // Back to the rest pose before layering procedural motion on top.
            for (int i = 0; i < posed.Count; i++)
            {
                posed[i].localRotation = restRot[posed[i]];
                posed[i].localPosition = restPos[posed[i]];
            }
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;

            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            Transform pt = p.transform;
            Vector3 up = pt.up, fwd = pt.forward, right = pt.right;

            // ---- inputs ----
            Vector3 vel = p.Velocity; vel.y = 0f;
            smoothVel = Vector3.Lerp(smoothVel, vel, dt * 10f);
            float speed = smoothVel.magnitude;
            moveBlend = Mathf.MoveTowards(moveBlend, Mathf.Clamp01(speed / 1.2f), dt * 5f);
            crouchBlend = Mathf.MoveTowards(crouchBlend, p.Crouch01, dt * 4f);
            airBlend = Mathf.MoveTowards(airBlend, p.Grounded ? 0f : 1f, dt * 6f);
            if (p.IsDead) deathT = Mathf.Min(1f, deathT + dt * 1.2f);

            float stride = p.IsSprinting ? 1.45f : (p.IsCrouching ? 0.8f : 1.05f);
            walkPhase += speed / stride * Mathf.PI * dt;

            Vector3 cam = p.Cam.transform.position;
            Vector3 camFwd = p.Cam.transform.forward;
            float pitch = -Mathf.Asin(Mathf.Clamp(camFwd.y, -1f, 1f)) * Mathf.Rad2Deg; // + = looking down

            // ---- hips & spine ----
            float crouch = Mathf.Max(crouchBlend, deathT);
            float hipDrop = 0.5f * crouch + 0.03f * moveBlend * (p.IsSprinting ? 1.5f : 1f) * Mathf.Abs(Mathf.Sin(walkPhase));
            hips.position -= up * hipDrop;
            // Lean into a crouch / sprint, and let the chest follow where you look.
            float lean = 32f * crouch + (p.IsSprinting ? 12f : 0f) * moveBlend;
            hips.rotation = Quaternion.AngleAxis(lean * 0.35f, right) * hips.rotation;
            if (spine != null) spine.rotation = Quaternion.AngleAxis(lean * 0.35f + pitch * 0.12f, right) * spine.rotation;
            if (spine1 != null && spine1 != spine) spine1.rotation = Quaternion.AngleAxis(lean * 0.3f + pitch * 0.14f, right) * spine1.rotation;
            if (spine2 != null && spine2 != spine1) spine2.rotation = Quaternion.AngleAxis(pitch * 0.14f, right) * spine2.rotation;
            // Twist the hips a little with the stride.
            hips.rotation = Quaternion.AngleAxis(Mathf.Sin(walkPhase) * 6f * moveBlend * (1f - crouch), up) * hips.rotation;

            // ---- put the neck just behind the eye ----
            if (!p.IsDead)
            {
                float back = NeckBehindCamera + 0.12f * Mathf.Clamp01(pitch / 70f);
                Vector3 want = cam - Vector3.ProjectOnPlane(camFwd, up).normalized * back;
                Vector3 shift = want - neck.position;
                shift = Vector3.ProjectOnPlane(shift, up);
                root.position += shift;
            }

            // ---- legs ----
            Vector3 moveDir = speed > 0.05f ? smoothVel / speed : fwd;
            float amp = moveBlend * (p.IsSprinting ? 0.42f : (p.IsCrouching ? 0.22f : 0.3f));
            float lift = moveBlend * (p.IsSprinting ? 0.2f : 0.12f);
            Vector3 spread = right * 0.06f * crouch;
            Leg(lUpLeg, lLeg, lFoot, pt.TransformPoint(lFootRest) - spread, pt.rotation * lFootRotRest, walkPhase, amp, lift, moveDir, fwd, up);
            Leg(rUpLeg, rLeg, rFoot, pt.TransformPoint(rFootRest) + spread, pt.rotation * rFootRotRest, walkPhase + Mathf.PI, amp, lift, moveDir, fwd, up);

            // ---- arms ----
            Transform gun = p.Gun != null ? p.Gun.Model : null;
            bool gunUp = gun != null && gun.gameObject.activeInHierarchy;
            Vector3 elbowOutR = right * 0.35f - up * 0.6f - fwd * 0.1f;
            Vector3 elbowOutL = -right * 0.35f - up * 0.6f - fwd * 0.1f;
            if (gunUp)
            {
                Quaternion g = gun.rotation;
                Vector3 rT = gun.TransformPoint(p.Gun.TriggerPoint) + g * rGripOfs * BodyScale;
                Vector3 lT = gun.TransformPoint(p.Gun.FrontGripPoint) + g * lGripOfs * BodyScale;
                Quaternion lRot = g * lGripRot;
                if (p.Gun.IsReloading)
                {
                    // Left hand drops to swap the magazine, then slaps it home.
                    float t = p.Gun.ReloadProgress;
                    float k = Mathf.Sin(Mathf.Clamp01(t * 1.15f) * Mathf.PI);
                    Vector3 magT = gun.TransformPoint(p.Gun.MagPoint) + g * lGripOfs * BodyScale;
                    lT = Vector3.Lerp(lT, magT - up * 0.18f * Mathf.Sin(Mathf.Clamp01(t * 1.6f) * Mathf.PI), k);
                }
                Arm(rShoulder, rArm, rFore, rHand, rT, g * rGripRot, rArm.position + elbowOutR);
                Arm(lShoulder, lArm, lFore, lHand, lT, lRot, lArm.position + elbowOutL);
            }
            else
            {
                Arm(rShoulder, rArm, rFore, rHand, pt.TransformPoint(rHandRelaxed) + up * hipDrop * -0.5f, pt.rotation * rHandRotRelaxed, rArm.position + elbowOutR);
                Arm(lShoulder, lArm, lFore, lHand, pt.TransformPoint(lHandRelaxed) + up * hipDrop * -0.5f, pt.rotation * lHandRotRelaxed, lArm.position + elbowOutL);
            }

            // ---- dying: the body slumps over sideways ----
            if (deathT > 0f)
            {
                float k = Mathf.SmoothStep(0f, 1f, deathT);
                Vector3 pivot = pt.position;
                Quaternion fall = Quaternion.AngleAxis(-80f * k, fwd);
                root.rotation = fall * root.rotation;
                root.position = pivot + fall * (root.position - pivot) + up * 0.12f * k;
            }

            shadowHead.position = head.position + up * 0.09f;
            shadowHead.rotation = neck.rotation;
        }

        void Leg(Transform upLeg, Transform leg, Transform foot, Vector3 restFoot, Quaternion footRot, float phase,
                 float amp, float lift, Vector3 moveDir, Vector3 fwd, Vector3 up)
        {
            float s = Mathf.Sin(phase), c = Mathf.Cos(phase);
            // Foot swings forward while lifted (c > 0), plants and slides back while on the ground.
            Vector3 target = restFoot + moveDir * (amp * s) + up * (lift * Mathf.Max(0f, c));
            target += up * 0.18f * airBlend;
            Quaternion rot = Quaternion.AngleAxis(-25f * Mathf.Max(0f, c) * Mathf.Clamp01(amp * 3f), Vector3.Cross(up, moveDir)) * footRot;
            Vector3 hint = leg.position + fwd * 0.6f;
            SolveTwoBone(upLeg, leg, foot, target, hint, 1f);
            foot.rotation = rot;
        }

        void Arm(Transform shoulder, Transform arm, Transform fore, Transform hand, Vector3 target, Quaternion handRot, Vector3 hint)
        {
            // Let the collarbone help a little when reaching forward.
            if (shoulder != null)
            {
                Quaternion reach = Quaternion.FromToRotation(arm.position - shoulder.position, target - shoulder.position);
                shoulder.rotation = Quaternion.Slerp(Quaternion.identity, reach, 0.25f) * shoulder.rotation;
            }
            SolveTwoBone(arm, fore, hand, target, hint, ArmStretch);
            hand.rotation = handRot;
        }

        void SolveTwoBone(Transform a, Transform b, Transform c, Vector3 target, Vector3 hint, float maxStretch)
        {
            float ab = Vector3.Distance(a.position, b.position);
            float bc = Vector3.Distance(b.position, c.position);
            Vector3 at = target - a.position;
            float dist = at.magnitude;
            if (dist < 1e-4f) return;
            float reach = (ab + bc) * 0.999f;
            if (maxStretch > 1f && dist > reach)
            {
                float k = Mathf.Min(dist / reach, maxStretch);
                b.localPosition = restPos[b] * k;
                c.localPosition = restPos[c] * k;
                ab *= k; bc *= k;
            }
            dist = Mathf.Clamp(dist, Mathf.Abs(ab - bc) + 1e-3f, ab + bc - 1e-3f);
            Vector3 dir = at.normalized;
            Vector3 pole = Vector3.ProjectOnPlane(hint - a.position, dir);
            if (pole.sqrMagnitude < 1e-6f) pole = Vector3.ProjectOnPlane(Vector3.down, dir);
            pole.Normalize();
            float cosA = Mathf.Clamp((ab * ab + dist * dist - bc * bc) / (2f * ab * dist), -1f, 1f);
            float sinA = Mathf.Sqrt(1f - cosA * cosA);
            Vector3 elbow = a.position + dir * (cosA * ab) + pole * (sinA * ab);
            a.rotation = Quaternion.FromToRotation(b.position - a.position, elbow - a.position) * a.rotation;
            b.rotation = Quaternion.FromToRotation(c.position - b.position, target - b.position) * b.rotation;
        }
    }
}
