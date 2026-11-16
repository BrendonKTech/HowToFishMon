using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using UnityEngine;

namespace HowToFishMon
{

    public static class Battle
    {
        public const byte Request = 0, Accept = 1, Decline = 2, Start = 3, Hit = 4, Result = 5, Cancel = 6;
        public enum Phase { Idle, Asking, Waiting, Countdown, Fighting }
        private enum Outcome { None, Won, Lost, Draw }

        public const float MaxStartDistance = 50f;  // how close the other player has to be to ask
        public const float Leash = 80f;             // walk further than this from the arena and you forfeit
        public const float ArenaRadius = 9f;        // the FishMon stay inside this
        public const float TimeLimit = 180f;
        private const float CountdownTime = 3f;
        private const float AskTimeout = 30f, InviteTimeout = 60f, StartTimeout = 15f;
        private const float HomeOffset = 4f;        // where each FishMon lines up, from the middle

        public static Phase State { get; private set; }
        public static int Partner { get; private set; } = -1;
        public static string PartnerName { get; private set; } = "";
        public static FishMon Opponent { get; private set; }   // their FishMon as they sent it (at full HP)
        public static Companion Fighter { get; private set; }  // ours
        public static Vector3 Arena { get; private set; }
        private static Vector3 _axis = Vector3.forward;         // from the challenger's side towards the other side
        private static int _side;                               // -1 challenger, +1 the one who accepted
        private static int _session;
        private static float _phaseAt;
        private static int _lastCount;

        // how our FishMon was before the battle, put back afterwards
        private static FishMon _mon;
        private static int _savedHp;
        private static StatusCond _savedCond;
        private static float _savedUntil;
        private static string _usedItem;

        public class Invite { public int From; public int Session; public string Name; public float At; }
        public static readonly List<Invite> Invites = new List<Invite>();

        private class StartInfo { public string Mon; public float X, Y, Z, AX, AZ; }

        private static readonly CultureInfo CI = CultureInfo.InvariantCulture;
        private static readonly Color Info = new Color(0.7f, 0.9f, 1f);
        private static readonly Color Bad = new Color(1f, 0.6f, 0.5f);
        private static readonly Color Good = new Color(0.6f, 1f, 0.7f);

        public static bool InBattle => State == Phase.Countdown || State == Phase.Fighting;
        // a battle is lined up or going on: the FishMon that's out is spoken for
        public static bool Busy => State == Phase.Waiting || InBattle;
        public static bool Owns(Companion c) => InBattle && c && c == Fighter;

        public static float TimeLeft => State == Phase.Fighting ? Mathf.Max(0f, TimeLimit - (Time.unscaledTime - _phaseAt)) : TimeLimit;
        public static int CountdownLeft => Mathf.Max(0, Mathf.CeilToInt(CountdownTime - (Time.unscaledTime - _phaseAt)));

        // where our FishMon lines up, and which way "our side" of the arena is
        public static Vector3 HomeSpot => Arena + _axis * (_side * HomeOffset);
        public static Vector3 OurSide => _axis * _side;

        // the other player's FishMon as we see it
        public static RemoteCompanion OpponentBody => Partner >= 0 && Net.Remotes.TryGetValue(Partner, out var rc) && rc ? rc : null;

        private static string NameOf(int clientId)
        {
            var p = Net.PlayerFor(clientId);
            return p && !string.IsNullOrEmpty(p.SteamName) ? p.SteamName : "Another player";
        }

        private static string MyName()
        {
            var p = Player.LocalPlayer;
            return p && !string.IsNullOrEmpty(p.SteamName) ? p.SteamName : "A player";
        }

        private static float Flat(Vector3 v) { v.y = 0f; return v.magnitude; }

        private static bool BossUp => BossManager.Boss && !BossManager.Boss.IsDead;

        // can we battle right now? Sends out the lead FishMon if none is out, otherwise says why not
        private static bool ReadyToFight()
        {
            if (BossUp) { FishMonController.ShowToast("Beat the boss first, then battle!", Bad); return false; }
            if (!FishMonController.EnsureOut())
            {
                FishMonController.ShowToast("You need a FishMon that can fight. Revive one in the party menu first.", Bad);
                return false;
            }
            return true;
        }

        // asking

        public static void Ask(Player p)
        {
            if (State != Phase.Idle || !p || p.Owner == null) return;
            var me = Player.LocalPlayer;
            if (me && Flat(Companion.PlayerPos(me) - Companion.PlayerPos(p)) > MaxStartDistance)
            {
                FishMonController.ShowToast($"Get closer to {NameOf(p.Owner.ClientId)} to battle (within {MaxStartDistance:0} m).", Bad);
                return;
            }
            if (!ReadyToFight()) return;
            Partner = p.Owner.ClientId;
            PartnerName = NameOf(Partner);
            _session = Random.Range(1, int.MaxValue);
            _phaseAt = Time.unscaledTime;
            State = Phase.Asking;
            Net.SendBattle(Partner, Request, _session);
        }

