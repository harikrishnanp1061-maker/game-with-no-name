using System;
using UnityEngine;

/// <summary>
/// Interactive pickup component for the green SM_FuseCell in Area 7 (Dark Fuse Room).
/// Implements IInteractable so InteractionManager prompts the player with [E] TAKE FUSE CELL.
/// </summary>
[RequireComponent(typeof(Collider))]
[AddComponentMenu("FLIP//FALL/Gameplay/Fuse Cell Pickup")]
[DisallowMultipleComponent]
public class FuseCellPickup : MonoBehaviour, IInteractable
{
    // Static player inventory state
    public static bool PlayerHasFuseCell { get; set; } = false;

    /// <summary>Raised when any player picks up the fuse cell.</summary>
    public static event Action<FuseCellPickup, PlayerSession> OnPickedUp;

    [Header("Prompt")]
    [SerializeField] private string _promptText = "TAKE FUSE CELL";
    [SerializeField] private string _promptCollected = "FUSE CELL ACQUIRED";
    [ColorUsage(true, true)]
    [SerializeField] private Color _promptColor = new Color(0.2f, 1.0f, 0.4f, 1f);

    [Header("Range")]
    [SerializeField] private float _interactionDistance = 4.5f;

    [Header("Audio")]
    [SerializeField] private AudioClip _pickupSound;

    [Header("Visual Glow")]
    [SerializeField] private Light _attachedGlowLight;

    private bool _isCollected = false;

    public string PromptText => _isCollected ? _promptCollected : _promptText;
    public Color PromptColor => _promptColor;
    public float InteractionDistance => _interactionDistance;
    public string State => _isCollected ? "Collected" : "Available";

    private static FuseCellPickup _instance;
    private FuseCellIlluminationReceiver _illuminationReceiver;

    private void Awake()
    {
        _instance = this;
        PlayerHasFuseCell = false;
        _isCollected = false;
        _illuminationReceiver = GetComponent<FuseCellIlluminationReceiver>();
        if (_attachedGlowLight == null)
        {
            _attachedGlowLight = GetComponentInChildren<Light>();
        }
        // If no illumination receiver present, default to enabled; otherwise receiver manages it
        if (_attachedGlowLight != null && _illuminationReceiver == null)
        {
            _attachedGlowLight.enabled = true;
        }

#if UNITY_EDITOR
        if (_pickupSound == null)
        {
            _pickupSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Items/Item_Pickup_Equip.mp3");
            if (_pickupSound == null)
            {
                _pickupSound = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Items/freesound_community-item-equip-6904.mp3");
            }
        }
#endif

        Renderer[] rends = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rends.Length; i++)
        {
            rends[i].enabled = true;
        }

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = true;
    }

    public bool CanInteract(PlayerSession player)
    {
        if (_isCollected) return false;

        Vector3 eyePos = (player != null && player.IsActive) 
            ? player.HeadPosition 
            : (Camera.main != null ? Camera.main.transform.position : transform.position);

        float dist = Vector3.Distance(eyePos, transform.position);
        if (dist > _interactionDistance) return false;

        return true;
    }

    public bool OnInteract(PlayerSession player)
    {
        if (!CanInteract(player)) return false;

        _isCollected = true;
        PlayerHasFuseCell = true;

        Debug.Log("[FuseCellPickup] Player acquired the green fuse cell! Bring it to the Exit Elevator in Area 8.");

        // Play audio if available
        if (_pickupSound != null)
        {
            AudioSource.PlayClipAtPoint(_pickupSound, transform.position, 1.0f);
        }

        // Notify subscribers & tutorial tracker
        OnPickedUp?.Invoke(this, player);
        if (TutorialGameTracker.Instance != null)
        {
            TutorialGameTracker.Instance.CompleteBeat("fuse", "Acquired green fuse cell", player);
        }

        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.RefreshInteractables();
        }

        // Hide visual and glow
        if (_attachedGlowLight != null) _attachedGlowLight.enabled = false;
        
        Renderer[] rends = GetComponentsInChildren<Renderer>();
        for (int i = 0; i < rends.Length; i++)
        {
            rends[i].enabled = false;
        }

        Collider col = GetComponent<Collider>();
        if (col != null) col.enabled = false;

        return true;
    }

    public static void DropAtPosition(Vector3 dropPosition)
    {
        if (!PlayerHasFuseCell) return;

        PlayerHasFuseCell = false;

        FuseCellPickup instance = _instance != null 
            ? _instance 
            : UnityEngine.Object.FindAnyObjectByType<FuseCellPickup>(FindObjectsInactive.Include);

        if (instance != null)
        {
            instance._isCollected = false;

            // Find floor surface below death position so it rests cleanly on ground
            Vector3 groundPos = dropPosition;
            if (Physics.Raycast(dropPosition + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 6.0f, ~LayerMask.GetMask("Player", "Ignore Raycast")))
            {
                groundPos = hit.point + Vector3.up * 0.05f;
            }

            instance.transform.position = groundPos;

            // Re-enable all visuals
            Renderer[] rends = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < rends.Length; i++)
            {
                rends[i].enabled = true;
            }

            Collider col = instance.GetComponent<Collider>();
            if (col != null) col.enabled = true;

            if (instance._attachedGlowLight != null)
            {
                instance._attachedGlowLight.enabled = true;
            }

            // Immediately apply glow if outside dark room
            if (instance._illuminationReceiver == null)
            {
                instance._illuminationReceiver = instance.GetComponent<FuseCellIlluminationReceiver>();
            }

            if (instance._illuminationReceiver != null)
            {
                if (!instance._illuminationReceiver.IsInsideDarkFuseRoom())
                {
                    instance._illuminationReceiver.ApplyGlow(1.0f);
                }
                else
                {
                    instance._illuminationReceiver.ApplyGlow(0.0f);
                }
            }

            Debug.Log($"[FuseCellPickup] Dropped green fuse cell on death at {groundPos}!");
        }

        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.RefreshInteractables();
        }
    }

    public static void ResetState()
    {
        PlayerHasFuseCell = false;
    }
}
