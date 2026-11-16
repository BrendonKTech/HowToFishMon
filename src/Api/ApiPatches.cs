// Adapted from MoreFishAPI by hiccup444 (MIT License, see src/Api/LICENSE-MoreFishAPI.txt).
using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace HowToFishMon.Api
{
    [HarmonyPatch(typeof(Item), nameof(Item.GetName))]
    internal static class ApiNamePatch
    {
        private static bool Prefix(Item __instance, ref string __result)
        {
            var d = Injector.FromItem(__instance);
            if (d == null) return true;
            __result = d.Name;
            return false;
        }
    }

    // the game's loader drops everything after an unknown item id, so strip a removed mod's items first
    [HarmonyPatch(typeof(PlayerInventory), "LoadFromSave")]
    internal static class ApiSaveGuardPatch
    {
        private static void Prefix(PlayerInventory __instance)
        {
            try
            {
                if (!(AccessTools.Field(typeof(PlayerInventory), "_toLoadFrom")?.GetValue(__instance) is SavedPlayer saved)) return;
                int removed = 0;
                var items = saved.InventoryItems;
                if (items != null)
                    for (int i = items.Count - 1; i >= 0; i--)
                        if (items[i] != null && items[i].Exists && GameInfo.GetSpawnable(items[i].ItemID) == null) { items.RemoveAt(i); removed++; }
                if (saved.HeldItem != null && saved.HeldItem.Exists && GameInfo.GetSpawnable(saved.HeldItem.ItemID) == null) { saved.HeldItem.Exists = false; removed++; }
                if (removed > 0) Plugin.Log.LogWarning($"This save holds {removed} item(s) from a fish mod that isn't installed. Skipped them so the rest of the inventory loads.");
            }
            catch (Exception e) { Plugin.Log.LogError("Couldn't check the saved inventory: " + e); }
        }
    }

    [HarmonyPatch(typeof(Creature), "SetDrip")]
    internal static class ApiShinyPatch
    {
        private static bool Prefix(Creature __instance) => Injector.FromItem(__instance)?.AllowShiny ?? true;
    }

    // picks what bites. Prefix: ForceNextCatch. Postfix: a custom species that doesn't live on this island
    // is swapped for a fresh pick from the same bait without it (the game itself only looks at the bait).
    [HarmonyPatch(typeof(CreatureManager), "GetRandomItem")]
    internal static class ApiCatchPickPatch
    {
        private static bool _forced;

        private static bool Prefix(ref Fishable __result)
        {
            if (FishMonAPI.Forced == null) return true;
            var sample = BaitTables.SampleFor(FishMonAPI.Forced);
            FishMonAPI.Forced = null;
            if (sample == null) return true;
            __result = sample;
            _forced = true;
            return false;
        }

        private static void Postfix(List<ItemInfoWeight> weights, ref Fishable __result)
        {
            if (_forced) { _forced = false; return; }
            if (__result == null || weights == null) return;
            int island = Presets.CurrentIsland + 1;
            if (Allowed(__result, island)) return;

            float total = 0f;
            foreach (var w in weights) if (Allowed(w.Fishable, island)) total += w.Weight;
            if (total <= 0f) return;
            float roll = UnityEngine.Random.Range(0f, total);
            foreach (var w in weights)
            {
                if (!Allowed(w.Fishable, island)) continue;
                if ((roll -= w.Weight) <= 0f) { __result = w.Fishable; return; }
            }
        }

        private static bool Allowed(Fishable f, int island)
        {
            var d = f != null ? Injector.FromItem(f.ItemToSpawn) : null;
            return d == null || d.BitesOn(island);
        }
    }

    internal static class ApiPreviews
    {
        private static readonly Dictionary<Renderer, Material> Painted = new Dictionary<Renderer, Material>();

        public static void Paint(object slot, Item item)
        {
            var r = AccessTools.Field(slot.GetType(), "_renderer")?.GetValue(slot) as Renderer;
            if (r == null) r = (AccessTools.Field(slot.GetType(), "_filter")?.GetValue(slot) as MeshFilter)?.GetComponent<Renderer>();
            if (r == null) return;

            var d = Injector.FromItem(item);
            if (d?.RuntimeMaterial == null)
            {
                Painted.Remove(r); // the game's own item: it set its own material
                return;
            }
            if (Painted.TryGetValue(r, out var already) && already == d.RuntimeMaterial) return;
            r.sharedMaterial = d.RuntimeMaterial;
            Painted[r] = d.RuntimeMaterial;
            if (Painted.Count > 128)
            {
                var dead = new List<Renderer>();
                foreach (var k in Painted.Keys) if (k == null) dead.Add(k);
                foreach (var k in dead) Painted.Remove(k);
            }
        }
    }

    [HarmonyPatch(typeof(InventorySlot), "ApplySkin")]
    internal static class ApiInventoryIconPatch
    {
        private static void Postfix(InventorySlot __instance, Item item) => ApiPreviews.Paint(__instance, item);
    }

    [HarmonyPatch(typeof(JournalSlot), "SetCreature")]
    internal static class ApiJournalIconPatch
    {
        private static void Postfix(JournalSlot __instance, Creature creature) => ApiPreviews.Paint(__instance, creature);
    }
}
