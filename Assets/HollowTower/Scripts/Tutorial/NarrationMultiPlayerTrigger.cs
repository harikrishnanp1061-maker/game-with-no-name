using System.Collections.Generic;
using HollowTower;
using UnityEngine;

/// <summary>
/// A narration zone (same name and fields as the collage game's): plays its line once enough players have passed
/// through it. Watches the party's bodies against the zone's box, so it works for players steered on any machine.
/// </summary>
[RequireComponent(typeof(Collider))]
public class NarrationMultiPlayerTrigger : MonoBehaviour
{
    [SerializeField] string _narrationId = "CROSS_FIRST_HIDDEN_DOOR";
    [Range(1, 4)] [SerializeField] int _requiredPlayerCount = 2;
    [SerializeField] bool _cumulativeCrossing = true;
    [SerializeField] bool _triggerOnceOnly = true;

    readonly HashSet<int> crossed = new HashSet<int>();
    Collider zone;
    bool done;

    void Awake() { zone = GetComponent<Collider>(); zone.isTrigger = true; }

    void Update()
    {
        if (done && _triggerOnceOnly) return;
        var party = PlayerParty.Instance;
        if (party == null) return;
        int inside = 0;
        var b = zone.bounds;
        for (int i = 0; i < party.players.Length; i++)
        {
            var p = party.players[i];
            if (p == null || p.IsDead) continue;
            if (b.Contains(p.transform.position + Vector3.up)) { inside++; crossed.Add(i); }
        }
        int need = Mathf.Min(_requiredPlayerCount, PlayerParty.TwoPlayer ? 2 : 4);
        if ((_cumulativeCrossing ? crossed.Count : inside) >= need)
        {
            done = true;
            if (NarrationManager.Instance != null) NarrationManager.Instance.TriggerNarration(_narrationId);
        }
    }
}
