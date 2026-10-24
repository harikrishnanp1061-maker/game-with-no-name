using System.Collections.Generic;
using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// Every player carries a spray can in their own colour. Holding the left mouse button sprays where the steered player
    /// is aiming (walls, floors, doors), so the team can leave marks for each other: P2 can paint round a hidden pit,
    /// P1 can paint an arrow on a false wall. Paint on a hidden pit's fake floor lands on the floor surface even though
    /// the cover has nothing to hit, so the mark sits exactly over the hole (P2 sees it hanging over the drop).
    /// The paint is unlit, like fluorescent marking paint, so the marks read even in the dark rooms.
    /// All the dots go into a few shared meshes (vertex colours carry the player colour), so thousands of marks cost a
    /// handful of draw calls; when the budget is used up the oldest paint is painted over. Paint on something that moves
    /// goes into a mesh that moves with it: a door's paint swings with the door, and paint on a false wall slides into
    /// the floor with its panel when it is opened.
    /// </summary>
    public class SprayPaint : MonoBehaviour, Net.INetShare
    {
        public Material paintMaterial;
        public KeyCode sprayKey = KeyCode.Mouse0;
        public float reach = 3.6f;
        [Tooltip("Dots per mesh chunk, and how many chunks are kept before the oldest is reused.")]
        public int dotsPerChunk = 1200;
        public int maxChunks = 16;
        [Tooltip("The can shown at the bottom right of the screen while spraying (child of the camera).")]
        public Transform viewCan;
        public Renderer viewCanBody;

        class Chunk
        {
            public GameObject go;
            public Transform anchor;   // null: world paint
            public Mesh mesh;
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector3> n = new List<Vector3>();
            public readonly List<Color32> c = new List<Color32>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> t = new List<int>();
            public bool dirty;
            public int Dots { get { return v.Count / 4; } }
        }

        readonly List<Chunk> chunks = new List<Chunk>();
        readonly Dictionary<Transform, Chunk> anchored = new Dictionary<Transform, Chunk>();
        int writeChunk;
        Transform lastAnchor;
        Material mat, mistMat;
        Vector3? lastPoint;
        Vector3 lastNormal;
        float spacingLeft;
        float lift;
        AudioSource hiss;
        ParticleSystem mist;
        float viewCanTimer;
        GUIStyle hintStyle;
        Texture2D white;
        MaterialPropertyBlock canBlock;

        void Start()
        {
            var tex = MakeDotTexture(64);
            mat = new Material(paintMaterial) { name = "SprayPaint (runtime)" };
            mat.SetTexture("_BaseMap", tex);
            mistMat = new Material(mat) { name = "SprayMist (runtime)" };

            hiss = gameObject.AddComponent<AudioSource>();
            hiss.clip = MakeHiss();
            hiss.loop = true;
            hiss.volume = 0.22f;
            hiss.spatialBlend = 0f;
            hiss.playOnAwake = false;

            BuildMist();
            if (viewCan != null) viewCan.gameObject.SetActive(false);
        }

        void Update()
        {
            var party = PlayerParty.Instance;
            bool spraying = party != null && party.Current != null && !party.Current.IsFrozen && !party.Spectating && !RemasteredMapHUD.IsFullMapOpen
                            && Cursor.lockState == CursorLockMode.Locked && InputWrap.Held(sprayKey);
            Color color = party != null ? PlayerParty.Colors[party.CurrentIndex] : Color.white;
            var cam = party != null && party.playerCamera != null ? party.playerCamera : Camera.main;

            if (spraying && cam != null)
            {
                Vector3 p, n;
                Transform anchor;
                if (Aim(new Ray(cam.transform.position, cam.transform.forward), out p, out n, out anchor))
                {
                    if (anchor != lastAnchor) lastPoint = null;
                    lastAnchor = anchor;
                    Stroke(p, n, Vector3.Distance(cam.transform.position, p), color, anchor);
                    EmitMist(cam, p, color);
                    // the other machines draw the same line (in the world: a false wall's own anchor isn't shared)
                    if (Time.time > nextShare) { nextShare = Time.time + 1f / 20f; Net.NetWorld.Share(this, party.CurrentIndex, p, n); }
                }
                else lastPoint = null;
                if (!hiss.isPlaying) hiss.Play();
                viewCanTimer = 0.5f;
            }
            else
            {
                lastPoint = null;
                if (hiss.isPlaying) hiss.Stop();
                viewCanTimer -= Time.deltaTime;
            }
            UpdateViewCan(cam, color, spraying);

            foreach (var ch in chunks)
                if (ch.dirty) Upload(ch);
            foreach (var ch in anchored.Values)
                if (ch.dirty) Upload(ch);
        }

        // ------------------------------------------------------------------ where the paint lands
        bool Aim(Ray ray, out Vector3 point, out Vector3 normal, out Transform anchor)
        {
            point = normal = Vector3.zero;
            anchor = null;
            float best = reach;
            bool found = false;
            Collider hitCol = null;
            foreach (var h in Physics.RaycastAll(ray, reach, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.distance >= best) continue;
                if (h.collider.GetComponentInParent<RemasteredPlayerController>() != null) continue;
                best = h.distance; point = h.point; normal = h.normal; found = true; hitCol = h.collider;
            }
            // a hidden pit's fake floor has no collider: paint the floor surface where the ray crosses it
            Vector3 fp;
            if (PitHazards.HiddenFloorHit(ray, best, out fp)) { point = fp; normal = Vector3.up; return true; }
            if (hitCol != null)
            {
                var wall = HiddenWallDoor.ForCollider(hitCol);
                if (wall != null && !wall.IsOpen) anchor = wall.PaintAnchor;
                else if (hitCol.GetComponentInParent<InteractiveDoor>() != null) anchor = hitCol.transform;   // the door leaf
            }
            return found;
        }

        float nextShare;
        readonly Dictionary<int, Vector3> remoteLast = new Dictionary<int, Vector3>();
        readonly Dictionary<int, float> remoteTime = new Dictionary<int, float>();

        // a teammate's spray, from their machine: dots along the line from their last point
        public void NetShare(int player, Vector3 p, Vector3 n)
        {
            Color color = PlayerParty.Colors[Mathf.Clamp(player, 0, 3)];
            const float radius = 0.08f, spacing = radius * 0.45f;
            Vector3 a;
            float t;
            if (remoteLast.TryGetValue(player, out a) && remoteTime.TryGetValue(player, out t) && Time.time - t < 0.25f && Vector3.Distance(a, p) < 0.8f)
            {
                float len = Vector3.Distance(a, p);
                for (float s = spacing; s <= len; s += spacing) AddDot(Vector3.Lerp(a, p, s / len), n, radius, color, null);
            }
            else AddDot(p, n, radius, color, null);
            remoteLast[player] = p;
            remoteTime[player] = Time.time;
        }

        void Stroke(Vector3 p, Vector3 n, float dist, Color color, Transform anchor)
        {
            float radius = Mathf.Clamp(0.035f + dist * 0.03f, 0.04f, 0.14f);
            float spacing = radius * 0.45f;
            if (lastPoint.HasValue && Vector3.Distance(lastPoint.Value, p) < 0.8f && Vector3.Angle(lastNormal, n) < 25f)
            {
                // fill the gap since last frame so fast swipes still draw a line
                Vector3 a = lastPoint.Value;
                float len = Vector3.Distance(a, p);
                float s = spacingLeft;
                while (s <= len)
                {
                    AddDot(Vector3.Lerp(a, p, len > 0f ? s / len : 1f), n, radius, color, anchor);
                    s += spacing;
                }
                spacingLeft = s - len;
            }
            else
            {
                AddDot(p, n, radius, color, anchor);
                spacingLeft = spacing;
            }
            lastPoint = p;
            lastNormal = n;
        }

        void AddDot(Vector3 p, Vector3 n, float radius, Color color, Transform anchor)
        {
            var ch = anchor != null ? AnchoredChunk(anchor) : WritableChunk();
            float r = radius * Random.Range(0.85f, 1.15f);
            Vector3 t1 = Vector3.Cross(n, Mathf.Abs(n.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
            Vector3 t2 = Vector3.Cross(n, t1);
            float a = Random.Range(0f, Mathf.PI * 2f);
            Vector3 u = (t1 * Mathf.Cos(a) + t2 * Mathf.Sin(a)) * r, w = Vector3.Cross(n, u);
            // a hair off the surface, creeping up a little per dot so overlapping dots don't flicker
            lift = (lift + 0.00004f) % 0.002f;
            Vector3 c = p + n * (0.005f + lift) + (t1 * Random.Range(-0.15f, 0.15f) + t2 * Random.Range(-0.15f, 0.15f)) * r;
            int i = ch.v.Count;
            var space = ch.anchor;
            ch.v.Add(Local(space, c - u - w)); ch.v.Add(Local(space, c + u - w)); ch.v.Add(Local(space, c + u + w)); ch.v.Add(Local(space, c - u + w));
            Vector3 ln = space != null ? space.InverseTransformDirection(n) : n;
            for (int k = 0; k < 4; k++) ch.n.Add(ln);
            var col = (Color32)new Color(color.r * 0.7f, color.g * 0.7f, color.b * 0.7f, Random.Range(0.55f, 0.9f));
            for (int k = 0; k < 4; k++) ch.c.Add(col);
            ch.uv.Add(new Vector2(0, 0)); ch.uv.Add(new Vector2(1, 0)); ch.uv.Add(new Vector2(1, 1)); ch.uv.Add(new Vector2(0, 1));
            ch.t.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            ch.dirty = true;
        }

        static Vector3 Local(Transform space, Vector3 world) { return space != null ? space.InverseTransformPoint(world) : world; }

        /// <summary>The paint mesh that rides on a moving thing (a door leaf, a false wall).</summary>
        Chunk AnchoredChunk(Transform anchor)
        {
            Chunk ch;
            if (!anchored.TryGetValue(anchor, out ch) || ch.go == null)
            {
                ch = NewChunk("SprayPaint_on_" + anchor.name);
                ch.anchor = anchor;
                ch.go.transform.SetParent(anchor, false);
                anchored[anchor] = ch;
            }
            if (ch.Dots >= dotsPerChunk) { ch.v.Clear(); ch.n.Clear(); ch.c.Clear(); ch.uv.Clear(); ch.t.Clear(); }
            return ch;
        }

        Chunk NewChunk(string name)
        {
            var ch = new Chunk();
            ch.go = new GameObject(name);
            ch.mesh = new Mesh { name = "SprayPaint", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            ch.mesh.MarkDynamic();
            ch.go.AddComponent<MeshFilter>().sharedMesh = ch.mesh;
            var mr = ch.go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            return ch;
        }

        Chunk WritableChunk()
        {
            if (chunks.Count == 0 || chunks[writeChunk].Dots >= dotsPerChunk)
            {
                if (chunks.Count < maxChunks)
                {
                    var ch = NewChunk("SprayPaint_" + chunks.Count);
                    chunks.Add(ch);
                    writeChunk = chunks.Count - 1;
                }
                else
                {
                    // out of budget: paint over the oldest marks
                    writeChunk = (writeChunk + 1) % chunks.Count;
                    var old = chunks[writeChunk];
                    old.v.Clear(); old.n.Clear(); old.c.Clear(); old.uv.Clear(); old.t.Clear();
                    old.dirty = true;
                }
            }
            return chunks[writeChunk];
        }

        static void Upload(Chunk ch)
        {
            ch.mesh.Clear();
            ch.mesh.SetVertices(ch.v);
            ch.mesh.SetNormals(ch.n);
            ch.mesh.SetColors(ch.c);
            ch.mesh.SetUVs(0, ch.uv);
            ch.mesh.SetTriangles(ch.t, 0);
            ch.mesh.RecalculateBounds();
            ch.dirty = false;
        }

        // ------------------------------------------------------------------ the can, the mist, the hiss
        void UpdateViewCan(Camera cam, Color color, bool spraying)
        {
            if (viewCan == null) return;
            bool show = viewCanTimer > 0f && cam != null;
            if (viewCan.gameObject.activeSelf != show) viewCan.gameObject.SetActive(show);
            if (!show) return;
            if (viewCan.parent != cam.transform)
            {
                viewCan.SetParent(cam.transform, false);
                viewCan.localPosition = new Vector3(0.2f, -0.24f, 0.4f);
                viewCan.localRotation = Quaternion.Euler(18f, -12f, 0f);
            }
            // a small kick while the nozzle is held down
            viewCan.localPosition = Vector3.Lerp(viewCan.localPosition, new Vector3(0.2f, spraying ? -0.2f : -0.25f, spraying ? 0.42f : 0.4f), Time.deltaTime * 12f);
            if (viewCanBody != null)
            {
                if (canBlock == null) canBlock = new MaterialPropertyBlock();
                viewCanBody.GetPropertyBlock(canBlock);
                canBlock.SetColor("_BaseColor", color);
                viewCanBody.SetPropertyBlock(canBlock);
            }
        }

        void BuildMist()
        {
            var go = new GameObject("SprayMist");
            go.transform.SetParent(transform, false);
            mist = go.AddComponent<ParticleSystem>();
            mist.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = mist.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.3f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.1f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 400;
            var emission = mist.emission;
            emission.rateOverTime = 0f;
            var shape = mist.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 6f;
            shape.radius = 0.005f;
            var col = mist.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mistMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void EmitMist(Camera cam, Vector3 target, Color color)
        {
            if (mist == null) return;
            Vector3 nozzle = viewCan != null && viewCan.gameObject.activeInHierarchy
                ? viewCan.TransformPoint(new Vector3(0f, 0.11f, 0f))
                : cam.transform.TransformPoint(new Vector3(0.2f, -0.15f, 0.5f));
            mist.transform.position = nozzle;
            mist.transform.rotation = Quaternion.LookRotation(target - nozzle);
            var ep = new ParticleSystem.EmitParams { startColor = new Color(color.r, color.g, color.b, 0.6f), applyShapeToPosition = true };
            mist.Emit(ep, 3);
        }

        static AudioClip MakeHiss()
        {
            int rate = 22050, n = rate;   // one second, looped
            var data = new float[n];
            var rnd = new System.Random(7);
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float white = (float)(rnd.NextDouble() * 2.0 - 1.0);
                lp += (white - lp) * 0.55f;           // take the harsh top off
                data[i] = (white - lp * 0.6f) * 0.5f;  // keep it airy
            }
            var clip = AudioClip.Create("SprayHiss", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>A soft round dot with a speckled edge, like the overspray from a can.</summary>
        static Texture2D MakeDotTexture(int size)
        {
            var t = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, name = "SprayDot" };
            var rnd = new System.Random(11);
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                    float core = Mathf.Clamp01((0.62f - d) / 0.25f);
                    float speck = d < 1f && rnd.NextDouble() < (1f - d) * 0.5f ? (float)rnd.NextDouble() * (1f - d) * 1.4f : 0f;
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(Mathf.Max(core, speck))));
                }
            t.Apply(true);
            return t;
        }

        void OnGUI()
        {
            var party = PlayerParty.Instance;
            if (party == null || party.Current == null || party.Spectating || RemasteredMapHUD.IsFullMapOpen) return;
            if (hintStyle == null) hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            if (white == null) { white = new Texture2D(1, 1); white.SetPixel(0, 0, Color.white); white.Apply(); }
            var c = PlayerParty.Colors[party.CurrentIndex];
            float x = Screen.width - 190f, y = Screen.height - 46f;
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(x, y, 176f, 32f), white);
            GUI.color = c;
            GUI.DrawTexture(new Rect(x + 8f, y + 6f, 10f, 20f), white);   // the can
            GUI.DrawTexture(new Rect(x + 10f, y + 2f, 6f, 4f), white);
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            GUI.Label(new Rect(x + 26f, y, 150f, 32f), "Spray can  [LMB hold]", hintStyle);
            GUI.color = Color.white;
        }
    }
}
