using System.Linq;
using UnityEngine;

namespace HowToFishMon
{
    public partial class FishMonController
    {
        private bool _showingTrade;
        private Vector2 _tradeScroll, _tradeInviteScroll;

        private void DrawTrade(Rect win, float h)
        {
            var left = new Rect(win.x + 16, win.y + 62, 330, h - 78);
            var right = new Rect(left.xMax + 10, win.y + 62, win.xMax - left.xMax - 26, h - 78);
            Styles.Fill(left, Styles.PanelDark);
            Styles.Fill(right, Styles.PanelDark);

            if (Trade.State == Trade.Phase.Trading) { DrawTradePickList(left); DrawTradeOffers(right); }
            else
            {
                DrawTradePlayers(left);
                if (Battle.State != Battle.Phase.Idle) DrawBattlePanel(right);
                else DrawTradeRequests(right);
            }
        }

        private void DrawTradePlayers(Rect r)
        {
            GUI.Label(new Rect(r.x + 14, r.y + 10, r.width - 28, 26), "Players with FishMon", Styles.BodyBold);
            var players = Net.ModPlayers();
            if (players.Count == 0)
            {
                GUI.Label(new Rect(r.x + 14, r.y + 44, r.width - 28, 200),
                    "Nobody else here has the FishMon mod yet.\n\nTo trade or battle, both of you need this version of the mod, and so does the host.", Styles.SmallWrap);
                return;
            }

            var view = new Rect(r.x + 6, r.y + 44, r.width - 12, r.height - 52);
            _tradeScroll = GUI.BeginScrollView(view, _tradeScroll, new Rect(0, 0, view.width - 20, players.Count * 50f));
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                var row = new Rect(4, i * 50f, view.width - 28, 44);
                Styles.Fill(row, new Color(1f, 1f, 1f, 0.06f));
                GUI.Label(new Rect(row.x + 10, row.y, row.width - 196, row.height), p.SteamName, Styles.Small);
                GUI.enabled = Battle.State == Battle.Phase.Idle;
                if (GUI.Button(new Rect(row.xMax - 180, row.y + 6, 86, 32), "Battle", Styles.Button)) Battle.Ask(p);
                GUI.enabled = Trade.State == Trade.Phase.Idle;
                if (GUI.Button(new Rect(row.xMax - 88, row.y + 6, 82, 32), "Trade", Styles.Button)) Trade.Ask(p);
                GUI.enabled = true;
            }
            GUI.EndScrollView();
        }

        private void DrawTradeRequests(Rect r)
        {
            float x = r.x + 18, w = r.width - 36, y = r.y + 10;
            if (Trade.State == Trade.Phase.Asking)
            {
                GUI.Label(new Rect(x, y, w, 26), "Waiting...", Styles.BodyBold);
                GUI.Label(new Rect(x, y + 34, w, 60), $"Asked <b>{Trade.PartnerName}</b> to trade. Waiting for them to accept.", Styles.SmallWrap);
                if (GUI.Button(new Rect(x, y + 100, 160, 36), "Cancel", Styles.Button)) Trade.CancelNow();
                return;
            }

            GUI.Label(new Rect(x, y, w, 26), "Requests", Styles.BodyBold);
            var invites = Trade.Invites.ToList();
            var battles = Battle.Invites.ToList();
            if (invites.Count == 0 && battles.Count == 0)
            {
                GUI.Label(new Rect(x, y + 34, w, r.height - 50),
                    "No one has asked to trade or battle yet.\n\n<b>Battles</b>\n" +
                    "Press <b>Battle</b> next to a player who's close by. Once they accept, the FishMon you both have out " +
                    "fight it out halfway between you, and you can walk around and watch. Afterwards HP and items go back to " +
                    "how they were, and both FishMon get EXP (the winner gets the most).\n\n<b>Trading</b>\n" +
                    "1. Press <b>Trade</b> next to a player.\n" +
                    "2. Once they accept, each of you picks a FishMon to offer. You can see each other's stats, ability and IVs.\n" +
                    "3. When you're both happy, you both press <b>Confirm trade</b>. Changing your offer resets both confirmations.",
                    Styles.SmallWrap);
                return;
            }

            int rows = battles.Count + invites.Count;
            var view = new Rect(r.x + 6, y + 40, r.width - 12, r.height - 50);
            _tradeInviteScroll = GUI.BeginScrollView(view, _tradeInviteScroll, new Rect(0, 0, view.width - 20, rows * 56f));
            for (int i = 0; i < rows; i++)
            {
                bool battle = i < battles.Count;
                var row = new Rect(4, i * 56f, view.width - 28, 50);
                Styles.Fill(row, battle ? new Color(1f, 0.5f, 0.3f, 0.1f) : new Color(1f, 0.85f, 0.3f, 0.08f));
                string name = battle ? battles[i].Name : invites[i - battles.Count].Name;
                GUI.Label(new Rect(row.x + 12, row.y, row.width - 260, row.height), $"<b>{name}</b> wants to {(battle ? "battle" : "trade")}", Styles.Small);
                bool accept = GUI.Button(new Rect(row.xMax - 236, row.y + 8, 110, 34), "Accept", Styles.Button);
                bool decline = GUI.Button(new Rect(row.xMax - 118, row.y + 8, 110, 34), "Decline", Styles.DangerButton);
                if (battle)
                {
                    if (accept) Battle.AcceptInvite(battles[i]);
                    else if (decline) Battle.DeclineInvite(battles[i]);
                }
                else
                {
                    if (accept) Trade.AcceptInvite(invites[i - battles.Count]);
                    else if (decline) Trade.DeclineInvite(invites[i - battles.Count]);
                }
            }
            GUI.EndScrollView();
        }

