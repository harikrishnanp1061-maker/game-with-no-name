using System.Collections.Generic;
using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// The user's hand-picked sound effects (data/sfx_annotations_level3.json), cut and copied into
    /// Resources/Sfx/User: the giant's footsteps (a colossal boom from afar, the four heavy steps up close), the
    /// monster shout on a kill, the growl and lurking breath in a monster's quiet spells, the underwater moan of the
    /// thing still prowling the building, the drone as the party first steps out of it, the item pick-up, running
    /// indoors and on grass, jogging on grass, the breathing and heartbeat when worn out, the suffocation as a mind
    /// gives out. Loaded once, by name.
    /// </summary>
    public static class UserSfx
    {
        static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();

        public static AudioClip Get(string name)
        {
            AudioClip c;
            if (!cache.TryGetValue(name, out c)) { c = Resources.Load<AudioClip>("Sfx/User/" + name); cache[name] = c; }
            return c;
        }

        /// <summary>One of name_1 .. name_n.</summary>
        public static AudioClip Pick(string name, int n) { return Get(name + "_" + Random.Range(1, n + 1)); }
    }
}
