using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// What an item does when its carrier presses Q with it selected (sits next to the PartyItem that makes it a pickup):
    ///   Sweets      a bag of party sweets: +40 sanity.
    ///   Soda        a can of fizzy pop: stamina full, and ten seconds of sprinting that costs nothing.
    ///   Popper      a party popper, thrown ahead: a bang where it lands that every monster within 25 m runs to.
    ///   GlowStick   cracked, it glows green in the hand for two minutes (light, and no losing your mind in the dark);
    ///               dropped, it keeps glowing on the floor and marks the spot. Gone when it fades.
    /// </summary>
    public class UsableItem : MonoBehaviour, Net.INetAction
    {
        public enum Kind { Sweets, Soda, Popper, GlowStick }
        public Kind kind;

        /// <summary>Every item, carried (inactive) or not: VitalsDirector ticks the glow sticks.</summary>
        public static readonly System.Collections.Generic.List<UsableItem> All = new System.Collections.Generic.List<UsableItem>();
        void Awake() { if (!All.Contains(this)) All.Add(this); }

        public float glowSeconds = 120f;
        float glowLeft;
        Light glow;
        bool cracked;

        public string UseHint
        {
            get
            {
                switch (kind)
                {
                    case Kind.Sweets: return "[Q] eat - calms you";
                    case Kind.Soda: return "[Q] drink - get your wind back";
                    case Kind.Popper: return "[Q] throw - draws them away";
                    default: return cracked ? "glowing (" + Mathf.CeilToInt(glowLeft) + "s)" : "[Q] crack - light";
                }
            }
        }

        /// <summary>The steered player presses Q: worked out here (where a popper lands depends on where they look),
        /// then done the same on every machine.</summary>
        public void UseBy(RemasteredPlayerController body, int player)
        {
            Net.NetWorld.Act(this, 1, player, 0, kind == Kind.Popper ? Landing(body) : Vector3.zero);
        }

        public bool NetApply(int action, int a, int b, Vector3 v)
        {
            var party = PlayerParty.Instance;
            if (action != 1 || party == null || a < 0 || a >= party.players.Length || party.players[a] == null) return false;
            var body = party.players[a];
            var item = GetComponent<PartyItem>();
            if (item == null || item.Holder == null || item.Holder.gameObject != body.gameObject) return false;
            if (Use(body, v)) { item.Holder.Remove(item); Destroy(gameObject); }
            return true;
        }

        // a thrown popper lands up to 9 m ahead, short of the first wall
        static Vector3 Landing(RemasteredPlayerController body)
        {
            var party = PlayerParty.Instance;
            var cam = party != null && party.playerCamera != null ? party.playerCamera.transform : body.transform;
            Vector3 dir = cam.forward; dir.y = Mathf.Max(dir.y, -0.2f);
            Vector3 from = body.transform.position + Vector3.up * 1.5f;
            Vector3 at = from + dir.normalized * 9f;
            if (Physics.Raycast(from, dir.normalized, out RaycastHit hit, 9f, ~0, QueryTriggerInteraction.Ignore)) at = hit.point - dir.normalized * 0.3f;
            if (Physics.Raycast(at, Vector3.down, out RaycastHit down, 4f, ~0, QueryTriggerInteraction.Ignore)) at = down.point;
            return at;
        }

        /// <summary>Uses it; returns true if it is used up (taken out of the inventory). Runs on every machine: what it
        /// does to the player's own mind and breath only on the machine that steers them.</summary>
        bool Use(RemasteredPlayerController body, Vector3 target)
        {
            bool mine = Net.NetBody.Mine(body);
            var vitals = body.GetComponent<PlayerVitals>();
            var party = PlayerParty.Instance;
            switch (kind)
            {
                case Kind.Sweets:
                    if (vitals != null && mine) vitals.AddSanity(40f);
                    SfxBank.At(ItemSfx.Rustle, body.transform.position + Vector3.up * 1.5f, 0.7f, Random.Range(0.95f, 1.1f), 6f);
                    if (party != null) party.Flash("Sugar. The shaking stops, a little.", 2f);
                    return true;
                case Kind.Soda:
                    if (vitals != null && mine) { vitals.RefillStamina(); vitals.freeSprintUntil = Time.time + 10f; }
                    SfxBank.At(ItemSfx.Fizz, body.transform.position + Vector3.up * 1.5f, 0.7f, 1f, 8f);
                    if (party != null) party.Flash("Fizzy pop - you can run again", 2f);
                    return true;
                case Kind.Popper:
                {
                    Vector3 at = target;
                    SfxBank.At(ItemSfx.Pop, at + Vector3.up * 0.3f, 1f, Random.Range(0.95f, 1.05f), 40f);
                    Confetti(at);
                    if (Net.Net.IsAuthority) { PartyMonster.HeardAt(at, 25f, 5f); NeedleMonster.Hear(at, 25f); }
                    if (party != null) party.Flash("POP - that should pull them away", 2f);
                    return true;
                }
                default:
                    if (cracked) return false;
                    cracked = true;
                    glowLeft = glowSeconds;
                    SfxBank.At(ItemSfx.Crack, body.transform.position + Vector3.up * 1.3f, 0.6f, 1f, 6f);
                    if (party != null) party.Flash("The glow stick cracks green", 2f);
                    return false;
            }
        }

        void Confetti(Vector3 at)
        {
            var go = new GameObject("Confetti");
            go.transform.position = at + Vector3.up * 0.4f;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.3f; main.loop = false; main.startLifetime = 2.5f; main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f); main.gravityModifier = 0.35f; main.maxParticles = 120;
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.3f, 0.5f), new Color(0.3f, 0.8f, 1f));
            var em = ps.emission; em.rateOverTime = 0f; em.SetBursts(new[] { new ParticleSystem.Burst(0f, 110) });
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Hemisphere; shape.radius = 0.2f;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            ps.Play();
            Destroy(go, 4f);
        }

        // the glow stick: lit while cracked - on whoever carries it, or on the floor where it was dropped. Called every
        // frame by VitalsDirector (a carried item's object is switched off, so it can't update itself)
        public void Tick()
        {
            if (!cracked) return;
            glowLeft -= Time.deltaTime;
            var item = GetComponent<PartyItem>();
            var holder = item != null && item.Holder != null ? item.Holder.GetComponent<RemasteredPlayerController>() : null;
            if (glow == null)
            {
                var go = new GameObject("GlowStickLight");
                glow = go.AddComponent<Light>();
                glow.type = LightType.Point; glow.color = new Color(0.35f, 1f, 0.45f); glow.range = 7f; glow.shadows = LightShadows.None;
            }
            float fade = Mathf.Clamp01(glowLeft / 15f);
            glow.intensity = 2.2f * fade;
            if (holder != null && !holder.IsDead)
            {
                glow.transform.position = holder.transform.position + Vector3.up * 1.1f + holder.transform.forward * 0.35f;
                var v = holder.GetComponent<PlayerVitals>();
                if (v != null) v.carriedLight = glow;
            }
            else glow.transform.position = transform.position + Vector3.up * 0.2f;
            glow.enabled = holder != null || gameObject.activeInHierarchy;
            if (holder == null) foreach (var p in PlayerParty.Instance != null ? PlayerParty.Instance.players : new RemasteredPlayerController[0])
                { var v = p != null ? p.GetComponent<PlayerVitals>() : null; if (v != null && v.carriedLight == glow) v.carriedLight = null; }
            if (glowLeft <= 0f)
            {
                Destroy(glow.gameObject);
                if (holder != null) { holder.GetComponent<PlayerInventory>().Remove(item); if (PlayerParty.Instance != null) PlayerParty.Instance.Flash("The glow stick has faded", 2f); }
                Destroy(gameObject);
            }
        }

        void OnDestroy() { All.Remove(this); if (glow != null) Destroy(glow.gameObject); }
    }
}
