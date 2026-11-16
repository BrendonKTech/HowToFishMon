using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace HowToFishMon.Sea
{
    // The ocean species' built-in models: .obj files and one palette.png embedded in the DLL
    // (made by tools/models/make_models.py). A model in fishmon-ocean.bundle still wins over these.
    internal static class Models
    {
        private const string Prefix = "HowToFishMon.models.";
        private static GameObject _holder;
        private static Texture2D _palette;

        public static Texture2D Palette
        {
            get
            {
                if (_palette != null) return _palette;
                var bytes = Read("palette.png");
                if (bytes == null) return null;
                _palette = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "FishMon_OceanPalette" };
                // the byte[] overload, called directly: this .NET target can't compile against Unity's newer Span overload
                var load = typeof(ImageConversion).GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]), typeof(bool) });
                if (load == null || !(bool)load.Invoke(null, new object[] { _palette, bytes, false }))
                {
                    Plugin.Log.LogWarning("Couldn't read the ocean palette.");
                    return _palette = null;
                }
                _palette.filterMode = FilterMode.Point; // one flat colour per cell, no blending into the neighbours
                _palette.wrapMode = TextureWrapMode.Clamp;
                float k = Mathf.Clamp(Plugin.CustomFishBrightness.Value, 0.1f, 2f);
                var px = _palette.GetPixels();
                for (int i = 0; i < px.Length; i++) px[i] = new Color(Mathf.Clamp01(px[i].r * k), Mathf.Clamp01(px[i].g * k), Mathf.Clamp01(px[i].b * k), px[i].a);
                _palette.SetPixels(px);
                _palette.Apply(false, false);
                Object.DontDestroyOnLoad(_palette);
                return _palette;
            }
        }

        // the model called name (e.g. "coralit", "reefguard"), or null if there's none
        public static GameObject Get(string name)
        {
            string text = ReadText(name.Replace(" ", "").ToLowerInvariant() + ".obj");
            if (text == null) return null;
            var mesh = ParseObj(text, name);
            if (mesh == null) return null;

            if (_holder == null)
            {
                // inactive, so the models sit there like prefabs and never show up in the world
                _holder = new GameObject("FishMon_OceanModels");
                _holder.SetActive(false);
                _holder.hideFlags = HideFlags.HideAndDontSave;
                Object.DontDestroyOnLoad(_holder);
            }
            var go = new GameObject(name);
            go.transform.SetParent(_holder.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            // no material: Api.Looks puts the game's fish material on it, with Palette as the colours
            go.AddComponent<MeshRenderer>().sharedMaterials = new Material[1];
            return go;
        }

        // just what make_models.py writes: v, vt, vn and triangles as v/vt/vn. Corners that share all three are
        // shared vertices, so smooth surfaces stay smooth and hard edges (different normals) stay hard.
        private static Mesh ParseObj(string text, string name)
        {
            var v = new List<Vector3>();
            var vt = new List<Vector2>();
            var vn = new List<Vector3>();
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var normals = new List<Vector3>();
            var tris = new List<int>();
            var corners = new Dictionary<string, int>();
            var inv = CultureInfo.InvariantCulture;
            foreach (var raw in text.Split('\n'))
            {
                var p = raw.Trim().Split(' ');
                if (p.Length < 3) continue;
                if (p[0] == "v") v.Add(new Vector3(float.Parse(p[1], inv), float.Parse(p[2], inv), float.Parse(p[3], inv)));
                else if (p[0] == "vt") vt.Add(new Vector2(float.Parse(p[1], inv), float.Parse(p[2], inv)));
                else if (p[0] == "vn") vn.Add(new Vector3(float.Parse(p[1], inv), float.Parse(p[2], inv), float.Parse(p[3], inv)));
                else if (p[0] == "f" && p.Length == 4)
                    for (int k = 1; k <= 3; k++)
                    {
                        if (!corners.TryGetValue(p[k], out int index))
                        {
                            var ids = p[k].Split('/');
                            index = corners[p[k]] = verts.Count;
                            verts.Add(v[int.Parse(ids[0], inv) - 1]);
                            uvs.Add(ids.Length > 1 && ids[1].Length > 0 ? vt[int.Parse(ids[1], inv) - 1] : Vector2.zero);
                            normals.Add(ids.Length > 2 && ids[2].Length > 0 ? vn[int.Parse(ids[2], inv) - 1] : Vector3.zero);
                        }
                        tris.Add(index);
                    }
            }
            if (tris.Count == 0) { Plugin.Log.LogWarning($"Model '{name}' has no faces."); return null; }

            var mesh = new Mesh { name = "FishMon_" + name };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            if (vn.Count > 0) mesh.SetNormals(normals);
            else mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.RecalculateTangents();
            Object.DontDestroyOnLoad(mesh);
            return mesh;
        }

        private static byte[] Read(string file)
        {
            using (var s = typeof(Models).Assembly.GetManifestResourceStream(Prefix + file))
            {
                if (s == null) return null;
                using (var ms = new MemoryStream()) { s.CopyTo(ms); return ms.ToArray(); }
            }
        }

        private static string ReadText(string file)
        {
            var b = Read(file);
            return b == null ? null : System.Text.Encoding.UTF8.GetString(b);
        }
    }
}
