using System.Collections.Generic;
using UnityEngine;

namespace HollowTower
{
    /// <summary>Flickering ceiling lights, pulsing emergency lamps and fire.</summary>
    public class FlickerLight : MonoBehaviour
    {
        enum Mode { Flicker, Pulse, Fire }

        Mode mode = Mode.Flicker;
        Light lamp;
        float baseIntensity;
        Renderer fixture;
        Transform flame;
        Vector3 flameScale;
        float nextBurst, burstEnd, seed;
        MaterialPropertyBlock block;
        Color fixtureEmission;

        void Awake()
        {
            lamp = GetComponent<Light>();
            baseIntensity = lamp != null ? lamp.intensity : 1f;
            seed = Random.value * 100f;
            nextBurst = Time.time + Random.Range(1f, 6f);
        }

        public void Setup(Renderer fixtureRenderer, bool unused)
        {
            mode = Mode.Flicker;
            fixture = fixtureRenderer;
            if (fixture != null)
            {
                block = new MaterialPropertyBlock();
                fixtureEmission = fixture.sharedMaterial.HasProperty(Gfx.EmissiveId) ? fixture.sharedMaterial.GetColor(Gfx.EmissiveId) : Color.white;
            }
        }

        public void SetupPulse() { mode = Mode.Pulse; }

        public void SetupFire(Transform flameTransform)
        {
            mode = Mode.Fire;
            flame = flameTransform;
            if (flame != null) flameScale = flame.localScale;
        }

        void Update()
        {
            if (lamp == null || !lamp.enabled) return;
            float t = Time.time;
            switch (mode)
            {
                case Mode.Pulse:
                    lamp.intensity = baseIntensity * (0.25f + 0.75f * Mathf.Abs(Mathf.Sin(t * 2.2f + seed)));
                    break;
                case Mode.Fire:
                    float n = Mathf.PerlinNoise(seed, t * 7f);
                    lamp.intensity = baseIntensity * (0.7f + 0.5f * n);
                    if (flame != null)
                        flame.localScale = new Vector3(flameScale.x * (0.9f + 0.2f * n), flameScale.y * (0.8f + 0.6f * Mathf.PerlinNoise(t * 9f, seed)), flameScale.z * (0.9f + 0.2f * n));
                    break;
                default:
                    float k = 1f;
                    if (t > nextBurst)
                    {
                        burstEnd = t + Random.Range(0.15f, 0.6f);
                        nextBurst = burstEnd + Random.Range(6f, 16f);
                    }
                    // A short stutter that steps ~12 times a second (not every frame), and never fully dark,
                    // so it reads as a failing tube rather than the whole screen strobing.
                    if (t < burstEnd) k = Mathf.PerlinNoise(seed, Mathf.Floor(t * 12f) * 0.37f) < 0.45f ? 0.3f : 0.85f;
                    lamp.intensity = baseIntensity * k;
                    if (fixture != null)
                    {
                        fixture.GetPropertyBlock(block);
                        block.SetColor(Gfx.EmissiveId, fixtureEmission * k);
                        fixture.SetPropertyBlock(block);
                    }
                    break;
            }
        }
    }

    /// <summary>Makes the blood-red wall writing breathe so it catches the eye in the dark.</summary>
    public class HintGlow : MonoBehaviour
    {
        static readonly int MapId = Shader.PropertyToID("_UnlitColorMap");
        TextMesh text;
        Renderer rend;
        Color baseEmission;

        void Awake()
        {
            text = GetComponent<TextMesh>();
            rend = GetComponent<Renderer>();
            if (rend != null && rend.sharedMaterial != null) baseEmission = rend.sharedMaterial.GetColor(Gfx.EmissiveId);
        }

