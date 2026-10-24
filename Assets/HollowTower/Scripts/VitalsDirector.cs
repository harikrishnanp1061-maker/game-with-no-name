using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HollowTower
{
    /// <summary>
    /// Runs the party's vitals and items (made at scene load wherever there is a PlayerParty): a PlayerVitals on every
    /// body, Q to use the selected item, a teammate's death shaking everyone who saw it, the stamina and sanity bars
    /// (bottom left), and what low sanity does to the steered player - the screen narrows, warps and greys, and they
    /// start hearing things: giggles and little footsteps behind them that aren't there.
    /// </summary>
    public class VitalsDirector : MonoBehaviour
    {
        public static VitalsDirector Instance { get; private set; }
        /// <summary>The lamps that count as light for sanity (refreshed every few seconds; no player torches).</summary>
        public static readonly List<Light> Lamps = new List<Light>();

        bool[] wasDead = new bool[4];
        float nextLamps, nextPhantom;
        Volume vol;
        Vignette vig;
        LensDistortion lens;
        ChromaticAberration ca;
        ColorAdjustments grade;
        Texture2D white;
        GUIStyle label, hint;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            Spawn();
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => Spawn();
        }

        static void Spawn()
        {
            if (Instance != null || Object.FindAnyObjectByType<PlayerParty>() == null) return;
            new GameObject("VitalsDirector").AddComponent<VitalsDirector>();
        }

        void Awake() { Instance = this; }
        void OnDestroy() { if (Instance == this) Instance = null; }

        void Start()
        {
            var go = new GameObject("Sanity FX (volume)");
            go.transform.SetParent(transform, false);
            vol = go.AddComponent<Volume>();
            vol.isGlobal = true; vol.priority = 55f;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            vol.sharedProfile = p;
            vig = p.Add<Vignette>(true); vig.color.Override(Color.black); vig.smoothness.Override(0.7f); vig.intensity.Override(0f);
            lens = p.Add<LensDistortion>(true); lens.intensity.Override(0f);
            ca = p.Add<ChromaticAberration>(true); ca.intensity.Override(0f);
            grade = p.Add<ColorAdjustments>(true); grade.saturation.Override(0f);
            vol.weight = 0f;
        }

        void Update()
        {
            var party = PlayerParty.Instance;
            if (party == null) return;

            if (Time.time > nextLamps)
            {
                nextLamps = Time.time + 4f;
                Lamps.Clear();
                foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (l.type != LightType.Directional && l.GetComponentInParent<RemasteredPlayerController>() == null) Lamps.Add(l);
            }

            for (int i = 0; i < party.players.Length; i++)
            {
                var b = party.players[i];
                if (b == null) continue;
                if (b.GetComponent<PlayerVitals>() == null) b.gameObject.AddComponent<PlayerVitals>();
                if (b.IsDead && !wasDead[i])
                    foreach (var other in party.players)
                        if (other != null && other != b) { var v = other.GetComponent<PlayerVitals>(); if (v != null) v.Witnessed(b.transform.position); }
                wasDead[i] = b.IsDead;
            }

            for (int k = UsableItem.All.Count - 1; k >= 0; k--) if (UsableItem.All[k] != null) UsableItem.All[k].Tick();

            // Q: use what's in the selected slot
            var cur = party.Current;
            if (cur != null && !cur.IsDead && !cur.IsFrozen && !RemasteredMapHUD.IsFullMapOpen && InputWrap.Down(KeyCode.Q))
            {
                var inv = cur.GetComponent<PlayerInventory>();
                if (inv != null && inv.Items.Count > 0)
                {
                    var item = inv.Items[Mathf.Clamp(inv.Selected, 0, inv.Items.Count - 1)];
                    var use = item != null ? item.GetComponent<UsableItem>() : null;
                    if (use != null) use.UseBy(cur, party.CurrentIndex);
                }
            }

            Madness(cur);
        }

        // what low sanity does to the one you're steering
        void Madness(RemasteredPlayerController cur)
        {
            var v = cur != null && !cur.IsDead ? cur.GetComponent<PlayerVitals>() : null;
            float m = v != null ? Mathf.Clamp01(1f - v.Sanity / 60f) : 0f;   // nothing above 60%, all of it at 0
            m *= m;
            float t = Time.time;
            vol.weight = Mathf.MoveTowards(vol.weight, m > 0.01f ? 1f : 0f, Time.deltaTime * 2f);
            vig.intensity.value = m * (0.45f + 0.08f * Mathf.Sin(t * 1.7f));
            lens.intensity.value = m * 0.3f * Mathf.Sin(t * 0.9f) * Mathf.Sin(t * 0.37f);
            ca.intensity.value = m * 0.6f;
            grade.saturation.value = -m * 45f;

            // hearing things: the lower they are, the more often
            if (v == null || v.Sanity > 50f) { nextPhantom = Time.time + 6f; return; }
            if (Time.time < nextPhantom) return;
            nextPhantom = Time.time + Mathf.Lerp(5f, 18f, v.Sanity / 50f) * Random.Range(0.7f, 1.3f);
            Vector3 behind = cur.transform.position - cur.transform.forward * Random.Range(3f, 6f) + cur.transform.right * Random.Range(-3f, 3f) + Vector3.up * 0.8f;
            var bank = SfxBank.Instance;
            if (Random.value < 0.55f) PartyLaughs.At(behind, Random.Range(0.9f, 1.2f));
            else if (bank != null && bank.monsterSteps != null && bank.monsterSteps.Length > 0)
                for (int k = 0; k < 3; k++) SfxBank.At(SfxBank.Pick(bank.monsterSteps), behind + cur.transform.forward * k * 0.5f, 0.35f, Random.Range(1.5f, 1.8f), 12f);
        }

        void OnGUI()
        {
            var party = PlayerParty.Instance;
            var cur = party != null ? party.Current : null;
            if (cur == null || cur.IsDead || RemasteredMapHUD.IsFullMapOpen) return;
            var v = cur.GetComponent<PlayerVitals>();
            if (v == null) return;
            if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
            if (label == null)
            {
                label = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold };
                hint = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            }

            float x = 18f, y = Screen.height - 58f;
            Bar(x, y, "SANITY", v.Sanity / PlayerVitals.MaxSanity, Color.Lerp(new Color(0.85f, 0.15f, 0.25f), new Color(0.65f, 0.55f, 1f), v.Sanity / 100f), v.Sanity < 25f);
            if (v.Stamina < PlayerVitals.MaxStamina - 0.5f || v.Winded)
                Bar(x, y + 22f, v.Winded ? "WINDED" : "STAMINA", v.Stamina / PlayerVitals.MaxStamina, v.Winded ? new Color(0.9f, 0.4f, 0.2f) : new Color(1f, 0.85f, 0.3f), v.Winded);

            // what Q does with the selected item, over the inventory
            var inv = cur.GetComponent<PlayerInventory>();
            if (inv != null && inv.Items.Count > 0)
            {
                var item = inv.Items[Mathf.Clamp(inv.Selected, 0, inv.Items.Count - 1)];
                var use = item != null ? item.GetComponent<UsableItem>() : null;
                if (use != null)
                {
                    GUI.color = new Color(0f, 0f, 0f, 0.6f);
                    GUI.Label(new Rect(1f, Screen.height - 54f - 16f - 25f, Screen.width, 22f), use.UseHint, hint);
                    GUI.color = new Color(1f, 0.95f, 0.8f, 0.95f);
                    GUI.Label(new Rect(0f, Screen.height - 54f - 16f - 26f, Screen.width, 22f), use.UseHint, hint);
                }
            }
            GUI.color = Color.white;
        }

        void Bar(float x, float y, string name, float k, Color c, bool pulse)
        {
            const float w = 170f, h = 9f;
            float a = pulse ? 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.time * 5f)) : 1f;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(x - 2f, y - 2f, w + 4f, h + 4f), white);
            GUI.color = new Color(c.r, c.g, c.b, 0.9f * a);
            GUI.DrawTexture(new Rect(x, y, w * Mathf.Clamp01(k), h), white);
            GUI.color = new Color(1f, 1f, 1f, 0.8f * a);
            GUI.Label(new Rect(x + w + 8f, y - 5f, 90f, 18f), name, label);
        }
    }
}
