using UnityEngine;
using UnityEngine.Video;

namespace HollowTower
{
    /// <summary>
    /// Full-screen video over everything, kept across scenes:
    ///   Intro    - first launch: the title loop (played forwards then backwards, baked into one file) with
    ///              "PRESS ENTER TO CONTINUE"; Enter fades it into the lobby.
    ///   Loading  - the same loop with LOADING while a level loads (solo or networked; clients learn of a load from
    ///              Netcode's scene events), held a moment after the scene is in so nothing pops.
    ///   Cutscene - the first-entry film before the tutorial, with sound; hold Enter to skip (the host's skip, in a
    ///              session, is what moves everyone on).
    /// Videos live in StreamingAssets/Video (H.264 1080p: plays on any Windows PC).
    /// </summary>
    public class CinemaScreen : MonoBehaviour
    {
        public enum Mode { None, Intro, Loading, Cutscene }

        static CinemaScreen instance;
        public static CinemaScreen I
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("CinemaScreen");
                    DontDestroyOnLoad(go);
                    instance = go.AddComponent<CinemaScreen>();
                }
                return instance;
            }
        }

        /// <summary>The intro has been passed this run (the lobby goes straight to its menu after that).</summary>
        public static bool IntroDone;
        public static bool Showing { get { return instance != null && instance.mode != Mode.None; } }
        public Mode Current { get { return mode; } }

        Mode mode = Mode.None;
        VideoPlayer vp;
        RenderTexture rt;
        float fade, modeAt, skipHeld, loadedAt = -1f;
        System.Action onDone;
        bool canSkip;
        string playing;
        bool hookedNet;

        public static string Url(string file) { return System.IO.Path.Combine(Application.streamingAssetsPath, "Video", file); }

        void Awake()
        {
            vp = gameObject.AddComponent<VideoPlayer>();
            vp.playOnAwake = false; vp.renderMode = VideoRenderMode.RenderTexture;
            rt = new RenderTexture(1920, 1080, 0); vp.targetTexture = rt;
            vp.audioOutputMode = VideoAudioOutputMode.Direct;
            vp.loopPointReached += Ended;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (s, m) => { if (mode == Mode.Loading) loadedAt = Time.unscaledTime; };
        }

        void Play(string file, bool loop, bool sound)
        {
            if (playing != file || !vp.isPlaying)
            {
                vp.url = Url(file); vp.isLooping = loop; playing = file;
                vp.SetDirectAudioMute(0, !sound);
                vp.Play();
            }
            else vp.isLooping = loop;
        }

        public void ShowIntro() { mode = Mode.Intro; modeAt = Time.unscaledTime; Play("intro_loop.mp4", true, false); }

        public void ShowLoading()
        {
            if (mode == Mode.Cutscene) return;
            mode = Mode.Loading; modeAt = Time.unscaledTime; loadedAt = -1f;
            Play("intro_loop.mp4", true, false);
        }

        /// <summary>The first-entry film; done() runs when it ends or is skipped.</summary>
        public void PlayCutscene(System.Action done, bool skippable)
        {
            mode = Mode.Cutscene; modeAt = Time.unscaledTime; onDone = done; canSkip = skippable; skipHeld = 0f;
            playing = null;
            Play("first_entry.mp4", false, true);
        }

        /// <summary>A session's cutscene ended elsewhere (the host moved on): switch to the loading loop.</summary>
        public void EndCutscene() { if (mode == Mode.Cutscene) { onDone = null; mode = Mode.None; ShowLoading(); } }

        void Ended(VideoPlayer v)
        {
            if (mode != Mode.Cutscene) return;
            var d = onDone; onDone = null;
            mode = Mode.None;   // out of the film first: ShowLoading keeps a playing film on screen
            ShowLoading();
            if (d != null) d();
        }

        void Update()
        {
            HookNet();
            fade = Mathf.MoveTowards(fade, mode == Mode.None ? 0f : 1f, Time.unscaledDeltaTime * 2.5f);
            if (mode == Mode.None && fade <= 0f && vp.isPlaying) { vp.Stop(); playing = null; }
            switch (mode)
            {
                case Mode.Intro:
                    if (Time.unscaledTime > modeAt + 0.6f && (InputWrap.Down(KeyCode.Return) || InputWrap.Down(KeyCode.KeypadEnter) || InputWrap.Down(KeyCode.Space)))
                    { IntroDone = true; mode = Mode.None; if (SfxBank.Instance != null) SfxBank.Flat(SfxBank.Instance.itemPickup, 0.6f, 0.8f); }
                    break;
                case Mode.Loading:
                    if (loadedAt > 0f && Time.unscaledTime > loadedAt + 1.2f && Time.unscaledTime > modeAt + 1.5f) mode = Mode.None;
                    break;
                case Mode.Cutscene:
                    if (canSkip && (InputWrap.Held(KeyCode.Return) || InputWrap.Held(KeyCode.KeypadEnter))) { skipHeld += Time.unscaledDeltaTime; if (skipHeld > 1.2f) Ended(vp); }
                    else skipHeld = 0f;
                    break;
            }
        }

        /// <summary>Networked level loads reach clients as scene events: show the loading loop for them too.</summary>
        void HookNet()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm == null || nm.SceneManager == null) { hookedNet = false; return; }
            if (hookedNet) return;
            hookedNet = true;
            nm.SceneManager.OnSceneEvent += e =>
            {
                if (e.SceneEventType == Unity.Netcode.SceneEventType.Load && e.ClientId == nm.LocalClientId) { EndCutscene(); ShowLoading(); }
                if (e.SceneEventType == Unity.Netcode.SceneEventType.LoadComplete && e.ClientId == nm.LocalClientId && mode == Mode.Loading) loadedAt = Time.unscaledTime;
            };
        }

        void OnGUI()
        {
            if (fade <= 0f) return;
            UiStyle.Use();
            GUI.depth = -1000;
            float W = Screen.width, H = Screen.height;
            var c = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, fade);
            GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, fade);
            if (vp.isPlaying && vp.frame > 0) GUI.DrawTexture(new Rect(0, 0, W, H), rt, ScaleMode.ScaleAndCrop, false);
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 3f);
            if (mode == Mode.Intro)
            {
                var r = new Rect(W * 0.5f - 260f, H - 120f, 520f, 54f);
                UiStyle.Slab(r, -0.8f);
                UiStyle.Label(r, "PRESS ENTER TO CONTINUE", 24, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.5f + 0.5f * pulse));
            }
            else if (mode == Mode.Loading)
            {
                var r = new Rect(W - 300f, H - 90f, 260f, 50f);
                UiStyle.Slab(r, 0.8f);
                UiStyle.Label(r, "LOADING" + new string('.', 1 + (int)(Time.unscaledTime * 2f) % 3), 22);
            }
            else if (mode == Mode.Cutscene && canSkip)
                UiStyle.Label(new Rect(W - 320f, H - 50f, 300f, 30f), skipHeld > 0f ? "SKIPPING..." : "HOLD ENTER TO SKIP", 15, TextAnchor.MiddleRight, new Color(1f, 1f, 1f, 0.6f));
            GUI.color = c;
        }
    }
}
