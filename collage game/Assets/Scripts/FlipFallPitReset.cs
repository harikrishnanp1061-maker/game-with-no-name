using UnityEngine;

// =========================================================================
// FLIP//FALL - Pit reset & fall detection
//
// A trigger volume under the corridor pits. When the player falls through a
// pit they are immediately returned to the reset point, so the reflex-dodge
// jump can be repeated without reloading. Also counts falls and notifies
// FlipFallReactionTimer when the player is put back.
//
// Attached by the Editor builder, not by hand.
// =========================================================================
public class FlipFallPitReset : MonoBehaviour
{
    [Header("Reset")]
    [Tooltip("Position the player is returned to after a fall.")]
    public Vector3 resetPosition = new Vector3(0f, 1.7f, 3f);
    [Tooltip("Forward yaw the player faces after a reset (degrees).")]
    public float resetYaw = 0f;

    [Header("Detection")]
    [Tooltip("Only react to objects tagged with this tag.")]
    public string playerTag = "Player";
    [Tooltip("Also treat a FirstPersonController as the player.")]
    public bool useFpsController = true;
    [Tooltip("Y below which a player counts as having fallen, regardless of the trigger volume.")]
    public float fallYThreshold = -2f;

    [Header("Reporting")]
    [SerializeField] private int fallCount = 0;
    public int FallCount => fallCount;

    public void ResetPlayer()
    {
        fallCount++;

        var timer = Object.FindAnyObjectByType<FlipFallReactionTimer>();
        if (timer != null) timer.OnPlayerReset();

        if (PlayerSuffocationPitEffect.Instance != null)
        {
            PlayerSuffocationPitEffect.Instance.TriggerPitSuffocation(resetPosition);
            return;
        }

        var t = FindPlayerTransform();
        if (t == null) return;

        t.position = resetPosition;
        t.rotation = Quaternion.Euler(0f, resetYaw, 0f);

        Debug.Log("[FlipFallPitReset] Player fell - reset to " + resetPosition +
            "  (fall #" + fallCount + ")");
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayer(other)) ResetPlayer();
    }

    private void FixedUpdate()
    {
        // Safety net: catches falls the trigger volume misses (respawn racing
        // the physics step, or stepping off an uncollided gap).
        var t = FindPlayerTransform();
        if (t != null && t.position.y < fallYThreshold) ResetPlayer();
    }

    private bool IsPlayer(Collider other)
    {
        if (other == null) return false;
        GameObject o = other.gameObject;
        if (o.CompareTag(playerTag)) return true;
        return useFpsController && o.GetComponentInParent<FirstPersonController>() != null;
    }

    private Transform FindPlayerTransform()
    {
        var fps = Object.FindAnyObjectByType<FirstPersonController>();
        if (fps != null) return fps.transform;

        GameObject[] byTag = UnityEngine.GameObject.FindGameObjectsWithTag(playerTag);
        return byTag.Length > 0 ? byTag[0].transform : null;
    }
}
