using System.Collections.Generic;
using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// Floor 2, the children's party. While a player is on the floor it runs the cycle the floor is built around:
    /// carnival music and bright, warm light (explore) until the music winds down and dies, then the lamps and ceiling
    /// go red and a low drone fills the floor (chaos: the party guests wake and hunt; hide), then the music starts again.
    /// The guests (PartyMonster) follow Current; the screen side of the chaos is ChaosFx.
    /// </summary>
    public class PartyFloor : MonoBehaviour, Net.INetAction
    {
        public enum Phase { Explore, WindDown, Chaos }

        public static PartyFloor Instance { get; private set; }
        public static event System.Action ChaosStarted, ChaosEnded;

        [Header("Cycle (seconds)")]
        public Vector2 exploreTime = new Vector2(45f, 75f);
        public float windDownTime = 1.8f;
        public Vector2 chaosTime = new Vector2(26f, 34f);   // the guests stay still for the first few seconds

        [Header("Explore light")]
        public Color lampColor = new Color(1f, 0.93f, 0.8f);
        public float lampIntensity = 3.4f, lampRange = 2.2f;
        public Color ambient = new Color(0.58f, 0.56f, 0.53f);

        [Header("Chaos light")]
        public Color redLamp = new Color(1f, 0.06f, 0.04f);
        public float redIntensity = 2.6f;
        public Color redAmbient = new Color(0.3f, 0.045f, 0.04f);   // dark enough to scare, light enough to find a hiding spot

        public float musicVolume = 0.55f;
        [Tooltip("The party song. Left empty, a clip at Resources/PartyMusic is used if there is one, else the generated waltz.")]
        public AudioClip song;

        public Phase Current { get; private set; }
        public bool PlayerOnFloor { get; private set; }
        public float PhaseTimeLeft { get { return phaseEnd - Time.time; } }
        /// <summary>0 in normal light, 1 fully red (eases in and out with the chaos).</summary>
        public float RedMix { get { return redMix; } }

        const int FloorIndex = 1;
        const float Story = 4.2f;

        class Lamp { public Light light; public float baseIntensity; public Renderer tube; public Color tubeEmission; }
        readonly List<Lamp> lamps = new List<Lamp>();
        readonly List<Renderer> ceilings = new List<Renderer>();
        Material ceilingMat;
        Color ceilingEmission;
        MaterialPropertyBlock block;
        AudioSource music, drone;
        float phaseEnd, redMix, chaosStart, darkMix;   // darkMix: 1 while the view is inside a DarkZone
        bool wasOnFloor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            Spawn();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => Spawn();
        }

        static void Spawn()
        {
            if (FindAnyObjectByType<PartyFloor>() != null) return;
            if (FloorRoot() == null || FloorRoot().Find("Floor_2_Party") == null) return;   // only once the party floor is built
            new GameObject("PartyFloor").AddComponent<PartyFloor>();
        }

        static Transform FloorRoot()
        {
            var env = GameObject.Find("HollowTower_Remastered_Environment");
            return env != null ? env.transform.Find("Floor_2_School") : null;
        }

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            block = new MaterialPropertyBlock();
            QualitySettings.realtimeReflectionProbes = true;   // the foil balloons mirror the room through each room's probe
            var root = FloorRoot();
            var fixtures = root != null ? root.Find("Lighting_Fixtures") : null;
            if (fixtures != null)
                foreach (var l in fixtures.GetComponentsInChildren<Light>(true))
                {
                    foreach (var f in l.GetComponents<RemasteredFlicker>()) Destroy(f);   // a party floor isn't failing (until it is)
                    l.range *= lampRange;
                    var tubeT = l.transform.parent != null ? l.transform.parent.Find("Fluorescent_Tube") : null;
                    var tube = tubeT != null ? tubeT.GetComponent<Renderer>() : null;
                    var lamp = new Lamp { light = l, baseIntensity = l.intensity, tube = tube };
                    if (tube != null && tube.sharedMaterial != null && tube.sharedMaterial.HasProperty("_EmissionColor")) lamp.tubeEmission = tube.sharedMaterial.GetColor("_EmissionColor");
                    lamps.Add(lamp);
                }
            var slabs = root != null ? root.Find("Slabs_And_Ceilings") : null;
            if (slabs != null)
                foreach (Transform s in slabs)
                {
                    if (!s.name.StartsWith("CeilingSlab")) continue;
                    var r = s.GetComponent<Renderer>();
                    if (r == null || r.sharedMaterial == null) continue;
                    if (ceilingMat == null)
                    {
                        ceilingMat = new Material(r.sharedMaterial) { name = "Ceiling_Party" };
                        ceilingEmission = ceilingMat.HasProperty("_EmissionColor") ? ceilingMat.GetColor("_EmissionColor") : Color.black;
                    }
                    r.sharedMaterial = ceilingMat;
                    ceilings.Add(r);
                }

            music = gameObject.AddComponent<AudioSource>();
            if (song == null) song = Resources.Load<AudioClip>("PartyMusic");
            music.clip = song != null ? song : PartyMusic.Waltz; music.loop = true; music.spatialBlend = 0f; music.volume = 0f;
            drone = gameObject.AddComponent<AudioSource>();
            drone.clip = PartyMusic.Drone; drone.loop = true; drone.spatialBlend = 0f; drone.volume = 0f;
            gameObject.AddComponent<ChaosFx>();
            gameObject.AddComponent<HardLanding>();   // the floor's broken boards drop you to floor 1
            BeginExplore(Net.Net.IsAuthority ? Random.Range(exploreTime.x, exploreTime.y) : 9999f);   // a client waits for the host's timing
        }

        // the host times the cycle and tells everyone; a client just follows (and is re-told every few seconds, so one
        // that loaded the level a moment late catches up)
        float nextResend;

        void Next(Phase p, float duration)
        {
            if (Net.Net.IsAuthority) Net.NetWorld.Act(this, 1, (int)p, 0, new Vector3(duration, 0f, 0f));
        }

        public bool NetApply(int action, int a, int b, Vector3 v)
        {
            if (action != 1) return false;
            var p = (Phase)a;
            if (p == Current) { phaseEnd = Time.time + v.x; return true; }
            if (p == Phase.Explore) BeginExplore(v.x);
            else if (p == Phase.WindDown) { Current = Phase.WindDown; phaseEnd = Time.time + v.x; }
            else BeginChaos(v.x);
            return true;
        }

        void BeginChaos(float duration)
        {
            Current = Phase.Chaos;
            chaosStart = Time.time;
            phaseEnd = Time.time + duration;
            music.Pause();   // not Stop: Stop rewinds to the start
            if (PlayerOnFloor) SfxBank.Flat(PartyMusic.Boom, 0.9f);
            if (!drone.isPlaying) drone.Play();
            if (ChaosStarted != null) ChaosStarted();
        }

        void BeginExplore(float duration)
        {
            Current = Phase.Explore;
            phaseEnd = Time.time + duration;
            music.pitch = 1f;
            if (!music.isPlaying)
            {
                if (music.time > 0f) music.UnPause();   // pick the song up where the tape died
                else music.Play();
            }
            if (ChaosEnded != null && redMix > 0.5f) ChaosEnded();
        }

        void Update()
        {
            PlayerOnFloor = OnFloor();
            float t = Time.time;
            if (Net.Net.IsAuthority && t >= phaseEnd)
            {
                if (Current == Phase.Explore) Next(Phase.WindDown, windDownTime);
                else if (Current == Phase.WindDown) Next(Phase.Chaos, Random.Range(chaosTime.x, chaosTime.y));
                else Next(Phase.Explore, Random.Range(exploreTime.x, exploreTime.y));
            }
            if (Net.Net.Active && Net.Net.IsAuthority && t > nextResend) { nextResend = t + 4f; Next(Current, Mathf.Max(0.1f, phaseEnd - t)); }

            // music: full while exploring, a tape-stop slide while winding down, gone in chaos
            float hear = PlayerOnFloor ? 1f : 0f;
            if (Current == Phase.WindDown)
            {
                float k = Mathf.Clamp01(1f - (phaseEnd - t) / windDownTime);
                music.pitch = Mathf.Lerp(1f, 0.25f, k * k);
                music.volume = musicVolume * hear * (1f - k * 0.7f);
            }
            else music.volume = Mathf.MoveTowards(music.volume, Current == Phase.Explore ? musicVolume * hear : 0f, Time.deltaTime);
            drone.volume = Mathf.MoveTowards(drone.volume, Current == Phase.Chaos ? 0.5f * hear : 0f, Time.deltaTime * (Current == Phase.Chaos ? 0.8f : 0.4f));
            if (Current != Phase.Chaos && drone.volume <= 0f && drone.isPlaying) drone.Stop();

            // light: red snaps in when the music dies (with a stutter), fades back when it returns
            float target = Current == Phase.Chaos ? 1f : 0f;
            redMix = Mathf.MoveTowards(redMix, target, Time.deltaTime * (target > redMix ? 3f : 0.7f));
            float pulse = Current == Phase.Chaos ? 0.75f + 0.25f * Mathf.Sin(t * 3.2f) : 1f;
            if (Current == Phase.Chaos && t < chaosStart + 0.6f && Random.value < 0.3f) pulse = 0.15f;   // the lamps stutter as the red comes on
            ApplyLight(redMix, pulse);
        }

        void ApplyLight(float red, float pulse)
        {
            Color lampCol = Color.Lerp(lampColor, redLamp, red);
            float inten = Mathf.Lerp(lampIntensity, redIntensity, red) * (red > 0.01f ? Mathf.Lerp(1f, pulse, red) : 1f);
            // inside a dead-lit room the eyes adjust to the dark: ambient near black, the lamps around fade
            var view = PlayerParty.Active;
            var zone = view != null ? DarkZone.At(view.position) : null;
            darkMix = Mathf.MoveTowards(darkMix, zone != null ? 1f : 0f, Time.deltaTime * 1.5f);
            for (int li = 0; li < lamps.Count; li++)
            {
                var l = lamps[li];
                if (l.light == null) continue;
                l.light.color = lampCol;
                // in the chaos every lamp strobes on its own: dead, blinding, dead, half...
                float strobe = 1f;
                if (red > 0.2f)
                {
                    float n = Mathf.PerlinNoise(Time.time * 9f, li * 1.37f);
                    strobe = n > 0.62f ? 1.8f : n > 0.45f ? 0.6f : n > 0.3f ? 0.08f : 0f;
                    if (Random.value < 0.04f) strobe = 2.6f;   // a sudden flare
                    strobe = Mathf.Lerp(1f, strobe, Mathf.Clamp01((red - 0.2f) / 0.6f));
                }
                float dim = 1f;
                if (darkMix > 0f && zone != null)
                {
                    Vector3 lp = l.light.transform.position;
                    var r = zone.rect;
                    float dx = Mathf.Max(r.xMin - lp.x, 0f, lp.x - r.xMax), dz = Mathf.Max(r.yMin - lp.z, 0f, lp.z - r.yMax);
                    dim = Mathf.Lerp(1f, Mathf.Clamp01(Mathf.Sqrt(dx * dx + dz * dz) / 14f) * 0.8f, darkMix);
                }
                l.light.intensity = l.baseIntensity * inten * dim * strobe;
                if (l.tube != null)
                {
                    l.tube.GetPropertyBlock(block);
                    block.SetColor("_EmissionColor", Color.Lerp(l.tubeEmission, new Color(6f, 0.25f, 0.15f), red) * Mathf.Lerp(1f, pulse, red) * strobe);   // the tube flickers with its light
                    l.tube.SetPropertyBlock(block);
                }
            }
            if (ceilingMat != null && ceilingMat.HasProperty("_EmissionColor"))
                ceilingMat.SetColor("_EmissionColor", Color.Lerp(ceilingEmission, new Color(1.6f, 0.05f, 0.03f), red) * Mathf.Lerp(1f, pulse * 0.8f, red));
            if (PlayerOnFloor)
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = Color.Lerp(Color.Lerp(ambient, redAmbient, red), new Color(0.012f, 0.012f, 0.016f), darkMix);
                wasOnFloor = true;
            }
            else if (wasOnFloor)
            {
                wasOnFloor = false;
                RenderSettings.ambientLight = FloorLightMood.ambient;   // back to floor 1's mood
            }
        }

        static bool OnFloor()
        {
            var party = PlayerParty.Instance;
            Transform body = party != null && party.Current != null ? party.Current.transform : (Camera.main != null ? Camera.main.transform : null);
            if (body == null) return false;
            return Mathf.Clamp(Mathf.FloorToInt((body.position.y + 0.5f) / Story), 0, 3) == FloorIndex;
        }
    }
}
