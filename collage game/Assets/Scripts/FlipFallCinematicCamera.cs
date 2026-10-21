using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// =========================================================================
// FLIP//FALL - Cinematic review camera
//
// Auto-flies a Catmull-Rom spline through the tutorial areas so the level can
// be reviewed without manual navigation. Press 'C' to toggle between the
// cinematic camera and the live FPS camera.
//
// No Cinemachine dependency - the project does not have the package installed.
// =========================================================================
public class FlipFallCinematicCamera : MonoBehaviour
{
    [Header("Flight")]
    [Tooltip("World-space control points. The path is interpolated as a Catmull-Rom spline.")]
    public List<Vector3> controlPoints = new List<Vector3>();

    [Tooltip("Camera look targets, one per control point. Empty look list = look at the point ahead on the path.")]
    public List<Vector3> lookAtPoints = new List<Vector3>();

    [Tooltip("Seconds to travel the whole path.")]
    public float duration = 60f;

    [Tooltip("Loop the flythrough instead of stopping at the end.")]
    public bool loop = false;

    [Tooltip("Vertical offset added to every control point.")]
    public float heightOffset = 0f;

    [Header("Camera")]
    public float fieldOfView = 62f;
    public float nearClipPlane = 0.1f;
    public float farClipPlane = 500f;
    public float rollEulerZ = 0f;

    [Header("Look")]
    [Tooltip("Gaze smoothing. 0 = hard snap, larger = lazier drift.")]
    [Range(0.001f, 1f)] public float lookSlerp = 0.08f;

    [Header("Runtime")]
    public bool autoStart = false;
    public bool enabledByDefault = true;

    private Camera cam;
    private bool running = false;
    private float t = 0f;

    void Awake()
    {
        cam = GetComponent<Camera>();
        if (cam == null) cam = gameObject.AddComponent<Camera>();

        cam.fieldOfView = fieldOfView;
        cam.nearClipPlane = nearClipPlane;
        cam.farClipPlane = farClipPlane;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.02f, 0.025f, 0.03f);

        if (!cam.gameObject.CompareTag("MainCamera")) cam.gameObject.tag = "MainCamera";

        enabledByDefault = gameObject.activeSelf;
        running = enabledByDefault && autoStart;

        if (controlPoints.Count > 0)
        {
            transform.position = controlPoints[0];
            if (controlPoints.Count > 1)
                transform.rotation = Quaternion.LookRotation(controlPoints[1] - controlPoints[0]);
        }
    }

    void Start()
    {
        if (controlPoints.Count < 2)
        {
            Debug.LogWarning("[FlipFallCinematicCamera] '" + gameObject.name +
                "' has fewer than 2 control points - the path will not move.");
        }
        else if (running)
        {
            StartCoroutine(Flythrough());
        }
    }

    void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.cKey.wasPressedThisFrame) SetEnabled(!isActiveAndEnabled);
#else
        if (Input.GetKeyDown(KeyCode.C)) SetEnabled(!isActiveAndEnabled);
#endif
    }

    // Take or release focus from the FPS rig. Exactly one camera should be
    // tagged MainCamera and exactly one AudioListener should be active.
    public void SetEnabled(bool on)
    {
        gameObject.SetActive(on);
        running = on;

        // Hand the MainCamera tag to the FPS camera when the cinematics step back.
        var fps = Object.FindAnyObjectByType<FirstPersonController>();
        var fpsCam = fps != null ? fps.GetComponentInChildren<Camera>() : null;
        if (fpsCam != null && !on) fpsCam.gameObject.tag = "MainCamera";

        var listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        foreach (var l in listeners)
            l.enabled = on ? l.gameObject == gameObject : (fpsCam != null && l.gameObject != gameObject);

        if (!on)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    IEnumerator Flythrough()
    {
        int n = controlPoints.Count;
        if (n < 2) yield break;

        bool firstFrame = true;
        do
        {
            float frac = loop ? Mathf.Repeat(t / duration, 1f) : Mathf.Clamp01(t / duration);
            float seg = frac * (n - 1);
            int i = Mathf.Min((int)seg, n - 2);
            float l = seg - i, l2 = l * l, l3 = l2 * l;

            // Centripetal-free Catmull-Rom, clamped at both ends.
            Vector3 p0 = Get(i - 1), p1 = Get(i), p2 = Get(i + 1), p3 = Get(i + 2);
            Vector3 pos = 0.5f * ((2f * p1) + (-p0 + p2) * l +
                (2f * p0 - 5f * p1 + 4f * p2 - p3) * l2 +
                (-p0 + 3f * p1 - 3f * p2 + p3) * l3);
            pos.y += heightOffset;

            transform.position = pos;

            Quaternion target = Quaternion.LookRotation(
                (LookPoint(i, l) - pos).normalized,
                Quaternion.Euler(0, 0, rollEulerZ) * Vector3.up);
            transform.rotation = firstFrame ? target : Quaternion.Slerp(transform.rotation, target, lookSlerp);
            firstFrame = false;

            t += Time.deltaTime;
            if (!loop && t >= duration) { running = false; yield break; }
        } while (true);
    }

    private Vector3 Get(int index)
    {
        if (index < 0) index = 0;
        if (index >= controlPoints.Count) index = controlPoints.Count - 1;
        return controlPoints[index];
    }

    private Vector3 LookPoint(int seg, float local)
    {
        if (lookAtPoints.Count == 0)
        {
            // No explicit targets: gaze slightly ahead along the path.
            int ahead = Mathf.Clamp(seg + 1, 0, controlPoints.Count - 1);
            return controlPoints[ahead];
        }

        int a = Mathf.Clamp(seg, 0, lookAtPoints.Count - 1);
        int b = Mathf.Clamp(a + 1, 0, lookAtPoints.Count - 1);
        return Vector3.Lerp(lookAtPoints[a], lookAtPoints[b], local);
    }
}
