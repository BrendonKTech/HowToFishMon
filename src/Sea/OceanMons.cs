using System.Collections.Generic;
using System.IO;
using HowToFishMon.Api;
using UnityEngine;

namespace HowToFishMon.Sea
{
    // Models come from fishmon-ocean.bundle (next to the DLL) first, then the built-in ones (Models.cs), named like the
    // species/stage in lowercase: coralit, reefguard... Anything without a model is a recoloured template.
    // Never change a Key once released.
    internal static class OceanMons
    {
        public const string BundleFile = "fishmon-ocean.bundle";
        private static AssetBundle _bundle;
        private static readonly List<string> Found = new List<string>();

        private static GameObject Model(string name)
        {
            string asset = name.Replace(" ", "").ToLowerInvariant();
            var go = _bundle != null ? _bundle.LoadAsset<GameObject>(asset) : null;
            if (go != null) { Found.Add(asset + " (bundle)"); return go; }
            go = Models.Get(asset);
            if (go != null) Found.Add(asset);
            return go;
        }

        private static void LoadBundle()
        {
            string path = Path.Combine(Path.GetDirectoryName(typeof(OceanMons).Assembly.Location) ?? "", BundleFile);
            if (!File.Exists(path)) return;
            _bundle = FishMonAPI.LoadBundle(path);
            if (_bundle != null) Plugin.Log.LogInfo($"Loaded {BundleFile}. It contains: {string.Join(", ", _bundle.GetAllAssetNames())}");
        }

        private static void Add(string key, string name, string desc, string template, Color tint,
                                MonType t1, MonType t2, int[] stats, int worth, float rarity, int health, EvoStage[] evos,
                                int[] islands, string baits, bool? hostile = null, float waterPull = 1f)
        {
            if (evos != null) foreach (var s in evos) s.Prefab = Model(s.Name);
            FishMonAPI.Register(new SpeciesDef
            {
                Prefab = Model(name),
                Texture = Models.Palette, // colours for the built-in models
                Key = "fishmon.ocean." + key,
                Name = name,
                Description = desc,
                Template = template,
                Tint = tint,
                Type1 = t1,
                Type2 = t2,
                BaseStats = stats,
                Worth = worth,
                Rarity = rarity,
                Health = health,
                Evolutions = evos,
                Islands = islands,
                BaitFilter = baits,
                Hostile = hostile,
                WaterPull = waterPull,
                Endangered = false,
            });
        }

        public static void Register()
        {
            if (!Plugin.OceanSpecies.Value) { Plugin.Log.LogInfo("Ocean species are switched off in the config."); return; }
            LoadBundle();
            try { RegisterAll(); }
            finally { Plugin.Log.LogInfo($"Ocean models: {(Found.Count > 0 ? string.Join(", ", Found) : "none, using recoloured templates")}"); }
        }

