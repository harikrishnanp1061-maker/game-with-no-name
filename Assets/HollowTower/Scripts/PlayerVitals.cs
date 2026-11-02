using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// One player's stamina and sanity (VitalsDirector puts one on each body).
    /// Stamina: sprinting burns it (about eleven seconds flat out); it comes back while walking, faster standing still. Run
    /// it dry and they are winded: no sprinting until it is back past 45%, and loud panting that monsters can hear.
    /// Sanity: drains in the floor 2 chaos, with a hunting monster close, in the dark, and in one blow when a teammate
    /// dies nearby. It creeps back on its own only while things are calm and lit, and only to 60%: items do the rest.
    /// At zero they break down - collapse for a couple of seconds and scream (every monster near hears it), then come
    /// round at 25%.
    /// </summary>
    public class PlayerVitals : MonoBehaviour
    {
        public const float MaxStamina = 100f, MaxSanity = 100f;

        public float Stamina { get; private set; } = MaxStamina;
        public float Sanity { get; private set; } = MaxSanity;
        public bool Winded { get; private set; }
        public bool BreakingDown { get; private set; }
        /// <summary>A soda's sugar rush: sprinting costs nothing until this time.</summary>
        public float freeSprintUntil;
        /// <summary>A cracked glow stick held by this player keeps the dark off them.</summary>
        public Light carriedLight;

        RemasteredPlayerController body;
        AudioSource pant;
        float sinceSprint, nextPantNoise, darkCheck;
        bool dark;

        void Awake()
        {
            body = GetComponent<RemasteredPlayerController>();
            pant = gameObject.AddComponent<AudioSource>();
            pant.loop = true; pant.playOnAwake = false; pant.spatialBlend = 1f; pant.minDistance = 1.5f; pant.maxDistance = 14f; pant.volume = 0f;
        }

        public void AddSanity(float amount) { Sanity = Mathf.Clamp(Sanity + amount, 0f, MaxSanity); }
        public void RefillStamina() { Stamina = MaxStamina; Winded = false; if (body != null) body.sprintBlocked = false; }

        void Update()
        {
            if (body == null || body.IsDead) { if (pant.isPlaying) pant.Stop(); return; }
            float dt = Time.deltaTime;
            var nb = GetComponent<Net.NetBody>();
            if (!Net.NetBody.Mine(body))
            {
                // someone else's player: their machine keeps their stamina and sanity; here we only hear them pant
                Pant(nb != null && nb.Winded ? 1f : 0f, dt);
                return;
            }

            // ---------------- stamina
            bool sprinting = body.isControlled && !body.IsFrozen && body.IsSprinting;
            if (sprinting && Time.time > freeSprintUntil) { Stamina -= 9f * dt; sinceSprint = 0f; }
            else
            {
                sinceSprint += dt;
                var cc = GetComponent<CharacterController>();
                bool still = cc == null || !cc.enabled || new Vector2(cc.velocity.x, cc.velocity.z).sqrMagnitude < 0.2f;
                if (sinceSprint > 0.8f) Stamina += (Winded ? 10f : still ? 22f : 14f) * dt;   // winded: a good few seconds of walking before they can run
            }
            Stamina = Mathf.Clamp(Stamina, 0f, MaxStamina);
            if (!Winded && Stamina <= 0f) Winded = true;
            if (Winded && Stamina >= 45f) Winded = false;
            body.sprintBlocked = Winded || BreakingDown;
            if (nb != null) nb.Winded = Winded;

            // panting: loud while winded, fading as they get their breath back; monsters within earshot come to look
            Pant(Winded ? 1f : Mathf.Clamp01((60f - Stamina) / 60f) * 0.5f, dt);
            if (Winded && Time.time > nextPantNoise) { nextPantNoise = Time.time + 2f; Net.NetWorld.Noise(transform.position, 7f); }

            SanityTick(dt);
        }

        AudioSource heart, choke;
        void Pant(float wantPant, float dt)
        {
            if (pant.clip == null) pant.clip = UserSfx.Get("breathing_fast") ?? (SfxBank.Instance != null ? SfxBank.Instance.breathing : null);
            // worn out: the heart pounding under the breath
            if (heart == null)
            {
                heart = gameObject.AddComponent<AudioSource>(); heart.loop = true; heart.playOnAwake = false; heart.clip = UserSfx.Get("heart_breath");
                heart.rolloffMode = AudioRolloffMode.Linear; heart.maxDistance = 12f;
            }
            heart.spatialBlend = PlayerParty.Instance != null && PlayerParty.Instance.Current == body ? 0f : 1f;
            heart.volume = Mathf.MoveTowards(heart.volume, wantPant > 0.6f ? 0.6f : 0f, dt * 0.5f);
            if (heart.clip != null) { if (heart.volume > 0.02f && !heart.isPlaying) heart.Play(); else if (heart.volume <= 0.02f && heart.isPlaying) heart.Stop(); }
            pant.volume = Mathf.MoveTowards(pant.volume, wantPant * 0.85f, dt * 0.8f);
            pant.spatialBlend = PlayerParty.Instance != null && PlayerParty.Instance.Current == body ? 0f : 1f;
            if (pant.clip != null)
            {
                if (pant.volume > 0.02f && !pant.isPlaying) pant.Play();
                else if (pant.volume <= 0.02f && pant.isPlaying) pant.Stop();
            }
        }

        void SanityTick(float dt)
        {
            if (BreakingDown) return;
            if (Time.time > darkCheck) { darkCheck = Time.time + 0.5f; dark = InTheDark(); }
            float drain = 0f;
            var floor = PartyFloor.Instance;
            bool onParty = floor != null && Mathf.Abs(transform.position.y - 4.2f) < 2.5f;
            bool chaos = onParty && floor.Current == PartyFloor.Phase.Chaos;
            if (chaos) drain += PartyHideSpot.HiddenIn(body) != null ? 0.5f : 1.0f;
            float near = NearestHunter();
            if (near < 10f) drain += Mathf.Lerp(3f, 0.8f, near / 10f);
            if (dark) drain += 0.45f;
            // the place wears on you all the time: only a tonic (or sweets) wins it back
            drain += 0.05f;
            Sanity -= drain * dt;
            Sanity = Mathf.Clamp(Sanity, 0f, MaxSanity);
            // a monster on you and your mind going: the choking starts so that it ends as the last of it does
            var clip = UserSfx.Get("suffocation");
            if (clip != null && near < 10f && drain > 0.5f && Sanity / drain <= clip.length && (choke == null || !choke.isPlaying))
            {
                if (choke == null) { choke = gameObject.AddComponent<AudioSource>(); choke.playOnAwake = false; choke.spatialBlend = 0f; }
                choke.clip = clip; choke.time = Mathf.Clamp(clip.length - Sanity / drain, 0f, clip.length - 0.1f); choke.volume = 0.9f; choke.Play();
            }
            else if (choke != null && choke.isPlaying && near >= 12f) choke.Stop();
            if (Sanity <= 0f)
            {
                // with something right on them, losing their mind is the end; alone, they break down and come back
                if (near < 8f && PlayerParty.Instance != null) PlayerParty.Instance.KillPlayer(body, "lost their mind as it closed in");
                else StartCoroutine(Breakdown());
            }
        }

        // a teammate dying where they could see it
        public void Witnessed(Vector3 where)
        {
            if (body == null || body.IsDead) return;
            if (Vector3.Distance(where, transform.position) < 15f) AddSanity(-20f);
        }

        System.Collections.IEnumerator Breakdown()
        {
            BreakingDown = true;
            var party = PlayerParty.Instance;
            var bank = SfxBank.Instance;
            int i = party != null ? System.Array.IndexOf(party.players, body) : -1;
            if (party != null) party.Flash("Player " + (i + 1) + " is losing it...", 3f);
            if (bank != null) SfxBank.At(bank.fallScream, transform.position + Vector3.up * 1.5f, 1f, 0.8f, 35f);
            Net.NetWorld.Noise(transform.position, 25f);
            if (party != null && party.Current == body) ChaosFx.Kick(1.3f);
            body.SetFrozen(true);
            yield return new WaitForSeconds(2.2f);
            if (!body.IsDead) body.SetFrozen(false);
            Sanity = 25f;
            BreakingDown = false;
        }

        float NearestHunter()
        {
            float best = 99f;
            foreach (var m in PartyMonster.All)
                if (m != null && m.Hunting) best = Mathf.Min(best, Vector3.Distance(m.transform.position, transform.position));
            foreach (var m in Campus.NailedMonster.All)
                if (m != null && (m.Current == Campus.NailedMonster.State.Chase || m.Current == Campus.NailedMonster.State.Alert || m.Current == Campus.NailedMonster.State.Attack))
                    best = Mathf.Min(best, Vector3.Distance(m.transform.position, transform.position));
            var g = Campus.CampusGiant.Instance;
            if (g != null && (g.Current == Campus.CampusGiant.Mode.Chase || g.Current == Campus.CampusGiant.Mode.Strike || g.Current == Campus.CampusGiant.Mode.Roar))
                best = Mathf.Min(best, Vector3.Distance(g.transform.position, transform.position) * 0.25f);   // it is huge: it fills your eyes from far off
            foreach (var m in NeedleMonster.All)
                if (m != null && (m.Current == NeedleMonster.State.Chase || m.Current == NeedleMonster.State.Alert || m.Current == NeedleMonster.State.Attack))
                    best = Mathf.Min(best, Vector3.Distance(m.transform.position, transform.position));
            return best;
        }

        // no working lamp reaching them, no torch on, no glow stick
        bool InTheDark()
        {
            if (carriedLight != null && carriedLight.enabled) return false;
            if (body.flashlightLight != null && body.flashlightLight.enabled) return false;
            Vector3 p = transform.position + Vector3.up * 1.2f;
            if (DarkZone.At(transform.position) != null) return true;
            foreach (var l in VitalsDirector.Lamps)
            {
                if (l == null || !l.isActiveAndEnabled || l.intensity < 0.3f) continue;
                float r = l.range * 0.8f;
                if ((l.transform.position - p).sqrMagnitude < r * r) return false;
            }
            return true;
        }
    }
}