        public static void AcceptInvite(Invite inv)
        {
            if (State != Phase.Idle) { FishMonController.ShowToast("Finish your other battle first.", Bad); return; }
            if (!Net.PlayerFor(inv.From)) { Invites.Remove(inv); FishMonController.ShowToast($"{inv.Name} isn't here any more.", Bad); return; }
            if (!ReadyToFight()) return;
            Invites.Remove(inv);
            Partner = inv.From;
            PartnerName = inv.Name;
            _session = inv.Session;
            Fighter = FishMonController.Active;
            _phaseAt = Time.unscaledTime;
            State = Phase.Waiting;
            Net.SendBattle(Partner, Accept, _session, JsonConvert.SerializeObject(Fighter.Mon));
        }

        public static void DeclineInvite(Invite inv)
        {
            Invites.Remove(inv);
            Net.SendBattle(inv.From, Decline, inv.Session);
        }

        // before it starts: call it off. Once it's started: give up
        public static void CancelNow()
        {
            if (InBattle) { Forfeit("You forfeited the battle."); return; }
            if (State != Phase.Idle && Partner >= 0) Net.SendBattle(Partner, Cancel, _session);
            Finish(Outcome.None);
        }

        public static void Forfeit(string why)
        {
            if (!InBattle) { CancelNow(); return; }
            FishMonController.ShowToast(why, Bad, 4f);
            SendResult('L', $"{MyName()} forfeited the battle against {PartnerName}.");
            Finish(Outcome.Lost);
        }

        // our FishMon's HP hit 0
        public static void Lose()
        {
            if (!InBattle) return;
            string mine = _mon != null ? _mon.DisplayName : "Your FishMon";
            string theirs = Opponent != null ? Opponent.DisplayName : "FishMon";
            SendResult('L', $"{PartnerName}'s {theirs} defeated {MyName()}'s {mine}!");
            FishMonController.ShowToast($"{mine} fainted! {PartnerName}'s {theirs} wins the battle.", Bad, 5f);
            Finish(Outcome.Lost);
        }

        private static void SendResult(char kind, string text) => Net.SendBattle(-1, Result, _session, kind + "|" + text);

        // our FishMon hit theirs: tell their game how much it took (it rolls the status itself)
        public static void SendHit(Move move, HitResult h)
        {
            if (State != Phase.Fighting) return;
            Net.SendBattle(Partner, Hit, _session,
                string.Join("|", move.Name, h.Damage.ToString(CI), h.Eff.ToString(CI), h.Crit ? "1" : "0"));
        }

        // a one-use item (berry, Focus Sash) got used up mid-battle: it comes back afterwards
        public static void NoteItemUsed(FishMon m, HeldItem item)
        {
            if (InBattle && m == _mon && _usedItem == null && item != HeldItem.None) _usedItem = item.ToString();
        }

        // receiving

        public static void OnMessage(MonBattleMsg m)
        {
            bool ours = State != Phase.Idle && m.Owner == Partner && m.Session == _session;
            switch (m.Kind)
            {
                case Request:
                    if (m.To != Net.MyClientId) return;
                    Invites.RemoveAll(i => i.From == m.Owner);
                    var inv = new Invite { From = m.Owner, Session = m.Session, Name = NameOf(m.Owner), At = Time.unscaledTime };
                    Invites.Add(inv);
                    FishMonController.ShowToast($"{inv.Name} wants to battle! Open the party menu ({Plugin.KeyName(Plugin.PartyKey)}) → Players.", Info, 6f);
                    break;

                case Accept:
                    if (!ours || State != Phase.Asking) return;
                    OnAccepted(m.Payload);
                    break;

                case Decline:
                    if (!ours || State != Phase.Asking) return;
                    FishMonController.ShowToast($"{PartnerName} doesn't want to battle right now.", Bad, 4f);
                    Finish(Outcome.None);
                    break;

                case Start:
                    if (!ours || State != Phase.Waiting) return;
                    OnStart(m.Payload);
                    break;

                case Hit:
                    if (!ours || State != Phase.Fighting || !Fighter) return;
                    var parts = (m.Payload ?? "").Split('|');
                    int n = parts.Length;
                    if (n < 4) return;
                    var move = Moves.Find(string.Join("|", parts, 0, n - 3));
                    if (move == null || !int.TryParse(parts[n - 3], NumberStyles.Integer, CI, out int dmg)) return;
                    float.TryParse(parts[n - 2], NumberStyles.Float, CI, out float eff);
                    Fighter.TakePvpHit(move, Mathf.Max(0, dmg), eff, parts[n - 1] == "1");
                    break;

                case Result:
                    string payload = m.Payload ?? "";
                    int bar = payload.IndexOf('|');
                    char kind = bar > 0 ? payload[0] : 'L';
                    string text = bar >= 0 ? payload.Substring(bar + 1) : payload;
                    if (ours && InBattle)
                    {
                        if (kind == 'D') { FishMonController.ShowToast("Time's up! The battle is a draw.", Info, 5f); Finish(Outcome.Draw); }
                        else { FishMonController.ShowToast("You won! " + text, Good, 6f); Finish(Outcome.Won); }
                    }
                    else if (!ours && text.Length > 0) FishMonController.ShowToast(text, Info, 5f); // someone else's battle
                    break;

                case Cancel:
                    Invites.RemoveAll(i => i.From == m.Owner && i.Session == m.Session);
                    if (!ours) return;
                    FishMonController.ShowToast($"{PartnerName} called off the battle.", Bad, 4f);
                    Finish(Outcome.None);
                    break;
            }
        }

