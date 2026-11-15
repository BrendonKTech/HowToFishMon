using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HowToFishMon
{
    public enum StatusCond : byte { None, Burn, Poison, Paralysis, Sleep, Freeze }

    public class StatusState
    {
        public StatusCond Cond;
        public float Until;
        public float NextTick;
        public void Clear() => Cond = StatusCond.None;
    }

    public static class Statuses
    {
        public const float Tick = 3f;
        public const float BurnDamage = 1f / 16f;
        public const float PoisonDamage = 1f / 12f;
        public const float BossDotScale = 0.25f;
        public const float BurnAttack = 0.5f;
        public const float ParaSpeed = 0.6f;
        public const float ParaSkip = 0.25f;

        public static bool Is(StatusState s, StatusCond c) => s != null && s.Cond == c && Time.time < s.Until;
        public static bool Has(StatusState s) => s != null && s.Cond != StatusCond.None && Time.time < s.Until;
        public static bool CantAct(StatusState s) => Is(s, StatusCond.Sleep) || Is(s, StatusCond.Freeze);

        public static string Tag(StatusCond c)
        {
            switch (c)
            {
                case StatusCond.Burn: return "BRN";
                case StatusCond.Poison: return "PSN";
                case StatusCond.Paralysis: return "PAR";
                case StatusCond.Sleep: return "SLP";
                case StatusCond.Freeze: return "FRZ";
                default: return "";
            }
        }

        public static Color ColorOf(StatusCond c)
        {
            switch (c)
            {
                case StatusCond.Burn: return new Color(1f, 0.5f, 0.25f);
                case StatusCond.Poison: return new Color(0.75f, 0.4f, 1f);
                case StatusCond.Paralysis: return new Color(1f, 0.9f, 0.3f);
                case StatusCond.Sleep: return new Color(0.7f, 0.75f, 0.9f);
                case StatusCond.Freeze: return new Color(0.55f, 0.9f, 1f);
                default: return Color.white;
            }
        }

        public static string RichTag(StatusState s)
        {
            if (!Has(s)) return "";
            return $"  <b><color=#{ColorUtility.ToHtmlStringRGB(ColorOf(s.Cond))}>{Tag(s.Cond)}</color></b>";
        }

        public static string Inflicted(StatusCond c)
        {
            switch (c)
            {
                case StatusCond.Burn: return "was burned!";
                case StatusCond.Poison: return "was poisoned!";
                case StatusCond.Paralysis: return "is paralyzed!";
                case StatusCond.Sleep: return "fell asleep!";
                case StatusCond.Freeze: return "was frozen solid!";
                default: return "";
            }
        }

        public static string Cured(StatusCond c)
        {
            switch (c)
            {
                case StatusCond.Burn: return "'s burn healed.";
                case StatusCond.Poison: return " is no longer poisoned.";
                case StatusCond.Paralysis: return " can move freely again.";
                case StatusCond.Sleep: return " woke up!";
                case StatusCond.Freeze: return " thawed out!";
                default: return "";
            }
        }

        public static bool Immune(StatusCond c, MonType t1, MonType t2)
        {
            bool Is_(MonType t) => t1 == t || t2 == t;
            switch (c)
            {
                case StatusCond.Burn: return Is_(MonType.Fire);
                case StatusCond.Poison: return Is_(MonType.Poison) || Is_(MonType.Steel);
                case StatusCond.Paralysis: return Is_(MonType.Electric);
                case StatusCond.Freeze: return Is_(MonType.Ice);
                default: return false;
            }
        }

        private static float Duration(StatusCond c, bool boss)
        {
            switch (c)
            {
                case StatusCond.Burn:
                case StatusCond.Poison: return 15f;
                case StatusCond.Paralysis: return 20f;
                case StatusCond.Sleep: return Random.Range(3f, 6f) * (boss ? 0.5f : 1f);
                case StatusCond.Freeze: return Random.Range(2f, 5f) * (boss ? 0.5f : 1f);
                default: return 0f;
            }
        }

        public static bool TryInflict(StatusState s, Move m, MonType t1, MonType t2, bool boss)
        {
            if (s == null || m.Status == StatusCond.None || Has(s) || Immune(m.Status, t1, t2)) return false;
            if (Combat.Random01() * 100f >= m.StatusChance) return false;
            s.Cond = m.Status;
            s.Until = Time.time + Duration(m.Status, boss);
            s.NextTick = Time.time + Tick;
            return true;
        }

        public static bool TryThaw(StatusState s, Move m)
        {
            if (!Is(s, StatusCond.Freeze) || m.Type != MonType.Fire) return false;
            s.Clear();
            return true;
        }

        public static float DueDamage(StatusState s, bool boss)
        {
            if (!Is(s, StatusCond.Burn) && !Is(s, StatusCond.Poison)) return 0f;
            if (Time.time < s.NextTick) return 0f;
            s.NextTick += Tick;
            return (s.Cond == StatusCond.Burn ? BurnDamage : PoisonDamage) * (boss ? BossDotScale : 1f);
        }

        private static readonly Dictionary<Creature, StatusState> _foes = new Dictionary<Creature, StatusState>();

        public static StatusState Foe(Creature c)
        {
            if (!c) return null;
            if (!_foes.TryGetValue(c, out var s)) _foes[c] = s = new StatusState();
            return s;
        }

        public static StatusState FoePeek(Creature c) => c && _foes.TryGetValue(c, out var s) ? s : null;

        public static void TickFoes()
        {
            if (_foes.Count == 0) return;
            foreach (var kv in _foes.ToList())
            {
                var c = kv.Key;
                var s = kv.Value;
                if (!c || c.IsDead || c.IsDestroying || !Has(s)) { _foes.Remove(c); continue; }

                bool boss = c.BossType != BossType.None;
                float frac = DueDamage(s, boss);
                if (frac <= 0f || (boss && BossManager.IsImmortal)) continue;

                var foe = BossMon.From(c);
                int battle = Mathf.Max(1, Mathf.RoundToInt(foe.VirtualMaxHp * frac));
                int real = Mathf.Min(Combat.ToRealDamage(battle, foe), Mathf.Max(1, foe.RealHp));
                Vector3 at = foe.Center;
                Combat.DealRealDamage(foe, real, at, Vector3.up);
                if (Plugin.AttackEffects.Value) Fx.Burst(at, ColorOf(s.Cond), 6, 0.7f);
                FishMonController.AddFloat(at + Vector3.up, $"{foe.Name} is hurt by its {(s.Cond == StatusCond.Burn ? "burn" : "poison")}!", ColorOf(s.Cond));
            }
        }
    }

    public partial class Companion
    {
        private StatusCond _lastStatus;
        private float _zzzAt;

        private bool TickStatus()
        {
            var s = Mon.Status;
            Vector3 head = transform.position + Vector3.up * (HalfHeight + 0.8f);

            if (s.Cond != StatusCond.None && !Statuses.Has(s))
            {
                var was = s.Cond;
                s.Clear();
                FishMonController.AddFloat(head, Mon.DisplayName + Statuses.Cured(was), new Color(0.7f, 1f, 0.75f));
            }
            // just got one: a little burst in its colour
            if (s.Cond != _lastStatus)
            {
                _lastStatus = s.Cond;
                if (s.Cond != StatusCond.None && Plugin.AttackEffects.Value) Fx.Burst(transform.position, Statuses.ColorOf(s.Cond), 8, 1f);
            }
            if (s.Cond == StatusCond.None) return false;

            if (Statuses.Is(s, StatusCond.Sleep) && Time.time >= _zzzAt)
            {
                _zzzAt = Time.time + 1.5f;
                FishMonController.AddFloat(head, "Zzz", Statuses.ColorOf(StatusCond.Sleep), 1.2f);
            }

            float frac = Statuses.DueDamage(s, false);
            if (frac <= 0f) return false;
            int dmg = Mathf.Max(1, Mathf.RoundToInt(Mon.MaxHp * frac));
            Mon.CurrentHp = Mathf.Max(0, Mon.CurrentHp - dmg);
            string what = s.Cond == StatusCond.Burn ? "burn" : "poison";
            FishMonController.AddFloat(head, $"{Mon.DisplayName} is hurt by its {what}!  -{dmg}", Statuses.ColorOf(s.Cond));
            if (Plugin.AttackEffects.Value) Fx.Burst(transform.position, Statuses.ColorOf(s.Cond), 5, 0.6f);
            if (!Mon.Fainted) return false;
            s.Clear();
            FishMonController.OnCompanionFainted(this);
            return true;
        }

        private void InflictOnFoe(BossMon foe, Move move)
        {
            if (foe == null || foe.Dead) return;
            var s = Statuses.Foe(foe.Creature);
            Vector3 top = foe.Center + Vector3.up * 1.5f;
            if (Statuses.TryThaw(s, move))
                FishMonController.AddFloat(top, foe.Name + Statuses.Cured(StatusCond.Freeze), Statuses.ColorOf(StatusCond.Freeze));
            else if (Statuses.TryInflict(s, move, foe.Type1, foe.Type2, foe.IsBoss))
            {
                FishMonController.AddFloat(top, $"{foe.Name} {Statuses.Inflicted(s.Cond)}", Statuses.ColorOf(s.Cond), 2f);
                if (Plugin.AttackEffects.Value) Fx.Burst(foe.Center, Statuses.ColorOf(s.Cond), 10, 1.4f);
            }
        }
    }
}
