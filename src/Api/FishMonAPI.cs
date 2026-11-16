// Adapted from MoreFishAPI by hiccup444 (MIT License, see src/Api/LICENSE-MoreFishAPI.txt).
// Changes for FishMon: species can be a recoloured template with no model, carry FishMon data
// (types, stats, evolutions), and are saved by Key so a shifted item id can't swap species.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace HowToFishMon.Api
{
    // one custom species. Register from your plugin's Awake; anything unset comes from the template.
    public class SpeciesDef
    {
        public string Key;              // unique, e.g. you.yourmod.thing. Saves depend on it: never change it once released
        public string Name;
        public string Description = "";

        // look: Prefab > Mesh > Texture/Material > Tint (recolour only)
        public string Template = "Cod"; // the game creature it's built on (body, physics, hostile or not). Names are logged at startup
        public Color? Tint;
        public GameObject Prefab;       // placed exactly as authored
        public Mesh Mesh;               // auto-fitted to the template
        public Texture Texture;
        public Material Material;
        public Vector3 MeshRotation = Vector3.zero;
        public float MeshScale = 1f;
        public bool AutoFit = true;
        // draw Prefab / stage models with the game's fish shader, using their texture as its colour palette
        public bool UseGameShader = true;
        // resize the template's colliders to the Prefab, so a differently shaped model doesn't sink into the ground
        public bool FitColliders = true;

        // in the game
        public int Worth = 10;
        public float Rarity = 1f;       // vs an average catch on the same bait: 1 = typical, 0.1 = a tenth as often
        public string BaitFilter = "";  // comma separated bait names, empty = every normal bait
        public int[] Islands;           // main islands it bites on, counting from 1. Null = all of them

        public bool BitesOn(int island) => Islands == null || Islands.Length == 0 || Array.IndexOf(Islands, island) >= 0;

        // for the Fishdex: "Islands 3–5 · Professional Lure, Scientific Lure"
        public string WhereText
        {
            get
            {
                string where = Islands == null || Islands.Length == 0 ? "Any island"
                    : Islands.Length == 1 ? $"Island {Islands[0]} only"
                    : $"Islands {Islands[0]}–{Islands[Islands.Length - 1]}";
                string baits = string.IsNullOrEmpty(BaitFilter) ? "any bait" : BaitFilter.Replace(",", ", ").Replace("  ", " ");
                return $"{where}  ·  {baits}";
            }
        }
        public int Health = -1;         // -1 = template's
        public bool? Endangered;
        public bool? Hostile;           // false calms a hostile template; true can't add hostility
        public float WaterPull = 1f;    // scales the template's pull back towards the water once landed: lower = stays on land longer
        public bool AllowShiny = true;  // keep true for journal fish, or the "every shiny" achievement becomes impossible
        public bool InJournal = true;
        public int FoodValue = -1, HealValue = -1;
        public bool KeepTemplateSkin;

        // FishMon
        public MonType Type1 = MonType.Water;
        public MonType Type2 = MonType.None;
        public int[] BaseStats;         // HP, Atk, Def, SpA, SpD, Spe. Null = worked out like any other fish
        public EvoStage[] Evolutions;   // null = "Big X" / "Mega X", empty = never evolves

        // filled in by the API
        public string SourcePlugin { get; internal set; }
        public byte RuntimeItemId { get; internal set; } // can change between sessions: save Key, not this
        public bool Live { get; internal set; }          // really in the game (check after FishMonAPI.OnBuilt)
        public Material RuntimeMaterial { get; internal set; }
    }

    // builds the species once the game's item list and FishNet exist, then switches off
    internal class SpeciesBuilder : MonoBehaviour
    {
        private void Update()
        {
            Injector.TryBuild();
            if (Injector.AlreadyBuilt || FishMonAPI.All.Count == 0) enabled = false;
        }
    }

    public static class FishMonAPI
    {
        private static readonly Dictionary<string, SpeciesDef> ByKey = new Dictionary<string, SpeciesDef>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, AssetBundle> Bundles = new Dictionary<string, AssetBundle>(StringComparer.OrdinalIgnoreCase);

        public static event Action OnBuilt;
        public static bool IsAvailable => true;
        public static IReadOnlyCollection<SpeciesDef> All => ByKey.Values;

        // true only means the definition was accepted; check SpeciesDef.Live after OnBuilt
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool Register(SpeciesDef def)
        {
            if (def == null || string.IsNullOrEmpty(def.Key)) { Plugin.Log.LogError("A species was registered with no Key, ignoring it."); return false; }
            if (def.Prefab != null && def.Prefab.GetComponentInChildren<MeshFilter>(true) == null)
            {
                Plugin.Log.LogError($"Species '{def.Key}': prefab '{def.Prefab.name}' has no MeshFilter, ignoring it.");
                return false;
            }
            if (def.Evolutions != null)
                foreach (var s in def.Evolutions)
                    if (s?.Prefab != null && s.Prefab.GetComponentInChildren<Renderer>(true) == null)
                    {
                        Plugin.Log.LogWarning($"Species '{def.Key}': stage model '{s.Prefab.name}' for {s.Name} has nothing to draw, ignoring it.");
                        s.Prefab = null;
                    }
            if (ByKey.ContainsKey(def.Key)) { Plugin.Log.LogWarning($"Species '{def.Key}' is already registered, ignoring the duplicate."); return false; }

            def.SourcePlugin = Caller();
            if (Injector.AlreadyBuilt)
            {
                Plugin.Log.LogError($"Species '{def.Key}' from {def.SourcePlugin} was registered too late. Register from your plugin's Awake.");
                return false;
            }

            if (string.IsNullOrEmpty(def.Name)) def.Name = def.Key;
            def.Worth = Mathf.Max(0, def.Worth);
            def.Rarity = Mathf.Max(0.0001f, def.Rarity);
            def.MeshScale = Mathf.Max(0.01f, def.MeshScale);
            if (def.Type2 == def.Type1) def.Type2 = MonType.None;
            if (def.BaseStats != null && def.BaseStats.Length != 6)
            {
                Plugin.Log.LogWarning($"Species '{def.Key}': BaseStats needs 6 numbers (HP, Atk, Def, SpA, SpD, Spe), ignoring them.");
                def.BaseStats = null;
            }

            ByKey[def.Key] = def;
            Injector.MarkDirty();
            return true;
        }

        public static SpeciesDef Get(string key) => !string.IsNullOrEmpty(key) && ByKey.TryGetValue(key, out var d) ? d : null;
        public static SpeciesDef FromItem(Item item) => Injector.FromItem(item);
        public static SpeciesDef FromId(byte id) => Injector.FromId(id);

        public static SpeciesDef FromName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (var d in ByKey.Values)
                if (string.Equals(d.Name, name, StringComparison.OrdinalIgnoreCase)) return d;
            return null;
        }

        // sorted by Key: the same order on every machine, which id allocation relies on
        public static List<SpeciesDef> Ordered()
        {
            var list = new List<SpeciesDef>(ByKey.Values);
            list.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            return list;
        }

        public static AssetBundle LoadBundle(string absolutePath)
        {
            if (string.IsNullOrEmpty(absolutePath)) return null;
            if (Bundles.TryGetValue(absolutePath, out var cached) && cached != null) return cached;
            try
            {
                if (!File.Exists(absolutePath)) { Plugin.Log.LogError("No asset bundle at " + absolutePath); return null; }
                var bundle = AssetBundle.LoadFromFile(absolutePath);
                if (bundle == null) { Plugin.Log.LogError($"Unity refused the asset bundle at {absolutePath} (already loaded, wrong platform or Unity version?)."); return null; }
                Bundles[absolutePath] = bundle;
                return bundle;
            }
            catch (Exception e) { Plugin.Log.LogError("Failed to load asset bundle: " + e); return null; }
        }

        internal static string Forced;

        // testing: the next thing landed on any rod is this species, once. Host only.
        public static bool ForceNextCatch(string key)
        {
            var d = Get(key);
            if (d == null || !d.Live) { Plugin.Log.LogWarning($"Can't force '{key}': it isn't in the game."); return false; }
            Forced = d.Key;
            return true;
        }

        internal static void RaiseBuilt()
        {
            var listeners = OnBuilt?.GetInvocationList();
            if (listeners == null) return;
            foreach (var l in listeners)
            {
                try { ((Action)l)(); }
                catch (Exception e) { Plugin.Log.LogError("An OnBuilt listener threw, the others still ran: " + e); }
            }
        }

        private static string Caller()
        {
            try
            {
                var trace = new System.Diagnostics.StackTrace();
                for (int i = 1; i < trace.FrameCount; i++)
                {
                    var t = trace.GetFrame(i).GetMethod()?.DeclaringType;
                    if (t != null && t.Assembly != typeof(FishMonAPI).Assembly) return t.Assembly.GetName().Name;
                }
            }
            catch { }
            return Plugin.Name;
        }
    }
}
