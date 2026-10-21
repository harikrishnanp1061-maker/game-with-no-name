using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

// =========================================================================
// FLIP//FALL - Reflex-dodge reaction timer
//
// Measures how long it takes the player to leave the safety zone in front of
// a hidden pit. That window is the core reflex test for the pit-jump tutorial
// mechanic. Two outcomes close the window: jumping clear, or falling in.
//
// Press R to reset the run.
// =========================================================================
public class FlipFallReactionTimer : MonoBehaviour
{
    [Header("Window")]
    [Tooltip("Horizontal position of the pit lip the player is expected to jump from.")]
    public Vector3 pitEdgePosition = new Vector3(0f, 0f, 27.5f);
    [Tooltip("Player must be within this horizontal distance of the lip to start a measurement.")]
    public float windowRadius = 3f;
    [Tooltip("A drop below this Y means the player fell instead of jumping clear.")]
    public float fallYThreshold = -2f;

    [Header("Reporting")]
    [SerializeField] private bool measuring = false;
    [SerializeField] private float startTime = 0f;
    [SerializeField] private float bestTime = 0f;
    [SerializeField] private int attempts = 0;

    public float BestTime => bestTime;
    public int Attempts => attempts;
    public bool Measuring => measuring;

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame)
#else
        if (Input.GetKeyDown(KeyCode.R))
#endif
        {
            measuring = false;
            attempts = 0;
            bestTime = 0f;
            Debug.Log("[FlipFallReactionTimer] Run reset.");
            return;
        }

        Transform player = FindPlayer();
        if (player == null) return;

        bool near = HorizontalDistance(player.position, pitEdgePosition) <= windowRadius;

        if (!measuring)
        {
            // Open the window the moment the player steps onto the lip zone.
            if (near)
            {
                measuring = true;
                startTime = Time.time;
            }
            return;
        }

        // Close it once they either leave the lip or drop away from it.
        if (!near || player.position.y < fallYThreshold)
        {
            float elapsed = Time.time - startTime;
            bool jumpedClear = player.position.y >= fallYThreshold;
            measuring = false;
            attempts++;
            if (bestTime <= 0f || elapsed < bestTime) bestTime = elapsed;

            Debug.Log("[FlipFallReactionTimer] window " + elapsed.ToString("F3") + "s  [" +
                (jumpedClear ? "CLEARED" : "FELT") + "]  best=" + bestTime.ToString("F3") +
                "s  attempt #" + attempts);
        }
    }

    public void OnPlayerReset()
    {
        measuring = false;
    }

    private float HorizontalDistance(Vector3 a, Vector3 b)
    {
        Vector3 d = a - b;
        d.y = 0f;
        return d.magnitude;
    }

    private Transform FindPlayer()
    {
        FirstPersonController fps = Object.FindAnyObjectByType<FirstPersonController>();
        return fps != null ? fps.transform : null;
    }
}
