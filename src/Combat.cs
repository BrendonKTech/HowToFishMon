using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HowToFishMon
{
    // the boss as a FishMon-style opponent. Its HP is the REAL boss HP in the game world.
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

        public static HitResult Calc(int level, int atk, int def, Move m, MonType a1, MonType a2, MonType d1, MonType d2, float mult)
        {
            var h = new HitResult();
            if (Rng.Next(100) >= m.Accuracy) { h.Missed = true; return h; }
            h.Eff = TypeChart.Effectiveness(m.Type, d1, d2);
            if (h.Eff <= 0f) return h;
            h.Crit = Rng.Next(16) == 0;
            float baseDmg = ((2f * level / 5f + 2f) * m.Power * atk / Math.Max(1, def)) / 50f + 2f;
            float stab = (m.Type == a1 || m.Type == a2) ? 1.5f : 1f;
            float rand = 0.85f + (float)Rng.NextDouble() * 0.15f;
            h.Damage = Math.Max(1, Mathf.FloorToInt(baseDmg * stab * h.Eff * rand * (h.Crit ? 1.5f : 1f) * mult));
            return h;
        }

        // fish attacks boss. Returns the hit (Damage = battle damage).
        public static HitResult FishHitsBoss(FishMon f, BossMon b, Move m)
        {
            bool phys = m.Category == MoveCategory.Physical;
            return Calc(f.Level, f.CalcStat(phys ? Stat.Atk : Stat.SpA), b.Stats[phys ? Stat.Def : Stat.SpD],
                m, f.Type1, f.Type2, b.Type1, b.Type2, 1f);
        }

        public static HitResult BossHitsFish(BossMon b, FishMon f, Move m)
        {
            bool phys = m.Category == MoveCategory.Physical;
            return Calc(b.Level, b.Stats[phys ? Stat.Atk : Stat.SpA], f.CalcStat(phys ? Stat.Def : Stat.SpD),
                m, b.Type1, b.Type2, f.Type1, f.Type2, Plugin.BossDamageToFishMultiplier.Value);
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
            boss.LocalHit(boss.transform, point, dir, player, amount, false, b.IsBoss ? Vector3.zero : dir * 4f);
            try { PlayerUI.AddDamageNumber(point, amount, boss.Hp - amount <= 0); } catch { }
        }

        // the fish's smartest move against this boss, with a bit of randomness
        public static Move PickMove(List<Move> moves, MonType self1, MonType self2, MonType tgt1, MonType tgt2)
        {
            if (moves.Count == 0) return HowToFishMon.Moves.All[0];
            if (Rng.NextDouble() < 0.25) return moves[Rng.Next(moves.Count)];
            return moves.OrderByDescending(m =>
                m.Power * (m.Accuracy / 100f) * TypeChart.Effectiveness(m.Type, tgt1, tgt2) *
                ((m.Type == self1 || m.Type == self2) ? 1.5f : 1f)).First();
        }

        public static float Random01() => (float)Rng.NextDouble();
    }
}
