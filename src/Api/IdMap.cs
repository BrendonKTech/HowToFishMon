// Adapted from MoreFishAPI by hiccup444 (MIT License, see src/Api/LICENSE-MoreFishAPI.txt).
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace HowToFishMon.Api
{
    // ids are a byte: custom species get 128-250 (255 is the boat), hashed from the Key so every machine agrees
    internal static class IdMap
    {
        public const byte Lowest = 128;
        public const byte Highest = 250;
        public const int Slots = Highest - Lowest + 1;

        private class FileData { public int Version = 1; public Dictionary<string, byte> Assigned = new Dictionary<string, byte>(); }
        private static FileData _file;

        private static string FilePath => Path.Combine(Application.persistentDataPath, "FishMon", "species-ids.json");

        public static byte Preferred(string key)
        {
            unchecked
            {
                uint h = 2166136261u;
                foreach (char c in key.ToLowerInvariant()) { h ^= c; h *= 16777619u; }
                return (byte)(Lowest + (int)(h % (uint)Slots));
            }
        }

        public static Dictionary<string, byte> Assign(List<SpeciesDef> defs, IDictionary takenItems)
        {
            Load();
            var result = new Dictionary<string, byte>(StringComparer.OrdinalIgnoreCase);
            var taken = new HashSet<byte>();
            foreach (var d in defs)
            {
                byte start = Preferred(d.Key), chosen = 0;
                for (int step = 0; step < Slots; step++)
                {
                    byte c = (byte)(Lowest + (start - Lowest + step) % Slots);
                    if (!taken.Contains(c) && !takenItems.Contains(c)) { chosen = c; break; }
                }
                if (chosen == 0) { Plugin.Log.LogError($"No free item id left for species '{d.Key}'. Only {Slots} custom fish can exist across all fish mods."); continue; }
                result[d.Key] = chosen;
                taken.Add(chosen);
                if (chosen != start)
                    Plugin.Log.LogWarning($"Species '{d.Key}' wanted id {start} but it was taken, so it got {chosen}. Players with a different set of fish mods may not match.");
            }
            ReportDrift(result);
            return result;
        }

        public static string Checksum(Dictionary<string, byte> assigned)
        {
            var keys = new List<string>(assigned.Keys);
            keys.Sort(StringComparer.OrdinalIgnoreCase);
            unchecked
            {
                uint h = 2166136261u;
                foreach (var k in keys)
                {
                    foreach (char c in k.ToLowerInvariant()) { h ^= c; h *= 16777619u; }
                    h ^= assigned[k]; h *= 16777619u;
                }
                return h.ToString("X8");
            }
        }

        private static void Load()
        {
            if (_file != null) return;
            try
            {
                if (File.Exists(FilePath))
                    _file = JsonConvert.DeserializeObject<FileData>(File.ReadAllText(FilePath));
            }
            catch (Exception e) { Plugin.Log.LogWarning("Couldn't read the species id record, it will be rewritten: " + e.Message); }
            if (_file?.Assigned == null) _file = new FileData();
        }

        private static void ReportDrift(Dictionary<string, byte> assigned)
        {
            bool changed = false;
            foreach (var p in assigned)
            {
                if (_file.Assigned.TryGetValue(p.Key, out byte was) && was != p.Value)
                    Plugin.Log.LogWarning($"Species '{p.Key}' had id {was} last time and {p.Value} now. Your FishMon are fine, but that fish in the game's own inventory won't come back as the same fish.");
                if (!_file.Assigned.TryGetValue(p.Key, out byte old) || old != p.Value) { _file.Assigned[p.Key] = p.Value; changed = true; }
            }
            if (!changed) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                File.WriteAllText(FilePath, JsonConvert.SerializeObject(_file, Formatting.Indented));
            }
            catch (Exception e) { Plugin.Log.LogWarning("Couldn't write the species id record: " + e.Message); }
        }
    }
}
