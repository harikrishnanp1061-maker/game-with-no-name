using System.Collections.Generic;
using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// Sound-based stealth. Anything loud (footsteps, gunfire, alarms) calls Emit,
    /// and every enemy within earshot gets told where it came from.
    /// </summary>
    public static class NoiseSystem
    {
        public interface IListener
        {
            Vector3 ListenPosition { get; }
            void HearNoise(Vector3 position, float loudness);
        }

        static readonly List<IListener> listeners = new List<IListener>();

        public static void Register(IListener l) { if (!listeners.Contains(l)) listeners.Add(l); }
        public static void Unregister(IListener l) { listeners.Remove(l); }
        public static void Clear() { listeners.Clear(); }

        /// <param name="radius">How far away (metres) the noise can be heard. Sound is muffled across floors.</param>
        public static void Emit(Vector3 position, float radius)
        {
            for (int i = listeners.Count - 1; i >= 0; i--)
            {
                var l = listeners[i];
                Vector3 d = l.ListenPosition - position;
                float floorGap = Mathf.Abs(d.y);
                // Each storey of concrete between you and the noise halves its reach.
                float effective = radius * Mathf.Pow(0.5f, Mathf.Floor(floorGap / (LevelBuilder.Story * 0.75f)));
                d.y = 0f;
                float dist = d.magnitude;
                if (dist <= effective)
                    l.HearNoise(position, 1f - dist / Mathf.Max(0.01f, effective));
            }
        }
    }
}
