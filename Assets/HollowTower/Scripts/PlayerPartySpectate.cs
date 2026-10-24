using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// Death and spectating. A player who dies (falls into a pit) is not respawned: steering them puts the camera behind
    /// a living teammate's eyes. A spectator looks around with the mouse, changes teammate with the mouse wheel and drops
    /// pings (left click) that everyone sees, and keeps their ability: a dead P2 still sees the hidden pits, a dead P1 the
    /// false walls, from wherever they are watching. A dead P4 lends their torch to the teammate they watch. The light
    /// rides on that teammate but only P4 aims it (it points where P4's spectator view looks), and it stays where P4
    /// left it while the local player steers someone else.
    /// </summary>
    public partial class PlayerParty
    {
        [Header("Spectating")]
        public float spectatorSensitivity = 2.5f;
        [Tooltip("Transparent tinted material for the pings dead players drop.")]
        public Material pingMaterial;
        public float pingLifetime = 9f;

        readonly int[] watching = { -1, -1, -1, -1 };     // which teammate each dead player is watching
        readonly Vector2[] look = new Vector2[4];          // each spectator's look offset (yaw, pitch) from that teammate's view
        Light blessing;
        bool allDead;
        AudioSource pingAudio;

        class Ping { public GameObject go; public Material mat; public Transform ring; public float born; public int owner; public Color color; }
        readonly List<Ping> pings = new List<Ping>();

        /// <summary>The steered player is dead and watching someone.</summary>
        public bool Spectating { get { return current != null && current.IsDead; } }
        /// <summary>The living teammate the steered (dead) player is watching.</summary>
        public RemasteredPlayerController Watched { get { return Spectating && watching[currentIndex] >= 0 ? players[watching[currentIndex]] : null; } }
        /// <summary>The body the camera is in: the steered player, or the teammate a spectator is watching.</summary>
        RemasteredPlayerController ViewBody { get { return Spectating ? Watched : current; } }
        public bool AllDead { get { return allDead; } }

        /// <summary>Every torch shining in the party: P4's own, or the one a dead P4 lends a teammate.</summary>
        public IEnumerable<Light> Torches()
        {
            foreach (var p in players)
                if (p != null && !p.IsDead && p.hasTorch && p.flashlightLight != null && p.flashlightLight.enabled && p.flashlightLight.gameObject.activeInHierarchy)
                    yield return p.flashlightLight;
            if (blessing != null && blessing.enabled && blessing.gameObject.activeInHierarchy) yield return blessing;
        }

        // ------------------------------------------------------------------ dying
        /// <summary>Kills a player where they stand (a monster): what they carried drops at their feet.</summary>
        public void KillPlayer(RemasteredPlayerController body, string how)
        {
            if (body == null || body.IsDead) return;
            var inv = body.GetComponent<PlayerInventory>();
            if (inv != null && inv.Items.Count > 0) inv.DropAllAt(body.transform.position);
            var bank = SfxBank.Instance;
            if (bank != null) SfxBank.At(bank.deathGasp, body.transform.position + Vector3.up * 1.5f, 1f);
            if (body == current) SetFade(1f);
            body.Die();
            OnPlayerDied(body, how);
        }

        /// <summary>Called once a player is dead (pits, monsters).</summary>
        public void OnPlayerDied(RemasteredPlayerController body, string how = "fell to their death")
        {
            int i = System.Array.IndexOf(players, body);
            if (i < 0) return;
            Net.NetBody.Report(body, how);   // in a session: the same death on every machine
            Flash("Player " + (i + 1) + " " + how + (body == current ? " - you are spectating" : ""), 4f);

            // whoever was watching them finds someone else
            for (int k = 0; k < 4; k++)
                if (k != i && watching[k] == i) watching[k] = NearestLiving(players[k].transform.position);
            watching[i] = NearestLiving(body.transform.position);
            if (watching[i] < 0)
            {
                allDead = true;
                SetFade(0.75f);
                return;
            }
            if (Spectating) AttachSpectator();
            if (body == current) StartCoroutine(FadeIn(0.9f));
        }

        IEnumerator FadeIn(float time)
        {
            for (float t = 0f; t < 1f; t += Time.deltaTime / time)
            {
                SetFade(1f - t);
                yield return null;
            }
            SetFade(0f);
        }

        int NearestLiving(Vector3 from)
        {
            int best = -1;
            float bestD = float.MaxValue;
            for (int k = 0; k < players.Length; k++)
            {
                if (players[k] == null || players[k].IsDead) continue;
                float d = (players[k].transform.position - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = k; }
            }
            return best;
        }

        int NextLiving(int from, int step)
        {
            for (int n = 1; n <= players.Length; n++)
            {
                int k = ((from + step * n) % players.Length + players.Length) % players.Length;
                if (players[k] != null && !players[k].IsDead) return k;
            }
            return -1;
        }

        // ------------------------------------------------------------------ spectating
        /// <summary>Puts the camera in the watched teammate's head and gives them the "Player" tag (floor culling follows).</summary>
        void AttachSpectator()
        {
            var w = Watched;
            foreach (var p in players)
                if (p != null)
                {
                    p.ViewedFromInside = false;
                    if (!p.IsDead) p.gameObject.tag = "Untagged";
                }
            if (w == null) return;
            w.gameObject.tag = "Player";
            w.ViewedFromInside = true;
            playerCamera.transform.SetParent(w.cameraHolder, false);
            playerCamera.transform.localPosition = Vector3.zero;
            playerCamera.transform.localRotation = Quaternion.Euler(look[currentIndex].y, look[currentIndex].x, 0f);
            RefreshTrackers();
        }

        void RefreshTrackers()
        {
            if (RemasteredLightOptimizer.Instance != null) RemasteredLightOptimizer.Instance.FindPlayer();
            var body = ViewBody;
            if (TowerFloorCulling.Instance != null && body != null)
            {
                TowerFloorCulling.Instance.FindPlayerAndCamera();
                TowerFloorCulling.Instance.ForceFloorUpdate(FloorOf(body.transform.position.y));
            }
        }

        void UpdateSpectator()
        {
            int i = currentIndex;
            if (watching[i] < 0 || players[watching[i]] == null || players[watching[i]].IsDead)
            {
                watching[i] = NearestLiving(current.transform.position);
                AttachSpectator();
                if (watching[i] < 0) return;
            }

            // the mouse cursor: Escape frees it, a click takes it back
            if (InputWrap.Down(KeyCode.Escape)) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            bool clickedToLock = false;
            if (Cursor.lockState != CursorLockMode.Locked && InputWrap.Down(KeyCode.Mouse0) && !RemasteredMapHUD.IsFullMapOpen)
            {
                Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; clickedToLock = true;
            }
            if (Cursor.lockState != CursorLockMode.Locked || RemasteredMapHUD.IsFullMapOpen) return;

            var d = InputWrap.MouseDelta * spectatorSensitivity;
            look[i] = new Vector2(Mathf.Clamp(look[i].x + d.x, -160f, 160f), Mathf.Clamp(look[i].y - d.y, -80f, 80f));
            playerCamera.transform.localRotation = Quaternion.Euler(look[i].y, look[i].x, 0f);

            float wheel = InputWrap.Scroll;
            if (Mathf.Abs(wheel) > 0.1f)
            {
                int next = NextLiving(watching[i], wheel > 0f ? -1 : 1);
                if (next >= 0 && next != watching[i])
                {
                    watching[i] = next;
                    look[i] = Vector2.zero;
                    AttachSpectator();
                    bannerTimer = 2f;
                }
            }
            if (!clickedToLock && InputWrap.Down(KeyCode.Mouse0)) DropPing(i);
        }

        // ------------------------------------------------------------------ P4's torch, lent from beyond
        void UpdateBlessing()
        {
            var p4 = players.Length > 3 ? players[3] : null;
            if (p4 == null || !p4.IsDead)
            {
                if (blessing != null) blessing.enabled = false;
                return;
            }
            int t = watching[3];
            if (t < 0 || players[t] == null || players[t].IsDead)
            {
                t = watching[3] = NearestLiving(p4.transform.position);
                if (current == p4) AttachSpectator();
            }
            if (t < 0) { if (blessing != null) blessing.enabled = false; return; }
            var target = players[t];

            if (blessing == null)
            {
                var go = new GameObject("Blessing_Torch_P4");
                blessing = go.AddComponent<Light>();
                var src = p4.flashlightLight;
                blessing.type = LightType.Spot;
                blessing.range = src != null ? src.range : 30f;
                blessing.spotAngle = src != null ? src.spotAngle : 50f;
                blessing.innerSpotAngle = src != null ? src.innerSpotAngle : 30f;
                blessing.intensity = src != null ? src.intensity : 4f;
                // a faint violet cast: P4's colour, so everyone can tell whose light it is
                blessing.color = Color.Lerp(src != null ? src.color : Color.white, Colors[3], 0.18f);
            }
            if (blessing.transform.parent != target.cameraHolder)
            {
                blessing.transform.SetParent(target.cameraHolder, false);
                blessing.transform.localPosition = new Vector3(0.18f, -0.15f, 0.25f);
            }
            // only P4 aims it: it points where P4's spectator view looks, and stays there while someone else is steered
            blessing.transform.localRotation = Quaternion.Euler(look[3].y, look[3].x, 0f);
            blessing.shadows = ViewBody == target ? LightShadows.Soft : LightShadows.None;
            blessing.enabled = true;
        }

        // ------------------------------------------------------------------ pings
        void DropPing(int owner)
        {
            var cam = playerCamera;
            var ray = new Ray(cam.transform.position, cam.transform.forward);
            float best = 40f;
            Vector3 at = Vector3.zero;
            bool found = false;
            foreach (var h in Physics.RaycastAll(ray, best, ~0, QueryTriggerInteraction.Ignore))
            {
                if (h.distance >= best || h.collider.GetComponentInParent<RemasteredPlayerController>() != null) continue;
                best = h.distance; at = h.point; found = true;
            }
            Vector3 fp;
            if (PitHazards.HiddenFloorHit(ray, best, out fp)) { at = fp; found = true; }
            if (!found) return;

            // three pings each at most
            int mine = 0;
            for (int k = pings.Count - 1; k >= 0; k--) if (pings[k].owner == owner && ++mine >= 3) RemovePing(k);

            var c = Colors[owner];
            var root = new GameObject("Ping_P" + (owner + 1));
            root.transform.position = at;
            var mat = new Material(pingMaterial);
            mat.SetColor("_BaseColor", new Color(c.r, c.g, c.b, 0.85f));
            var beam = PingPart(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 1.3f, 0f), new Vector3(0.035f, 1.3f, 0.035f), mat);
            var ring = PingPart(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0.015f, 0f), new Vector3(0.7f, 0.004f, 0.7f), mat);
            PingPart(root.transform, PrimitiveType.Sphere, new Vector3(0f, 2.65f, 0f), new Vector3(0.14f, 0.14f, 0.14f), mat);
            pings.Add(new Ping { go = root, mat = mat, ring = ring.transform, born = Time.time, owner = owner, color = c });

            if (pingAudio == null)
            {
                pingAudio = gameObject.AddComponent<AudioSource>();
                pingAudio.spatialBlend = 0f;
                pingAudio.playOnAwake = false;
                pingAudio.volume = 0.35f;
                pingAudio.clip = MakeBeep();
            }
            pingAudio.pitch = 0.9f + owner * 0.08f;
            pingAudio.Play();
            Flash("Player " + (owner + 1) + " (spectating) marked a spot", 2f);
        }

        static GameObject PingPart(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        void RemovePing(int k)
        {
            Destroy(pings[k].go);
            Destroy(pings[k].mat);
            pings.RemoveAt(k);
        }

        void UpdatePings()
        {
            for (int k = pings.Count - 1; k >= 0; k--)
            {
                var p = pings[k];
                float age = Time.time - p.born;
                if (age > pingLifetime) { RemovePing(k); continue; }
                float pulse = 1f + 0.35f * Mathf.Repeat(age * 1.2f, 1f);
                p.ring.localScale = new Vector3(0.7f * pulse, 0.004f, 0.7f * pulse);
                float a = Mathf.Clamp01((pingLifetime - age) / 2f) * 0.85f;
                p.mat.SetColor("_BaseColor", new Color(p.color.r, p.color.g, p.color.b, a));
            }
        }

        static AudioClip MakeBeep()
        {
            int rate = 22050, n = rate / 5;
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate;
                data[i] = Mathf.Sin(t * 2f * Mathf.PI * 880f) * Mathf.Exp(-t * 14f) * 0.6f;
            }
            var clip = AudioClip.Create("PingBeep", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ------------------------------------------------------------------ HUD for the dead
        void DrawSpectatorHud()
        {
            var w = Watched;
            float y = Screen.height - 70f;
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(Screen.width * 0.5f - 270f, y, 540f, currentIndex == 3 ? 54f : 32f), white);
            GUI.color = Colors[currentIndex];
            string who = w != null ? "SPECTATING PLAYER " + (System.Array.IndexOf(players, w) + 1) : "NO ONE LEFT TO WATCH";
            GUI.Label(new Rect(Screen.width * 0.5f - 270f, y, 540f, 32f), who + "     [Wheel] other teammate     [LMB] ping", chipStyle);
            if (currentIndex == 3 && w != null)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.85f);
                GUI.Label(new Rect(Screen.width * 0.5f - 270f, y + 26f, 540f, 26f), "Your torch is in their hands - you aim it with the mouse", chipStyle);
            }
            GUI.color = Color.white;
        }

        void DrawGameOver()
        {
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(0f, Screen.height * 0.4f, Screen.width, 90f), white);
            GUI.color = new Color(0.9f, 0.25f, 0.2f, 1f);
            GUI.Label(new Rect(0f, Screen.height * 0.4f + 10f, Screen.width, 40f), "THE TOWER TOOK EVERYONE", bannerStyle);
            GUI.color = new Color(1f, 1f, 1f, 0.85f);
            GUI.Label(new Rect(0f, Screen.height * 0.4f + 52f, Screen.width, 26f), "[R] start again", smallStyle);
            GUI.color = Color.white;
        }
    }
}
