using System.Collections.Generic;

namespace HowToFishMon
{
    public class EvoStage
    {
        public string Name;
        public int Level;
        public MonType NewType1; // None = keep
        public MonType NewType2; // None = keep

        public EvoStage(string name, int level, MonType t2 = MonType.None, MonType t1 = MonType.None)
        {
            Name = name; Level = level; NewType2 = t2; NewType1 = t1;
        }
    }

    public static class Evolutions
    {
        // matched against the species' ORIGINAL name (lowercase "contains"), first match wins
        private static readonly (string key, EvoStage[] stages)[] Chains =
        {
            ("shrimp",    new[] { new EvoStage("Prawn", 20),            new EvoStage("Lobster", 40, MonType.Fighting) }),
            ("spider",    new[] { new EvoStage("Arachnocrab", 30),      new EvoStage("Widow Crab", 50, MonType.Poison) }),
            ("crab",      new[] { new EvoStage("King Crab", 25),        new EvoStage("Titan Crab", 45, MonType.Steel) }),
            ("cod",       new[] { new EvoStage("Big Cod", 20),          new EvoStage("The Codfather", 40, MonType.Dark) }),
            ("mackerel",  new[] { new EvoStage("Mackerelite", 22),      new EvoStage("Mackerel Monarch", 42) }),
            ("shark",     new[] { new EvoStage("Great Shark", 30),      new EvoStage("Megalodon", 50, MonType.Dragon) }),
            ("eel",       new[] { new EvoStage("Volt Eel", 25),         new EvoStage("Thunder Eel", 45) }),
            ("pike",      new[] { new EvoStage("Iron Pike", 28),        new EvoStage("Lance Pike", 48) }),
            ("seahorse",  new[] { new EvoStage("Sea Dragon", 30),       new EvoStage("Leviathan", 50) }),
            ("puffer",    new[] { new EvoStage("Spikefish", 28),        new EvoStage("Blowfish King", 48) }),
            ("piranha",   new[] { new EvoStage("Razorfin", 25),         new EvoStage("Frenzy Piranha", 45) }),
            ("trigger",   new[] { new EvoStage("Triggerclaw", 25),      new EvoStage("Hairtrigger", 45) }),
            ("gar",       new[] { new EvoStage("Steel Gar", 28),        new EvoStage("Gargantua", 48) }),
            ("sole",      new[] { new EvoStage("Sole Survivor", 26),    new EvoStage("Sole Sovereign", 46) }),
            ("drip",      new[] { new EvoStage("Drizzler", 25),         new EvoStage("Downpour", 45) }),
            ("whale",     new[] { new EvoStage("Blue Whale", 35),       new EvoStage("Lava Whale", 55, MonType.Fire) }),
        };

        public static EvoStage[] ChainFor(FishMon m) => ChainFor(m.BaseSpecies ?? m.SpeciesName ?? "");

        public static EvoStage[] ChainFor(string baseName)
        {
            string s = (baseName ?? "").ToLowerInvariant();
            foreach (var c in Chains)
                if (s.Contains(c.key)) return c.stages;
            return new[] { new EvoStage("Big " + baseName, 25), new EvoStage("Mega " + baseName, 45) };
        }

        // the next stage this FishMon can reach, or null if fully evolved
        public static EvoStage Next(FishMon m)
        {
            var chain = ChainFor(m);
            return m.Stage < chain.Length ? chain[m.Stage] : null;
        }
    }
}