        void Update()
        {
            if (text == null || rend == null || rend.sharedMaterial == null) return;
            var m = rend.sharedMaterial;
            // Dynamic font atlases can be rebuilt at runtime; keep our material pointing at the live one.
            if (text.font != null && m.GetTexture(MapId) != text.font.material.mainTexture) m.SetTexture(MapId, text.font.material.mainTexture);
            float k = 0.6f + 0.4f * Mathf.Sin(Time.time * 1.7f);
            m.SetColor(Gfx.EmissiveId, baseEmission * (0.7f + 0.6f * k));
        }
    }

    /// <summary>
    /// Somewhere to hide (E): lockers, hotel wardrobes, toilet stalls, under a teacher's desk (crouch first),
    /// or - safest of all - a stone seat in the vault, where you also slowly heal.
    /// </summary>
    public class HidingSpot : MonoBehaviour
    {
        public enum Kind { Locker, Wardrobe, Stall, Desk, Seat }

        public static readonly List<HidingSpot> All = new List<HidingSpot>();

        public Kind kind;
        public bool Occupied { get; set; }
        Transform door;
        float doorAngle, doorTarget;

        public float EyeHeight { get { return kind == Kind.Desk ? 0.62f : kind == Kind.Seat ? 1.12f : kind == Kind.Stall ? 1.25f : 1.6f; } }
        public bool RequiresCrouch { get { return kind == Kind.Desk; } }
        /// <summary>Seats in the vault: enemies can't get to you there.</summary>
        public bool Safe { get { return kind == Kind.Seat; } }
        public Vector3 InsidePosition
        {
            get
            {
                switch (kind)
                {
                    case Kind.Desk: return transform.position - transform.forward * 0.1f + Vector3.up * 0.02f;
                    case Kind.Seat: return transform.position + Vector3.up * 0.02f;
                    case Kind.Stall: return transform.position - transform.forward * 0.2f + Vector3.up * 0.02f;
                    default: return transform.position + Vector3.up * 0.02f;
                }
            }
        }
        public Vector3 ExitPosition { get { return transform.position + transform.forward * (kind == Kind.Stall ? 1.25f : kind == Kind.Desk ? 1.1f : 1.0f) + Vector3.up * 0.05f; } }
        public Quaternion ViewRotation { get { return Quaternion.LookRotation(transform.forward, Vector3.up); } }
        public string Verb
        {
            get
            {
                switch (kind)
                {
                    case Kind.Wardrobe: return "Hide in the wardrobe";
                    case Kind.Stall: return "Hide in the stall";
                    case Kind.Desk: return "Crawl under the desk";
                    case Kind.Seat: return "Sit and rest";
                    default: return "Hide in locker";
                }
            }
        }

        public static HidingSpot Create(Kind kind, Transform parent, Vector3 position, Vector3 facing, Mats m)
        {
            var root = new GameObject(kind.ToString());
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.transform.rotation = Quaternion.LookRotation(facing, Vector3.up);
            var spot = root.AddComponent<HidingSpot>();
            spot.kind = kind;
            var t = root.transform;
            switch (kind)
            {
                case Kind.Locker: spot.door = Cabinet(t, 0.95f, 2.1f, 0.7f, m.locker, m.steel, true); break;
                case Kind.Wardrobe: spot.door = Cabinet(t, 1.1f, 2.15f, 0.72f, m.darkWood, m.brass, false); break;
                case Kind.Stall: spot.door = Stall(t, m); break;
                case Kind.Desk: TeacherDesk(t, m); break;
                case Kind.Seat: Seat(t, m); break;
            }
            return spot;
        }

