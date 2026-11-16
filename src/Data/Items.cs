using System.Linq;

namespace HowToFishMon
{
    // items a FishMon can hold (one each). Saved by name, so the order here can change freely.
    public enum HeldItem
    {
        None,
        LuckyEgg,
        Leftovers,
        SitrusBerry,
        LumBerry,
        FocusSash,
        ChoiceBand,
        ChoiceSpecs,
        QuickClaw,
        ScopeLens,
        ExpertBelt,
    }

    public class ItemInfo
    {
        public HeldItem Id;
        public string Name;
        public int Price;
        public string Desc;
        public bool OneUse;   // used up when it triggers (berries, Focus Sash)
    }

    public static class Items
    {
        // tuning numbers, so the descriptions and the code can't drift apart
        public const float LuckyEggExp = 1.5f;          // +50% EXP
        public const float LeftoversHeal = 1f / 16f;    // of max HP...
        public const float LeftoversEvery = 5f;         // ...every this many seconds while out
        public const float SitrusBelow = 0.5f;          // eats it below half HP...
        public const float SitrusHeal = 0.25f;          // ...healing a quarter
        public const float ChoiceBoost = 1.3f;          // +30% physical (Band) / special (Specs) damage
        public const float QuickClawRate = 1.2f;        // attacks 20% more often
        public const int ScopeLensCritOdds = 8;         // 1 in 8 crits instead of 1 in 16
        public const float ExpertBeltBoost = 1.2f;      // +20% on super-effective hits

        public static readonly ItemInfo[] All =
        {
            new ItemInfo { Id = HeldItem.LuckyEgg,    Name = "Lucky Egg",    Price = 600, Desc = "Gains 50% more EXP." },
            new ItemInfo { Id = HeldItem.Leftovers,   Name = "Leftovers",    Price = 500, Desc = "Heals 1/16 of its HP every 5 seconds while it's out." },
            new ItemInfo { Id = HeldItem.ChoiceBand,  Name = "Choice Band",  Price = 450, Desc = "Physical moves do 30% more damage." },
            new ItemInfo { Id = HeldItem.ChoiceSpecs, Name = "Choice Specs", Price = 450, Desc = "Special moves do 30% more damage." },
            new ItemInfo { Id = HeldItem.QuickClaw,   Name = "Quick Claw",   Price = 350, Desc = "Attacks 20% more often." },
            new ItemInfo { Id = HeldItem.ExpertBelt,  Name = "Expert Belt",  Price = 350, Desc = "Super-effective hits do 20% more damage." },
            new ItemInfo { Id = HeldItem.ScopeLens,   Name = "Scope Lens",   Price = 300, Desc = "Lands critical hits twice as often." },
            new ItemInfo { Id = HeldItem.FocusSash,   Name = "Focus Sash",   Price = 150, OneUse = true, Desc = "One use: at full HP, survives a hit that would knock it out." },
            new ItemInfo { Id = HeldItem.SitrusBerry, Name = "Sitrus Berry", Price = 60,  OneUse = true, Desc = "One use: heals 25% of its HP when it drops below half." },
            new ItemInfo { Id = HeldItem.LumBerry,    Name = "Lum Berry",    Price = 50,  OneUse = true, Desc = "One use: cures a status condition (burn, sleep...)." },
        };

        public static ItemInfo Get(HeldItem i) => All.FirstOrDefault(x => x.Id == i);

        public static int PriceOf(ItemInfo i) => UnityEngine.Mathf.Max(0, UnityEngine.Mathf.RoundToInt(i.Price * Plugin.ItemPriceMultiplier.Value));
    }
}
