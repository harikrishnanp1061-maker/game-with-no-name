using UnityEngine;

namespace HollowTower.Campus
{
    /// <summary>
    /// An abandoned car whose battery still holds a charge - the tiny red LED on its dashboard still blinks. Smash a
    /// window (E) and the alarm screams for half a minute with the indicators flashing: the giant turns, walks over and
    /// stomps the car flat (anyone standing next to it too), and a Nailed nearby comes to look. One player sets it off
    /// and runs; the others cross while the giant is busy. Each car works once. Kept in step through NetWorld.
    /// </summary>
    public class CarAlarm : MonoBehaviour, IPartyInteractable, Net.INetAction
    {
        public float alarmSeconds = 30f;
        public float giantRange = 170f, nailedRange = 45f;
        [Tooltip("The dashboard LED that blinks while the battery is live.")]
        public Renderer led;
        public Light[] indicators;
        public Renderer[] indicatorGlass;

        public bool Triggered { get; private set; }
        public bool Crushed { get; private set; }
        public bool Sounding { get { return Triggered && !Crushed && Time.time < stopAt; } }

        float stopAt, nextLure;
        AudioSource src;
        MaterialPropertyBlock block;

        public string Prompt(int player, RemasteredPlayerController body)
        {
            if (Triggered || Crushed) return null;
            return "[E] Smash the window - set off the car alarm (draws the giant here)";
        }

        public void Interact(int player, RemasteredPlayerController body)
        {
            if (Triggered || Crushed) return;
            Net.NetWorld.Act(this, 1, player);
        }

        /// <summary>The giant's foot came down on it (host).</summary>
        public void Crush() { if (!Crushed) Net.NetWorld.Act(this, 2); }

        public bool NetApply(int action, int a, int b, Vector3 v)
        {
            if (action == 1)
            {
                if (Triggered || Crushed) return false;
                Triggered = true;
                stopAt = Time.time + alarmSeconds;
                SfxBank.At(CampusSynth.Glass, transform.position + Vector3.up, 1f, 1f, 30f);
                if (src == null) src = CampusSynth.Loop(gameObject, CampusSynth.CarAlarm, 1f, 160f);
                src.Play();
                nextLure = 0f;
                var party = PlayerParty.Instance;
                if (party != null) party.Flash("Player " + (a + 1) + " set off a car alarm - the giant will come for the car", 4f);
                return true;
            }
            if (action == 2)
            {
                if (Crushed) return false;
                Crushed = true;
                if (src != null) src.Stop();
                // flattened: the roof caves in, it settles crooked
                foreach (Transform t in transform)
                    if (t.GetComponentInChildren<Renderer>() != null && t.GetComponent<Light>() == null)
                    {
                        t.localScale = Vector3.Scale(t.localScale, new Vector3(1.12f, 0.42f, 1.05f));
                        t.localRotation = Quaternion.Euler(Random.Range(-4f, 4f), 0f, Random.Range(-6f, 6f)) * t.localRotation;
                    }
                SetIndicators(false);
                SfxBank.At(CampusSynth.Glass, transform.position + Vector3.up, 1f, 0.6f, 60f);
                return true;
            }
            return false;
        }

        void Update()
        {
            if (Sounding)
            {
                bool on = Mathf.Repeat(Time.time, 0.5f) < 0.25f;
                SetIndicators(on);
                // keeps calling while it screams: the giant may be busy hunting someone when it starts
                if (Time.time > nextLure)
                {
                    nextLure = Time.time + 3f;
                    CampusLure.Emit(transform.position, CampusLure.Kind.CarAlarm, giantRange, nailedRange, this);
                }
            }
            else if (Triggered && !Crushed && src != null && src.isPlaying) { src.Stop(); SetIndicators(false); }

            // the live battery's tell: a slow red blink on the dash, until the alarm drains it
            if (led != null)
            {
                if (block == null) block = new MaterialPropertyBlock();
                bool blink = !Triggered && Mathf.Repeat(Time.time + transform.position.x * 0.37f, 2.2f) < 0.12f;
                led.GetPropertyBlock(block);
                block.SetColor(Gfx.EmissiveId, Gfx.ToNits(blink ? new Color(1f, 0.05f, 0.03f) * 3f : Color.black));
                led.SetPropertyBlock(block);
            }
        }

        void SetIndicators(bool on)
        {
            if (indicators != null) foreach (var l in indicators) if (l != null) l.enabled = on;
            if (indicatorGlass == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            foreach (var r in indicatorGlass)
            {
                if (r == null) continue;
                r.GetPropertyBlock(block);
                block.SetColor(Gfx.EmissiveId, Gfx.ToNits(on ? new Color(1f, 0.55f, 0.1f) * 4f : Color.black));
                r.SetPropertyBlock(block);
            }
        }
    }
}
