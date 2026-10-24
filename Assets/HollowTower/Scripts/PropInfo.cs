using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// Measured size of a dressing prop, written by the editor's PropLibrary.
    /// The prefab pivot is the bottom centre (floor props), the back-face centre (wall props)
    /// or the top centre (ceiling props), and the front faces +Z.
    /// </summary>
    [DisallowMultipleComponent]
    public class PropInfo : MonoBehaviour
    {
        public int mount;       // 0 floor, 1 wall, 2 ceiling
        public Vector3 size;    // local bounds size
        public Vector3 center;  // local bounds centre
    }
}
