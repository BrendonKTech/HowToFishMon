using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace HowToFishMon
{
    [BepInPlugin(Guid, Name, Version)]
    // if installed, load after MoreFishAPI so its fish take their ids first
    [BepInDependency("dazed.howtofish.morefishapi", BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.howtofish.fishmon";
        public const string Name = "FishMon";
        public const string Version = "1.3.1";

        public static Plugin Instance { get; private set; }
        public static ManualLogSource Log { get; private set; }

        // keys
        public static ConfigEntry<KeyCode> PartyKey;
        public static ConfigEntry<KeyCode> SummonKey;
        public static ConfigEntry<KeyCode> SwapKey;
        public static ConfigEntry<KeyCode> AttackKey;
        public static ConfigEntry<KeyCode> StayKey;
        public static ConfigEntry<KeyCode> ModMenuKey;
        public static ConfigEntry<KeyCode>[] KeyBindings;
        public static ConfigEntry<float> FishHpMultiplier;
        public static ConfigEntry<bool> AttackEffects;

        // readable name for a key: Alpha1 -> 1, Mouse3 -> Mouse 4, LeftShift -> Left Shift
        public static string KeyName(KeyCode k)
        {
            if (k >= KeyCode.Alpha0 && k <= KeyCode.Alpha9) return ((int)(k - KeyCode.Alpha0)).ToString();
            if (k >= KeyCode.Keypad0 && k <= KeyCode.Keypad9) return "Numpad " + (int)(k - KeyCode.Keypad0);
            if (k >= KeyCode.Mouse0 && k <= KeyCode.Mouse6) return "Mouse " + (int)(k - KeyCode.Mouse0 + 1);
            switch (k)
            {
                case KeyCode.None: return "(none)";
                case KeyCode.BackQuote: return "`";
                case KeyCode.Minus: return "-";
                case KeyCode.Equals: return "=";
                case KeyCode.LeftBracket: return "[";
                case KeyCode.RightBracket: return "]";
                case KeyCode.Semicolon: return ";";
                case KeyCode.Quote: return "'";
                case KeyCode.Comma: return ",";
                case KeyCode.Period: return ".";
                case KeyCode.Slash: return "/";
                case KeyCode.Backslash: return "\\";
            }
            return System.Text.RegularExpressions.Regex.Replace(k.ToString(), "(?<=[a-z])(?=[A-Z])", " ");
        }

        public static string KeyName(ConfigEntry<KeyCode> e) => KeyName(e.Value);

        // catching
        public static ConfigEntry<bool> OnlyCatchHooked;
        public static ConfigEntry<int> MinCatchLevel;
        public static ConfigEntry<int> MaxCatchLevel;
        public static ConfigEntry<int> MaxPartySize;
        public static ConfigEntry<float> CatchDifficulty;
        public static ConfigEntry<float> DexRewardMultiplier;
        public static ConfigEntry<Preset> SettingsPreset;

        public static ConfigEntry<bool> OceanSpecies;
        public static ConfigEntry<float> CustomFishRarity;
        public static ConfigEntry<bool> CustomFishInJournal;
        public static ConfigEntry<float> CustomFishBrightness;

        // battle
        public static ConfigEntry<int> MiniBossLevel;
        public static ConfigEntry<int> BossLevel;
        public static ConfigEntry<float> DamageToBossMultiplier;
        public static ConfigEntry<float> BossDamageToFishMultiplier;
        public static ConfigEntry<float> ExpMultiplier;
        public static ConfigEntry<float> AttackSpeedMultiplier;
        public static ConfigEntry<float> AggroRange;
        public static ConfigEntry<bool> FightWildCreatures;
        public static ConfigEntry<float> WildAggroRange;
        public static ConfigEntry<int> WildFoeLevel;
        public static ConfigEntry<bool> AutoSendNext;
        public static ConfigEntry<float> CompanionScale;
        public static ConfigEntry<bool> RoamAroundYou;
        public static ConfigEntry<float> RoamRadius;

        // healing
        public static ConfigEntry<float> RegenPercentPerMinute;
        public static ConfigEntry<int> HealAllCost;
        public static ConfigEntry<int> ReviveCost;
        public static ConfigEntry<float> ReviveHpPercent;
        public static ConfigEntry<int> AbilityRerollCost;
        public static ConfigEntry<float> ItemPriceMultiplier;

        private static float _lastHpMult = 1f;

        // set while LobbySettings briefly swaps your own values back in to save them: not a real change
        internal static bool QuietSettings;

        // keep everyone's HP at the same percentage when the HP multiplier changes
        private static void RescaleHp()
        {
            if (QuietSettings) return;
            float now = Mathf.Max(0.05f, FishHpMultiplier.Value), was = Mathf.Max(0.05f, _lastHpMult);
            _lastHpMult = now;
            foreach (var m in FishMonStorage.Party) Rescale(m, now / was);
            foreach (var m in FishMonStorage.Box) Rescale(m, now / was);
        }

        internal static void Rescale(FishMon m, float k)
        {
            if (m.CurrentHp <= 0) return; // fainted stays fainted
            m.CurrentHp = Mathf.Clamp(Mathf.RoundToInt(m.CurrentHp * k), 1, m.MaxHp);
        }

        // patch one class at a time instead of PatchAll, so if a game update renames a method
        // only that feature breaks (with a warning in the log) instead of the whole mod not loading
        private static void ApplyPatches()
        {
            var harmony = new Harmony(Guid);
            int ok = 0, failed = 0;
            foreach (var type in AccessTools.GetTypesFromAssembly(typeof(Plugin).Assembly))
            {
                if (type.GetCustomAttributes(typeof(HarmonyPatch), true).Length == 0) continue;
                try
                {
                    harmony.CreateClassProcessor(type).Patch();
                    ok++;
                }
                catch (System.Exception e)
                {
                    failed++;
                    Log.LogWarning($"Couldn't apply {type.Name}, that part of FishMon won't work. The game probably updated. " +
                                   $"({e.GetBaseException().Message})");
                }
            }
            if (failed > 0) Log.LogWarning($"{failed} FishMon patch(es) failed, {ok} worked.");
        }

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            PartyKey = Config.Bind("Keys", "OpenParty", KeyCode.F6, "Open / close your FishMon party and storage box.");
            SummonKey = Config.Bind("Keys", "SendOutRecall", KeyCode.F7, "Send out / recall your lead FishMon. It follows you and attacks bosses on its own.");
            SwapKey = Config.Bind("Keys", "SwapFishMon", KeyCode.F8, "Swap the FishMon that's out for the next healthy one in your party.");
            AttackKey = Config.Bind("Keys", "CommandAttack", KeyCode.G, "Order your FishMon to attack the creature you're looking at. Look at nothing to call it back to you.");
            ModMenuKey = Config.Bind("Keys", "ModMenu", KeyCode.F9, "Open / close the FishMon mod menu (EXP rate, HP, damage, levels, costs...).");
            StayKey = Config.Bind("Keys", "CommandStayFollow", KeyCode.H, "Toggle: tell your FishMon to stay put (it only fights what you order) / follow you again.");
            KeyBindings = new[] { PartyKey, SummonKey, SwapKey, AttackKey, StayKey, ModMenuKey };

            OnlyCatchHooked = Config.Bind("Catching", "OnlyCatchHooked", true, "Only fish you hooked with your own rod count as a catch when you pick them up (bought / found fish don't count). Set false to catch any creature you pick up.");
            MinCatchLevel = Config.Bind("Catching", "MinCatchLevel", 3, "Lowest level a freshly caught FishMon can be.");
            MaxCatchLevel = Config.Bind("Catching", "MaxCatchLevel", 35, "Highest level a freshly caught FishMon can be.");
            MaxPartySize = Config.Bind("Catching", "MaxPartySize", 6, "How many FishMon you can carry. Extra catches go to the Box.");
            CatchDifficulty = Config.Bind("Catching", "CatchDifficulty", 1f, "How often fish break free when you try to catch them. 0 = always catch, 1 = normal (common fish ~90%, rare fish ~50%), 2 = they break free twice as often.");
            DexRewardMultiplier = Config.Bind("Fishdex", "RewardMoneyMultiplier", 1f, "Scales the money from Fishdex milestones and the new-species bonus. 0 = no money (milestone items are still given).");

            MiniBossLevel = Config.Bind("Battle", "MiniBossLevel", 25, "Level of mini bosses in FishMon battles.");
            BossLevel = Config.Bind("Battle", "BossLevel", 45, "Level of main bosses in FishMon battles.");
            FishHpMultiplier = Config.Bind("Battle", "FishHpMultiplier", 2f, "Multiplies your FishMon's max HP. Higher = they survive more boss hits.");
            DamageToBossMultiplier = Config.Bind("Battle", "DamageToBossMultiplier", 1f, "Scales the real damage your FishMon deal to bosses.");
            BossDamageToFishMultiplier = Config.Bind("Battle", "BossDamageToFishMultiplier", 1f, "Scales how hard bosses hit your FishMon.");
            ExpMultiplier = Config.Bind("Battle", "ExpMultiplier", 1f, "Scales experience gained.");
            AttackSpeedMultiplier = Config.Bind("Battle", "AttackSpeedMultiplier", 1f, "Higher = your FishMon leap at bosses more often. (Base rate comes from its Speed stat.)");
            AggroRange = Config.Bind("Battle", "AggroRange", 60f, "Your FishMon attacks a boss when the boss is within this many metres of YOU.");
            FightWildCreatures = Config.Bind("Battle", "FightWildCreatures", true, "Also attack hostile non-boss creatures near you (crabs, piranhas and other fish that attack you).");
            WildAggroRange = Config.Bind("Battle", "WildAggroRange", 20f, "How close (to YOU) a hostile non-boss creature must be before your FishMon goes for it.");
            WildFoeLevel = Config.Bind("Battle", "WildFoeLevel", 15, "Battle level of hostile non-boss creatures.");
            AutoSendNext = Config.Bind("Battle", "AutoSendNext", true, "When your FishMon faints, automatically send out the next healthy one.");
            RoamAroundYou = Config.Bind("Following", "RoamAroundYou", true, "Your FishMon wanders around near you, mostly where you're looking, instead of trailing right behind you.");
            RoamRadius = Config.Bind("Following", "RoamRadius", 7f, "How far (metres) from you a roaming FishMon may wander.");
            CompanionScale = Config.Bind("Visuals", "CompanionScale", 1f, "Size multiplier for FishMon out in the world.");
            AttackEffects = Config.Bind("Visuals", "AttackEffects", true, "Type-themed lights, splashes, lightning, lava, trails and sounds on attacks.");

            RegenPercentPerMinute = Config.Bind("Healing", "RegenPercentPerMinute", 10f, "Percent of max HP your FishMon recover each minute while not battling. Fainted FishMon never regenerate; they need a Revive.");
            HealAllCost = Config.Bind("Healing", "HealAllCost", 25, "Money cost of the instant 'Heal All' button. It only heals FishMon that haven't fainted.");
            ReviveCost = Config.Bind("Healing", "ReviveCost", 100, "Money cost to revive one fainted FishMon (party menu). 0 = free.");
            ReviveHpPercent = Config.Bind("Healing", "ReviveHpPercent", 50f, "How much HP (percent of max) a revived FishMon comes back with.");
            AbilityRerollCost = Config.Bind("Abilities", "AbilityRerollCost", 250, "Money cost to reroll a FishMon's ability in the party menu. 0 = free.");
            ItemPriceMultiplier = Config.Bind("Items", "ItemPriceMultiplier", 1f, "Scales the price of every held item in the shop. 0 = free.");

            // presets: updating from a version without them? (decides whether you start on Normal or Custom)
            bool firstPresetRun = true;
            try { firstPresetRun = !System.IO.File.Exists(Config.ConfigFilePath) || !System.IO.File.ReadAllText(Config.ConfigFilePath).Contains("Preset = "); } catch { }
            SettingsPreset = Config.Bind("General", "Preset", Preset.Normal,
                "Normal / Challenge set the balance settings for you (they're locked) and make enemies and catches stronger island by island. " +
                "Custom lets you change everything. Boss badges can only be earned on Challenge. Change it in the Mod Menu.");

            OceanSpecies = Config.Bind("Custom species", "OceanSpecies", true, "Add FishMon's own ocean species (Coralit, Glimmerjel, Leviathorn...) to the game. Everyone in a lobby needs the same setting. Restart the game after changing it.");
            CustomFishRarity = Config.Bind("Custom species", "Rarity", 1f, "How often custom species bite compared to normal. 0 = they never bite. Restart the game after changing it.");
            CustomFishInJournal = Config.Bind("Custom species", "InJournal", true, "List custom species in the game's journal (they count towards its achievements). Restart the game after changing it.");
            CustomFishBrightness = Config.Bind("Custom species", "Brightness", 0.85f, "How bright custom species' colours are. 1 = full, lower = dimmer. Restart the game after changing it.");

            // the mod menu changes values while you drag sliders; save once when it closes instead of every frame
            Config.SaveOnConfigSet = false;
            Presets.Init(firstPresetRun);
            Config.Save();

            // species must be registered in Awake
            Sea.OceanMons.Register();
            Api.FishMonAPI.OnBuilt += () => { Fishdex.Invalidate(); FishMonStorage.RelinkCustomSpecies(); };

            FishMonStorage.Load();
            _lastHpMult = FishHpMultiplier.Value;
            FishHpMultiplier.SettingChanged += (_, __) => RescaleHp();

            ApplyPatches();

            var go = new GameObject("FishMon");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<FishMonController>();
            go.AddComponent<Api.SpeciesBuilder>();

            Log.LogInfo($"{Name} {Version} loaded. {FishMonStorage.Party.Count} in party, {FishMonStorage.Box.Count} in box.");
        }
    }
}
