using System.Collections.Generic;
using UnityEngine;

namespace HowToFishMon
{
    // visual + audio flair for attacks. Uses the game's own VFX/sounds plus lights and lines we draw ourselves.
    public static class Fx
    {
        private static Material _lineMat;
        private static bool _lineMatTried;

        // an unlit material that can take a colour, for trails and lightning. Null if no suitable shader exists.
        public static Material LineMaterial
        {
            get
            {
                if (_lineMatTried) return _lineMat;
                _lineMatTried = true;
                foreach (var name in new[] { "Universal Render Pipeline/Particles/Unlit", "Sprites/Default", "Universal Render Pipeline/Unlit", "Unlit/Color", "Hidden/Internal-Colored" })
                {
                    var sh = Shader.Find(name);
                    if (!sh) continue;
                    _lineMat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
                    Plugin.Log.LogInfo("FishMon effects using shader: " + name);
                    break;
                }
                if (!_lineMat) Plugin.Log.LogWarning("No unlit shader found; trails and lightning are disabled (lights still work).");
                return _lineMat;
            }
        }

        private static bool On => Plugin.AttackEffects.Value;

        public static void Sound(string clip, Vector3 pos, float vol = 0.5f, AudioDistance dist = AudioDistance.Medium)
        {
            try { AudioManager.PlayClipAt(clip, pos, true, dist, vol); } catch { }
        }

        public static void Vfx(string name, Vector3 pos, Vector3 dir = default)
        {
            try { VFXManager.Play(name, pos, dir); } catch { }
        }

        public static void Flash(Vector3 pos, Color color, float intensity = 6f, float range = 6f, float duration = 0.35f)
        {
            var go = new GameObject("FishMonFlash");
            go.transform.position = pos;
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            go.AddComponent<Fader>().Init(duration, t => light.intensity = intensity * (1f - t));
        }

        public static void Lightning(Vector3 from, Vector3 to, Color color, float width = 0.12f, float duration = 0.25f)
        {
            var mat = LineMaterial;
            if (!mat) return;
            var go = new GameObject("FishMonBolt");
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = mat;
            lr.useWorldSpace = true;
            lr.numCapVertices = 2;
            const int segs = 10;
            lr.positionCount = segs + 1;
            Vector3 dir = to - from;
            Vector3 side = Vector3.Cross(dir.normalized, Vector3.up);
            if (side.sqrMagnitude < 0.01f) side = Vector3.right;
            for (int i = 0; i <= segs; i++)
            {
                float t = i / (float)segs;
                Vector3 p = Vector3.Lerp(from, to, t);
                if (i > 0 && i < segs)
                    p += (side * Random.Range(-1f, 1f) + Vector3.up * Random.Range(-1f, 1f)) * dir.magnitude * 0.06f;
                lr.SetPosition(i, p);
            }
            var c0 = Color.Lerp(color, Color.white, 0.5f);
            lr.startColor = c0; lr.endColor = color;
            lr.startWidth = width; lr.endWidth = width * 0.5f;
            go.AddComponent<Fader>().Init(duration, t =>
            {
                var a = 1f - t;
                lr.startColor = new Color(c0.r, c0.g, c0.b, a);
                lr.endColor = new Color(color.r, color.g, color.b, a);
            });
        }

        // burst of short coloured rays (cheap "particles")
        public static void Burst(Vector3 pos, Color color, int rays = 8, float length = 1.2f, float duration = 0.35f)
        {
            var mat = LineMaterial;
            if (!mat) return;
            for (int i = 0; i < rays; i++)
            {
                var go = new GameObject("FishMonRay");
                var lr = go.AddComponent<LineRenderer>();
                lr.sharedMaterial = mat;
                lr.positionCount = 2;
                Vector3 d = Random.onUnitSphere;
                Vector3 a = pos + d * 0.2f;
                float len = length * Random.Range(0.6f, 1.2f);
                lr.SetPosition(0, a);
                lr.SetPosition(1, a + d * 0.05f);
                lr.startWidth = 0.08f; lr.endWidth = 0.02f;
                lr.startColor = lr.endColor = color;
                go.AddComponent<Fader>().Init(duration, t =>
                {
                    lr.SetPosition(0, a + d * len * t * 0.6f);
                    lr.SetPosition(1, a + d * len * t);
                    var c = color; c.a = 1f - t;
                    lr.startColor = lr.endColor = c;
                });
            }
        }

        public static TrailRenderer AddTrail(GameObject go, Color color, float width)
        {
            var mat = LineMaterial;
            if (!mat) return null;
            var tr = go.AddComponent<TrailRenderer>();
            tr.sharedMaterial = mat;
            tr.time = 0.35f;
            tr.startWidth = width;
            tr.endWidth = 0f;
            tr.minVertexDistance = 0.1f;
            tr.startColor = new Color(color.r, color.g, color.b, 0.9f);
            tr.endColor = new Color(color.r, color.g, color.b, 0f);
            tr.emitting = false;
            return tr;
        }

