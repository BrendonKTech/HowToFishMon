using BepInEx.Configuration;
using UnityEngine;

namespace HowToFishMon
{
    // in-game settings window. Every slider writes straight into the BepInEx config.
    internal static class ModMenu
    {
        public static bool Open;

        private static Vector2 _scroll;
        private static float _y, _w, _contentH = 1200f;
        private static bool _confirmReset;

        // key binding currently waiting for a key press (null = not rebinding)
        private static ConfigEntry<KeyCode> _listening;
        private static KeyCode[] _allKeys;
        public static bool Listening => _listening != null;
        public static void CancelRebind() => _listening = null;

        public static void Draw(float vw, float vh, System.Action close)
        {
            float w = 860f, h = Mathf.Min(780f, vh - 40f);
            var win = new Rect((vw - w) / 2f, (vh - h) / 2f, w, h);
            Styles.Window(win, 76f);
            GUI.Label(new Rect(win.x + 20, win.y + 12, 400, 36), "FishMon Mod Menu", Styles.Title);
            if (GUI.Button(new Rect(win.xMax - 100, win.y + 14, 80, 34), "Close", Styles.Button)) { close(); return; }
            GUI.Label(new Rect(win.x + 20, win.y + 50, w - 40, 22), _listening != null
                ? "<color=#ffe27a>Press the new key for this action (Esc or click the button again to cancel).</color>"
                : "Changes apply straight away and are saved when you close this menu. ↺ resets one setting to its default.", Styles.Dim);

            var area = new Rect(win.x + 16, win.y + 80, w - 32, h - 140);
            Styles.Fill(area, Styles.PanelDark);
            _w = area.width - 24;
            _scroll = GUI.BeginScrollView(area, _scroll, new Rect(0, 0, _w, _contentH));
            _y = 6f;

            Section("Controls");
            K(Plugin.ModMenuKey, "Mod Menu", "Open / close this settings menu.");
            K(Plugin.PartyKey, "Party menu", "Open / close your party, Box and Fishdex.");
            K(Plugin.SummonKey, "Send out / recall", "Throw out your lead FishMon, or call it back.");
            K(Plugin.SwapKey, "Swap FishMon", "Switch to the next healthy FishMon in your party.");
            K(Plugin.AttackKey, "Attack target", "Order your FishMon to attack what you're looking at.");
            K(Plugin.StayKey, "Stay / follow", "Tell your FishMon to stay put, or follow you again.");

            Section("Progression");
            F(Plugin.ExpMultiplier, "EXP rate", "How fast your FishMon level up (and evolve).", 0.1f, 10f, "x");

            Section("Your FishMon");
            F(Plugin.FishHpMultiplier, "Max HP", "Multiplies every FishMon's HP. Raise this if bosses one-shot them.", 0.5f, 10f, "x");
            F(Plugin.DamageToBossMultiplier, "Damage dealt", "How hard your FishMon hit bosses and creatures.", 0.1f, 10f, "x");
            F(Plugin.BossDamageToFishMultiplier, "Damage taken", "How hard bosses and creatures hit your FishMon. 0 = they can't be hurt.", 0f, 3f, "x");
            F(Plugin.AttackSpeedMultiplier, "Attack speed", "How often your FishMon leap at their target.", 0.25f, 4f, "x");
            F(Plugin.CompanionScale, "Size", "How big FishMon are out in the world (applies the next time one is sent out).", 0.3f, 3f, "x");
            B(Plugin.RoamAroundYou, "Roam around you", "Wander near you, mostly where you're looking, instead of trailing behind you.");
            F(Plugin.RoamRadius, "Roam distance", "How far from you a roaming FishMon may wander.", 3f, 20f, "m");
            B(Plugin.AutoSendNext, "Auto send next", "When your FishMon faints, send out the next healthy one.");
            B(Plugin.AttackEffects, "Attack effects", "Type-themed lights, splashes, lightning, trails and sounds.");

            Section("Enemies");
            I(Plugin.MiniBossLevel, "Mini boss level", "Battle level of mini bosses.", 1, 100, "");
            I(Plugin.BossLevel, "Main boss level", "Battle level of main bosses.", 1, 100, "");
            I(Plugin.WildFoeLevel, "Wild creature level", "Battle level of crabs, piranhas and other hostile creatures.", 1, 100, "");
            F(Plugin.AggroRange, "Boss range", "Your FishMon go for a boss when it's this close to you.", 10f, 200f, "m");
            B(Plugin.FightWildCreatures, "Fight wild creatures", "Also attack hostile creatures that aren't bosses.");
            F(Plugin.WildAggroRange, "Wild creature range", "How close a hostile creature must be before your FishMon go for it.", 0f, 100f, "m");

            Section("Healing");
            F(Plugin.RegenPercentPerMinute, "Regen per minute", "HP healed per minute outside fights (fainted FishMon don't regen).", 0f, 100f, "%");
            I(Plugin.HealAllCost, "Heal All cost", "Price of Heal All in the party menu.", 0, 1000, "$");
            I(Plugin.ReviveCost, "Revive cost", "Price of reviving one fainted FishMon.", 0, 2000, "$");
            F(Plugin.ReviveHpPercent, "Revive HP", "How much HP a revived FishMon comes back with.", 1f, 100f, "%");

            Section("Abilities");
            I(Plugin.AbilityRerollCost, "Reroll cost", "Price of rerolling a FishMon's ability in the party menu.", 0, 2000, "$");

            Section("Held items");
            F(Plugin.ItemPriceMultiplier, "Item prices", "Scales the price of every item in the shop (party menu → Item tab). 0 = free.", 0f, 5f, "x");

            Section("Catching");
            B(Plugin.OnlyCatchHooked, "Only hooked fish", "Only fish you hooked with your own rod count as a catch.");
            I(Plugin.MinCatchLevel, "Lowest catch level", "Lowest level a new catch can be.", 1, 100, "");
            I(Plugin.MaxCatchLevel, "Highest catch level", "Highest level a new catch can be.", 1, 100, "");
            I(Plugin.MaxPartySize, "Party size", "How many FishMon you can carry (extras go to the Box).", 1, 12, "");

            _contentH = _y + 10f;
            GUI.EndScrollView();

            // footer: what the HP setting means for the fish you'd fight with
            var lead = FishMonController.Active ? FishMonController.Active.Mon : FishMonStorage.Lead;
            float fy = win.yMax - 50f;
            if (lead != null)
                GUI.Label(new Rect(win.x + 20, fy, w - 260, 34), $"{lead.DisplayName} (Lv. {lead.Level}) has <b>{lead.MaxHp} HP</b> with these settings.", Styles.Small);
            if (_confirmReset)
            {
                if (GUI.Button(new Rect(win.xMax - 350, fy, 160, 34), "Yes, reset all", Styles.DangerButton)) { ResetAll(); _confirmReset = false; }
                if (GUI.Button(new Rect(win.xMax - 180, fy, 160, 34), "Cancel", Styles.Button)) _confirmReset = false;
            }
            else if (GUI.Button(new Rect(win.xMax - 220, fy, 200, 34), "Reset all to defaults", Styles.Button)) _confirmReset = true;
        }

