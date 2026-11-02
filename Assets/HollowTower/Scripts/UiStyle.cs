using System.Collections.Generic;
using UnityEngine;

namespace HollowTower
{
    /// <summary>
    /// LAST BELL's look for every player-facing screen (the user's reference art): black slabs, a little crooked, with a
    /// chiselled white edge; chunky rounded lettering in pale metal with a dark rim (Fredoka, OFL); the title in a
    /// heavy condensed face in brackets - "[last bell]" (Anton, OFL); small monster silhouettes peeking over the edges
    /// of the big panels, claws hooked over the top. All IMGUI: a script calls Use() at the top of its OnGUI (its
    /// boxes, buttons and labels take the skin), and uses Slab/Button/Label/Title for the hand-drawn parts.
    /// Dev overlays deliberately keep Unity's default look.
    /// </summary>
    public static class UiStyle
    {
        static Font body, title;
        static Texture2D slab, slabHover, slabSolid, monster, claws;
        static GUISkin skin;
        static readonly Dictionary<long, GUIStyle> texts = new Dictionary<long, GUIStyle>();

        public static readonly Color Ink = new Color(0.9f, 0.9f, 0.88f);
        public static readonly Color Dim = new Color(0.62f, 0.62f, 0.6f);
        public static readonly Color Alarm = new Color(1f, 0.42f, 0.32f);

        public static Font Body { get { if (body == null) body = Resources.Load<Font>("Fonts/FredokaBold"); return body; } }
        public static Font TitleFont { get { if (title == null) title = Resources.Load<Font>("Fonts/Anton"); return title; } }

        /// <summary>Call first thing in OnGUI: this script's boxes, buttons and labels take the LAST BELL skin.</summary>
        public static void Use() { GUI.skin = Skin; }

        public static GUISkin Skin
        {
            get
            {
                if (skin != null) return skin;
                Textures();
                skin = Object.Instantiate(GUI.skin);
                skin.font = Body;
                foreach (var s in new[] { skin.label, skin.box, skin.button, skin.textField, skin.toggle, skin.window })
                {
                    s.font = Body;
                    s.normal.textColor = s.hover.textColor = s.active.textColor = s.focused.textColor = Ink;
                }
                var b = new RectOffset(7, 7, 7, 7);
                skin.box.normal.background = slab; skin.box.border = b; skin.box.padding = new RectOffset(12, 12, 8, 8);
                skin.button.normal.background = slab; skin.button.hover.background = slabHover; skin.button.active.background = slabSolid;
                skin.button.focused.background = slab; skin.button.border = b; skin.button.active.textColor = Color.black;
                skin.textField.normal.background = slab; skin.textField.focused.background = slabHover; skin.textField.hover.background = slabHover; skin.textField.border = b;
                skin.window.normal.background = slab; skin.window.onNormal.background = slab; skin.window.border = b;
                return skin;
            }
        }

        // ------------------------------------------------------------------ textures, drawn once
        static void Textures()
        {
            if (slab != null) return;
            slab = Frame(new Color(0f, 0f, 0f, 0.93f), new Color(0.92f, 0.92f, 0.9f), new Color(0.45f, 0.45f, 0.44f));
            slabHover = Frame(new Color(0.07f, 0.07f, 0.07f, 0.96f), Color.white, new Color(0.85f, 0.85f, 0.85f));
            slabSolid = Frame(new Color(0.82f, 0.82f, 0.8f, 1f), Color.white, new Color(0.3f, 0.3f, 0.3f));
            monster = Silhouette(160, 120, MonsterShape);
            claws = Silhouette(96, 40, ClawShape);
        }

        /// <summary>A 9-sliced slab: fill, a white edge (3 px), a dark groove, a grey inner line - the chiselled rim.</summary>
        static Texture2D Frame(Color fill, Color edge, Color inner)
        {
            const int N = 32;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[N * N];
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    int d = Mathf.Min(Mathf.Min(x, N - 1 - x), Mathf.Min(y, N - 1 - y));
                    Color c = d < 1 ? new Color(edge.r, edge.g, edge.b, 0.55f) : d < 3 ? edge : d < 4 ? new Color(0.05f, 0.05f, 0.05f, 1f) : d < 5 ? inner : fill;
                    px[y * N + x] = c;
                }
            t.SetPixels(px); t.Apply();
            return t;
        }

