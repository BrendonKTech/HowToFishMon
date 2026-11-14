using System.Collections.Generic;
using FishNet.Broadcast;
using FishNet.Connection;
using FishNet.Managing;
using FishNet.Serializing;
using FishNet.Transporting;
using UnityEngine;

namespace HowToFishMon
{
    // messages. Each client sends these to the host, the host stamps who
    // sent it (Owner = their client id) and passes it on to everyone else
    // players without the mod never see them: FishNet skips broadcasts it
    // has no handler for

    // "This is the FishMon I have out" (or Out = false: "I recalled it")
    public struct MonInfoMsg : IBroadcast
    {
        public int Owner;
        public bool Out;
        public byte SpeciesId;
        public bool Shiny;
        public byte Stage;
        public byte Level;
        public byte Type1, Type2;
        public string Name;
    }

    // where it is right now. Sent ~15 times a second, unreliable.
    public struct MonPosMsg : IBroadcast
    {
        public int Owner;
        public Vector3 Pos;
        public float Yaw;
        public byte State;   // Companion.State
        public bool InWater;
        public float HpFrac;
    }

    // an attack, so other players see the move name and effects
    public struct MonAttackMsg : IBroadcast
    {
        public int Owner;
        public byte Kind;    // 0 = launch, 1 = hit, 2 = missed / no effect
        public string Move;
        public Vector3 Pos, Dir;
        public float Eff;
        public bool Crit;
        public bool InWater;
    }

    // sends your FishMon to other players and shows theirs
    public static class Net
    {
        private const float PosRate = 1f / 15f;
        private const float InfoHeartbeat = 3f;   // resend info now and then, so players who join late see it
        private const float RemoteTimeout = 10f;

        public static readonly Dictionary<int, RemoteCompanion> Remotes = new Dictionary<int, RemoteCompanion>();

        private static NetworkManager _registeredOn;
        private static bool _serializersSet;
        private static float _posTimer, _infoTimer;
        private static Companion _lastActive;
        private static string _lastInfoKey = "";

        private static NetworkManager Manager
        {
            get
            {
                var all = NetworkManager.Instances;
                return all != null && all.Count > 0 ? all[0] : null;
            }
        }

        // true when this game is the host (also true in solo worlds)
        public static bool IsHost
        {
            get { var nm = Manager; return nm && nm.ServerManager && nm.ServerManager.Started; }
        }

        private static bool ClientOnline(NetworkManager nm) => nm && nm.ClientManager && nm.ClientManager.Started;

        // setup

        private static void SetSerializers()
        {
            if (_serializersSet) return;
            _serializersSet = true;

            GenericWriter<MonInfoMsg>.SetWrite((w, m) =>
            {
                w.WriteInt32(m.Owner); w.WriteBoolean(m.Out); w.WriteUInt8Unpacked(m.SpeciesId); w.WriteBoolean(m.Shiny);
                w.WriteUInt8Unpacked(m.Stage); w.WriteUInt8Unpacked(m.Level); w.WriteUInt8Unpacked(m.Type1); w.WriteUInt8Unpacked(m.Type2);
                w.WriteString(m.Name ?? "");
            });
            GenericReader<MonInfoMsg>.SetRead(r => new MonInfoMsg
            {
                Owner = r.ReadInt32(), Out = r.ReadBoolean(), SpeciesId = r.ReadUInt8Unpacked(), Shiny = r.ReadBoolean(),
                Stage = r.ReadUInt8Unpacked(), Level = r.ReadUInt8Unpacked(), Type1 = r.ReadUInt8Unpacked(), Type2 = r.ReadUInt8Unpacked(),
                Name = r.ReadStringAllocated(),
            });

            GenericWriter<MonPosMsg>.SetWrite((w, m) =>
            {
                w.WriteInt32(m.Owner); w.WriteVector3(m.Pos); w.WriteSingle(m.Yaw); w.WriteUInt8Unpacked(m.State);
                w.WriteBoolean(m.InWater); w.WriteSingle(m.HpFrac);
            });
            GenericReader<MonPosMsg>.SetRead(r => new MonPosMsg
            {
                Owner = r.ReadInt32(), Pos = r.ReadVector3(), Yaw = r.ReadSingle(), State = r.ReadUInt8Unpacked(),
                InWater = r.ReadBoolean(), HpFrac = r.ReadSingle(),
            });

            GenericWriter<MonAttackMsg>.SetWrite((w, m) =>
            {
                w.WriteInt32(m.Owner); w.WriteUInt8Unpacked(m.Kind); w.WriteString(m.Move ?? ""); w.WriteVector3(m.Pos); w.WriteVector3(m.Dir);
                w.WriteSingle(m.Eff); w.WriteBoolean(m.Crit); w.WriteBoolean(m.InWater);
            });
            GenericReader<MonAttackMsg>.SetRead(r => new MonAttackMsg
            {
                Owner = r.ReadInt32(), Kind = r.ReadUInt8Unpacked(), Move = r.ReadStringAllocated(), Pos = r.ReadVector3(), Dir = r.ReadVector3(),
                Eff = r.ReadSingle(), Crit = r.ReadBoolean(), InWater = r.ReadBoolean(),
            });
        }

