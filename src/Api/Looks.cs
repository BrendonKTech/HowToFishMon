// Adapted from MoreFishAPI by hiccup444 (MIT License, see src/Api/LICENSE-MoreFishAPI.txt).
// Added: the Tint path, so a species can ship with no art and just recolour its template.
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace HowToFishMon.Api
{
    internal static class Looks
    {
        private static readonly string[] ColorSlots = { "_BaseColor", "_Color", "_MainColor", "_Tint", "_TintColor" };
        private static readonly string[] TextureSlots = { "_BaseMap", "_MainTex", "_BaseColorMap" };
        private static bool _loggedShader;

        public static void Apply(GameObject clone, SpeciesDef d, Creature basis, FieldInfo itemMesh, FieldInfo dripMesh)
        {
            if (d.Prefab != null) GraftPrefab(clone, d, basis, itemMesh, dripMesh);
            else if (d.Mesh != null) SwapMesh(clone, d, basis, itemMesh, dripMesh);

            if (d.Prefab == null && (d.Material != null || d.Texture != null || d.Tint.HasValue)) Repaint(clone, d);
            if (!d.AllowShiny) Quiet(clone);
        }

        private static void Repaint(GameObject clone, SpeciesDef d)
        {
            foreach (var r in clone.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || r.sharedMaterials.Length == 0) continue;
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    if (d.Material != null) { mats[i] = d.Material; continue; }
                    var src = r.sharedMaterials[i];
                    if (src == null) continue;
                    var copy = new Material(src) { name = src.name + "_" + d.Key };
                    if (d.Texture != null)
                    {
                        foreach (var s in TextureSlots) if (copy.HasProperty(s)) copy.SetTexture(s, d.Texture);
                        if (copy.HasProperty("_Use_Skin")) copy.SetInt("_Use_Skin", 0);
                        if (copy.HasProperty("_Rainbow_Skin")) copy.SetInt("_Rainbow_Skin", 0);
                    }
                    if (d.Tint.HasValue) { TintMaterial(copy, d.Tint.Value); Dim(copy); }
                    Object.DontDestroyOnLoad(copy);
                    mats[i] = copy;
                }
                r.sharedMaterials = mats;
                if (d.RuntimeMaterial == null && mats.Length > 0) d.RuntimeMaterial = mats[0];
            }
        }

        // the game's fish shader has no colour property, its colours come from the _Colors palette texture
        private static readonly Dictionary<string, Texture2D> Recoloured = new Dictionary<string, Texture2D>();

        private static Texture2D RecolourPalette(Texture src, Color tint)
        {
            string key = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(src) + "_" + ColorUtility.ToHtmlStringRGB(tint);
            if (Recoloured.TryGetValue(key, out var done) && done) return done;

            // game textures aren't CPU-readable
            var rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false) { name = src.name + "_" + key };
            tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            // keep each pixel's brightness so patterns still show
            float tintLum = Mathf.Max(0.05f, 0.299f * tint.r + 0.587f * tint.g + 0.114f * tint.b);
            var px = tex.GetPixels();
            for (int i = 0; i < px.Length; i++)
            {
                var p = px[i];
                float lum = 0.299f * p.r + 0.587f * p.g + 0.114f * p.b;
                var target = tint * Mathf.Clamp(lum / tintLum, 0.15f, 1.6f);
                var c = Color.Lerp(p, target, 0.85f) * Brightness;
                px[i] = new Color(Mathf.Clamp01(c.r), Mathf.Clamp01(c.g), Mathf.Clamp01(c.b), p.a);
            }
            tex.SetPixels(px);
            tex.filterMode = src.filterMode; // palettes are usually point-filtered: keep the colour bands crisp
            tex.wrapMode = src.wrapMode;
            tex.Apply(false, false);
            Object.DontDestroyOnLoad(tex);
            Recoloured[key] = tex;
            return tex;
        }

        private static void TintMaterial(Material m, Color tint)
        {
            if (m.HasProperty("_Colors") && m.GetTexture("_Colors") is Texture palette)
            {
                try { m.SetTexture("_Colors", RecolourPalette(palette, tint)); return; }
                catch (System.Exception e) { Plugin.Log.LogWarning($"Couldn't recolour palette '{palette.name}': {e.Message}"); }
            }
            bool any = false;
            foreach (var s in ColorSlots)
            {
                if (!m.HasProperty(s)) continue;
                var c = m.GetColor(s);
                m.SetColor(s, new Color(c.r * tint.r, c.g * tint.g, c.b * tint.b, c.a));
                any = true;
            }
            if (!any && !_loggedShader)
            {
                _loggedShader = true;
                var sh = m.shader;
                var props = new List<string>();
                for (int i = 0; i < sh.GetPropertyCount(); i++) props.Add($"{sh.GetPropertyName(i)}:{sh.GetPropertyType(i)}");
                Plugin.Log.LogWarning($"Couldn't tint shader '{sh.name}', it has no colour slot we know. Its properties: {string.Join(", ", props)}");
            }
        }


        public static GameObject PrepareStage(EvoStage stage, SpeciesDef d, Creature basis, Transform parent)
        {
            var visual = Object.Instantiate(stage.Prefab, parent);
            visual.name = "FishMon_Stage_" + stage.Name;
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localRotation = Quaternion.Euler(d.MeshRotation);
            visual.transform.localScale = Vector3.one * d.MeshScale;
            int layer = basis.GetComponentInChildren<Renderer>(true)?.gameObject.layer ?? basis.gameObject.layer;
            Prepare(visual, d, basis, layer);
            return visual;
        }

        private static void Prepare(GameObject visual, SpeciesDef d, Creature basis, int layer)
        {
            foreach (var b in visual.GetComponentsInChildren<Behaviour>(true))
                if (b != null && !(b is Animator) && !(b is Animation)) b.enabled = false; // no stray scripts on the art
            foreach (var c in visual.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            // the game's cameras may not draw the Default layer
            foreach (var t in visual.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
            if (d.UseGameShader) ToGameShader(visual, d, GameMaterial(basis));
        }

        private static Material GameMaterial(Creature basis)
        {
            Material any = null;
            foreach (var r in basis.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null) continue;
                    if (m.HasProperty("_Colors")) return m;
                    if (any == null) any = m;
                }
            return any;
        }

        private static bool _loggedOffsets;

        // our own materials can't use the game's shader: copy the template's and use our texture as its palette
        private static void ToGameShader(GameObject visual, SpeciesDef d, Material game)
        {
            if (game == null || !game.HasProperty("_Colors"))
            {
                Plugin.Log.LogWarning($"Species '{d.Key}': the template has no game fish material to copy, keeping the bundle's own materials.");
                return;
            }
            if (!_loggedOffsets && game.HasProperty("_Color_Offset"))
            {
                _loggedOffsets = true; // in case palettes come out shifted: these move the palette lookup
                Plugin.Log.LogInfo($"Game fish material '{game.name}': _Color_Offset {game.GetVector("_Color_Offset")}, _Color_Offset_2 {(game.HasProperty("_Color_Offset_2") ? game.GetVector("_Color_Offset_2").ToString() : "-")}");
            }

            var made = new Dictionary<Material, Material>();
            foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var src = mats[i];
                    // a model with no material (built in code) takes the species' Texture as its palette
                    var cacheKey = src != null ? src : game;
                    if (made.TryGetValue(cacheKey, out var done)) { mats[i] = done; continue; }
                    Texture tex = null;
                    if (src != null)
                        foreach (var s in new[] { "_Colors", "_BaseMap", "_MainTex", "_BaseColorMap" })
                            if (tex == null && src.HasProperty(s)) tex = src.GetTexture(s);
                    if (tex == null) tex = d.Texture;
                    if (tex == null)
                    {
                        Plugin.Log.LogWarning($"Species '{d.Key}': a material has no texture, so it can't be put on the game's shader. Give it a palette texture.");
                        continue;
                    }
                    var copy = new Material(game) { name = "FishMon_" + d.Key + "_" + (src != null ? src.name : "palette") };
                    copy.SetTexture("_Colors", tex);
                    Dim(copy);
                    if (copy.HasProperty("_Use_Skin")) copy.SetFloat("_Use_Skin", 0f);
                    if (copy.HasProperty("_Rainbow_Skin")) copy.SetFloat("_Rainbow_Skin", 0f);
                    Object.DontDestroyOnLoad(copy);
                    made[cacheKey] = mats[i] = copy;
                }
                r.sharedMaterials = mats;
            }
        }

        // physics uses the template's colliders, not the model: make every solid one cover the model's bounds.
        // Triggers (pickup / attack ranges) are left alone.
        private static void FitColliders(GameObject clone, GameObject visual, SpeciesDef d)
        {
            var root = clone.transform;
            if (!TryBounds(visual, root.worldToLocalMatrix, out var model)) return;
            int fitted = 0;
            foreach (var col in clone.GetComponentsInChildren<Collider>(true))
            {
                if (col.isTrigger || col.transform.IsChildOf(visual.transform)) continue;
                // the model's box, seen from the collider's own transform
                var b = Transform(model, col.transform.worldToLocalMatrix * root.localToWorldMatrix);
                switch (col)
                {
                    case BoxCollider box:
                        box.center = b.center; box.size = b.size; break;
                    case SphereCollider sphere:
                        sphere.center = b.center; sphere.radius = Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z)); break;
                    case CapsuleCollider cap:
                        int axis = b.size.x >= b.size.y && b.size.x >= b.size.z ? 0 : b.size.y >= b.size.z ? 1 : 2;
                        cap.center = b.center;
                        cap.direction = axis;
                        cap.height = b.size[axis];
                        cap.radius = Mathf.Max(b.size[(axis + 1) % 3], b.size[(axis + 2) % 3]) / 2f;
                        break;
                    case MeshCollider mc:
                        var mf = visual.GetComponentInChildren<MeshFilter>(true);
                        if (mf == null || mf.sharedMesh == null) continue;
                        // a mesh collider has to sit on the same transform as the mesh it uses
                        if (mf.transform != col.transform)
                        {
                            col.enabled = false;
                            var own = mf.gameObject.AddComponent<MeshCollider>();
                            own.sharedMesh = mf.sharedMesh; own.convex = true; own.sharedMaterial = mc.sharedMaterial;
                        }
                        else { mc.sharedMesh = mf.sharedMesh; mc.convex = true; }
                        break;
                    default: continue;
                }
                fitted++;
            }
            Plugin.Log.LogInfo($"Species '{d.Key}': fitted {fitted} collider(s) to its model ({model.size.x:0.00} x {model.size.y:0.00} x {model.size.z:0.00} m).");
        }

        private static bool TryBounds(GameObject visual, Matrix4x4 toRoot, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = r is SkinnedMeshRenderer smr ? smr.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null) continue;
                var b = Transform(mesh.bounds, toRoot * r.transform.localToWorldMatrix);
                if (!any) { bounds = b; any = true; } else bounds.Encapsulate(b);
            }
            return any;
        }

        private static Bounds Transform(Bounds b, Matrix4x4 m)
        {
            var result = new Bounds(m.MultiplyPoint3x4(b.min), Vector3.zero);
            for (int i = 1; i < 8; i++)
                result.Encapsulate(m.MultiplyPoint3x4(new Vector3((i & 1) != 0 ? b.max.x : b.min.x, (i & 2) != 0 ? b.max.y : b.min.y, (i & 4) != 0 ? b.max.z : b.min.z)));
            return result;
        }

        private static float Brightness => Mathf.Clamp(Plugin.CustomFishBrightness.Value, 0.1f, 2f);

        // the game's fish shader can make a material glow: turn that down along with the colours
        private static void Dim(Material m)
        {
            if (m.HasProperty("_Emission")) m.SetFloat("_Emission", m.GetFloat("_Emission") * Brightness);
        }

        private static float Nz(float f) => Mathf.Abs(f) < 1e-5f ? 1f : f;

        private static void GraftPrefab(GameObject clone, SpeciesDef d, Creature basis, FieldInfo itemMesh, FieldInfo dripMesh)
        {
            var filters = clone.GetComponentsInChildren<MeshFilter>(true);
            var skinned = clone.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Transform anchor = filters.Length > 0 ? filters[0].transform : skinned.Length > 0 ? skinned[0].transform : clone.transform;
            foreach (var f in filters) { var r = f.GetComponent<Renderer>(); if (r) r.enabled = false; }
            foreach (var s in skinned) s.enabled = false;

            var visual = Object.Instantiate(d.Prefab, anchor);
            visual.name = "FishMon_Visual";
            // parented to the template's mesh so it follows its movement, but lined up with the creature itself:
            // the template's mesh object is often rotated or scaled (imported models), ours isn't
            var root = clone.transform;
            visual.transform.position = anchor.position;
            visual.transform.rotation = root.rotation * Quaternion.Euler(d.MeshRotation);
            Vector3 rs = root.lossyScale, a = anchor.lossyScale;
            visual.transform.localScale = new Vector3(rs.x / Nz(a.x), rs.y / Nz(a.y), rs.z / Nz(a.z)) * d.MeshScale;
            Prepare(visual, d, basis, anchor.gameObject.layer);
            if (d.FitColliders) FitColliders(clone, visual, d);

            var own = new List<Renderer>(visual.GetComponentsInChildren<Renderer>(true));
            var item = clone.GetComponent<Item>();
            if (own.Count > 0 && AccessTools.Field(typeof(Item), "_renderers")?.GetValue(item) is List<Renderer> list) { list.Clear(); list.AddRange(own); }
            if (own.Count > 0 && d.KeepTemplateSkin && AccessTools.Field(typeof(Item), "_skinRenderers")?.GetValue(item) is List<Renderer> skin) { skin.Clear(); skin.AddRange(own); }

            var primary = visual.GetComponentInChildren<MeshFilter>(true);
            if (primary != null && primary.sharedMesh != null)
            {
                itemMesh?.SetValue(item, primary.sharedMesh);
                dripMesh?.SetValue(clone.GetComponent<Creature>(), primary.sharedMesh);
                var pr = primary.GetComponent<Renderer>();
                if (pr != null && pr.sharedMaterial != null) d.RuntimeMaterial = pr.sharedMaterial;
            }
        }

        private static void SwapMesh(GameObject clone, SpeciesDef d, Creature basis, FieldInfo itemMesh, FieldInfo dripMesh)
        {
            var filters = clone.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0) { Plugin.Log.LogError($"Species '{d.Key}': template '{basis.name}' is a skinned mesh and can't take a Mesh. Give a Prefab or pick another template."); return; }

            var whole = CombinedBounds(clone);
            float fit = 1f; Vector3 autoTurn = Vector3.zero;
            if (d.AutoFit && whole.size.magnitude > 0.0001f)
            {
                float src = d.Mesh.bounds.size.magnitude;
                if (src > 0.0001f) fit = whole.size.magnitude / src;
                autoTurn = AlignAxes(d.Mesh.bounds, whole);
            }
            var shaped = Bake(d.Mesh, fit * d.MeshScale, Quaternion.Euler(autoTurn) * Quaternion.Euler(d.MeshRotation));
            for (int i = 0; i < filters.Length; i++)
            {
                if (i == 0) { filters[i].sharedMesh = shaped; continue; }
                var extra = filters[i].GetComponent<Renderer>();
                if (extra) extra.enabled = false;
            }
            itemMesh?.SetValue(clone.GetComponent<Item>(), shaped);
            dripMesh?.SetValue(clone.GetComponent<Creature>(), shaped);
        }

        public static Bounds CombinedBounds(GameObject root)
        {
            bool started = false;
            var total = new Bounds(Vector3.zero, Vector3.zero);
            void Add(Mesh mesh, Transform t)
            {
                if (mesh == null) return;
                var b = new Bounds(mesh.bounds.center + t.localPosition, Vector3.Scale(mesh.bounds.size, t.localScale));
                if (!started) { started = true; total = b; } else total.Encapsulate(b);
            }
            foreach (var f in root.GetComponentsInChildren<MeshFilter>(true)) Add(f.sharedMesh, f.transform);
            foreach (var s in root.GetComponentsInChildren<SkinnedMeshRenderer>(true)) Add(s.sharedMesh, s.transform);
            return total;
        }

        private static int LongestAxis(Vector3 s) => s.x >= s.y && s.x >= s.z ? 0 : s.y >= s.z ? 1 : 2;

        private static Vector3 AlignAxes(Bounds source, Bounds target)
        {
            int from = LongestAxis(source.size), to = LongestAxis(target.size);
            if (from == to) return Vector3.zero;
            if (from == 1 && to == 0) return new Vector3(0f, 0f, -90f);
            if (from == 0 && to == 1) return new Vector3(0f, 0f, 90f);
            if (from == 1 && to == 2) return new Vector3(90f, 0f, 0f);
            if (from == 2 && to == 1) return new Vector3(-90f, 0f, 0f);
            if (from == 2 && to == 0) return new Vector3(0f, 90f, 0f);
            return new Vector3(0f, -90f, 0f);
        }

        private static Mesh Bake(Mesh source, float scale, Quaternion turn)
        {
            if (!source.isReadable)
            {
                Plugin.Log.LogWarning($"Mesh '{source.name}' isn't Read/Write enabled in its bundle, so it can't be fitted. Tick Read/Write when building.");
                return source;
            }
            var baked = new Mesh { name = source.name + "_fitted" };
            var v = source.vertices;
            for (int i = 0; i < v.Length; i++) v[i] = turn * v[i] * scale;
            baked.vertices = v;
            var n = source.normals;
            bool hasNormals = n != null && n.Length == v.Length;
            if (hasNormals) { for (int i = 0; i < n.Length; i++) n[i] = turn * n[i]; baked.normals = n; }
            baked.uv = source.uv;
            if (source.uv2 != null && source.uv2.Length == v.Length) baked.uv2 = source.uv2;
            if (source.colors32 != null && source.colors32.Length == v.Length) baked.colors32 = source.colors32;
            baked.subMeshCount = source.subMeshCount;
            for (int i = 0; i < source.subMeshCount; i++) baked.SetTriangles(source.GetTriangles(i), i);
            baked.RecalculateBounds();
            if (!hasNormals) baked.RecalculateNormals();
            baked.RecalculateTangents();
            Object.DontDestroyOnLoad(baked);
            return baked;
        }

        private static void Quiet(GameObject clone)
        {
            var shiny = AccessTools.Field(typeof(Creature), "_enableOnShiny")?.GetValue(clone.GetComponent<Creature>()) as GameObject;
            if (shiny != null) shiny.SetActive(false);
            foreach (var l in clone.GetComponentsInChildren<Light>(true)) l.enabled = false;
            foreach (var p in clone.GetComponentsInChildren<ParticleSystem>(true)) { var em = p.emission; em.enabled = false; }
        }
    }
}
