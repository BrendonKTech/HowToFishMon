using System.Collections.Generic;
using HarmonyLib;

namespace HowToFishMon
{
    // remember every creature that gets hooked on the LOCAL player's rod
    [HarmonyPatch(typeof(Item), "InitializeBait")]
    internal static class HookPatch
    {
        public static readonly HashSet<Creature> HookedByMe = new HashSet<Creature>();

        private static void Postfix(Item __instance, FishingRod rod)
        {
            try
            {
                if (!rod || !rod.Holder || rod.Holder != Player.LocalPlayer) return;
                var c = __instance ? __instance.Creature : null;
                if (c) Fishdex.MarkSeen(c);
                if (c && HookedByMe.Add(c))
                {
                    string odds = "";
                    if (Fishdex.Catchable(c) && Plugin.CatchDifficulty.Value > 0f)
                        odds = $" Catch chance about {Catching.Estimate(c, Abilities.PlayerHas(rod.Holder, Ability.LuckyCharm)):P0}.";
                    FishMonController.ShowToast($"{Species.SafeName(c)} is hooked! Land it and pick it up (E) to catch it.{odds}", new UnityEngine.Color(0.7f, 0.9f, 1f), 3.5f);
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError("FishMon hook tracking failed: " + e);
            }
        }
    }

    // picking up a fish you hooked yourself = catching it. (The game only lets you pick up creatures once
    // they're dead, so we don't require it alive.)
    [HarmonyPatch(typeof(Item), nameof(Item.PickUp))]
    internal static class CatchPatch
    {
        // creatures already caught this session, so re-grabbing a fish doesn't duplicate it
        private static readonly HashSet<Creature> Caught = new HashSet<Creature>();

        private static void Postfix(Item __instance, Player player)
        {
            try
            {
                if (!player || player != Player.LocalPlayer) return;
                var c = __instance ? __instance.Creature : null;
                if (!c || c.Bird || c.BossType != BossType.None || (c.ExcludeFromJournal && Api.FishMonAPI.FromItem(c) == null)) return;
                if (Plugin.OnlyCatchHooked.Value && !HookPatch.HookedByMe.Contains(c)) return;
                if (!Caught.Add(c)) return;
                HookPatch.HookedByMe.Remove(c);

                bool lucky = Abilities.PlayerHas(player, Ability.LuckyCharm);
                var mon = Species.CreateFromCreature(c, lucky);

                // it can break free (one try per fish: you keep it as a normal fish)
                float chance = Catching.Chance(c, mon.Level, lucky);
                if (chance < 1f && UnityEngine.Random.value >= chance) { Catching.BrokeFree(c, chance); return; }

                bool newSpecies = (Fishdex.Get(c.ID)?.Caught ?? 0) == 0;
                int readyBefore = DexRewards.ReadyCount;
                bool party = FishMonStorage.Add(mon);
                FishMonController.OnCaught(mon, party);
                if (lucky) FishMonController.ShowToast($"{FishMonController.Active.Mon.DisplayName}'s Lucky Charm gave {mon.SpeciesName} a boost!", new UnityEngine.Color(1f, 0.75f, 0.3f), 4f);
                if (newSpecies) DexRewards.OnNewSpecies(c, readyBefore);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError("FishMon catch failed: " + e);
            }
        }
    }

    // freeze the player's movement/look/attacks while a FishMon menu is open
    [HarmonyPatch(typeof(Player), nameof(Player.BlockInputs), MethodType.Getter)]
    internal static class BlockInputsPatch
    {
        private static void Postfix(Player __instance, ref bool __result)
        {
            if (FishMonController.UIOpen && __instance == Player.LocalPlayer) __result = true;
        }
    }

    // while a FishMon menu is open, Esc closes that menu (FishMonController.Update) instead of also
    // opening the game's pause menu underneath it
    [HarmonyPatch(typeof(PauseManager), "PauseInput")]
    internal static class PauseInputPatch
    {
        private static bool Prefix() => !FishMonController.UIOpen || PauseManager.IsPaused;
    }

    // keep the mouse cursor free while a FishMon menu is open
    [HarmonyPatch(typeof(PlayerCamera), nameof(PlayerCamera.ToggleMouse))]
    internal static class ToggleMousePatch
    {
        private static void Prefix(ref bool unlock)
        {
            if (FishMonController.UIOpen) unlock = true;
        }
    }
}
