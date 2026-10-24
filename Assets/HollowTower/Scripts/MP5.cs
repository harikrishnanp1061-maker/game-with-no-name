using System.Collections.Generic;
using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// The player's guns (the class keeps its old MP5 name). The rifle is an M4A1: full-auto, 30-round mags,
    /// every shot LOUD - it pulls in every zombie within about 30 metres. The sidearm (X swaps) is a 9 mm pistol:
    /// semi-auto, 12 rounds, harder-hitting per shot and quieter (heard about 20 m away).
    /// </summary>
    public class MP5 : MonoBehaviour
    {
        public const int MagSize = 30;
        const string M4A1Path = "Models/M4A1/M4A1";
        const float M4A1Scale = 4.4f;
        /// <summary>Where the hands go, in the gun model's frame: right hand on the trigger, left on the handguard.</summary>
        public Vector3 TriggerPoint { get; private set; }
        public Vector3 FrontGripPoint { get; private set; }
        /// <summary>Bottom of the magazine (the left hand goes here on reloads) and the weapon light mount.</summary>
        public Vector3 MagPoint { get; private set; }
        public Vector3 LightPoint { get; private set; }
        /// <summary>Used by the -gunshots picture run to aim / reload without input.</summary>
        public static bool ForceAim, ForceReload;
        public int Ammo { get; private set; }
        public bool IsPistol { get; private set; }
        /// <summary>Rounds per magazine (30 for the rifle, 12 for the pistol).</summary>
        public int Capacity { get { return IsPistol ? PistolMag : MagSize; } }
        public string DisplayName { get { return IsPistol ? "9mm PISTOL" : "M4A1"; } }
        public const int PistolMag = 12;
        public int Reserve { get; private set; }
        public bool IsReloading { get; private set; }
        public bool IsAiming { get; private set; }
        public bool FiredRecently { get { return Time.time - lastShot < 1.5f; } }
        /// <summary>The visible gun (hands and the weapon light attach to it).</summary>
        public Transform Model { get { return model; } }
        /// <summary>0..1 through the current reload.</summary>
        public float ReloadProgress { get { return IsReloading ? Mathf.Clamp01(1f - (reloadEnd - Time.time) / reloadDuration) : 0f; } }

        const float Range = 120f;
        float FireInterval { get { return IsPistol ? 0.15f : 60f / 800f; } }
        float Damage { get { return IsPistol ? 30f : 22f; } }
        float ReloadTime { get { return IsPistol ? 1.0f : 1.1f; } }      // manual reload (R)
        float FastReloadTime { get { return IsPistol ? 0.8f : 0.7f; } }  // automatic reload the moment the mag runs dry
        float NoiseRadius { get { return IsPistol ? 20f : 30f; } }
        const string PistolPath = "Models/Pistol/Pistol";

        PlayerController owner;
        Camera cam;
        Transform model, mag, muzzle;
        Light flashLight;
        Renderer flashRenderer;
        AudioSource audioSrc;
        LineRenderer tracer;
        Vector3 hipPos = new Vector3(0.17f, -0.2f, 0.34f);
        Vector3 aimPos = new Vector3(0f, -0.066f, 0.31f);
        Vector3 magHome;
        float nextFire, lastShot = -10f, reloadEnd, reloadDuration = 1.1f, kick, flashEnd, tracerEnd;
        Vector2 sway;
        bool triggerWasDown;
        static readonly Queue<GameObject> holes = new Queue<GameObject>();
        static Material holeMat, sparkMat, bloodMat;

        public static MP5 Create(Transform camTransform, PlayerController owner, bool pistol = false)
        {
            var root = new GameObject(pistol ? "Pistol" : "MP5");
            root.transform.SetParent(camTransform, false);
            var gun = root.AddComponent<MP5>();
            gun.IsPistol = pistol;
            gun.owner = owner;
            gun.cam = camTransform.GetComponent<Camera>();
            gun.Ammo = gun.Capacity;
            // Block-MP5 hand/light points; BuildM4A1 replaces them for the real model.
            gun.TriggerPoint = new Vector3(0f, -0.045f, 0f);
            gun.FrontGripPoint = new Vector3(0f, -0.07f, 0.21f);
            gun.MagPoint = new Vector3(-0.02f, -0.16f, 0.06f);
            gun.LightPoint = new Vector3(0f, -0.04f, 0.33f);
            gun.Reserve = pistol ? 48 : 150;
            gun.BuildModel();
            gun.audioSrc = root.AddComponent<AudioSource>();
            gun.audioSrc.spatialBlend = 0f;
            gun.audioSrc.volume = 0.8f;
            return gun;
        }

        void BuildModel()
        {
            model = new GameObject("Model").transform;
            model.SetParent(transform, false);
            model.localPosition = hipPos;

            if (IsPistol) { if (!BuildPistol()) BuildBlockPistol(); }
            else if (!BuildM4A1()) BuildBlockModel();

            var flash = Gfx.Prim(PrimitiveType.Sphere, "MuzzleFlash", muzzle, Vector3.zero, new Vector3(0.07f, 0.07f, 0.16f),
                Gfx.Emissive(new Color(1f, 0.7f, 0.3f), new Color(6f, 3.5f, 1f)), false);
            flashRenderer = flash.GetComponent<Renderer>();
            flashRenderer.enabled = false;
            flashLight = Gfx.PointLight(muzzle, new Vector3(0f, 0f, 0.1f), new Color(1f, 0.7f, 0.35f), 4f, 9f, LightShadows.None);
            flashLight.enabled = false;

            foreach (var r in model.GetComponentsInChildren<Renderer>())
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var tr = new GameObject("Tracer");
            tracer = tr.AddComponent<LineRenderer>();
            tracer.positionCount = 2;
            tracer.startWidth = 0.015f;
            tracer.endWidth = 0.005f;
            tracer.material = Gfx.UnlitGlow(null, new Color(9f, 5f, 1.5f), true);
            tracer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            tracer.startColor = new Color(1f, 0.85f, 0.5f, 0.9f);
            tracer.endColor = new Color(1f, 0.6f, 0.2f, 0f);
            tracer.enabled = false;

            if (holeMat == null) holeMat = Gfx.Lit(new Color(0.02f, 0.02f, 0.02f), 0.1f);
            if (sparkMat == null) sparkMat = Gfx.Emissive(new Color(1f, 0.7f, 0.3f), new Color(5f, 3f, 0.8f));
            if (bloodMat == null) bloodMat = Gfx.Lit(new Color(0.25f, 0.01f, 0.01f), 0.6f);
        }

        /// <summary>
        /// The M4A1 model (Resources/Models/M4A1, "M4 Carbine" by Umang Rank on Sketchfab). The FBX is
        /// +Z forward / +Y up at about a quarter of real size; it is scaled to a 0.81 m carbine and placed
        /// so its trigger sits where the hands expect it. Magazine and rounds move together on reloads.
        /// </summary>
        bool BuildM4A1()
        {
            var prefab = Resources.Load<GameObject>(M4A1Path);
            if (prefab == null) return false;

            var mesh = Instantiate(prefab, model, false).transform;
            mesh.name = "M4A1";
            mesh.localScale = Vector3.one * M4A1Scale;
            // FBX trigger is at (0, 0.0336, -0.0353); put it on TriggerPoint.
            mesh.localPosition = TriggerPoint - new Vector3(0f, 0.0336f, -0.0353f) * M4A1Scale;

            var mat = Gfx.Lit(new Color(0.6f, 0.6f, 0.62f), 1f, 1f, Resources.Load<Texture2D>(M4A1Path + "_BaseColor"));
            mat.name = "M4A1 (HDRP)";
            var normal = Resources.Load<Texture2D>(M4A1Path + "_Normal");
            if (normal != null) { mat.SetTexture("_NormalMap", normal); mat.SetFloat("_NormalScale", 1f); }
            var mask = Resources.Load<Texture2D>(M4A1Path + "_Mask");
            if (mask != null)
            {
                // Anodised black aluminium and polymer, not chrome: keep the reflections soft.
                mat.SetTexture("_MaskMap", mask);
                mat.SetFloat("_MetallicRemapMax", 0.55f);
                mat.SetFloat("_SmoothnessRemapMax", 0.7f);
            }
            else { mat.SetFloat("_Metallic", 0.6f); mat.SetFloat("_Smoothness", 0.4f); }
            Gfx.Validate(mat);
            foreach (var r in mesh.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = mat;

            mag = new GameObject("Mag").transform;
            mag.SetParent(model, false);
            mag.localPosition = new Vector3(0f, -0.093f, 0.08f);
            foreach (var t in mesh.GetComponentsInChildren<Transform>(true))
                if (t.name == "Magazine" || t.name == "Bullets") t.SetParent(mag, true);
            magHome = mag.localPosition;

            // Flash hider ends at FBX z 0.0864 on the bore line (y 0.0478).
            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(model, false);
            muzzle.localPosition = mesh.localPosition + new Vector3(0f, 0.0478f, 0.0864f) * M4A1Scale;

            FrontGripPoint = new Vector3(0.05f, -0.035f, 0.22f);
            MagPoint = new Vector3(-0.02f, -0.15f, 0.085f);
            LightPoint = new Vector3(0f, -0.03f, 0.40f);
            hipPos = new Vector3(0.17f, -0.2f, 0.25f);
            aimPos = new Vector3(0f, -0.094f, 0.40f);      // eye in line with the front sight post
            model.localPosition = hipPos;
            return true;
        }

        /// <summary>
        /// The pistol: "9 mm" by Slava Zemlyanik (Sketchfab), from the gun pistol.glb you uploaded, prepared in Blender at
        /// real size (about 19 cm) with the trigger at its origin and the muzzle along +Z. Held in both hands.
        /// </summary>
        bool BuildPistol()
        {
            var prefab = Resources.Load<GameObject>(PistolPath);
            if (prefab == null) return false;
            var mesh = Instantiate(prefab, model, false).transform;
            mesh.name = "Pistol9mm";
            mesh.localPosition = TriggerPoint;
            Vector3 muzzleLocal = new Vector3(0f, 0.047f, 0.07f);
            foreach (var t in mesh.GetComponentsInChildren<Transform>(true)) if (t.name == "Muzzle") muzzleLocal = mesh.InverseTransformPoint(t.position);
            // The importer may hand it over facing backwards: turn it so the muzzle points ahead.
            if (muzzleLocal.z < 0f)
            {
                mesh.localRotation = Quaternion.Euler(0f, 180f, 0f);
                muzzleLocal = mesh.localRotation * muzzleLocal;
            }
            foreach (var r in mesh.GetComponentsInChildren<Renderer>(true))
            {
                var mats = Gfx.FromGltf(r.sharedMaterials);
                foreach (var m in mats) if (m != null) { m.SetFloat("_Smoothness", Mathf.Clamp(m.GetFloat("_Smoothness"), 0.35f, 0.6f)); m.SetFloat("_Metallic", 0.4f); }
                r.sharedMaterials = mats;
            }
            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(model, false);
            muzzle.localPosition = mesh.localPosition + muzzleLocal;
            // Support hand cupped under the grip, just ahead of the trigger hand.
            FrontGripPoint = new Vector3(-0.025f, -0.06f, 0.0f);
            MagPoint = new Vector3(0f, -0.11f, -0.03f);
            LightPoint = new Vector3(0f, -0.025f, 0.11f);
            hipPos = new Vector3(0.13f, -0.17f, 0.36f);
            aimPos = new Vector3(0f, -0.098f, 0.33f);
            model.localPosition = hipPos;
            return true;
        }

        /// <summary>Fallback pistol out of boxes if the model is missing.</summary>
        void BuildBlockPistol()
        {
            var body = Gfx.Lit(new Color(0.05f, 0.05f, 0.055f), 0.45f, 0.6f);
            Gfx.Box("Slide", model, new Vector3(0f, 0.03f, 0.04f), new Vector3(0.03f, 0.035f, 0.19f), body, false);
            Gfx.Box("Grip", model, new Vector3(0f, -0.04f, -0.03f), new Vector3(0.028f, 0.11f, 0.045f), body, false).transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(model, false);
            muzzle.localPosition = new Vector3(0f, 0.03f, 0.14f);
            FrontGripPoint = new Vector3(-0.025f, -0.06f, 0f);
            hipPos = new Vector3(0.13f, -0.17f, 0.36f);
            aimPos = new Vector3(0f, -0.1f, 0.33f);
            model.localPosition = hipPos;
        }

        /// <summary>Fallback when the M4A1 model is missing: the original primitive MP5.</summary>
        void BuildBlockModel()
        {
            var body = Gfx.Lit(new Color(0.06f, 0.06f, 0.065f), 0.45f, 0.7f);
            var parts = Gfx.Lit(new Color(0.12f, 0.12f, 0.13f), 0.35f, 0.6f);
            var polymer = Gfx.Lit(new Color(0.04f, 0.04f, 0.04f), 0.15f);

            // Receiver and the characteristic MP5 cocking tube on top.
            Gfx.Box("Receiver", model, new Vector3(0f, 0f, 0f), new Vector3(0.05f, 0.075f, 0.32f), body, false);
            Gfx.Prim(PrimitiveType.Cylinder, "CockingTube", model, new Vector3(0f, 0.035f, 0.14f), new Vector3(0.028f, 0.12f, 0.028f), parts, false)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            // Handguard (slimline) and barrel.
            Gfx.Box("Handguard", model, new Vector3(0f, -0.012f, 0.24f), new Vector3(0.056f, 0.06f, 0.15f), polymer, false);
            Gfx.Prim(PrimitiveType.Cylinder, "Barrel", model, new Vector3(0f, 0.008f, 0.36f), new Vector3(0.018f, 0.06f, 0.018f), parts, false)
                .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            // Front sight hood and rear drum sight.
            Gfx.Box("FrontSightL", model, new Vector3(-0.012f, 0.06f, 0.29f), new Vector3(0.005f, 0.035f, 0.02f), body, false);
            Gfx.Box("FrontSightR", model, new Vector3(0.012f, 0.06f, 0.29f), new Vector3(0.005f, 0.035f, 0.02f), body, false);
            Gfx.Box("FrontPost", model, new Vector3(0f, 0.058f, 0.29f), new Vector3(0.003f, 0.025f, 0.004f), Gfx.Emissive(Color.white, new Color(0.15f, 0.6f, 0.15f)), false);
            Gfx.Prim(PrimitiveType.Cylinder, "RearDrum", model, new Vector3(0f, 0.058f, -0.11f), new Vector3(0.035f, 0.012f, 0.035f), body, false)
                .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            // Pistol grip and trigger guard.
            Gfx.Box("Grip", model, new Vector3(0f, -0.085f, -0.06f), new Vector3(0.04f, 0.11f, 0.05f), polymer, false)
                .transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
            Gfx.Box("TriggerGuard", model, new Vector3(0f, -0.05f, 0.0f), new Vector3(0.012f, 0.012f, 0.07f), polymer, false);
            // Curved 30-round magazine.
            mag = Gfx.Box("Magazine", model, new Vector3(0f, -0.1f, 0.075f), new Vector3(0.026f, 0.14f, 0.045f), parts, false).transform;
            mag.localRotation = Quaternion.Euler(18f, 0f, 0f);
            magHome = mag.localPosition;
            // Retractable stock rails.
            Gfx.Box("StockRailL", model, new Vector3(-0.02f, 0.005f, -0.24f), new Vector3(0.008f, 0.012f, 0.18f), body, false);
            Gfx.Box("StockRailR", model, new Vector3(0.02f, 0.005f, -0.24f), new Vector3(0.008f, 0.012f, 0.18f), body, false);
            Gfx.Box("StockButt", model, new Vector3(0f, -0.01f, -0.33f), new Vector3(0.05f, 0.09f, 0.02f), polymer, false);

            muzzle = new GameObject("Muzzle").transform;
            muzzle.SetParent(model, false);
            muzzle.localPosition = new Vector3(0f, 0.008f, 0.43f);
        }

        public void SetVisible(bool visible)
        {
            model.gameObject.SetActive(visible);
            if (!visible) IsAiming = false;
        }

        public void AddAmmo(int rounds) { Reserve += rounds; }

        /// <summary>Restores the magazine and spare rounds (used when respawning at a checkpoint).</summary>
        public void SetAmmo(int mag, int reserve) { Ammo = Mathf.Clamp(mag, 0, Capacity); Reserve = Mathf.Max(0, reserve); }

        /// <summary>Put away (X): a reload in progress is abandoned.</summary>
        public void Holster() { IsReloading = false; IsAiming = false; triggerWasDown = true; if (mag != null) mag.localPosition = magHome; }

        /// <summary>Called by the player each frame when they're free to act.</summary>
        public void Tick()
        {
            // Only while the game has the mouse: clicks in other windows (or on the editor) never fire the gun.
            bool focused = InputWrap.GameHasMouse;
            bool trigger = focused && InputWrap.Held(KeyCode.Mouse0);
            IsAiming = (ForceAim || focused && InputWrap.Held(KeyCode.Mouse1)) && !owner.IsSprinting && !IsReloading;

            if (IsReloading && Time.time >= reloadEnd) FinishReload();
            if (InputWrap.Down(KeyCode.R) || ForceReload) StartReload(ReloadTime);

            // The pistol is semi-automatic: one shot per pull.
            if (trigger && !IsReloading && Time.time >= nextFire && (!IsPistol || !triggerWasDown || Ammo == 0))
            {
                if (Ammo > 0) Fire();
                else if (!triggerWasDown)
                {
                    audioSrc.PlayOneShot(Sfx.Click, 0.8f);
                    if (Reserve > 0) StartReload(FastReloadTime);
                    else GameManager.Instance.Toast(IsPistol ? "Pistol empty - swap to the rifle (X)" : "Out of ammo - find a green ammo box, or swap to the pistol (X)");
                }
            }
            triggerWasDown = trigger;

            Animate();
        }

        void Fire()
        {
            nextFire = Time.time + FireInterval;
            lastShot = Time.time;
            Ammo--;
            kick = 1f;

            audioSrc.pitch = Random.Range(0.92f, 1.08f) * (IsPistol ? 1.3f : 1f);
            audioSrc.PlayOneShot(Sfx.Gunshot, IsPistol ? 0.65f : 0.85f);
            NoiseSystem.Emit(owner.transform.position, NoiseRadius);

            flashRenderer.enabled = true;
            flashLight.enabled = true;
            flashRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            flashEnd = Time.time + 0.04f;

            float spread = IsAiming ? (IsPistol ? 0.25f : 0.35f) : (owner.IsCrouching ? 1.0f : IsPistol ? 1.4f : 1.8f);
            if (owner.IsSprinting) spread *= 2f;
            Vector3 dir = Quaternion.Euler(Random.Range(-spread, spread), Random.Range(-spread, spread), 0f) * Vector3.forward;
            dir = cam.transform.TransformDirection(dir);
            Vector3 origin = cam.transform.position;
            Vector3 end = origin + dir * Range;

            RaycastHit hit;
            bool hitWorld = Physics.Raycast(origin, dir, out hit, Range, Gfx.ShootMask, QueryTriggerInteraction.Ignore);
            // Ghosts have no colliders, so test them along the ray up to whatever solid thing it hit.
            float ghostDist;
            var ghost = Ghost.RayHit(origin, dir, hitWorld ? hit.distance : Range, out ghostDist);
            // Rays starting inside a collider never hit it, so a brute pressed right up against you
            // (camera inside its hit box) would be bulletproof. Point-blank shots hit whatever you're inside.
            Zombie pointBlank = null;
            if (ghost == null)
                foreach (var c in Physics.OverlapSphere(origin, 0.05f, 1 << Gfx.EnemyLayer, QueryTriggerInteraction.Ignore))
                {
                    pointBlank = c.GetComponentInParent<Zombie>();
                    if (pointBlank != null) break;
                }
            if (pointBlank != null)
            {
                end = origin + dir * 0.3f;
                pointBlank.TakeDamage(Damage, end, dir, false);
                SpawnBurst(end, -dir, bloodMat, 6, 2.5f);
                GameManager.Instance.ShowHitMarker(false);
            }
            else if (ghost != null)
            {
                end = origin + dir * ghostDist;
                ghost.TakeHit(Damage);
                SpawnBurst(end, -dir, sparkMat, 3, 2f);
                GameManager.Instance.ShowHitMarker(false);
            }
            else if (hitWorld)
            {
                end = hit.point;
                var z = hit.collider.GetComponentInParent<Zombie>();
                if (z != null)
                {
                    bool head = hit.collider.name == "Head";
                    z.TakeDamage(Damage * (head ? 2.5f : 1f), hit.point, dir, head);
                    SpawnBurst(hit.point, hit.normal, bloodMat, 6, 2.5f);
                    GameManager.Instance.ShowHitMarker(head);
                }
                else
                {
                    SpawnBurst(hit.point, hit.normal, sparkMat, 4, 4f);
                    SpawnHole(hit);
                }
            }

            tracer.SetPosition(0, muzzle.position);
            tracer.SetPosition(1, end);
            tracer.enabled = Random.value < 0.4f;
            tracerEnd = Time.time + 0.03f;

            owner.AddRecoil((IsAiming ? 0.45f : 0.75f) * (IsPistol ? 1.5f : 1f), Random.Range(-0.25f, 0.3f));

            // Last round fired: slap a fresh mag in straight away.
            if (Ammo == 0 && Reserve > 0) StartReload(FastReloadTime);
        }

        void StartReload(float duration)
        {
            if (IsReloading || Ammo >= Capacity || Reserve <= 0) return;
            IsReloading = true;
            reloadDuration = duration;
            reloadEnd = Time.time + duration;
            audioSrc.PlayOneShot(Sfx.Click, 0.7f);
            NoiseSystem.Emit(owner.transform.position, 3f);
        }

        void FinishReload()
        {
            int take = Mathf.Min(Capacity - Ammo, Reserve);
            Ammo += take;
            Reserve -= take;
            IsReloading = false;
            audioSrc.PlayOneShot(Sfx.Click, 1f);
        }

        void Animate()
        {
            if (Time.time > flashEnd) { flashRenderer.enabled = false; flashLight.enabled = false; }
            if (Time.time > tracerEnd) tracer.enabled = false;

            // Weapon sway follows the mouse, bob follows movement, kick on each shot.
            Vector2 m = InputWrap.MouseDelta;
            sway = Vector2.Lerp(sway, new Vector2(-m.x, -m.y) * 0.004f, Time.deltaTime * 8f);
            kick = Mathf.MoveTowards(kick, 0f, Time.deltaTime * 14f);
            Vector3 target = IsAiming ? aimPos : hipPos;
            if (owner.IsSprinting) target += new Vector3(0.05f, -0.06f, -0.05f);
            Vector3 pos = target + new Vector3(sway.x, sway.y, -kick * 0.035f);
            model.localPosition = Vector3.Lerp(model.localPosition, pos, Time.deltaTime * 18f);

            Quaternion rot = Quaternion.Euler(-kick * 3f + sway.y * 200f, sway.x * 200f, 0f);
            if (owner.IsSprinting) rot *= Quaternion.Euler(10f, -25f, 10f);
            if (IsReloading)
            {
                float t = 1f - (reloadEnd - Time.time) / reloadDuration;
                rot *= Quaternion.Euler(-20f * Mathf.Sin(t * Mathf.PI), 0f, 25f * Mathf.Sin(t * Mathf.PI));
                if (mag != null) mag.localPosition = magHome + Vector3.down * 0.25f * Mathf.Sin(Mathf.Clamp01(t * 1.3f) * Mathf.PI);
            }
            else if (mag != null) mag.localPosition = magHome;
            model.localRotation = Quaternion.Slerp(model.localRotation, rot, Time.deltaTime * 15f);

            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, IsAiming ? 50f : 70f, Time.deltaTime * 12f);
        }

        static void SpawnHole(RaycastHit hit)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.name = "BulletHole";
            g.transform.position = hit.point + hit.normal * 0.004f;
            g.transform.rotation = Quaternion.LookRotation(-hit.normal) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            g.transform.localScale = Vector3.one * Random.Range(0.05f, 0.08f);
            g.GetComponent<Renderer>().sharedMaterial = holeMat;
            g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            holes.Enqueue(g);
            while (holes.Count > 80)
            {
                var old = holes.Dequeue();
                if (old != null) Object.Destroy(old);
            }
        }

        static void SpawnBurst(Vector3 point, Vector3 normal, Material mat, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(g.GetComponent<Collider>());
                g.transform.position = point + normal * 0.03f;
                g.transform.localScale = Vector3.one * Random.Range(0.015f, 0.04f);
                g.GetComponent<Renderer>().sharedMaterial = mat;
                g.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                var p = g.AddComponent<Particle>();
                p.velocity = (normal + Random.insideUnitSphere * 0.8f).normalized * speed * Random.Range(0.4f, 1f);
                p.life = Random.Range(0.25f, 0.5f);
            }
        }
    }

    /// <summary>Tiny physics-free debris particle (sparks, blood drops).</summary>
    public class Particle : MonoBehaviour
    {
        public Vector3 velocity;
        public float life = 0.4f;
        float age;
        Vector3 startScale;

        void Start() { startScale = transform.localScale; }

        void Update()
        {
            age += Time.deltaTime;
            velocity += Physics.gravity * Time.deltaTime;
            transform.position += velocity * Time.deltaTime;
            transform.localScale = startScale * Mathf.Clamp01(1f - age / life);
            if (age >= life) Destroy(gameObject);
        }
    }
}
