using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// Turns the carousel while the party music plays; when the music dies it slows to a stop with the tape, and starts
    /// again when the music comes back.
    /// </summary>
    public class PartySpinner : MonoBehaviour
    {
        public float degreesPerSecond = 22f;
        float speed;

        void Update()
        {
            var party = PartyFloor.Instance;
            bool playing = party == null || party.Current == PartyFloor.Phase.Explore;
            float target = playing ? degreesPerSecond : 0f;
            speed = Mathf.MoveTowards(speed, target, Time.deltaTime * (playing ? 6f : 14f));
            transform.Rotate(0f, speed * Time.deltaTime, 0f, Space.Self);
        }
    }
}
