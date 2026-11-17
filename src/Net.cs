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
        public byte Kind;    // 0 = launch, 1 = hit, 2 = missed / no effect, 3 = spread move hitting an extra foe
        public string Move;
        public Vector3 Pos, Dir;
        public float Eff;
        public bool Crit;
        public bool InWater;
    }

    // guest -> host only: "my Merchant wants to sell this item" (the host checks it's really theirs)
    public struct MonSellMsg : IBroadcast
    {
        public int ObjectId;
    }

    // the friend prank (see Prank.cs). The host stamps who sent it, so the target's game can check it came from me
    public struct MonPrankMsg : IBroadcast
    {
        public int Owner;
    }

    // guest -> host only: "give me this Fishdex reward money" (only the host can add money). Capped by the host
    public struct MonRewardMsg : IBroadcast
    {
        public int Amount;
    }

    // guest -> host only: abilities that need the host (healing you, digging up loot, sell bonus, faster bites)
    public struct MonAbilityMsg : IBroadcast
    {
        public const byte State = 0, Heal = 1, Dig = 2;
        public byte Kind;
        public string Ability;   // State: the ability of the FishMon it has out ("" = none / switched off). Sent by name, not number.
        public Vector3 Pos;      // Dig: where
    }

    // a status condition: on the sender's own FishMon (Foe = false) or on a creature it's fighting (Foe = true)
    public struct MonStatusMsg : IBroadcast
    {
        public int Owner;
        public bool Foe;
        public int ObjectId;   // the creature's network id (Foe only)
        public byte Cond;      // StatusCond, None = cured
        public float Seconds;  // how long it has left
    }

    // trading. Relayed to everyone, only the player it's addressed To acts on it (see Trade.cs)
    public struct MonTradeMsg : IBroadcast
    {
        public int Owner;
        public int To;
        public byte Kind;
        public int Session;
        public string Payload;
    }

    public struct MonBattleMsg : IBroadcast
    {
        public int Owner;
        public int To;
        public byte Kind;
        public int Session;
        public string Payload;
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

        // host: the ability each guest's FishMon has out right now
        private static readonly Dictionary<int, Ability> _remoteAbility = new Dictionary<int, Ability>();

        public static Ability RemoteAbility(int clientId) => _remoteAbility.TryGetValue(clientId, out var a) ? a : Ability.None;

        // players with the mod (they send info heartbeats every few seconds), for the trade menu
        private static readonly Dictionary<int, float> _modPlayers = new Dictionary<int, float>();

        public static List<Player> ModPlayers()
        {
            var list = new List<Player>();
            foreach (var kv in _modPlayers)
            {
                if (Time.unscaledTime - kv.Value > RemoteTimeout || IsMe(kv.Key)) continue;
                var p = PlayerFor(kv.Key);
                if (p) list.Add(p);
            }
            return list;
        }

        public static int MyClientId
        {
            get { var me = Player.LocalPlayer; return me && me.Owner != null ? me.Owner.ClientId : -1; }
        }

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
            SetSellSerializer();

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

        private static void SetSellSerializer()
        {
            GenericWriter<MonSettingsMsg>.SetWrite((w, m) => w.WriteString(m.Payload ?? ""));
            GenericReader<MonSettingsMsg>.SetRead(r => new MonSettingsMsg { Payload = r.ReadStringAllocated() });
            GenericWriter<MonSellMsg>.SetWrite((w, m) => w.WriteInt32(m.ObjectId));
            GenericReader<MonSellMsg>.SetRead(r => new MonSellMsg { ObjectId = r.ReadInt32() });
            GenericWriter<MonPrankMsg>.SetWrite((w, m) => w.WriteInt32(m.Owner));
            GenericReader<MonPrankMsg>.SetRead(r => new MonPrankMsg { Owner = r.ReadInt32() });
            GenericWriter<MonRewardMsg>.SetWrite((w, m) => w.WriteInt32(m.Amount));
            GenericReader<MonRewardMsg>.SetRead(r => new MonRewardMsg { Amount = r.ReadInt32() });
            GenericWriter<MonAbilityMsg>.SetWrite((w, m) => { w.WriteUInt8Unpacked(m.Kind); w.WriteString(m.Ability ?? ""); w.WriteVector3(m.Pos); });
            GenericReader<MonAbilityMsg>.SetRead(r => new MonAbilityMsg { Kind = r.ReadUInt8Unpacked(), Ability = r.ReadStringAllocated(), Pos = r.ReadVector3() });

            GenericWriter<MonStatusMsg>.SetWrite((w, m) =>
            {
                w.WriteInt32(m.Owner); w.WriteBoolean(m.Foe); w.WriteInt32(m.ObjectId); w.WriteUInt8Unpacked(m.Cond); w.WriteSingle(m.Seconds);
            });
            GenericReader<MonStatusMsg>.SetRead(r => new MonStatusMsg
            {
                Owner = r.ReadInt32(), Foe = r.ReadBoolean(), ObjectId = r.ReadInt32(), Cond = r.ReadUInt8Unpacked(), Seconds = r.ReadSingle(),
            });

            GenericWriter<MonTradeMsg>.SetWrite((w, m) =>
            {
                w.WriteInt32(m.Owner); w.WriteInt32(m.To); w.WriteUInt8Unpacked(m.Kind); w.WriteInt32(m.Session); w.WriteString(m.Payload ?? "");
            });
            GenericReader<MonTradeMsg>.SetRead(r => new MonTradeMsg
            {
                Owner = r.ReadInt32(), To = r.ReadInt32(), Kind = r.ReadUInt8Unpacked(), Session = r.ReadInt32(), Payload = r.ReadStringAllocated(),
            });

            GenericWriter<MonBattleMsg>.SetWrite((w, m) =>
            {
                w.WriteInt32(m.Owner); w.WriteInt32(m.To); w.WriteUInt8Unpacked(m.Kind); w.WriteInt32(m.Session); w.WriteString(m.Payload ?? "");
            });
            GenericReader<MonBattleMsg>.SetRead(r => new MonBattleMsg
            {
                Owner = r.ReadInt32(), To = r.ReadInt32(), Kind = r.ReadUInt8Unpacked(), Session = r.ReadInt32(), Payload = r.ReadStringAllocated(),
            });
        }

        // status conditions, so other players see BRN / PAR... on our FishMon and on what it's fighting
        public static void SendStatus(StatusState s)
        {
            Send(new MonStatusMsg { Foe = false, Cond = (byte)(Statuses.Has(s) ? s.Cond : StatusCond.None), Seconds = Remaining(s) });
        }

        public static void SendFoeStatus(Creature c, StatusState s)
        {
            if (!c || !c.NetworkObject) return;
            Send(new MonStatusMsg { Foe = true, ObjectId = c.NetworkObject.ObjectId, Cond = (byte)(Statuses.Has(s) ? s.Cond : StatusCond.None), Seconds = Remaining(s) });
        }

        private static float Remaining(StatusState s) => Statuses.Has(s) ? s.Until - Time.time : 0f;

        private static void OnStatus(MonStatusMsg m)
        {
            if (IsMe(m.Owner)) return;
            var cond = (StatusCond)m.Cond;
            if (!m.Foe)
            {
                if (Remotes.TryGetValue(m.Owner, out var rc) && rc) rc.SetStatus(cond, m.Seconds);
                return;
            }
            var nm = _registeredOn;
            if (!nm || !nm.ClientManager.Objects.Spawned.TryGetValue(m.ObjectId, out var nob) || !nob) return;
            var c = nob.GetComponent<Creature>();
            if (!c) c = nob.GetComponentInChildren<Creature>();
            if (c) Statuses.ShowRemoteFoe(c, cond, m.Seconds);
        }

        public static void SendTrade(int to, byte kind, int session, string payload = "")
        {
            Send(new MonTradeMsg { To = to, Kind = kind, Session = session, Payload = payload });
        }

        public static void SendBattle(int to, byte kind, int session, string payload = "")
        {
            Send(new MonBattleMsg { To = to, Kind = kind, Session = session, Payload = payload });
        }

        // guest: ask the host to do something for our FishMon's ability
        public static bool SendAbility(byte kind, Vector3 pos = default)
        {
            if (!ClientOnline(_registeredOn)) return false;
            Send(new MonAbilityMsg { Kind = kind, Pos = pos });
            return true;
        }

        // host: a guest's FishMon ability wants something
        private static void OnAbilityMsg(NetworkConnection from, MonAbilityMsg m)
        {
            var player = PlayerFor(from.ClientId);
            if (m.Kind == MonAbilityMsg.State)
            {
                _remoteAbility[from.ClientId] = System.Enum.TryParse(m.Ability, out Ability a) ? a : Ability.None;
                return;
            }
            if (!player) return;
            var has = RemoteAbility(from.ClientId);
            if (m.Kind == MonAbilityMsg.Heal && has == Ability.Medic) Medic.HealOnServer(player);
            else if (m.Kind == MonAbilityMsg.Dig && has == Ability.TreasureHunter) TreasureHunter.DigOnServer(player, m.Pos);
        }

        public static void SendPrank() => Send(new MonPrankMsg());

        // guest: ask the host for Fishdex reward money
        public static bool SendReward(int amount)
        {
            if (amount <= 0 || !ClientOnline(_registeredOn)) return false;
            Send(new MonRewardMsg { Amount = amount });
            return true;
        }

        // guest: ask the host to sell an item for our Merchant
        public static bool SendSell(Item it)
        {
            if (!it || !it.NetworkObject || !ClientOnline(_registeredOn)) return false;
            Send(new MonSellMsg { ObjectId = it.NetworkObject.ObjectId });
            return true;
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
            nm.ServerManager.RegisterBroadcast<MonSellMsg>((conn, m, ch) => Safe(() => Merchant.OnSellRequest(conn, m.ObjectId)));
            nm.ServerManager.RegisterBroadcast<MonAbilityMsg>((conn, m, ch) => Safe(() => OnAbilityMsg(conn, m)));
            nm.ServerManager.RegisterBroadcast<MonRewardMsg>((conn, m, ch) => Safe(() => DexRewards.GiveOnServer(PlayerFor(conn.ClientId), m.Amount)));
            nm.ServerManager.RegisterBroadcast<MonStatusMsg>((conn, m, ch) => { m.Owner = conn.ClientId; Relay(conn, m, ch); });
            nm.ServerManager.RegisterBroadcast<MonTradeMsg>((conn, m, ch) => { m.Owner = conn.ClientId; Relay(conn, m, ch); });
            nm.ServerManager.RegisterBroadcast<MonBattleMsg>((conn, m, ch) => { m.Owner = conn.ClientId; Relay(conn, m, ch); });
            nm.ServerManager.RegisterBroadcast<MonPrankMsg>((conn, m, ch) => { m.Owner = conn.ClientId; Relay(conn, m, ch); });

            // every client: show other players' FishMon
            nm.ClientManager.RegisterBroadcast<MonInfoMsg>((m, ch) => Safe(() => OnInfo(m)));
            nm.ClientManager.RegisterBroadcast<MonPosMsg>((m, ch) => Safe(() => OnPos(m)));
            nm.ClientManager.RegisterBroadcast<MonAttackMsg>((m, ch) => Safe(() => OnAttack(m)));
            nm.ClientManager.RegisterBroadcast<MonStatusMsg>((m, ch) => Safe(() => OnStatus(m)));
            nm.ClientManager.RegisterBroadcast<MonTradeMsg>((m, ch) => Safe(() => { if (m.To == MyClientId) Trade.OnMessage(m); }));
            nm.ClientManager.RegisterBroadcast<MonBattleMsg>((m, ch) => Safe(() => { if (!IsMe(m.Owner) && (m.To == MyClientId || m.To == -1)) Battle.OnMessage(m); }));
            nm.ClientManager.RegisterBroadcast<MonPrankMsg>((m, ch) => Safe(() => { if (!IsMe(m.Owner)) Prank.OnMessage(m.Owner); }));
            // only the host sends these (straight from its server), guests use them
            nm.ClientManager.RegisterBroadcast<MonSettingsMsg>((m, ch) => Safe(() => LobbySettings.OnMessage(m)));
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
                _remoteAbility.Clear();
                _modPlayers.Clear();
                if (Remotes.Count > 0) ClearRemotes();
                _lastActive = null;
                _lastInfoKey = "";
                LobbySettings.Restore();
                LobbySettings.ResetHostState();
                return;
            }

            if (IsHost) Safe(() => LobbySettings.HostTick(nm));

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
            return $"{m.Uid}|{m.Level}|{m.Stage}|{m.DisplayName}|{m.SpeciesId}|{m.Ability}|{m.AbilityOn}";
        }

        private static void SendInfo(Companion c)
        {
            // tell the host which ability we have out (it applies the ones that run on its side). It's resent with
            // every info heartbeat, so a host that joined or reloaded catches up.
            if (!IsHost) Send(new MonAbilityMsg { Kind = MonAbilityMsg.State, Ability = c && c.Mon.AbilityOn ? c.Mon.Ability.ToString() : "" });
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
            // so players who joined late still see its status
            if (Statuses.Has(m.Status)) SendStatus(m.Status);
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
            _modPlayers[m.Owner] = Time.unscaledTime;
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

            // forget the abilities of players who left
            _dead.Clear();
            foreach (var id in _remoteAbility.Keys) if (!PlayerFor(id)) _dead.Add(id);
            foreach (var id in _dead) _remoteAbility.Remove(id);
        }

        public static void ClearRemotes()
        {
            foreach (var rc in Remotes.Values) if (rc) rc.Despawn(false);
            Remotes.Clear();
        }
    }
}