        delegate float Shape(float x, float y);   // signed distance in pixels (negative inside)

        static Texture2D Silhouette(int w, int h, Shape f)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float d = f(x, y);
                    Color c;
                    if (d < -2.5f) c = Color.black;
                    else if (d < 0f) c = Color.Lerp(Color.black, new Color(0.85f, 0.85f, 0.85f), (d + 2.5f) / 2.5f);
                    else if (d < 2.5f) c = new Color(0.95f, 0.95f, 0.95f, 1f - d / 2.5f);
                    else c = new Color(0, 0, 0, 0);
                    px[y * w + x] = c;
                }
            t.SetPixels(px); t.Apply();
            return t;
        }

        static float Circle(float x, float y, float cx, float cy, float r) { return Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r; }
        static float Capsule(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            float px = x - ax, py = y - ay, dx = bx - ax, dy = by - ay;
            float h = Mathf.Clamp01((px * dx + py * dy) / (dx * dx + dy * dy));
            float qx = px - dx * h, qy = py - dy * h;
            return Mathf.Sqrt(qx * qx + qy * qy) - r;
        }

        /// <summary>A squat horned imp peering over an edge, one clawed hand up in a wave (texture y goes up).</summary>
        static float MonsterShape(float x, float y)
        {
            float d = Circle(x, y, 70, 38, 34);                                    // body, cut by the edge below
            d = Mathf.Min(d, Circle(x, y, 70, 66, 26));                            // head
            d = Mathf.Min(d, Capsule(x, y, 52, 82, 44, 104, 5));                   // horns
            d = Mathf.Min(d, Capsule(x, y, 88, 82, 96, 104, 5));
            d = Mathf.Min(d, Capsule(x, y, 98, 50, 124, 84, 8));                   // waving arm
            d = Mathf.Min(d, Capsule(x, y, 124, 84, 118, 104, 3));                 // three claws
            d = Mathf.Min(d, Capsule(x, y, 124, 84, 128, 106, 3));
            d = Mathf.Min(d, Capsule(x, y, 124, 84, 136, 102, 3));
            d = Mathf.Min(d, Capsule(x, y, 40, 30, 22, 12, 7));                    // other hand gripping the edge
            float eyes = Mathf.Min(Circle(x, y, 60, 70, 4.5f), Circle(x, y, 80, 70, 4.5f));
            d = Mathf.Max(d, -eyes);                                               // eye holes show the rim colour
            return Mathf.Max(d, 6f - y);                                           // nothing below the edge line
        }

        /// <summary>A hand's worth of claws hooked over an edge.</summary>
        static float ClawShape(float x, float y)
        {
            float d = Capsule(x, y, 20, 8, 76, 8, 7);
            for (int i = 0; i < 4; i++) d = Mathf.Min(d, Capsule(x, y, 26 + i * 15, 10, 22 + i * 15 + (i % 2) * 3, 30, 4));
            return Mathf.Max(d, 2f - y);
        }

        // ------------------------------------------------------------------ drawing
        static float Tilt(Rect r) { return ((Mathf.RoundToInt(r.x * 7f + r.y * 13f) % 5) - 2) * 0.45f; }

        /// <summary>A black slab with the chiselled edge, turned a little (degrees) like a hand-cut card.</summary>
        public static void Slab(Rect r, float tilt)
        {
            Textures();
            var m = GUI.matrix;
            if (tilt != 0f) GUIUtility.RotateAroundPivot(tilt, r.center);
            Draw9(r, slab);
            GUI.matrix = m;
        }

        static void Draw9(Rect r, Texture2D t)
        {
            // 9-slice by hand (GUI.DrawTexture stretches the border): corners stay 7 px
            const float b = 7f, u = 7f / 32f;
            float[] xs = { r.x, r.x + b, r.xMax - b, r.xMax }, ys = { r.y, r.y + b, r.yMax - b, r.yMax };
            float[] us = { 0f, u, 1f - u, 1f };
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    var dst = Rect.MinMaxRect(xs[i], ys[j], xs[i + 1], ys[j + 1]);
                    var src = Rect.MinMaxRect(us[i], 1f - us[j + 1], us[i + 1], 1f - us[j]);
                    if (dst.width > 0f && dst.height > 0f) GUI.DrawTextureWithTexCoords(dst, t, src);
                }
        }

        /// <summary>A straight slab for HUD strips (returns the rect for chaining).</summary>
        public static Rect Bar(Rect r) { Slab(r, 0f); return r; }

        /// <summary>The rounded lettering at a size, cached.</summary>
        public static GUIStyle Text(int size, TextAnchor anchor = TextAnchor.MiddleCenter, bool wrap = true)
        {
            long key = size * 100 + (int)anchor * 2 + (wrap ? 1 : 0);
            GUIStyle s;
            if (!texts.TryGetValue(key, out s))
            {
                s = new GUIStyle { font = Body, fontSize = size, alignment = anchor, wordWrap = wrap, richText = true };
                s.normal.textColor = Ink;
                texts[key] = s;
            }
            return s;
        }

        /// <summary>Pale metal letters with a dark rim (the reference's chiselled buttons).</summary>
        public static void Label(Rect r, string text, int size, TextAnchor anchor = TextAnchor.MiddleCenter, Color? colour = null)
        {
            var s = Text(size, anchor);
            var keep = s.normal.textColor;
            s.normal.textColor = new Color(0f, 0f, 0f, 0.85f);
            float o = Mathf.Max(1f, size / 14f);
            GUI.Label(new Rect(r.x - o, r.y, r.width, r.height), text, s); GUI.Label(new Rect(r.x + o, r.y, r.width, r.height), text, s);
            GUI.Label(new Rect(r.x, r.y - o, r.width, r.height), text, s); GUI.Label(new Rect(r.x, r.y + o * 1.6f, r.width, r.height), text, s);
            s.normal.textColor = colour ?? Ink;
            GUI.Label(r, text, s);
            s.normal.textColor = keep;
        }

        /// <summary>A slab button with its label; hover lifts the edge, press fills it. Returns true when clicked.</summary>
        public static bool Button(Rect r, string text, int size = 18, float? tilt = null)
        {
            Textures();
            bool over = GUI.enabled && r.Contains(Event.current.mousePosition);
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(tilt ?? Tilt(r), r.center);
            Draw9(over ? new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4) : r, over ? slabHover : slab);
            var c = GUI.color;
            if (!GUI.enabled) GUI.color = new Color(1f, 1f, 1f, 0.4f);
            Label(r, text, size, TextAnchor.MiddleCenter, over ? Color.white : Ink);
            GUI.color = c;
            bool hit = GUI.Button(r, GUIContent.none, GUIStyle.none);
            GUI.matrix = m;
            return hit;
        }

        /// <summary>The game's name the way the reference draws it: "[last bell]", heavy, black inside a white rim.</summary>
        public static void Title(Rect r, int size, string text = "[last bell]")
        {
            var s = new GUIStyle { font = TitleFont, fontSize = size, alignment = TextAnchor.MiddleCenter };
            float o = Mathf.Max(2f, size / 58f);
            s.normal.textColor = new Color(0.92f, 0.92f, 0.9f);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6f;
                GUI.Label(new Rect(r.x + Mathf.Cos(a) * o, r.y + Mathf.Sin(a) * o, r.width, r.height), text, s);
            }
            s.normal.textColor = new Color(0.25f, 0.25f, 0.25f);
            GUI.Label(new Rect(r.x + o * 0.5f, r.y + o * 0.5f, r.width, r.height), text, s);
            s.normal.textColor = Color.black;
            GUI.Label(r, text, s);
        }

        /// <summary>A little imp peering over an edge (bottom of the rect on the edge).</summary>
        public static void Monster(Rect r, bool flip = false)
        {
            Textures();
            if (flip) GUI.DrawTextureWithTexCoords(r, monster, new Rect(1f, 0f, -1f, 1f));
            else GUI.DrawTexture(r, monster);
        }

        /// <summary>Claws hooked over an edge.</summary>
        public static void Claws(Rect r) { Textures(); GUI.DrawTexture(r, claws); }

        /// <summary>A panel with a monster peeking over its top-right corner and claws on its top-left - for the big ones.</summary>
        public static void Panel(Rect r, float tilt = -0.6f, bool creatures = true)
        {
            Slab(r, tilt);
            if (!creatures) return;
            Claws(new Rect(r.x + 18, r.y - 22, 64, 26));
            Monster(new Rect(r.xMax - 120, r.y - 74, 100, 75));
        }
    }
}
