using System.Collections;
using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// A false wall (the collage game tutorial's HiddenDoor). Until it is opened it IS the original wall segment, so to
    /// everyone but P1 it is indistinguishable from the rest of the wall; P1 sees it painted bright orange (a
    /// PartyPerception child). Everyone gets the push prompt when looking at it, so P1 only has to say where it is.
    /// Opening hides the wall segment, shows a doorway frame in its place and slides a panel down into the floor.
    /// </summary>
    public class HiddenWallDoor : MonoBehaviour, IPartyInteractable, Net.INetAction
    {
        public int seer = 0;
        [Tooltip("The original wall segment and baseboard the doorway is cut through (hidden, with their colliders, on opening).")]
        public GameObject[] hideOnOpen;
        [Tooltip("Posts and lintel left standing once the door is open (inactive until then).")]
        public GameObject openFrame;
        [Tooltip("The panel that slides into the floor (inactive until the door opens).")]
        public Transform panel;
        public GameObject outline;
        public float slideTime = 1.6f;

        public bool IsOpen { get; private set; }
        bool opening;
        Transform paintAnchor;
        static readonly System.Collections.Generic.List<HiddenWallDoor> all = new System.Collections.Generic.List<HiddenWallDoor>();

        void OnEnable() { all.Add(this); }
        void OnDisable() { all.Remove(this); }

        /// <summary>Where spray paint on this false wall is kept, so it goes with the panel when the wall opens.</summary>
        public Transform PaintAnchor
        {
            get
            {
                if (paintAnchor == null)
                {
                    paintAnchor = new GameObject("Paint").transform;
                    paintAnchor.SetParent(transform, false);
                }
                return paintAnchor;
            }
        }

        /// <summary>The false wall whose wall segment this collider belongs to, if any.</summary>
        public static HiddenWallDoor ForCollider(Collider c)
        {
            foreach (var d in all)
                if (d.hideOnOpen != null)
                    foreach (var go in d.hideOnOpen)
                        if (go != null && c.transform.IsChildOf(go.transform)) return d;
            return null;
        }

        public string Prompt(int player, RemasteredPlayerController body)
        {
            if (IsOpen || opening) return null;
            return "[E] Push the false wall";
        }

        public void Interact(int player, RemasteredPlayerController body)
        {
            if (IsOpen || opening) return;
            Net.NetWorld.Act(this, 1, player);
        }

        public bool NetApply(int action, int a, int b, Vector3 v)
        {
            if (action != 1 || IsOpen || opening) return false;
            StartCoroutine(Open(a));
            return true;
        }

        IEnumerator Open(int player)
        {
            opening = true;
            if (hideOnOpen != null)
                foreach (var go in hideOnOpen)
                {
                    if (go == null) continue;
                    foreach (var r in go.GetComponentsInChildren<Renderer>()) r.enabled = false;
                    foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
                }
            if (openFrame != null) openFrame.SetActive(true);
            if (outline != null) outline.SetActive(false);
            var bank = SfxBank.Instance;
            if (bank != null) { SfxBank.At(bank.wallGrind, transform.position + Vector3.up * 1.3f, 1f, 1f, 30f); NeedleMonster.Hear(transform.position, 12f); }
            if (PlayerParty.Instance != null) PlayerParty.Instance.Flash(player == seer ? "The false wall grinds open" : "Player " + (player + 1) + " found the false wall");

            if (panel != null)
            {
                panel.gameObject.SetActive(true);
                if (paintAnchor != null) paintAnchor.SetParent(panel, true);   // the marks go down with the panel
                Vector3 a = panel.localPosition, b = a + Vector3.down * 2.7f;
                var cols = panel.GetComponentsInChildren<Collider>();
                for (float t = 0f; t < 1f; t += Time.deltaTime / slideTime)
                {
                    float e = t * t * (3f - 2f * t);
                    panel.localPosition = Vector3.Lerp(a, b, e) + new Vector3(Mathf.Sin(t * 90f) * 0.004f, 0f, 0f);
                    if (t > 0.55f) foreach (var c in cols) c.enabled = false;
                    yield return null;
                }
                panel.gameObject.SetActive(false);
            }
            IsOpen = true;
            opening = false;
        }
    }
}