        static Transform Cabinet(Transform t, float w, float h, float d, Material body, Material trim, bool vents)
        {
            float hw = w * 0.5f, hd = d * 0.5f;
            Gfx.Box("Back", t, new Vector3(0f, h * 0.5f, -hd + 0.02f), new Vector3(w, h, 0.04f), body, false);
            Gfx.Box("SideL", t, new Vector3(-hw + 0.02f, h * 0.5f, 0f), new Vector3(0.04f, h, d), body, false);
            Gfx.Box("SideR", t, new Vector3(hw - 0.02f, h * 0.5f, 0f), new Vector3(0.04f, h, d), body, false);
            Gfx.Box("Top", t, new Vector3(0f, h, 0f), new Vector3(w, 0.04f, d), body, false);
            Gfx.Box("Bottom", t, new Vector3(0f, 0.02f, 0f), new Vector3(w, 0.04f, d), body, false);

            // Door hinged on the left edge; vent slats at eye height to peek through.
            var hinge = new GameObject("DoorHinge").transform;
            hinge.SetParent(t, false);
            hinge.gameObject.AddComponent<NoMerge>();
            hinge.localPosition = new Vector3(-hw + 0.02f, 0f, hd - 0.01f);
            float dw = w - 0.05f;
            Gfx.Box("DoorLower", hinge, new Vector3(dw * 0.5f, 0.68f, 0f), new Vector3(dw, 1.3f, 0.03f), body, false);
            Gfx.Box("DoorUpper", hinge, new Vector3(dw * 0.5f, (h + 1.9f) * 0.5f + 0.05f, 0f), new Vector3(dw, h - 1.9f, 0.03f), body, false);
            for (int i = 0; i < 5; i++)
                Gfx.Box("Slat", hinge, new Vector3(dw * 0.5f, 1.38f + i * 0.11f, 0.01f), new Vector3(dw, vents ? 0.055f : 0.075f, 0.03f), body, false)
                    .transform.localRotation = Quaternion.Euler(30f, 0f, 0f);
            Gfx.Box("SlatFrameL", hinge, new Vector3(0.03f, 1.62f, 0f), new Vector3(0.06f, 0.64f, 0.03f), body, false);
            Gfx.Box("SlatFrameR", hinge, new Vector3(dw - 0.03f, 1.62f, 0f), new Vector3(0.06f, 0.64f, 0.03f), body, false);
            Gfx.Box("Handle", hinge, new Vector3(dw - 0.1f, 1.1f, 0.04f), new Vector3(0.04f, 0.18f, 0.04f), trim, false);
            StaticMerge.Merge(hinge, "Door");   // the swinging door becomes one mesh per material

            var col = t.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, h * 0.5f, 0f);
            col.size = new Vector3(w + 0.03f, h + 0.02f, d + 0.04f);
            return hinge;
        }

        static Transform Stall(Transform t, Mats m)
        {
            // Cubicle 1.0 x 1.5 m, partitions raised off the floor, toilet at the back.
            Material part = m.plasticGrey;
            Gfx.Box("SideL", t, new Vector3(-0.5f, 1.1f, -0.15f), new Vector3(0.04f, 1.9f, 1.5f), part, false);
            Gfx.Box("SideR", t, new Vector3(0.5f, 1.1f, -0.15f), new Vector3(0.04f, 1.9f, 1.5f), part, false);
            Gfx.Box("Back", t, new Vector3(0f, 1.1f, -0.9f), new Vector3(1.0f, 1.9f, 0.04f), part, false);
            Gfx.Box("Bowl", t, new Vector3(0f, 0.22f, -0.55f), new Vector3(0.4f, 0.44f, 0.55f), m.porcelain, false);
            Gfx.Box("Cistern", t, new Vector3(0f, 0.65f, -0.82f), new Vector3(0.42f, 0.4f, 0.14f), m.porcelain, false);
            var hinge = new GameObject("DoorHinge").transform;
            hinge.SetParent(t, false);
            hinge.gameObject.AddComponent<NoMerge>();
            hinge.localPosition = new Vector3(-0.46f, 0f, 0.6f);
            Gfx.Box("Door", hinge, new Vector3(0.45f, 1.1f, 0f), new Vector3(0.9f, 1.75f, 0.03f), part, false);
            Gfx.Box("Latch", hinge, new Vector3(0.82f, 1.05f, 0.03f), new Vector3(0.05f, 0.04f, 0.03f), m.steel, false);
            StaticMerge.Merge(hinge, "Door");
            foreach (var s in new[] { -1f, 1f })
            {
                var c = t.gameObject.AddComponent<BoxCollider>();
                c.center = new Vector3(s * 0.5f, 1.1f, -0.15f);
                c.size = new Vector3(0.06f, 1.9f, 1.5f);
            }
            var back = t.gameObject.AddComponent<BoxCollider>();
            back.center = new Vector3(0f, 1.1f, -0.9f);
            back.size = new Vector3(1.0f, 1.9f, 0.06f);
            return hinge;
        }