        // challenger: they said yes. Pick the arena (halfway between the two of you) and start
        private static void OnAccepted(string payload)
        {
            var opp = Trade.Parse(payload);
            var me = Player.LocalPlayer;
            var them = Net.PlayerFor(Partner);
            if (BossUp) { FishMonController.ShowToast("A boss showed up, so the battle was called off.", Bad); CancelNow(); return; }
            if (opp == null || !me || !them || !FishMonController.EnsureOut())
            {
                FishMonController.ShowToast("The battle couldn't start.", Bad);
                CancelNow();
                return;
            }

            Vector3 a = Companion.PlayerPos(me), b = Companion.PlayerPos(them);
            Vector3 axis = b - a; axis.y = 0f;
            if (axis.sqrMagnitude < 0.01f) axis = Companion.PlayerForward(me);
            Opponent = opp;
            Arena = (a + b) * 0.5f;
            _axis = axis.normalized;
            _side = -1;
            Fighter = FishMonController.Active;

            var info = new StartInfo { Mon = JsonConvert.SerializeObject(Fighter.Mon), X = Arena.x, Y = Arena.y, Z = Arena.z, AX = _axis.x, AZ = _axis.z };
            Net.SendBattle(Partner, Start, _session, JsonConvert.SerializeObject(info));
            BeginCountdown();
        }

        // the one who accepted: the challenger sent their FishMon and the arena
        private static void OnStart(string payload)
        {
            StartInfo info = null;
            try { info = JsonConvert.DeserializeObject<StartInfo>(payload); } catch { }
            var opp = info != null ? Trade.Parse(info.Mon) : null;
            if (opp == null || !Fighter || Fighter != FishMonController.Active)
            {
                FishMonController.ShowToast("The battle couldn't start.", Bad);
                CancelNow();
                return;
            }
            Opponent = opp;
            Arena = new Vector3(info.X, info.Y, info.Z);
            var axis = new Vector3(info.AX, 0f, info.AZ);
            _axis = axis.sqrMagnitude > 0.01f ? axis.normalized : Vector3.forward;
            _side = 1;
            BeginCountdown();
        }

        private static void BeginCountdown()
        {
            _mon = Fighter.Mon;
            _savedHp = _mon.CurrentHp;
            _savedCond = _mon.Status.Cond;
            _savedUntil = _mon.Status.Until;
            _usedItem = null;
            // everyone starts at full HP with no status
            _mon.CurrentHp = _mon.MaxHp;
            _mon.Status.Clear();

            State = Phase.Countdown;
            _phaseAt = Time.unscaledTime;
            _lastCount = -1;
            Invites.RemoveAll(i => i.From == Partner);
            Fighter.BeginPvp();
            FishMonController.ShowToast($"Battle! Your {_mon.DisplayName} vs {PartnerName}'s {Opponent.DisplayName}", Info, 4f);
            Plugin.Log.LogInfo($"Battle with {PartnerName}: {_mon.SpeciesName} (Lv {_mon.Level}) vs {Opponent.SpeciesName} (Lv {Opponent.Level})");
        }

