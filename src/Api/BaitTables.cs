// Adapted from MoreFishAPI by hiccup444 (MIT License, see src/Api/LICENSE-MoreFishAPI.txt).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HowToFishMon.Api
{
    internal static class BaitTables
    {
        private static readonly HashSet<object> Added = new HashSet<object>();
        private static readonly Dictionary<string, Fishable> Samples = new Dictionary<string, Fishable>(StringComparer.OrdinalIgnoreCase);

        public static Fishable SampleFor(string key) => !string.IsNullOrEmpty(key) && Samples.TryGetValue(key, out var f) ? f : null;

        public static void Inject(List<SpeciesDef> defs, FieldInfo allBaits)
        {
            var weightsF = AccessTools.Field(typeof(BaitInfo), "_itemWeights");
            var fishableF = AccessTools.Field(typeof(ItemInfoWeight), "fishable");
            var valueF = AccessTools.Field(typeof(ItemInfoWeight), "_weight");
            var itemF = AccessTools.Field(typeof(Fishable), "_itemToSpawn");

            var baits = allBaits.GetValue(null) as IList;
            if (baits == null || baits.Count == 0) { Plugin.Log.LogWarning("No baits loaded yet, so custom species weren't added to any catch table."); return; }

            int additions = 0;
            var names = new List<string>();
            foreach (var o in baits)
            {
                if (!(o is BaitInfo bait)) continue;
                names.Add(bait.name);
                if (!(weightsF.GetValue(bait) is IList weights) || weights.Count == 0) continue; // baits that catch nothing stay that way

                float baseline = Baseline(weights, valueF);
                foreach (var d in defs)
                {
                    if (!d.Live || !Matches(d, bait)) continue;
                    var item = GameInfo.IDToItem(d.RuntimeItemId);
                    if (item == null) continue;

                    var fishable = ScriptableObject.CreateInstance<Fishable>();
                    fishable.name = "FishMon_" + d.Key;
                    itemF.SetValue(fishable, item);
                    UnityEngine.Object.DontDestroyOnLoad(fishable);

                    object w = Activator.CreateInstance(typeof(ItemInfoWeight), true);
                    fishableF.SetValue(w, fishable);
                    valueF.SetValue(w, baseline * d.Rarity * Mathf.Max(0f, Plugin.CustomFishRarity.Value));
                    weights.Add(w);
                    Added.Add(w);
                    Samples[d.Key] = fishable;
                    additions++;
                }
            }
            Plugin.Log.LogInfo($"Custom species added to catch tables {additions} time(s). Bait names for BaitFilter: {string.Join(", ", names)}");
        }

        private static float Baseline(IList weights, FieldInfo valueF)
        {
            float total = 0f; int n = 0;
            foreach (var w in weights)
            {
                if (Added.Contains(w)) continue;
                if (valueF.GetValue(w) is float f && f > 0f) { total += f; n++; }
            }
            return n > 0 ? total / n : 1f;
        }

        // boss lures summon bosses and the other two are developer test baits: never on these unless asked for by name
        private static readonly string[] SkippedByDefault = { "boss", "testing", "fishbucket" };

        private static bool Matches(SpeciesDef d, BaitInfo bait)
        {
            string b = bait.name.Replace(" ", "").ToLowerInvariant();
            if (string.IsNullOrEmpty(d.BaitFilter)) return !SkippedByDefault.Any(b.Contains);
            foreach (var raw in d.BaitFilter.Split(','))
            {
                string want = raw.Trim().Replace(" ", "").ToLowerInvariant();
                if (want.Length > 0 && b.Contains(want)) return true;
            }
            return false;
        }
    }
}
