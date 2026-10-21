using System;
using System.Collections.Generic;
using UnityEngine;

// =========================================================================
// TutorialGameTracker - lightweight objective tracker for the FLIP//FALL
// tutorial map, holding the four solo-phase beats:
//
//   1. Find the hidden pit      (Area02)
//   2. Find the hidden door     (false wall near z ~ 90)
//   3. Trip the green fuse      (Area07, powers the exit elevator)
//   4. Reach the elevator       (Area09, the objective)
//
// It subscribes to the existing gameplay events, so nothing needs to be
// wired in the inspector: drop this on any object and it starts tracking.
// Per-player completion is optional (_trackPerPlayer); by default the first
// player to hit a beat completes it for everyone, which is right for a
// co-op room but forgiving to override.
//
// Explicitly no networking: the multiplayer layer is out of scope for this
// phase.
// =========================================================================
[AddComponentMenu("FLIP//FALL/Gameplay/Tutorial Game Tracker")]
[DisallowMultipleComponent]
public class TutorialGameTracker : MonoBehaviour
{
    // -----------------------------------------------------------------
    // BEATS
    // -----------------------------------------------------------------
    [Header("Beats")]
    [SerializeField] private string _pitBeatLabel = "Find the hidden pit";
    [SerializeField] private string _doorBeatLabel = "Find the hidden door";
    [SerializeField] private string _fuseBeatLabel = "Trip the green fuse";
    [SerializeField] private string _elevatorBeatLabel = "Reach the working elevator";

    [Header("Options")]
    [Tooltip("Complete beats for whoever hits them first, instead of tracking each player separately.")]
    [SerializeField] private bool _trackPerPlayer = false;
    [SerializeField] private bool _logEvents = true;

    // -----------------------------------------------------------------
    // EVENTS
    // -----------------------------------------------------------------
    /// <summary>Raised once per beat, with the beat's label.</summary>
    public event Action<string, PlayerSession> OnBeatComplete;

    /// <summary>Raised once when every beat is complete.</summary>
    public event Action<PlayerSession> OnAllComplete;

    /// <summary>Static version of <see cref="OnAllComplete"/>.</summary>
    public static event Action<PlayerSession> OnTutorialComplete;

    /// <summary>Static version of <see cref="OnBeatComplete"/>.</summary>
    public static event Action<string, PlayerSession> OnBeatCompleted;

    // -----------------------------------------------------------------
    // STATE
    // -----------------------------------------------------------------
    private readonly HashSet<string> _completed = new HashSet<string>();
    private readonly Dictionary<PlayerSession, HashSet<string>> _completedPerPlayer =
        new Dictionary<PlayerSession, HashSet<string>>();
    private bool _allFired;
    private PlayerSession _allCompletePlayer;

    // -----------------------------------------------------------------
    // STATIC STATE (for HUDs that cannot hold a reference)
    // -----------------------------------------------------------------
    private static TutorialGameTracker _instance;

    /// <summary>The active tracker, or null.</summary>
    public static TutorialGameTracker Instance
    {
        get { return _instance; }
    }

    // -----------------------------------------------------------------
    // PUBLIC ACCESSORS
    // -----------------------------------------------------------------
    public int CompletedCount
    {
        get { return _completed.Count; }
    }

    public int BeatCount
    {
        get { return 4; }
    }

    public bool AllComplete
    {
        get { return _allFired; }
    }

    public bool IsBeatComplete(string beatKey, PlayerSession player = null)
    {
        if (_completed.Contains(beatKey)) return true;
        if (player != null && _completedPerPlayer.TryGetValue(player, out var set)) return set.Contains(beatKey);
        foreach (var pSet in _completedPerPlayer.Values)
        {
            if (pSet.Contains(beatKey)) return true;
        }
        return false;
    }

    public bool IsPitComplete => IsBeatComplete("pit");
    public bool IsDoorComplete => IsBeatComplete("door");
    public bool IsFuseComplete => IsBeatComplete("fuse");
    public bool IsElevatorComplete => IsBeatComplete("elevator");

    /// <summary>
    /// Next uncompleted beat label, for a simple HUD line. Empty when all
    /// four are done.
    /// </summary>
    public string NextObjective
    {
        get
        {
            if (!IsPitComplete) return _pitBeatLabel;
            if (!IsDoorComplete) return _doorBeatLabel;
            if (!IsFuseComplete) return _fuseBeatLabel;
            if (!IsElevatorComplete) return _elevatorBeatLabel;
            return "";
        }
    }

