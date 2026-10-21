using System.Collections;
using UnityEngine;

namespace HollowTower
{
    [RequireComponent(typeof(BoxCollider))]
    public class TowerPitHazard : MonoBehaviour
    {
        [Header("Hazard Configuration")]
        public Vector3 safeRespawnPosition;
        public float fallDamage = 25f;
        public bool instantDeath = false;

        [Header("Audio & Effects")]
        public AudioClip pitFallClip;
        private AudioSource audioSource;
        private bool isTriggered = false;

        void Awake()
        {
            var col = GetComponent<BoxCollider>();
            col.isTrigger = true;

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 0.5f;
                audioSource.playOnAwake = false;
            }
        }

        void OnTriggerEnter(Collider other)
        {
            if (isTriggered) return;

            // Check if player
            if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null)
            {
                StartCoroutine(HandlePlayerPitFall(other.gameObject));
            }
        }

        IEnumerator HandlePlayerPitFall(GameObject player)
        {
            isTriggered = true;

            if (pitFallClip != null && audioSource != null)
            {
                audioSource.PlayOneShot(pitFallClip);
            }

            // Disable character controller momentarily to allow instant teleport
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;

            // Brief horror freeze / screen effect delay
            yield return new WaitForSeconds(0.4f);

            // Teleport to safe position
            Vector3 target = safeRespawnPosition != Vector3.zero ? safeRespawnPosition : transform.position + Vector3.up * 1.5f + Vector3.back * 2.5f;
            player.transform.position = target;

            yield return new WaitForSeconds(0.1f);
            if (cc != null) cc.enabled = true;

            yield return new WaitForSeconds(1.0f);
            isTriggered = false;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.1f, 0.1f, 0.45f);
            var col = GetComponent<BoxCollider>();
            if (col != null)
            {
                Gizmos.matrix = transform.localToWorldMatrix;
                Gizmos.DrawCube(col.center, col.size);
            }
        }
    }
}
