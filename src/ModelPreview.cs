using UnityEngine;

namespace HowToFishMon
{
    // renders a slowly spinning copy of a creature's model into a texture for the Fishdex. The model, its
    // camera and lights live far away from the world on a layer nothing else uses
    public static class ModelPreview
    {
        private const int FallbackLayer = 0;
        private static int _layer = FallbackLayer;
        private static readonly Color Bg = new Color(0.09f, 0.11f, 0.16f, 1f);
        private static int _checkIn = -1;
        private static bool _checked;
        private static int _renders;
        private static readonly Vector3 Origin = new Vector3(0f, 2500f, 0f);

        private static GameObject _root;
        private static Camera _cam;
        private static RenderTexture _rt;
        private static Transform _spin;
        private static GameObject _model;
        private static int _shownId = -1;
        private static bool _shownShiny;
        private static float _yaw = 120f, _pitch = 12f;
        private static float _lastDrawn = -10f;
        private static bool _failed;

        public static Texture Texture => _rt;

        private static bool Setup()
        {
            if (_root) return true;
            if (_failed) return false;
            try
            {
                _root = new GameObject("FishMonPreview");
                Object.DontDestroyOnLoad(_root);
                _root.hideFlags = HideFlags.HideAndDontSave;
                _root.transform.position = Origin;

                _spin = new GameObject("Spin").transform;
                _spin.SetParent(_root.transform, false);

                _rt = new RenderTexture(512, 512, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1, hideFlags = HideFlags.HideAndDontSave };
                _rt.Create();

                var camGo = new GameObject("PreviewCam");
                camGo.transform.SetParent(_root.transform, false);
                camGo.transform.localPosition = new Vector3(0f, 0f, -2.6f);
                _cam = camGo.AddComponent<Camera>();
                _cam.clearFlags = CameraClearFlags.SolidColor;
                _cam.backgroundColor = Bg;
                _cam.cullingMask = 1 << _layer;
                _cam.fieldOfView = 30f;
                _cam.nearClipPlane = 0.05f;
                _cam.farClipPlane = 20f;
                _cam.targetTexture = _rt;
                _cam.depth = -50;
                _cam.enabled = false;
                UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += (ctx, cam) => { if (cam == _cam) _renders++; };

                // point lights only reach the preview model (nothing else is anywhere near)
                AddLight(new Vector3(-1.6f, 1.8f, -2.2f), new Color(1f, 0.97f, 0.9f), 6f);
                AddLight(new Vector3(2f, 0.4f, -1.5f), new Color(0.6f, 0.75f, 1f), 2.5f);
                AddLight(new Vector3(0f, 1.5f, 2.5f), new Color(1f, 1f, 1f), 3f);
                return true;
            }
            catch (System.Exception e)
            {
                _failed = true;
                Plugin.Log.LogWarning("Fishdex model preview unavailable: " + e.Message);
                return false;
            }
        }

        private static void AddLight(Vector3 pos, Color c, float intensity)
        {
            var go = new GameObject("PreviewLight");
            go.transform.SetParent(_root.transform, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = c;
            l.intensity = intensity;
            l.range = 8f;
            l.shadows = LightShadows.None;
            l.cullingMask = ~0;
        }

        // draw the creature's model in a GUI rect (call from OnGUI). Drag to turn it.
        public static void Draw(Rect r, byte speciesId, bool shiny)
        {
            if (!Setup()) return;
            if (_shownId != speciesId || _shownShiny != shiny) Build(speciesId, shiny);
            _lastDrawn = Time.unscaledTime;
            _cam.enabled = true;

            var e = Event.current;
            if (e.type == EventType.MouseDrag && r.Contains(e.mousePosition))
            {
                _yaw -= e.delta.x * 0.6f;
                _pitch = Mathf.Clamp(_pitch + e.delta.y * 0.4f, -60f, 60f);
                _dragUntil = Time.unscaledTime + 2f;
                e.Use();
            }
            // opaque: some game shaders write alpha 0, which would make the fish invisible if we alpha-blended
            if (e.type == EventType.Repaint && _rt) GUI.DrawTexture(r, _rt, ScaleMode.ScaleToFit, false);
        }

        private static float _dragUntil;

        // call every frame: spins the model and turns the camera off when the Fishdex isn't showing it
        public static void Tick()
        {
            if (!_root) return;
            bool visible = Time.unscaledTime - _lastDrawn < 0.25f;
            if (_cam.enabled != visible) _cam.enabled = visible;
            if (!visible) return;
            if (Time.unscaledTime > _dragUntil) _yaw += Time.unscaledDeltaTime * 30f;
            _spin.localRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            if (_checkIn > 0 && --_checkIn == 0) SelfCheck();
        }

        // once: read the picture back and log whether anything was actually drawn (for troubleshooting)
        private static void SelfCheck()
        {
            _checked = true;
            try
            {
                var prev = RenderTexture.active;
                RenderTexture.active = _rt;
                var tex = new Texture2D(_rt.width, _rt.height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, _rt.width, _rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = prev;
                var px = tex.GetPixels32();
                int drawn = 0;
                Color32 bg = Bg;
                foreach (var p in px)
                    if (System.Math.Abs(p.r - bg.r) + System.Math.Abs(p.g - bg.g) + System.Math.Abs(p.b - bg.b) > 12) drawn++;
                var mid = tex.GetPixel(_rt.width / 2, _rt.height / 2);
                Plugin.Log.LogInfo($"Fishdex preview check: camera rendered {_renders} times, {drawn * 100f / px.Length:0.0}% of pixels drawn, centre colour {mid}");
                Object.Destroy(tex);
            }
            catch (System.Exception ex) { Plugin.Log.LogInfo("Fishdex preview check failed: " + ex.Message); }
        }

        private static void Build(byte id, bool shiny)
        {
            _shownId = id;
            _shownShiny = shiny;
            if (_model) Object.Destroy(_model);
            _model = new GameObject("Model");
            var t = _model.transform;
            t.SetParent(_spin, false);
            int parts = 0;
            int prefabLayer = -1;
            try { parts = Companion.CopyPrefabVisuals(id, shiny, t, out prefabLayer); }
            catch (System.Exception ex) { Plugin.Log.LogWarning("Fishdex couldn't copy model: " + ex.Message); }
            if (parts == 0)
            {
                var cap = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Object.Destroy(cap.GetComponent<Collider>());
                cap.transform.SetParent(t, false);
                cap.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                cap.transform.localScale = new Vector3(0.35f, 0.4f, 0.3f);
            }

            // centre it and fit it to the frame
            var b = Companion.LocalBounds(t);
            float longest = Mathf.Max(0.01f, Mathf.Max(b.size.x, Mathf.Max(b.size.y, b.size.z)));
            float scale = 1.25f / longest;
            t.localScale = Vector3.one * scale;
            t.localPosition = -b.center * scale;
            // use the fish's own layer: the game's renderer is guaranteed to draw it (unused layers can be filtered out)
            _layer = prefabLayer >= 0 ? prefabLayer : FallbackLayer;
            _cam.cullingMask = 1 << _layer;
            foreach (var tr in _model.GetComponentsInChildren<Transform>(true)) tr.gameObject.layer = _layer;
            if (!_checked) _checkIn = 10;
            Plugin.Log.LogInfo($"Fishdex preview: item {id}, {parts} parts, layer {_layer}, size {longest:0.###}, scale {scale:0.##}");
            foreach (var mr in _model.GetComponentsInChildren<Renderer>(true))
            {
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
            }
        }
    }
}
