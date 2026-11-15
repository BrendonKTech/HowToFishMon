using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HowToFishMon
{
    // Haggler: anything you sell (sell box, fish quests) while it's out pays 25% more.
    // Selling only happens on the host, so the host adds the bonus for itself and for guests with a Haggler out.
    [HarmonyPatch(typeof(MoneyManager), nameof(MoneyManager.SellItem))]
    internal static class HagglerPatch
    {
        // the sell box can see the same item for a few frames: only pay the bonus once per item
        private static readonly HashSet<Item> _paid = new HashSet<Item>();
        private static bool _errorLogged;

        private static void Postfix(Item item)
        {
            try
            {
                if (!item || !MoneyManager.Instance || !MoneyManager.Instance.IsServerInitialized) return;
                var seller = item.LastHolder;
                if (!Abilities.PlayerHas(seller, Ability.Haggler)) return;
                _paid.RemoveWhere(i => !i);
                if (!_paid.Add(item)) return;

                int bonus = Mathf.RoundToInt(item.TotalWorth * Abilities.HagglerBonus);
                if (bonus <= 0) return;
                MoneyManager.AddMoney(bonus, seller, item.Currency);
                if (seller == Player.LocalPlayer)
                    FishMonController.ShowToast($"{FishMonController.Active.Mon.DisplayName} haggled an extra ${bonus}!", new Color(1f, 0.85f, 0.4f), 3f);
            }
            catch (System.Exception e)
            {
                if (!_errorLogged) Plugin.Log.LogWarning("FishMon Haggler failed: " + e.Message);
                _errorLogged = true;
            }
        }
    }
}
