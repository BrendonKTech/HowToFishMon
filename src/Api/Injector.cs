// Adapted from MoreFishAPI by hiccup444 (MIT License, see src/Api/LICENSE-MoreFishAPI.txt).
// Fish are clones of game creatures: their FishNet code is generated at the game's build and can't be made by a mod.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Bootstrap;
using FishNet;
using FishNet.Managing.Object;
using FishNet.Object;
using HarmonyLib;
using UnityEngine;

namespace HowToFishMon.Api
{
    internal static class Injector
    {
        // MoreFishAPI uses collection 41
        private const ushort CollectionId = 42;
        private const string MoreFishGuid = "dazed.howtofish.morefishapi";

        private static FieldInfo _allItems, _idToSpawnable, _nameToSpawnable, _allCreatures, _allBaits;
        private static FieldInfo _itemId, _itemWorth, _itemMesh, _dripMesh, _excludeFromJournal;

        private static readonly Dictionary<byte, SpeciesDef> ById = new Dictionary<byte, SpeciesDef>();
        private static readonly Dictionary<string, Creature> Candidates = new Dictionary<string, Creature>(StringComparer.OrdinalIgnoreCase);
        private static GameObject _nursery;
        private static bool _resolved, _resolvedOk, _dirty, _attempted;
        private static float _firstTry;

        public static bool AlreadyBuilt => _attempted;
        public static bool Succeeded { get; private set; }

        public static SpeciesDef FromItem(Item item) => item != null ? FromId(item.ID) : null;
        public static SpeciesDef FromId(byte id) => ById.TryGetValue(id, out var d) ? d : null;

        public static bool Resolve()
        {
            if (_resolved) return _resolvedOk;
            _resolved = true;

            _allItems = AccessTools.Field(typeof(GameInfo), "_allItems");
            _idToSpawnable = AccessTools.Field(typeof(GameInfo), "_idToSpawnable");
            _nameToSpawnable = AccessTools.Field(typeof(GameInfo), "_nameToSpawnable");
            _allCreatures = AccessTools.Field(typeof(GameInfo), "_allCreatures");
            _allBaits = AccessTools.Field(typeof(GameInfo), "_allBaits");
            _itemId = AccessTools.Field(typeof(Item), "_id");
            _itemWorth = AccessTools.Field(typeof(Item), "_worth");
            _itemMesh = AccessTools.Field(typeof(Item), "_mesh");
            _dripMesh = AccessTools.Field(typeof(Creature), "_dripMesh");
            _excludeFromJournal = AccessTools.Field(typeof(Creature), "_excludeFromJournal");

            var required = new Dictionary<string, FieldInfo>
            {
                { "GameInfo._allItems", _allItems }, { "GameInfo._allCreatures", _allCreatures }, { "GameInfo._allBaits", _allBaits },
                { "Item._id", _itemId },
                { "BaitInfo._itemWeights", AccessTools.Field(typeof(BaitInfo), "_itemWeights") },
                { "ItemInfoWeight.fishable", AccessTools.Field(typeof(ItemInfoWeight), "fishable") },
                { "ItemInfoWeight._weight", AccessTools.Field(typeof(ItemInfoWeight), "_weight") },
                { "Fishable._itemToSpawn", AccessTools.Field(typeof(Fishable), "_itemToSpawn") },
            };
            foreach (var p in required)
                if (p.Value == null) { Plugin.Log.LogError($"Couldn't find {p.Key}. Custom species can't be added to this version of the game."); return false; }

            if (_allItems.FieldType.IsGenericType && _allItems.FieldType.GetGenericArguments()[0] != typeof(byte))
            {
                Plugin.Log.LogError("The game no longer keys items by byte, so custom species won't be added.");
                return false;
            }
            return _resolvedOk = true;
        }

        public static void MarkDirty() { if (!_attempted) _dirty = true; }

        public static void TryBuild()
        {
            if (!_dirty || _attempted || !Resolve()) return;
            if (!(_allItems.GetValue(null) is IDictionary items) || items.Count == 0) return;
            if (InstanceFinder.NetworkManager == null) return;
            if (_firstTry <= 0f) _firstTry = Time.realtimeSinceStartup;
            if (!MoreFishApiDone()) return; // let MoreFishAPI take its ids first, so the result is the same on every machine

            Creature fallback = FindTemplates(items);
            if (fallback == null)
            {
                if (Time.realtimeSinceStartup - _firstTry > 10f)
                {
                    _attempted = true;
                    Plugin.Log.LogError("No ordinary fish found to use as a template, so no custom species were added.");
                    FishMonAPI.RaiseBuilt();
                }
                return;
            }

            _dirty = false;
            _attempted = true;
            try { Succeeded = Build(items, fallback); }
            catch (Exception e) { Plugin.Log.LogError("Failed to add custom species: " + e); }
            finally { FishMonAPI.RaiseBuilt(); }
        }

