using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace HowToFishMon
{
    // trading FishMon between players. Each player's FishMon live in their own save, so a trade is a
    // conversation between the two games (through the host):
    //   A: Request -> B: Accept (or Decline)
    //   both: Offer (the FishMon as JSON) - any new offer resets both confirmations
    //   both: Confirm (naming both offers) - once a game has both confirmations it swaps
    // Confirming locks your offer, so nobody can swap it out after the other side has said yes.
    public static class Trade
    {
        public const byte Request = 0, Accept = 1, Decline = 2, Offer = 3, Confirm = 4, Cancel = 5;
        public enum Phase { Idle, Asking, Trading }

        private const float AskTimeout = 30f;
        private const float InviteTimeout = 60f;

        public static Phase State { get; private set; }
        public static int Partner { get; private set; } = -1;
        public static string PartnerName { get; private set; } = "";
        public static FishMon MyOffer { get; private set; }
        public static FishMon TheirOffer { get; private set; }
        public static bool IConfirmed { get; private set; }
        public static bool TheyConfirmed { get; private set; }

        private static int _session;
        private static float _askedAt;

        public class Invite { public int From; public int Session; public string Name; public float At; }
        public static readonly List<Invite> Invites = new List<Invite>();

        private static readonly Color Info = new Color(0.7f, 0.9f, 1f);
        private static readonly Color Bad = new Color(1f, 0.6f, 0.5f);

        private static string NameOf(int clientId)
        {
            var p = Net.PlayerFor(clientId);
            return p && !string.IsNullOrEmpty(p.SteamName) ? p.SteamName : "Another player";
        }

        // actions from the menu

        public static void Ask(Player p)
        {
            if (State != Phase.Idle || !p || p.Owner == null) return;
            Partner = p.Owner.ClientId;
            PartnerName = NameOf(Partner);
            _session = Random.Range(1, int.MaxValue);
            _askedAt = Time.unscaledTime;
            State = Phase.Asking;
            Net.SendTrade(Partner, Request, _session);
        }

        public static void AcceptInvite(Invite inv)
        {
            Invites.Remove(inv);
            if (State != Phase.Idle) CancelNow();
            if (!Net.PlayerFor(inv.From)) { FishMonController.ShowToast($"{inv.Name} isn't here any more.", Bad); return; }
            Partner = inv.From;
            PartnerName = inv.Name;
            _session = inv.Session;
            State = Phase.Trading;
            Net.SendTrade(Partner, Accept, _session);
        }

        public static void DeclineInvite(Invite inv)
        {
            Invites.Remove(inv);
            Net.SendTrade(inv.From, Decline, inv.Session);
        }

        public static void SetOffer(FishMon m)
        {
            if (State != Phase.Trading || IConfirmed || m == MyOffer) return;
            MyOffer = m;
            IConfirmed = TheyConfirmed = false;
            Net.SendTrade(Partner, Offer, _session, m != null ? JsonConvert.SerializeObject(m) : "");
        }

        public static bool CanConfirm => State == Phase.Trading && !IConfirmed && MyOffer != null && TheirOffer != null && Owned(MyOffer);

        public static void ConfirmNow()
        {
            if (!CanConfirm) return;
            IConfirmed = true;
            Net.SendTrade(Partner, Confirm, _session, $"{MyOffer.Uid}>{TheirOffer.Uid}");
            TryFinish();
        }

        public static void CancelNow()
        {
            if (State != Phase.Idle && Partner >= 0) Net.SendTrade(Partner, Cancel, _session);
            Reset();
        }

        private static void Reset()
        {
            State = Phase.Idle;
            Partner = -1;
            PartnerName = "";
            MyOffer = TheirOffer = null;
            IConfirmed = TheyConfirmed = false;
            _session = 0;
        }

        private static bool Owned(FishMon m) => m != null && (FishMonStorage.Party.Contains(m) || FishMonStorage.Box.Contains(m));

        private static bool Ours(MonTradeMsg m) => State != Phase.Idle && m.Owner == Partner && m.Session == _session;

        // messages from the other player

        public static void OnMessage(MonTradeMsg m)
        {
            switch (m.Kind)
            {
                case Request:
                    Invites.RemoveAll(i => i.From == m.Owner);
                    var inv = new Invite { From = m.Owner, Session = m.Session, Name = NameOf(m.Owner), At = Time.unscaledTime };
                    Invites.Add(inv);
                    FishMonController.ShowToast($"{inv.Name} wants to trade FishMon! Open the party menu ({Plugin.KeyName(Plugin.PartyKey)}) → Trade.", Info, 6f);
                    break;

                case Accept:
                    if (State != Phase.Asking || m.Owner != Partner || m.Session != _session) return;
                    State = Phase.Trading;
                    FishMonController.ShowToast($"{PartnerName} wants to trade! Pick a FishMon to offer.", Info, 4f);
                    break;

                case Decline:
                    if (State != Phase.Asking || m.Owner != Partner || m.Session != _session) return;
                    FishMonController.ShowToast($"{PartnerName} doesn't want to trade right now.", Bad, 4f);
                    Reset();
                    break;

                case Offer:
                    if (!Ours(m) || State != Phase.Trading) return;
                    TheirOffer = Parse(m.Payload);
                    IConfirmed = TheyConfirmed = false;
                    break;

                case Confirm:
                    if (!Ours(m) || State != Phase.Trading || MyOffer == null || TheirOffer == null) return;
                    if (m.Payload != $"{TheirOffer.Uid}>{MyOffer.Uid}") return; // they confirmed different offers (one just changed)
                    TheyConfirmed = true;
                    TryFinish();
                    break;

                case Cancel:
                    Invites.RemoveAll(i => i.From == m.Owner && i.Session == m.Session);
                    if (!Ours(m)) return;
                    FishMonController.ShowToast($"{PartnerName} cancelled the trade.", Bad, 4f);
                    Reset();
                    break;
            }
        }

        // a FishMon from the network: fill in anything missing and clamp numbers, so a broken message can't break the save
        private static FishMon Parse(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                var m = JsonConvert.DeserializeObject<FishMon>(json);
                if (m == null) return null;
                if (m.BaseStats == null || m.BaseStats.Length != 6) m.BaseStats = new[] { 50, 50, 50, 50, 50, 50 };
                if (m.IVs == null || m.IVs.Length != 6) m.IVs = new int[6];
                if (m.EVs == null || m.EVs.Length != 6) m.EVs = new int[6];
                for (int i = 0; i < 6; i++) { m.IVs[i] = Mathf.Clamp(m.IVs[i], 0, 31); m.EVs[i] = Mathf.Clamp(m.EVs[i], 0, 252); }
                m.Level = Mathf.Clamp(m.Level, 1, 100);
                m.Nature = Mathf.Clamp(m.Nature, 0, Natures.All.Length - 1);
                if (string.IsNullOrEmpty(m.SpeciesName)) m.SpeciesName = "Fish";
                if (string.IsNullOrEmpty(m.Uid)) m.Uid = System.Guid.NewGuid().ToString("N");
                m.CurrentHp = Mathf.Clamp(m.CurrentHp, 0, m.MaxHp);
                return m;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("FishMon trade: couldn't read the other player's FishMon: " + e.Message);
                return null;
            }
        }

        private static void TryFinish()
        {
            if (!IConfirmed || !TheyConfirmed) return;
            var give = MyOffer;
            var get = TheirOffer;
            string from = PartnerName;
            Reset();

            if (!Owned(give)) { FishMonController.ShowToast("The trade failed: your FishMon isn't in your party or Box any more.", Bad, 5f); return; }
            FishMonController.ReleaseForTrade(give);
            get.TradedFrom = from;
            get.JustEvolved = false;
            bool toParty = FishMonStorage.AddTraded(get);
            FishMonController.ShowToast($"Traded {give.DisplayName} for {from}'s {get.DisplayName}!" + (toParty ? "" : " It went to your Box."), new Color(0.6f, 1f, 0.7f), 6f);
            try { AudioManager.PlayGlobalClip("Confetti"); } catch { }
            Plugin.Log.LogInfo($"Traded {give.SpeciesName} (Lv {give.Level}) for {get.SpeciesName} (Lv {get.Level}) with {from}");
        }

        // every frame: time out unanswered requests, and drop trades with players who left
        public static void Tick()
        {
            Invites.RemoveAll(i => Time.unscaledTime - i.At > InviteTimeout || !Net.PlayerFor(i.From));
            if (State == Phase.Idle) return;
            if (!Net.PlayerFor(Partner))
            {
                FishMonController.ShowToast($"{PartnerName} left, so the trade was cancelled.", Bad, 4f);
                Reset();
            }
            else if (State == Phase.Asking && Time.unscaledTime - _askedAt > AskTimeout)
            {
                FishMonController.ShowToast($"{PartnerName} didn't answer. (They need the latest FishMon mod to trade.)", Bad, 5f);
                CancelNow();
            }
            else if (MyOffer != null && !Owned(MyOffer))
            {
                // released it mid-trade
                MyOffer = null;
                IConfirmed = TheyConfirmed = false;
                Net.SendTrade(Partner, Offer, _session, "");
            }
        }

        // left the world / switched saves
        public static void Abort()
        {
            if (State != Phase.Idle) CancelNow();
            Invites.Clear();
        }
    }
}
