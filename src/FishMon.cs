using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;
using UnityEngine;

namespace HowToFishMon
{
    // one caught fish with its stats. Saved to disk as JSON.
    public class FishMon
    {
        public string Uid = System.Guid.NewGuid().ToString("N");
        public byte SpeciesId;
        public string SpeciesName = "Fish";
        public string Nickname = "";
        public int Level = 5;
        public int Exp;
        public int[] BaseStats = new int[6];
        public int[] IVs = new int[6];
        public int[] EVs = new int[6];
        public int Nature;
        public MonType Type1 = MonType.Water;
        public MonType Type2 = MonType.None;
        public bool Shiny;
        public float Weight = 1f;
        public int CurrentHp = -1;
        public string CaughtOn = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        public int BossesDefeated;
        public int Stage;              // 0 = base form, 1 = first evolution, 2 = final
        public string BaseSpecies;     // the species it was caught as (evolution chains are keyed on this)

        [JsonIgnore] public float RegenCarry;
        // set when it evolved; the controller swaps the model and clears it
        [JsonIgnore] public bool JustEvolved;
        [JsonIgnore] public string DisplayName => string.IsNullOrEmpty(Nickname) ? SpeciesName : Nickname;
        [JsonIgnore] public bool Fainted => CurrentHp <= 0;
        [JsonIgnore] public int MaxHp => Math.Max(1, Mathf.RoundToInt(CalcStat(Stat.HP) * Mathf.Max(0.05f, Plugin.FishHpMultiplier.Value)));
        [JsonIgnore] public int IVTotal => IVs.Sum();
        [JsonIgnore] public float IVPercent => IVTotal / 186f;
        [JsonIgnore] public bool PhysicalLeaning => Stat_(Stat.Atk) >= Stat_(Stat.SpA);

        public int Stat_(int s) => CalcStat(s);

        public int CalcStat(int s)
        {
            int b = BaseStats[s], iv = IVs[s], ev = EVs[s] / 4;
            int core = (2 * b + iv + ev) * Level / 100;
            if (s == Stat.HP) return core + Level + 10;
            return Mathf.FloorToInt((core + 5) * Natures.Modifier(Nature, s));
        }

        // moves you picked yourself. Null = automatic (always its best 4).
        public List<string> KnownMoves;

        [JsonIgnore] public bool CustomMoves => KnownMoves != null;

        public List<Move> Learnable() => Moves.LearnableFor(Type1, Type2, Level);

        public List<Move> GetMoves()
        {
            if (KnownMoves != null)
            {
                var learnable = Learnable();
                var picked = KnownMoves.Select(Moves.Find).Where(m => m != null && learnable.Contains(m)).Distinct().Take(4).ToList();
                if (picked.Count > 0) return picked;
            }
            return Moves.MovesetFor(Type1, Type2, Level, PhysicalLeaning);
        }

        // switch to a hand-picked moveset, starting from whatever it knows now
        public void BeginCustomMoves()
        {
            if (KnownMoves == null) KnownMoves = GetMoves().Select(m => m.Name).ToList();
        }

        // drop moves it can no longer use (e.g. lost a type), keep at least one.
        public void PruneMoves()
        {
            if (KnownMoves == null) return;
            var learnable = Learnable();
            KnownMoves = KnownMoves.Where(n => learnable.Any(m => m.Name == n)).Distinct().Take(4).ToList();
            if (KnownMoves.Count == 0) KnownMoves = null;
        }

        public void Revive(float hpFraction) => CurrentHp = Mathf.Clamp(Mathf.RoundToInt(MaxHp * hpFraction), 1, MaxHp);

        public static int ExpForLevel(int level) => level <= 1 ? 0 : level * level * level;
        [JsonIgnore] public int ExpToNext => ExpForLevel(Level + 1) - Exp;
        [JsonIgnore] public float ExpProgress
        {
            get
            {
                if (Level >= 100) return 1f;
                int a = ExpForLevel(Level), b = ExpForLevel(Level + 1);
                return Mathf.Clamp01((Exp - a) / (float)Math.Max(1, b - a));
            }
        }

