using UnityEngine;

namespace HollowTower.Puzzles
{
    /// <summary>One usable piece of a WorldPuzzle (a book, a mirror, a statue, a handwheel, a floor pad, the music box):
    /// E on it, or - for a pad - stepping onto it, presses that piece.</summary>
    public class WorldPuzzlePart : MonoBehaviour, IPartyInteractable
    {
        public WorldPuzzle puzzle;
        public int index;
        [Tooltip("Pressed by stepping onto it (a trigger collider) instead of E.")]
        public bool step;

        public string Prompt(int player, RemasteredPlayerController body)
        {
            if (puzzle == null || step) return null;
            return puzzle.PartPrompt(index, player);
        }

        public void Interact(int player, RemasteredPlayerController body)
        {
            if (puzzle != null && !step) puzzle.Press(index, player);
        }

        void OnTriggerEnter(Collider other)
        {
            if (!step || puzzle == null) return;
            var party = PlayerParty.Instance;
            if (party == null || party.Current == null || party.Current.IsDead) return;
            var body = other.GetComponentInParent<RemasteredPlayerController>();
            if (body != party.Current) return;   // only this machine's own player presses it
            puzzle.Press(index, party.CurrentIndex);
        }
    }
}
