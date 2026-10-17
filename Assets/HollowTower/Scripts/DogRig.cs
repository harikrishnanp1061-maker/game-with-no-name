using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HollowTower
{
    /// <summary>
    /// The zombie dog's body: "Silent Hill 1 Monsters - Groaner" by vinrax (Sketchfab, CC-BY-4.0),
    /// Resources/Models/Zombies/Dog/Dog.glb. A PS1-era rig (36 bones, 222 vertices) whose single 16.5 s
    /// clip holds every take back to back; each move below is a slice of it, sampled by hand every frame.
    /// </summary>
    public class DogRig
    {
        const string Path = "Models/Zombies/Dog/Dog";

        /// <summary>Slices of the one clip (seconds).</summary>
        struct Take { public float start, end; public bool loop; public Take(float s, float e, bool l) { start = s; end = e; loop = l; } }
        static readonly Take Idle = new Take(0.75f, 1.56f, true);
        static readonly Take Run = new Take(15.5f, 16.54f, true);
        static readonly Take Bite = new Take(8.75f, 10.0f, false);
        static readonly Take Howl = new Take(14.44f, 15.13f, false);
        static readonly Take Die = new Take(5.5f, 6.9f, false);
        /// <summary>The run take covers this many metres per loop at its own pace.</summary>
        const float RunStride = 2.6f;

        public Transform Head { get; private set; }
        public float Length { get; private set; }

        Transform owner, holder;
        Animation anim;
        AnimationState state;
        float runPhase, idlePhase, deadT = -1f;

        public static DogRig Create(Transform owner, float targetHeight)
        {
            var prefab = Resources.Load<GameObject>(Path);
            if (prefab == null) return null;
            var rig = new DogRig();
            if (!rig.Build(owner, prefab, targetHeight))
            {
                if (rig.holder != null) Object.Destroy(rig.holder.gameObject);
                return null;
            }
            return rig;
        }

        bool Build(Transform owner, GameObject prefab, float targetHeight)
        {
            this.owner = owner;
            holder = new GameObject("DogModel").transform;
            holder.SetParent(owner, false);
            var model = Object.Instantiate(prefab, holder, false);
            model.name = prefab.name;

            anim = model.GetComponentInChildren<Animation>();
            AnimationClip clip = null;
            if (anim != null)
            {
                clip = anim.clip;
                if (clip == null) foreach (AnimationState s in anim) { clip = s.clip; break; }
            }
            foreach (var a in model.GetComponentsInChildren<Animator>(true)) Object.Destroy(a);
            if (clip == null)
            {
                Debug.LogWarning("Hollow Tower: the dog model has no animation clip.");
                return false;
            }
            anim.Stop();
            anim.playAutomatically = false;
            anim.cullingType = AnimationCullingType.AlwaysAnimate;
            state = anim[clip.name];
            if (state == null) { anim.AddClip(clip, clip.name); state = anim[clip.name]; }

            Transform root = null, neck = null;
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith("bone00")) root = t;
                else if (t.name.StartsWith("bone03")) neck = t;
                else if (t.name.StartsWith("bone04")) Head = t;
            }
            if (root == null || neck == null || Head == null)
            {
                Debug.LogWarning("Hollow Tower: dog rig not recognised.");
                return false;
            }

            foreach (var r in model.GetComponentsInChildren<Renderer>(true))
            {
                r.sharedMaterials = Gfx.FromGltf(r.sharedMaterials);
                r.shadowCastingMode = ShadowCastingMode.Off; // 222 vertices: its shadow would be a blob anyway
                r.rayTracingMode = UnityEngine.Experimental.Rendering.RayTracingMode.Off;
                var smr = r as SkinnedMeshRenderer;
                if (smr != null) smr.updateWhenOffscreen = false;
                // The smoothed, re-textured version (Editor/DogImport.cs): rounder body, 1024 px skin with a bump map.
                if (smr != null && HQMesh != null && smr.sharedMesh != null && HQMesh.bindposes.Length == smr.sharedMesh.bindposes.Length)
                {
                    smr.sharedMesh = HQMesh;
                    smr.sharedMaterial = HQMaterial;
                    smr.shadowCastingMode = ShadowCastingMode.On;
                }
            }

            // Stand in the idle pose, face +Z (the neck is ahead of the hips), fit the height, feet on the floor.
            Sample(Idle.start);
            Vector3 f = Vector3.ProjectOnPlane(neck.position - root.position, owner.up);
            if (f.sqrMagnitude > 1e-8f) holder.rotation = Quaternion.FromToRotation(f.normalized, owner.forward) * holder.rotation;
            Bounds b;
            if (!PoseBounds(model, out b)) return false;
            holder.localScale *= targetHeight * owner.lossyScale.y / Mathf.Max(1e-4f, b.size.y);
            PoseBounds(model, out b);
            holder.position += owner.up * (owner.position.y - b.min.y);
            holder.position -= Vector3.ProjectOnPlane(b.center - owner.position, owner.up);
            PoseBounds(model, out b);
            Length = Mathf.Max(b.size.x, b.size.z);

            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var lb = smr.localBounds;
                lb.Expand(lb.size.magnitude * 0.5f);
                smr.localBounds = lb;
            }
            idlePhase = Random.value * 10f;
            runPhase = Random.value;
            return true;
        }

        static Mesh hqMesh;
        static Material hqMat;
        static bool hqLoaded;

        static Mesh HQMesh
        {
            get
            {
                if (!hqLoaded) { hqLoaded = true; hqMesh = Resources.Load<Mesh>(Path + "_HQ"); }
                return hqMesh;
            }
        }

        static Material HQMaterial
        {
            get
            {
                if (hqMat != null) return hqMat;
                hqMat = Gfx.Lit(Color.white, 0.42f, 0f, Resources.Load<Texture2D>(Path + "_BaseColor")); // wet, raw skin
                var n = Resources.Load<Texture2D>(Path + "_Normal");
                if (n != null) { hqMat.SetTexture("_NormalMap", n); hqMat.SetFloat("_NormalScale", 1.2f); }
                hqMat.SetFloat("_DoubleSidedEnable", 1f); // the PS1 body is open box pieces, drawn from both sides
                UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(hqMat);
                return hqMat;
            }
        }

        /// <summary>World bounds of the skinned mesh as currently posed (BakeMesh: the model is tiny).</summary>
        static bool PoseBounds(GameObject model, out Bounds b)
        {
            b = new Bounds();
            bool any = false;
            var mesh = new Mesh();
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.BakeMesh(mesh, true);
                var m = smr.transform.localToWorldMatrix;
                foreach (var v in mesh.vertices)
                {
                    Vector3 w = m.MultiplyPoint3x4(v);
                    if (!any) { b = new Bounds(w, Vector3.zero); any = true; } else b.Encapsulate(w);
                }
            }
            Object.Destroy(mesh);
            return any;
        }

        void Sample(float t)
        {
            state.enabled = true;
            state.weight = 1f;
            state.time = t;
            anim.Sample();
            state.enabled = false;
        }

        static float At(Take k, float u)
        {
            float len = k.end - k.start;
            u = k.loop ? Mathf.Repeat(u, 1f) : Mathf.Clamp01(u);
            return k.start + u * len;
        }

        /// <summary>Screenshot helper: show the raw clip at time t.</summary>
        public void Raw(float t) { Sample(t); }

        /// <summary>
        /// Call from LateUpdate. speed m/s; bite 0..1 = the lunge (wind-up through snap); howl 0..1 = rearing to bark / howl.
        /// </summary>
        public void Pose(float dt, float speed, float bite, float howl)
        {
            if (deadT >= 0f) return;
            if (bite > 0f) { Sample(At(Bite, bite)); return; }
            if (howl > 0f) { Sample(At(Howl, Mathf.Sin(howl * Mathf.PI) * 0.999f)); return; }
            if (speed > 0.4f)
            {
                runPhase += dt * speed / RunStride;
                Sample(At(Run, runPhase));
                return;
            }
            idlePhase += dt / (Idle.end - Idle.start) * 0.6f;
            // The idle take isn't a closed loop: ping-pong it.
            float u = Mathf.PingPong(idlePhase, 1f);
            Sample(At(Idle, u));
        }

        /// <summary>Plays the collapse; call every frame once dead.</summary>
        public void Dying(float dt)
        {
            if (deadT < 0f) deadT = 0f;
            deadT += dt;
            Sample(At(Die, deadT / (Die.end - Die.start)));
        }
    }
}
