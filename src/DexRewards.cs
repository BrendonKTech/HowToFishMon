using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HowToFishMon
{
    // how likely a fish is to be caught when you pick it up. Rare (valuable) fish and high levels break free more
    public static class Catching
    {
        // 0 = cheap common fish, 1 = very valuable fish ($1000+)
        public static float Rarity(Creature c) => c ? Mathf.Clamp01((Mathf.Log10(Mathf.Max(0, c.DefaultWorth) + 1f) - 1f) / 2f) : 0f;

        public static float Chance(Creature c, int level, bool lucky)
        {
            float difficulty = Plugin.CatchDifficulty.Value;
            if (difficulty <= 0f) return 1f;
            float chance = Mathf.Lerp(0.92f, 0.5f, Rarity(c));
            Presets.CatchRange(out int min, out int max);
            max = Mathf.Max(min + 1, max);
            chance -= 0.1f * Mathf.Clamp01((level - min) / (float)(max - min));
            if (lucky) chance += 0.1f;
            chance += Badges.CatchBonus;
            // the difficulty scales the chance to break free
            return Mathf.Clamp(1f - (1f - chance) * difficulty, 0.05f, 1f);
        }

        // before we know its level (hooked message, Fishdex page): assume a middling one
        public static float Estimate(Creature c, bool lucky)
        {
            Presets.CatchRange(out int min, out int max);
            return Chance(c, (min + max) / 2, lucky);
        }

        public static void BrokeFree(Creature c, float chance)
        {
            string name = Species.SafeName(c);
            Vector3 at = c.Rig ? c.Rig.worldCenterOfMass : c.transform.position;
            FishMonController.ShowToast($"Oh no! The {name} broke free! ({chance:P0} catch chance) It's just a normal fish now.", new Color(1f, 0.65f, 0.5f), 5f);
            Fx.Sound("Poof", at, 0.6f, AudioDistance.Short);
            Fx.Vfx("WaterSplash", at);
            Plugin.Log.LogInfo($"{name} broke free ({chance:P0} chance)");
        }
    }

    public class Milestone
    {
        public string Id;
        public string Name;
        public int Count;      // species caught, or...
        public float Percent;  // ...this share of the Fishdex
        public int Money;
        public (HeldItem item, int count)[] Items = new (HeldItem, int)[0];
    }

    // rewards for filling in the Fishdex: a bit of money for every new species, and milestones to claim
    public static class DexRewards
    {
        public const int MaxReward = 10000; // the host never hands out more than this at once

        public static readonly Milestone[] All =
        {
            new Milestone { Id = "c5",   Name = "Rookie Angler",  Count = 5,       Money = 150,  Items = new[] { (HeldItem.SitrusBerry, 2) } },
            new Milestone { Id = "c10",  Name = "Keen Angler",    Count = 10,      Money = 300,  Items = new[] { (HeldItem.LumBerry, 2), (HeldItem.FocusSash, 1) } },
            new Milestone { Id = "p25",  Name = "Fish Collector", Percent = 0.25f, Money = 500,  Items = new[] { (HeldItem.ScopeLens, 1) } },
            new Milestone { Id = "p50",  Name = "Fish Expert",    Percent = 0.5f,  Money = 1000, Items = new[] { (HeldItem.LuckyEgg, 1) } },
            new Milestone { Id = "p75",  Name = "Fish Professor", Percent = 0.75f, Money = 1500, Items = new[] { (HeldItem.Leftovers, 1) } },
            new Milestone { Id = "p100", Name = "Fishdex Master", Percent = 1f,    Money = 3000, Items = new[] { (HeldItem.ChoiceBand, 1), (HeldItem.ChoiceSpecs, 1), (HeldItem.ExpertBelt, 1) } },
        };

        private static readonly Color Gold = new Color(1f, 0.85f, 0.4f);

        // species you need to have caught
        public static int Needed(Milestone m)
        {
            int total = Fishdex.Total;
            if (m.Percent > 0f) return Mathf.Max(1, Mathf.CeilToInt(total * m.Percent));
            return total > 0 ? Mathf.Min(m.Count, total) : m.Count;
        }

        public static List<Milestone> Sorted => All.OrderBy(Needed).ToList();
        public static bool Reached(Milestone m) => Fishdex.Total > 0 && Fishdex.CaughtCount >= Needed(m);
        public static bool Claimed(Milestone m) => FishMonStorage.IsClaimed(m.Id);
        public static int ReadyCount => All.Count(m => Reached(m) && !Claimed(m));

        public static int MoneyFor(int baseAmount) => Mathf.Max(0, Mathf.RoundToInt(baseAmount * Plugin.DexRewardMultiplier.Value));

        // the bonus for the first catch of a species: more for rarer fish
        public static int NewSpeciesBonus(Creature c) => MoneyFor(Mathf.RoundToInt(25f + 125f * Catching.Rarity(c)));

        public static string ItemsText(Milestone m) =>
            string.Join(", ", m.Items.Select(i => (i.count > 1 ? i.count + "× " : "") + (Items.Get(i.item)?.Name ?? i.item.ToString())));

        // a new species just went in the Fishdex as caught. readyBefore = milestones that were ready before it
        public static void OnNewSpecies(Creature c, int readyBefore)
        {
            int bonus = NewSpeciesBonus(c);
            if (bonus > 0 && GiveMoney(bonus))
                FishMonController.ShowToast($"New species bonus: +${bonus}!", Gold, 4f);
            if (ReadyCount > readyBefore)
                FishMonController.ShowToast($"Fishdex milestone reached! Claim your reward: {Plugin.KeyName(Plugin.PartyKey)} → Fishdex → Rewards.", Gold, 6f);
        }

        public static void Claim(Milestone m)
        {
            if (!Reached(m) || Claimed(m)) return;
            int money = MoneyFor(m.Money);
            if (money > 0 && !GiveMoney(money))
            {
                FishMonController.ShowToast("Couldn't reach the host to hand out the money. Try again in a moment.", new Color(1f, 0.6f, 0.6f));
                return;
            }
            FishMonStorage.MarkClaimed(m.Id);
            foreach (var (item, count) in m.Items) FishMonStorage.AddToBag(item, count);
            string got = money > 0 ? $"${money}" : "";
            if (m.Items.Length > 0) got += (got.Length > 0 ? " and " : "") + ItemsText(m);
            FishMonController.ShowToast($"{m.Name} reward: {got}! Items go in your bag (pick a FishMon → Item tab).", Gold, 6f);
            try { AudioManager.PlayGlobalClip("Confetti"); } catch { }
            Plugin.Log.LogInfo($"Claimed Fishdex reward {m.Id} ({got})");
        }

        // money goes into the team's pot. Only the host can add money, so guests ask it to
        public static bool GiveMoney(int amount)
        {
            if (amount <= 0) return true;
            var p = Player.LocalPlayer;
            var mm = MoneyManager.Instance;
            if (!p || !mm) return false;
            try
            {
                if (mm.IsServerInitialized) { MoneyManager.AddMoney(amount, p); return true; }
                return Net.SendReward(amount);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("FishMon couldn't give reward money: " + e.Message);
                return false;
            }
        }

        // host: a guest claimed a reward
        public static void GiveOnServer(Player p, int amount)
        {
            if (!p || amount <= 0 || !MoneyManager.Instance || !MoneyManager.Instance.IsServerInitialized) return;
            MoneyManager.AddMoney(Mathf.Min(amount, MaxReward), p);
        }
    }
}
