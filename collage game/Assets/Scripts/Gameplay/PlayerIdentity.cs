using System;
using UnityEngine;

// =========================================================================
// PlayerIdentity - Centralized identity definitions for FLIP//FALL
//
// In FLIP//FALL's 4-player cooperative mode, each player takes on one of four
// distinct roles / identities (Player 1 through Player 4).
//
// During single-laptop development, PlayerIdentityManager allows developers
// and playtesters to switch the logical identity of the currently controlled
// player at runtime (via the Dev Player Switching Panel or F1-F4 keys) without
// altering player physics, transforms, or reloading scenes.
//
// Future player-specific systems (such as distinct perception abilities,
// role-based puzzles, or UI cues) query PlayerIdentityManager.CurrentIdentity
// or subscribe to OnIdentityChanged.
// =========================================================================

/// <summary>
/// The 4 cooperative player identities in FLIP//FALL.
/// </summary>
public enum PlayerIdentity
{
    Player1 = 1,
    Player2 = 2,
    Player3 = 3,
    Player4 = 4
}

/// <summary>
/// Centralized manager and accessor for the active player identity.
/// </summary>
public static class PlayerIdentityManager
{
    // -----------------------------------------------------------------
    // EVENTS
    // -----------------------------------------------------------------
    /// <summary>
    /// Invoked whenever the active player identity changes.
    /// Parameters: (newIdentity, previousIdentity)
    /// </summary>
    public static event Action<PlayerIdentity, PlayerIdentity> OnIdentityChanged;

    /// <summary>
    /// Invoked whenever the active player identity changes.
    /// Parameter: (newIdentity)
    /// </summary>
    public static event Action<PlayerIdentity> OnCurrentIdentityChanged;

    // -----------------------------------------------------------------
    // STATE
    // -----------------------------------------------------------------
    private static PlayerIdentity _currentIdentity = PlayerIdentity.Player1;
    private static string _lastChangeSource = "Default";
    private static float _lastChangeTime = 0f;

    /// <summary>
    /// The currently active player identity (Player1..Player4).
    /// </summary>
    public static PlayerIdentity CurrentIdentity => _currentIdentity;

    /// <summary>
    /// Source of the most recent identity switch (e.g., "UI Button", "F2", "Initial").
    /// </summary>
    public static string LastChangeSource => _lastChangeSource;

    /// <summary>
    /// Time.time at which the most recent identity switch occurred.
    /// </summary>
    public static float LastChangeTime => _lastChangeTime;

    // -----------------------------------------------------------------
    // INITIALIZATION & LIFECYCLE
    // -----------------------------------------------------------------
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    public static void ResetToDefault()
    {
        _currentIdentity = PlayerIdentity.Player1;
        _lastChangeSource = "Default";
        _lastChangeTime = 0f;
        OnIdentityChanged = null;
        OnCurrentIdentityChanged = null;
    }

    // -----------------------------------------------------------------
    // PUBLIC API
    // -----------------------------------------------------------------
    /// <summary>
    /// Updates the currently active player identity.
    /// Notifies all registered listeners and logs the transition.
    /// </summary>
    /// <param name="newIdentity">Target identity (Player1..Player4)</param>
    /// <param name="source">Description of what triggered the change (for debugging and dev HUD)</param>
    /// <returns>True if identity changed, false if it was already active</returns>
    public static bool SetIdentity(PlayerIdentity newIdentity, string source = "Manual")
    {
        PlayerIdentity previous = _currentIdentity;
        _currentIdentity = newIdentity;
        _lastChangeSource = string.IsNullOrEmpty(source) ? "Manual" : source;
        _lastChangeTime = Time.time;

        Debug.Log($"[PlayerIdentity] Active identity changed: {previous} -> {newIdentity} (Triggered by: {_lastChangeSource})");

        try
        {
            OnIdentityChanged?.Invoke(newIdentity, previous);
            OnCurrentIdentityChanged?.Invoke(newIdentity);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PlayerIdentity] Error in OnIdentityChanged event subscriber: {ex}");
        }

        return true;
    }

    /// <summary>
    /// Returns true if the query identity matches the active identity.
    /// </summary>
    public static bool IsActive(PlayerIdentity identity)
    {
        return _currentIdentity == identity;
    }

    /// <summary>
    /// Returns human-readable display name (e.g. "Player 1").
    /// </summary>
    public static string GetDisplayName(PlayerIdentity id)
    {
        switch (id)
        {
            case PlayerIdentity.Player1: return "Player 1";
            case PlayerIdentity.Player2: return "Player 2";
            case PlayerIdentity.Player3: return "Player 3";
            case PlayerIdentity.Player4: return "Player 4";
            default: return id.ToString();
        }
    }

    /// <summary>
    /// Returns abbreviated name (e.g. "P1").
    /// </summary>
    public static string GetShortName(PlayerIdentity id)
    {
        switch (id)
        {
            case PlayerIdentity.Player1: return "P1";
            case PlayerIdentity.Player2: return "P2";
            case PlayerIdentity.Player3: return "P3";
            case PlayerIdentity.Player4: return "P4";
            default: return id.ToString();
        }
    }

    /// <summary>
    /// Distinct, harmonious color theme for each player identity in the UI.
    /// </summary>
    public static Color GetIdentityColor(PlayerIdentity id)
    {
        switch (id)
        {
            case PlayerIdentity.Player1: return new Color(0.25f, 0.85f, 1.0f, 1.0f); // Cyan / Blue
            case PlayerIdentity.Player2: return new Color(1.0f, 0.75f, 0.20f, 1.0f); // Amber / Gold
            case PlayerIdentity.Player3: return new Color(0.30f, 0.95f, 0.55f, 1.0f); // Mint / Emerald
            case PlayerIdentity.Player4: return new Color(0.85f, 0.45f, 1.0f, 1.0f); // Violet / Magenta
            default: return Color.white;
        }
    }
}
