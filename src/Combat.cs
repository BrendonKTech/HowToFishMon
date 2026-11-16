using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HowToFishMon
{
    // the boss as a FishMon-style opponent, HP is the REAL boss HP in the game world.
    public class BossMon
    {
        public Creature Creature;
        public string Name;
        public int Level;
        public MonType Type1, Type2;
        public int[] Stats = new int[6];
        public int VirtualMaxHp;
        public List<Move> Moves;
        public bool IsMain;
        public bool IsBoss;

        public static BossMon From(Creature c)
        {
            var b = new BossMon { Creature = c, IsMain = c.BossType == BossType.Boss, IsBoss = c.BossType != BossType.None };
            b.Name = Species.SafeName(c);
            int lvl = b.IsMain ? Plugin.BossLevel.Value : b.IsBoss ? Plugin.MiniBossLevel.Value : Plugin.WildFoeLevel.Value;
            b.Level = Mathf.Clamp(lvl, 1, 100);
            var types = Species.TypesFor(b.Name, c.GetType().Name, c.ID);
            b.Type1 = types.Item1;
            b.Type2 = types.Item2 == types.Item1 ? MonType.None : types.Item2;

            int[] bases = b.IsMain ? new[] { 150, 115, 110, 115, 110, 85 }
                        : b.IsBoss ? new[] { 120, 95, 90, 95, 90, 80 }
                        : new[] { 75, 70, 65, 60, 60, 65 };
            for (int s = 0; s < 6; s++)
            {
                int core = (2 * bases[s] + 31) * b.Level / 100;
                b.Stats[s] = s == Stat.HP ? core + b.Level + 10 : core + 5;
            }
            b.VirtualMaxHp = b.Stats[Stat.HP];
            b.Moves = HowToFishMon.Moves.MovesetFor(b.Type1, b.Type2, b.Level, true);
            return b;
        }

        public bool Gone => !Creature || Creature.IsDestroying;
        public bool Dead => Gone || Creature.IsDead;
        public int RealHp => Gone ? 0 : Math.Max(0, Creature.Hp);
        public int RealMaxHp
        {
            get
            {
                int m = IsBoss ? BossManager.BossMaxHp : 0;
                if (m <= 0 && !Gone) m = Creature.MaxHp;
                return Math.Max(1, m);
            }
        }

        // exp for beating this boss outright
        public int FullExp => Mathf.RoundToInt((IsMain ? 320f : IsBoss ? 190f : 70f) * Level / 7f * Plugin.ExpMultiplier.Value);

        // where to aim: the middle of the boss's body
        public Vector3 Center
        {
            get
            {
                if (Gone) return Vector3.zero;
                var rig = Creature.Rig;
                return rig ? rig.worldCenterOfMass : Creature.transform.position;
            }
        }
    }

    public struct HitResult
    {
        public int Damage;
        public float Eff;
        public bool Crit;
        public bool Missed;
    }

    public static class Combat
    {
        private static readonly System.Random Rng = new System.Random();

        // critOdds: 1 in this many hits is a critical. superMult: extra damage on super-effective hits (Expert Belt)
        public static HitResult Calc(int level, int atk, int def, Move m, MonType a1, MonType a2, MonType d1, MonType d2, float mult,
                                     int critOdds = 16, float superMult = 1f)
        {
            var h = new HitResult();
            if (Rng.Next(100) >= m.Accuracy) { h.Missed = true; return h; }
            h.Eff = TypeChart.Effectiveness(m.Type, d1, d2);
            if (h.Eff <= 0f) return h;
            if (h.Eff >= 2f) mult *= superMult;
            h.Crit = Rng.Next(Mathf.Max(1, critOdds)) == 0;
            float baseDmg = ((2f * level / 5f + 2f) * m.Power * atk / Math.Max(1, def)) / 50f + 2f;
            float stab = (m.Type == a1 || m.Type == a2) ? 1.5f : 1f;
            float rand = 0.85f + (float)Rng.NextDouble() * 0.15f;
            h.Damage = Math.Max(1, Mathf.FloorToInt(baseDmg * stab * h.Eff * rand * (h.Crit ? 1.5f : 1f) * mult));
            return h;
        }

        // fish attacks boss. Returns the hit (Damage = battle damage).
        public static HitResult FishHitsBoss(FishMon f, BossMon b, Move m, float mult = 1f)
        {
            bool phys = m.Category == MoveCategory.Physical;
            // Berserker: the more HP it has lost, the harder it hits
            if (f.Has(Ability.Berserker))
                mult *= 1f + Abilities.BerserkerMax * (1f - Mathf.Clamp01(f.CurrentHp / (float)Mathf.Max(1, f.MaxHp)));
            if (phys && Statuses.Is(f.Status, StatusCond.Burn)) mult *= Statuses.BurnAttack;
            if (f.Holds(phys ? HeldItem.ChoiceBand : HeldItem.ChoiceSpecs)) mult *= Items.ChoiceBoost;
            return Calc(f.Level, f.CalcStat(phys ? Stat.Atk : Stat.SpA), b.Stats[phys ? Stat.Def : Stat.SpD],
                m, f.Type1, f.Type2, b.Type1, b.Type2, mult,
                f.Holds(HeldItem.ScopeLens) ? Items.ScopeLensCritOdds : 16,
                f.Holds(HeldItem.ExpertBelt) ? Items.ExpertBeltBoost : 1f);
        }

        public static HitResult BossHitsFish(BossMon b, FishMon f, Move m)
        {
            bool phys = m.Category == MoveCategory.Physical;
            float mult = Plugin.BossDamageToFishMultiplier.Value * (f.Has(Ability.ThickScales) ? Abilities.ThickScalesTaken : 1f);
            if (phys && Statuses.Is(Statuses.FoePeek(b.Creature), StatusCond.Burn)) mult *= Statuses.BurnAttack;
            return Calc(b.Level, b.Stats[phys ? Stat.Atk : Stat.SpA], f.CalcStat(phys ? Stat.Def : Stat.SpD),
                m, b.Type1, b.Type2, f.Type1, f.Type2, mult);
        }

        // a hit that takes X% of the boss's battle HP removes X% of its real HP bar
        public static int ToRealDamage(int battleDamage, BossMon b)
        {
            return Mathf.Max(1, Mathf.RoundToInt(battleDamage / (float)b.VirtualMaxHp * b.RealMaxHp * Plugin.DamageToBossMultiplier.Value));
        }

        // same path as a melee hit from you: real damage, hit effects, killscore credit
        public static void DealRealDamage(BossMon b, int amount, Vector3 point, Vector3 dir)
        {
            var boss = b.Creature;
            var player = Player.LocalPlayer;
            if (!boss || boss.IsDead || !player) return;
            // knock wild creatures around a bit; bosses ignore force anyway
            CallLocalHit(boss, point, dir, player, amount, b.IsBoss ? Vector3.zero : dir * 4f);
            try { PlayerUI.AddDamageNumber(point, amount, boss.Hp - amount <= 0); } catch { }
        }

        // the game keeps adding params to LocalHit (an update added withArrow + whileInspecting and broke
        // every attack), so find it by name and fill in whatever extra params it has with their defaults
        private static System.Reflection.MethodInfo _localHit;

        private static void CallLocalHit(Creature boss, Vector3 point, Vector3 dir, Player player, int damage, Vector3 force)
        {
            if (_localHit == null)
            {
                _localHit = typeof(Creature).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)
                    .Where(m => m.Name == "LocalHit")
                    .OrderByDescending(m => m.GetParameters().Length)
                    .FirstOrDefault();
                if (_localHit == null) throw new MissingMethodException("Creature", "LocalHit");
                Plugin.Log.LogInfo($"Using Creature.LocalHit with {_localHit.GetParameters().Length} params");
            }

            var ps = _localHit.GetParameters();
            var args = new object[ps.Length];
            // the ones we care about, in the order they've always been in
            object[] known = { boss.transform, point, dir, player, damage, false, force };
            for (int i = 0; i < ps.Length; i++)
            {
                if (i < known.Length && ps[i].ParameterType.IsInstanceOfType(known[i])) args[i] = known[i];
                else if (ps[i].HasDefaultValue) args[i] = ps[i].DefaultValue;
                else args[i] = ps[i].ParameterType.IsValueType ? Activator.CreateInstance(ps[i].ParameterType) : null;
            }
            _localHit.Invoke(boss, args);
        }

        // the fish's smartest move against this boss, with a bit of randomness
        public static Move PickMove(List<Move> moves, MonType self1, MonType self2, MonType tgt1, MonType tgt2, int extraFoes = 0)
        {
            if (moves.Count == 0) return HowToFishMon.Moves.All[0];
            if (Rng.NextDouble() < 0.25) return moves[Rng.Next(moves.Count)];
            return moves.OrderByDescending(m =>
                m.Power * (m.Accuracy / 100f) * TypeChart.Effectiveness(m.Type, tgt1, tgt2) *
                ((m.Type == self1 || m.Type == self2) ? 1.5f : 1f) *
                (m.Spread && extraFoes > 0 ? 0.75f * (1 + extraFoes) : 1f)).First();
        }

        public static float Random01() => (float)Rng.NextDouble();
    }
}
