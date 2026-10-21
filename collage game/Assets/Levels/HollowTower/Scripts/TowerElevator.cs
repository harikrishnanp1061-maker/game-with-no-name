using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HollowTower
{
    public class TowerElevator : MonoBehaviour
    {
        [Header("Elevator Components")]
        public Transform cabinTransform;
        public Transform cabinDoorL;
        public Transform cabinDoorR;
        public Light cabinInteriorLight;

        [Header("Shaft & Floors")]
        public float[] floorHeights = new float[] { 0.05f, 4.25f, 8.45f, 12.65f };
        public int currentFloor = 0; // 0 = Floor 1 (Ground), 3 = Floor 4 (Offices)
        public float travelSpeed = 3.0f;
        public float doorSlideDistance = 1.0f;
        public float doorSlideSpeed = 2.5f;

        [Header("State")]
        public bool isMoving = false;
        public bool isDoorsOpen = false;

        private AudioSource audioSource;
        private Coroutine moveCoroutine;
        private Coroutine doorCoroutine;

        void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1.0f;
                audioSource.playOnAwake = false;
            }
        }

        public void CallElevator(int targetFloor)
        {
            if (isMoving || targetFloor < 0 || targetFloor >= floorHeights.Length) return;

            if (targetFloor == currentFloor)
            {
                if (!isDoorsOpen) ToggleDoors(true);
                return;
            }

            StartCoroutine(MoveToFloorRoutine(targetFloor));
        }

        public void ToggleDoors(bool open)
        {
            if (isMoving) return;
            if (doorCoroutine != null) StopCoroutine(doorCoroutine);
            doorCoroutine = StartCoroutine(SlideDoorsRoutine(open));
        }

        IEnumerator MoveToFloorRoutine(int targetFloor)
        {
            isMoving = true;

            // 1. Close doors first if open
            if (isDoorsOpen)
            {
                yield return SlideDoorsRoutine(false);
                yield return new WaitForSeconds(0.3f);
            }

            // 2. Play elevator travel hum
            if (audioSource != null)
            {
                audioSource.pitch = 0.9f;
                audioSource.Play();
            }

            // 3. Smooth vertical translation of cabin
            float targetY = floorHeights[targetFloor];
            Vector3 startPos = cabinTransform.localPosition;
            Vector3 endPos = new Vector3(startPos.x, targetY, startPos.z);

            float dist = Mathf.Abs(endPos.y - startPos.y);
            float duration = dist / travelSpeed;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                cabinTransform.localPosition = Vector3.Lerp(startPos, endPos, t);
                yield return null;
            }

            cabinTransform.localPosition = endPos;
            currentFloor = targetFloor;
            isMoving = false;

            if (audioSource != null) audioSource.Stop();

            // 4. Ding chime upon arrival & open doors
            yield return new WaitForSeconds(0.2f);
            yield return SlideDoorsRoutine(true);
        }

        IEnumerator SlideDoorsRoutine(bool open)
        {
            isDoorsOpen = open;
            float targetOffset = open ? doorSlideDistance : 0f;

            if (cabinDoorL != null && cabinDoorR != null)
            {
                Vector3 leftStart = cabinDoorL.localPosition;
                Vector3 rightStart = cabinDoorR.localPosition;
                Vector3 leftEnd = new Vector3(-targetOffset, leftStart.y, leftStart.z);
                Vector3 rightEnd = new Vector3(targetOffset, rightStart.y, rightStart.z);

                float elapsed = 0f;
                float duration = 1.0f / doorSlideSpeed;

                while (elapsed < duration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                    cabinDoorL.localPosition = Vector3.Lerp(leftStart, leftEnd, t);
                    cabinDoorR.localPosition = Vector3.Lerp(rightStart, rightEnd, t);
                    yield return null;
                }

                cabinDoorL.localPosition = leftEnd;
                cabinDoorR.localPosition = rightEnd;
            }

            doorCoroutine = null;
        }

        void OnGUI()
        {
            // Simple elevator call prompt if player is nearby
            Camera cam = Camera.main;
            if (cam == null) return;

            float dist = Vector3.Distance(cam.transform.position, cabinTransform.position);
            if (dist < 4.0f && !isMoving)
            {
                GUI.Box(new Rect(Screen.width * 0.5f - 140, Screen.height - 80, 280, 50),
                    $"<color=yellow><b>ELEVATOR: FLOOR {currentFloor + 1}</b></color>\n[1] Ground  [2] School  [3] Hotel  [4] Offices");

                if (Input.GetKeyDown(KeyCode.Alpha1)) CallElevator(0);
                if (Input.GetKeyDown(KeyCode.Alpha2)) CallElevator(1);
                if (Input.GetKeyDown(KeyCode.Alpha3)) CallElevator(2);
                if (Input.GetKeyDown(KeyCode.Alpha4)) CallElevator(3);
                if (Input.GetKeyDown(KeyCode.E)) ToggleDoors(!isDoorsOpen);
            }
        }
    }
}
