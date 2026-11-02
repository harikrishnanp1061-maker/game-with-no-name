using UnityEngine;

namespace HollowTower.Puzzles
{
    /// <summary>
    /// The code of a hidden-code keypad, written somewhere easy to miss nearby (a sticky note on the side of a filing
    /// cabinet, a scrawl on a desk). The host rolls the lock's seed as soon as the session is up, so the note shows the
    /// same digits on every machine before anyone tries the door.
    /// </summary>
    public class CodeNote : MonoBehaviour
    {
        public PuzzleLock target;
        public TextMesh text;
        [Tooltip("{0} is replaced with the code, digits separated by this.")]
        public string format = "{0}";
        public string separator = " ";

        int shownSeed = -1;

        void Update()
        {
            if (target == null || text == null) return;
            target.EnsureSeed();
            if (target.Seed == shownSeed) return;
            shownSeed = target.Seed;
            if (shownSeed == 0) { text.text = ""; return; }
            var c = target.Code.code;
            var parts = new string[c.Length];
            for (int i = 0; i < c.Length; i++) parts[i] = c[i].ToString();
            text.text = string.Format(format, string.Join(separator, parts));
        }
    }
}