        private static void ResetAll()
        {
            foreach (var kv in Plugin.Instance.Config)
            {
                if (kv.Key.Section == "Keys") continue; // keep key bindings
                kv.Value.BoxedValue = kv.Value.DefaultValue;
            }
            FishMonController.ShowToast("FishMon settings reset to defaults.");
        }

        // rows

        private static void Section(string title)
        {
            _y += 6f;
            GUI.Label(new Rect(8, _y, _w, 28), $"<color=#9fd3ff>{title}</color>", Styles.BodyBold);
            _y += 30f;
        }

        private static Rect RowStart(string label, string desc)
        {
            var row = new Rect(4, _y, _w - 8, 48);
            Styles.Fill(row, new Color(1f, 1f, 1f, 0.04f));
            GUI.Label(new Rect(14, _y + 2, 260, 24), label, Styles.SmallBold);
            GUI.Label(new Rect(14, _y + 24, _w - 30, 22), desc, Styles.Dim);
            _y += 52f;
            return row;
        }

        private static bool ResetButton(Rect row, bool isDefault)
        {
            GUI.enabled = !isDefault;
            bool hit = GUI.Button(new Rect(row.xMax - 40, row.y + 4, 34, 24), "↺", Styles.Button);
            GUI.enabled = true;
            return hit;
        }

        private static string Fmt(float v, string unit)
        {
            switch (unit)
            {
                case "x": return v.ToString("0.00") + "×";
                case "%": return v.ToString("0") + "%";
                case "m": return v.ToString("0") + " m";
                case "$": return "$" + v.ToString("0");
                default: return v.ToString("0");
            }
        }