        private static bool MoreFishApiDone()
        {
            if (!Chainloader.PluginInfos.TryGetValue(MoreFishGuid, out var info) || info.Instance == null) return true;
            try
            {
                var t = info.Instance.GetType().Assembly.GetType("MoreFishAPI.Injector");
                var p = t?.GetProperty("AlreadyBuilt", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (p == null) return true;
                // it never builds if nobody registered a fish with it, so don't wait forever
                return (bool)p.GetValue(null) || Time.realtimeSinceStartup - _firstTry > 15f;
            }
            catch { return true; }
        }

        // returns Cod (or any) as the fallback
        private static Creature FindTemplates(IDictionary items)
        {
            Candidates.Clear();
            foreach (DictionaryEntry e in items)
            {
                if (!(e.Value is Item item) || FromId(item.ID) != null) continue;
                var c = item.GetComponent<Creature>();
                if (c == null || c.BossType != BossType.None || c.ExcludeFromJournal || item.GetComponent<NetworkObject>() == null) continue;
                if (item.GetComponentsInChildren<MeshFilter>(true).Length == 0 && item.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == 0) continue;
                if (c.name.StartsWith("MoreFish_")) continue; // not another mod's custom fish
                Candidates[c.name] = c;
            }
            if (Candidates.Count == 0) return null;

            var names = new List<string>(Candidates.Keys);
            names.Sort(string.CompareOrdinal);
            Plugin.Log.LogInfo("Templates for custom species: " + string.Join(", ", names));
            if (Candidates.TryGetValue("Cod", out var cod)) return cod;
            foreach (var c in Candidates.Values) return c;
            return null;
        }

        private static Creature TemplateFor(SpeciesDef d, Creature fallback)
        {
            if (!string.IsNullOrEmpty(d.Template) && Candidates.TryGetValue(d.Template, out var c)) return c;
            Plugin.Log.LogWarning($"Species '{d.Key}' asked for template '{d.Template}', which this game doesn't have. Using '{fallback.name}'.");
            return fallback;
        }

        private static GameObject Nursery()
        {
            if (_nursery == null)
            {
                _nursery = new GameObject("FishMon_SpeciesTemplates");
                UnityEngine.Object.DontDestroyOnLoad(_nursery);
                _nursery.SetActive(false); // clones sit here inactive, like prefabs
            }
            return _nursery;
        }

        private static bool Build(IDictionary items, Creature fallback)
        {
            var ordered = FishMonAPI.Ordered();
            if (ordered.Count == 0) return false;
            var assigned = IdMap.Assign(ordered, items);
            if (assigned.Count == 0) return false;

            var collection = InstanceFinder.NetworkManager.GetPrefabObjects<SinglePrefabObjects>(CollectionId, true);
            if (collection == null) { Plugin.Log.LogError("FishNet has no prefab collection for us, so custom species can't be spawned."); return false; }
            if (collection.GetObjectCount() > 0) { Plugin.Log.LogError($"FishNet collection {CollectionId} is already used by something else, so custom species were not added."); return false; }

            var built = new Dictionary<byte, GameObject>();
            var report = new List<string>();
            foreach (var d in ordered)
            {
                if (!assigned.TryGetValue(d.Key, out byte id)) continue;
                var basis = TemplateFor(d, fallback);
                var clone = UnityEngine.Object.Instantiate(basis.gameObject, Nursery().transform);
                clone.name = "FishMon_" + d.Key;
                var item = clone.GetComponent<Item>();
                var creature = clone.GetComponent<Creature>();

                _itemId.SetValue(item, id);
                _itemWorth?.SetValue(item, d.Worth);
                _excludeFromJournal?.SetValue(creature, !(d.InJournal && Plugin.CustomFishInJournal.Value));

                Looks.Apply(clone, d, basis, _itemMesh, _dripMesh);
                ApplyBehaviour(clone, d, creature, basis);
                if (d.Evolutions != null)
                    foreach (var s in d.Evolutions)
                        if (s?.Prefab != null)
                        {
                            try { s.Visual = Looks.PrepareStage(s, d, basis, Nursery().transform); }
                            catch (Exception e) { Plugin.Log.LogWarning($"Species '{d.Key}': couldn't prepare the {s.Name} model: {e.Message}"); }
                        }

                if (clone.GetComponent<NetworkObject>() == null) { UnityEngine.Object.Destroy(clone); continue; }
                built[id] = clone;
                d.RuntimeItemId = id;
                report.Add($"{d.Key}={id} on {basis.name}");
            }
            if (built.Count == 0) return false;

            if (!SeedCollection(collection, built))
            {
                collection.Clear();
                UnityEngine.Object.Destroy(_nursery);
                Plugin.Log.LogError("Custom species weren't added: they couldn't be registered safely for multiplayer.");
                return false;
            }

            Publish(items, built, ordered, assigned);
            Plugin.Log.LogInfo($"Added {built.Count} custom species: {string.Join(", ", report)}");
            Plugin.Log.LogInfo($"Species checksum {IdMap.Checksum(assigned)}. Everyone in a lobby must show the same one, or fish won't match.");
            return true;
        }

        // slot i = id 128+i on every machine, unclaimed ids get an empty placeholder
        private static bool SeedCollection(PrefabObjects collection, Dictionary<byte, GameObject> built)
        {
            var slots = new List<NetworkObject>(IdMap.Slots);
            for (int i = 0; i < IdMap.Slots; i++)
            {
                byte id = (byte)(IdMap.Lowest + i);
                if (built.TryGetValue(id, out var real)) { slots.Add(real.GetComponent<NetworkObject>()); continue; }
                var filler = new GameObject("FishMon_Unused_" + id);
                filler.transform.SetParent(Nursery().transform, false);
                slots.Add(filler.AddComponent<NetworkObject>());
            }
            collection.AddObjects(slots, false, true);
            for (int i = 0; i < slots.Count; i++)
                if (slots[i] == null || slots[i].SpawnableCollectionId != CollectionId || slots[i].PrefabId != i)
                {
                    Plugin.Log.LogError($"FishNet didn't take slot {i}, so custom species aren't safe to spawn.");
                    return false;
                }
            return true;
        }

        private static void Publish(IDictionary items, Dictionary<byte, GameObject> built, List<SpeciesDef> ordered, Dictionary<string, byte> assigned)
        {
            var idToSpawnable = _idToSpawnable?.GetValue(null) as IDictionary;
            var nameToSpawnable = _nameToSpawnable?.GetValue(null) as IDictionary;
            var creatures = _allCreatures.GetValue(null) as IList;

            foreach (var d in ordered)
            {
                if (!assigned.TryGetValue(d.Key, out byte id) || !built.TryGetValue(id, out var clone)) continue;
                var item = clone.GetComponent<Item>();
                var creature = clone.GetComponent<Creature>();
                items[id] = item;
                if (idToSpawnable != null && !idToSpawnable.Contains(id)) idToSpawnable[id] = item;
                string nameKey = "fishmon_" + d.Key.Replace(" ", "").ToLowerInvariant();
                if (nameToSpawnable != null && !nameToSpawnable.Contains(nameKey)) nameToSpawnable[nameKey] = item;
                if (d.InJournal && Plugin.CustomFishInJournal.Value && creatures != null && !creatures.Contains(creature)) creatures.Add(creature);
                d.Live = true;
                ById[id] = d;
            }
            BaitTables.Inject(ordered, _allBaits);
        }

        private static void SetFloat(object target, string field, float v) { if (v >= 0f && target != null) AccessTools.Field(target.GetType(), field)?.SetValue(target, v); }
        private static void SetInt(object target, string field, int v) { if (v >= 0 && target != null) AccessTools.Field(target.GetType(), field)?.SetValue(target, v); }

        private static void ApplyBehaviour(GameObject clone, SpeciesDef d, Creature creature, Creature basis)
        {
            if (!d.KeepTemplateSkin) AccessTools.Field(typeof(Item), "_skinPreset")?.SetValue(clone.GetComponent<Item>(), null);
            SetInt(creature, "_maxHp", d.Health);
            SetInt(creature, "_fullnessToRestore", d.FoodValue);
            SetInt(creature, "_hpToRestore", d.HealValue);
            if (d.Endangered.HasValue) AccessTools.Field(typeof(Creature), "_isEndangered")?.SetValue(creature, d.Endangered.Value);

            var swimmer = clone.GetComponent<Fish>();
            var pull = swimmer != null ? AccessTools.Field(typeof(Fish), "_towardsWaterForce") : null;
            if (pull != null && !Mathf.Approximately(d.WaterPull, 1f))
            {
                float was = (float)pull.GetValue(swimmer);
                pull.SetValue(swimmer, was * Mathf.Max(0f, d.WaterPull));
                Plugin.Log.LogInfo($"Species '{d.Key}': pull towards the water {was:0.##} -> {was * d.WaterPull:0.##}.");
            }

            var hunter = clone.GetComponent<AttackingFish>();
            if (d.Hostile == true && hunter == null)
                Plugin.Log.LogWarning($"Species '{d.Key}' wants to be hostile, but '{basis.name}' doesn't attack. Pick a hostile template instead.");
            if (d.Hostile == false && hunter != null)
            {
                SetFloat(hunter, "_towardsPlayerForce", 0f);
                SetFloat(hunter, "_airTowardsPlayerForce", 0f);
                SetFloat(hunter, "_underwaterAttackForceMulti", 0f);
                AccessTools.Field(typeof(AttackingFish), "_timeBetweenDamage")?.SetValue(hunter, 999999f);
            }
        }
    }
}
