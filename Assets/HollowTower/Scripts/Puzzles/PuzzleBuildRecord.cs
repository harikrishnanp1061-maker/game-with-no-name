using System.Collections.Generic;
using UnityEngine;

namespace HollowTower.Puzzles
{
    /// <summary>Editor bookkeeping for a puzzle build: the dressing it hid to make room, so a rebuild can put it back.</summary>
    public class PuzzleBuildRecord : MonoBehaviour
    {
        public List<GameObject> hidden = new List<GameObject>();
    }
}
