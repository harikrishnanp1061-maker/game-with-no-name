using System.Collections.Generic;
using UnityEngine;

namespace HollowTower.Campus
{
    /// <summary>
    /// A bank of lights the control room can switch from its console: a wing of the administration building, or a set
    /// of floodlights out on the grounds (those need the generator). Throwing them on - the relays clunk, the tubes
    /// buzz - draws a Nailed that can see the glow, and floodlights pull the giant's head round: light up the way you
    /// are NOT going. Switching them off draws nothing. A short cooldown stops it flickering like a strobe.
    /// </summary>
    public class LightZone : MonoBehaviour, Net.INetAction
    {
        public static readonly List<LightZone> All = new List<LightZone>();

        public string label = "Lights";
        public bool outdoor;
        public CampusLamp[] lamps;
        [Tooltip("The area (world x/z) it lights, for the console map.")]
        public Rect area;
        public float floorY;
        public float nailedRange = 30f, giantRange = 160f, cooldown = 6f;
        public bool startOn;

        public bool On { get; private set; }
        public bool Ready { get { return Time.time > readyAt && (!outdoor || CampusPower.GridOn); } }
        float readyAt;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Start() { Apply(startOn, false); }

        public Vector3 Centre { get { return new Vector3(area.center.x, floorY + 1f, area.center.y); } }

        /// <summary>Flip it (the console).</summary>
        public void Toggle(int player) { if (Ready) Net.NetWorld.Act(this, On ? 2 : 1, player); }

        public bool NetApply(int action, int a, int b, Vector3 v)
        {
            if (action == 1 && !On) { Apply(true, true); return true; }
            if (action == 2 && On) { Apply(false, true); return true; }
            return false;
        }

        void Apply(bool on, bool loud)
        {
            On = on;
            readyAt = Time.time + cooldown;
            if (lamps != null) foreach (var l in lamps) if (l != null) l.powered = on;
            if (!loud) return;
            SfxBank.At(CampusSynth.Relay, Centre, 1f, on ? 1f : 0.8f, outdoor ? 50f : 25f);
            if (on) CampusLure.Emit(Centre, CampusLure.Kind.Lights, outdoor ? giantRange : 0f, nailedRange);
        }
    }
}
