using System.IO;
using System.Linq;
using UnityEngine;

namespace HowToFishMon
{
    // This code is here so I can scare my friend (he's afraid of praying mantises). It does nothing for anyone
    // else: the mantis only ever shows up on the games of the Steam IDs in Targets, and only when the Prankster
    // (me) presses Ctrl+Shift+M in the same lobby. The host stamps who sent the message and the target's game
    // checks it really came from me, so nobody else can set it off. It's a picture and a sound for 5 seconds.
    public class Prank : MonoBehaviour
    {
        public const ulong Prankster = 76561198090648190;

        // who gets pranked: my friend. (Put Prankster in here too to see it on your own screen again)
        private static readonly ulong[] Targets = { 76561198390418793 };

        private const float Duration = 5f;

        private static Prank _instance;
        private Texture2D _tex;
        private AudioClip _clip;
        private AudioSource _src;
        private GUIStyle _caption;
        private float _start = -100f;

        private static ulong SteamIdOf(Player p)
        {
            try { return p ? p.SteamID : 0UL; } catch { return 0UL; }
        }

        // every frame (FishMonController.Update): the prankster's key
        public static void Tick()
        {
            var me = Player.LocalPlayer;
            if (SteamIdOf(me) != Prankster || ChatManager.IsTyping) return;
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (!ctrl || !shift || !Input.GetKeyDown(KeyCode.M)) return;

            Net.SendPrank();
            bool selfTest = Targets.Contains(Prankster);
            if (selfTest) Show();
            FishMonController.ShowToast(selfTest ? "Mantis test (only you see this)." : "Mantis sent.", new Color(0.6f, 1f, 0.5f), 2.5f);
        }

        // someone sent the prank: only shows if it came from the prankster and we're a target
        public static void OnMessage(int senderClientId)
        {
            if (SteamIdOf(Net.PlayerFor(senderClientId)) != Prankster) return;
            if (!Targets.Contains(SteamIdOf(Player.LocalPlayer))) return;
            Show();
        }

        private static void Show()
        {
            if (!_instance)
            {
                var go = new GameObject("FishMon_Prank");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<Prank>();
            }
            _instance.Play();
        }

        private void Play()
        {
            if (!_tex) _tex = LoadTexture();
            if (!_clip) _clip = LoadWav();
            if (!_src)
            {
                _src = gameObject.AddComponent<AudioSource>();
                _src.spatialBlend = 0f;
                _src.playOnAwake = false;
                _src.volume = 1f;
            }
            if (_clip) { _src.Stop(); _src.clip = _clip; _src.Play(); }
            _start = Time.unscaledTime;
        }

        private void OnGUI()
        {
            float t = Time.unscaledTime - _start;
            if (t > Duration || !_tex) return;
            GUI.depth = -1000; // on top of everything

            // pops in with a bit of overshoot, shakes, and shrinks away at the end
            float pop = t < 0.25f ? Mathf.Sin(t / 0.25f * Mathf.PI * 0.75f) / Mathf.Sin(Mathf.PI * 0.75f) * 1.1f : 1f;
            float outro = Mathf.Clamp01((Duration - t) / 0.3f);
            float size = Mathf.Min(Screen.width, Screen.height) * 0.85f * pop * outro;
            float aspect = _tex.width / (float)Mathf.Max(1, _tex.height);
            float w = size * aspect, h = size;
            float shake = Mathf.Max(0f, 1f - t / 2f) * Screen.height * 0.012f;
            float sx = Mathf.Sin(t * 53f) * shake, sy = Mathf.Cos(t * 47f) * shake;
            GUI.DrawTexture(new Rect((Screen.width - w) / 2f + sx, (Screen.height - h) / 2f + sy, w, h), _tex, ScaleMode.ScaleToFit);

            if (_caption == null)
                _caption = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(24, Screen.height / 22), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
            if (t > 1.5f)
                GUI.Label(new Rect(0, Screen.height * 0.86f, Screen.width, Screen.height * 0.1f), "<color=#9fff7a>GOTCHA! It's just a prank :)</color>", _caption);
        }

        // the picture and sound are built into the DLL

        private static byte[] Resource(string name)
        {
            using (var s = typeof(Prank).Assembly.GetManifestResourceStream(name))
            {
                if (s == null) { Plugin.Log.LogWarning("Prank: missing " + name); return null; }
                using (var ms = new MemoryStream()) { s.CopyTo(ms); return ms.ToArray(); }
            }
        }

        private static Texture2D LoadTexture()
        {
            var bytes = Resource("HowToFishMon.prank.jpg");
            if (bytes == null) return null;
            var tex = new Texture2D(2, 2);
            // called by reflection: the game's Unity also has a ReadOnlySpan overload this project can't compile against
            var load = typeof(ImageConversion).GetMethod("LoadImage", new[] { typeof(Texture2D), typeof(byte[]) });
            return load != null && (bool)load.Invoke(null, new object[] { tex, bytes }) ? tex : null;
        }

        // a plain 16-bit PCM .wav
        private static AudioClip LoadWav()
        {
            var b = Resource("HowToFishMon.prank.wav");
            if (b == null || b.Length < 44) return null;
            int channels = 1, rate = 22050, pos = 12;
            while (pos + 8 <= b.Length)
            {
                string id = System.Text.Encoding.ASCII.GetString(b, pos, 4);
                int len = System.BitConverter.ToInt32(b, pos + 4);
                pos += 8;
                if (id == "fmt ")
                {
                    channels = System.BitConverter.ToInt16(b, pos + 2);
                    rate = System.BitConverter.ToInt32(b, pos + 4);
                }
                else if (id == "data")
                {
                    int count = Mathf.Min(len, b.Length - pos) / 2;
                    var samples = new float[count];
                    for (int i = 0; i < count; i++) samples[i] = System.BitConverter.ToInt16(b, pos + i * 2) / 32768f;
                    var clip = AudioClip.Create("prank", count / Mathf.Max(1, channels), Mathf.Max(1, channels), rate, false);
                    // by reflection, same reason as LoadImage
                    typeof(AudioClip).GetMethod("SetData", new[] { typeof(float[]), typeof(int) })?.Invoke(clip, new object[] { samples, 0 });
                    return clip;
                }
                pos += len + (len & 1);
            }
            return null;
        }
    }
}
