using UnityEngine;

namespace HollowTower.Campus
{
    /// <summary>
    /// A door that stays shut until a condition is met, then swings open for good: the administration building's main
    /// entrance (needs the main entrance key, which is kept) or the head administrator's office (a keypad code, through
    /// the Puzzles.PuzzleLock on the same object). Opening goes through NetWorld so every machine sees the same doors.
    /// </summary>
    public class CampusKeyDoor : MonoBehaviour, IPartyInteractable, Net.INetAction
    {
        public string doorName = "doors";
        [Tooltip("Item id that has to be in the player's inventory (empty: none).")]
        public string requiredItem = "";
        public string lockedText = "Locked";
        [Tooltip("Uses the PuzzleLock keypad on this object before opening.")]
        public bool needsCode;
        public Transform[] leaves;
        public float[] openAngles;
        public float openTime = 1.4f;
        [Tooltip("Gone once it opens (the chain across the main doors).")]
        public GameObject[] hideWhenOpen;

        public bool Open { get; private set; }

        Quaternion[] closed;
        float t;

        void Awake()
        {
            closed = new Quaternion[leaves != null ? leaves.Length : 0];
            for (int i = 0; i < closed.Length; i++) closed[i] = leaves[i] != null ? leaves[i].localRotation : Quaternion.identity;
        }

        bool Has(RemasteredPlayerController body)
        {
            if (string.IsNullOrEmpty(requiredItem)) return true;
            var inv = body != null ? body.GetComponent<PlayerInventory>() : null;
            return inv != null && inv.Find(requiredItem) != null;
        }

        public string Prompt(int player, RemasteredPlayerController body)
        {
            if (Open) return null;
            if (!Has(body)) return lockedText;
            if (needsCode)
            {
                var keypad = GetComponent<Puzzles.PuzzleLock>();
                if (keypad != null && !keypad.Solved) return keypad.Status("[E] Enter the code on the keypad");
            }
            return string.IsNullOrEmpty(requiredItem) ? "[E] Open the " + doorName : "[E] Unlock the " + doorName;
        }

        public void Interact(int player, RemasteredPlayerController body)
        {
            if (Open) return;
            if (!Has(body))
            {
                if (SfxBank.Instance != null) SfxBank.At(SfxBank.Instance.doorLocked, transform.position + Vector3.up, 0.8f);
                return;
            }
            if (needsCode)
            {
                var keypad = GetComponent<Puzzles.PuzzleLock>();
                if (keypad != null && !keypad.TryOpen(player, () => Net.NetWorld.Act(this, 1, player))) return;
            }
            Net.NetWorld.Act(this, 1, player);
        }

        public bool NetApply(int action, int a, int b, Vector3 v)
        {
            if (action != 1 || Open) return false;
            Open = true;
            // the box you pressed E on filled the doorway: out of the way now (the leaves keep their own colliders)
            foreach (var c in GetComponents<Collider>()) c.enabled = false;
            // the monsters' paths: a locked door carves the navmesh until it opens
            foreach (var o in GetComponentsInChildren<UnityEngine.AI.NavMeshObstacle>()) o.enabled = false;
            if (hideWhenOpen != null) foreach (var g in hideWhenOpen) if (g != null) g.SetActive(false);
            if (SfxBank.Instance != null) SfxBank.At(SfxBank.Instance.doorOpen, transform.position + Vector3.up, 1f, 0.9f, 18f);
            var party = PlayerParty.Instance;
            if (party != null && !string.IsNullOrEmpty(requiredItem)) party.Flash("Player " + (a + 1) + " unlocked the " + doorName, 3f);
            return true;
        }

        void Update()
        {
            if (!Open || t >= 1f || leaves == null) return;
            t = Mathf.MoveTowards(t, 1f, Time.deltaTime / openTime);
            float k = Mathf.SmoothStep(0f, 1f, t);
            for (int i = 0; i < leaves.Length; i++)
                if (leaves[i] != null) leaves[i].localRotation = closed[i] * Quaternion.Euler(0f, openAngles[i] * k, 0f);
        }
    }
}