        private void DrawTradePickList(Rect r)
        {
            GUI.Label(new Rect(r.x + 14, r.y + 10, r.width - 28, 26), Trade.IConfirmed ? "Your offer is locked in" : "Pick a FishMon to offer", Styles.BodyBold);
            var all = FishMonStorage.Party.Concat(FishMonStorage.Box).ToList();
            int partyCount = FishMonStorage.Party.Count;

            var view = new Rect(r.x + 6, r.y + 44, r.width - 12, r.height - 52);
            _tradeScroll = GUI.BeginScrollView(view, _tradeScroll, new Rect(0, 0, view.width - 20, all.Count * 46f));
            GUI.enabled = !Trade.IConfirmed;
            for (int i = 0; i < all.Count; i++)
            {
                var m = all[i];
                var row = new Rect(4, i * 46f, view.width - 28, 40);
                bool sel = m == Trade.MyOffer;
                Styles.Fill(row, sel ? new Color(0.25f, 0.45f, 0.7f, 0.9f) : new Color(1f, 1f, 1f, 0.06f));
                string star = m.Shiny ? "<color=#ffd84a>★</color>" : "";
                string where = i < partyCount ? "" : "  <size=12><color=#9aa6bd>Box</color></size>";
                string fainted = m.Fainted ? "  <size=12><color=#ff9a9a>fainted</color></size>" : "";
                GUI.Label(new Rect(row.x + 10, row.y, row.width - 20, row.height), $"{star}{m.DisplayName}  <size=13>Lv.{m.Level}</size>{where}{fainted}", Styles.Small);
                if (GUI.Button(row, GUIContent.none, GUIStyle.none)) Trade.SetOffer(m);
            }
            GUI.enabled = true;
            GUI.EndScrollView();
        }

