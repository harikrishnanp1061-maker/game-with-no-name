using System;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering;

namespace FlipFall.Networking
{
    /// <summary>
    /// Bridges FLIP//FALL's PlayerCharacter and FirstPersonController with Unity Netcode.
    /// Manages local vs remote ownership, camera attachment, animation synchronization,
    /// and teammate nameplates.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerCharacter))]
    [RequireComponent(typeof(FirstPersonController))]
    [RequireComponent(typeof(OwnerNetworkTransform))]
    [AddComponentMenu("FLIP//FALL/Networking/Network Player Bridge")]
    public class NetworkPlayerBridge : NetworkBehaviour
    {
        [Header("Components")]
        [SerializeField] private PlayerCharacter _playerCharacter;
        [SerializeField] private FirstPersonController _firstPersonController;
        [SerializeField] private CharacterController _characterController;
        [SerializeField] private Animator _animator;

        [Header("Nameplate (Teammates)")]
        [SerializeField] private GameObject _nameplateRoot;
        [SerializeField] private TextMeshPro _nameplateText;
        [SerializeField] private Vector3 _nameplateOffset = new Vector3(0f, 2.15f, 0f);

        // Network synchronization variables
        private readonly NetworkVariable<float> _networkPitch = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<bool> _networkIsCrouching = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<FixedString64Bytes> _networkPlayerName = new NetworkVariable<FixedString64Bytes>(
            "", NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // Remote animation tracking
        private Vector3 _lastPosition;
        private float _remoteSpeedSmoothed = 0f;
        private Camera _cachedLocalCam;

        public PlayerCharacter PlayerCharacter => _playerCharacter;
        public FirstPersonController FirstPersonController => _firstPersonController;
        public PlayerIdentity Identity => _playerCharacter != null ? _playerCharacter.Identity : PlayerIdentity.Player1;

        private void Awake()
        {
            ResolveReferences();
            CreateNameplate();
        }

        private void ResolveReferences()
        {
            if (_playerCharacter == null) _playerCharacter = GetComponent<PlayerCharacter>();
            if (_firstPersonController == null) _firstPersonController = GetComponent<FirstPersonController>();
            if (_characterController == null) _characterController = GetComponent<CharacterController>();
            if (_animator == null) _animator = GetComponentInChildren<Animator>();
        }

        public override void OnNetworkSpawn()
        {
            ResolveReferences();

            // Hook up network variable changes
            _networkPlayerName.OnValueChanged += HandlePlayerNameChanged;
            _networkIsCrouching.OnValueChanged += HandleCrouchStateChanged;

            // Apply ownership state
            ApplyOwnershipState(IsOwner);

            // If we are the owner, set our network player name
            if (IsOwner)
            {
                string myName = NetworkManagerBootstrap.LocalPlayerName;
                if (GameLobbyManager.Instance != null)
                {
                    string lobbyName = GameLobbyManager.Instance.GetPlayerName(Identity);
                    if (!string.IsNullOrEmpty(lobbyName)) myName = lobbyName;
                }
                _networkPlayerName.Value = new FixedString64Bytes(myName);
            }
            else
            {
                UpdateNameplateText(_networkPlayerName.Value.ToString());
            }

            _lastPosition = transform.position;
        }

        public override void OnNetworkDespawn()
        {
            _networkPlayerName.OnValueChanged -= HandlePlayerNameChanged;
            _networkIsCrouching.OnValueChanged -= HandleCrouchStateChanged;
        }

        public override void OnGainedOwnership()
        {
            Debug.Log($"[NetworkPlayerBridge] {Identity} gained local ownership!");
            ApplyOwnershipState(true);
        }

        public override void OnLostOwnership()
        {
            Debug.Log($"[NetworkPlayerBridge] {Identity} lost local ownership.");
            ApplyOwnershipState(false);
        }

        // =====================================================================
        // OWNERSHIP & CONTROL CONFIGURATION
        // =====================================================================

        /// <summary>
        /// Configures this character body for either local player control or remote display.
        /// </summary>
        public void ApplyOwnershipState(bool isLocalOwner)
        {
            ResolveReferences();

            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                Camera[] cams = FindObjectsByType<Camera>(FindObjectsSortMode.None);
                if (cams.Length > 0) mainCam = cams[0];
            }

            AudioListener listener = null;
            if (mainCam != null)
            {
                listener = mainCam.GetComponent<AudioListener>();
                if (listener == null) listener = mainCam.gameObject.AddComponent<AudioListener>();
            }

            if (isLocalOwner)
            {
                Debug.Log($"[NetworkPlayerBridge] Activating LOCAL player controls for {Identity}");

                // 1. Set identity manager
                PlayerIdentityManager.SetIdentity(Identity, "Network Local Spawn");

                // 2. Attach main camera and audio listener to this character's CameraHolder
                if (_playerCharacter != null)
                {
                    _playerCharacter.SetControlled(true, mainCam, listener);
                }

                // 3. Unlock input and character controller
                if (_firstPersonController != null)
                {
                    _firstPersonController.IsControlled = true;
                    _firstPersonController.CanMove = true;
                    _firstPersonController.CanLook = true;
                    if (mainCam != null)
                    {
                        _firstPersonController.playerCamera = mainCam.transform;
                        _firstPersonController.ResetLookAngles(0f, transform.eulerAngles.y);
                    }
                }

                if (_characterController != null)
                {
                    _characterController.enabled = true;
                }

                // 4. Hide own nameplate (do not render above own eyes)
                if (_nameplateRoot != null)
                {
                    _nameplateRoot.SetActive(false);
                }

                // 5. Notify MultiPlayerCharacterManager of active player
                if (MultiPlayerCharacterManager.Instance != null)
                {
                    MultiPlayerCharacterManager.Instance.SwitchToPlayer(Identity, force: true);
                }
            }
            else
            {
                Debug.Log($"[NetworkPlayerBridge] Configuring REMOTE player for {Identity}");

                // 1. Relinquish local control
                if (_playerCharacter != null)
                {
                    _playerCharacter.SetControlled(false);
                }

                // 2. Disable local inputs and detach camera
                if (_firstPersonController != null)
                {
                    _firstPersonController.IsControlled = false;
                    _firstPersonController.CanMove = false;
                    _firstPersonController.CanLook = false;
                    if (_firstPersonController.playerCamera == (mainCam != null ? mainCam.transform : null))
                    {
                        _firstPersonController.playerCamera = null;
                    }
                }

                // 3. CharacterController can stay enabled or disabled (NetworkTransform drives position)
                if (_characterController != null)
                {
                    _characterController.enabled = true;
                }

                // 4. Show teammate nameplate
                if (_nameplateRoot != null)
                {
                    _nameplateRoot.SetActive(true);
                }

                // 5. Ensure 3D model is fully visible to teammates
                EnsureRemoteMeshVisible();
            }
        }

