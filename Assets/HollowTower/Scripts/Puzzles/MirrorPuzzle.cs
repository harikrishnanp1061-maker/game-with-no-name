using UnityEngine;

namespace HollowTower.Puzzles
{
    /// <summary>
    /// The darkroom's light trap (floor 2): an old enlarger throws a hard white beam across the dark room. Mirrors stand
    /// on swivel stands; turn them (E, an eighth of a turn each) to bounce the beam round the benches onto the photocell
    /// on the steel print safe - when the light falls on it, the safe opens and the fuse cell is inside. The beam is
    /// drawn live, bounce by bounce, so you can see where it goes. One mirror is a decoy.
    /// </summary>
    public class MirrorPuzzle : WorldPuzzle
    {
        public Transform source;
        public Transform[] mirrors;
        [Tooltip("The turn (0-7, eighths) each mirror must have; -1 for a decoy.")]
        public int[] solution;
        public Collider sensor;
        public Renderer sensorLamp;
        public LineRenderer beam;

        int[] rot;
        float[] baseYaw;
        bool lit;
        MaterialPropertyBlock block;

        protected override void Init()
        {
            base.Init();
            baseYaw = new float[mirrors.Length];
            for (int i = 0; i < mirrors.Length; i++) baseYaw[i] = mirrors[i].localEulerAngles.y;
        }

        protected override void Setup(System.Random rnd)
        {
            rot = new int[mirrors.Length];
            for (int i = 0; i < rot.Length; i++)
            {
                int s = solution != null && i < solution.Length ? solution[i] : -1;
                do rot[i] = rnd.Next(8); while (s >= 0 && (rot[i] == s || rot[i] == (s + 4) % 8));   // never already lined up
            }
        }

        public override string PartPrompt(int part, int player) { return Solved ? null : "[E] Turn the mirror"; }

        protected override void OnPress(int part, int player)
        {
            if (rot == null || part < 0 || part >= rot.Length) return;
            rot[part] = (rot[part] + 1) % 8;
            SfxBank.At(Tone(220f, 0.15f), mirrors[part].position, 0.5f, 1f, 12f);
            bool all = true;
            for (int i = 0; i < rot.Length; i++)
                if (solution[i] >= 0 && rot[i] != solution[i] && rot[i] != (solution[i] + 4) % 8) all = false;   // a mirror has two faces
            if (all) Solve(player);
        }

        protected override void Update()
        {
            base.Update();
            if (rot != null && baseYaw != null)
                for (int i = 0; i < mirrors.Length; i++)
                {
                    var want = Quaternion.Euler(0f, baseYaw[i] + rot[i] * 45f, 0f);
                    mirrors[i].localRotation = Quaternion.RotateTowards(mirrors[i].localRotation, want, 240f * Time.deltaTime);
                }
            Trace();
        }

        /// <summary>The beam, bounce by bounce (visual: whether it is solved is decided by the mirrors' turns).</summary>
        void Trace()
        {
            if (beam == null || source == null) return;
            var pts = new System.Collections.Generic.List<Vector3> { source.position };
            Vector3 p = source.position, d = source.forward;
            bool hitSensor = false;
            for (int bounce = 0; bounce < 8; bounce++)
            {
                RaycastHit best = default(RaycastHit); bool any = false; float bd = 40f;
                foreach (var h in Physics.RaycastAll(p, d, 40f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                {
                    if (h.collider.GetComponentInParent<RemasteredPlayerController>() != null) continue;
                    if (h.collider.isTrigger && h.collider != sensor && !h.collider.name.StartsWith("Mirror_Surface")) continue;
                    if (h.distance < bd && h.distance > 0.02f) { bd = h.distance; best = h; any = true; }
                }
                if (!any) { pts.Add(p + d * 40f); break; }
                pts.Add(best.point);
                if (best.collider == sensor) { hitSensor = true; break; }
                if (!best.collider.name.StartsWith("Mirror_Surface")) break;
                d = Vector3.Reflect(d, best.collider.transform.forward); d.y = 0f; d.Normalize();
                p = best.point + d * 0.03f;
            }
            beam.positionCount = pts.Count;
            beam.SetPositions(pts.ToArray());
            if (hitSensor != lit || block == null)
            {
                lit = hitSensor;
                if (sensorLamp != null)
                {
                    if (block == null) block = new MaterialPropertyBlock();
                    sensorLamp.GetPropertyBlock(block);
                    block.SetColor(Gfx.EmissiveId, Gfx.ToNits(lit || Solved ? new Color(0.3f, 1f, 0.4f) * 3f : new Color(1f, 0.1f, 0.05f)));
                    sensorLamp.SetPropertyBlock(block);
                }
            }
        }

        public override string Describe()
        {
            if (rot == null) return title + " (not rolled)";
            var s = title + ": turns ";
            for (int i = 0; i < rot.Length; i++) s += rot[i] + "/" + (solution[i] < 0 ? "-" : solution[i].ToString()) + " ";
            return s;
        }
    }
}
