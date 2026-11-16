using UnityEngine;

namespace HowToFishMon
{
    // held items that act on their own while the FishMon is out. (The damage / EXP ones are a line in
    // Combat.FishHitsBoss and FishMonController.GiveExp, Focus Sash is in BossCounterAttack.)
    public partial class Companion
    {
        private float _leftoversTimer = Items.LeftoversEvery;

        private void TickItem(float dt)
        {
            var item = Mon.Item;
            if (item == HeldItem.None || Mon.Fainted) return;

            switch (item)
            {
                case HeldItem.Leftovers:
                    _leftoversTimer -= dt;
                    if (_leftoversTimer > 0f) break;
                    _leftoversTimer = Items.LeftoversEvery;
                    if (Mon.CurrentHp >= Mon.MaxHp) break;
                    int heal = Mathf.Max(1, Mathf.RoundToInt(Mon.MaxHp * Items.LeftoversHeal));
                    Mon.CurrentHp = Mathf.Min(Mon.MaxHp, Mon.CurrentHp + heal);
                    FishMonController.AddFloat(transform.position + Vector3.up * (HalfHeight + 0.8f), $"Leftovers +{heal}", new Color(0.5f, 1f, 0.6f), 1.2f);
                    break;

                case HeldItem.SitrusBerry:
                    if (Mon.CurrentHp >= Mon.MaxHp * Items.SitrusBelow) break;
                    int berry = Mathf.Max(1, Mathf.RoundToInt(Mon.MaxHp * Items.SitrusHeal));
                    Mon.CurrentHp = Mathf.Min(Mon.MaxHp, Mon.CurrentHp + berry);
                    UseUpItem($"ate its Sitrus Berry! +{berry} HP");
                    break;

                case HeldItem.LumBerry:
                    if (!Statuses.Has(Mon.Status)) break;
                    Mon.Status.Clear();
                    UseUpItem("ate its Lum Berry and was cured!");
                    break;
            }
        }

        // a one-use item did its thing
        private void UseUpItem(string what)
        {
            var info = Mon.ItemInfo;
            Mon.Item = HeldItem.None;
            FishMonController.AddFloat(transform.position + Vector3.up * (HalfHeight + 1f), $"{Mon.DisplayName} {what}", new Color(1f, 0.85f, 0.4f), 2f);
            if (Plugin.AttackEffects.Value) Fx.Burst(transform.position, new Color(1f, 0.85f, 0.4f), 8, 0.9f);
            FishMonStorage.SaveSoon();
            Plugin.Log.LogInfo($"{Mon.DisplayName} used up its {info?.Name}");
        }
    }
}