        private void EnsureRemoteMeshVisible()
        {
            Transform model = transform.Find("Model_Animated");
            if (model != null)
            {
                Renderer[] rends = model.GetComponentsInChildren<Renderer>(true);
                foreach (var r in rends)
                {
                    if (r.gameObject.name.Contains("Torch") || r.gameObject.name.Contains("Lens")) continue;
                    r.shadowCastingMode = ShadowCastingMode.On;
                }
            }
        }

        // =====================================================================
        // UPDATE: VARIABLE SYNC & REMOTE ANIMATION
        // =====================================================================

        private void Update()
        {
            if (IsSpawned && IsOwner)
            {
                // Owner: push pitch and crouching to network
                if (_firstPersonController != null)
                {
                    _networkPitch.Value = _firstPersonController.VerticalRotation;
                    _networkIsCrouching.Value = _firstPersonController.IsCrouching;
                }
            }
            else if (IsSpawned && !IsOwner)
            {
                // Remote: animate movement based on position delta
                AnimateRemotePlayer();

                // Orient nameplate toward local camera
                UpdateNameplateOrientation();
            }
        }

        private void AnimateRemotePlayer()
        {
            if (_animator == null) return;

            Vector3 currentPos = transform.position;
            Vector3 delta = currentPos - _lastPosition;
            _lastPosition = currentPos;

            // Horizontal speed
            float dist = new Vector2(delta.x, delta.z).magnitude;
            float rawSpeed = dist / Mathf.Max(Time.deltaTime, 0.001f);

            // Smooth the remote speed
            _remoteSpeedSmoothed = Mathf.Lerp(_remoteSpeedSmoothed, rawSpeed, Time.deltaTime * 10f);

            float animSpeed = 0f;
            if (_remoteSpeedSmoothed > 0.15f)
            {
                bool isSprinting = _remoteSpeedSmoothed > 5.2f;
                animSpeed = isSprinting ? 2.0f : 1.0f;
            }

            _animator.SetFloat("Speed", animSpeed);
            _animator.SetBool("IsCrouching", _networkIsCrouching.Value);
            _animator.SetBool("IsGrounded", true);
        }

