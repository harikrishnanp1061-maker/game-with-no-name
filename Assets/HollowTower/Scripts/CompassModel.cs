using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// P4's brass compass, a real object rather than a HUD dial: one in P4's left hand (seen by everyone) and a view
    /// copy held up at the bottom left of the screen while the local player is steering P4. The needle swings, with a
    /// little lag and wobble, towards the floor objective's target (the fuse cell, then the socket, then the stairs),
    /// and the glass glows brighter the closer it gets.
    /// </summary>
    public class CompassModel : MonoBehaviour
    {
        public Transform needle;
        public Renderer glass;
        public bool viewModel;
        public int owner = 3;
        public Color glowColor = new Color(0.85f, 0.45f, 1f);

        float needleAngle;
        MaterialPropertyBlock block;
        bool shown = true;

        void LateUpdate()
        {
            if (viewModel) PlaceInView();
            var obj = FloorObjective.Current;
            if (needle == null || obj == null) return;
            var t = obj.CompassTarget;
            if (!t.HasValue) return;

            Vector3 up = transform.up;
            Vector3 d = Vector3.ProjectOnPlane(t.Value - needle.position, up);
            if (d.sqrMagnitude < 1e-4f) return;
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, up).normalized;
            float want = Vector3.SignedAngle(fwd, d.normalized, up);
            // a real needle overshoots and settles
            needleAngle = Mathf.LerpAngle(needleAngle, want, Time.deltaTime * 4f) + Mathf.Sin(Time.time * 7f) * 0.6f;
            needle.localRotation = Quaternion.Euler(0f, needleAngle, 0f);

            if (glass != null)
            {
                float near = Mathf.Clamp01(1f - Vector3.Distance(transform.position, t.Value) / 40f);
                if (block == null) block = new MaterialPropertyBlock();
                glass.GetPropertyBlock(block);
                block.SetColor("_EmissionColor", glowColor * (0.15f + near * near * 1.6f));
                glass.SetPropertyBlock(block);
            }
        }

        // the view copy rides on the camera only while P4 is the living player being steered
        void PlaceInView()
        {
            var party = PlayerParty.Instance;
            bool show = party != null && PlayerParty.BodyHas(party.CurrentIndex, owner) && !party.Spectating && party.Current != null && !party.Current.IsDead
                        && party.playerCamera != null && !RemasteredMapHUD.IsFullMapOpen;
            if (show && transform.parent != party.playerCamera.transform)
            {
                transform.SetParent(party.playerCamera.transform, false);
                transform.localPosition = new Vector3(-0.2f, -0.22f, 0.42f);
                transform.localRotation = Quaternion.Euler(-52f, 14f, 0f);   // tipped up towards the eye, face readable
            }
            if (show != shown)
            {
                shown = show;
                foreach (var r in GetComponentsInChildren<Renderer>(true)) r.enabled = show;
            }
        }
    }
}
