using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// Falling a storey (through one of floor 2's broken or hidden floors, down into floor 1) doesn't kill, but it
    /// hurts: a scream on the way down, a crunch and a hard shake on landing, a second or so flat on the floor, then a
    /// limp for a while. The noise brings floor 1's monsters to look. Any drop of more than a few metres counts.
    /// </summary>
    public class HardLanding : MonoBehaviour
    {
        public float hardDrop = 2.8f;
        public float stunTime = 1.2f;
        public float limpTime = 7f;
        public float limpSpeed = 0.55f;

        class Track { public float peak; public bool airborne, screamed; }   // peak: the highest point of this fall
        readonly Dictionary<RemasteredPlayerController, Track> tracks = new Dictionary<RemasteredPlayerController, Track>();

        void Update()
        {
            var party = PlayerParty.Instance;
            if (party == null) return;
            foreach (var p in party.players)
            {
                if (p == null || p.IsDead || p.IsFrozen || !Net.NetBody.Mine(p)) continue;   // each machine catches its own falls
                var cc = p.GetComponent<CharacterController>();
                if (cc == null || !cc.enabled) continue;
                Track t;
                if (!tracks.TryGetValue(p, out t)) tracks[p] = t = new Track { peak = p.transform.position.y };
                if (cc.isGrounded)
                {
                    if (t.airborne && t.peak - p.transform.position.y > hardDrop) StartCoroutine(Land(p, party));
                    t.airborne = false; t.screamed = false;
                    t.peak = p.transform.position.y;
                }
                else
                {
                    if (!t.airborne) t.peak = p.transform.position.y;
                    t.airborne = true;
                    t.peak = Mathf.Max(t.peak, p.transform.position.y);
                    if (!t.screamed && t.peak - p.transform.position.y > 1.2f && cc.velocity.y < -5f)
                    {
                        t.screamed = true;
                        var bank = SfxBank.Instance;
                        if (bank != null) SfxBank.At(bank.fallScream, p.transform.position + Vector3.up * 1.5f, 0.9f, 1f, 30f);
                    }
                }
            }
        }

        IEnumerator Land(RemasteredPlayerController p, PlayerParty party)
        {
            var bank = SfxBank.Instance;
            if (bank != null) SfxBank.At(bank.fallImpact, p.transform.position, 1f, 1f, 24f);
            Net.NetWorld.Noise(p.transform.position, 22f);
            int i = System.Array.IndexOf(party.players, p);
            if (p == party.Current) { ChaosFx.Kick(1.5f); party.SetFade(0.6f); }
            party.Flash("Player " + (i + 1) + " fell through the floor!", 3f);

            p.SetFrozen(true);
            yield return new WaitForSeconds(stunTime);
            if (p == party.Current) party.SetFade(0f);
            if (p.IsDead) yield break;
            p.SetFrozen(false);

            float walk = p.walkSpeed, sprint = p.sprintSpeed;
            p.walkSpeed = walk * limpSpeed; p.sprintSpeed = sprint * limpSpeed;
            for (float t = 0f; t < limpTime; t += Time.deltaTime)
            {
                float k = Mathf.Lerp(limpSpeed, 1f, t / limpTime);
                p.walkSpeed = walk * k; p.sprintSpeed = sprint * k;
                yield return null;
            }
            p.walkSpeed = walk; p.sprintSpeed = sprint;
        }
    }
}
