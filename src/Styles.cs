using System.Collections.Generic;
using UnityEngine;

namespace HowToFishMon
{
    // IMGUI look &amp; feel: flat dark panels, coloured type chips, HP bars
    internal static class Styles
    {
        public static readonly Color Panel = new Color(0.10f, 0.13f, 0.19f, 0.96f);
        public static readonly Color PanelDark = new Color(0.06f, 0.08f, 0.12f, 0.95f);
        public static readonly Color Accent = new Color(0.35f, 0.65f, 1f, 1f);

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

        private static GUIStyle Btn(Color bg, Color hover, int size = 16)
        {
            var s = new GUIStyle(GUI.skin.button) { fontSize = size, richText = true, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            s.normal.background = T(bg); s.hover.background = T(hover); s.active.background = T(hover * 0.8f);
            s.focused.background = T(bg);
            s.normal.textColor = s.hover.textColor = s.active.textColor = s.focused.textColor = Color.white;
            s.border = new RectOffset(0, 0, 0, 0);
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
            TabOn = Btn(Accent * 0.8f, Accent);
            TabOff = Btn(new Color(1f, 1f, 1f, 0.08f), new Color(1f, 1f, 1f, 0.18f));

            Field = new GUIStyle(GUI.skin.textField) { fontSize = 15 };
            Field.normal.background = Field.focused.background = Field.hover.background = T(new Color(1f, 1f, 1f, 0.12f));
            Field.normal.textColor = Field.focused.textColor = Field.hover.textColor = Color.white;
        }

        public static void Fill(Rect r, Color c)
        {
            if (Event.current.type != EventType.Repaint) return;
            GUI.DrawTexture(r, T(c));
        }

        public static void Border(Rect r, Color c, float t)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        public static void Bar(Rect r, float frac, Color c)
        {
            Fill(r, new Color(0f, 0f, 0f, 0.55f));
            Fill(new Rect(r.x, r.y, r.width * Mathf.Clamp01(frac), r.height), c);
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