        // adds exp, returns list of messages (level ups / new moves)
        public List<string> GainExp(int amount, int[] evYield = null)
        {
            var msgs = new List<string>();
            if (evYield != null)
                for (int i = 0; i < 6; i++)
                {
                    int total = EVs.Sum();
                    int add = Math.Min(Math.Min(evYield[i], 252 - EVs[i]), 510 - total);
                    if (add > 0) EVs[i] += add;
                }

            if (Level >= 100 || amount <= 0) return msgs;
            Exp += amount;
            var oldMoves = GetMoves().Select(m => m.Name).ToList();
            var oldLearnable = Learnable().Select(m => m.Name).ToList();
            while (Level < 100 && Exp >= ExpForLevel(Level + 1))
            {
                int oldMax = MaxHp;
                Level++;
                if (CurrentHp > 0) CurrentHp += MaxHp - oldMax; // keep damage taken, fainted stays fainted
                msgs.Add($"{DisplayName} grew to Lv. {Level}!");
            }
            TryEvolve(msgs);

            if (KnownMoves == null)
            {
                foreach (var m in GetMoves())
                    if (!oldMoves.Contains(m.Name)) msgs.Add($"{DisplayName} learned {m.Name}!");
            }
            else
            {
                // hand-picked moveset: fill empty slots, otherwise let the player decide
                PruneMoves();
                foreach (var m in Learnable())
                {
                    if (oldLearnable.Contains(m.Name)) continue;
                    if (KnownMoves == null) break;
                    if (KnownMoves.Count < 4) { KnownMoves.Add(m.Name); msgs.Add($"{DisplayName} learned {m.Name}!"); }
                    else msgs.Add($"{DisplayName} can learn {m.Name}! Teach it in the party menu ({Plugin.KeyName(Plugin.PartyKey)}) → Moves.");
                }
            }
            return msgs;
        }

        // evolves as many stages as its level allows. Returns true if it evolved.
        public bool TryEvolve(List<string> msgs)
        {
            if (string.IsNullOrEmpty(BaseSpecies)) BaseSpecies = SpeciesName;
            bool evolved = false;
            EvoStage next;
            while ((next = Evolutions.Next(this)) != null && Level >= next.Level)
            {
                string oldName = DisplayName;
                float hpFrac = MaxHp > 0 ? CurrentHp / (float)MaxHp : 1f;

                Stage++;
                SpeciesName = next.Name;
                for (int i = 0; i < 6; i++) BaseStats[i] = Math.Min(200, Mathf.RoundToInt(BaseStats[i] * 1.2f) + 3);
                if (next.NewType1 != MonType.None) Type1 = next.NewType1;
                if (next.NewType2 != MonType.None && next.NewType2 != Type1) Type2 = next.NewType2;
                if (CurrentHp > 0) CurrentHp = Math.Max(1, Mathf.RoundToInt(MaxHp * hpFrac));

                msgs?.Add($"What? {oldName} is evolving!");
                msgs?.Add($"Congratulations! {oldName} evolved into {next.Name}!");
                evolved = true;
                JustEvolved = true;
            }
            if (evolved) PruneMoves();
            return evolved;
        }

        [JsonIgnore] public int MaxStage => Evolutions.ChainFor(this).Length;

        public void HealFull() => CurrentHp = MaxHp;

        public string IVJudge()
        {
            int t = IVTotal;
            if (t >= 151) return "Outstanding potential";
            if (t >= 121) return "Relatively superior";
            if (t >= 91) return "Above average";
            return "Decent";
        }

        public static string StatJudge(int iv)
        {
            if (iv >= 31) return "Best";
            if (iv >= 30) return "Fantastic";
            if (iv >= 26) return "Very Good";
            if (iv >= 16) return "Pretty Good";
            if (iv >= 1) return "Decent";
            return "No Good";
        }
    }

    // turns a game creature into FishMon species data (base stats + types)
    public static class Species
    {
        private static readonly (string key, MonType t1, MonType t2)[] Keywords =
        {
            ("puffer", MonType.Water, MonType.Poison),
            ("piranha", MonType.Water, MonType.Dark),
            ("shark", MonType.Water, MonType.Dark),
            ("eel", MonType.Water, MonType.Electric),
            ("whale", MonType.Water, MonType.Fire),
            ("spider", MonType.Bug, MonType.Rock),
            ("crab", MonType.Water, MonType.Rock),
            ("albatross", MonType.Flying, MonType.Normal),
            ("gull", MonType.Flying, MonType.Water),
            ("bird", MonType.Flying, MonType.Normal),
            ("seahorse", MonType.Water, MonType.Dragon),
            ("gar", MonType.Water, MonType.Steel),
            ("pike", MonType.Water, MonType.Steel),
            ("trigger", MonType.Water, MonType.Fighting),
            ("sole", MonType.Water, MonType.Ground),
            ("lemon", MonType.Water, MonType.Grass),
            ("mackerel", MonType.Water, MonType.Ice),
            ("drip", MonType.Water, MonType.Fairy),
            ("cod", MonType.Water, MonType.Normal),
            ("jelly", MonType.Water, MonType.Poison),
            ("squid", MonType.Water, MonType.Psychic),
            ("octo", MonType.Water, MonType.Psychic),
            ("ghost", MonType.Water, MonType.Ghost),
            ("lava", MonType.Fire, MonType.Water),
        };

