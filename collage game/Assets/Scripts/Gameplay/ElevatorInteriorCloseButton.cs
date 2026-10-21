using System;
using UnityEngine;

/// <summary>
/// Physical button located inside the elevator cab on the interior console.
/// Allows the player to toggle (close or open) the elevator doors from inside.
/// </summary>
[AddComponentMenu("FLIP//FALL/Gameplay/Elevator Interior Close Button")]
[DisallowMultipleComponent]
public class ElevatorInteriorCloseButton : MonoBehaviour, IInteractable
{
    [Header("Elevator Reference")]
    [SerializeField] private ElevatorPowerSocket _elevator;

    [Header("Prompt Settings")]
    [SerializeField] private string _promptCloseText = "CLOSE ELEVATOR DOORS";
    [SerializeField] private string _promptOpenText = "OPEN ELEVATOR DOORS";
    [ColorUsage(true, true)]
    [SerializeField] private Color _promptColor = new Color(0.2f, 1.0f, 0.4f, 1f);

    [Header("Interaction Distance")]
    [SerializeField] private float _interactionDistance = 2.5f;

    [Header("Audio")]
    [SerializeField] private AudioClip _buttonClickSound;

    [Header("Visual Indicator")]
    [SerializeField] private Renderer _buttonCapRenderer;

    public string PromptText => (_elevator != null && _elevator.AreDoorsOpen) ? _promptCloseText : _promptOpenText;
    public Color PromptColor => _promptColor;
    public float InteractionDistance => _interactionDistance;
    public string State => (_elevator != null && _elevator.AreDoorsOpen) ? "Open" : "Closed";

    private void Awake()
    {
        if (_elevator == null)
        {
            _elevator = GetComponentInParent<ElevatorPowerSocket>();
            if (_elevator == null)
            {
                _elevator = FindFirstObjectByType<ElevatorPowerSocket>();
            }
        }
        if (_buttonCapRenderer == null)
        {
            _buttonCapRenderer = GetComponentInChildren<Renderer>();
        }
    }

    public void SetElevator(ElevatorPowerSocket elevator)
    {
        _elevator = elevator;
    }

    public void SetButtonCapRenderer(Renderer r)
    {
        _buttonCapRenderer = r;
    }

    public bool CanInteract(PlayerSession player)
    {
        if (_elevator == null) return false;
        // The elevator must have power to operate doors
        if (!_elevator.IsPowered) return false;

        if (player != null && player.IsActive)
        {
            float dist = Vector3.Distance(player.HeadPosition, transform.position);
            if (dist > _interactionDistance) return false;
        }

        return true;
    }

    public bool OnInteract(PlayerSession player)
    {
        if (!CanInteract(player)) return false;

        if (_buttonClickSound != null)
        {
            AudioSource.PlayClipAtPoint(_buttonClickSound, transform.position, 1.0f);
        }

        // Animate button push slightly
        transform.localPosition += new Vector3(0f, 0f, 0.0003f);

        if (_elevator != null)
        {
            if (_elevator.AreDoorsOpen)
            {
                Debug.Log("[ElevatorInteriorCloseButton] Button pressed! Closing elevator doors from inside.");
                _elevator.CloseDoors();
                if (_buttonCapRenderer != null && _buttonCapRenderer.material != null)
                {
                    _buttonCapRenderer.material.SetColor("_EmissionColor", new Color(1.0f, 0.4f, 0.1f) * 4.0f);
                }
            }
            else
            {
                Debug.Log("[ElevatorInteriorCloseButton] Button pressed! Opening elevator doors from inside.");
                _elevator.OpenDoors();
                if (_buttonCapRenderer != null && _buttonCapRenderer.material != null)
                {
                    _buttonCapRenderer.material.SetColor("_EmissionColor", new Color(0.2f, 1.0f, 0.4f) * 4.0f);
                }
            }
        }

        if (InteractionManager.Instance != null)
        {
            InteractionManager.Instance.RefreshInteractables();
        }

        return true;
    }
}
