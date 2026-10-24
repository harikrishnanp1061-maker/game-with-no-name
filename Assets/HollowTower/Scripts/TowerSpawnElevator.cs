using System.Collections;
using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// Controls the Floor 1 Spawn Elevator in "Parking access".
    /// When the player begins the game, they are inside this cabin.
    /// After a brief immersion pause, the elevator chime sounds and the doors slide open,
    /// welcoming the player into Floor 1.
    /// </summary>
    public class TowerSpawnElevator : MonoBehaviour
    {
        public static TowerSpawnElevator Instance { get; private set; }

        [Header("Doors")]
        public Transform doorLeft;
        public Transform doorRight;
        public Vector3 doorSlideVector = new Vector3(0.008f, 0f, 0f);
        public float doorOpenSpeed = 1.2f;

        [Header("Timing")]
        public float initialDelay = 1.0f;

        [Header("Audio & Lighting")]
        public AudioSource audioSource;
        public Light interiorLight;

        [Header("Door Positions (Recorded)")]
        public Vector3 leftClosedPos;
        public Vector3 rightClosedPos;
        private Vector3 leftOpenPos;
        private Vector3 rightOpenPos;
        private bool hasOpened = false;

        void Awake()
        {
            Instance = this;
            if (doorLeft != null && doorRight != null)
            {
                if (leftClosedPos == Vector3.zero && rightClosedPos == Vector3.zero)
                {
                    leftClosedPos = doorLeft.localPosition;
                    rightClosedPos = doorRight.localPosition;
                }
                leftOpenPos = leftClosedPos + doorSlideVector;
                rightOpenPos = rightClosedPos - doorSlideVector;

                // Ensure doors start closed
                doorLeft.localPosition = leftClosedPos;
                doorRight.localPosition = rightClosedPos;
            }
        }

        void Start()
        {
            StartCoroutine(SpawnSequenceRoutine());
        }

        private IEnumerator SpawnSequenceRoutine()
        {
            yield return new WaitForSeconds(initialDelay);

            // Play elevator arrival chime
            PlayChime();

            yield return new WaitForSeconds(0.4f);

            // Smoothly slide doors open
            float elapsed = 0f;
            float duration = 1.6f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

                if (doorLeft != null) doorLeft.localPosition = Vector3.Lerp(leftClosedPos, leftOpenPos, t);
                if (doorRight != null) doorRight.localPosition = Vector3.Lerp(rightClosedPos, rightOpenPos, t);

                yield return null;
            }

            if (doorLeft != null) doorLeft.localPosition = leftOpenPos;
            if (doorRight != null) doorRight.localPosition = rightOpenPos;
            hasOpened = true;
        }

        private void PlayChime()
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.spatialBlend = 0.5f;
            // Synthesize subtle ding if no clip attached
            audioSource.PlayOneShot(CreateDingClip());
        }

        private AudioClip CreateDingClip()
        {
            int sampleRate = 44100;
            float length = 1.2f;
            int numSamples = (int)(sampleRate * length);
            float[] samples = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float decay = Mathf.Exp(-3.5f * t);
                samples[i] = (Mathf.Sin(2f * Mathf.PI * 784f * t) * 0.4f +
                              Mathf.Sin(2f * Mathf.PI * 1046.5f * t) * 0.3f) * decay * 0.5f;
            }
            var clip = AudioClip.Create("ElevatorDing", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
