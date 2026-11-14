using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HowToFishMon
{
    // what you know about one species. Saved with your FishMon.
    public class DexEntry
    {
        public bool Seen;
        public int Caught;
        public bool DripCaught;
        public int BestIVs = -1;
        public int BestLevel;
        public string FirstCaught;
        public int Defeated;
    }

    // the FishMon encyclopedia: every creature in the game's journal, and what you've seen / caught
    public static class Fishdex
    {
        private static List<Creature> _species;

        // all journal creatures in the game's own order (fish by value, bosses last). Empty until the game has loaded.
        public static IReadOnlyList<Creature> All
        {
            get
            {
                if (_species != null && _species.Count > 0) return _species;
                _species = new List<Creature>();
                try
                {
                    for (int i = 0; i < GameInfo.AllCreatureCount; i++)
                    {
                        var c = GameInfo.GetCreature(i);
                        if (c) _species.Add(c);
                    }
                }
                catch (Exception e) { Plugin.Log.LogWarning("Couldn't read the creature list: " + e.Message); }
                if (_species.Count > 0) Backfill();
                return _species;
            }
        }

        private static readonly Dictionary<byte, Species.Info> InfoCache = new Dictionary<byte, Species.Info>();

        public static Species.Info InfoFor(Creature prefab)
        {
            if (!InfoCache.TryGetValue(prefab.ID, out var info))
                InfoCache[prefab.ID] = info = HowToFishMon.Species.Describe(prefab);
            return info;
        }

        public static bool Catchable(Creature c) => c && !c.Bird && c.BossType == BossType.None;

        public static DexEntry Get(int id) => FishMonStorage.Dex.TryGetValue(id, out var e) ? e : null;

        private static DexEntry GetOrAdd(int id)
        {
            if (!FishMonStorage.Dex.TryGetValue(id, out var e)) FishMonStorage.Dex[id] = e = new DexEntry();
            return e;
        }

        public static int Number(int id)
        {
            var list = All;
            for (int i = 0; i < list.Count; i++) if (list[i].ID == id) return i + 1;
            return 0;
        }

        public static int Total => All.Count(Catchable);
        public static int CaughtCount => All.Count(c => Catchable(c) && (Get(c.ID)?.Caught ?? 0) > 0);
        public static int SeenCount => All.Count(c => Catchable(c) && (Get(c.ID)?.Seen ?? false));

        // mark a creature as seen. Returns true the first time.
        public static bool MarkSeen(Creature c)
        {
            if (!c || c.ExcludeFromJournal) return false;
            var e = GetOrAdd(c.ID);
            if (e.Seen) return false;
            e.Seen = true;
            int n = Number(c.ID);
            if (n > 0)
                FishMonController.ShowToast($"Fishdex #{n:000}: {HowToFishMon.Species.SafeName(c)} seen!", new Color(0.75f, 0.8f, 1f), 3f);
            FishMonStorage.SaveSoon();
            return true;
        }

        // record a catch. Returns true if it's the first of its species.
        public static bool RecordCatch(FishMon m)
        {
            var e = GetOrAdd(m.SpeciesId);
            bool first = e.Caught == 0;
            e.Seen = true;
            e.Caught++;
            if (m.Shiny) e.DripCaught = true;
            e.BestIVs = Math.Max(e.BestIVs, m.IVTotal);
            e.BestLevel = Math.Max(e.BestLevel, m.Level);
            if (string.IsNullOrEmpty(e.FirstCaught)) e.FirstCaught = m.CaughtOn;
            return first;
        }

        public static void RecordDefeat(Creature c)
        {
            if (!c) return;
            var e = GetOrAdd(c.ID);
            e.Seen = true;
            e.Defeated++;
            FishMonStorage.SaveSoon();
        }

        // fill the dex from FishMon you already own and creatures the game's journal says you've killed
        private static void Backfill()
        {
            foreach (var m in FishMonStorage.Party.Concat(FishMonStorage.Box))
            {
                var e = GetOrAdd(m.SpeciesId);
                e.Seen = true;
                if (e.Caught == 0) e.Caught = 1;
                if (m.Shiny) e.DripCaught = true;
                e.BestIVs = Math.Max(e.BestIVs, m.IVTotal);
                e.BestLevel = Math.Max(e.BestLevel, m.Level);
                if (string.IsNullOrEmpty(e.FirstCaught)) e.FirstCaught = m.CaughtOn;
            }
            try
            {
                if (SaveManager.HasLocalSave)
                    foreach (var c in _species)
                        if (SaveManager.HasKilledCreature(c.ID, false)) GetOrAdd(c.ID).Seen = true;
            }
            catch (Exception e) { Plugin.Log.LogInfo("Couldn't read the game's journal: " + e.Message); }
            FishMonStorage.SaveSoon();
        }

        // learn-by-level list for a species' base form
        public static List<Move> MovePool(Species.Info info) => Moves.PoolFor(info.Type1, info.Type2);
    }
}
