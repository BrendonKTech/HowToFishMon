using System;
using HarmonyLib;

namespace HowToFishMon
{
    // Angler: your bait's "time in the water" counts up 50% faster, so fish bite sooner.
    // The host decides when fish bite, so it speeds up its own bait and the bait of guests with an Angler out.
    [HarmonyPatch(typeof(Bait), "IncreaseUnderwaterTime")]
    internal static class AnglerPatch
    {
        private static Action<Bait, float> _setTime;
        private static bool _broken;

        private static void Prefix(Bait __instance, out float __state) => __state = __instance.TimeUnderWater;

        private static void Postfix(Bait __instance, float __state)
        {
            if (_broken) return;
            try
            {
                float gained = __instance.TimeUnderWater - __state;
                if (gained <= 0f || !Net.IsHost) return;
                var rod = __instance.FishingRod;
                if (!rod || !Abilities.PlayerHas(rod.Holder, Ability.Angler)) return;

                if (_setTime == null)
                    _setTime = AccessTools.MethodDelegate<Action<Bait, float>>(AccessTools.PropertySetter(typeof(Bait), nameof(Bait.TimeUnderWater)));
                _setTime(__instance, __instance.TimeUnderWater + gained * (Abilities.AnglerRate - 1f));
            }
            catch (Exception e)
            {
                _broken = true; // don't spam the log every physics frame
                Plugin.Log.LogWarning("FishMon Angler stopped working, the game probably updated: " + e.Message);
            }
        }
    }
}
