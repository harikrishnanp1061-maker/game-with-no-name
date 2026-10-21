using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// =========================================================================
// NarrationManager - Singleton Voice & Storyteller Narration System
//
// Features:
// 1. Dedicated 2D Voice AudioSource (non-spatialized so audio plays clearly
//    regardless of which player identity F1-F4 or camera is currently active).
// 2. Strict Play-Once Rule: Each narration has a unique ID stored in a
//    HashSet. Triggering an already-played ID is ignored and does NOT
//    interrupt or restart any ongoing narration.
// 3. Overlap & Interrupt Rule: If a NEW, unplayed narration is triggered while
//    an older clip is still playing, the older clip is immediately stopped
//    and the new one starts cleanly.
// 4. Pre-configured clip table with automatic asset discovery fallback.
// =========================================================================
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
[AddComponentMenu("FLIP//FALL/Gameplay/Narration Manager")]
public class NarrationManager : MonoBehaviour
{
    private static NarrationManager _instance;
    public static NarrationManager Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindAnyObjectByType<NarrationManager>();
                if (_instance == null)
                {
                    GameObject go = new GameObject("NarrationManager");
                    _instance = go.AddComponent<NarrationManager>();
                }
            }
            return _instance;
        }
    }

    [System.Serializable]
    public class NarrationEntry
    {
        [Tooltip("Unique ID for this voice line (e.g. TUTORIAL_WAKEUP, FIRST_PIT_FALL).")]
        public string id;

        [Tooltip("Audio clip to play.")]
        public AudioClip clip;

        [Range(0f, 1f)]
        public float volume = 1.0f;

        [TextArea(2, 4)]
        public string subtitleText;
    }

    // -----------------------------------------------------------------
    // CONFIGURATION
    // -----------------------------------------------------------------
    [Header("Audio Settings")]
    [Range(0f, 1f)]
    [SerializeField] private float _masterVoiceVolume = 1.0f;

    [Header("Registered Narration Clips")]
    [SerializeField] private List<NarrationEntry> _narrationEntries = new List<NarrationEntry>();

    [Header("Subtitles (Optional HUD)")]
    [SerializeField] private bool _showSubtitles = true;
    [SerializeField] private float _subtitlePaddingBottom = 65f;

    // -----------------------------------------------------------------
    // RUNTIME STATE
    // -----------------------------------------------------------------
    private AudioSource _voiceAudioSource;
    private readonly HashSet<string> _playedNarrationIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, NarrationEntry> _entriesById = new Dictionary<string, NarrationEntry>(StringComparer.OrdinalIgnoreCase);

    private string _currentNarrationId = "";
    private string _currentSubtitle = "";
    private Coroutine _narrationRoutine;
    private GUIStyle _subtitleStyle;

    // Events
    public static event Action<string, AudioClip> OnNarrationStarted;
    public static event Action<string> OnNarrationFinished;

    public bool IsPlaying => _voiceAudioSource != null && _voiceAudioSource.isPlaying;
    public string CurrentNarrationId => _currentNarrationId;
    public IReadOnlyCollection<string> PlayedNarrationIds => _playedNarrationIds;

    // -----------------------------------------------------------------
    // LIFECYCLE
    // -----------------------------------------------------------------
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        EnsureAudioSource();
        RebuildLookup();
    }

    private void OnEnable()
    {
        EnsureAudioSource();
        RebuildLookup();
    }

    private void Reset()
    {
        EnsureAudioSource();
        RebuildLookup();
    }

    private void OnValidate()
    {
        EnsureAudioSource();
    }

    private void EnsureAudioSource()
    {
        if (_voiceAudioSource == null)
        {
            _voiceAudioSource = GetComponent<AudioSource>();
            if (_voiceAudioSource == null)
            {
                _voiceAudioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        // Strict 2D non-spatialized voice settings
        _voiceAudioSource.spatialBlend = 0.0f;
        _voiceAudioSource.playOnAwake = false;
        _voiceAudioSource.loop = false;
        _voiceAudioSource.priority = 0; // Highest priority
        _voiceAudioSource.bypassEffects = true;
        _voiceAudioSource.bypassListenerEffects = true;
        _voiceAudioSource.bypassReverbZones = true;
        _voiceAudioSource.ignoreListenerPause = true;
        _voiceAudioSource.volume = _masterVoiceVolume;
    }

    private void RebuildLookup()
    {
        _entriesById.Clear();

        // 1. Register inspector entries
        for (int i = 0; i < _narrationEntries.Count; i++)
        {
            var e = _narrationEntries[i];
            if (e != null && !string.IsNullOrEmpty(e.id))
            {
                _entriesById[e.id] = e;
            }
        }

        // 2. Auto-populate standard 4 situations if missing
        EnsureDefaultClip("TUTORIAL_WAKEUP",
            "Assets/Audio/Narration/ElevenLabs_2026-10-21T04_55_00_Peter Harwood - British Audiobook Storyteller_pvc_sp100_s50_sb75_v4 (1).mp3",
            "Consciousness returns... in the dark wreckage of the crashed facility.");

        EnsureDefaultClip("FIRST_PIT_FALL",
            "Assets/Audio/Narration/ElevenLabs_2026-10-21T04_30_46_Peter Harwood - British Audiobook Storyteller_pvc_sp100_s50_sb75_v4 (1).mp3",
            "Down into the hidden pit... but death is not the end here.");

        EnsureDefaultClip("CROSS_FIRST_HIDDEN_DOOR",
            "Assets/Audio/Narration/ElevenLabs_2026-10-21T04_45_06_Peter Harwood - British Audiobook Storyteller_pvc_sp100_s50_sb75_v4.mp3",
            "Beyond the secret wall, the passage turns deeper into the complex.");

        EnsureDefaultClip("PAST_MONSTER_ROOM",
            "Assets/Audio/Narration/ElevenLabs_2026-10-21T04_47_37_Peter Harwood - British Audiobook Storyteller_pvc_sp100_s50_sb75_v4 (1).mp3",
            "Past the creature's chamber... the exit elevator awaits beyond.");
    }

    private void EnsureDefaultClip(string id, string assetPath, string defaultSubtitle)
    {
        if (_entriesById.ContainsKey(id) && _entriesById[id].clip != null) return;

        AudioClip clip = null;
#if UNITY_EDITOR
        clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
#endif
        if (clip == null)
        {
            // Fallback load by filename from Resources if available
            string filename = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            clip = Resources.Load<AudioClip>("Narration/" + filename);
        }

        if (clip != null)
        {
            NarrationEntry entry = new NarrationEntry
            {
                id = id,
                clip = clip,
                volume = 1.0f,
                subtitleText = defaultSubtitle
            };
            _entriesById[id] = entry;

            // Also keep inspector list synced if running in editor
            if (!_narrationEntries.Exists(e => string.Equals(e.id, id, StringComparison.OrdinalIgnoreCase)))
            {
                _narrationEntries.Add(entry);
            }
        }
    }

    // -----------------------------------------------------------------
    // PUBLIC PLAYBACK API
    // -----------------------------------------------------------------
    /// <summary>
    /// Checks if a narration has already been played.
    /// </summary>
    public bool HasPlayed(string narrationId)
    {
        if (string.IsNullOrEmpty(narrationId)) return false;
        return _playedNarrationIds.Contains(narrationId);
    }

    /// <summary>
    /// Triggers a registered narration by ID.
    /// Follows strict Play-Once and Overlap-Interrupt rules.
    /// Returns true if the clip was started, or false if already played or not found.
    /// </summary>
    public bool TriggerNarration(string narrationId)
    {
        if (string.IsNullOrEmpty(narrationId)) return false;

        // Rule 2: Strict Play-Once Rule
        // If already played, MUST NOT replay and MUST NOT cut off current audio!
        if (_playedNarrationIds.Contains(narrationId))
        {
            return false;
        }

        if (!_entriesById.TryGetValue(narrationId, out NarrationEntry entry) || entry.clip == null)
        {
            // Re-attempt asset discovery in case of hot reload
            RebuildLookup();
            if (!_entriesById.TryGetValue(narrationId, out entry) || entry.clip == null)
            {
                Debug.LogWarning($"[NarrationManager] Narration ID '{narrationId}' not found or has no AudioClip assigned.");
                return false;
            }
        }

        return PlayInternal(narrationId, entry.clip, entry.volume, entry.subtitleText);
    }

    /// <summary>
    /// Triggers narration with an explicit clip override.
    /// Follows strict Play-Once and Overlap-Interrupt rules.
    /// </summary>
    public bool TriggerNarration(string narrationId, AudioClip clip, float volume = 1.0f, string subtitle = "")
    {
        if (string.IsNullOrEmpty(narrationId) || clip == null) return false;

        // Rule 2: Strict Play-Once Rule
        if (_playedNarrationIds.Contains(narrationId))
        {
            return false;
        }

        return PlayInternal(narrationId, clip, volume, subtitle);
    }

    private bool PlayInternal(string id, AudioClip clip, float volume, string subtitle)
    {
        EnsureAudioSource();

        // Mark as played immediately
        _playedNarrationIds.Add(id);

        // Rule 3: Overlap & Interrupt Rule
        // If a new, unplayed narration event triggers while an older one is still playing,
        // immediately stop the old narration and cleanly play the newest one.
        if (_voiceAudioSource.isPlaying)
        {
            _voiceAudioSource.Stop();
        }

        if (_narrationRoutine != null)
        {
            StopCoroutine(_narrationRoutine);
            _narrationRoutine = null;
        }

        _currentNarrationId = id;
        _currentSubtitle = string.IsNullOrEmpty(subtitle) && _entriesById.TryGetValue(id, out var entry) ? entry.subtitleText : subtitle;

        _voiceAudioSource.clip = clip;
        _voiceAudioSource.volume = volume * _masterVoiceVolume;
        _voiceAudioSource.Play();

        Debug.Log($"[NarrationManager] Playing narration '{id}' (Length: {clip.length:F1}s).");
        OnNarrationStarted?.Invoke(id, clip);

        _narrationRoutine = StartCoroutine(NarrationPlaybackRoutine(id, clip.length));
        return true;
    }

    private IEnumerator NarrationPlaybackRoutine(string id, float duration)
    {
        yield return new WaitForSeconds(duration);

        if (_currentNarrationId == id)
        {
            _currentNarrationId = "";
            _currentSubtitle = "";
            OnNarrationFinished?.Invoke(id);
        }
        _narrationRoutine = null;
    }

    /// <summary>
    /// Stops the currently playing narration immediately.
    /// </summary>
    public void StopNarration()
    {
        if (_voiceAudioSource != null && _voiceAudioSource.isPlaying)
        {
            _voiceAudioSource.Stop();
        }

        if (_narrationRoutine != null)
        {
            StopCoroutine(_narrationRoutine);
            _narrationRoutine = null;
        }

        if (!string.IsNullOrEmpty(_currentNarrationId))
        {
            string old = _currentNarrationId;
            _currentNarrationId = "";
            _currentSubtitle = "";
            OnNarrationFinished?.Invoke(old);
        }
    }

    /// <summary>
    /// Resets played history (useful for editor/testing restart).
    /// </summary>
    public void ResetPlayedHistory()
    {
        _playedNarrationIds.Clear();
        StopNarration();
    }

    // -----------------------------------------------------------------
    // SUBTITLES / HUD
    // -----------------------------------------------------------------
    private void OnGUI()
    {
        if (!_showSubtitles || !IsPlaying || string.IsNullOrEmpty(_currentSubtitle)) return;

        if (_subtitleStyle == null)
        {
            _subtitleStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                wordWrap = true
            };
            _subtitleStyle.normal.textColor = new Color(0.95f, 0.95f, 0.95f, 1.0f);
        }

        float width = Mathf.Min(Screen.width * 0.72f, 780f);
        float height = 48f;
        float x = (Screen.width - width) * 0.5f;
        float y = Screen.height - _subtitlePaddingBottom - height;

        Color prevColor = GUI.color;
        GUI.color = new Color(0.04f, 0.05f, 0.07f, 0.82f);
        GUI.Box(new Rect(x - 12, y - 4, width + 24, height + 8), GUIContent.none);

        GUI.color = prevColor;
        GUI.Label(new Rect(x, y, width, height), _currentSubtitle, _subtitleStyle);
    }
}
