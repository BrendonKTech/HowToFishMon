using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace HowToFishMon
{
    // party + Box, saved to BepInEx/config/FishMon_save.json
    // worlds that chose "start fresh" get their own file in BepInEx/config/FishMon_worlds/
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
            public Dictionary<string, int> Bag = new Dictionary<string, int>(); // held items you own but haven't given out, by name
            public List<string> ClaimedRewards = new List<string>(); // Fishdex milestones already claimed, by id
            public Dictionary<int, int> Badges = new Dictionary<int, int>(); // boss creature id -> best badge (BadgeTier)
        }

        public static Dictionary<int, int> BadgeTiers => _data.Badges ?? (_data.Badges = new Dictionary<int, int>());

        public static void SetBadge(int bossId, int tier)
        {
            BadgeTiers[bossId] = tier;
            Save();
        }

        // how many FishMon you can carry: the setting, plus a slot from the badge perk
        public static int PartyLimit => Plugin.MaxPartySize.Value + HowToFishMon.Badges.ExtraPartySlots;

        private static SaveData _data = new SaveData();

        public static List<FishMon> Party => _data.Party;
        public static List<FishMon> Box => _data.Box;
        public static int TotalCaught => _data.TotalCaught;
        public static Dictionary<int, DexEntry> Dex => _data.Dex ?? (_data.Dex = new Dictionary<int, DexEntry>());
        private static Dictionary<string, int> Bag => _data.Bag ?? (_data.Bag = new Dictionary<string, int>());

        private static List<string> Claimed => _data.ClaimedRewards ?? (_data.ClaimedRewards = new List<string>());
        public static bool IsClaimed(string rewardId) => Claimed.Contains(rewardId);

        public static void MarkClaimed(string rewardId)
        {
            if (!Claimed.Contains(rewardId)) Claimed.Add(rewardId);
            Save();
        }

        public static int BagCount(HeldItem i) => Bag.TryGetValue(i.ToString(), out int n) ? n : 0;

        public static void AddToBag(HeldItem i, int n = 1)
        {
            if (i == HeldItem.None || n <= 0) return;
            Bag[i.ToString()] = BagCount(i) + n;
            Save();
        }

        public static bool TakeFromBag(HeldItem i)
        {
            int n = BagCount(i);
            if (n <= 0) return false;
            if (n == 1) Bag.Remove(i.ToString()); else Bag[i.ToString()] = n - 1;
            return true;
        }

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

        private static string WorldsDir => Path.Combine(BepInEx.Paths.ConfigPath, "FishMon_worlds");
        private static string ChoicesPath => Path.Combine(WorldsDir, "choices.json");

        // null = the shared save every world uses by default
        public static string CurrentWorld { get; private set; }

        private static string FilePath => CurrentWorld == null
            ? Path.Combine(BepInEx.Paths.ConfigPath, "FishMon_save.json")
            : Path.Combine(WorldsDir, SafeFileName(CurrentWorld) + ".json");

        private static string SafeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
            return name.Trim().Length == 0 ? "_unnamed" : name.Trim();
        }

        // world name -> true if it has its own FishMon, false if it uses the shared ones
        private static Dictionary<string, bool> _choices;

        private static Dictionary<string, bool> Choices
        {
            get
            {
                if (_choices != null) return _choices;
                try
                {
                    if (File.Exists(ChoicesPath))
                        _choices = JsonConvert.DeserializeObject<Dictionary<string, bool>>(File.ReadAllText(ChoicesPath));
                }
                catch (Exception e) { Plugin.Log.LogWarning("Couldn't read FishMon world choices: " + e.Message); }
                return _choices ?? (_choices = new Dictionary<string, bool>());
            }
        }

        // has this world been asked yet? ownSave = what they picked
        public static bool HasChoice(string world, out bool ownSave) => Choices.TryGetValue(world, out ownSave);

        public static void SetChoice(string world, bool ownSave)
        {
            Choices[world] = ownSave;
            try
            {
                Directory.CreateDirectory(WorldsDir);
                File.WriteAllText(ChoicesPath, JsonConvert.SerializeObject(Choices, Formatting.Indented));
            }
            catch (Exception e) { Plugin.Log.LogError("Couldn't save FishMon world choices: " + e.Message); }
        }

        // switch to a world's own save (or null for the shared one). saves the current one first
        public static void UseSave(string world)
        {
            if (world == CurrentWorld) return;
            Save();
            CurrentWorld = world;
            Load();
            Fishdex.Refill();
            Plugin.Log.LogInfo(world == null ? "Using the shared FishMon save" : $"Using this world's own FishMon save ({world})");
        }

        public static void Load()
        {
            Dirty = false;
            _data = new SaveData();
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
            if (m.Ability == Ability.None || Abilities.Get(m.Ability) == null)
            {
                m.Ability = Abilities.Roll(new System.Random(), m.Shiny); // FishMon caught before abilities existed
                Dirty = true;
            }
        }

        public static void Save()
        {
            Dirty = false;
            try
            {
                _data.HpMult = Plugin.FishHpMultiplier.Value;
                string tmp = FilePath + ".tmp";
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
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
            bool toParty = Party.Count < PartyLimit;
            if (toParty) Party.Add(mon); else Box.Add(mon);
            Save();
            return toParty;
        }

        // a FishMon you got in a trade: counts for the Fishdex, but not as one you caught yourself
        public static bool AddTraded(FishMon mon)
        {
            Fishdex.RecordCatch(mon);
            bool toParty = Party.Count < PartyLimit;
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
            if (!Box.Contains(mon) || Party.Count >= PartyLimit) return false;
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