        private static void F(ConfigEntry<float> e, string label, string desc, float min, float max, string unit)
        {
            var row = RowStart(label, desc);
            float step = max <= 10f ? 0.05f : 1f;
            float v = GUI.HorizontalSlider(new Rect(290, row.y + 9, row.width - 470, 16), e.Value, min, max);
            v = Mathf.Round(v / step) * step;
            if (Mathf.Abs(v - e.Value) > step * 0.5f) e.Value = v;
            GUI.Label(new Rect(row.xMax - 170, row.y + 2, 120, 24), Fmt(e.Value, unit), Styles.SmallRight);
            if (ResetButton(row, Mathf.Approximately(e.Value, (float)e.DefaultValue))) e.Value = (float)e.DefaultValue;
        }

        private static void I(ConfigEntry<int> e, string label, string desc, int min, int max, string unit)
        {
            var row = RowStart(label, desc);
            float v = GUI.HorizontalSlider(new Rect(290, row.y + 9, row.width - 470, 16), e.Value, min, max);
            int iv = Mathf.RoundToInt(v);
            if (max >= 500) iv = Mathf.RoundToInt(v / 5f) * 5; // money in $5 steps
            if (iv != e.Value) e.Value = iv;
            GUI.Label(new Rect(row.xMax - 170, row.y + 2, 120, 24), Fmt(e.Value, unit), Styles.SmallRight);
            if (ResetButton(row, e.Value == (int)e.DefaultValue)) e.Value = (int)e.DefaultValue;
        }

        // key binding row: click the button, then press the new key
        private static void K(ConfigEntry<KeyCode> e, string label, string desc)
        {
            var row = RowStart(label, desc);
            bool listening = _listening == e;
            string text = listening ? "<color=#ffe27a>Press a key...</color>" : $"<b>{Plugin.KeyName(e)}</b>";
            if (GUI.Button(new Rect(row.xMax - 210, row.y + 4, 160, 26), text, Styles.Button))
                _listening = listening ? null : e;
            string clash = listening ? null : FishMonController.GameActionFor(e.Value);
            if (clash != null)
                GUI.Label(new Rect(290, row.y + 4, row.width - 510, 24), $"<color=#ffcc66>also the game's '{clash}' key</color>", Styles.SmallRight);
            if (ResetButton(row, e.Value == (KeyCode)e.DefaultValue)) { _listening = null; SetKey(e, (KeyCode)e.DefaultValue); }
        }

        // called from Update while a binding is waiting for a key
        public static void TickRebind()
        {
            if (_listening == null) return;
            if (!Open) { _listening = null; return; }
            if (Input.GetKeyDown(KeyCode.Escape)) { _listening = null; return; }
            if (_allKeys == null) _allKeys = (KeyCode[])System.Enum.GetValues(typeof(KeyCode));
            foreach (var k in _allKeys)
            {
                // left/right click are for the menu itself; gamepads aren't supported
                if (k == KeyCode.None || k == KeyCode.Escape || k == KeyCode.Mouse0 || k == KeyCode.Mouse1 || k >= KeyCode.JoystickButton0) continue;
                if (!Input.GetKeyDown(k)) continue;
                var e = _listening;
                _listening = null;
                SetKey(e, k);
                return;
            }
        }

        // set a binding; if another FishMon action already uses that key, the two swap
        private static void SetKey(ConfigEntry<KeyCode> e, KeyCode k)
        {
            var old = e.Value;
            if (old == k) return;
            foreach (var other in Plugin.KeyBindings)
                if (other != e && other.Value == k)
                {
                    other.Value = old;
                    FishMonController.ShowToast($"{Plugin.KeyName(k)} was already used for {other.Definition.Key}, so that's now {Plugin.KeyName(old)}.", new Color(1f, 0.85f, 0.5f), 4f);
                }
            e.Value = k;
        }

        private static void B(ConfigEntry<bool> e, string label, string desc)
        {
            var row = RowStart(label, desc);
            if (GUI.Button(new Rect(row.xMax - 170, row.y + 4, 120, 26), e.Value ? "<color=#9fffb0>ON</color>" : "<color=#ff9a9a>OFF</color>", Styles.Button))
                e.Value = !e.Value;
            if (ResetButton(row, e.Value == (bool)e.DefaultValue)) e.Value = (bool)e.DefaultValue;
        }
    }
}
