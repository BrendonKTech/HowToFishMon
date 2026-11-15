using System.Collections.Generic;
using UnityEngine;

namespace HowToFishMon
{
    // Treasure Hunter: every few minutes (when it isn't fighting) it digs up a dead fish you can sell or eat,
    // or a bit of cash. Spawning things is the host's job, so a guest's Treasure Hunter asks the host.
    public partial class Companion
    {
        private float _digTimer = TreasureHunter.NextDelay();

        private void TickTreasure(Player player, float dt)
        {
            if (!Mon.Has(Ability.TreasureHunter) || Engaged) return;
            _digTimer -= dt;
            if (_digTimer > 0f) return;
            _digTimer = TreasureHunter.NextDelay();

            Vector3 at = transform.position + transform.forward * (HalfHeight + 0.8f) + Vector3.up * 0.6f;
            Vector3 head = transform.position + Vector3.up * (HalfHeight + 0.8f);
            if (Net.IsHost)
            {
                string found = TreasureHunter.DigOnServer(player, at);
                if (found == null) return;
                Effects(transform.position, "Poof", _inWater);
                FishMonController.AddFloat(head, "Found something!", new Color(1f, 0.85f, 0.3f));
                FishMonController.ShowToast($"{Mon.DisplayName} dug up {found}!", new Color(1f, 0.85f, 0.4f), 4f);
            }
            else if (Net.SendAbility(MonAbilityMsg.Dig, at))
            {
                Effects(transform.position, "Poof", _inWater);
                FishMonController.AddFloat(head, "Digging...", new Color(1f, 0.85f, 0.3f));
            }
        }
    }

    public static class TreasureHunter
    {
        private const float MinDelay = 120f, MaxDelay = 240f;
        private const float CashChance = 0.3f;

        public static float NextDelay() => Random.Range(MinDelay, MaxDelay);

        private static readonly Dictionary<Player, float> _lastDig = new Dictionary<Player, float>();

        // host only. Returns what was found ("a Cod", "$25"), or null if nothing happened.
        public static string DigOnServer(Player p, Vector3 at)
        {
            if (!p || !MoneyManager.Instance || !MoneyManager.Instance.IsServerInitialized || !Server.Instance) return null;
            if ((at - Companion.PlayerPos(p)).sqrMagnitude > 40f * 40f) return null;                    // has to be near them
            if (_lastDig.TryGetValue(p, out float t) && Time.time - t < MinDelay * 0.5f) return null;  // no spamming
            _lastDig[p] = Time.time;

            try
            {
                var prefab = Random.value < CashChance ? null : PickCreature();
                if (!prefab)
                {
                    int cash = Random.Range(15, 61);
                    MoneyManager.AddMoney(cash, p);
                    return "$" + cash;
                }

                var c = Object.Instantiate(prefab, at, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), Server.Instance.DynamicObjectsHolder);
                bool drip = Random.value < 0.05f;
                if (drip) c.SetDrip();
                c.ServerKillOnSpawn(); // same as the game's own "spawndead" command
                Server.Instance.Spawn(c.gameObject);
                return (drip ? "a Drip " : "a ") + Species.SafeName(prefab);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("FishMon Treasure Hunter couldn't dig: " + e.Message);
                return null;
            }
        }

        // a random normal creature from the journal. Cheap ones are much more common than valuable ones.
        private static Creature PickCreature()
        {
            var pool = new List<Creature>();
            var weights = new List<float>();
            float total = 0f;
            int n = Mathf.Min(GameInfo.AllCreatureCount, 1000);
            for (int i = 0; i < n; i++)
            {
                var c = GameInfo.GetCreature(i);
                if (!c || c.BossType != BossType.None || c.Bird || c.ExcludeFromJournal || c.DefaultWorth <= 0) continue;
                float w = 1f / Mathf.Sqrt(c.DefaultWorth);
                pool.Add(c); weights.Add(w); total += w;
            }
            if (pool.Count == 0) return null;
            float r = Random.value * total;
            for (int i = 0; i < pool.Count; i++)
            {
                r -= weights[i];
                if (r <= 0f) return pool[i];
            }
            return pool[pool.Count - 1];
        }
    }
}
