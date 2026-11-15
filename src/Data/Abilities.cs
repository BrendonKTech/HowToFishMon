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
        // tuning numbers, so the descriptions and the code can't drift apart
        public const float ScholarExp = 1.25f;        // +25% EXP
        public const float ThickScalesTaken = 0.8f;   // -20% damage taken
        public const float SpeedsterSpeed = 1.25f;    // +25% move and attack speed
        public const float BerserkerMax = 0.5f;       // up to +50% damage at 0 HP
        public const float MentorShare = 0.3f;        // benched party members get 30% of its EXP
        public const float LeechHeal = 0.15f;         // heals 15% of the damage it deals
        public const float HagglerBonus = 0.25f;      // +25% sell value
        public const float AnglerRate = 1.5f;         // bite timer runs 50% faster
        public const float GuardRoamRadius = 3.5f;    // stays this close while roaming

        public static readonly AbilityInfo[] All =
        {
            new AbilityInfo { Id = Ability.Merchant,       Name = "Merchant",        Rarity = Rarity.Common,    Implemented = true,
                              Desc = "Picks up fish you drop near it and sells them for you." },
            new AbilityInfo { Id = Ability.Scholar,        Name = "Scholar",         Rarity = Rarity.Common,    Implemented = true,
                              Desc = "Gains 25% more EXP." },
            new AbilityInfo { Id = Ability.ThickScales,    Name = "Thick Scales",    Rarity = Rarity.Common,    Implemented = true,
                              Desc = "Takes 20% less damage." },
            new AbilityInfo { Id = Ability.Speedster,      Name = "Speedster",       Rarity = Rarity.Common,    Implemented = true,
                              Desc = "Moves and attacks 25% faster." },
            new AbilityInfo { Id = Ability.Guard,          Name = "Guard",           Rarity = Rarity.Uncommon,  Implemented = true,
                              Desc = "Stays close and goes after anything that hurts you." },
            new AbilityInfo { Id = Ability.Angler,         Name = "Angler",          Rarity = Rarity.Uncommon,  Implemented = true,
                              Desc = "Helps you fish: bites come a third sooner." },
            new AbilityInfo { Id = Ability.Berserker,      Name = "Berserker",       Rarity = Rarity.Uncommon,  Implemented = true,
                              Desc = "Hits up to 50% harder the lower its HP is." },
            new AbilityInfo { Id = Ability.Mentor,         Name = "Mentor",          Rarity = Rarity.Uncommon,  Implemented = true,
                              Desc = "Party members on the bench get 30% of the EXP it earns." },
            new AbilityInfo { Id = Ability.Medic,          Name = "Medic",           Rarity = Rarity.Rare,      Implemented = true,
                              Desc = "Slowly heals you while it's out." },
            new AbilityInfo { Id = Ability.TreasureHunter, Name = "Treasure Hunter", Rarity = Rarity.Rare,      Implemented = true,
                              Desc = "Every few minutes digs up a dead fish or some cash." },
            new AbilityInfo { Id = Ability.Leech,          Name = "Leech",           Rarity = Rarity.Rare,      Implemented = true,
                              Desc = "Heals itself for 15% of the damage it deals." },
            new AbilityInfo { Id = Ability.Haggler,        Name = "Haggler",         Rarity = Rarity.Rare,      Implemented = true,
                              Desc = "Fish you sell while it's out are worth 25% more." },
            new AbilityInfo { Id = Ability.LuckyCharm,     Name = "Lucky Charm",     Rarity = Rarity.Legendary, Implemented = true,
                              Desc = "Fish you catch while it's out get better IVs and levels." },
        };

        // the ability of the FishMon this player has out right now (switched-off abilities count as none).
        // For other players this only works on the host, and only if they have the mod (they tell the host through Net).
        public static bool PlayerHas(Player p, Ability a)
        {
            if (!p) return false;
            if (p == Player.LocalPlayer)
            {
                var c = FishMonController.Active;
                return c && c.Mon.Has(a);
            }
            return p.Owner != null && Net.RemoteAbility(p.Owner.ClientId) == a;
        }

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
