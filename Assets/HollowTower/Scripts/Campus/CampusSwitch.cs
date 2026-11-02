using UnityEngine;

namespace HollowTower.Campus
{
    /// <summary>
    /// Something a player turns on once with E: the campus generator (its yard floodlights and the lamps it feeds come
    /// on, and it hums), or the gate control in the security booth (needs the generator; opens the main gate). Goes
    /// through NetWorld so it happens on every machine.
    /// </summary>
    public class CampusSwitch : MonoBehaviour, IPartyInteractable, Net.INetAction
    {
        public string prompt = "[E] Start the generator";
        public string doneMessage = "The generator coughs into life";
        [Tooltip("Has to be on first (the gate control needs the generator).")]
        public CampusSwitch requires;
        public string requiresText = "No power";
        public CampusLamp[] lamps;
        public Renderer[] indicators;
        public Color indicatorOn = new Color(0.2f, 1f, 0.35f);
        public Color indicatorOff = new Color(1f, 0.15f, 0.1f);
        public CampusGate opens;
        public bool humWhenOn;
        [Tooltip("A timing puzzle to beat first (the generator: catch the ignition).")]
        public Puzzles.NeedleLock needle;
        [Tooltip("An item a player must carry to use it (the generator's crank handle).")]
        public string requiredItem = "";
        public string requiredItemText = "Something is missing";

        bool Has(RemasteredPlayerController body)
        {
            if (string.IsNullOrEmpty(requiredItem)) return true;
            var inv = body != null ? body.GetComponent<PlayerInventory>() : null;
            return inv != null && inv.Find(requiredItem) != null;
        }
        public string needlePrompt = "[E] Crank the generator - catch the ignition";

        public bool On { get; private set; }

        AudioSource hum;
        MaterialPropertyBlock block;

        void Start()
        {
            Indicate();
            if (needle != null) needle.Missed += Backfire;
        }

        /// <summary>A missed ignition: the engine backfires with a bang that carries across the campus.</summary>
        void Backfire(int player)
        {
            SfxBank.At(CampusSynth.Backfire, transform.position + Vector3.up, 1f, Random.Range(0.85f, 1.1f), 120f);
            CampusLure.Emit(transform.position, CampusLure.Kind.Backfire, 200f, 70f);
            Net.NetWorld.Noise(transform.position, 50f);
        }

        public string Prompt(int player, RemasteredPlayerController body)
        {
            if (On) return null;
            if (requires != null && !requires.On) return requiresText;
            if (!Has(body)) return requiredItemText;
            if (needle != null && !needle.Solved) return needle.Jammed ? "Flooded - wait " + Mathf.CeilToInt(needle.JamLeft) + " s" : needlePrompt;
            return prompt;
        }

        public void Interact(int player, RemasteredPlayerController body)
        {
            if (On) return;
            if (requires != null && !requires.On)
            {
                if (SfxBank.Instance != null) SfxBank.At(SfxBank.Instance.doorLocked, transform.position + Vector3.up, 0.6f, 1.3f);
                return;
            }
            if (!Has(body)) { if (SfxBank.Instance != null) SfxBank.At(SfxBank.Instance.doorLocked, transform.position + Vector3.up, 0.6f, 1.3f); return; }
            if (needle != null && !needle.Solved) { needle.TryOpen(player, () => Net.NetWorld.Act(this, 1, player)); return; }
            Net.NetWorld.Act(this, 1, player);
        }

        public bool NetApply(int action, int a, int b, Vector3 v)
        {
            if (action != 1 || On || (requires != null && !requires.On)) return false;
            On = true;
            if (lamps != null) foreach (var l in lamps) if (l != null) l.powered = true;
            if (opens != null) opens.OpenGate();
            if (SfxBank.Instance != null) SfxBank.At(SfxBank.Instance.socketPower, transform.position + Vector3.up, 1f, humWhenOn ? 0.7f : 1.1f, 40f);
            if (humWhenOn)
            {
                hum = gameObject.AddComponent<AudioSource>();
                hum.clip = LampSfx.Hum; hum.loop = true; hum.spatialBlend = 1f; hum.minDistance = 2f; hum.maxDistance = 30f;
                hum.rolloffMode = AudioRolloffMode.Linear; hum.pitch = 0.45f; hum.volume = 0.8f; hum.Play();
            }
            Indicate();
            var party = PlayerParty.Instance;
            if (party != null) party.Flash(doneMessage + " (Player " + (a + 1) + ")", 4f);
            return true;
        }

        void Indicate()
        {
            if (indicators == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            foreach (var r in indicators)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(Gfx.EmissiveId, Gfx.ToNits((On ? indicatorOn : indicatorOff) * 2.5f));
                r.SetPropertyBlock(block);
            }
        }
    }
}
