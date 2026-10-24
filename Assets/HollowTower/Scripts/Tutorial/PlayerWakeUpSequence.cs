using HollowTower;
using UnityEngine;

/// <summary>
/// The tutorial's opening (same name and fields as the collage game's): everyone comes round on the floor of the crash
/// room - the screen starts dark and dizzy, the view rises from the floor with the head tilted, the bodies get up,
/// and once you're on your feet the narrator starts (TUTORIAL_WAKEUP). Movement waits until you're up.
/// </summary>
public class PlayerWakeUpSequence : MonoBehaviour
{
    [SerializeField] float _wakeUpDuration = 2.8f;
    [SerializeField] float _initialBlackDelay = 0f;
    [SerializeField] float _lyingEyeHeight = 0.38f;
    [SerializeField] float _standingEyeHeight = 1.82f;
    [SerializeField] float _initialRoll = 14f;
    [SerializeField] Color _fadeColor = new Color(0.015f, 0.02f, 0.035f, 1f);
    [SerializeField] AudioClip _wakeUpSound;

    float t = -1f;
    bool finished;
    Texture2D white;
    RemasteredPlayerController woke;

    void Update()
    {
        var party = PlayerParty.Instance;
        if (finished || party == null || party.Current == null) return;
        var body = party.Current;
        if (t < 0f)
        {
            t = 0f;
            woke = body;
            foreach (var p in party.players)
                if (p != null && p.animator != null)
                {
                    int s = Animator.StringToHash("Anim_WakeUp_Stand");
                    if (p.animator.HasState(0, s)) p.animator.CrossFade(s, 0.1f);
                }
            if (_wakeUpSound != null) SfxBank.Flat(_wakeUpSound, 0.9f);
            body.SetFrozen(true);
        }
        t += Time.deltaTime;
        float k = Mathf.Clamp01((t - _initialBlackDelay) / Mathf.Max(0.1f, _wakeUpDuration));
        float e = k * k * (3f - 2f * k);
        if (body.cameraHolder != null)
        {
            body.cameraHolder.localPosition = new Vector3(0f, Mathf.Lerp(_lyingEyeHeight, _standingEyeHeight, e), body.firstPersonForward);
            body.cameraHolder.localRotation = Quaternion.Euler(Mathf.Lerp(-25f, 0f, e), 0f, Mathf.Lerp(_initialRoll, 0f, e) + Mathf.Sin(t * 2.3f) * 3f * (1f - e));
        }
        if (k >= 1f)
        {
            finished = true;
            if (!body.IsDead) body.SetFrozen(false);
            if (NarrationManager.Instance != null) NarrationManager.Instance.TriggerNarration("TUTORIAL_WAKEUP");
        }
    }

    void OnGUI()
    {
        if (finished || t < 0f) return;
        if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
        float k = Mathf.Clamp01((t - _initialBlackDelay) / Mathf.Max(0.1f, _wakeUpDuration));
        var c = _fadeColor; c.a = Mathf.Lerp(1f, 0f, k) * (0.85f + 0.15f * Mathf.Sin(t * 5f));
        GUI.color = c;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), white);
        GUI.color = Color.white;
    }
}
