using System.Collections.Generic;
using UnityEngine;

namespace HowToFishMon
{
    // IMGUI look &amp; feel: rounded dark panels with soft shadows, coloured type chips, HP bars
    internal static class Styles
    {
        public static readonly Color Panel = new Color(0.09f, 0.11f, 0.16f, 0.97f);
        public static readonly Color PanelDark = new Color(0.05f, 0.07f, 0.11f, 0.92f);
        public static readonly Color Header = new Color(0.13f, 0.17f, 0.25f, 1f);
        public static readonly Color Accent = new Color(0.35f, 0.65f, 1f, 1f);

        private const float Radius = 6f;         // panels, rows, chips
        private const float WindowRadius = 12f;  // whole windows

        public static GUIStyle Title, TitleRight, Body, BodyBold, BodyRight, BodyWrap, Small, SmallWrap, SmallBold, SmallRight,
            Dim, Toast, Message, Plate, Floater, Center, Button, DangerButton, MoveButton, TabOn, TabOff, Chip, Field;

        private static readonly Dictionary<Color, Texture2D> Tex = new Dictionary<Color, Texture2D>();
        private static bool _ready;

        public static Texture2D T(Color c)
        {
            if (Tex.TryGetValue(c, out var t) && t) return t;
            t = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            Tex[c] = t;
            return t;
        }

        private static GUIStyle Label(int size, FontStyle fs = FontStyle.Normal, TextAnchor a = TextAnchor.MiddleLeft, Color? col = null, bool wrap = false)
        {
            var s = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = fs, alignment = a, richText = true, wordWrap = wrap, clipping = TextClipping.Clip };
            s.normal.textColor = col ?? Color.white;
            return s;
        }

        // a rounded rectangle texture for 9-slicing (GUIStyle.border keeps the corners from stretching),
        // a touch lighter at the top so buttons don't look completely flat
        private static readonly Dictionary<Color, Texture2D> RoundTex = new Dictionary<Color, Texture2D>();
        private const int RoundSize = 32, RoundRadius = 8;

        private static Texture2D Rounded(Color c)
        {
            if (RoundTex.TryGetValue(c, out var t) && t) return t;
            t = new Texture2D(RoundSize, RoundSize, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear
            };
            for (int y = 0; y < RoundSize; y++)
                for (int x = 0; x < RoundSize; x++)
                {
                    // distance outside the rounded corner -> soft (anti-aliased) edge
                    float px = x + 0.5f, py = y + 0.5f;
                    float cx = Mathf.Clamp(px, RoundRadius, RoundSize - RoundRadius), cy = Mathf.Clamp(py, RoundRadius, RoundSize - RoundRadius);
                    float a = Mathf.Clamp01(RoundRadius - Vector2.Distance(new Vector2(px, py), new Vector2(cx, cy)) + 0.5f);
                    float shade = Mathf.Lerp(0.9f, 1.12f, y / (float)(RoundSize - 1)); // texture y goes up: top is lighter
                    t.SetPixel(x, y, new Color(c.r * shade, c.g * shade, c.b * shade, c.a * a));
                }
            t.Apply();
            RoundTex[c] = t;
            return t;
        }

