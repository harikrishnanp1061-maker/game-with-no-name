using System.Collections.Generic;
using UnityEngine;

namespace HollowTower.Campus
{
    /// <summary>
    /// An office phone or a payphone the control room can dial from its console. It rings, loud, for a quarter of a
    /// minute: a Nailed within earshot comes to find it, and out on the grounds the giant turns to look too. Then the
    /// line needs a while before it can be rung again. Payphones outdoors are on the campus grid (the generator).
    /// </summary>
    public class CampusPhone : MonoBehaviour, Net.INetAction
    {
        public static readonly List<CampusPhone> All = new List<CampusPhone>();

        public string label = "Phone";
        public bool outdoor;
        public float ringSeconds = 14f, cooldown = 25f;
        public float nailedRange = 32f, giantRange = 120f;
        public Light bellLight;

        public bool Ringing { get { return Time.time < ringUntil; } }
        public bool Ready { get { return Time.time > readyAt && (!outdoor || CampusPower.GridOn); } }
        public float CooldownLeft { get { return Mathf.Max(0f, readyAt - Time.time); } }

        float ringUntil, readyAt, nextLure;
        AudioSource src;

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        /// <summary>Dial it (the console).</summary>
        public void Dial(int player) { if (Ready) Net.NetWorld.Act(this, 1, player); }

        public bool NetApply(int action, int a, int b, Vector3 v)
        {
            if (action != 1 || Ringing || (Time.time < readyAt && Net.Net.IsAuthority)) return false;
            ringUntil = Time.time + ringSeconds;
            readyAt = ringUntil + cooldown;
            if (src == null) src = CampusSynth.Loop(gameObject, CampusSynth.PhoneRing, 1f, outdoor ? 60f : 40f);
            src.Play();
            nextLure = 0f;
            return true;
        }

        void Update()
        {
            if (Ringing)
            {
                if (Time.time > nextLure)
                {
                    nextLure = Time.time + 2.5f;
                    CampusLure.Emit(transform.position, CampusLure.Kind.Phone, outdoor ? giantRange : 0f, nailedRange);
                }
                if (bellLight != null) bellLight.enabled = Mathf.Repeat(Time.time, 3f) < 1f;
            }
            else
            {
                if (src != null && src.isPlaying) src.Stop();
                if (bellLight != null && bellLight.enabled) bellLight.enabled = false;
            }
        }
    }
}