        private static readonly MonType[] RandomSecondaries =
        {
            MonType.None, MonType.None, MonType.None, MonType.Normal, MonType.Ice, MonType.Electric,
            MonType.Dark, MonType.Psychic, MonType.Ground, MonType.Fighting, MonType.Dragon, MonType.Fairy
        };

        public static string SafeName(Item item)
        {
            try { var n = item.GetName(); if (!string.IsNullOrWhiteSpace(n)) return n.Trim(); }
            catch { }
            return item.GetType().Name;
        }

        public static (MonType, MonType) TypesFor(string name, string className, int seed)
        {
            string s = (name + " " + className).ToLowerInvariant();
            foreach (var k in Keywords)
                if (s.Contains(k.key)) return (k.t1, k.t2);
            var rng = new System.Random(seed);
            var t2 = RandomSecondaries[rng.Next(RandomSecondaries.Length)];
            return (MonType.Water, t2);
        }

        private static int StableHash(string s)
        {
            unchecked
            {
                int h = 23;
                foreach (char c in s) h = h * 31 + c;
                return h;
            }
        }

        // the fixed, per-species part of a FishMon: same for every fish of that kind (works on prefabs too)
        public class Info
        {
            public string Name;
            public int[] BaseStats = new int[6];
            public MonType Type1, Type2;
            public float Weight;
            public int Worth;
        }

        public static Info Describe(Creature c)
        {
            string name = SafeName(c);
            int seed = StableHash(c.GetType().Name + ":" + c.ID);
            var speciesRng = new System.Random(seed);

            int worth = Math.Max(1, c.DefaultWorth);
            int maxHp = Math.Max(1, c.MaxHp);
            float weight = 1f;
            try { weight = Traverse.Create(c).Field<float>("_weight").Value; } catch { }

            // base stat total grows with how valuable / tough the species is
            int bst = Mathf.Clamp(Mathf.RoundToInt(250f + 70f * Mathf.Log10(worth + 1) + 15f * Mathf.Log10(maxHp + 1)), 240, 580);

            float[] w = new float[6];
            for (int i = 0; i < 6; i++) w[i] = 0.6f + (float)speciesRng.NextDouble() * 0.8f;
            // heavy fish are bulky, light fish are fast
            float heavy = Mathf.Clamp(Mathf.Log10(weight + 1f), 0f, 2f);
            w[Stat.HP] += 0.25f * heavy;
            w[Stat.Def] += 0.2f * heavy;
            w[Stat.Spe] += 0.3f * (1f - Mathf.Clamp01(heavy));
            float sum = w.Sum();

            var info = new Info { Name = name, Weight = weight, Worth = c.DefaultWorth };
            for (int i = 0; i < 6; i++) info.BaseStats[i] = Mathf.Clamp(Mathf.RoundToInt(bst * w[i] / sum), 20, 160);
            var types = TypesFor(name, c.GetType().Name, seed);
            info.Type1 = types.Item1;
            info.Type2 = types.Item2 == types.Item1 ? MonType.None : types.Item2;
            return info;
        }

        public static FishMon CreateFromCreature(Creature c)
        {
            var info = Describe(c);
            var rng = new System.Random();

            var mon = new FishMon
            {
                SpeciesId = c.ID,
                SpeciesName = info.Name,
                BaseSpecies = info.Name,
                Nature = rng.Next(Natures.All.Length),
                Shiny = c.IsDrip,
                Weight = info.Weight * c.RandomizedWeight,
                BaseStats = (int[])info.BaseStats.Clone(),
                Type1 = info.Type1,
                Type2 = info.Type2,
            };
            for (int i = 0; i < 6; i++) mon.IVs[i] = rng.Next(32);
            if (mon.Shiny)
            {
                // Drip (shiny) fish get 3 guaranteed perfect IVs
                foreach (int i in Enumerable.Range(0, 6).OrderBy(_ => rng.Next()).Take(3)) mon.IVs[i] = 31;
            }

            // level: random, nudged up by bigger-than-average weight rolls
            int min = Plugin.MinCatchLevel.Value, max = Math.Max(min, Plugin.MaxCatchLevel.Value);
            float roll = (float)rng.NextDouble();
            roll = Mathf.Clamp01(roll * roll + (c.RandomizedWeight - 1f) * 0.8f); // most catches are low level
            mon.Level = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(min, max, roll)), 1, 100);
            mon.Exp = FishMon.ExpForLevel(mon.Level);
            // high-level catches come already evolved
            mon.TryEvolve(null);
            mon.JustEvolved = false;
            mon.HealFull();
            return mon;
        }
    }
}
