using System.Collections.Generic;
using UnityEngine;

namespace HowToFishMon
{
    // Medic: heals you a little every few seconds while it's out. Player health lives on the host,
    // so a guest's Medic asks the host to do it.
    public partial class Companion
    {
        private float _medicTimer = Medic.Interval;
        private int _medicMisses;
        private int _medicHpAtAsk = -1;
        private bool _medicWarned;

        private void TickMedic(Player player, float dt)
        {
            if (!Mon.Has(Ability.Medic)) return;
            _medicTimer -= dt;
            if (_medicTimer > 0f) return;
            _medicTimer = Medic.Interval;

            int hp = Medic.Health(player);
            // guest: if our heals never arrive, the host doesn't have this version of the mod
            if (!Net.IsHost && _medicHpAtAsk >= 0)
            {
                _medicMisses = hp > _medicHpAtAsk ? 0 : _medicMisses + 1;
                if (_medicMisses >= 4 && !_medicWarned)
                {
                    _medicWarned = true;
                    FishMonController.ShowToast($"{Mon.DisplayName} can't heal you here. The host needs the latest FishMon mod for Medic to work in their world.", new Color(1f, 0.7f, 0.4f), 6f);
                }
            }
            _medicHpAtAsk = -1;
            if (!Medic.NeedsHealing(player)) return;

            bool ok;
            if (Net.IsHost) ok = Medic.HealOnServer(player);
            else { ok = Net.SendAbility(MonAbilityMsg.Heal); if (ok) _medicHpAtAsk = hp; }
            if (ok && !_medicWarned)
                FishMonController.AddFloat(PlayerPos(player) + Vector3.up * 2.2f, $"+{Medic.Amount} HP", new Color(0.5f, 1f, 0.6f), 1.2f);
        }
    }

    public static class Medic
    {
        public const float Interval = 4f; // seconds between heals
        public const int Amount = 2;      // health per heal (players have 100)

        private static readonly Dictionary<Player, float> _lastHeal = new Dictionary<Player, float>();

        public static int Health(Player p)
        {
            try { return p && p.Vitals ? p.Vitals.Health : -1; } catch { return -1; }
        }

        public static bool NeedsHealing(Player p)
        {
            if (!p || !p.Vitals || (p.Dying && p.Dying.IsDead)) return false;
            return p.Vitals.Health > 0 && p.Vitals.HealthPercent < 0.999f;
        }

        // host only
        public static bool HealOnServer(Player p)
        {
            if (!NeedsHealing(p)) return false;
            if (_lastHeal.TryGetValue(p, out float t) && Time.time - t < Interval * 0.75f) return false; // no spamming
            _lastHeal[p] = Time.time;
            try { p.Vitals.Heal(Amount); return true; }
            catch (System.Exception e) { Plugin.Log.LogWarning("FishMon Medic couldn't heal: " + e.Message); return false; }
        }
    }
}
