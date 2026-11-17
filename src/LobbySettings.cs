using System.Collections.Generic;
using BepInEx.Configuration;
using FishNet.Broadcast;
using FishNet.Managing;
using FishNet.Transporting;
using Newtonsoft.Json;
using UnityEngine;

namespace HowToFishMon
{
    // host -> guests: the host's preset and balance settings (Presets.SyncedEntries), as "Section/Key" -> value
    public struct MonSettingsMsg : IBroadcast
    {
        public string Payload;
    }

    // The host's preset and balance settings are used by everyone in its lobby. Each game works out its own FishMon's
    // HP, EXP, catch levels and enemy levels, so without this every player would play on their own settings.
    // A guest's own values are kept aside (never written to their config) and come back when they leave.
    internal static class LobbySettings
    {
        private const float Heartbeat = 3f;     // resend now and then, for players who join late
        private const float MinGap = 0.5f;      // while the host drags a slider

        // true while a guest is using the host's settings
        public static bool FromHost { get; private set; }

        private static Dictionary<string, string> _own;     // the guest's own values, while the host's are on
        private static Dictionary<string, string> _host;    // the host's values, as last applied
        private static string _hostPayload;
        private static string _lastSent = "";
        private static float _sendTimer;

        private static Dictionary<string, string> Snapshot()
        {
            var d = new Dictionary<string, string>();
            foreach (var e in Presets.SyncedEntries) d[Presets.KeyOf(e)] = e.GetSerializedValue();
            return d;
        }

        private static void Put(Dictionary<string, string> values)
        {
            if (values == null) return;
            foreach (var e in Presets.SyncedEntries)
                if (values.TryGetValue(Presets.KeyOf(e), out var s) && s != e.GetSerializedValue())
                {
                    try { e.SetSerializedValue(s); }
                    catch (System.Exception ex) { Plugin.Log.LogWarning($"Couldn't use the host's {Presets.KeyOf(e)} ({s}): {ex.Message}"); }
                }
        }

        // host, every frame while online
        public static void HostTick(NetworkManager nm)
        {
            if (FromHost) Restore(); // just became the host
            string payload = JsonConvert.SerializeObject(Snapshot());
            _sendTimer -= Time.unscaledDeltaTime;
            bool changed = payload != _lastSent;
            if (changed ? _sendTimer > Heartbeat - MinGap : _sendTimer > 0f) return;
            _sendTimer = Heartbeat;
            _lastSent = payload;
            try { nm.ServerManager.Broadcast(new MonSettingsMsg { Payload = payload }, true, Channel.Reliable); }
            catch (System.Exception e) { Plugin.Log.LogWarning("Couldn't send FishMon settings to the lobby: " + e.Message); }
        }

        // guest: the host's settings arrived
        public static void OnMessage(MonSettingsMsg m)
        {
            if (Net.IsHost || string.IsNullOrEmpty(m.Payload) || m.Payload == _hostPayload) return;
            var values = JsonConvert.DeserializeObject<Dictionary<string, string>>(m.Payload);
            if (values == null) return;
            bool first = !FromHost;
            if (first) _own = Snapshot();
            _host = values;
            _hostPayload = m.Payload;
            FromHost = true;
            Put(values);
            Plugin.Log.LogInfo($"Using the host's FishMon settings ({Presets.Current} preset).");
            FishMonController.ShowToast(first ? $"Using the host's FishMon settings ({Presets.Current} preset)." : $"The host changed the FishMon settings ({Presets.Current} preset).",
                new Color(0.7f, 0.9f, 1f), 4f);
        }

        // left the lobby (or became the host): your own settings come back
        public static void Restore()
        {
            if (!FromHost) return;
            FromHost = false;
            Put(_own);
            _own = _host = null;
            _hostPayload = null;
            Plugin.Log.LogInfo($"Back on your own FishMon settings ({Presets.Current} preset).");
        }

        public static void ResetHostState() { _lastSent = ""; _sendTimer = 0f; }

        // saves the config with your own values, never the host's
        public static void SaveConfig()
        {
            try
            {
                if (!FromHost) { Plugin.Instance.Config.Save(); return; }
                Plugin.QuietSettings = true;
                try { Put(_own); Plugin.Instance.Config.Save(); }
                finally { Put(_host); Plugin.QuietSettings = false; }
            }
            catch (System.Exception e) { Plugin.Log.LogError("Couldn't save config: " + e.Message); }
        }
    }
}
