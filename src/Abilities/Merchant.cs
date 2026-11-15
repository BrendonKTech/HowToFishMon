using System.Collections.Generic;
using System.Linq;
using FishNet.Connection;
using HarmonyLib;
using UnityEngine;

namespace HowToFishMon
{
    // Merchant: walks over to fish you dropped and sells them, like dropping them in the sell box
    public partial class Companion
    {
        private const float MerchantRange = 14f;  // only fish this close to YOU
        private const float MerchantRest = 2.5f;  // the fish has to lie still this long first (so it doesn't snatch throws)

        private Item _sellTarget;
        private float _sellScan;
        private readonly Dictionary<Item, float> _restingSince = new Dictionary<Item, float>();
        private readonly Dictionary<Item, float> _sellTriedAt = new Dictionary<Item, float>();
        private readonly Dictionary<Item, int> _sellTries = new Dictionary<Item, int>();

        // true while the merchant is busy (walking to a fish), so it doesn't follow / roam this frame
        private bool TickMerchant(Player player, float dt)
        {
            if (!Mon.Has(Ability.Merchant)) { _sellTarget = null; return false; }

            _sellScan -= dt;
            if (_sellScan <= 0f) { _sellScan = 0.5f; ScanForSellable(player); }
            if (!_sellTarget) return false;
            if (!Merchant.CanSell(_sellTarget, player)) { _sellTarget = null; return false; }

            Vector3 flat = Merchant.ItemPos(_sellTarget) - transform.position; flat.y = 0f;
            float dist = flat.magnitude;
            MoveFlat(flat, dist, MoveSpeed * 1.4f, 0.6f, dt);
            if (dist <= 1f + HalfHeight)
            {
                SellTarget(player);
                _sellTarget = null;
            }
            return true;
        }

        private void ScanForSellable(Player player)
        {
            float now = Time.time;
            Vector3 me = PlayerPos(player);
            Item best = null;
            float bestD = float.MaxValue;
            var seen = new HashSet<Item>();

            foreach (var it in ItemManager.Items.Values)
            {
                if (!it || !seen.Add(it)) continue;
                if (!Merchant.CanSell(it, player) || (_sellTries.TryGetValue(it, out int n) && n >= 2))
                {
                    _restingSince.Remove(it);
                    continue;
                }
                // still moving (just thrown / rolling): restart its rest timer
                bool moving = it.Rig && it.Rig.linearVelocity.sqrMagnitude > 0.3f;
                if (moving || !_restingSince.TryGetValue(it, out float since)) { _restingSince[it] = now; continue; }
                if (now - since < MerchantRest) continue;
                if (_sellTriedAt.TryGetValue(it, out float tried) && now - tried < 5f) continue; // waiting on the host

                float d = (Merchant.ItemPos(it) - me).magnitude;
                if (d <= MerchantRange && d < bestD) { bestD = d; best = it; }
            }

            // forget fish that are gone
            foreach (var k in _restingSince.Keys.Where(k => !k || !seen.Contains(k)).ToList()) _restingSince.Remove(k);
            foreach (var k in _sellTriedAt.Keys.Where(k => !k).ToList()) { _sellTriedAt.Remove(k); _sellTries.Remove(k); }

            _sellTarget = best;
        }

        private void SellTarget(Player player)
        {
            var it = _sellTarget;
            int worth = it.TotalWorth;
            string name = Species.SafeName(it);
            _sellTriedAt[it] = Time.time;
            _sellTries[it] = (_sellTries.TryGetValue(it, out int n) ? n : 0) + 1;

            if (_sellTries[it] >= 2 && !Net.IsHost)
            {
                FishMonController.ShowToast($"{Mon.DisplayName} couldn't sell that fish. The host needs the FishMon mod for Merchant to work in their world.", new Color(1f, 0.7f, 0.4f), 6f);
                return;
            }

            bool ok = Net.IsHost ? Merchant.SellOnServer(it, player) : Net.SendSell(it);
            if (!ok) return;
            Effects(transform.position, "Poof", false);
            FishMonController.AddFloat(transform.position + Vector3.up * (HalfHeight + 0.8f), $"+${worth}", new Color(1f, 0.85f, 0.3f));
            FishMonController.ShowToast($"{Mon.DisplayName} sold {name} for ${worth}.", new Color(1f, 0.85f, 0.4f), 3f);
        }
    }

