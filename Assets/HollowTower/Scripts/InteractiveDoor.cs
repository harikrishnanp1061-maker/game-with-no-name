using System.Collections;
using UnityEngine;

namespace HollowTower
{
    public class InteractiveDoor : MonoBehaviour, Net.INetAction
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

        /// <summary>Open or close it, the same on every machine (a player's E, a monster barging through).</summary>
        public void SetOpen(bool open)
        {
            if (isLocked) { PlaySound(false); return; }
            if (open != isOpen) Net.NetWorld.Act(this, 1, open ? 1 : 0);
        }

        public bool NetApply(int action, int a, int b, Vector3 v)
        {
            if (action != 1 || isLocked) return false;
            if (isOpen != (a == 1)) ToggleDoor();
            return true;
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
            var bank = SfxBank.Instance;
            if (bank != null)
            {
                // a creak or a thud, or a rattle when it won't move; a door being used is a noise monsters hear
                audioSource.clip = !success ? bank.doorLocked : isOpen ? bank.doorOpen : bank.doorClose;
                audioSource.pitch = Random.Range(0.93f, 1.07f);
                audioSource.volume = 0.7f;
                audioSource.Play();
                NeedleMonster.Hear(transform.position, success ? 7f : 5f);
                return;
            }
            // Synthesize subtle creak / click
            audioSource.pitch = success ? (isOpen ? 1.0f : 0.85f) : 0.6f;
            audioSource.Play();
        }

        // Context raycast detection when player presses E
        void Update()
        {
            // If player is looking at door and presses E
            if (InputWrap.Down(KeyCode.E) && (PlayerParty.Instance == null || !PlayerParty.Instance.Spectating))   // the dead can't open doors
            {
                Camera cam = Camera.main;
                if (cam != null)
                {
                    Ray ray = new Ray(cam.transform.position, cam.transform.forward);
                    if (Physics.Raycast(ray, out RaycastHit hit, interactDistance))
                    {
                        if (hit.transform == transform || hit.transform.IsChildOf(transform) || hit.transform.parent == transform.parent)
                        {
                            SetOpen(!isOpen);
                        }
                    }
                }
            }
        }
    }
}