        // per-type attacks

        // when the fish launches its attack
        public static void Launch(Move m, Vector3 pos, Vector3 dir, bool inWater)
        {
            if (!On) return;
            var col = TypeChart.ColorOf(m.Type);
            switch (m.Type)
            {
                case MonType.Water:
                    Vfx("WaterSplash", pos, Vector3.zero);
                    Sound("FishCaught_Splash", pos, 0.4f);
                    break;
                case MonType.Fire:
                case MonType.Dragon:
                    Vfx("LavaBarf", pos, dir);
                    Sound("Ignite", pos, 0.45f);
                    break;
                case MonType.Fighting:
                case MonType.Dark:
                    Vfx("Angry", pos + Vector3.up * 0.6f, Vector3.zero);
                    break;
                case MonType.Electric:
                    Lightning(pos, pos + Vector3.up * 2.5f + dir * 0.5f, col, 0.06f, 0.15f);
                    break;
            }
            if (m.Power >= 80) Flash(pos, col, 3f, 4f, 0.25f);
        }

        // when the attack lands
        public static void Impact(Move m, Vector3 pos, Vector3 dir, float eff, bool crit)
        {
            if (!On) return;
            var col = TypeChart.ColorOf(m.Type);
            bool big = m.Power >= 80 || crit;
            float mult = eff >= 2f ? 1.6f : eff < 1f ? 0.6f : 1f;

            Flash(pos, col, (big ? 9f : 5f) * mult, big ? 9f : 6f, big ? 0.45f : 0.3f);
            Burst(pos, col, big ? 12 : 7, big ? 1.8f : 1.1f);

            switch (m.Type)
            {
                case MonType.Water:
                    Vfx(big ? "WaterExplosion" : "WaterSplash", pos, Vector3.zero);
                    Sound("FishCaught_Splash", pos, 0.6f);
                    break;
                case MonType.Ice:
                    Vfx("WaterSplash", pos, Vector3.zero);
                    Sound("BaitInWater", pos, 0.6f);
                    break;
                case MonType.Fire:
                    Vfx(big ? "LavaExplosion" : "LavaBarf", pos, dir);
                    Sound(big ? "LavaPoof" : "Ignite", pos, 0.6f);
                    break;
                case MonType.Electric:
                    Lightning(pos + Vector3.up * 8f + Random.insideUnitSphere, pos, col, big ? 0.25f : 0.14f, 0.3f);
                    if (big) Lightning(pos + Vector3.up * 8f + Random.insideUnitSphere * 2f, pos, col, 0.1f, 0.25f);
                    Sound("RadarPing", pos, 0.5f);
                    break;
                case MonType.Poison:
                    Vfx("Poison", pos, Vector3.zero);
                    Sound("Swallow", pos, 0.6f);
                    break;
                case MonType.Grass:
                case MonType.Fairy:
                    Sound("Confetti", pos, 0.35f);
                    break;
                case MonType.Ground:
                case MonType.Rock:
                case MonType.Steel:
                    Sound("Explosion", pos, big ? 0.35f : 0.2f);
                    break;
                case MonType.Dragon:
                    Vfx("LavaBarf", pos, dir);
                    Sound("Explosion", pos, 0.3f);
                    break;
                case MonType.Ghost:
                case MonType.Psychic:
                    Sound("Poof", pos, 0.6f);
                    break;
                case MonType.Dark:
                case MonType.Fighting:
                    Vfx("Angry", pos + Vector3.up * 0.5f, Vector3.zero);
                    break;
            }
            Sound("FishPunchHit", pos, big ? 0.8f : 0.55f);
        }

        public static void Evolve(Vector3 pos, Color color)
        {
            Flash(pos, Color.white, 14f, 10f, 0.9f);
            Flash(pos, color, 8f, 8f, 1.4f);
            Burst(pos, Color.white, 16, 2.5f, 0.7f);
            Burst(pos, color, 16, 3f, 0.9f);
            Sound("Win", pos, 0.6f, AudioDistance.Long);
            Sound("Confetti", pos, 0.6f);
            Vfx("WaterExplosion", pos, Vector3.zero);
        }

        public static void Ping(Vector3 pos, Color color)
        {
            Flash(pos, color, 4f, 4f, 0.4f);
            Burst(pos, color, 6, 0.8f, 0.3f);
        }

        // animates over a duration, then destroys its GameObject
        private class Fader : MonoBehaviour
        {
            private float _dur, _t;
            private System.Action<float> _tick;

            public void Init(float duration, System.Action<float> tick) { _dur = Mathf.Max(0.01f, duration); _tick = tick; }

            private void Update()
            {
                _t += Time.deltaTime;
                float k = Mathf.Clamp01(_t / _dur);
                try { _tick?.Invoke(k); } catch { }
                if (k >= 1f) Destroy(gameObject);
            }
        }
    }
}
