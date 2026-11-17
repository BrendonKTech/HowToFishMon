using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx.Configuration;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace HowToFishMon
{
    public enum Preset { Normal, Challenge, Custom }

    // settings presets. Normal and Challenge set (and lock) the balance settings, and make boss, wild creature
    // and catch levels grow island by island. Custom lets you change everything; your own values are kept
    // while you play on a preset and come back when you switch to Custom. Badges are only earned on Challenge.
    public static class Presets
    {
        public static Preset Current => Plugin.SettingsPreset.Value;
        public static bool Locked => Current != Preset.Custom;

        // main boss level on the first and the last island (the whale)
        public const int FirstIslandLevel = 10, LastIslandLevel = 50;

        private static Dictionary<ConfigEntryBase, (object normal, object challenge)> _values;

        // the settings a preset sets: (Normal, Challenge). Controls, visuals and following are always yours to change
        private static Dictionary<ConfigEntryBase, (object normal, object challenge)> Values =>
            _values ?? (_values = new Dictionary<ConfigEntryBase, (object, object)>
            {
                [Plugin.ExpMultiplier] = (1f, 1f),
                [Plugin.FishHpMultiplier] = (2f, 1.5f),
                [Plugin.DamageToBossMultiplier] = (1f, 1f),
                [Plugin.BossDamageToFishMultiplier] = (1f, 1.25f),
                [Plugin.AttackSpeedMultiplier] = (1f, 1f),
                [Plugin.RegenPercentPerMinute] = (10f, 5f),
                [Plugin.HealAllCost] = (25, 50),
                [Plugin.ReviveCost] = (100, 200),
                [Plugin.ReviveHpPercent] = (50f, 50f),
                [Plugin.AbilityRerollCost] = (250, 250),
                [Plugin.ItemPriceMultiplier] = (1f, 1f),
                [Plugin.OnlyCatchHooked] = (true, true),
                [Plugin.MaxPartySize] = (6, 6),
                [Plugin.CatchDifficulty] = (1f, 1.25f),
                [Plugin.DexRewardMultiplier] = (1f, 1f),
            });

        // the level settings: on a preset the island decides these instead
        private static IEnumerable<ConfigEntryBase> LevelEntries =>
            new ConfigEntryBase[] { Plugin.BossLevel, Plugin.MiniBossLevel, Plugin.WildFoeLevel, Plugin.MinCatchLevel, Plugin.MaxCatchLevel };

        // everything a host's lobby shares with its guests (see LobbySettings)
        internal static IEnumerable<ConfigEntryBase> SyncedEntries =>
            new ConfigEntryBase[] { Plugin.SettingsPreset }.Concat(Values.Keys).Concat(LevelEntries);

        // greyed out in the Mod Menu: set by the preset, or by the host when you're in someone else's world
        public static bool IsLocked(ConfigEntryBase e) =>
            (LobbySettings.FromHost && SyncedEntries.Contains(e)) || (Locked && (Values.ContainsKey(e) || LevelEntries.Contains(e)));

        public static string Describe(Preset p)
        {
            switch (p)
            {
                case Preset.Normal: return "The way FishMon is meant to be played. Bosses, wild creatures and catches get stronger island by island (main bosses go from Lv. 10 to Lv. 50).";
                case Preset.Challenge: return "Like Normal, but tougher: less HP, bosses hit harder, slower healing, pricier heals. The only preset where you can earn boss badges.";
                default: return "Change everything yourself. Badges can't be earned.";
            }
        }

        // firstRun: the config had no preset yet (updating from an older version)
        public static void Init(bool firstRun)
        {
            if (firstRun)
            {
                // keep your own settings if you'd changed any
                bool changed = Values.Any(kv => !Equals(kv.Key.BoxedValue, kv.Value.normal)) ||
                               LevelEntries.Any(e => !Equals(e.BoxedValue, e.DefaultValue));
                Plugin.SettingsPreset.Value = changed ? Preset.Custom : Preset.Normal;
                if (changed) SaveCustom();
                Plugin.Log.LogInfo(changed ? "You had changed FishMon settings, so the Custom preset is on." : "Using the Normal preset.");
            }
            if (Locked) Apply(Current);
        }

        public static void Set(Preset p)
        {
            var was = Current;
            if (p == was) return;
            if (LobbySettings.FromHost)
            {
                FishMonController.ShowToast("You're using the host's settings. Only the host can change the preset.", new Color(1f, 0.8f, 0.5f), 3f);
                return;
            }
            if (was == Preset.Custom) SaveCustom();
            Plugin.SettingsPreset.Value = p;
            if (p == Preset.Custom) LoadCustom();
            else Apply(p);
            LobbySettings.SaveConfig();
            FishMonController.ShowToast($"{p} preset on.", new Color(0.7f, 0.9f, 1f), 3f);
        }

        private static void Apply(Preset p)
        {
            foreach (var kv in Values)
            {
                var v = p == Preset.Challenge ? kv.Value.challenge : kv.Value.normal;
                if (!Equals(kv.Key.BoxedValue, v)) kv.Key.BoxedValue = v;
            }
        }

        // your own values, kept while a preset is on
        private static string CustomPath => Path.Combine(BepInEx.Paths.ConfigPath, "FishMon_custom_settings.json");
        internal static string KeyOf(ConfigEntryBase e) => e.Definition.Section + "/" + e.Definition.Key;

        private static void SaveCustom()
        {
            try
            {
                var d = Values.Keys.ToDictionary(KeyOf, e => e.GetSerializedValue());
                File.WriteAllText(CustomPath, JsonConvert.SerializeObject(d, Formatting.Indented));
            }
            catch (System.Exception e) { Plugin.Log.LogWarning("Couldn't save your custom FishMon settings: " + e.Message); }
        }

        private static void LoadCustom()
        {
            try
            {
                if (!File.Exists(CustomPath)) return; // never had any: keep what the preset had
                var d = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(CustomPath));
                if (d == null) return;
                foreach (var e in Values.Keys)
                    if (d.TryGetValue(KeyOf(e), out var s)) e.SetSerializedValue(s);
            }
            catch (System.Exception e) { Plugin.Log.LogWarning("Couldn't load your custom FishMon settings: " + e.Message); }
        }

        // islands

        private static int _mainIslands;

        // how many main islands the game has (cached once the game's island list exists)
        public static int MainIslandCount
        {
            get
            {
                if (_mainIslands > 0) return _mainIslands;
                int n = 0;
                try
                {
                    var infos = Traverse.Create(typeof(IslandManager)).Field("_instance").Field("_islandInfos").GetValue<System.Array>();
                    if (infos != null) n = infos.Length;
                }
                catch { }
                if (n > 0)
                {
                    _mainIslands = n;
                    Plugin.Log.LogInfo($"FishMon: the game has {n} main islands.");
                }
                return Mathf.Max(1, n);
            }
        }

        // the main island everyone is on, from 0 (mini islands don't change it)
        public static int CurrentIsland
        {
            get
            {
                try { return OnlineIslandManager.Instance ? Mathf.Clamp(OnlineIslandManager.CurIsland, 0, MainIslandCount - 1) : 0; }
                catch { return 0; }
            }
        }

        public static string IslandText => $"Island {CurrentIsland + 1} of {MainIslandCount}";

        // main boss level on this island
        public static int IslandLevel
        {
            get
            {
                int n = MainIslandCount;
                float t = n <= 1 ? 0f : CurrentIsland / (float)(n - 1);
                return Mathf.RoundToInt(Mathf.Lerp(FirstIslandLevel, LastIslandLevel, t));
            }
        }

        public static int FoeLevel(bool mainBoss, bool boss)
        {
            int lvl;
            if (!Locked) lvl = mainBoss ? Plugin.BossLevel.Value : boss ? Plugin.MiniBossLevel.Value : Plugin.WildFoeLevel.Value;
            else
            {
                int main = IslandLevel;
                lvl = mainBoss ? main : boss ? Mathf.Max(5, Mathf.RoundToInt(main * 0.7f)) : Mathf.Max(3, Mathf.RoundToInt(main * 0.4f));
            }
            return Mathf.Clamp(lvl, 1, 100);
        }

        // levels a new catch can be
        public static void CatchRange(out int min, out int max)
        {
            if (!Locked)
            {
                min = Plugin.MinCatchLevel.Value;
                max = Mathf.Max(min, Plugin.MaxCatchLevel.Value);
                return;
            }
            min = 3;
            max = Mathf.Max(8, IslandLevel - 5);
        }
    }
}
