using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Controls the 3 power sockets on the Exit Elevator and coordinates door opening
/// when all 3 cells are installed.
/// </summary>
[AddComponentMenu("FLIP//FALL/Gameplay/Elevator Power Socket")]
[DisallowMultipleComponent]
public class ElevatorPowerSocket : MonoBehaviour, IInteractable
{
    public static event Action OnElevatorPowered;

    [Header("Socket References")]
    [SerializeField] private Transform _socket01;
    [SerializeField] private Transform _socket02;
    [SerializeField] private Transform _socket03;
    [SerializeField] private Renderer _socket03StatusLed;

    [Header("Prefabs")]
    [SerializeField] private GameObject _fuseCellPrefab;

    [Header("Door References")]
    [SerializeField] private Transform _leftSlidingDoor;
    [SerializeField] private Transform _rightSlidingDoor;
    [SerializeField] private Animation _doorAnimation;

    [Header("Door Positions (Local)")]
    [SerializeField] private float _leftClosedX = 0.00335f;
    [SerializeField] private float _leftOpenX = 0.00915f;
    [SerializeField] private float _rightClosedX = -0.00335f;
    [SerializeField] private float _rightOpenX = -0.00915f;
    [SerializeField] private float _doorY = -0.0098f;
    [SerializeField] private float _doorZ = 0.0105f;
    [SerializeField] private float _doorSlideDuration = 1.4f;

    [Header("Interaction Settings")]
    [SerializeField] private float _interactionDistance = 3.5f;
    [SerializeField] private string _promptInsert = "INSERT FUSE CELL";
    [SerializeField] private string _promptRequiresCell = "INSERT 3RD FUSE CELL";
    [SerializeField] private string _promptPowered = "ELEVATOR ACTIVE";
    [ColorUsage(true, true)]
    [SerializeField] private Color _promptReadyColor = new Color(0.2f, 1.0f, 0.4f, 1f);
    [ColorUsage(true, true)]
    [SerializeField] private Color _promptMissingColor = new Color(1.0f, 0.7f, 0.2f, 1f);

    [Header("Cabin Trigger")]
    [SerializeField] private Collider _cabinFinishTrigger;

    public GameObject FuseCellPrefab { get => _fuseCellPrefab; set => _fuseCellPrefab = value; }
    public Collider CabinFinishTrigger { get => _cabinFinishTrigger; set => _cabinFinishTrigger = value; }

    [Header("State")]
    [SerializeField] private bool _isPowered = false;
    private bool _doorsOpen = false;
    private GameObject _socket03CellInstance;

    [Header("End Game & 4-Player Boarding")]
    private readonly System.Collections.Generic.HashSet<PlayerCharacter> _playersInsideCabin = new System.Collections.Generic.HashSet<PlayerCharacter>();
    private bool _gameEnded = false;
    private bool _showVictoryScreen = false;

    public bool IsPowered => _isPowered;
    public bool IsGameEnded => _gameEnded;
    public int PlayersInsideCount => _playersInsideCabin.Count;
    public string State => _isPowered ? "Powered" : (FuseCellPickup.PlayerHasFuseCell ? "ReadyToInsert" : "AwaitingCell");

    public string PromptText
    {
        get
        {
            if (_isPowered) return _promptPowered;
            if (FuseCellPickup.PlayerHasFuseCell) return _promptInsert;
            return _promptRequiresCell;
        }
    }

    public Color PromptColor => FuseCellPickup.PlayerHasFuseCell ? _promptReadyColor : _promptMissingColor;
    public float InteractionDistance => _interactionDistance;

    private void Awake()
    {
        FindReferencesIfNeeded();
        _isPowered = false;
        _doorsOpen = false;
        _gameEnded = false;
        _showVictoryScreen = false;
        _playersInsideCabin.Clear();
    }

    private void Start()
    {
        InitializeSockets();
    }

