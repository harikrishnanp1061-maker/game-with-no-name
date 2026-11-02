using UnityEngine;

namespace HollowTower.Campus
{
    /// <summary>
    /// The ride up from floor 2 ends here: the screen fades in inside the elevator, it settles with a chime, and the
    /// doors slide open onto the administration building's lower floor.
    /// </summary>
    public class CampusArrival : MonoBehaviour
    {
        public Transform leftDoor, rightDoor;
        public Vector3 slide = new Vector3(1.05f, 0f, 0f);
        public float fadeTime = 2.2f, openDelay = 1.2f, openTime = 1.8f;
        [Tooltip("Off when the cab (TowerSpawnElevator) plays its own ding.")]
        public bool playChime = true;

        Vector3 leftClosed, rightClosed;
        float start;
        bool chimed;
        AudioSource audioSource;

        void Start()
        {
            start = Time.time;
            if (leftDoor != null) leftClosed = leftDoor.localPosition;
            if (rightDoor != null) rightClosed = rightDoor.localPosition;
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.spatialBlend = 0.6f; audioSource.maxDistance = 25f;
            if (PlayerParty.Instance != null) PlayerParty.Instance.SetFade(1f);
        }

        void Update()
        {
            float t = Time.time - start;
            var party = PlayerParty.Instance;
            if (t < fadeTime + 0.1f && party != null) party.SetFade(1f - Mathf.Clamp01(t / fadeTime));
            if (!chimed && t > fadeTime + openDelay - 0.4f)
            {
                chimed = true;
                if (playChime) audioSource.PlayOneShot(Chime(), 0.8f);
                if (party != null) party.Flash("Ground floor - Administration", 3f);
            }
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - fadeTime - openDelay) / openTime));
            if (leftDoor != null) leftDoor.localPosition = leftClosed - slide * k;
            if (rightDoor != null) rightDoor.localPosition = rightClosed + slide * k;
            if (k >= 1f) enabled = false;
        }

        static AudioClip chime;
        static AudioClip Chime()
        {
            if (chime != null) return chime;
            const int rate = 44100;
            int n = (int)(rate * 1.4f);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                float a = t < 0.45f ? Mathf.Sin(2f * Mathf.PI * 784f * t) : Mathf.Sin(2f * Mathf.PI * 587.3f * t);
                s[i] = a * Mathf.Exp(-3f * (t < 0.45f ? t : t - 0.45f)) * 0.35f;
            }
            chime = AudioClip.Create("CampusChime", n, 1, rate, false);
            chime.SetData(s, 0);
            return chime;
        }
    }
}
