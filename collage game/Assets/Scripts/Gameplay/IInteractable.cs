using System;
using UnityEngine;

// =========================================================================
// IInteractable / InteractableBase - the shared contract for anything the
// player can press E on.
//
// IInteractable is tiny on purpose so anything in the scene can implement it
// without inheriting. InteractableBase is the practical default: it owns the
// prompt text, the interaction distance, the state machine plumbing and the
// state-changed event, and it forwards the interaction input.
//
// Interaction input itself is owned by InteractionManager, which also handles
// the prompt (InteractionPrompt). InteractableBase never reads input directly,
// so there is exactly one place in the game that touches the mouse/keyboard
// for gameplay - which matters because FirstPersonController already owns the
// rest of the input.
// =========================================================================

/// <summary>
/// Implemented by anything the player can interact with.
/// </summary>
public interface IInteractable
{
    /// <summary>Short prompt shown to the player, e.g. "TRIP FUSE".</summary>
    string PromptText { get; }

    /// <summary>
    /// Coloured variant for the prompt so different beat types read
    /// differently at a glance. Unused if InteractionPrompt is disabled.
    /// </summary>
    Color PromptColor { get; }

    /// <summary>
    /// How close the player has to be for this to be a candidate. The manager
    /// raycasts from the camera centre and then checks distance as a fallback,
    /// so this is the effective "prompt range".
    /// </summary>
    float InteractionDistance { get; }

    /// <summary>
    /// Whether this object currently accepts input. Returning false hides the
    /// prompt and swallows the key press - used by one-shot beats that are
    /// already resolved, and by the elevator while it is unpowered.
    /// </summary>
    bool CanInteract(PlayerSession player);

    /// <summary>
    /// Called by the manager when the player presses the interaction key while
    /// this object is the active candidate. Returning false lets the caller
    /// decide whether to consume the press.
    /// </summary>
    bool OnInteract(PlayerSession player);

    /// <summary>Read-only state summary for logs, UI and the tracker.</summary>
    string State { get; }
}

// =========================================================================

/// <summary>
/// Default MonoBehaviour implementation of <see cref="IInteractable"/>.
/// Attach to a greybox part of the scene (fuse, false wall, pit, elevator)
/// and set the prompt; everything else works out of the box.
/// </summary>
[RequireComponent(typeof(Collider))]
[AddComponentMenu("FLIP//FALL/Interactable Base")]
[DisallowMultipleComponent]
public class InteractableBase : MonoBehaviour, IInteractable
{
    [Header("Prompt")]
    [Tooltip("Text shown in brackets: [E] <prompt>.")]
    [SerializeField] private string _promptText = "INTERACT";
    [Tooltip("Optional second line, shown small under the prompt.")]
    [SerializeField] private string _subtitle = "";
    [ColorUsage(true, true)]
    [SerializeField] private Color _promptColor = new Color(0.35f, 0.95f, 1f, 1f);

    [Header("Range")]
    [SerializeField] private float _interactionDistance = 3f;
    [SerializeField] private int _interactionLayerMask = -1;

    [Header("Behaviour")]
    [Tooltip("After the first interaction the prompt disappears permanently.")]
    [SerializeField] private bool _opensOnce = false;

    /// <summary>Raised whenever <see cref="State"/> changes. The tutorial tracker listens.</summary>
    public event Action<InteractableBase, string> OnStateChanged;

    // -----------------------------------------------------------------
    // STATE
    // -----------------------------------------------------------------
    [SerializeField] protected string _state = "Idle";
    [SerializeField] protected bool _resolved = false;

    /// <summary>Current state string. Subclasses set this via <see cref="SetState"/>.</summary>
    public virtual string State
    {
        get { return _state; }
    }

    /// <summary>True once this beat has been completed. Read-only from outside.</summary>
    public bool IsResolved => _resolved;

    // -----------------------------------------------------------------
    // IInteractable
    // -----------------------------------------------------------------
    public virtual string PromptText => _promptText;

    public virtual string Subtitle => _subtitle;

    public virtual Color PromptColor => _promptColor;

    public virtual float InteractionDistance => _interactionDistance;

    public virtual int InteractionLayerMask => _interactionLayerMask;

    /// <summary>
    /// In range by distance from the player, and not already resolved.
    /// Subclasses should call base and add their own gate (powered, opened...).
    /// </summary>
    public virtual bool CanInteract(PlayerSession player)
    {
        if (_resolved && _opensOnce) return false;
        if (player == null || !player.IsActive) return false;

        return Vector3.Distance(player.HeadPosition, transform.position) <= _interactionDistance;
    }

    /// <summary>
    /// Base implementation marks the beat resolved when _opensOnce is set.
    /// Subclasses override and mark their own one-shot state.
    /// </summary>
    public virtual bool OnInteract(PlayerSession player)
    {
        if (!CanInteract(player)) return false;

        if (_opensOnce && !_resolved)
        {
            _resolved = true;
            SetState("Completed");
        }

        return true;
    }

    // -----------------------------------------------------------------
    // STATE HELPERS
    // -----------------------------------------------------------------
    /// <summary>
    /// Changes <see cref="State"/> and raises <see cref="OnStateChanged"/> only
    /// when the value actually changed, so subscribers cannot see a duplicate
    /// "Powered" event from repeated calls.
    /// </summary>
    protected void SetState(string newState)
    {
        if (string.IsNullOrEmpty(newState)) return;
        if (string.Equals(_state, newState, StringComparison.Ordinal)) return;

        _state = newState;
        OnStateChanged?.Invoke(this, newState);
    }

    /// <summary>Editor/runtime helper to re-arm a one-shot beat.</summary>
    public void ResetInteractable()
    {
        _resolved = false;
        SetState("Idle");
    }
}