        private static void RegisterAll()
        {
            // islands count from 1. Bait names are matched loosely (see the log for the full list).
            // Quest / special baits (Leech Bait, Crab Carcass, Carrot, the chum bucket, boss lures, Coconut, EmptyBeerCan) are never used, see BaitTables.
            //   key            name          blurb                                              template          tint                                  types                          HP  Atk Def SpA SpD Spe       worth rarity hp
            Add("coralit",     "Coralit",    "Grows a little reef on its back. Very stubborn.", "Yellow Boxfish", new Color(1.00f, 0.55f, 0.60f), MonType.Water, MonType.Rock,     new[] { 70, 60, 95, 40, 70, 30 },   40, 0.6f, 160,
                new[] { new EvoStage("Reefguard", 24), new EvoStage("Atollith", 44, MonType.Steel) },
                new[] { 1, 2, 3 }, "HotDog, Beginner Lure, Standard Lure");

            // key stays "glimmerjel" (its first name) so existing saves keep working
            Add("glimmerjel",  "Sparkfin",   "Its stripes crackle. Anything that touches it goes numb.", "Clownfish", new Color(0.55f, 0.95f, 1.00f), MonType.Water, MonType.Electric, new[] { 50, 40, 45, 80, 70, 75 },   60, 0.5f, 140,
                new[] { new EvoStage("Stormfin", 30) },
                new[] { 2, 3, 4 }, "Standard Lure, Professional Lure");

            Add("inkling",     "Inkling",    "Reads your mind and squirts ink at what it sees.", "Catfish",      new Color(0.60f, 0.45f, 0.95f), MonType.Water, MonType.Psychic,  new[] { 55, 40, 50, 75, 65, 65 },   70, 0.4f, 200,
                new[] { new EvoStage("Abyssquid", 28), new EvoStage("Krakenmind", 48, MonType.Dark) },
                new[] { 2, 3, 4, 5 }, "Professional Lure");

            Add("urchip",      "Urchip",     "Bites back. Don't grab it bare-handed.",         "Sea Urchin",     new Color(0.75f, 0.35f, 0.85f), MonType.Water, MonType.Poison,   new[] { 55, 70, 80, 40, 55, 35 },   55, 0.5f, 100,
                new[] { new EvoStage("Spinebrute", 32, MonType.Fighting) },
                new[] { 1, 2, 3, 4 }, "HotDog, Standard Lure");

            Add("driftray",    "Driftray",   "Glides through the water like it's flying.",      "Halibut",       new Color(0.60f, 0.85f, 0.70f), MonType.Water, MonType.Flying,   new[] { 60, 55, 50, 70, 75, 80 },  120, 0.35f, 300,
                new[] { new EvoStage("Mantagale", 34) },
                new[] { 3, 4, 5 }, "Professional Lure, Scientific Lure");

            Add("tidefang",    "Tidefang",   "Hunts in the rip current. Fast, mean, fragile.",  "Red Snapper",   new Color(0.35f, 0.40f, 0.55f), MonType.Water, MonType.Dark,     new[] { 50, 90, 45, 45, 45, 100 }, 150, 0.3f, 280,
                new[] { new EvoStage("Riptide", 36) },
                new[] { 3, 4, 5 }, "Professional Lure, Scientific Lure");

            Add("frostfin",    "Frostfin",   "Leaves a trail of ice crystals as it swims.",     "Mackerel",      new Color(0.80f, 0.95f, 1.00f), MonType.Water, MonType.Ice,      new[] { 55, 50, 50, 80, 70, 75 },   45, 0.6f, 60,
                new[] { new EvoStage("Glacifin", 26), new EvoStage("Floemaw", 46) },
                new[] { 1, 2, 3 }, "Beginner Lure, Standard Lure");

            Add("leviathorn",  "Leviathorn", "Sailors' stories call it the end of the sea.",    "Oarfish",       new Color(0.30f, 0.65f, 0.95f), MonType.Water, MonType.Dragon,   new[] { 100, 110, 90, 110, 90, 90 }, 2500, 0.03f, 1000,
                new EvoStage[0],
                new[] { 5 }, "Scientific Lure"); // legendary: never evolves

            Add("wavelet",     "Wavelet",    "Rides the bow wave of passing boats, chattering.", "Sengarat",      new Color(0.55f, 0.85f, 0.90f), MonType.Water, MonType.Fairy,    new[] { 65, 60, 60, 80, 85, 95 },  180, 0.35f, 350,
                new[] { new EvoStage("Tidesong", 32) },
                new[] { 2, 3, 4 }, "Standard Lure");

            Add("hullback",    "Hullback",   "So big and slow that barnacles move in for life. Do not poke it.", "Parrotfish",       new Color(0.30f, 0.38f, 0.55f), MonType.Water, MonType.Ground,   new[] { 130, 85, 110, 60, 80, 35 }, 900, 0.08f, 1000,
                new[] { new EvoStage("Barnaclad", 45, MonType.Rock) },
                new[] { 4, 5 }, "Scientific Lure, Professional Lure",
                waterPull: 0.25f); // hostile (Parrotfish template) and slow to slip back into the water, so you have to fight it

            Add("bonefin",     "Bonefin",    "A fish skeleton that never noticed it died. Its eyes still glow.", "Perch", new Color(0.85f, 0.85f, 0.80f), MonType.Water, MonType.Ghost, new[] { 60, 75, 65, 90, 75, 70 }, 220, 0.25f, 250,
                new[] { new EvoStage("Wraithmaw", 35, MonType.Dark) },
                new[] { 3, 4, 5 }, "Professional Lure");

            Add("cindergill",  "Cindergill", "Lives by undersea vents. The cracks in its scales glow with heat.", "Bluegill", new Color(0.35f, 0.30f, 0.30f), MonType.Fire, MonType.Water, new[] { 65, 85, 60, 90, 60, 70 }, 200, 0.3f, 220,
                new[] { new EvoStage("Scorchgill", 34) },
                new[] { 4, 5 }, "Scientific Lure, Professional Lure");

            Add("kelpling",    "Kelpling",   "Kelp took root on its back, and it decided that was fine.", "Goldfish", new Color(0.45f, 0.75f, 0.40f), MonType.Grass, MonType.Water, new[] { 70, 55, 70, 65, 80, 50 }, 35, 0.6f, 120,
                new[] { new EvoStage("Kelpwarden", 28) },
                new[] { 1, 2, 3 }, "Beginner Lure, HotDog");

            Add("shellmite",   "Shellmite",  "Scuttles along the seabed eating whatever sinks.", "RockCrab", new Color(0.65f, 0.58f, 0.75f), MonType.Bug, MonType.Water, new[] { 50, 70, 90, 35, 55, 45 }, 25, 0.6f, 80,
                new[] { new EvoStage("Carapod", 22, MonType.Rock) },
                new[] { 1, 2, 3, 4 }, "HotDog");
        }
    }
}
