using UnityEngine;

namespace HowToFishMon
{
    // Guard: sticks close to you (see DoRoam) and goes after whatever just hurt you
    public partial class Companion
    {
        private const float GuardSearch = 9f;  // how close to you an attacker has to be

        private int _lastPlayerHp = -1;

        private void TickGuard(Player player)
        {
            int hp = Medic.Health(player);
            int was = _lastPlayerHp;
            _lastPlayerHp = hp;

            if (!Mon.Has(Ability.Guard) || hp < 0 || was < 0 || hp >= was) return;
            if (player.Dying && player.Dying.IsDead) return;
            if (OrderedTarget && IsValidOrder(OrderedTarget)) return; // already going after something you pointed at

            var foe = FindAttacker(player);
            if (!foe || (_boss != null && _boss.Creature == foe)) return; // nothing nearby (hunger, drowning...) or already on it
            OrderAttack(foe);
            FishMonController.AddFloat(transform.position + Vector3.up * (HalfHeight + 0.8f), $"{Mon.DisplayName} protects you!", new Color(1f, 0.6f, 0.4f));
        }

        // the game doesn't say who hit you, so: the closest hostile creature, else the boss if it's near
        private static Creature FindAttacker(Player player)
        {
            Vector3 me = PlayerPos(player);
            Creature best = null;
            float bestD = GuardSearch;
            foreach (var item in ItemManager.Items.Values)
            {
                var c = item ? item.Creature : null;
                if (!IsHostile(c) || c.AttachedRod || c.Holder) continue;
                float d = Vector3.Distance(me, c.transform.position);
                if (d < bestD) { bestD = d; best = c; }
            }
            if (best) return best;

            var boss = BossManager.Boss;
            if (boss && !boss.IsDead && !boss.IsDestroying && !BossManager.IsImmortal &&
                Vector3.Distance(me, boss.transform.position) <= 40f)
                return boss;
            return null;
        }
    }
}
