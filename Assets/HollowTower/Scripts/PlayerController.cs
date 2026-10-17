using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// First-person survivor: walk / sprint / crouch, flashlight, lockers, health.
    /// Also works out how visible you are (light around you, stance, torch) for the stealth system.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        public float MaxHealth = 100f;
        public float Health { get; private set; }
        public bool IsDead { get; private set; }
        public bool IsHidden { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool FlashlightOn { get { return flashlight != null && flashlight.enabled; } }
        public bool InputLocked { get; set; }
        /// <summary>True if an enemy was watching when you climbed into the locker.</summary>
        public bool SeenHiding { get; private set; }
        public HidingSpot CurrentSpot { get; private set; }
        public float Stamina { get; private set; }
        /// <summary>0 = invisible, 1 = normal, &gt;1 = lit up like a target.</summary>
        public float Visibility { get; private set; }
        public float LightLevel { get; private set; }
        public float LastHurtTime { get; private set; }

        public Camera Cam { get; private set; }
        /// <summary>The gun in your hands (X swaps between the rifle and the pistol).</summary>
        public MP5 Gun { get; private set; }
        public MP5 Rifle { get; private set; }
        public MP5 Pistol { get; private set; }
        public float Yaw { get { return yaw; } }
        public int Floor { get { return LevelBuilder.FloorAt(transform.position); } }
        public Vector3 ChestPosition { get { return transform.position + Vector3.up * (IsCrouching ? 0.75f : 1.3f); } }
        public Vector3 EyePosition { get { return Cam.transform.position; } }
        public Vector3 Velocity { get { return cc != null && cc.enabled ? cc.velocity : Vector3.zero; } }
        public bool Grounded { get { return cc == null || !cc.enabled || cc.isGrounded; } }
        /// <summary>0 = standing, 1 = fully crouched.</summary>
        public float Crouch01 { get { return cc == null ? 0f : Mathf.InverseLerp(StandHeight, CrouchHeight, cc.height); } }
        public PlayerBody Body { get; private set; }

        public float MouseSensitivity = 2.2f;

        CharacterController cc;
        Light flashlight, fill;
        Transform camPivot;
        float yaw, pitch, recoilPitch, recoilYaw;
        float verticalVel;
        float stepDistance, bobPhase;
        float camHeight = 1.65f;
        bool wasGrounded = true;
        bool crouchToggle;
        float nextLightSample;
        float flashBase, fillBase;
        float hideYawCenter;
        float deathT;
        AudioSource voice;
        AudioSource heartbeat;

        // Faster than everything in the tower: ghosts hunt at 4.3 m/s, Runners sprint at 6.0 m/s.
        const float WalkSpeed = 4.8f, SprintSpeed = 8.2f, CrouchSpeed = 2.6f;
        const float StandHeight = 1.8f, CrouchHeight = 1.1f;

        public static PlayerController Spawn(Vector3 position, float yaw)
        {
            var g = new GameObject("Player");
            g.layer = Gfx.PlayerLayer;
            g.transform.position = position;
            var cc = g.AddComponent<CharacterController>();
            cc.height = StandHeight;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, StandHeight * 0.5f, 0f);
            cc.stepOffset = 0.4f;
            cc.slopeLimit = 50f;
            cc.skinWidth = 0.04f;
            var p = g.AddComponent<PlayerController>();
            p.yaw = yaw;
            return p;
        }

        void Awake()
        {
            Instance = this;
            cc = GetComponent<CharacterController>();
            Health = MaxHealth;
            Stamina = 1f;
            Visibility = 1f;

            camPivot = new GameObject("CameraPivot").transform;
            camPivot.SetParent(transform, false);
            camPivot.localPosition = new Vector3(0f, camHeight, 0f);

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(camPivot, false);
            Cam = camGo.AddComponent<Camera>();
            Cam.fieldOfView = 70f;
            Cam.nearClipPlane = 0.03f;
            Cam.farClipPlane = 150f;
            Cam.clearFlags = CameraClearFlags.SolidColor;
            Cam.backgroundColor = new Color(0.005f, 0.006f, 0.008f);
            HDRPLook.SetupCamera(Cam);
            camGo.AddComponent<AudioListener>();
            camGo.AddComponent<ScreenFX>();

            // Flashlight: the main light source you carry. Casts soft real-time shadows.
            flashlight = Gfx.SpotLight(camGo.transform, new Vector3(0.18f, -0.12f, 0.1f), Quaternion.identity, new Color(1f, 0.94f, 0.84f),
                1100f, 34f, 64f, 0.42f, LightShadows.Soft);
            flashlight.name = "Flashlight";
            var flHD = flashlight.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
            if (flHD != null) flHD.useRayTracedShadows = HDRPLook.RayTracing && HDRPLook.RayTracedFlashlightShadows;
            var fl = flashlight.gameObject;
            flashBase = flashlight.intensity;

            // Very faint bounce light so you can always see your own hands.
            fill = Gfx.PointLight(camGo.transform, new Vector3(0f, 0f, 0.3f), new Color(0.55f, 0.6f, 0.75f), 0.05f, 4f, LightShadows.None);
            fill.name = "Fill";
            fillBase = fill.intensity;

            Rifle = MP5.Create(camGo.transform, this);
            Pistol = MP5.Create(camGo.transform, this, true);
            Pistol.SetVisible(false);
            Gun = Rifle;

            // Your own body. The flashlight moves onto the gun so your arms don't shadow the beam.
            Body = PlayerBody.Create(this);
            if (Body != null)
            {
                fl.transform.SetParent(Gun.Model, false);
                fl.transform.localPosition = Gun.LightPoint;
                fl.transform.localRotation = Quaternion.identity;
            }

            voice = gameObject.AddComponent<AudioSource>();
            voice.spatialBlend = 0f;
            heartbeat = gameObject.AddComponent<AudioSource>();
            heartbeat.clip = Sfx.Heartbeat;
            heartbeat.loop = true;
            heartbeat.spatialBlend = 0f;
            heartbeat.volume = 0f;
            heartbeat.Play();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        void Update()
        {
            if (IsDead) { UpdateDeath(); return; }
            var gm = GameManager.Instance;
            bool paused = gm != null && gm.IsPaused;
            if (paused) return;

            UpdateHeartbeat();
            RegenerateHealth();
            UpdateFlashlightFlicker();
            if (Time.time >= nextLightSample) { nextLightSample = Time.time + 0.2f; SampleLight(); }

            if (InputLocked) { ApplyLook(); return; }

            Look();
            if (IsHidden)
            {
                if (CurrentSpot != null && CurrentSpot.Safe && Health < MaxHealth) Heal(5f * Time.deltaTime);
                if (InputWrap.Down(KeyCode.E)) Unhide();
                Visibility = 0f;
                return;
            }

            if (InCover) UpdateCover();
            else
            {
                Move();
                if (InputWrap.Down(KeyCode.Q)) TryTakeCover();
            }
            if (InputWrap.Down(KeyCode.F))
            {
                flashlight.enabled = !flashlight.enabled;
                voice.PlayOneShot(Sfx.Click, 0.6f);
            }
            if (InputWrap.Down(KeyCode.E)) TryHide();
            if (InputWrap.Down(KeyCode.X)) SwapWeapon();
            if (InputWrap.Down(KeyCode.F8) && HDRPLook.RayTracing)
            {
                HDRPLook.SetRayTracedEffects(!HDRPLook.RayTracedEffects);
                GameManager.Instance.Toast(HDRPLook.RayTracedEffects ? "Ray-traced GI, reflections & AO ON (F8)" : "Performance mode: screen-space GI, reflections & AO (F8)");
            }
            if (InputWrap.Down(KeyCode.F9) && HDRPLook.RayTracing)
            {
                // Ray-traced torch shadows look softer and more exact, but cost ~5 ms on a laptop GPU.
                HDRPLook.RayTracedFlashlightShadows = !HDRPLook.RayTracedFlashlightShadows;
                var hd = flashlight.GetComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
                if (hd != null) hd.useRayTracedShadows = HDRPLook.RayTracedFlashlightShadows;
                GameManager.Instance.Toast(HDRPLook.RayTracedFlashlightShadows ? "Ray-traced flashlight shadows ON (F9)" : "Ray-traced flashlight shadows OFF (F9)");
            }
            Gun.Tick();
            // Aiming brings the rifle right up to the fill light; dim it so the receiver doesn't glare.
            fill.intensity = Mathf.MoveTowards(fill.intensity, fillBase * (Gun.IsAiming ? 0.2f : 1f), fillBase * 4f * Time.deltaTime);
            ComputeVisibility();
        }

        // ---------------- look & move ----------------

        void Look()
        {
            Vector2 m = InputWrap.MouseDelta * MouseSensitivity;
            float aimScale = Gun.IsAiming ? 0.55f : 1f;
            yaw += m.x * aimScale;
            pitch = Mathf.Clamp(pitch - m.y * aimScale, -85f, 85f);
            if (IsHidden)
            {
                yaw = hideYawCenter + Mathf.Clamp(Mathf.DeltaAngle(hideYawCenter, yaw), -50f, 50f);
                pitch = Mathf.Clamp(pitch, -30f, 30f);
            }
            ApplyLook();
        }

        void ApplyLook()
        {
            recoilPitch = Mathf.Lerp(recoilPitch, 0f, 10f * Time.deltaTime);
            recoilYaw = Mathf.Lerp(recoilYaw, 0f, 10f * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, yaw + recoilYaw, 0f);
            camPivot.localRotation = Quaternion.Euler(pitch - recoilPitch, 0f, leanRoll);
        }

        /// <summary>X: put one gun away and draw the other. The flashlight moves to the gun in your hands.</summary>
        public void SwapWeapon()
        {
            if (IsHidden || IsDead) return;
            var next = Gun == Rifle ? Pistol : Rifle;
            Gun.Holster();
            Gun.SetVisible(false);
            Gun = next;
            Gun.Holster();
            Gun.SetVisible(true);
            if (Body != null)
            {
                flashlight.transform.SetParent(Gun.Model, false);
                flashlight.transform.localPosition = Gun.LightPoint;
                flashlight.transform.localRotation = Quaternion.identity;
            }
            voice.PlayOneShot(Sfx.Click, 0.7f);
            NoiseSystem.Emit(transform.position, 2f);
            var gm = GameManager.Instance;
            if (gm != null) gm.Toast(Gun.DisplayName + "  " + Gun.Ammo + " / " + Gun.Reserve);
        }

        /// <summary>A kill's reward: a few rounds for each gun.</summary>
        public void AddKillAmmo(int rifleRounds, int pistolRounds)
        {
            Rifle.AddAmmo(rifleRounds);
            Pistol.AddAmmo(pistolRounds);
        }

        public void AddRecoil(float up, float side)
        {
            recoilPitch += up;
            recoilYaw += side;
            pitch -= up * 0.35f; // a little of the kick stays, so you must pull down
        }

        void Move()
        {
            Vector2 input = InputWrap.Move;
            bool crouchHeld = InputWrap.Held(KeyCode.LeftControl);
            if (InputWrap.Down(KeyCode.C)) crouchToggle = !crouchToggle;
            if (InputWrap.Held(KeyCode.LeftShift) && input.y > 0.1f) crouchToggle = false;
            bool crouch = crouchToggle || crouchHeld;
            IsCrouching = crouch;

            bool wantSprint = InputWrap.Held(KeyCode.LeftShift) && input.y > 0.1f && !crouch && !Gun.IsAiming;
            IsSprinting = wantSprint && Stamina > 0.05f;
            Stamina = Mathf.Clamp01(Stamina + (IsSprinting ? -Time.deltaTime / 7f : Time.deltaTime / 5f));

            float speed = crouch ? CrouchSpeed : (IsSprinting ? SprintSpeed : WalkSpeed);
            if (Gun.IsAiming) speed *= 0.6f;

            ApplyStance(crouch);

            Vector3 move = (transform.right * input.x + transform.forward * input.y) * speed;

            if (cc.isGrounded)
            {
                verticalVel = -2f;
                if (InputWrap.Down(KeyCode.Space) && !crouch) verticalVel = 4.6f;
            }
            verticalVel += Physics.gravity.y * Time.deltaTime;
            move.y = verticalVel;
            cc.Move(move * Time.deltaTime);

            // Landing thud - and a long drop (off the mezzanine, through a rotten floor) hurts.
            if (cc.isGrounded && !wasGrounded && verticalVel < -6f)
            {
                NoiseSystem.Emit(transform.position, 8f);
                Sfx.At(Sfx.Step, transform.position, 1f, 0.7f);
                if (verticalVel < -8f) TakeDamage((-verticalVel - 8f) * 12f, transform.position + Vector3.down);
            }
            wasGrounded = cc.isGrounded;

            // Footsteps make noise; crouching is almost silent.
            Vector3 flat = cc.velocity; flat.y = 0f;
            float v = flat.magnitude;
            if (cc.isGrounded && v > 0.2f)
            {
                stepDistance += v * Time.deltaTime;
                float stride = IsSprinting ? 2.6f : 2.0f;
                if (stepDistance > stride)
                {
                    stepDistance = 0f;
                    float loud = crouch ? 1.2f : (IsSprinting ? 14f : 5f);
                    NoiseSystem.Emit(transform.position, loud);
                    voice.pitch = Random.Range(0.85f, 1.15f);
                    voice.PlayOneShot(Sfx.Step, crouch ? 0.15f : (IsSprinting ? 0.8f : 0.4f));
                    voice.pitch = 1f;
                }
                bobPhase += v * Time.deltaTime * (IsSprinting ? 1.6f : 2.2f);
            }
            float bob = Mathf.Sin(bobPhase * Mathf.PI) * (IsSprinting ? 0.06f : 0.03f) * Mathf.Clamp01(v / 3f);
            SetLean(0f);
            camPivot.localPosition = new Vector3(Mathf.Cos(bobPhase * Mathf.PI * 0.5f) * bob * 0.6f + leanX, camHeight + bob, 0f);
        }

        void ApplyStance(bool crouch)
        {
            // Smooth crouch height.
            float targetH = crouch ? CrouchHeight : StandHeight;
            cc.height = Mathf.MoveTowards(cc.height, targetH, 5f * Time.deltaTime);
            cc.center = new Vector3(0f, cc.height * 0.5f, 0f);
            camHeight = Mathf.MoveTowards(camHeight, crouch ? 0.98f : 1.65f, 5f * Time.deltaTime);
        }

        // ---------------- cover ----------------

        /// <summary>Snapped behind something with Q.</summary>
        public bool InCover { get; private set; }
        /// <summary>Leaning or popping up out of cover (exposed, able to aim).</summary>
        public bool IsPeeking { get; private set; }
        /// <summary>True when the current cover is waist-high (you crouch behind it and pop up over it).</summary>
        public bool CoverIsLow { get; private set; }

        Vector3 coverSpot;
        float coverYaw, snapUntil, leanX, leanRoll;
        int peekSide;
        bool wasPeeking;
        const float CoverSearch = 3.5f, LeanDistance = 0.6f, LeanAngle = 14f;

        /// <summary>Q: find the nearest solid thing around you and slide in behind it, facing it.</summary>
        void TryTakeCover()
        {
            Vector3 waist = transform.position + Vector3.up * 0.75f;
            RaycastHit best = new RaycastHit();
            float bestScore = float.MaxValue;
            for (int i = 0; i < 24; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 15f, 0f) * Vector3.forward;
                RaycastHit h;
                if (!Physics.Raycast(waist, dir, out h, CoverSearch, Gfx.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                if (Mathf.Abs(h.normal.y) > 0.4f) continue;
                // Nearest wins, with a slight preference for whatever is in front of you.
                float score = h.distance - 0.5f * Mathf.Max(0f, Vector3.Dot(dir, transform.forward));
                if (score < bestScore) { bestScore = score; best = h; }
            }
            if (bestScore == float.MaxValue)
            {
                GameManager.Instance.Toast("No cover nearby");
                return;
            }

            Vector3 n = best.normal; n.y = 0f; n.Normalize();
            coverSpot = best.point + n * (cc.radius + 0.12f);
            coverSpot.y = transform.position.y;
            // Low cover (crates, desks, counters) if nothing is in the way at standing head height.
            CoverIsLow = !Physics.Raycast(coverSpot + Vector3.up * 1.5f, -n, cc.radius + 0.7f, Gfx.WorldMask, QueryTriggerInteraction.Ignore);
            coverYaw = Quaternion.LookRotation(-n).eulerAngles.y;
            snapUntil = Time.time + 0.5f;
            InCover = true;
            IsSprinting = false;
            IsPeeking = wasPeeking = false;
            voice.PlayOneShot(Sfx.Step, 0.5f);
            NoiseSystem.Emit(transform.position, 2f);
        }

        public void ExitCover()
        {
            if (!InCover) return;
            InCover = false;
            IsPeeking = wasPeeking = false;
        }

        void UpdateCover()
        {
            float dt = Time.deltaTime;
            Vector2 input = InputWrap.Move;
            bool breakOut = InputWrap.Down(KeyCode.Q) || InputWrap.Down(KeyCode.Space) || input.y < -0.5f
                || (InputWrap.Held(KeyCode.LeftShift) && input.sqrMagnitude > 0.1f);
            if (breakOut) { ExitCover(); return; }

            // Slide into position and turn to face the cover.
            Vector3 to = coverSpot - transform.position; to.y = 0f;
            Vector3 move = Vector3.down * 2f;
            if (Time.time < snapUntil && to.magnitude > 0.03f)
            {
                move += Vector3.ClampMagnitude(to / dt, 9f);
                yaw += Mathf.DeltaAngle(yaw, coverYaw) * Mathf.Clamp01(dt * 14f);
            }
            cc.Move(move * dt);

            // Peek: right click (aim) or firing pops you out; A / D lean by hand.
            bool focused = InputWrap.GameHasMouse;
            bool wantsPeek = focused && (InputWrap.Held(KeyCode.Mouse1) || InputWrap.Held(KeyCode.Mouse0));
            if (wantsPeek && !wasPeeking) peekSide = CoverIsLow ? 0 : PickPeekSide();
            wasPeeking = wantsPeek;
            int lean = (InputWrap.Held(KeyCode.D) ? 1 : 0) - (InputWrap.Held(KeyCode.A) ? 1 : 0);
            if (lean == 0 && wantsPeek) lean = peekSide;
            IsPeeking = wantsPeek || lean != 0;

            if (InputWrap.Down(KeyCode.C)) crouchToggle = !crouchToggle;
            bool crouch = CoverIsLow ? !wantsPeek : (crouchToggle || InputWrap.Held(KeyCode.LeftControl));
            IsCrouching = crouch;
            ApplyStance(crouch);

            SetLean(lean);
            camPivot.localPosition = new Vector3(leanX, camHeight, 0f);
        }

        /// <summary>Which edge of tall cover to lean round: the side with clear space ahead (right first).</summary>
        int PickPeekSide()
        {
            Vector3 head = transform.position + Vector3.up * camHeight;
            for (int side = 1; side >= -1; side -= 2)
            {
                Vector3 r = transform.right * side;
                if (Physics.SphereCast(head, 0.15f, r, out _, LeanDistance, Gfx.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                if (!Physics.Raycast(head + r * LeanDistance, transform.forward, 1.5f, Gfx.WorldMask, QueryTriggerInteraction.Ignore)) return side;
            }
            return 0;
        }

        /// <summary>Slides the camera sideways (stopping short of walls) and tilts it into the lean.</summary>
        void SetLean(float side)
        {
            float target = side * LeanDistance;
            if (side != 0f)
            {
                RaycastHit h;
                Vector3 head = transform.position + Vector3.up * camHeight;
                if (Physics.SphereCast(head, 0.15f, transform.right * Mathf.Sign(side), out h, LeanDistance, Gfx.WorldMask, QueryTriggerInteraction.Ignore))
                    target = Mathf.Sign(side) * Mathf.Max(0f, h.distance - 0.05f);
            }
            leanX = Mathf.MoveTowards(leanX, target, 4f * Time.deltaTime);
            leanRoll = -leanX / LeanDistance * LeanAngle;
        }

        // ---------------- stealth ----------------

        void SampleLight()
        {
            // How lit is the spot you're standing in? (0 = pitch black, 1 = under a lamp)
            float best = 0f;
            var gm = GameManager.Instance;
            if (gm == null || gm.Level == null) return;
            Vector3 chest = ChestPosition;
            foreach (var tl in gm.Level.lights)
            {
                var l = tl.light;
                if (l == null || !l.enabled || !l.gameObject.activeInHierarchy) continue;
                Vector3 lp = l.transform.position;
                float d = Vector3.Distance(lp, chest);
                if (d > l.range) continue;
                float k = (1f - d / l.range) * Mathf.Clamp01(l.intensity / (1.5f * Gfx.Lumens));
                if (k <= best) continue;
                if (Physics.Linecast(lp, chest, Gfx.WorldMask, QueryTriggerInteraction.Ignore)) continue;
                best = k;
            }
            LightLevel = Mathf.Clamp01(best * 1.6f);
        }

        void ComputeVisibility()
        {
            float v = 0.45f + 0.75f * LightLevel;
            if (FlashlightOn) v = Mathf.Max(v, 1.05f);
            if (IsCrouching) v *= 0.55f;
            if (IsSprinting) v *= 1.25f;
            if (InCover && !IsPeeking) v *= 0.5f; // tucked in behind something
            if (Gun.FiredRecently) v = Mathf.Max(v, 1.4f);
            Visibility = v;
        }

        void TryHide()
        {
            HidingSpot best = null;
            float bestD = 2.0f;
            foreach (var s in HidingSpot.All)
            {
                if (s.Occupied) continue;
                Vector3 front = s.ExitPosition;
                float d = Vector3.Distance(new Vector3(front.x, transform.position.y, front.z), transform.position);
                if (Mathf.Abs(front.y - transform.position.y) > 1.2f) continue;
                if (d < bestD) { bestD = d; best = s; }
            }
            if (best == null) return;
            if (best.RequiresCrouch && !IsCrouching)
            {
                GameManager.Instance.Toast("Crouch (C) to squeeze under the desk");
                return;
            }
            Hide(best);
        }

        public HidingSpot NearbyHidingSpot()
        {
            foreach (var s in HidingSpot.All)
            {
                if (s.Occupied) continue;
                Vector3 front = s.ExitPosition;
                if (Mathf.Abs(front.y - transform.position.y) > 1.2f) continue;
                if (Vector3.Distance(new Vector3(front.x, transform.position.y, front.z), transform.position) < 2.0f) return s;
            }
            return null;
        }

        void Hide(HidingSpot spot)
        {
            SeenHiding = !spot.Safe && (Zombie.AnyWatching(this) || Ghost.AnyChasing());
            ExitCover();
            leanX = leanRoll = 0f;
            CurrentSpot = spot;
            spot.Occupied = true;
            spot.SwingDoor();
            IsHidden = true;
            IsSprinting = false;
            cc.enabled = false;
            transform.position = spot.InsidePosition;
            camHeight = spot.EyeHeight;
            camPivot.localPosition = new Vector3(0f, camHeight, 0f);
            yaw = hideYawCenter = spot.ViewRotation.eulerAngles.y;
            pitch = 0f;
            flashlight.enabled = false;
            Gun.SetVisible(false);
            voice.PlayOneShot(Sfx.Click, 0.8f);
            NoiseSystem.Emit(transform.position, 3f);
            GameManager.Instance.Toast(spot.Safe ? "Safe. Rest here - your wounds will slowly heal." : SeenHiding ? "They saw you get in..." : "Hidden. Hold your breath.");
        }

        /// <summary>A zombie dog is barking at your hiding spot: everything it called now knows you're in there.</summary>
        public void Expose()
        {
            if (!IsHidden || SeenHiding || (CurrentSpot != null && CurrentSpot.Safe)) return;
            SeenHiding = true;
            GameManager.Instance.Toast("The dog is barking at your hiding spot!");
        }

        public void Unhide()
        {
            if (!IsHidden) return;
            var spot = CurrentSpot;
            IsHidden = false;
            CurrentSpot = null;
            if (spot != null)
            {
                spot.Occupied = false;
                spot.SwingDoor();
                transform.position = spot.ExitPosition;
            }
            cc.enabled = true;
            Gun.SetVisible(true);
            voice.PlayOneShot(Sfx.Click, 0.8f);
        }

        // ---------------- health ----------------

        public void TakeDamage(float amount, Vector3 from)
        {
            if (IsDead) return;
            Health -= amount;
            LastHurtTime = Time.time;
            voice.PlayOneShot(Sfx.Hurt, 0.9f);
            Vector3 dir = (transform.position - from); dir.y = 0f;
            recoilPitch += 4f;
            recoilYaw += Random.Range(-4f, 4f);
            if (cc.enabled && dir.sqrMagnitude > 0.01f) cc.Move(dir.normalized * 0.3f);
            if (Health <= 0f) Die();
        }

        public void Heal(float amount) { Health = Mathf.Min(MaxHealth, Health + amount); }

        /// <summary>Points the view without moving (used by the -playtest bot to aim).</summary>
        public void SetLook(float newYaw, float newPitch) { yaw = newYaw; pitch = Mathf.Clamp(newPitch, -85f, 85f); }

        public void Teleport(Vector3 feet, float newYaw, float newPitch)
        {
            if (IsHidden) Unhide();
            ExitCover();
            leanX = leanRoll = 0f;
            cc.enabled = false;
            transform.position = feet + Vector3.up * 0.05f;
            cc.enabled = true;
            yaw = newYaw;
            pitch = newPitch;
        }

        /// <summary>Seconds after the last hit before health starts creeping back.</summary>
        public const float RegenDelay = 6f;
        /// <summary>Health per second while recovering (full heal from 1 HP takes about 25 s).</summary>
        public const float RegenRate = 4f;
        public bool IsRegenerating { get; private set; }

        void RegenerateHealth()
        {
            IsRegenerating = !IsDead && Health < MaxHealth && Time.time - LastHurtTime > RegenDelay;
            if (IsRegenerating) Health = Mathf.Min(MaxHealth, Health + RegenRate * Time.deltaTime);
        }

        void Die()
        {
            Health = 0f;
            IsDead = true;
            ExitCover();
            // The gun is dropped, so the torch goes back to the head to light the death cam.
            flashlight.transform.SetParent(Cam.transform, false);
            flashlight.transform.localPosition = new Vector3(0.18f, -0.12f, 0.1f);
            flashlight.transform.localRotation = Quaternion.identity;
            flashlight.enabled = true;
            Gun.SetVisible(false);
            deathT = 0f;
            GameManager.Instance.Lose();
        }

        void UpdateDeath()
        {
            deathT = Mathf.Min(1f, deathT + Time.deltaTime * 1.2f);
            camPivot.localPosition = Vector3.Lerp(camPivot.localPosition, new Vector3(0f, 0.25f, 0f), deathT);
            camPivot.localRotation = Quaternion.Slerp(camPivot.localRotation, Quaternion.Euler(-20f, 0f, 75f), deathT * 0.2f);
        }

        void UpdateHeartbeat()
        {
            // Heart pounds when monsters are close or you're hurt.
            float danger = 0f;
            var gm = GameManager.Instance;
            if (gm != null) danger = gm.ThreatLevel;
            danger = Mathf.Max(danger, 1f - Health / MaxHealth * 1.6f);
            heartbeat.volume = Mathf.Lerp(heartbeat.volume, Mathf.Clamp01(danger) * 1f, Time.deltaTime * 2f);
            heartbeat.pitch = 0.8f + Mathf.Clamp01(danger) * 0.6f;
        }

        void UpdateFlashlightFlicker()
        {
            if (flashlight == null) return;
            float ghostDist = Ghost.NearestDistance(transform.position);
            if (ghostDist < 10f)
            {
                float chance = Mathf.InverseLerp(10f, 2f, ghostDist);
                flashlight.intensity = Random.value < chance * 0.5f ? flashBase * Random.Range(0f, 0.4f) : flashBase;
            }
            else flashlight.intensity = flashBase;
        }

        /// <summary>Is the flashlight beam currently on this point (and not blocked)?</summary>
        public bool IlluminatesPoint(Vector3 p)
        {
            if (!FlashlightOn || IsHidden) return false;
            Vector3 origin = flashlight.transform.position;
            Vector3 d = p - origin;
            if (d.magnitude > flashlight.range * 0.75f) return false;
            if (Vector3.Angle(flashlight.transform.forward, d) > flashlight.spotAngle * 0.4f) return false;
            return !Physics.Linecast(origin, p, Gfx.WorldMask, QueryTriggerInteraction.Ignore);
        }
    }
}
