using System.Linq;

namespace HowToFishMon
{
    // every FishMon rolls one ability when it's caught. Saved by name, so the order here can change freely.
    public enum Ability
    {
        None,
        Merchant,        // sells fish you drop
        Angler,          // helps you fish
        TreasureHunter,  // digs up random loot
        Guard,           // protects you
        Medic,           // heals you
        Scholar,         // more EXP
        ThickScales,     // takes less damage
        Speedster,       // moves and attacks faster
        Berserker,       // hits harder at low HP
        Mentor,          // benched party members share EXP
        LuckyCharm,      // better catches while it's out
        Leech,           // heals itself when it hits
        Haggler,         // fish you sell are worth more
    }

    public enum Rarity { Common, Uncommon, Rare, Legendary }

    public class AbilityInfo
    {
        public Ability Id;
        public string Name;
        public Rarity Rarity;
        public string Desc;
        public bool Implemented;
    }

    public static class Abilities
    {
        public static readonly AbilityInfo[] All =
        {
            new AbilityInfo { Id = Ability.Merchant,       Name = "Merchant",        Rarity = Rarity.Common,    Implemented = true,
                              Desc = "Picks up fish you drop near it and sells them for you." },
            new AbilityInfo { Id = Ability.Scholar,        Name = "Scholar",         Rarity = Rarity.Common,    Desc = "Gains 25% more EXP." },
            new AbilityInfo { Id = Ability.ThickScales,    Name = "Thick Scales",    Rarity = Rarity.Common,    Desc = "Takes 20% less damage." },
            new AbilityInfo { Id = Ability.Speedster,      Name = "Speedster",       Rarity = Rarity.Common,    Desc = "Moves and attacks 25% faster." },
            new AbilityInfo { Id = Ability.Guard,          Name = "Guard",           Rarity = Rarity.Uncommon,  Desc = "Stays close and goes after anything that hurts you." },
            new AbilityInfo { Id = Ability.Angler,         Name = "Angler",          Rarity = Rarity.Uncommon,  Desc = "Helps you fish: bites come faster." },
            new AbilityInfo { Id = Ability.Berserker,      Name = "Berserker",       Rarity = Rarity.Uncommon,  Desc = "Hits up to 50% harder the lower its HP is." },
            new AbilityInfo { Id = Ability.Mentor,         Name = "Mentor",          Rarity = Rarity.Uncommon,  Desc = "Party members on the bench get a share of its EXP." },
            new AbilityInfo { Id = Ability.Medic,          Name = "Medic",           Rarity = Rarity.Rare,      Desc = "Slowly heals you while it's out." },
            new AbilityInfo { Id = Ability.TreasureHunter, Name = "Treasure Hunter", Rarity = Rarity.Rare,      Desc = "Now and then digs up random loot." },
            new AbilityInfo { Id = Ability.Leech,          Name = "Leech",           Rarity = Rarity.Rare,      Desc = "Heals itself for part of the damage it deals." },
            new AbilityInfo { Id = Ability.Haggler,        Name = "Haggler",         Rarity = Rarity.Rare,      Desc = "Fish you sell while it's out are worth 25% more." },
            new AbilityInfo { Id = Ability.LuckyCharm,     Name = "Lucky Charm",     Rarity = Rarity.Legendary, Desc = "Fish you catch while it's out get better IVs and levels." },
        };

        public static AbilityInfo Get(Ability a) => All.FirstOrDefault(i => i.Id == a);

        public static string RarityColor(Rarity r)
        {
            switch (r)
            {
                case Rarity.Uncommon: return "#7ee08a";
                case Rarity.Rare: return "#6fb6ff";
                case Rarity.Legendary: return "#ffb347";
                default: return "#d8dde8";
            }
        }

        // chance of each rarity, out of 100. Drip fish roll one tier higher on average.
        private static Rarity RollRarity(System.Random rng, bool shiny)
        {
            int r = rng.Next(100);
            if (shiny) r = System.Math.Min(99, r + 25);
            if (r < 55) return Rarity.Common;
            if (r < 85) return Rarity.Uncommon;
            if (r < 97) return Rarity.Rare;
            return Rarity.Legendary;
        }

        public static Ability Roll(System.Random rng, bool shiny, Ability not = Ability.None)
        {
            for (int tries = 0; tries < 20; tries++)
            {
                var rarity = RollRarity(rng, shiny);
                var pool = All.Where(i => i.Rarity == rarity && i.Id != not).ToArray();
                if (pool.Length > 0) return pool[rng.Next(pool.Length)].Id;
            }
            return Ability.Merchant;
        }
    }
}
