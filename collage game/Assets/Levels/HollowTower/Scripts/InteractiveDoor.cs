using System.Collections;
using UnityEngine;

namespace HollowTower
{
    public class InteractiveDoor : MonoBehaviour
    {
        [Header("Door Configuration")]
        public Transform hingeTransform;
        public float openAngle = 90f;
        public float openSpeed = 3.5f;
        public bool isOpen = false;
        public bool isLocked = false;
        public string lockedMessage = "LOCKED - KEY OR FUSE REQUIRED";

        [Header("Interaction Details")]
        public float interactDistance = 2.8f;
        public string promptOpen = "[E] Open Door";
        public string promptClose = "[E] Close Door";

        private Quaternion closedRotation;
        private Quaternion targetRotation;
        private Coroutine swingCoroutine;
        private AudioSource audioSource;

        void Awake()
        {
            if (hingeTransform == null) hingeTransform = transform;
            closedRotation = hingeTransform.localRotation;
            targetRotation = closedRotation;

            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1.0f;
                audioSource.minDistance = 1.5f;
                audioSource.maxDistance = 12f;
                audioSource.playOnAwake = false;
            }
        }

        public string GetPrompt()
        {
            if (isLocked) return lockedMessage;
            return isOpen ? promptClose : promptOpen;
        }

        public bool CanInteract(Vector3 playerPosition)
        {
            return Vector3.Distance(transform.position, playerPosition) <= interactDistance;
        }

        public void ToggleDoor()
        {
            if (isLocked)
            {
                PlaySound(false);
                return;
            }

            isOpen = !isOpen;
            float angle = isOpen ? openAngle : 0f;
            targetRotation = closedRotation * Quaternion.Euler(0f, angle, 0f);

            if (swingCoroutine != null) StopCoroutine(swingCoroutine);
            swingCoroutine = StartCoroutine(SmoothSwing());

            PlaySound(true);
        }

        IEnumerator SmoothSwing()
        {
            while (Quaternion.Angle(hingeTransform.localRotation, targetRotation) > 0.5f)
            {
                hingeTransform.localRotation = Quaternion.Slerp(
                    hingeTransform.localRotation,
                    targetRotation,
                    Time.deltaTime * openSpeed
                );
                yield return null;
            }
            hingeTransform.localRotation = targetRotation;
            swingCoroutine = null;
        }

        void PlaySound(bool success)
        {
            if (audioSource == null) return;
            // Synthesize subtle creak / click
            audioSource.pitch = success ? (isOpen ? 1.0f : 0.85f) : 0.6f;
            audioSource.Play();
        }

        // Context raycast detection when player presses E
        void Update()
        {
            // If player is looking at door and presses E
            if (Input.GetKeyDown(KeyCode.E))
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Ray ray = new Ray(cam.transform.position, cam.transform.forward);
                    if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
                    {
                        if (hit.transform == transform || hit.transform.IsChildOf(transform) || hit.transform.parent == transform.parent)
                        {
                            ToggleDoor();
                        }
                    }
                }
            }
        }
    }
}
