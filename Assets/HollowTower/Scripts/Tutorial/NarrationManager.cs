using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The tutorial's narrator (same name and fields as the collage game's, so the scene keeps its clip table): voice
/// lines by id, each played once per session, a newer line cutting off an older one, with subtitles at the bottom.
/// Plays on every machine by itself - each sees the same players reach the same places.
/// </summary>
[DisallowMultipleComponent]
public class NarrationManager : MonoBehaviour
{
    [Serializable]
    public class NarrationEntry
    {
        public string id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [TextArea(2, 4)] public string subtitleText;
    }

    static NarrationManager instance;
    public static NarrationManager Instance { get { if (instance == null) instance = FindAnyObjectByType<NarrationManager>(); return instance; } }

    [Range(0f, 1f)] [SerializeField] float _masterVoiceVolume = 1f;
    [SerializeField] List<NarrationEntry> _narrationEntries = new List<NarrationEntry>();
    [SerializeField] bool _showSubtitles = true;
    [SerializeField] float _subtitlePaddingBottom = 65f;

    AudioSource voice;
    readonly HashSet<string> played = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    string subtitle = "";
    float subtitleUntil;
    GUIStyle style;

    void Awake()
    {
        instance = this;
        voice = GetComponent<AudioSource>();
        if (voice == null) voice = gameObject.AddComponent<AudioSource>();
        voice.playOnAwake = false; voice.spatialBlend = 0f; voice.loop = false;
    }

    public bool HasPlayed(string id) { return played.Contains(id); }

    /// <summary>Plays the line once; ignored if it has played before.</summary>
    public void TriggerNarration(string id)
    {
        if (string.IsNullOrEmpty(id) || played.Contains(id)) return;
        NarrationEntry e = null;
        foreach (var n in _narrationEntries) if (n != null && string.Equals(n.id, id, StringComparison.OrdinalIgnoreCase)) { e = n; break; }
        played.Add(id);
        if (e == null) return;
        voice.Stop();
        if (e.clip != null) { voice.clip = e.clip; voice.volume = e.volume * _masterVoiceVolume; voice.Play(); }
        subtitle = e.subtitleText ?? "";
        subtitleUntil = Time.time + (e.clip != null ? e.clip.length : 4f) + 0.5f;
    }

    void OnGUI()
    {
        if (!_showSubtitles || string.IsNullOrEmpty(subtitle) || Time.time > subtitleUntil) return;
        if (style == null) style = new GUIStyle(GUI.skin.label) { fontSize = 18, alignment = TextAnchor.MiddleCenter, wordWrap = true };
        var r = new Rect(Screen.width * 0.15f, Screen.height - _subtitlePaddingBottom - 60f, Screen.width * 0.7f, 60f);
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), subtitle, style);
        GUI.color = new Color(1f, 0.96f, 0.85f);
        GUI.Label(r, subtitle, style);
        GUI.color = Color.white;
    }
}
