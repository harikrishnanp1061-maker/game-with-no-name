using System.Collections.Generic;
using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// Each floor's mains. A ghost that starts hunting you can kill the power on its floor: the ceiling lamps
    /// stutter and die (battery emergency lamps and the vault's torches stay lit) until you restart that floor's
    /// generator (one per floor, marked on the map). The dark hides you better, but you can't see them either.
    /// </summary>
    public static class PowerGrid
    {
        public const int Floors = 4;
        /// <summary>No cut in the first minute, and at least this long between cuts.</summary>
        public const float FirstCutAfter = 60f, Cooldown = 90f;
        /// <summary>How long the lamps stutter while the power dies or comes back.</summary>
        public const float Transition = 1.4f;

        static readonly bool[] off = new bool[Floors];
        static readonly float[] changedAt = new float[Floors];
        static readonly List<Material>[] fixtureMats = new List<Material>[Floors];
        static readonly Dictionary<Material, Color> emission = new Dictionary<Material, Color>();
        static readonly Dictionary<Material, Material[]> clones = new Dictionary<Material, Material[]>();
        static readonly List<Renderer>[] flickerFixtures = new List<Renderer>[Floors];
        public static readonly Generator[] Generators = new Generator[Floors];
        static float lastCut = -999f;
        static MaterialPropertyBlock dark;

        /// <summary>Set false to stop ghosts cutting the power (benchmark, most play test sections).</summary>
        public static bool AutoCuts = true;
        public static int Cuts { get; private set; }

        public static void Reset()
        {
            for (int f = 0; f < Floors; f++)
            {
                off[f] = false;
                changedAt[f] = -999f;
                fixtureMats[f] = new List<Material>();
                flickerFixtures[f] = new List<Renderer>();
                Generators[f] = null;
            }
            emission.Clear();
            clones.Clear();
            lastCut = -999f;
            Cuts = 0;
            AutoCuts = true;
        }

        public static bool IsOn(int floor) { return floor < 0 || floor >= Floors || !off[floor]; }
        public static bool AnyOut { get { for (int f = 0; f < Floors; f++) if (off[f]) return true; return false; } }

        /// <summary>The ceiling-fixture material for this floor: a per-floor copy, so one floor's lamps can go dark alone.</summary>
        public static Material FixtureFor(Material shared, int floor)
        {
            if (shared == null || floor < 0 || floor >= Floors) return shared;
            Material[] perFloor;
            if (!clones.TryGetValue(shared, out perFloor)) clones[shared] = perFloor = new Material[Floors];
            Material m;
            if ((m = perFloor[floor]) != null) return m;
            m = new Material(shared) { name = shared.name + " F" + (floor + 1) };
            perFloor[floor] = m;
            fixtureMats[floor].Add(m);
            emission[m] = m.HasProperty(Gfx.EmissiveId) ? m.GetColor(Gfx.EmissiveId) : Color.black;
            return m;
        }

        /// <summary>A flickering lamp's own fixture (it keeps its own emission in a property block).</summary>
        public static void RegisterFlickerFixture(Renderer r, int floor)
        {
            if (r != null && floor >= 0 && floor < Floors) flickerFixtures[floor].Add(r);
        }

        /// <summary>Is this lamp lit right now? During a transition the floor's lamps stutter.</summary>
        public static bool LampLive(TowerLight tl)
        {
            if (!tl.electric || tl.floor < 0 || tl.floor >= Floors) return true;
            float since = Time.time - changedAt[tl.floor];
            if (since < Transition)
            {
                // Stutter in steps of ~0.1 s, each lamp on its own pattern, settling towards the new state.
                float n = Mathf.PerlinNoise((tl.light.transform.position.x * 1.37f + tl.light.transform.position.z * 0.71f) % 97f, Mathf.Floor(Time.time * 10f) * 0.71f);
                float settle = since / Transition;
                return off[tl.floor] ? n > 0.35f + settle * 0.6f : n < 0.25f + settle * 0.75f;
            }
            return !off[tl.floor];
        }

        public static bool InTransition(int floor)
        {
            return floor >= 0 && floor < Floors && Time.time - changedAt[floor] < Transition + 0.1f;
        }

        /// <summary>A ghost tries to kill this floor's power. Returns true if it did.</summary>
        public static bool TryCut(int floor, Vector3 at)
        {
            if (!AutoCuts || floor < 0 || floor >= Floors || off[floor]) return false;
            if (Time.timeSinceLevelLoad < FirstCutAfter || Time.time - lastCut < Cooldown) return false;
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying) return false;
            Cut(floor, at);
            return true;
        }

        /// <summary>
        /// You shot a ghost on floor 2 or 1: the lights die there straight away (no warm-up or cooldown), and stay off
        /// until you restart that floor's generator. Returns true if this cut the power.
        /// </summary>
        public static bool ShotCut(int floor, Vector3 at)
        {
            if (!AutoCuts || floor < 0 || floor >= Floors || off[floor]) return false;
            var gm = GameManager.Instance;
            if (gm == null || !gm.IsPlaying) return false;
            Cut(floor, at);
            if (gm != null) gm.Toast("The ghost shrieked and the lights died! Restart this floor's generator (map: M).");
            return true;
        }

        public static void Cut(int floor, Vector3 at)
        {
            if (floor < 0 || floor >= Floors || off[floor]) return;
            off[floor] = true;
            changedAt[floor] = Time.time;
            lastCut = Time.time;
            Cuts++;
            Sfx.At(Sfx.Drone, at, 0.9f, 0.35f, 40f);   // the hum winding down
            Sfx.At(Sfx.Click, at, 1f, 0.5f, 40f);
            var gm = GameManager.Instance;
            var p = PlayerController.Instance;
            if (gm != null && p != null && p.Floor == floor)
                gm.Toast("The ghost killed the power! Restart this floor's generator (map: M).");
            if (gm != null) gm.RefreshLights();
        }

        public static void Restore(int floor)
        {
            if (floor < 0 || floor >= Floors || !off[floor]) return;
            off[floor] = false;
            changedAt[floor] = Time.time;
            lastCut = Time.time;   // the cooldown counts from when the lights came back
            var gm = GameManager.Instance;
            if (gm != null) gm.RefreshLights();
        }

        /// <summary>Called every frame by the game manager: fixture glow follows the lamps.</summary>
        public static void Tick()
        {
            for (int f = 0; f < Floors; f++)
            {
                float since = Time.time - changedAt[f];
                if (since > Transition + 0.5f) continue;
                float k;
                if (since < Transition)
                {
                    float n = Mathf.PerlinNoise(f * 3.1f, Mathf.Floor(Time.time * 10f) * 0.71f);
                    k = off[f] ? (n > 0.35f + since / Transition * 0.6f ? 1f : 0.05f) : (n < 0.25f + since / Transition * 0.75f ? 1f : 0.05f);
                }
                else k = off[f] ? 0f : 1f;
                foreach (var m in fixtureMats[f])
                    if (m != null) Gfx.SetEmissionNits(m, emission[m] * k);
                if (dark == null) dark = new MaterialPropertyBlock();
                foreach (var r in flickerFixtures[f])
                {
                    if (r == null) continue;
                    if (k > 0.5f) r.SetPropertyBlock(null);   // FlickerLight takes over again
                    else { r.GetPropertyBlock(dark); dark.SetColor(Gfx.EmissiveId, Color.black); r.SetPropertyBlock(dark); }
                }
                var gm = GameManager.Instance;
                if (gm != null && since < Transition + 0.2f) gm.RefreshLights();
            }
        }
    }

    /// <summary>
    /// A floor's backup generator: a squat green machine with a status lamp (red when the floor is dark).
    /// Press E beside it to crank it back to life; it roars, which every zombie nearby hears.
    /// </summary>
    public class Generator : MonoBehaviour
    {
        public int floor;
        public bool PlayerNear { get; private set; }
        public bool Starting { get { return startT >= 0f; } }
        public const float StartTime = 2.2f;

        Renderer statusLamp;
        Light statusLight;
        Material lampOn, lampOff;
        Transform flywheel;
        AudioSource hum;
        float startT = -1f;

        public static Generator Create(Transform parent, Vector3 floorPos, Vector3 facing, int floor)
        {
            var root = new GameObject("Generator F" + (floor + 1));
            root.transform.SetParent(parent, false);
            root.transform.position = floorPos;
            root.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
            root.AddComponent<NoMerge>();
            var t = root.transform;
            var body = Gfx.Lit(new Color(0.16f, 0.24f, 0.15f), 0.35f, 0.4f);
            var metal = Gfx.Lit(new Color(0.35f, 0.35f, 0.36f), 0.5f, 0.9f);
            var black = Gfx.Lit(new Color(0.04f, 0.04f, 0.04f), 0.3f);
            var hazard = Gfx.Lit(new Color(0.85f, 0.65f, 0.08f), 0.3f);
            Gfx.Box("Skid", t, new Vector3(0f, 0.06f, 0f), new Vector3(1.3f, 0.12f, 0.75f), black);
            Gfx.Box("Housing", t, new Vector3(0f, 0.55f, 0f), new Vector3(1.15f, 0.86f, 0.62f), body);
            Gfx.Box("Stripe", t, new Vector3(0f, 0.2f, 0.315f), new Vector3(1.15f, 0.08f, 0.01f), hazard, false);
            Gfx.Box("Panel", t, new Vector3(0.28f, 0.62f, 0.32f), new Vector3(0.42f, 0.4f, 0.03f), black, false);
            var fw = Gfx.Prim(PrimitiveType.Cylinder, "Flywheel", t, new Vector3(-0.66f, 0.55f, 0f), new Vector3(0.5f, 0.04f, 0.5f), metal, false);
            fw.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Gfx.Prim(PrimitiveType.Cylinder, "Exhaust", t, new Vector3(-0.35f, 1.15f, -0.15f), new Vector3(0.09f, 0.22f, 0.09f), metal, false);
            Gfx.Box("Handle", t, new Vector3(0.28f, 0.95f, 0.36f), new Vector3(0.3f, 0.04f, 0.04f), metal, false);
            var g = root.AddComponent<Generator>();
            g.floor = floor;
            g.flywheel = fw.transform;
            g.lampOn = Gfx.Emissive(new Color(0.1f, 0.6f, 0.15f), new Color(0.5f, 6f, 0.8f));
            g.lampOff = Gfx.Emissive(new Color(0.6f, 0.05f, 0.03f), new Color(7f, 0.3f, 0.1f));
            g.statusLamp = Gfx.Prim(PrimitiveType.Sphere, "StatusLamp", t, new Vector3(0.16f, 0.72f, 0.34f), Vector3.one * 0.07f, g.lampOn, false).GetComponent<Renderer>();
            // Its own little lamp (not part of the mains) so you can find it in the dark.
            g.statusLight = Gfx.PointLight(t, new Vector3(0.16f, 0.75f, 0.45f), new Color(0.4f, 1f, 0.45f), 0.03f, 2.5f, LightShadows.None);
            foreach (var r in root.GetComponentsInChildren<Renderer>())
                if (r != g.statusLamp) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            g.statusLamp.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            g.hum = root.AddComponent<AudioSource>();
            g.hum.clip = Sfx.Drone;
            g.hum.loop = true;
            g.hum.spatialBlend = 1f;
            g.hum.rolloffMode = AudioRolloffMode.Linear;
            g.hum.minDistance = 1f;
            g.hum.maxDistance = 9f;
            g.hum.pitch = 1.7f;
            g.hum.volume = 0.35f;
            g.hum.Play();
            PowerGrid.Generators[floor] = g;
            return g;
        }

        /// <summary>Where you stand to use it.</summary>
        public Vector3 UsePosition { get { return transform.position + transform.forward * 1.1f; } }

        /// <summary>Start the restart sequence (the E key, or the play test).</summary>
        public bool Use()
        {
            if (PowerGrid.IsOn(floor) || Starting) return false;
            startT = 0f;
            Sfx.At(Sfx.Click, transform.position, 1f, 0.7f);
            Sfx.At(Sfx.Roar, transform.position, 0.45f, 0.45f, 25f); // the starter grinding
            return true;
        }

        void Update()
        {
            bool on = PowerGrid.IsOn(floor);
            var p = PlayerController.Instance;
            var gm = GameManager.Instance;
            PlayerNear = p != null && !p.IsDead && !p.IsHidden && Mathf.Abs(p.transform.position.y - transform.position.y) < 1.5f &&
                         Vector3.Distance(new Vector3(p.transform.position.x, transform.position.y, p.transform.position.z), transform.position) < 2.1f;
            if (PlayerNear && !on && gm != null && gm.IsPlaying && !gm.IsPaused && InputWrap.Down(KeyCode.E) && p.NearbyHidingSpot() == null) Use();

            if (Starting)
            {
                startT += Time.deltaTime;
                if (flywheel != null) flywheel.Rotate(0f, startT * 900f * Time.deltaTime, 0f, Space.Self);
                transform.GetChild(1).localPosition = new Vector3(0f, 0.55f, 0f) + Random.insideUnitSphere * 0.006f;
                if (startT >= StartTime)
                {
                    startT = -1f;
                    PowerGrid.Restore(floor);
                    NoiseSystem.Emit(transform.position, 18f);   // loud: the dead hear it
                    Sfx.At(Sfx.Roar, transform.position, 0.6f, 0.6f, 30f);
                    if (gm != null) gm.Toast("Power restored on floor " + (floor + 1) + ". That was loud...");
                }
            }
            else if (on && flywheel != null) flywheel.Rotate(0f, 240f * Time.deltaTime, 0f, Space.Self);

            statusLamp.sharedMaterial = on ? lampOn : lampOff;
            statusLight.color = on ? new Color(0.4f, 1f, 0.45f) : new Color(1f, 0.15f, 0.08f);
            statusLight.intensity = (on ? 0.03f : 0.06f * (0.4f + 0.6f * Mathf.Abs(Mathf.Sin(Time.time * 3f)))) * Gfx.Lumens;
            hum.volume = on || Starting ? 0.35f : 0f;
        }
    }
}
