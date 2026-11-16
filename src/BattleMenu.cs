using UnityEngine;

namespace HowToFishMon
{
    // the battle side of the Players tab, and the bar at the top of the screen while a battle is on
    public partial class FishMonController
    {
        private static readonly Color BattleOrange = new Color(1f, 0.55f, 0.3f);

        private void DrawBattlePanel(Rect r)
        {
            float x = r.x + 18, w = r.width - 36, y = r.y + 10;

            if (Battle.State == Battle.Phase.Asking || Battle.State == Battle.Phase.Waiting)
            {
                bool asking = Battle.State == Battle.Phase.Asking;
                GUI.Label(new Rect(x, y, w, 26), asking ? "Waiting..." : "Getting ready...", Styles.BodyBold);
                GUI.Label(new Rect(x, y + 34, w, 60), asking
                    ? $"Asked <b>{Battle.PartnerName}</b> to battle. Waiting for them to accept."
                    : $"Starting the battle with <b>{Battle.PartnerName}</b>...", Styles.SmallWrap);
                if (GUI.Button(new Rect(x, y + 100, 160, 36), "Cancel", Styles.Button)) Battle.CancelNow();
                return;
            }

            GUI.Label(new Rect(x, y, w, 26), $"Battle vs {Battle.PartnerName}", Styles.BodyBold);
            y += 40;
            float gap = 12f, cw = (w - gap) / 2f, ch = 150f;
            var mine = Battle.Fighter ? Battle.Fighter.Mon : null;
            var body = Battle.OpponentBody;
            DrawBattleCard(new Rect(x, y, cw, ch), "Your FishMon", mine, mine != null ? mine.CurrentHp / (float)Mathf.Max(1, mine.MaxHp) : 0f,
                mine != null ? mine.Status : null);
            DrawBattleCard(new Rect(x + cw + gap, y, cw, ch), $"{Battle.PartnerName}'s FishMon", Battle.Opponent, body ? body.HpFrac : 1f,
                body ? body.Status : null);
            y += ch + 16;

            string clock = Battle.State == Battle.Phase.Countdown
                ? $"Starting in <b>{Battle.CountdownLeft}</b>..."
                : $"Time left: <b>{Mathf.FloorToInt(Battle.TimeLeft / 60f)}:{Mathf.FloorToInt(Battle.TimeLeft % 60f):00}</b>";
            GUI.Label(new Rect(x, y, w, 24), clock, Styles.Small);
            y += 30;
            GUI.Label(new Rect(x, y, w, 90),
                $"Close this menu and walk around to watch! Stay within {Battle.Leash:0} m of the battle, or you forfeit. " +
                "If time runs out, it's a draw.", Styles.SmallWrap);

            if (GUI.Button(new Rect(x, r.yMax - 58, 180, 44), "Forfeit", Styles.DangerButton)) Battle.CancelNow();
        }

        private void DrawBattleCard(Rect r, string title, FishMon m, float hpFrac, StatusState status)
        {
            Styles.Fill(r, new Color(1f, 1f, 1f, 0.05f));
            float x = r.x + 12, w = r.width - 24, y = r.y + 8;
            GUI.Label(new Rect(x, y, w, 22), title, Styles.Dim);
            y += 26;
            if (m == null) return;
            string star = m.Shiny ? "<color=#ffd84a>★ </color>" : "";
            GUI.Label(new Rect(x, y, w, 26), $"{star}{m.DisplayName}  <size=13>Lv.{m.Level}</size>{Statuses.RichTag(status)}", Styles.BodyBold);
            y += 30;
            Styles.TypeChip(new Rect(x, y, 78, 22), m.Type1);
            if (m.Type2 != MonType.None && m.Type2 != m.Type1) Styles.TypeChip(new Rect(x + 84, y, 78, 22), m.Type2);
            y += 32;
            Styles.HpBar(new Rect(x, y, w, 12), hpFrac);
            y += 18;
            GUI.Label(new Rect(x, y, w, 22), $"HP {Mathf.RoundToInt(Mathf.Clamp01(hpFrac) * 100f)}%", Styles.Small);
        }

        // top of the screen during a battle: both FishMon's HP and the clock
        private void DrawBattleHud()
        {
            if (!Battle.InBattle) return;
            var mine = Battle.Fighter ? Battle.Fighter.Mon : null;
            var theirs = Battle.Opponent;
            if (mine == null || theirs == null) return;
            var body = Battle.OpponentBody;

            float w = 640f, h = 66f;
            var r = new Rect((VW - w) / 2f, 16f, w, h);
            Styles.Fill(r, new Color(0.06f, 0.08f, 0.12f, 0.85f));
            Styles.Border(r, BattleOrange, 2f);

            float side = (w - 120f) / 2f;
            var left = new Rect(r.x + 14, r.y + 8, side - 14, h - 16);
            var right = new Rect(r.xMax - side, r.y + 8, side - 14, h - 16);
            GUI.Label(new Rect(left.x, left.y, left.width, 22), $"{mine.DisplayName}  <size=12>Lv.{mine.Level}{Statuses.RichTag(mine.Status)}</size>", Styles.SmallBold);
            Styles.HpBar(new Rect(left.x, left.y + 28, left.width, 10), mine.CurrentHp / (float)Mathf.Max(1, mine.MaxHp));
            GUI.Label(new Rect(right.x, right.y, right.width, 22), $"{theirs.DisplayName}  <size=12>Lv.{theirs.Level}{(body ? Statuses.RichTag(body.Status) : "")}</size>", Styles.SmallBold);
            Styles.HpBar(new Rect(right.x, right.y + 28, right.width, 10), body ? body.HpFrac : 1f);
            GUI.Label(new Rect(right.x, right.y + 38, right.width, 18), $"<size=11><color=#9fc4ff>{Battle.PartnerName}</color></size>", Styles.Small);

            var mid = new Rect(r.x + side, r.y, 120f, h);
            string clock = Battle.State == Battle.Phase.Countdown
                ? $"<b>{Battle.CountdownLeft}</b>"
                : $"{Mathf.FloorToInt(Battle.TimeLeft / 60f)}:{Mathf.FloorToInt(Battle.TimeLeft % 60f):00}";
            GUI.Label(new Rect(mid.x, mid.y + 6, mid.width, 26), "<b><color=#ff9a5a>VS</color></b>", Styles.Center);
            GUI.Label(new Rect(mid.x, mid.y + 32, mid.width, 26), $"<size=13>{clock}</size>", Styles.Center);
        }
    }
}