    // -----------------------------------------------------------------
    // LIFECYCLE
    // -----------------------------------------------------------------
    private void Awake()
    {
        _instance = this;
    }

    private void OnEnable()
    {
        FuseSwitch.OnPowerRestored += HandleFusePowered;
        ElevatorObjective.OnObjectiveComplete += HandleElevatorComplete;
        HiddenPitTrap.OnPlayerFell += HandlePitFell;
        HiddenDoor.OnOpened += HandleDoorOpened;
    }

    private void OnDisable()
    {
        FuseSwitch.OnPowerRestored -= HandleFusePowered;
        ElevatorObjective.OnObjectiveComplete -= HandleElevatorComplete;
        HiddenPitTrap.OnPlayerFell -= HandlePitFell;
        HiddenDoor.OnOpened -= HandleDoorOpened;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    // -----------------------------------------------------------------
    // EVENT HANDLERS
    // -----------------------------------------------------------------
    private void HandleFusePowered(FuseSwitch fuse)
    {
        CompleteBeat("fuse", _fuseBeatLabel, GameplayUtil.FindPlayer());
    }

    private void HandleElevatorComplete(ElevatorObjective elevator)
    {
        CompleteBeat("elevator", _elevatorBeatLabel, GameplayUtil.FindPlayer());
    }

    private void HandlePitFell(HiddenPitTrap trap)
    {
        CompleteBeat("pit", _pitBeatLabel, GameplayUtil.FindPlayer());
    }

    private void HandleDoorOpened(HiddenDoor door)
    {
        CompleteBeat("door", _doorBeatLabel, GameplayUtil.FindPlayer());
    }

    // -----------------------------------------------------------------
    // BEAT MANAGEMENT
    // -----------------------------------------------------------------
    /// <summary>
    /// Marks a beat complete. Safe to call repeatedly - a second call for the
    /// same beat and the same player is ignored.
    /// </summary>
    public void CompleteBeat(string beatKey, string label, PlayerSession player)
    {
        if (string.IsNullOrEmpty(beatKey)) return;

        bool newlyDone;
        if (_trackPerPlayer)
        {
            HashSet<string> set;
            if (!_completedPerPlayer.TryGetValue(player, out set))
            {
                set = new HashSet<string>();
                _completedPerPlayer[player] = set;
            }
            newlyDone = set.Add(beatKey);
            if (!newlyDone) return;
        }
        else
        {
            if (!_completed.Add(beatKey)) return;
            newlyDone = true;
        }

        string display = string.IsNullOrEmpty(label) ? beatKey : label;
        string who = GameplayUtil.SafeName(player);

        OnBeatComplete?.Invoke(display, player);
        OnBeatCompleted?.Invoke(display, player);

        if (_logEvents)
        {
            Debug.Log("[TutorialGameTracker] Beat complete: " + display +
                " (" + CountCompleted(player) + "/" + BeatCount + ") by " + who);
        }

        if (AllBeatsComplete(player))
        {
            FireAllComplete(player);
        }
    }

    private int CountCompleted(PlayerSession player)
    {
        if (_trackPerPlayer)
        {
            HashSet<string> set;
            return _completedPerPlayer.TryGetValue(player, out set) ? set.Count : 0;
        }
        return _completed.Count;
    }

    private bool AllBeatsComplete(PlayerSession player)
    {
        if (_trackPerPlayer)
        {
            HashSet<string> set;
            if (!_completedPerPlayer.TryGetValue(player, out set)) return false;
            return set.Count >= BeatCount;
        }
        return _completed.Count >= BeatCount;
    }

    private void FireAllComplete(PlayerSession player)
    {
        if (_allFired) return;
        _allFired = true;
        _allCompletePlayer = player;

        OnAllComplete?.Invoke(player);
        OnTutorialComplete?.Invoke(player);

        if (_logEvents)
        {
            Debug.Log("[TutorialGameTracker] All tutorial beats complete - " +
                GameplayUtil.SafeName(player) + " finished the tutorial.");
        }
    }

    /// <summary>
    /// Re-arms the tracker. Call together with
    /// InteractionManager.ResetAllBeats() to replay the whole chain.
    /// </summary>
    public void ResetAll()
    {
        _completed.Clear();
        _completedPerPlayer.Clear();
        _allFired = false;
        _allCompletePlayer = null;

        if (_logEvents) Debug.Log("[TutorialGameTracker] Tracker reset.");
    }
}