        private static void EnsureRegistered(NetworkManager nm)
        {
            if (!nm || nm == _registeredOn) return;
            SetSerializers();
            _registeredOn = nm;

            // host side: pass every message on to all the other players
            nm.ServerManager.RegisterBroadcast<MonInfoMsg>((conn, m, ch) => { m.Owner = conn.ClientId; Relay(conn, m, ch); });
            nm.ServerManager.RegisterBroadcast<MonPosMsg>((conn, m, ch) => { m.Owner = conn.ClientId; Relay(conn, m, ch); });
            nm.ServerManager.RegisterBroadcast<MonAttackMsg>((conn, m, ch) => { m.Owner = conn.ClientId; Relay(conn, m, ch); });

            // every client: show other players' FishMon
            nm.ClientManager.RegisterBroadcast<MonInfoMsg>((m, ch) => Safe(() => OnInfo(m)));
            nm.ClientManager.RegisterBroadcast<MonPosMsg>((m, ch) => Safe(() => OnPos(m)));
            nm.ClientManager.RegisterBroadcast<MonAttackMsg>((m, ch) => Safe(() => OnAttack(m)));
            Plugin.Log.LogInfo("FishMon multiplayer sync ready.");
        }

        private static void Relay<T>(NetworkConnection from, T msg, Channel ch) where T : struct, IBroadcast
        {
            var nm = _registeredOn;
            if (!nm || !nm.ServerManager.Started) return;
            try { nm.ServerManager.BroadcastExcept(from, msg, true, ch); }
            catch (System.Exception e) { Plugin.Log.LogWarning("FishMon relay failed: " + e.Message); }
        }

        private static void Safe(System.Action a)
        {
            try { a(); }
            catch (System.Exception e) { Plugin.Log.LogWarning("FishMon sync message failed: " + e.Message); }
        }

        private static void Send<T>(T msg, Channel ch = Channel.Reliable) where T : struct, IBroadcast
        {
            var nm = _registeredOn;
            if (!ClientOnline(nm)) return;
            try { nm.ClientManager.Broadcast(msg, ch); }
            catch (System.Exception e) { Plugin.Log.LogWarning("FishMon send failed: " + e.Message); }
        }

        // sending (called every frame)

        public static void Tick()
        {
            var nm = Manager;
            EnsureRegistered(nm);
            if (!ClientOnline(nm))
            {
                if (Remotes.Count > 0) ClearRemotes();
                _lastActive = null;
                _lastInfoKey = "";
                return;
            }

            var active = FishMonController.Active;
            string key = InfoKey(active);
            _infoTimer -= Time.unscaledDeltaTime;
            if (key != _lastInfoKey || active != _lastActive || _infoTimer <= 0f)
            {
                _infoTimer = InfoHeartbeat;
                _lastActive = active;
                _lastInfoKey = key;
                SendInfo(active);
            }

            if (active)
            {
                _posTimer -= Time.unscaledDeltaTime;
                if (_posTimer <= 0f)
                {
                    _posTimer = PosRate;
                    var m = active.Mon;
                    Send(new MonPosMsg
                    {
                        Pos = active.transform.position,
                        Yaw = active.transform.eulerAngles.y,
                        State = active.StateByte,
                        InWater = active.InWater,
                        HpFrac = Mathf.Clamp01(m.CurrentHp / (float)Mathf.Max(1, m.MaxHp)),
                    }, Channel.Unreliable);
                }
            }

            TickRemotes();
        }