        private static Color Shade(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        private static GUIStyle Btn(Color bg, Color hover, int size = 16)
        {
            var s = new GUIStyle(GUI.skin.button) { fontSize = size, richText = true, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            s.normal.background = Rounded(bg); s.hover.background = Rounded(hover); s.active.background = Rounded(Shade(hover, 0.8f));
            s.focused.background = Rounded(bg);
            s.normal.textColor = s.hover.textColor = s.active.textColor = s.focused.textColor = Color.white;
            s.border = new RectOffset(RoundRadius + 1, RoundRadius + 1, RoundRadius + 1, RoundRadius + 1);
            return s;
        }

        public static void Init()
        {
            if (_ready && Title != null) return;
            _ready = true;
            Title = Label(24, FontStyle.Bold);
            TitleRight = Label(22, FontStyle.Bold, TextAnchor.MiddleRight);
            Body = Label(18);
            BodyBold = Label(18, FontStyle.Bold);
            BodyRight = Label(17, FontStyle.Bold, TextAnchor.MiddleRight);
            BodyWrap = Label(17, FontStyle.Normal, TextAnchor.UpperLeft, null, true);
            Small = Label(15);
            SmallWrap = Label(15, FontStyle.Normal, TextAnchor.UpperLeft, null, true);
            SmallBold = Label(15, FontStyle.Bold);
            SmallRight = Label(15, FontStyle.Bold, TextAnchor.MiddleRight);
            Dim = Label(14, FontStyle.Normal, TextAnchor.MiddleLeft, new Color(0.7f, 0.75f, 0.85f));
            Toast = Label(18, FontStyle.Bold, TextAnchor.MiddleCenter);
            Message = Label(24, FontStyle.Normal, TextAnchor.UpperLeft, null, true);
            Center = Label(16, FontStyle.Normal, TextAnchor.MiddleCenter);
            Plate = Label(14, FontStyle.Bold, TextAnchor.MiddleCenter);
            Floater = Label(20, FontStyle.Bold, TextAnchor.MiddleCenter);
            Chip = Label(13, FontStyle.Bold, TextAnchor.MiddleCenter);

            Button = Btn(new Color(0.22f, 0.32f, 0.48f), new Color(0.30f, 0.45f, 0.68f));
            DangerButton = Btn(new Color(0.55f, 0.18f, 0.18f), new Color(0.75f, 0.25f, 0.25f));
            MoveButton = Btn(new Color(0f, 0f, 0f, 0.15f), new Color(1f, 1f, 1f, 0.2f), 17);
            TabOn = Btn(Shade(Accent, 0.8f), Accent);
            TabOff = Btn(new Color(1f, 1f, 1f, 0.08f), new Color(1f, 1f, 1f, 0.18f));

            Field = new GUIStyle(GUI.skin.textField) { fontSize = 15 };
            Field.normal.background = Field.focused.background = Field.hover.background = T(new Color(1f, 1f, 1f, 0.12f));
            Field.normal.textColor = Field.focused.textColor = Field.hover.textColor = Color.white;
        }

        // rounded rectangle: filled when border is 0, otherwise just the outline
        private static void Draw(Rect r, Color c, float border, float radius)
        {
            if (Event.current.type != EventType.Repaint || r.width <= 0f || r.height <= 0f) return;
            radius = Mathf.Min(radius, Mathf.Min(r.width, r.height) * 0.5f);
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, border, radius);
        }

        public static void Fill(Rect r, Color c) => Draw(r, c, 0f, Radius);

        public static void Border(Rect r, Color c, float t) => Draw(r, c, t, Radius);

        // a whole window: dims the game behind it, soft drop shadow, header band with an accent line
        public static void Window(Rect r, float headerH)
        {
            if (Event.current.type != EventType.Repaint) return;
            Draw(new Rect(-5000f, -5000f, 20000f, 20000f), new Color(0f, 0f, 0f, 0.35f), 0f, 0f);
            for (int i = 1; i <= 4; i++)
                Draw(new Rect(r.x - i * 3f, r.y - i * 3f + 8f, r.width + i * 6f, r.height + i * 6f), new Color(0f, 0f, 0f, 0.09f), 0f, WindowRadius + i * 3f);
            Draw(r, Panel, 0f, WindowRadius);
            Draw(new Rect(r.x, r.y, r.width, headerH), Header, 0f, WindowRadius);
            Draw(new Rect(r.x, r.y + headerH - WindowRadius, r.width, WindowRadius), Header, 0f, 0f); // square off the header's bottom
            Draw(new Rect(r.x, r.y + headerH - 2f, r.width, 2f), new Color(Accent.r, Accent.g, Accent.b, 0.85f), 0f, 0f);
            Draw(r, new Color(Accent.r, Accent.g, Accent.b, 0.4f), 1.5f, WindowRadius);
        }

        // pill-shaped progress bar (HP, EXP)
        public static void Bar(Rect r, float frac, Color c)
        {
            float rad = r.height * 0.5f;
            Draw(r, new Color(0f, 0f, 0f, 0.55f), 0f, rad);
            Draw(new Rect(r.x, r.y, r.width * Mathf.Clamp01(frac), r.height), c, 0f, rad);
        }

        public static void HpBar(Rect r, float frac)
        {
            frac = Mathf.Clamp01(frac);
            Color c = frac > 0.5f ? new Color(0.3f, 0.85f, 0.4f) : frac > 0.2f ? new Color(0.95f, 0.8f, 0.2f) : new Color(0.95f, 0.3f, 0.25f);
            Bar(r, frac, c);
        }

        public static void TypeChip(Rect r, MonType t)
        {
            Fill(r, TypeChart.ColorOf(t));
            GUI.Label(r, t.ToString().ToUpperInvariant(), Chip);
        }

        public static bool Tab(Rect r, string text, bool on) => GUI.Button(r, text, on ? TabOn : TabOff);
    }
}
