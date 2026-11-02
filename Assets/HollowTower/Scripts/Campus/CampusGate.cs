using UnityEngine;

namespace HollowTower.Campus
{
    /// <summary>The campus main gate: two iron leaves that swing outwards, slowly and loudly, once the security booth
    /// opens them (CampusSwitch.opens). Its red beacons turn green. Unless it is welded shut: someone chained and welded
    /// the leaves from the outside, so the motors only grind and shudder them against the welds, and the way out has to
    /// be found elsewhere (the service gate in the far south-east corner, released from the control room).</summary>
    public class CampusGate : MonoBehaviour
    {
        public Transform leftLeaf, rightLeaf;
        public float openAngle = 100f;
        public float openTime = 6f;
        public CampusLamp[] beacons;
        public Color openColor = new Color(0.25f, 1f, 0.35f);

        public bool IsOpen { get; private set; }
        [Tooltip("Chained and welded from outside: the lever only makes the motors strain against it.")]
        public bool weldedShut = true;
        public bool Jammed { get; private set; }
        public string jamMessage = "The motors scream and the leaves shudder - but they don't move. The gate has been chained and welded shut from OUTSIDE.";
        float jamT = -1f;

        Quaternion leftClosed, rightClosed;
        float t;

        void Awake()
        {
            if (leftLeaf != null) leftClosed = leftLeaf.localRotation;
            if (rightLeaf != null) rightClosed = rightLeaf.localRotation;
        }

        public void OpenGate()
        {
            if (IsOpen || Jammed) return;
            if (weldedShut)
            {
                Jammed = true; jamT = 0f;
                if (SfxBank.Instance != null)
                {
                    SfxBank.At(SfxBank.Instance.wallGrind, transform.position + Vector3.up * 2f, 1f, 0.45f, 140f);
                    SfxBank.At(SfxBank.Instance.doorLocked, transform.position + Vector3.up * 2f, 1f, 0.5f, 80f);
                }
                if (PlayerParty.Instance != null) PlayerParty.Instance.Flash(jamMessage, 7f);
                var nm = NarrationManager.Instance;
                if (nm != null) nm.TriggerNarration("campus_gate_welded");
                return;
            }
            IsOpen = true;
            if (SfxBank.Instance != null)
            {
                SfxBank.At(SfxBank.Instance.wallGrind, transform.position + Vector3.up * 2f, 1f, 0.6f, 120f);
                SfxBank.At(SfxBank.Instance.doorOpen, transform.position + Vector3.up * 2f, 1f, 0.45f, 90f);
            }
            if (beacons != null) foreach (var b in beacons) if (b != null) b.SetColor(openColor);
        }

        void Update()
        {
            if (Jammed && jamT >= 0f && jamT < 4f)
            {
                // straining against the welds: a few degrees of shudder, then back
                jamT += Time.deltaTime;
                float jk = Mathf.Sin(jamT * 38f) * 2.5f * Mathf.Clamp01(jamT * 2f) * Mathf.Clamp01((4f - jamT) * 1.5f);
                if (leftLeaf != null) leftLeaf.localRotation = leftClosed * Quaternion.Euler(0f, -Mathf.Abs(jk), 0f);
                if (rightLeaf != null) rightLeaf.localRotation = rightClosed * Quaternion.Euler(0f, Mathf.Abs(jk), 0f);
                if (beacons != null) foreach (var b in beacons) if (b != null) b.SetColor(Mathf.Repeat(jamT, 0.5f) < 0.25f ? new Color(1f, 0.55f, 0.1f) : new Color(1f, 0.1f, 0.06f));
                return;
            }
            if (!IsOpen || t >= 1f) return;
            t = Mathf.MoveTowards(t, 1f, Time.deltaTime / openTime);
            float k = Mathf.SmoothStep(0f, 1f, t);
            if (leftLeaf != null) leftLeaf.localRotation = leftClosed * Quaternion.Euler(0f, -openAngle * k, 0f);
            if (rightLeaf != null) rightLeaf.localRotation = rightClosed * Quaternion.Euler(0f, openAngle * k, 0f);
        }
    }
}
