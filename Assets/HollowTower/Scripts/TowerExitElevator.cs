using System.Collections;
using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// The exit elevator, in floor 2's locker room (floors 3 and 4 were cut). It has no power until the fuse cells go in:
    /// floor 1's cell (it pops back out of the stairwell lock) and the two hidden on floor 2. Whoever is inside presses E
    /// to put in what they carry; with all three in, E closes the doors and the party escapes.
    /// </summary>
    public class TowerExitElevator : MonoBehaviour, Net.INetAction
    {
        public static TowerExitElevator Instance { get; private set; }

        [Header("Doors")]
        public Transform doorLeft;
        public Transform doorRight;
        public Vector3 doorSlideVector = new Vector3(0.008f, 0f, 0f);
        public float doorCloseSpeed = 1.2f;

        [Header("Audio & Lighting")]
        public AudioSource audioSource;
        public Light interiorLight;

        [Header("Power")]
        public string cellId = "fuse_cell";
        public int cellsNeeded = 3;
        public int CellsIn { get; private set; }

        [Header("State")]
        public bool isPlayerInside = false;
        public bool hasEscaped = false;
        public bool isClosing = false;

        [Header("Door Positions (Recorded)")]
        public Vector3 leftClosedPos;
        public Vector3 rightClosedPos;
        private Vector3 leftOpenPos;
        private Vector3 rightOpenPos;
        private float escapeStartTime = 0f;

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

                // Start with doors OPEN so player can enter
                doorLeft.localPosition = leftOpenPos;
                doorRight.localPosition = rightOpenPos;
            }
        }

        private BoxCollider triggerCollider;
        private Transform playerTransform;

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null)
            {
                isPlayerInside = true;
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player") || other.GetComponent<CharacterController>() != null)
            {
                if (!hasEscaped) isPlayerInside = false;
            }
        }

        void Update()
        {
            if (triggerCollider == null) triggerCollider = GetComponent<BoxCollider>();
            if (playerTransform == null || !playerTransform.CompareTag("Player"))   // the steered body changes with F1-F4
            {
                var p = GameObject.FindWithTag("Player");
                if (p != null) playerTransform = p.transform;
            }

            // High-reliability bounds detection for CharacterController
            if (triggerCollider != null && playerTransform != null && !hasEscaped && !isClosing)
            {
                isPlayerInside = triggerCollider.bounds.Contains(playerTransform.position + Vector3.up * 0.5f);
            }

            if (isPlayerInside && !hasEscaped && !isClosing)
            {
                if (InputWrap.Down(KeyCode.E))
                {
                    var party = PlayerParty.Instance;
                    if (CellsIn >= cellsNeeded) Net.NetWorld.Act(this, 2);
                    else if (party != null && party.Current != null)
                    {
                        var inv = party.Current.GetComponent<PlayerInventory>();
                        if (inv != null && inv.Find(cellId) != null) Net.NetWorld.Act(this, 1, party.CurrentIndex);
                        else party.Flash("No power - the elevator needs " + (cellsNeeded - CellsIn) + " more fuse cell" + (cellsNeeded - CellsIn == 1 ? "" : "s"));
                    }
                }
            }

            if (hasEscaped && InputWrap.Down(KeyCode.R))
            {
                if (!Net.Net.Active) RestartGame();
                else if (Net.Net.IsAuthority) Net.NetLobby.LoadLevel(Net.NetLobby.LobbyScene);   // everyone back to the lobby
            }
        }

        public bool NetApply(int action, int a, int b, Vector3 v)
        {
            if (action == 2) { if (CellsIn < cellsNeeded || isClosing || hasEscaped) return false; TriggerEscape(); return true; }
            if (action != 1 || CellsIn >= cellsNeeded) return false;
            InsertCells(a);
            return true;
        }

        void InsertCells(int player)
        {
            var party = PlayerParty.Instance;
            var body = party != null && player >= 0 && player < party.players.Length ? party.players[player] : null;
            var inv = body != null ? body.GetComponent<PlayerInventory>() : null;
            int n = 0;
            PartyItem cell;
            while (inv != null && CellsIn < cellsNeeded && (cell = inv.Find(cellId)) != null) { cell.Consume(); CellsIn++; n++; }
            if (party == null) return;
            if (n == 0) { party.Flash("No power - the elevator needs " + (cellsNeeded - CellsIn) + " more fuse cell" + (cellsNeeded - CellsIn == 1 ? "" : "s")); return; }
            if (SfxBank.Instance != null) SfxBank.At(SfxBank.Instance.socketPower, transform.position, 1f, 1f, 25f);
            party.Flash(CellsIn >= cellsNeeded ? "The elevator hums to life - [E] to go" : "Fuse cell in - " + CellsIn + " of " + cellsNeeded);
        }

        public void TriggerEscape()
        {
            if (!isClosing && !hasEscaped)
            {
                StartCoroutine(EscapeSequenceRoutine());
            }
        }

        public void RestartGame()
        {
            // Restart to Floor 1 spawn elevator
            var p = GameObject.FindWithTag("Player");
            if (p != null)
            {
                var cc = p.GetComponent<CharacterController>();
                if (cc != null) cc.enabled = false;
                p.transform.position = new Vector3(141.60f, 0.20f, 12.00f);
                p.transform.rotation = Quaternion.Euler(0f, 270f, 0f);
                if (cc != null) cc.enabled = true;
            }
            hasEscaped = false;
            isClosing = false;
            isPlayerInside = false;

            // Re-open exit doors
            if (doorLeft != null) doorLeft.localPosition = leftOpenPos;
            if (doorRight != null) doorRight.localPosition = rightOpenPos;

            // Reset Floor culling
            if (TowerFloorCulling.Instance != null)
            {
                TowerFloorCulling.Instance.ForceFloorUpdate(0);
            }
        }

        public IEnumerator EscapeSequenceRoutine()
        {
            isClosing = true;
            PlayDoorCloseSound();

            // Smoothly close doors
            float elapsed = 0f;
            float duration = 1.6f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);

                if (doorLeft != null) doorLeft.localPosition = Vector3.Lerp(leftOpenPos, leftClosedPos, t);
                if (doorRight != null) doorRight.localPosition = Vector3.Lerp(rightOpenPos, rightClosedPos, t);

                yield return null;
            }

            if (doorLeft != null) doorLeft.localPosition = leftClosedPos;
            if (doorRight != null) doorRight.localPosition = rightClosedPos;

            // Motor hum and descent sound
            PlayDescentHum();
            yield return new WaitForSeconds(0.6f);

            hasEscaped = true;
            escapeStartTime = Time.time;
        }

        void OnGUI()
        {
            // Interaction prompt when inside elevator
            if (isPlayerInside && !isClosing && !hasEscaped)
            {
                var promptStyle = new GUIStyle(GUI.skin.box);
                promptStyle.fontSize = 18;
                promptStyle.fontStyle = FontStyle.Bold;
                promptStyle.normal.textColor = new Color(0.3f, 1f, 0.5f);
                promptStyle.alignment = TextAnchor.MiddleCenter;

                float bw = 460f;
                float bh = 54f;
                float bx = (Screen.width - bw) * 0.5f;
                float by = Screen.height * 0.72f;

                GUI.backgroundColor = new Color(0.08f, 0.12f, 0.18f, 0.95f);
                string label = CellsIn >= cellsNeeded ? "[ E ] CLOSE THE DOORS AND ESCAPE"
                             : "[ E ] INSERT FUSE CELLS   (" + CellsIn + " / " + cellsNeeded + " powered)";
                GUI.Box(new Rect(bx, by, bw, bh), label, promptStyle);
            }

            // Victory screen overlay upon escape
            if (hasEscaped)
            {
                // Dark cinematic background
                GUI.color = new Color(0f, 0f, 0f, 0.85f);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;

                float boxW = 600f;
                float boxH = 320f;
                float boxX = (Screen.width - boxW) * 0.5f;
                float boxY = (Screen.height - boxH) * 0.45f;

                var panelStyle = new GUIStyle(GUI.skin.box);
                panelStyle.alignment = TextAnchor.UpperCenter;
                GUI.backgroundColor = new Color(0.06f, 0.10f, 0.15f, 0.96f);
                GUI.Box(new Rect(boxX, boxY, boxW, boxH), GUIContent.none, panelStyle);

                var titleStyle = new GUIStyle(GUI.skin.label);
                titleStyle.fontSize = 28;
                titleStyle.fontStyle = FontStyle.Bold;
                titleStyle.normal.textColor = new Color(0.25f, 1.0f, 0.45f);
                titleStyle.alignment = TextAnchor.MiddleCenter;

                GUI.Label(new Rect(boxX, boxY + 25f, boxW, 40f), "FACILITY EVACUATION SUCCESSFUL", titleStyle);

                var subStyle = new GUIStyle(GUI.skin.label);
                subStyle.fontSize = 16;
                subStyle.normal.textColor = new Color(0.85f, 0.92f, 1.0f);
                subStyle.alignment = TextAnchor.MiddleCenter;

                string details = "Objective Complete: Powered the exit elevator and got out\n" +
                                 "Tactical Facility Status: Cleared & Survived\n" +
                                 "Extraction Vehicle: Operational";
                GUI.Label(new Rect(boxX + 20f, boxY + 80f, boxW - 40f, 90f), details, subStyle);

                var promptRestartStyle = new GUIStyle(GUI.skin.box);
                promptRestartStyle.fontSize = 17;
                promptRestartStyle.fontStyle = FontStyle.Bold;
                promptRestartStyle.normal.textColor = Color.white;
                promptRestartStyle.alignment = TextAnchor.MiddleCenter;

                GUI.backgroundColor = new Color(0.12f, 0.25f, 0.38f, 0.9f);
                if (GUI.Button(new Rect(boxX + 110f, boxY + 195f, 380f, 48f), "[ R ] Restart from Floor 1 Spawn Elevator", promptRestartStyle))
                {
                    RestartGame();
                }

                var hintStyle = new GUIStyle(GUI.skin.label);
                hintStyle.fontSize = 13;
                hintStyle.normal.textColor = new Color(0.6f, 0.7f, 0.8f);
                hintStyle.alignment = TextAnchor.MiddleCenter;
                GUI.Label(new Rect(boxX, boxY + 260f, boxW, 30f), "Press [M] to toggle facility blueprint at any time", hintStyle);
            }
        }

        private void PlayDoorCloseSound()
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

            audioSource.spatialBlend = 0.5f;
            audioSource.PlayOneShot(CreateDingClip(523.25f)); // C5 chime
        }

        private void PlayDescentHum()
        {
            if (audioSource != null)
            {
                audioSource.pitch = 0.75f;
                audioSource.Play();
            }
        }

        private AudioClip CreateDingClip(float freq)
        {
            int sampleRate = 44100;
            float length = 1.0f;
            int numSamples = (int)(sampleRate * length);
            float[] samples = new float[numSamples];
            for (int i = 0; i < numSamples; i++)
            {
                float t = (float)i / sampleRate;
                float decay = Mathf.Exp(-4.0f * t);
                samples[i] = Mathf.Sin(2f * Mathf.PI * freq * t) * decay * 0.4f;
            }
            var clip = AudioClip.Create("ElevatorDingClose", numSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