        private void DrawTradeOffers(Rect r)
        {
            float pad = 14f, gap = 12f;
            float cw = (r.width - pad * 2 - gap) / 2f, ch = r.height - 140f;
            var mine = new Rect(r.x + pad, r.y + pad, cw, ch);
            var theirs = new Rect(mine.xMax + gap, mine.y, cw, ch);
            DrawTradeCard(mine, "You offer", Trade.MyOffer, Trade.IConfirmed);
            DrawTradeCard(theirs, $"{Trade.PartnerName} offers", Trade.TheirOffer, Trade.TheyConfirmed);

            float y = mine.yMax + 14f;
            string status =
                Trade.IConfirmed && Trade.TheyConfirmed ? "Trading..." :
                Trade.IConfirmed ? $"You confirmed. Waiting for {Trade.PartnerName}..." :
                Trade.TheyConfirmed ? $"<color=#9fffb0>{Trade.PartnerName} confirmed!</color> Confirm too to trade." :
                Trade.MyOffer == null ? "Pick one of your FishMon on the left." :
                Trade.TheirOffer == null ? $"Waiting for {Trade.PartnerName} to pick a FishMon..." :
                "Happy with it? Both of you need to confirm.";
            GUI.Label(new Rect(r.x + pad, y, r.width - pad * 2, 26), status, Styles.Small);

            GUI.enabled = Trade.CanConfirm;
            if (GUI.Button(new Rect(r.x + pad, r.yMax - 58, 220, 44), "Confirm trade", Styles.Button)) Trade.ConfirmNow();
            GUI.enabled = true;
            if (GUI.Button(new Rect(r.xMax - pad - 180, r.yMax - 58, 180, 44), "Cancel trade", Styles.DangerButton)) Trade.CancelNow();
        }

        private void DrawTradeCard(Rect r, string title, FishMon m, bool confirmed)
        {
            Styles.Fill(r, new Color(1f, 1f, 1f, 0.05f));
            if (confirmed) Styles.Border(r, new Color(0.5f, 1f, 0.6f, 0.9f), 2f);
            float x = r.x + 14, w = r.width - 28, y = r.y + 10;
            GUI.Label(new Rect(x, y, w, 22), title + (confirmed ? "  <color=#9fffb0>✔ confirmed</color>" : ""), Styles.Dim);
            y += 28;
            if (m == null)
            {
                GUI.Label(new Rect(x, r.y + r.height / 2f - 20, w, 40), "<color=#9aa6bd>Nothing picked yet</color>", Styles.Center);
                return;
            }

            string star = m.Shiny ? "<color=#ffd84a>★ </color>" : "";
            string stage = m.Stage > 0 ? $"  <size=12><color=#ffd84a>{new string('◆', m.Stage)}</color></size>" : "";
            GUI.Label(new Rect(x, y, w, 28), star + m.DisplayName + stage, Styles.BodyBold);
            y += 28;
            GUI.Label(new Rect(x, y, w, 22), $"{m.SpeciesName}  ·  Lv. {m.Level}", Styles.Small);
            y += 26;
            Styles.TypeChip(new Rect(x, y, 78, 22), m.Type1);
            if (m.Type2 != MonType.None && m.Type2 != m.Type1) Styles.TypeChip(new Rect(x + 84, y, 78, 22), m.Type2);
            y += 32;

            GUI.Label(new Rect(x, y, w, 22), $"{Natures.All[m.Nature].name} nature", Styles.Small);
            y += 24;
            var ab = m.AbilityInfo;
            if (ab != null)
            {
                string col = Abilities.RarityColor(ab.Rarity);
                GUI.Label(new Rect(x, y, w, 22), $"Ability: <b><color={col}>{ab.Name}</color></b> <size=12><color={col}>{ab.Rarity}</color></size>", Styles.Small);
                y += 24;
            }
            GUI.Label(new Rect(x, y, w, 22), $"IVs {m.IVTotal}/186  <color=#9aa6bd>({m.IVJudge()})</color>", Styles.Small);
            y += 24;
            string hp = m.Fainted ? "<color=#ff9a9a>Fainted</color>" : $"HP {m.CurrentHp}/{m.MaxHp}";
            GUI.Label(new Rect(x, y, w, 22), $"{hp}  ·  Bosses defeated: {m.BossesDefeated}", Styles.Small);
            y += 30;

            GUI.Label(new Rect(x, y, w, 20), "Moves", Styles.SmallBold);
            y += 22;
            foreach (var mv in m.GetMoves())
            {
                if (y > r.yMax - 24) break;
                GUI.Label(new Rect(x, y, w, 20), $"<color=#{ColorUtility.ToHtmlStringRGB(TypeChart.ColorOf(mv.Type))}>■</color> {mv.Name} <size=12><color=#9aa6bd>Pow {mv.Power}</color></size>", Styles.Small);
                y += 21;
            }
        }
    }
}