        // ends the battle (or calls off one that hasn't started) and puts our FishMon back how it was
        private static void Finish(Outcome how)
        {
            var mon = _mon;
            var opp = Opponent;
            if (Fighter) Fighter.EndPvp();

            if (mon != null)
            {
                mon.CurrentHp = Mathf.Clamp(_savedHp, 0, mon.MaxHp);
                mon.Status.Cond = _savedCond;
                mon.Status.Until = _savedUntil;
                mon.Status.NextTick = Time.time + Statuses.Tick;
                if (_usedItem != null && System.Enum.TryParse(_usedItem, out HeldItem used))
                {
                    if (mon.Item == HeldItem.None) mon.Item = used;
                    else FishMonStorage.AddToBag(used); // it was given something else mid-battle: the old one goes in the bag
                }
            }

            string partner = PartnerName;
            State = Phase.Idle;
            Partner = -1;
            PartnerName = "";
            Opponent = null;
            Fighter = null;
            _mon = null;
            _session = 0;
            _usedItem = null;

            if (mon == null) return;
            if (how != Outcome.None && opp != null)
            {
                int full = Mathf.Max(1, Mathf.RoundToInt(10f * opp.Level * Plugin.ExpMultiplier.Value));
                int exp = Mathf.Max(1, how == Outcome.Won ? full : how == Outcome.Draw ? full / 2 : full / 3);
                FishMonController.GiveExp(mon, exp, null);
                FishMonController.ShowToast($"{mon.DisplayName} gained {exp} Exp. Points!", new Color(0.7f, 0.85f, 1f));
                if (how == Outcome.Won) { try { AudioManager.PlayGlobalClip("Confetti"); } catch { } }
                Plugin.Log.LogInfo($"Battle with {partner} over: {how}, +{exp} exp");
            }
            FishMonStorage.Save();
        }

        // every frame

        public static void Tick()
        {
            float now = Time.unscaledTime;
            Invites.RemoveAll(i => now - i.At > InviteTimeout || !Net.PlayerFor(i.From));
            if (State == Phase.Idle) return;

            if (!Net.PlayerFor(Partner))
            {
                FishMonController.ShowToast($"{PartnerName} left, so the battle was called off.", Bad, 4f);
                Finish(Outcome.None);
                return;
            }

            if (State == Phase.Asking)
            {
                if (now - _phaseAt > AskTimeout)
                {
                    FishMonController.ShowToast($"{PartnerName} didn't answer. (They need the latest FishMon mod to battle.)", Bad, 5f);
                    CancelNow();
                }
                return;
            }
            if (State == Phase.Waiting)
            {
                if (!Fighter || Fighter != FishMonController.Active) { FishMonController.ShowToast("Your FishMon went back, so the battle was called off.", Bad); CancelNow(); }
                else if (now - _phaseAt > StartTimeout) { FishMonController.ShowToast("The battle didn't start. Try again?", Bad); CancelNow(); }
                return;
            }

            // countdown / fighting
            if (!Fighter || Fighter != FishMonController.Active)
            {
                Forfeit($"{(_mon != null ? _mon.DisplayName : "Your FishMon")} left the battle, so you forfeited.");
                return;
            }
            var me = Player.LocalPlayer;
            if (me && Flat(Companion.PlayerPos(me) - Arena) > Leash)
            {
                Forfeit("You went too far from the battle, so you forfeited.");
                return;
            }

            if (State == Phase.Countdown)
            {
                int left = CountdownLeft;
                if (left != _lastCount && left > 0)
                {
                    _lastCount = left;
                    FishMonController.AddFloat(Arena + Vector3.up * 2.5f, left.ToString(), Color.white, 0.9f);
                    try { AudioManager.PlayGlobalClip("Hitmarker"); } catch { }
                }
                if (now - _phaseAt >= CountdownTime)
                {
                    State = Phase.Fighting;
                    _phaseAt = now;
                    FishMonController.AddFloat(Arena + Vector3.up * 2.5f, "Battle!", new Color(1f, 0.6f, 0.35f), 1.5f);
                    try { AudioManager.PlayGlobalClip("Hitmarker"); } catch { }
                }
                return;
            }

            // time limit: the challenger calls it (the other side has a fallback in case that message never comes)
            float fought = now - _phaseAt;
            if ((_side < 0 && fought > TimeLimit) || fought > TimeLimit + 10f)
            {
                if (_side < 0) SendResult('D', $"{MyName()} and {PartnerName} battled to a draw.");
                FishMonController.ShowToast("Time's up! The battle is a draw.", Info, 5f);
                Finish(Outcome.Draw);
            }
        }

        // leaving the world / switching saves
        public static void Abort()
        {
            if (InBattle) Forfeit("You left, so you forfeited the battle.");
            else if (State != Phase.Idle) CancelNow();
            Invites.Clear();
        }
    }
}
