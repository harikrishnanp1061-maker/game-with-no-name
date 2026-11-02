using UnityEngine;

namespace HollowTower.Campus
{
    /// <summary>
    /// The old service gate in the far south-east corner of the campus: the way out once the main gate turns out to be
    /// welded shut. Its magnetic lock can only be released from the security control room's console, and only with the
    /// generator running. Two ways to release it:
    ///   Hold   it stays open for holdSeconds while the console keeps it released - the team at the gate radios the
    ///          control room when they are there (best with a walkie-talkie).
    ///   Timed  a long timed release for when nobody can stay behind: it opens for timedSeconds, but its warning siren
    ///          wails the whole time and draws the giant and the Nailed to the gate.
    /// A player can also check the lock from outside (E) - it tells them where it is released from.
    /// </summary>
    public class ServiceGate : MonoBehaviour, IPartyInteractable, Net.INetAction
    {
        public Transform leaf;
        public float openAngle = 105f, swingTime = 2.2f;
        public float holdSeconds = 30f, timedSeconds = 75f;
        public CampusLamp[] beacons;
        public Collider blocker;
        [Tooltip("A padlocked chain on the leaf as well: the groundskeeper's key (the quad statues) takes it off.")]
        public bool padlocked = true;
        public GameObject padlock;
        public string keyItem = "gate_key";

        public bool IsOpen { get { return Time.time < openUntil; } }
        /// <summary>The middle of the gateway (the component sits on a holder at the origin).</summary>
        public Vector3 Centre { get { return blocker != null ? new Vector3(blocker.transform.position.x, 0f, blocker.transform.position.z) : transform.position; } }
        public float OpenLeft { get { return Mathf.Max(0f, openUntil - Time.time); } }
        public bool EverOpened { get; private set; }
        public bool Timed { get; private set; }
        /// <summary>Someone has walked up to it (so the objective can say "release it from the control room").</summary>
        public bool Found { get; private set; }

        float openUntil, t, nextSiren, nextLure;
        Quaternion closedRot;
        AudioSource siren;
        static AudioClip sirenClip;

        void Awake() { if (leaf != null) closedRot = leaf.localRotation; }

        bool HasKey(RemasteredPlayerController body)
        {
            var inv = body != null ? body.GetComponent<PlayerInventory>() : null;
            return inv != null && inv.Find(keyItem) != null;
        }

        public string Prompt(int player, RemasteredPlayerController body)
        {
            if (padlocked) return HasKey(body) ? "[E] Unlock the padlock with the groundskeeper's key" : "Padlocked as well - a tag: 'GROUNDSKEEPER'S KEY - ASK THE WATCHERS'";
            if (IsOpen) return null;
            return CampusPower.GridOn ? "Magnetic lock - released only from the security control room (admin building)" : "Magnetic lock - dead. No power, and it is only released from the control room";
        }

        public void Interact(int player, RemasteredPlayerController body)
        {
            if (padlocked && HasKey(body)) { Net.NetWorld.Act(this, 5, player); return; }
            if (!Found) Net.NetWorld.Act(this, 3, player);
            if (SfxBank.Instance != null) SfxBank.At(SfxBank.Instance.doorLocked, Centre + Vector3.up, 0.8f, 0.8f);
        }

        /// <summary>The console's release: hold (false) or timed (true).</summary>
        public void Release(int player, bool timed)
        {
            if (!CampusPower.GridOn) return;
            Net.NetWorld.Act(this, timed ? 2 : 1, player);
        }

        public bool NetApply(int action, int a, int b, Vector3 v)
        {
            if (action == 3)
            {
                if (Found) return false;
                Found = true;
                var nm = NarrationManager.Instance;
                if (nm != null) nm.TriggerNarration("campus_service_gate_found");
                return true;
            }
            if (action == 5)
            {
                if (!padlocked) return false;
                padlocked = false;
                if (padlock != null) padlock.SetActive(false);
                if (!Found) Found = true;
                if (PlayerParty.Instance != null) PlayerParty.Instance.Flash("The padlock drops off the chain. Now the magnetic lock - the control room.", 4f);
                return true;
            }
            if (action != 1 && action != 2) return false;
            Timed = action == 2;
            openUntil = Time.time + (Timed ? timedSeconds : holdSeconds);
            if (!EverOpened && PlayerParty.Instance != null) PlayerParty.Instance.Flash("The service gate's lock clacks open - GO!", 4f);
            EverOpened = true; Found = true;
            SfxBank.At(CampusSynth.Relay, Centre + Vector3.up, 1f, 0.7f, 60f);
            if (SfxBank.Instance != null) SfxBank.At(SfxBank.Instance.doorOpen, Centre + Vector3.up, 1f, 0.7f, 50f);
            return true;
        }

        void Update()
        {
            bool open = IsOpen && !padlocked;   // the magnet can let go, but a padlocked chain still holds it
            t = Mathf.MoveTowards(t, open ? 1f : 0f, Time.deltaTime / swingTime);
            if (leaf != null) leaf.localRotation = closedRot * Quaternion.Euler(0f, -openAngle * Mathf.SmoothStep(0f, 1f, t), 0f);
            if (blocker != null) blocker.enabled = t < 0.6f;
            if (beacons != null)
                foreach (var b in beacons) if (b != null) b.SetColor(open ? (Mathf.Repeat(Time.time, 0.6f) < 0.3f ? new Color(1f, 0.6f, 0.1f) : new Color(0.3f, 1f, 0.35f)) : new Color(1f, 0.1f, 0.06f));
            // the timed release's siren: everyone hears where the gate is, and so does everything else
            if (open && Timed)
            {
                if (siren == null) { var sgo = new GameObject("Siren"); sgo.transform.SetParent(transform, false); sgo.transform.position = Centre + Vector3.up * 3f; siren = CampusSynth.Loop(sgo, Siren(), 1f, 200f); }
                if (!siren.isPlaying) siren.Play();
                if (Time.time > nextLure) { nextLure = Time.time + 4f; CampusLure.Emit(Centre, CampusLure.Kind.Siren, 260f, 90f); }
            }
            else if (siren != null && siren.isPlaying) siren.Stop();
        }

        static AudioClip Siren()
        {
            if (sirenClip != null) return sirenClip;
            const int rate = 44100; int n = rate * 3;
            var d = new float[n]; float ph = 0f;
            for (int i = 0; i < n; i++)
            {
                float tt = (float)i / rate;
                float f = 500f + 400f * Mathf.Sin(tt / 3f * Mathf.PI * 2f - Mathf.PI * 0.5f) * 0.5f + 200f;   // a slow wail up and down
                ph += f / rate;
                d[i] = Mathf.Sin(ph * Mathf.PI * 2f) * 0.35f + (Mathf.Repeat(ph, 1f) < 0.5f ? 0.08f : -0.08f);
            }
            sirenClip = AudioClip.Create("GateSiren", n, 1, rate, false);
            sirenClip.SetData(d, 0);
            return sirenClip;
        }
    }
}