    public static class Merchant
    {
        public static Vector3 ItemPos(Item it) => it.Rig ? it.Rig.worldCenterOfMass : it.transform.position;

        // what a Merchant will sell, lying on the ground near you:
        //  - dead creatures you dropped (but not ones you hooked and haven't picked up yet, those are catches!)
        //  - loot nobody has picked up yet: dead creatures (baby piranhas...) and boss meat chunks
        // never: tools, weapons, boss trophies, other players' drops, or anything cooking on a grill
        public static bool CanSell(Item it, Player owner)
        {
            if (!it || it.IsDestroying || it.IsDeinitializing || it.DeadPlayer) return false;
            if (it.Holder || it.HasPlayerHolder || it.BirdHolder || it.AttachedRod) return false;
            if (!owner || it.TotalWorth <= 0) return false;

            bool mine = it.LastHolder == owner;
            bool untouched = !it.LastHolder;
            if (!mine && !untouched) return false;

            if (it.Creature)
            {
                if (!it.Creature.IsDead) return false;
                if (HookPatch.HookedByMe.Contains(it.Creature)) return false; // not caught yet
            }
            else if (!IsMeat(it)) return false;

            return !OnGrill(it);
        }

        // boss meat chunks are plain items (not creatures). Recognised by their prefab name.
        private static readonly HashSet<string> _loggedNames = new HashSet<string>();
        private static bool IsMeat(Item it)
        {
            if (it.GetType() != typeof(Item) || it.Type == ItemType.Weapon) return false;
            string name = it.gameObject.name ?? "";
            if (name.IndexOf("meat", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            // so we can see what else is lying around if something should be sellable but isn't
            if (_loggedNames.Add(name)) Plugin.Log.LogInfo($"Merchant ignores item '{name}' (${it.TotalWorth}).");
            return false;
        }

        // host only: same as the sell box (adds the money), then removes the item with the game's normal poof
        public static bool SellOnServer(Item it, Player owner)
        {
            if (!MoneyManager.Instance || !MoneyManager.Instance.IsServerInitialized) return false;
            if (!CanSell(it, owner)) return false;
            try
            {
                MoneyManager.SellItem(it);
                it.DestroyItem((byte)DestroyReason.Default); // (the NPC reason needs a real NPC id, otherwise the item never despawns)
                return true;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("FishMon Merchant couldn't sell: " + e.Message);
                return false;
            }
        }

        // host: a guest's Merchant asked to sell an item
        public static void OnSellRequest(NetworkConnection from, int objectId)
        {
            var nm = FishNet.InstanceFinder.NetworkManager;
            if (!nm || !nm.ServerManager.Objects.Spawned.TryGetValue(objectId, out var nob) || !nob) return;
            var it = nob.GetComponent<Item>();
            if (!it) it = nob.GetComponentInChildren<Item>();
            var seller = PlayerManager.Players.Find(p => p && p.Owner != null && p.Owner.ClientId == from.ClientId);
            if (!it || !seller) return;
            SellOnServer(it, seller); // CanSell checks it's the guest's own drop or untouched loot
        }

        // grills keep their items in a private list
        private static GrillBox[] _grills;
        private static float _grillsAt = -999f;

        private static bool OnGrill(Item it)
        {
            if (Time.time - _grillsAt > 10f) { _grills = Object.FindObjectsByType<GrillBox>(); _grillsAt = Time.time; }
            if (_grills == null) return false;
            foreach (var g in _grills)
            {
                if (!g) continue;
                var items = Traverse.Create(g).Field("_items").GetValue<List<Item>>();
                if (items != null && items.Contains(it)) return true;
            }
            return false;
        }
    }
}
