using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace HowToFishMon
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.howtofish.fishmon";
        public const string Name = "FishMon";
        public const string Version = "1.0.0";

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

        private static float _lastHpMult = 1f;

        // keep everyone's HP at the same percentage when the HP multiplier changes
        private static void RescaleHp()
        {
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

            // the mod menu changes values while you drag sliders; save once when it closes instead of every frame
            Config.SaveOnConfigSet = false;
            Config.Save();

            FishMonStorage.Load();
            _lastHpMult = FishHpMultiplier.Value;
            FishHpMultiplier.SettingChanged += (_, __) => RescaleHp();

            ApplyPatches();

            var go = new GameObject("FishMon");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<FishMonController>();

            Log.LogInfo($"{Name} {Version} loaded. {FishMonStorage.Party.Count} in party, {FishMonStorage.Box.Count} in box.");
        }
    }
}
