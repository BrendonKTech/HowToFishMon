using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HowToFishMon
{
    public enum BadgeTier { None, Bronze, Silver, Gold }

    public class Perk
    {
        public string Name;
        public string Desc;
        public float Share; // of all the badge points there are
    }

    public static class Badges
    {
        public static readonly Perk[] Perks =
        {
            new Perk { Name = "Quick Learner",  Desc = "+10% EXP",                    Share = 0.1f },
            new Perk { Name = "Bargain Healer", Desc = "Heal All costs half",         Share = 0.25f },
            new Perk { Name = "Boss Breaker",   Desc = "+10% damage to bosses",       Share = 0.4f },
            new Perk { Name = "Big Bag",        Desc = "+1 party slot",               Share = 0.6f },
            new Perk { Name = "Steady Hands",   Desc = "+5% catch chance",            Share = 0.8f },
            new Perk { Name = "Badge Master",   Desc = "another +10% EXP",            Share = 1f },
        };

        private static readonly Color Gold = new Color(1f, 0.85f, 0.4f);

        public static string Color(BadgeTier t)
        {
            switch (t)
            {
                case BadgeTier.Gold: return "#ffd84a";
                case BadgeTier.Silver: return "#d6dde8";
                case BadgeTier.Bronze: return "#e0a36b";
                default: return "#6f7890";
            }
        }

        // highest level any FishMon of yours may be for this tier
        public static int Cap(int bossLevel, BadgeTier t)
        {
            float share = t == BadgeTier.Gold ? 0.6f : t == BadgeTier.Silver ? 0.8f : 1f;
            return Mathf.Max(1, Mathf.RoundToInt(bossLevel * share));
        }

        public static BadgeTier TierFor(int bossLevel, int teamLevel)
        {
            if (teamLevel <= Cap(bossLevel, BadgeTier.Gold)) return BadgeTier.Gold;
            if (teamLevel <= Cap(bossLevel, BadgeTier.Silver)) return BadgeTier.Silver;
            if (teamLevel <= Cap(bossLevel, BadgeTier.Bronze)) return BadgeTier.Bronze;
            return BadgeTier.None;
        }

        // every boss in the game's journal
        public static List<Creature> Bosses => Fishdex.All.Where(c => c && c.BossType != BossType.None).ToList();

        private static int _bossCount;
        private static int BossCount
        {
            get
            {
                if (_bossCount <= 0 && Fishdex.All.Count > 0) _bossCount = Bosses.Count;
                return _bossCount;
            }
        }

        public static BadgeTier Earned(int bossId) =>
            FishMonStorage.BadgeTiers.TryGetValue(bossId, out int t) ? (BadgeTier)Mathf.Clamp(t, 0, 3) : BadgeTier.None;

        public static int Points => FishMonStorage.BadgeTiers.Values.Sum(t => Mathf.Clamp(t, 0, 3));
        public static int MaxPoints => Mathf.Max(3, BossCount * 3);
        public static int Needed(Perk p) => Mathf.Max(1, Mathf.CeilToInt(MaxPoints * p.Share));
        public static bool Has(Perk p) => BossCount > 0 && Points >= Needed(p);

        // perks
        public static float ExpBonus => 1f + (Has(Perks[0]) ? 0.1f : 0f) + (Has(Perks[5]) ? 0.1f : 0f);
        public static int HealAllCost(int cost) => Has(Perks[1]) ? cost / 2 : cost;
        public static float DamageBonus => Has(Perks[2]) ? 1.1f : 1f;
        public static int ExtraPartySlots => Has(Perks[3]) ? 1 : 0;
        public static float CatchBonus => Has(Perks[4]) ? 0.05f : 0f;

        // the boss fight going on right now
        private class Fight
        {
            public Creature Boss;
            public int Level;
            public bool Valid;   // started on the Challenge preset
            public readonly Dictionary<FishMon, int> Joined = new Dictionary<FishMon, int>(); // level when it first joined
            public bool Warned;
            public int TeamLevel => Joined.Count == 0 ? 0 : Joined.Values.Max();
        }

        private static Fight _fight;

        public static void OnBossSpawn(Creature boss)
        {
            _fight = boss ? new Fight { Boss = boss, Level = BossMon.From(boss).Level, Valid = Presets.Current == Preset.Challenge } : null;
            // already going for it
            var a = FishMonController.Active;
            if (_fight != null && a && a.Target != null && a.Target.Creature == boss) OnJoined(boss, a.Mon);
        }

        public static void OnBossGone() => _fight = null;

        // one of our FishMon went for the boss. Its level counts from the first time it joins, so levelling up
        // during the fight doesn't break the challenge, but sending in a stronger FishMon does
        public static void OnJoined(Creature target, FishMon m)
        {
            var f = _fight;
            if (f == null || f.Boss != target || m == null || f.Joined.ContainsKey(m)) return;
            f.Joined[m] = m.Level;
            if (f.Valid && !f.Warned && m.Level > Cap(f.Level, BadgeTier.Bronze))
            {
                f.Warned = true;
                FishMonController.ShowToast($"{m.DisplayName} (Lv. {m.Level}) is over the badge cap for this boss (Lv. {Cap(f.Level, BadgeTier.Bronze)}), so no badge this fight.",
                    new Color(1f, 0.65f, 0.5f), 5f);
            }
        }

        // fought = every FishMon of ours that fought this boss (in case one went for it before the spawn event came)
        public static void OnBossDefeated(Creature boss, IEnumerable<FishMon> fought)
        {
            var f = _fight;
            _fight = null;
            if (f == null || f.Boss != boss) return;
            foreach (var m in fought) if (m != null && !f.Joined.ContainsKey(m)) f.Joined[m] = m.Level;
            if (!f.Valid || f.Joined.Count == 0 || Presets.Current != Preset.Challenge) return;

            string name = Species.SafeName(boss);
            var tier = TierFor(f.Level, f.TeamLevel);
            if (tier == BadgeTier.None)
            {
                FishMonController.ShowToast($"No badge for {name}: your team was over Lv. {Cap(f.Level, BadgeTier.Bronze)}.", new Color(1f, 0.65f, 0.5f), 5f);
                return;
            }
            var old = Earned(boss.ID);
            if (tier <= old) return; // already had this one (or better)

            var perksBefore = Perks.Where(Has).ToList();
            FishMonStorage.SetBadge(boss.ID, (int)tier);
            FishMonController.ShowToast($"<color={Color(tier)}>{tier} badge</color> earned for beating {name} with a Lv. {f.TeamLevel} team!", Gold, 6f);
            foreach (var p in Perks.Where(Has).Except(perksBefore))
                FishMonController.ShowToast($"Perk unlocked: {p.Name} ({p.Desc})!", Gold, 6f);
            try { AudioManager.PlayGlobalClip("Confetti"); } catch { }
            Plugin.Log.LogInfo($"Badge: {tier} for {name} (boss Lv {f.Level}, team Lv {f.TeamLevel})");
        }

        // the line under the HUD during a boss fight on Challenge
        public static string HudLine()
        {
            var f = _fight;
            if (f == null || !f.Valid || !f.Boss || f.Boss.IsDead || Presets.Current != Preset.Challenge) return null;
            int team = f.TeamLevel;
            if (team == 0 && FishMonController.Active) team = FishMonController.Active.Mon.Level; // nobody's joined yet
            if (team == 0) return null;
            var tier = TierFor(f.Level, team);
            string caps = $"<color=#9aa6bd>Gold ≤ Lv.{Cap(f.Level, BadgeTier.Gold)} · Silver ≤ {Cap(f.Level, BadgeTier.Silver)} · Bronze ≤ {Cap(f.Level, BadgeTier.Bronze)}</color>";
            return tier == BadgeTier.None
                ? $"<color=#ff9a9a>Badge: team too strong (Lv. {team})</color>  {caps}"
                : $"Badge: <b><color={Color(tier)}>{tier}</color></b> on track  {caps}";
        }
    }
}