        private void UpdateNameplateOrientation()
        {
            if (_nameplateRoot == null || !_nameplateRoot.activeInHierarchy) return;

            if (_cachedLocalCam == null || !_cachedLocalCam.gameObject.activeInHierarchy)
            {
                _cachedLocalCam = Camera.main;
            }

            if (_cachedLocalCam != null)
            {
                // Billboard: Face the camera directly
                _nameplateRoot.transform.LookAt(_nameplateRoot.transform.position + _cachedLocalCam.transform.rotation * Vector3.forward,
                                                _cachedLocalCam.transform.rotation * Vector3.up);
            }
        }

        // =====================================================================
        // NAMEPLATE CREATION & UPDATES
        // =====================================================================

        private void CreateNameplate()
        {
            if (_nameplateRoot != null) return;

            // Create container
            _nameplateRoot = new GameObject("Teammate_Nameplate");
            _nameplateRoot.transform.SetParent(transform, false);
            _nameplateRoot.transform.localPosition = _nameplateOffset;

            // Add TextMeshPro
            _nameplateText = _nameplateRoot.AddComponent<TextMeshPro>();
            _nameplateText.fontSize = 2.4f;
            _nameplateText.alignment = TextAlignmentOptions.Center;
            _nameplateText.fontStyle = FontStyles.Bold;
            _nameplateText.color = GetIdentityBadgeColor(Identity);
            _nameplateText.text = $"[{Identity}]";

            // Default hidden until spawned as remote
            _nameplateRoot.SetActive(false);
        }

        private void HandlePlayerNameChanged(FixedString64Bytes oldVal, FixedString64Bytes newVal)
        {
            UpdateNameplateText(newVal.ToString());
        }

        private void HandleCrouchStateChanged(bool oldVal, bool newVal)
        {
            if (!IsOwner && _animator != null)
            {
                _animator.SetBool("IsCrouching", newVal);
            }
        }

        private void UpdateNameplateText(string playerName)
        {
            if (_nameplateText == null) return;

            string displayName = string.IsNullOrEmpty(playerName) ? Identity.ToString() : playerName;
            _nameplateText.text = $"[{Identity}]\n<size=80%>{displayName}</size>";
            _nameplateText.color = GetIdentityBadgeColor(Identity);
        }

        private Color GetIdentityBadgeColor(PlayerIdentity id)
        {
            switch (id)
            {
                case PlayerIdentity.Player1: return new Color(0.35f, 0.75f, 1.0f); // Cyan/Blue
                case PlayerIdentity.Player2: return new Color(1.0f, 0.65f, 0.2f);  // Orange/Amber
                case PlayerIdentity.Player3: return new Color(0.4f, 0.95f, 0.45f); // Emerald Green
                case PlayerIdentity.Player4: return new Color(0.95f, 0.85f, 0.3f); // Warm Gold
                default: return Color.white;
            }
        }
    }
}