        static void TeacherDesk(Transform t, Mats m)
        {
            // Big wooden desk with a modesty panel toward the room: room underneath to hide (crouched).
            Gfx.Box("Top", t, new Vector3(0f, 0.76f, 0f), new Vector3(1.6f, 0.06f, 0.8f), m.darkWood, false);
            Gfx.Box("SideL", t, new Vector3(-0.77f, 0.37f, 0f), new Vector3(0.06f, 0.74f, 0.78f), m.darkWood, false);
            Gfx.Box("SideR", t, new Vector3(0.77f, 0.37f, 0f), new Vector3(0.06f, 0.74f, 0.78f), m.darkWood, false);
            Gfx.Box("Modesty", t, new Vector3(0f, 0.42f, -0.37f), new Vector3(1.5f, 0.6f, 0.04f), m.darkWood, false);
            Gfx.Box("Books", t, new Vector3(0.45f, 0.84f, 0.05f), new Vector3(0.3f, 0.1f, 0.22f), m.books[0], false);
            Gfx.Box("Apple", t, new Vector3(-0.5f, 0.82f, 0.1f), new Vector3(0.07f, 0.07f, 0.07f), m.fabricRed, false);
            var col = t.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.76f, 0f);
            col.size = new Vector3(1.6f, 0.08f, 0.8f);
            foreach (var s in new[] { -1f, 1f })
            {
                var c = t.gameObject.AddComponent<BoxCollider>();
                c.center = new Vector3(s * 0.77f, 0.37f, 0f);
                c.size = new Vector3(0.06f, 0.74f, 0.78f);
            }
        }

        static void Seat(Transform t, Mats m)
        {
            Gfx.Box("Bench", t, new Vector3(0f, 0.25f, -0.1f), new Vector3(1.4f, 0.5f, 0.6f), m.stone);
            Gfx.Box("Cushion", t, new Vector3(0f, 0.53f, -0.1f), new Vector3(1.2f, 0.06f, 0.5f), m.fabricRed, false);
            Gfx.Box("Blanket", t, new Vector3(0.3f, 0.57f, -0.1f), new Vector3(0.5f, 0.04f, 0.45f), m.sheet, false);
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public void SwingDoor() { doorTarget = 75f; }

        void Update()
        {
            if (door == null) return;
            doorAngle = Mathf.MoveTowards(doorAngle, doorTarget, 400f * Time.deltaTime);
            if (doorAngle >= 74f) doorTarget = 0f;
            door.localRotation = Quaternion.Euler(0f, -doorAngle, 0f);
        }
    }

    /// <summary>
    /// The gold-trimmed bookcase in the floor 2 library. Press E next to it and it grinds aside,
    /// revealing the passage into the Egypt "museum vault" - a safe room the dead can't enter.
    /// </summary>
    public class SecretBookcase : MonoBehaviour
    {
        public static SecretBookcase Instance { get; private set; }
        public bool IsOpen { get; private set; }
        public bool Discovered { get; private set; }
        public bool PlayerNear { get; private set; }
        public Vector3 FrontPosition { get { return closedPos + Vector3.left * 1.1f; } }

        Vector3 closedPos, openPos;
        float t, target;

        public static SecretBookcase Create(Transform parent, Vector3 floorPos, Mats m)
        {
            var root = new GameObject("Secret bookcase").transform;
            root.SetParent(parent, false);
            root.position = floorPos;
            root.rotation = Quaternion.LookRotation(Vector3.left, Vector3.up);   // faces into the library
            root.gameObject.AddComponent<NoMerge>();
            float w = 2.06f, h = 2.42f, d = 0.42f;
            Gfx.Box("Back", root, new Vector3(0f, h * 0.5f, -d * 0.5f + 0.02f), new Vector3(w, h, 0.04f), m.darkWood, false);
            Gfx.Box("SideL", root, new Vector3(-w * 0.5f, h * 0.5f, 0f), new Vector3(0.06f, h, d), m.darkWood, false);
            Gfx.Box("SideR", root, new Vector3(w * 0.5f, h * 0.5f, 0f), new Vector3(0.06f, h, d), m.darkWood, false);
            Gfx.Box("Crown", root, new Vector3(0f, h - 0.04f, 0.02f), new Vector3(w + 0.1f, 0.08f, d + 0.06f), m.gold, false);
            Gfx.Box("TrimL", root, new Vector3(-w * 0.5f, h * 0.5f, d * 0.5f), new Vector3(0.07f, h, 0.02f), m.gold, false);
            Gfx.Box("TrimR", root, new Vector3(w * 0.5f, h * 0.5f, d * 0.5f), new Vector3(0.07f, h, 0.02f), m.gold, false);
            var rnd = new System.Random(77);
            for (int i = 0; i < 5; i++)
            {
                float sy = 0.06f + i * 0.47f;
                Gfx.Box("Board", root, new Vector3(0f, sy, 0f), new Vector3(w, 0.03f, d), m.darkWood, false);
                float x = -w * 0.5f + 0.06f;
                while (x < w * 0.5f - 0.1f)
                {
                    float bw = 0.04f + (float)rnd.NextDouble() * 0.05f, bh = 0.28f + (float)rnd.NextDouble() * 0.1f;
                    Gfx.Box("Book", root, new Vector3(x + bw * 0.5f, sy + bh * 0.5f + 0.015f, 0f), new Vector3(bw * 0.9f, bh, d * 0.8f), m.books[rnd.Next(m.books.Length)], false);
                    x += bw;
                }
            }
            // The one book that doesn't belong.
            Gfx.Box("OddBook", root, new Vector3(0.3f, 1.12f, 0.12f), new Vector3(0.07f, 0.34f, 0.3f), m.gold, false)
                .transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            StaticMerge.Merge(root, "Bookcase");
            var col = root.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, h * 0.5f, 0f);
            col.size = new Vector3(w, h, d);
            var bc = root.gameObject.AddComponent<SecretBookcase>();
            bc.closedPos = root.position;
            bc.openPos = root.position + Vector3.forward * 2.05f;   // slides north, behind the wall
            return bc;
        }

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Update()
        {
            var p = PlayerController.Instance;
            PlayerNear = p != null && !p.IsDead && !p.IsHidden && Mathf.Abs(p.transform.position.y - closedPos.y) < 1.5f &&
                         Vector3.Distance(new Vector3(p.transform.position.x, closedPos.y, p.transform.position.z), Vector3.Lerp(closedPos, openPos, t) + Vector3.left * 0.6f) < 2.2f;
            var gm = GameManager.Instance;
            if (PlayerNear && gm != null && gm.IsPlaying && !gm.IsPaused && InputWrap.Down(KeyCode.E) && p.NearbyHidingSpot() == null)
            {
                target = IsOpen ? 0f : 1f;
                IsOpen = !IsOpen;
                Sfx.At(Sfx.Roar, closedPos, 0.35f, 0.35f, 20f);
                if (IsOpen && !Discovered)
                {
                    Discovered = true;
                    gm.Toast("A hidden passage... The dead can't follow you in there.");
                }
            }
            float prev = t;
            t = Mathf.MoveTowards(t, target, Time.deltaTime / 1.6f);
            if (t != prev) transform.position = Vector3.Lerp(closedPos, openPos, Mathf.SmoothStep(0f, 1f, t)) + Random.insideUnitSphere * 0.004f;
        }
    }

    public enum PickupKind { Ammo, Medkit, Fuse }

    /// <summary>Ammo, medkits and the three elevator fuses. Collected by walking into them.</summary>
    public class Pickup : MonoBehaviour
    {
        public static readonly List<Pickup> All = new List<Pickup>();

        public PickupKind kind;
        public int floor;
        public int rounds = 60;   // ammo boxes only
        Vector3 basePos;
        float phase;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        public static Pickup Create(Transform parent, Vector3 position, PickupKind kind)
        {
            var root = new GameObject(kind.ToString());
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            var t = root.transform;
            switch (kind)
            {
                case PickupKind.Ammo:
                    Gfx.Box("AmmoBox", t, Vector3.zero, new Vector3(0.45f, 0.25f, 0.3f), Gfx.Lit(new Color(0.2f, 0.26f, 0.12f), 0.3f), false);
                    Gfx.Box("Stripe", t, new Vector3(0f, 0f, 0.151f), new Vector3(0.45f, 0.06f, 0.005f), Gfx.Emissive(new Color(0.8f, 0.7f, 0.1f), new Color(2.4f, 2f, 0.2f)), false);
                    Gfx.PointLight(t, new Vector3(0f, 0.3f, 0f), new Color(0.6f, 1f, 0.4f), 0.25f, 2.5f, LightShadows.None);
                    break;
                case PickupKind.Medkit:
                    Gfx.Box("Kit", t, Vector3.zero, new Vector3(0.42f, 0.28f, 0.16f), Gfx.Lit(new Color(0.85f, 0.85f, 0.82f), 0.4f), false);
                    var red = Gfx.Emissive(new Color(0.8f, 0.05f, 0.05f), new Color(2.4f, 0.0f, 0.0f));
                    Gfx.Box("CrossV", t, new Vector3(0f, 0f, 0.081f), new Vector3(0.06f, 0.18f, 0.005f), red, false);
                    Gfx.Box("CrossH", t, new Vector3(0f, 0f, 0.081f), new Vector3(0.18f, 0.06f, 0.005f), red, false);
                    Gfx.PointLight(t, new Vector3(0f, 0.3f, 0f), new Color(1f, 0.3f, 0.3f), 0.25f, 2.5f, LightShadows.None);
                    break;
                case PickupKind.Fuse:
                    Gfx.Prim(PrimitiveType.Cylinder, "Glass", t, Vector3.zero, new Vector3(0.14f, 0.18f, 0.14f), Gfx.Transparent(new Color(0.6f, 0.7f, 0.8f, 0.25f), new Color(0.1f, 0.1f, 0.05f)), false);
                    Gfx.Prim(PrimitiveType.Cylinder, "Core", t, Vector3.zero, new Vector3(0.05f, 0.15f, 0.05f), Gfx.Emissive(new Color(1f, 0.8f, 0.2f), new Color(12f, 8.8f, 1.6f)), false);
                    Gfx.Prim(PrimitiveType.Cylinder, "CapTop", t, new Vector3(0f, 0.19f, 0f), new Vector3(0.15f, 0.025f, 0.15f), Gfx.Lit(new Color(0.7f, 0.6f, 0.3f), 0.8f, 1f), false);
                    Gfx.Prim(PrimitiveType.Cylinder, "CapBottom", t, new Vector3(0f, -0.19f, 0f), new Vector3(0.15f, 0.025f, 0.15f), Gfx.Lit(new Color(0.7f, 0.6f, 0.3f), 0.8f, 1f), false);
                    Gfx.PointLight(t, Vector3.zero, new Color(1f, 0.8f, 0.3f), 1.5f, 4f, LightShadows.None);
                    root.transform.localScale = Vector3.one * 1.4f;
                    break;
            }
            foreach (var r in root.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var p = root.AddComponent<Pickup>();
            p.kind = kind;
            return p;
        }

        void Start()
        {
            basePos = transform.position;
            phase = Random.value * 10f;
        }

        void Update()
        {
            transform.position = basePos + Vector3.up * Mathf.Sin(Time.time * 2f + phase) * 0.08f;
            transform.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);

            var player = PlayerController.Instance;
            if (player == null || player.IsDead || player.IsHidden) return;
            Vector3 d = player.transform.position + Vector3.up * 0.9f - transform.position;
            if (d.magnitude > 1.4f) return;

            switch (kind)
            {
                case PickupKind.Ammo:
                    int nine = Mathf.Max(6, rounds / 4);
                    player.Rifle.AddAmmo(rounds);
                    player.Pistol.AddAmmo(nine);
                    GameManager.Instance.Toast("+" + rounds + " M4A1 rounds, +" + nine + " 9mm");
                    break;
                case PickupKind.Medkit:
                    if (player.Health >= player.MaxHealth) return; // leave it for later
                    player.Heal(40f);
                    GameManager.Instance.Toast("Medkit used (+40 health)");
                    break;
                case PickupKind.Fuse:
                    GameManager.Instance.CollectFuse(this);
                    break;
            }
            Sfx.At(Sfx.Pickup, transform.position, 0.8f);
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// The walled-off service elevator. Once all fuses are in, the fake wall grinds
    /// down into the floor and the lift powers up. Step inside to escape.
    /// </summary>
    public class SecretElevator : MonoBehaviour
    {
        public bool IsOpen { get; private set; }
        public Vector3 CarCenter { get { return carCenter; } }
        public Vector3 WallPosition { get { return wallStart; } }

        Transform fakeWall;
        Vector3 wallStart, carCenter;
        Light carLight;
        Renderer panel;
        Transform doorA, doorB;
        Vector3 doorAOpen, doorBOpen, doorAClosed, doorBClosed;
        float openT = -1f, closeT = -1f;
        Material panelOn;

        public static SecretElevator Create(Transform parent, GameObject fake, Vector3 car, Material carMat, Material trim)
        {
            var root = new GameObject("SecretElevator");
            root.transform.SetParent(parent, false);
            root.AddComponent<NoMerge>();
            var e = root.AddComponent<SecretElevator>();
            e.fakeWall = fake.transform;
            e.wallStart = fake.transform.position;
            e.carCenter = car;

            float wallH = LevelBuilder.WallH;
            Vector3 toCar = car - e.wallStart; toCar.y = 0f; toCar.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, toCar);
            float cell = LevelBuilder.Cell;

            // Car interior: brushed metal floor, back wall, side walls.
            Gfx.Box("CarFloor", root.transform, car + Vector3.up * 0.02f, new Vector3(cell - 0.3f, 0.04f, cell - 0.3f), carMat, false);
            var back = Gfx.Box("CarBack", root.transform, car + toCar * (cell * 0.5f - 0.2f) + Vector3.up * wallH * 0.5f, new Vector3(cell - 0.3f, wallH, 0.1f), carMat, false);
            back.transform.rotation = Quaternion.LookRotation(toCar);
            for (int s = -1; s <= 1; s += 2)
            {
                var w = Gfx.Box("CarSide", root.transform, car + side * s * (cell * 0.5f - 0.2f) + Vector3.up * wallH * 0.5f, new Vector3(cell - 0.3f, wallH, 0.1f), carMat, false);
                w.transform.rotation = Quaternion.LookRotation(side);
            }
            Gfx.Box("Handrail", root.transform, car + toCar * (cell * 0.5f - 0.32f) + Vector3.up * 1f, new Vector3(cell - 0.6f, 0.05f, 0.05f), trim, false)
                .transform.rotation = Quaternion.LookRotation(toCar);
            var pnl = Gfx.Box("ButtonPanel", root.transform, car + side * (cell * 0.5f - 0.27f) + toCar * -0.6f + Vector3.up * 1.3f,
                new Vector3(0.05f, 0.5f, 0.25f), Gfx.Emissive(new Color(0.2f, 0.02f, 0.02f), new Color(1.2f, 0f, 0f)), false);
            pnl.transform.rotation = Quaternion.LookRotation(toCar);
            e.panel = pnl.GetComponent<Renderer>();
            e.panelOn = Gfx.Emissive(new Color(0.1f, 0.5f, 0.15f), new Color(0.6f, 7.5f, 1.2f));
            e.carLight = Gfx.PointLight(root.transform, car + Vector3.up * (wallH - 0.4f), new Color(0.5f, 1f, 0.6f), 0f, 6f, LightShadows.Soft);

            // Sliding doors on the car's threshold, hidden until the escape.
            Vector3 threshold = e.wallStart + toCar * 0.25f;
            threshold.y = car.y + wallH * 0.5f;
            e.doorAOpen = threshold + side * (cell * 0.75f);
            e.doorBOpen = threshold - side * (cell * 0.75f);
            e.doorAClosed = threshold + side * (cell * 0.25f);
            e.doorBClosed = threshold - side * (cell * 0.25f);
            e.doorA = Gfx.Box("DoorA", root.transform, e.doorAOpen, new Vector3(cell * 0.5f, wallH, 0.08f), carMat, false).transform;
            e.doorB = Gfx.Box("DoorB", root.transform, e.doorBOpen, new Vector3(cell * 0.5f, wallH, 0.08f), carMat, false).transform;
            e.doorA.rotation = Quaternion.LookRotation(toCar);
            e.doorB.rotation = Quaternion.LookRotation(toCar);
            e.doorA.gameObject.SetActive(false);
            e.doorB.gameObject.SetActive(false);
            return e;
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;
            openT = 0f;
            Sfx.At(Sfx.Roar, wallStart, 0.6f, 0.4f, 60f); // grinding concrete
            NoiseSystem.Emit(wallStart, 35f);
        }

        public void CloseDoors()
        {
            closeT = 0f;
            doorA.gameObject.SetActive(true);
            doorB.gameObject.SetActive(true);
            Sfx.At(Sfx.Ding, carCenter, 1f);
        }

        void Update()
        {
            if (openT >= 0f && openT < 1f)
            {
                openT += Time.deltaTime / 4f;
                float drop = Mathf.SmoothStep(0f, LevelBuilder.Story, openT);
                fakeWall.position = wallStart + Vector3.down * drop + Random.insideUnitSphere * 0.02f * (1f - openT);
                if (openT >= 1f)
                {
                    fakeWall.gameObject.SetActive(false);
                    carLight.intensity = 2.2f * Gfx.Lumens;
                    panel.sharedMaterial = panelOn;
                    Sfx.At(Sfx.Ding, carCenter, 1f);
                }
            }
            if (closeT >= 0f && closeT < 1f)
            {
                closeT = Mathf.Min(1f, closeT + Time.deltaTime / 1.5f);
                doorA.position = Vector3.Lerp(doorAOpen, doorAClosed, Mathf.SmoothStep(0f, 1f, closeT));
                doorB.position = Vector3.Lerp(doorBOpen, doorBClosed, Mathf.SmoothStep(0f, 1f, closeT));
            }

            if (IsOpen && openT >= 1f && GameManager.Instance != null && !GameManager.Instance.IsOver)
            {
                var player = PlayerController.Instance;
                if (player != null && !player.IsDead)
                {
                    Vector3 d = player.transform.position - carCenter;
                    if (Mathf.Abs(d.y) < 1.5f && new Vector2(d.x, d.z).magnitude < 1.1f)
                        GameManager.Instance.Win();
                }
            }
        }
    }
}