        private static string InfoKey(Companion c)
        {
            if (!c) return "";
            var m = c.Mon;
            return $"{m.Uid}|{m.Level}|{m.Stage}|{m.DisplayName}|{m.SpeciesId}";
        }

        private static void SendInfo(Companion c)
        {
            if (!c) { Send(new MonInfoMsg { Out = false }); return; }
            var m = c.Mon;
            Send(new MonInfoMsg
            {
                Out = true,
                SpeciesId = m.SpeciesId,
                Shiny = m.Shiny,
                Stage = (byte)Mathf.Clamp(m.Stage, 0, 255),
                Level = (byte)Mathf.Clamp(m.Level, 0, 255),
                Type1 = (byte)m.Type1,
                Type2 = (byte)m.Type2,
                Name = m.DisplayName,
            });
        }

        public static void SendAttack(byte kind, Move move, Vector3 pos, Vector3 dir, float eff, bool crit, bool inWater)
        {
            Send(new MonAttackMsg { Kind = kind, Move = move?.Name, Pos = pos, Dir = dir, Eff = eff, Crit = crit, InWater = inWater });
        }

        // receiving

        public static Player PlayerFor(int clientId)
        {
            foreach (var p in PlayerManager.Players)
                if (p && p.Owner != null && p.Owner.ClientId == clientId) return p;
            return null;
        }

        private static void OnInfo(MonInfoMsg m)
        {
            if (IsMe(m.Owner)) return;
            Remotes.TryGetValue(m.Owner, out var rc);
            if (!m.Out)
            {
                if (rc) rc.Despawn(true);
                Remotes.Remove(m.Owner);
                return;
            }
            if (rc && rc.Matches(m)) { rc.Refresh(m); return; }

            // new FishMon (or it evolved / swapped): rebuild the model
            Vector3? at = rc ? rc.transform.position : (Vector3?)null;
            bool evolved = rc && rc.Info.SpeciesId == m.SpeciesId && m.Stage > rc.Info.Stage;
            if (rc) rc.Despawn(!evolved);
            var owner = PlayerFor(m.Owner);
            Vector3 pos = at ?? (owner ? Companion.PlayerPos(owner) + Companion.PlayerForward(owner) * 2f : Vector3.zero);
            if (!at.HasValue && !owner) { Remotes.Remove(m.Owner); return; } // don't know where yet; wait for the owner to load in
            Remotes[m.Owner] = RemoteCompanion.Spawn(m, pos, !evolved);
            if (evolved) Fx.Evolve(pos, TypeChart.ColorOf((MonType)m.Type1));
        }

        private static void OnPos(MonPosMsg m)
        {
            if (IsMe(m.Owner)) return;
            if (Remotes.TryGetValue(m.Owner, out var rc) && rc) rc.OnPos(m);
        }

        private static void OnAttack(MonAttackMsg m)
        {
            if (IsMe(m.Owner)) return;
            if (Remotes.TryGetValue(m.Owner, out var rc) && rc) rc.OnAttack(m);
        }

        private static bool IsMe(int clientId)
        {
            var me = Player.LocalPlayer;
            return me && me.Owner != null && me.Owner.ClientId == clientId;
        }

        private static readonly List<int> _dead = new List<int>();

        private static void TickRemotes()
        {
            _dead.Clear();
            foreach (var kv in Remotes)
            {
                var rc = kv.Value;
                // owner left, went quiet, or the object got destroyed by a scene change
                if (!rc || !PlayerFor(kv.Key) || Time.unscaledTime - rc.LastHeard > RemoteTimeout)
                {
                    if (rc) rc.Despawn(false);
                    _dead.Add(kv.Key);
                }
            }
            foreach (var id in _dead) Remotes.Remove(id);
        }

        public static void ClearRemotes()
        {
            foreach (var rc in Remotes.Values) if (rc) rc.Despawn(false);
            Remotes.Clear();
        }
    }
}