    private void Update()
    {
        if (_doorsOpen && !_gameEnded)
        {
            UpdatePlayerBoarding();
        }

        if (_showVictoryScreen)
        {
            bool restartPressed = false;
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                restartPressed = UnityEngine.InputSystem.Keyboard.current.rKey.wasPressedThisFrame;
            }
#else
            restartPressed = Input.GetKeyDown(KeyCode.R);
#endif
            if (restartPressed)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
            }
        }
    }

    private void UpdatePlayerBoarding()
    {
        if (MultiPlayerCharacterManager.Instance != null)
        {
            foreach (PlayerCharacter p in MultiPlayerCharacterManager.Instance.AllPlayers)
            {
                if (p != null)
                {
                    if (IsPlayerInsideCabin(p))
                    {
                        if (_playersInsideCabin.Add(p))
                        {
                            Debug.Log($"[ElevatorPowerSocket] {p.Identity} boarded elevator! ({_playersInsideCabin.Count}/4 players inside)");
                        }
                    }
                    else
                    {
                        if (_playersInsideCabin.Remove(p))
                        {
                            Debug.Log($"[ElevatorPowerSocket] {p.Identity} exited elevator cabin. ({_playersInsideCabin.Count}/4 players inside)");
                        }
                    }
                }
            }

            if (_playersInsideCabin.Count >= 4 && !_gameEnded)
            {
                _gameEnded = true;
                StartCoroutine(EndGameSequenceRoutine());
            }
        }
    }

    public bool IsPlayerInsideCabin(PlayerCharacter pc)
    {
        if (pc == null) return false;
        Vector3 p = pc.transform.position;
        // Working_Elevator is at (-13.0, 0.0, 11.0). Cabin bounds in world space:
        float dx = Mathf.Abs(p.x - transform.position.x);
        float dz = Mathf.Abs(p.z - transform.position.z);
        return (dx <= 1.45f && dz <= 1.45f && p.y >= -0.5f && p.y <= 4.0f);
    }

    public void FindReferencesIfNeeded()
    {
        if (_socket01 == null) _socket01 = transform.Find("Exterior_Front_Assembly/Front_RightPillar_Base/PowerModule_MainHousing/Socket_01");
        if (_socket02 == null) _socket02 = transform.Find("Exterior_Front_Assembly/Front_RightPillar_Base/PowerModule_MainHousing/Socket_02");
        if (_socket03 == null) _socket03 = transform.Find("Exterior_Front_Assembly/Front_RightPillar_Base/PowerModule_MainHousing/Socket_03");

        if (_socket03StatusLed == null && _socket03 != null)
        {
            Transform led = _socket03.Find("Socket_03_StatusLED");
            if (led != null) _socket03StatusLed = led.GetComponent<Renderer>();
        }

        if (_leftSlidingDoor == null) _leftSlidingDoor = transform.Find("Exterior_Front_Assembly/LeftSlidingDoor");
        if (_rightSlidingDoor == null) _rightSlidingDoor = transform.Find("Exterior_Front_Assembly/RightSlidingDoor");

        // Destroy legacy Animation component to prevent baked (0,0,0) root keyframes from teleporting elevator
        Animation legacyAnim = GetComponent<Animation>();
        if (legacyAnim != null)
        {
            Destroy(legacyAnim);
            _doorAnimation = null;
        }
    }

    public void InitializeSockets()
    {
        if (_fuseCellPrefab == null) return;

        // Socket 1: Pre-installed Fuse Cell
        if (_socket01 != null && _socket01.Find("Preinstalled_FuseCell_01") == null)
        {
            InstallCellInSocket(_socket01, "Preinstalled_FuseCell_01");
        }

        // Socket 2: Pre-installed Fuse Cell
        if (_socket02 != null && _socket02.Find("Preinstalled_FuseCell_02") == null)
        {
            InstallCellInSocket(_socket02, "Preinstalled_FuseCell_02");
        }

        // Ensure doors start in closed position
        SetDoorsPosition(0f);
    }

    private GameObject InstallCellInSocket(Transform socketTransform, string instanceName)
    {
        if (socketTransform == null || _fuseCellPrefab == null) return null;

        GameObject cell = Instantiate(_fuseCellPrefab, socketTransform);
        cell.name = instanceName;
        cell.transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
        cell.transform.rotation = Quaternion.identity;

        // Position cell directly between the top and bottom socket clamps
        Transform top = socketTransform.Find(socketTransform.name + "_Clamp_Top");
        Transform bot = socketTransform.Find(socketTransform.name + "_Clamp_Bot");
        if (top != null && bot != null)
        {
            Renderer topR = top.GetComponent<Renderer>();
            Renderer botR = bot.GetComponent<Renderer>();
            if (topR != null && botR != null)
            {
                Vector3 clampMidpoint = (topR.bounds.center + botR.bounds.center) * 0.5f;
                cell.transform.position = clampMidpoint - new Vector3(0f, 0.078f, 0f);
            }
        }

        // Add radiant emerald glow point light to illuminate the socket clamps and housing
        Transform existingLight = socketTransform.Find("Socket_GlowLight");
        if (existingLight == null)
        {
            GameObject glowGo = new GameObject("Socket_GlowLight");
            glowGo.transform.SetParent(socketTransform, false);
            glowGo.transform.localPosition = new Vector3(0f, 0f, 0.0008f);
            Light pLight = glowGo.AddComponent<Light>();
            pLight.type = LightType.Point;
            pLight.color = new Color(0.15f, 1.0f, 0.35f);
            pLight.intensity = 2.8f;
            pLight.range = 0.85f;
            pLight.shadows = LightShadows.None;
        }

        // Remove any legacy Animation component to prevent it from resetting scale to (1,1,1) under 100x parent magnification
        Animation anim = cell.GetComponent<Animation>();
        if (anim != null)
        {
            Destroy(anim);
        }

        return cell;
    }

    private void PlayLoopingAnimation(GameObject cellObj)
    {
        // No-op: Animations on SM_FuseCell contain localScale (1,1,1) tracks that would blow up under 100x parent
        if (cellObj == null) return;
        Animation anim = cellObj.GetComponent<Animation>();
        if (anim != null)
        {
            Destroy(anim);
        }
    }

    public bool AreDoorsOpen => _doorsOpen;

    public void CloseDoors()
    {
        if (!_doorsOpen) return;
        StartCoroutine(CloseDoorsRoutine());
    }

    public void OpenDoors()
    {
        if (_doorsOpen) return;
        StartCoroutine(OpenDoorsRoutine());
    }

    private IEnumerator CloseDoorsRoutine()
    {
        _doorsOpen = false;
        Debug.Log("[ElevatorPowerSocket] Closing elevator doors...");

        float elapsed = 0f;
        while (elapsed < _doorSlideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _doorSlideDuration);
            float smoothT = t * t * (3f - 2f * t);
            SetDoorsPosition(1f - smoothT);
            yield return null;
        }

        SetDoorsPosition(0f);
        Debug.Log("[ElevatorPowerSocket] Elevator doors fully closed! Elevator descending. Objective accomplished!");

        // Trigger tutorial victory beat
        if (TutorialGameTracker.Instance != null)
        {
            TutorialGameTracker.Instance.CompleteBeat("elevator_depart", "Departed facility via working elevator", PlayerSession.Local);
        }
    }

    public bool CanInteract(PlayerSession player)
    {
        if (_isPowered) return false;
        if (player != null && player.IsActive)
        {
            Vector3 targetPos = _socket03 != null ? _socket03.position : transform.position;
            float dist = Vector3.Distance(player.HeadPosition, targetPos);
            if (dist > _interactionDistance) return false;
        }
        return true;
    }

    public bool OnInteract(PlayerSession player)
    {
        if (!CanInteract(player)) return false;

        if (!FuseCellPickup.PlayerHasFuseCell)
        {
            Debug.Log("[ElevatorPowerSocket] Missing the 3rd fuse cell. Locate it on the workbench in Area 7.");
            return false;
        }

        // Consume fuse cell
        FuseCellPickup.PlayerHasFuseCell = false;
        _isPowered = true;

        Debug.Log("[ElevatorPowerSocket] 3rd Fuse Cell successfully inserted! All 3 power sockets active. Opening elevator doors!");

        // Install 3rd cell into Socket 03
        if (_socket03 != null)
        {
            _socket03CellInstance = InstallCellInSocket(_socket03, "Inserted_FuseCell_03");
        }

        // Activate Socket 3 status LED
        if (_socket03StatusLed != null)
        {
            Material mat = _socket03StatusLed.material;
            if (mat != null)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", new Color(0.15f, 1.0f, 0.35f) * 5.0f);
            }
        }

        // Trigger door opening sequence
        StartCoroutine(OpenDoorsRoutine());

        // Notify subscribers & objective tracker
        OnElevatorPowered?.Invoke();
        ElevatorObjective elevObj = GetComponent<ElevatorObjective>();
        if (elevObj != null)
        {
            elevObj.SetPowered(true);
        }

        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.RefreshInteractables();
        }

        return true;
    }

    private IEnumerator OpenDoorsRoutine()
    {
        if (_doorsOpen) yield break;
        _doorsOpen = true;

        // Smoothly interpolate doors to open position
        float elapsed = 0f;
        while (elapsed < _doorSlideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _doorSlideDuration);
            // Smooth step easing
            float smoothT = t * t * (3f - 2f * t);
            SetDoorsPosition(smoothT);
            yield return null;
        }

        SetDoorsPosition(1f);
        Debug.Log("[ElevatorPowerSocket] Elevator doors fully open. Step inside to complete tutorial!");

        // Enable cabin trigger
        if (_cabinFinishTrigger != null)
        {
            _cabinFinishTrigger.enabled = true;
        }
    }

    private void SetDoorsPosition(float openAmount)
    {
        if (_leftSlidingDoor != null)
        {
            _leftSlidingDoor.localPosition = new Vector3(
                Mathf.Lerp(_leftClosedX, _leftOpenX, openAmount),
                _doorY,
                _doorZ
            );
        }

        if (_rightSlidingDoor != null)
        {
            _rightSlidingDoor.localPosition = new Vector3(
                Mathf.Lerp(_rightClosedX, _rightOpenX, openAmount),
                _doorY,
                _doorZ
            );
        }
    }

    private IEnumerator EndGameSequenceRoutine()
    {
        Debug.Log("[ElevatorPowerSocket] All 4 players have successfully entered the elevator! Initiating facility evacuation...");

        // 1. Lock all player controls inside elevator cabin
        if (MultiPlayerCharacterManager.Instance != null)
        {
            foreach (PlayerCharacter p in MultiPlayerCharacterManager.Instance.AllPlayers)
            {
                if (p != null && p.FirstPersonController != null)
                {
                    p.FirstPersonController.CanMove = false;
                    p.FirstPersonController.CanLook = false;
                }
            }
        }

        // 2. Mark arrival beat complete
        if (TutorialGameTracker.Instance != null)
        {
            TutorialGameTracker.Instance.CompleteBeat("elevator", "All 4 Players Reached Elevator", PlayerSession.Local);
        }

        yield return new WaitForSeconds(0.6f);

        // 3. Slide doors closed smoothly
        _doorsOpen = false;
        float elapsed = 0f;
        while (elapsed < _doorSlideDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / _doorSlideDuration);
            float smoothT = t * t * (3f - 2f * t);
            SetDoorsPosition(1f - smoothT);
            yield return null;
        }
        SetDoorsPosition(0f);

        Debug.Log("[ElevatorPowerSocket] Elevator doors sealed! Ascending to surface...");
        yield return new WaitForSeconds(1.0f);

        // 4. Trigger tutorial evacuation beat & victory
        if (TutorialGameTracker.Instance != null)
        {
            TutorialGameTracker.Instance.CompleteBeat("elevator_depart", "All 4 Players Departed Facility", PlayerSession.Local);
        }

        _showVictoryScreen = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnGUI()
    {
        // 1. Boarding Tracker banner when elevator doors are open
        if (_doorsOpen && !_gameEnded)
        {
            int insideCount = _playersInsideCabin.Count;
            float bannerW = 540f;
            float bannerH = 64f;
            Rect bannerRect = new Rect((Screen.width - bannerW) * 0.5f, 22f, bannerW, bannerH);

            GUI.Box(bannerRect, GUIContent.none);

            GUIStyle titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            titleStyle.normal.textColor = (insideCount == 4) ? new Color(0.2f, 1.0f, 0.4f) : new Color(0.3f, 0.9f, 1.0f);

            GUIStyle subStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 12,
                alignment = TextAnchor.MiddleCenter
            };
            subStyle.normal.textColor = new Color(0.9f, 0.9f, 0.9f, 0.95f);

            GUI.Label(new Rect(bannerRect.x, bannerRect.y + 6f, bannerW, 26f),
                $"ELEVATOR EVACUATION: {insideCount}/4 PLAYERS INSIDE", titleStyle);

            string hint = insideCount >= 4
                ? "All agents boarded! Sealing cabin doors..."
                : "(Press F1 - F4 to switch characters and guide all 4 agents into the elevator)";
            GUI.Label(new Rect(bannerRect.x, bannerRect.y + 32f, bannerW, 24f), hint, subStyle);
        }

        // 2. Fullscreen Victory Overlay
        if (_showVictoryScreen)
        {
            // Dark backdrop
            Texture2D blackTex = Texture2D.blackTexture;
            Color prevColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), blackTex);
            GUI.color = prevColor;

            // Center Modal Panel
            float panelW = 600f;
            float panelH = 380f;
            Rect panelRect = new Rect((Screen.width - panelW) * 0.5f, (Screen.height - panelH) * 0.5f, panelW, panelH);

            GUI.Box(panelRect, GUIContent.none);

            GUIStyle headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            headerStyle.normal.textColor = new Color(0.2f, 1.0f, 0.5f);

            GUIStyle subHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            subHeaderStyle.normal.textColor = new Color(0.4f, 0.9f, 1.0f);

            GUIStyle bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
            bodyStyle.normal.textColor = Color.white;

            GUIStyle rosterStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            rosterStyle.normal.textColor = new Color(0.3f, 1.0f, 0.5f);

            GUI.Label(new Rect(panelRect.x, panelRect.y + 24f, panelW, 36f), "FLIP // FALL", headerStyle);
            GUI.Label(new Rect(panelRect.x, panelRect.y + 60f, panelW, 26f), "MISSION ACCOMPLISHED — FACILITY EVACUATION COMPLETE", subHeaderStyle);
            GUI.Label(new Rect(panelRect.x, panelRect.y + 92f, panelW, 24f), "All 4 agents successfully entered the Sector 8 elevator and escaped.", bodyStyle);

            // Evacuated Roster Box
            float rosterY = panelRect.y + 130f;
            float rosterW = 400f;
            float rosterX = panelRect.x + (panelW - rosterW) * 0.5f;

            GUI.Label(new Rect(rosterX, rosterY + 0f, rosterW, 22f), "  • AGENT 1 (Player 1) : [ SAFELY EVACUATED ]", rosterStyle);
            GUI.Label(new Rect(rosterX, rosterY + 26f, rosterW, 22f), "  • AGENT 2 (Player 2) : [ SAFELY EVACUATED ]", rosterStyle);
            GUI.Label(new Rect(rosterX, rosterY + 52f, rosterW, 22f), "  • AGENT 3 (Player 3) : [ SAFELY EVACUATED ]", rosterStyle);
            GUI.Label(new Rect(rosterX, rosterY + 78f, rosterW, 22f), "  • AGENT 4 (Player 4) : [ SAFELY EVACUATED ]", rosterStyle);

            // Instructions
            GUIStyle hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
            hintStyle.normal.textColor = new Color(1.0f, 0.85f, 0.3f);
            GUI.Label(new Rect(panelRect.x, panelRect.y + 280f, panelW, 26f), "Press [R] to Restart Tutorial", hintStyle);

            if (GUI.Button(new Rect(panelRect.x + (panelW - 180f) * 0.5f, panelRect.y + 316f, 180f, 36f), "RESTART TUTORIAL"))
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!_doorsOpen || _gameEnded) return;

        PlayerCharacter pc = other.GetComponentInParent<PlayerCharacter>() ?? other.GetComponent<PlayerCharacter>();
        if (pc != null)
        {
            if (_playersInsideCabin.Add(pc))
            {
                Debug.Log($"[ElevatorPowerSocket] {pc.Identity} entered elevator cabin trigger ({_playersInsideCabin.Count}/4 players inside)");
            }

            if (_playersInsideCabin.Count >= 4 && !_gameEnded)
            {
                _gameEnded = true;
                StartCoroutine(EndGameSequenceRoutine());
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (!_doorsOpen || _gameEnded) return;

        PlayerCharacter pc = other.GetComponentInParent<PlayerCharacter>() ?? other.GetComponent<PlayerCharacter>();
        if (pc != null && !IsPlayerInsideCabin(pc))
        {
            if (_playersInsideCabin.Remove(pc))
            {
                Debug.Log($"[ElevatorPowerSocket] {pc.Identity} left elevator cabin trigger ({_playersInsideCabin.Count}/4 players inside)");
            }
        }
    }
}
