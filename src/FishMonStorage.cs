using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace HowToFishMon
{
    // party + Box, saved to BepInEx/config/FishMon_save.json
    public static class FishMonStorage
    {
        private class SaveData
        {
            public int Version = 2;
            public List<FishMon> Party = new List<FishMon>();
            public List<FishMon> Box = new List<FishMon>();
            public int TotalCaught;
            public Dictionary<int, DexEntry> Dex = new Dictionary<int, DexEntry>();
            public float HpMult = 1f; // FishHpMultiplier when this was saved
        }

        private static SaveData _data = new SaveData();

        public static List<FishMon> Party => _data.Party;
        public static List<FishMon> Box => _data.Box;
        public static int TotalCaught => _data.TotalCaught;
        public static Dictionary<int, DexEntry> Dex => _data.Dex ?? (_data.Dex = new Dictionary<int, DexEntry>());

        // set when something minor changed; the controller saves a few seconds later
        public static bool Dirty;
        public static void SaveSoon() => Dirty = true;

        public static FishMon Lead
        {
            get
            {
                foreach (var m in Party) if (!m.Fainted) return m;
                return null;
            }
        }

        private static string FilePath => Path.Combine(BepInEx.Paths.ConfigPath, "FishMon_save.json");

        public static void Load()
        {
            try
            {
                if (File.Exists(FilePath))
                    _data = JsonConvert.DeserializeObject<SaveData>(File.ReadAllText(FilePath)) ?? new SaveData();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Could not read FishMon save, backing it up and starting fresh: " + e.Message);
                try { File.Copy(FilePath, FilePath + ".broken", true); } catch { }
                _data = new SaveData();
            }
            // HP is stored as a number; if the HP multiplier changed since the last save, keep the same percentage
            float now = UnityEngine.Mathf.Max(0.05f, Plugin.FishHpMultiplier.Value), was = UnityEngine.Mathf.Max(0.05f, _data.HpMult);
            if (UnityEngine.Mathf.Abs(now - was) > 0.001f)
            {
                foreach (var m in Party) if (m.CurrentHp > 0) m.CurrentHp = System.Math.Max(1, (int)UnityEngine.Mathf.Round(m.CurrentHp * now / was));
                foreach (var m in Box) if (m.CurrentHp > 0) m.CurrentHp = System.Math.Max(1, (int)UnityEngine.Mathf.Round(m.CurrentHp * now / was));
            }
            foreach (var m in Party) Fix(m);
            foreach (var m in Box) Fix(m);
        }

        private static void Fix(FishMon m)
        {
            if (m.BaseStats == null || m.BaseStats.Length != 6) m.BaseStats = new[] { 50, 50, 50, 50, 50, 50 };
            if (m.IVs == null || m.IVs.Length != 6) m.IVs = new int[6];
            if (m.EVs == null || m.EVs.Length != 6) m.EVs = new int[6];
            if (m.CurrentHp < 0 || m.CurrentHp > m.MaxHp) m.CurrentHp = m.MaxHp;
        }

        public static void Save()
        {
            Dirty = false;
            try
            {
                _data.HpMult = Plugin.FishHpMultiplier.Value;
                string tmp = FilePath + ".tmp";
                File.WriteAllText(tmp, JsonConvert.SerializeObject(_data, Formatting.Indented));
                if (File.Exists(FilePath)) File.Delete(FilePath);
                File.Move(tmp, FilePath);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Could not save FishMon data: " + e.Message);
            }
        }

        // adds a new catch. Returns true if it went to the party, false if to the box.
        public static bool Add(FishMon mon)
        {
            _data.TotalCaught++;
            Fishdex.RecordCatch(mon);
            bool toParty = Party.Count < Plugin.MaxPartySize.Value;
            if (toParty) Party.Add(mon); else Box.Add(mon);
            Save();
            return toParty;
        }

        public static void Release(FishMon mon)
        {
            Party.Remove(mon);
            Box.Remove(mon);
            Save();
        }

        public static bool MoveToBox(FishMon mon)
        {
            if (!Party.Contains(mon) || Party.Count <= 1) return false;
            Party.Remove(mon);
            Box.Add(mon);
            Save();
            return true;
        }

        public static bool MoveToParty(FishMon mon)
        {
            if (!Box.Contains(mon) || Party.Count >= Plugin.MaxPartySize.Value) return false;
            Box.Remove(mon);
            Party.Add(mon);
            Save();
            return true;
        }

        public static void MakeLead(FishMon mon)
        {
            if (!Party.Remove(mon)) return;
            Party.Insert(0, mon);
            Save();
        }

        // restores HP of every FishMon that hasn't fainted. Fainted ones need a Revive.
        public static void HealAll()
        {
            foreach (var m in Party) if (!m.Fainted) m.HealFull();
            foreach (var m in Box) if (!m.Fainted) m.HealFull();
            Save();
        }
    }
}
