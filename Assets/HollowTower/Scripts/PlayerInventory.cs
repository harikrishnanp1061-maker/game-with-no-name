using System.Collections.Generic;
using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// What one player is carrying. Each body has its own: P1-P3 have two slots, P4 has four. Items are the world
    /// objects themselves (PartyItem), hidden while carried and put back in the world when dropped, so a teammate can
    /// pick them up.
    /// </summary>
    public class PlayerInventory : MonoBehaviour
    {
        public int capacity = 2;

        readonly List<PartyItem> items = new List<PartyItem>();
        public IList<PartyItem> Items { get { return items; } }
        public int Selected { get; private set; }
        public bool IsFull { get { return items.Count >= capacity; } }

        public bool Add(PartyItem item)
        {
            if (item == null || IsFull || items.Contains(item)) return false;
            items.Add(item);
            Selected = items.Count - 1;
            return true;
        }

        public bool Remove(PartyItem item)
        {
            bool ok = items.Remove(item);
            Selected = Mathf.Clamp(Selected, 0, Mathf.Max(0, items.Count - 1));
            return ok;
        }

        public PartyItem Find(string itemId)
        {
            foreach (var i in items) if (i != null && i.itemId == itemId) return i;
            return null;
        }

        public void Cycle(int step)
        {
            if (items.Count == 0) return;
            Selected = (Selected + step + items.Count) % items.Count;
        }

        /// <summary>Leaves everything on the floor around a point (a player who died drops it at the pit's edge).</summary>
        public void DropAllAt(Vector3 point)
        {
            var all = new List<PartyItem>(items);   // dropping (through the network) takes them out of the list as it goes
            for (int i = 0; i < all.Count; i++)
            {
                var item = all[i];
                Vector3 at = point + Quaternion.Euler(0f, i * 72f, 0f) * new Vector3(0.35f, 0.5f, 0f);
                if (Physics.Raycast(at, Vector3.down, out RaycastHit hit, 2f, ~0, QueryTriggerInteraction.Ignore)
                    && hit.collider.GetComponentInParent<RemasteredPlayerController>() == null) at = hit.point;
                else at = point;
                item.Drop(at, transform.eulerAngles.y);
            }
            Selected = 0;
        }

        /// <summary>Puts the selected item down on the floor in front of this player.</summary>
        public void DropSelected()
        {
            if (items.Count == 0) return;
            var item = items[Mathf.Clamp(Selected, 0, items.Count - 1)];
            Vector3 at = transform.position + transform.forward * 0.8f + Vector3.up * 0.5f;
            if (Physics.Raycast(at, Vector3.down, out RaycastHit hit, 2f, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<RemasteredPlayerController>() == null)
                at = hit.point;
            else
                at = transform.position;   // nothing in front (a pit edge): drop it at our feet
            item.Drop(at, transform.eulerAngles.y);
        }
    }
}
