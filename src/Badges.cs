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
            public int BossId;
            public string Name;
            public int Level;
            public bool Valid;   // started on the Challenge preset
            public readonly Dictionary<FishMon, int> Joined = new Dictionary<FishMon, int>(); // level when it first joined
            public bool Warned;
            public int TeamLevel => Joined.Count == 0 ? 0 : Joined.Values.Max();
        }

        private static Fight _fight;

        public static void OnBossSpawn(Creature boss)
        {
            _fight = boss ? new Fight { Boss = boss, BossId = boss.ID, Name = Species.SafeName(boss), Level = BossMon.From(boss).Level, Valid = Presets.Current == Preset.Challenge } : null;
            if (_fight != null) Plugin.Log.LogInfo($"Badge fight: {_fight.Name} Lv {_fight.Level}, {(_fight.Valid ? "counts (Challenge)" : $"doesn't count ({Presets.Current} preset)")}.");
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
            if (f == null) { Plugin.Log.LogInfo("Badge: a boss died, but no boss fight was being tracked."); return; }
            // by reference: the boss object may already be destroyed (Unity's == would say null)
            if (!ReferenceEquals(boss, null) && !ReferenceEquals(boss, f.Boss)) { Plugin.Log.LogInfo($"Badge: {Species.SafeName(boss)} died, but the tracked fight was {f.Name}."); return; }
            foreach (var m in fought) if (m != null && !f.Joined.ContainsKey(m)) f.Joined[m] = m.Level;
            if (!f.Valid || Presets.Current != Preset.Challenge) { Plugin.Log.LogInfo($"Badge: none for {f.Name}, not on the Challenge preset."); return; }
            if (f.Joined.Count == 0)
            {
                FishMonController.ShowToast($"No badge for {f.Name}: none of your FishMon fought it.", new Color(1f, 0.65f, 0.5f), 5f);
                return;
            }

            string name = f.Name;
            var tier = TierFor(f.Level, f.TeamLevel);
            if (tier == BadgeTier.None)
            {
                FishMonController.ShowToast($"No badge for {name}: your team was over Lv. {Cap(f.Level, BadgeTier.Bronze)}.", new Color(1f, 0.65f, 0.5f), 5f);
                return;
            }
            var old = Earned(f.BossId);
            if (tier <= old)
            {
                FishMonController.ShowToast($"You already have the <color={Color(old)}>{old} badge</color> for {name}.", new Color(0.8f, 0.85f, 0.95f), 4f);
                return;
            }

            var perksBefore = Perks.Where(Has).ToList();
            FishMonStorage.SetBadge(f.BossId, (int)tier);
            FishMonController.ShowToast($"<color={Color(tier)}>{tier} badge</color> earned for beating {name} with a Lv. {f.TeamLevel} team!", Gold, 6f);
            foreach (var p in Perks.Where(Has).Except(perksBefore))
                FishMonController.ShowToast($"Perk unlocked: {p.Name} ({p.Desc})!", Gold, 6f);
            try { AudioManager.PlayGlobalClip("Confetti"); } catch { }
            Plugin.Log.LogInfo($"Badge: {tier} for {name} (boss Lv {f.Level}, team Lv {f.TeamLevel})");
        }

        // Shown under the boss HP bar during a boss fight on Challenge.
        // caps: the level each badge needs, still-possible ones in their colour, the one you're on in bold, lost ones greyed.
        // status: where your team stands. It updates the moment an over-levelled FishMon joins the fight.
        public static bool BossBarText(out string caps, out string status)
        {
            caps = status = null;
            var f = _fight;
            if (f == null || !f.Valid || !f.Boss || f.Boss.IsDead || Presets.Current != Preset.Challenge) return false;

            // the strongest FishMon that has fought (at the level it joined with), or a preview with the one you have out
            FishMon top = null; int team = 0;
            foreach (var kv in f.Joined) if (top == null || kv.Value > team) { top = kv.Key; team = kv.Value; }
            bool preview = top == null;
            if (preview && FishMonController.Active) { top = FishMonController.Active.Mon; team = top.Level; }
            var tier = top == null ? BadgeTier.Gold : TierFor(f.Level, team);

            string Cell(BadgeTier t)
            {
                string text = $"{t} ≤ Lv. {Cap(f.Level, t)}";
                if (tier == BadgeTier.None || t < tier) return $"<color=#5c6478>{text}</color>"; // can't get this one any more
                return t == tier ? $"<b><color={Color(t)}>{text}</color></b>" : $"<color={Color(t)}>{text}</color>";
            }
            caps = $"{Cell(BadgeTier.Gold)}     {Cell(BadgeTier.Silver)}     {Cell(BadgeTier.Bronze)}";

            if (top == null)
                status = $"<color=#9aa6bd>Send out a FishMon at or under Lv. {Cap(f.Level, BadgeTier.Bronze)} for a badge</color>";
            else if (tier == BadgeTier.None)
                status = preview
                    ? $"<color=#ff9a9a>{top.DisplayName} (Lv. {team}) is over Lv. {Cap(f.Level, BadgeTier.Bronze)}: no badge if it fights</color>"
                    : $"<color=#ff9a9a>{top.DisplayName} (Lv. {team}) fought, over Lv. {Cap(f.Level, BadgeTier.Bronze)}: no badge this fight</color>";
            else
                status = preview
                    ? $"<color=#c8d2e6>{top.DisplayName} (Lv. {team}) would get <b><color={Color(tier)}>{tier}</color></b></color>"
                    : $"<color=#c8d2e6>On track for <b><color={Color(tier)}>{tier}</color></b>  (strongest that fought: {top.DisplayName}, Lv. {team})</color>";
            return true;
        }
    }
}
