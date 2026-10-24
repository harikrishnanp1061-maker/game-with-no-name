using System.Collections.Generic;
using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// Floor 1's lighting mood, applied to the built lamps and ceiling when the scene loads (no rebuild needed). Lamps
    /// overlap a lot, so dimming single lamps barely shows: the mood is picked per zone instead (a room, or a 3 x 3 cell
    /// stretch of corridor). A dark zone has every lamp dead but one, which flickers low, and its ceiling panels are
    /// nearly out; a lit zone keeps bright lamps, half of which stutter now and then. Some ceiling panels flicker on
    /// their own everywhere. Seeded by zone, so the same areas are dark every run.
    /// </summary>
    public class FloorLightMood : MonoBehaviour
    {
        /// <summary>Share of ordinary rooms and corridor stretches that are dark.</summary>
        public static float darkShare = 0.15f;
        /// <summary>
        /// The scene's flat ambient (0.21 grey) lit every corner evenly, so lamps going out changed nothing. This lower
        /// ambient leaves the dark zones readable and lets the lamps throw real pools of light.
        /// </summary>
        public static Color ambient = new Color(0.19f, 0.2f, 0.22f);
        public static float reflections = 0.3f;
        /// <summary>A lit zone's lamps reach further and burn brighter to make up for the lost ambient.</summary>
        public static float litRange = 1.9f, litIntensity = 2.3f;
        /// <summary>Share of a lit zone's lamps that stutter now and then.</summary>
        public static float litFailingShare = 0.5f;
        /// <summary>Ceiling panels: glow left in a dark zone, and the share that flicker in a lit zone.</summary>
        public static float darkCeilingGlow = 0.12f, ceilingFlickerShare = 0.12f;

        static readonly string[] DarkRooms =
        {
            "Cold Storage", "Generator room", "Boiler room", "Water Pump Station", "Washrooms",
        };
        static readonly string[] LitRooms = { "Parking access" };   // the spawn room starts you in the light

        // one per scene load (the first load and every wipe's reload)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            Spawn();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => Spawn();
        }

        static void Spawn()
        {
            if (FindAnyObjectByType<FloorLightMood>() == null) new GameObject("FloorLightMood").AddComponent<FloorLightMood>();
        }

        class Panel { public Material mat; public Color baseEmission; public float level, seed, nextEvent, outUntil, stutterUntil; }
        readonly List<Panel> panels = new List<Panel>();
        bool reported;

        System.Collections.IEnumerator Start()
        {
            yield return null;   // after every other Start (the light optimiser collects its lamps first)
            int panelsDim, panelsFlicker;
            ApplyCeiling(out panelsDim, out panelsFlicker);
            while (true)
            {
                int dim, dead;
                bool found = ApplyLamps(out dim, out dead);
                if (!reported && PlayerParty.Instance != null)
                {
                    reported = true;
                    PlayerParty.Instance.Flash(found ? "[dev] floor 1 lighting: " + dim + " failing, " + dead + " dead lamps; " + panelsDim + " ceiling panels dark, " + panelsFlicker + " flickering"
                                                     : "[dev] floor 1 lighting: lamps NOT found", 5f);
                }
                yield return new WaitForSeconds(3f);   // picks up lamps that appear later
            }
        }

        void Update()
        {
            float t = Time.time;
            foreach (var p in panels)
            {
                if (t >= p.nextEvent)
                {
                    if (Random.value < 0.35f) p.outUntil = t + Random.Range(0.3f, 1.8f);
                    else p.stutterUntil = t + Random.Range(0.15f, 0.6f);
                    p.nextEvent = t + Random.Range(1.5f, 7f);
                }
                float k;
                if (t < p.outUntil) k = 0.02f;
                else if (t < p.stutterUntil) k = Random.value < 0.5f ? 0.05f : 1f;
                else k = 0.85f + 0.15f * Mathf.PerlinNoise(p.seed, t * 1.5f);
                p.mat.SetColor("_EmissionColor", p.baseEmission * (p.level * k));
            }
        }

        // ------------------------------------------------------------------ zones
        struct Zone { public int key; public bool dark, cold; }

        static Zone ZoneAt(Vector3 p)
        {
            var plan = LevelLayout.Plans[0];
            int col = Mathf.Clamp(Mathf.FloorToInt(p.x / 4f), 0, LevelLayout.Cols - 1);
            int row = Mathf.Clamp(LevelLayout.Rows - 1 - Mathf.FloorToInt(p.z / 4f), 0, LevelLayout.Rows - 1);
            int id = plan.zone[row, col];
            Room room = id >= 0 && id < plan.rooms.Count ? plan.rooms[id] : null;
            bool corridor = room == null || room == plan.corridor;
            var z = new Zone { key = corridor ? 10000 + (col / 3) * 100 + row / 3 : id };
            z.cold = room != null && room.name == "Cold Storage";
            bool forcedLit = !corridor && System.Array.IndexOf(LitRooms, room.name) >= 0;
            z.dark = !forcedLit && (!corridor && System.Array.IndexOf(DarkRooms, room.name) >= 0 || Hash(z.key) < darkShare);
            return z;
        }

        static Transform FloorChild(string name)
        {
            var env = GameObject.Find("HollowTower_Remastered_Environment");
            var floor = env != null ? env.transform.Find("Floor_1_Ground") : null;
            return floor != null ? floor.Find(name) : null;
        }

        // ------------------------------------------------------------------ lamps
        static bool ApplyLamps(out int dim, out int dead)
        {
            dim = 0; dead = 0;
            var fixtures = FloorChild("Lighting_Fixtures");
            if (fixtures == null) return false;
            bool partyOwnsAmbient = PartyFloor.Instance != null && PartyFloor.Instance.PlayerOnFloor;   // floor 2 sets its own
            if (!partyOwnsAmbient && (RenderSettings.ambientMode != UnityEngine.Rendering.AmbientMode.Flat || RenderSettings.ambientLight != ambient))
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = ambient;
                RenderSettings.reflectionIntensity = reflections;
                DynamicGI.UpdateEnvironment();
            }
            var keptOne = new HashSet<int>();   // dark zones that already have their one flickering lamp

            foreach (var l in fixtures.GetComponentsInChildren<Light>(true))
            {
                var existing = l.GetComponent<DyingLight>();
                if (existing != null) { if (existing.brightness <= 0f) dead++; else if (existing.brightness < 1f) dim++; continue; }

                Vector3 p = l.transform.position;
                var z = ZoneAt(p);
                float brightness; float outChance; Vector2 gap; bool failing = true;
                if (z.dark)
                {
                    if (keptOne.Add(z.key)) { brightness = z.cold ? 0.15f : 0.3f; outChance = 0.45f; gap = new Vector2(1.5f, 4.5f); }   // the one failing lamp
                    else { brightness = 0f; outChance = 0f; gap = Vector2.one; }                                                        // the rest dead
                }
                else
                {
                    l.range *= litRange;
                    brightness = litIntensity; outChance = 0.25f; gap = new Vector2(2f, 6f);
                    failing = Hash(p.x * 31f + p.z) < litFailingShare;
                }

                foreach (var f in l.GetComponents<RemasteredFlicker>()) Destroy(f);
                var d = l.gameObject.AddComponent<DyingLight>();
                d.brightness = brightness; d.outChance = outChance; d.eventGap = gap; d.failing = failing;
                d.tube = Tube(l);
                if (brightness <= 0f) dead++; else if (brightness < 1f) dim++;
            }
            return true;
        }

        static Renderer Tube(Light l)
        {
            var t = l.transform.parent != null ? l.transform.parent.Find("Fluorescent_Tube") : null;
            return t != null ? t.GetComponent<Renderer>() : null;
        }

        // ------------------------------------------------------------------ ceiling panels
        /// <summary>
        /// The small light squares on the ceiling are the ceiling slabs' emission map. In a dark zone they go nearly out
        /// (one shared dim material); elsewhere some slabs get their own material and flicker (Update).
        /// </summary>
        void ApplyCeiling(out int dimmed, out int flickering)
        {
            dimmed = 0; flickering = 0;
            var slabs = FloorChild("Slabs_And_Ceilings");
            if (slabs == null) return;
            Material dark = null;
            foreach (Transform s in slabs)
            {
                if (!s.name.StartsWith("CeilingSlab")) continue;
                var r = s.GetComponent<Renderer>();
                if (r == null || r.sharedMaterial == null || !r.sharedMaterial.HasProperty("_EmissionColor")) continue;
                Color baseEm = r.sharedMaterial.GetColor("_EmissionColor");
                var z = ZoneAt(s.position);
                float roll = Hash(s.position.x * 17f + s.position.z * 3f);
                if (z.dark)
                {
                    if (roll < 0.15f && !z.cold) { AddPanel(r, baseEm, darkCeilingGlow * 2.5f); flickering++; }   // the odd panel still sputtering in the dark
                    else
                    {
                        if (dark == null) { dark = new Material(r.sharedMaterial) { name = "Ceiling_Dark" }; dark.SetColor("_EmissionColor", baseEm * darkCeilingGlow); }
                        r.sharedMaterial = dark;
                    }
                    dimmed++;
                }
                else if (roll < ceilingFlickerShare) { AddPanel(r, baseEm, 1f); flickering++; }
            }
        }

        void AddPanel(Renderer r, Color baseEm, float level)
        {
            var m = new Material(r.sharedMaterial) { name = "Ceiling_Flicker" };
            r.sharedMaterial = m;
            panels.Add(new Panel { mat = m, baseEmission = baseEm, level = level, seed = Random.value * 100f, nextEvent = Time.time + Random.Range(0.5f, 6f) });
        }

        static float Hash(float key)
        {
            unchecked
            {
                uint h = (uint)Mathf.RoundToInt(key * 10f) * 2654435761u;
                h ^= h >> 13; h *= 0x5bd1e995u; h ^= h >> 15;
                return (h & 0xffffff) / (float)0x1000000;
            }
        }
    }
}
