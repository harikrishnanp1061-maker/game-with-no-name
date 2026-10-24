using UnityEngine;

namespace HollowTower
{
    /// <summary>Anything the steered player can use with E (fuse cells, the fuse socket, false walls).</summary>
    public interface IPartyInteractable
    {
        /// <summary>Prompt for this player, or null when there is nothing for them to see or do here.</summary>
        string Prompt(int player, RemasteredPlayerController body);
        /// <summary>E pressed while looking at it (even with no prompt: anyone can push a false wall P1 pointed out).</summary>
        void Interact(int player, RemasteredPlayerController body);
    }

    /// <summary>
    /// Looks along the camera for an IPartyInteractable, shows its prompt and passes E on to it. Also shows the prompt of
    /// the ordinary doors (they handle E themselves), so a jammed or unpowered door says why it won't open.
    /// </summary>
    public class PartyInteractor : MonoBehaviour
    {
        public float reach = 3.0f;

        IPartyInteractable target;
        string prompt;
        GUIStyle style;

        void Update()
        {
            target = null;
            prompt = null;
            var party = PlayerParty.Instance;
            if (party == null || party.Current == null || party.Spectating || RemasteredMapHUD.IsFullMapOpen) return;
            var cam = party.playerCamera != null ? party.playerCamera : Camera.main;
            if (cam == null) return;

            var hits = Physics.SphereCastAll(cam.transform.position, 0.12f, cam.transform.forward, reach, ~0, QueryTriggerInteraction.Collide);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            float solidAt = float.MaxValue;
            foreach (var h in hits)
            {
                if (h.collider.GetComponentInParent<RemasteredPlayerController>() != null) continue;
                if (h.distance > solidAt + 0.35f) break;
                var it = h.collider.GetComponentInParent<IPartyInteractable>();
                if (it != null)
                {
                    target = it;
                    prompt = it.Prompt(party.CurrentIndex, party.Current);
                    break;
                }
                var door = h.collider.GetComponentInParent<InteractiveDoor>();
                if (door != null && h.distance <= door.interactDistance)
                {
                    prompt = door.isLocked ? door.lockedMessage : door.GetPrompt();
                    break;
                }
                if (!h.collider.isTrigger) solidAt = Mathf.Min(solidAt, h.distance);
            }

            if (target != null && InputWrap.Down(KeyCode.E)) target.Interact(party.CurrentIndex, party.Current);
        }

        void OnGUI()
        {
            if (string.IsNullOrEmpty(prompt)) return;
            if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            var r = new Rect(0f, Screen.height * 0.5f + 46f, Screen.width, 28f);
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), prompt, style);
            GUI.color = new Color(0.92f, 0.95f, 1f, 1f);
            GUI.Label(r, prompt, style);
            GUI.color = Color.white;
        }
    }
}
