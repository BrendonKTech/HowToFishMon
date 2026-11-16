using UnityEngine;

namespace HowToFishMon
{
    // the Item tab of a FishMon's details: what it's holding, your bag, and the shop
    public partial class FishMonController
    {
        private Vector2 _itemScroll;

        private void DrawItemTab(Rect r, FishMon m)
        {
            float x = r.x, w = r.width, y = r.y;

            // what it holds now
            var held = m.ItemInfo;
            var top = new Rect(x, y, w, 58);
            Styles.Fill(top, new Color(1f, 1f, 1f, 0.05f));
            if (held != null)
            {
                GUI.Label(new Rect(top.x + 12, top.y + 4, w - 180, 26), $"Holding: <b><color=#ffd84a>{held.Name}</color></b>", Styles.Small);
                GUI.Label(new Rect(top.x + 12, top.y + 28, w - 180, 24), held.Desc, Styles.Dim);
                if (GUI.Button(new Rect(top.xMax - 150, top.y + 12, 140, 34), "Take back", Styles.Button)) TakeItem(m);
            }
            else
            {
                GUI.Label(new Rect(top.x + 12, top.y + 4, w - 24, 26), "Not holding anything.", Styles.Small);
                GUI.Label(new Rect(top.x + 12, top.y + 28, w - 24, 24), "Buy an item below, then give it to this FishMon. It can hold one at a time.", Styles.Dim);
            }
            y = top.yMax + 12;

            // every item: how many you have, buy, give
            GUI.Label(new Rect(x, y, w, 22), $"Items  <color=#9aa6bd>(you have ${MoneyManager.Money})</color>", Styles.SmallBold);
            y += 26;
            var view = new Rect(x, y, w, r.yMax - y);
            float rowH = 54f;
            _itemScroll = GUI.BeginScrollView(view, _itemScroll, new Rect(0, 0, w - 20, Items.All.Length * (rowH + 6)));
            for (int i = 0; i < Items.All.Length; i++)
            {
                var it = Items.All[i];
                var row = new Rect(0, i * (rowH + 6), w - 22, rowH);
                bool holding = m.Holds(it.Id);
                Styles.Fill(row, holding ? new Color(1f, 0.85f, 0.3f, 0.12f) : new Color(1f, 1f, 1f, 0.05f));

                int own = FishMonStorage.BagCount(it.Id);
                string ownTag = own > 0 ? $"  <size=13><color=#9fffb0>×{own}</color></size>" : "";
                string oneUse = it.OneUse ? "  <size=12><color=#9aa6bd>one use</color></size>" : "";
                GUI.Label(new Rect(row.x + 10, row.y + 3, row.width - 250, 24), $"<b>{it.Name}</b>{ownTag}{oneUse}", Styles.Small);
                GUI.Label(new Rect(row.x + 10, row.y + 27, row.width - 250, 22), it.Desc, Styles.Dim);

                GUI.enabled = own > 0 && !holding;
                if (GUI.Button(new Rect(row.xMax - 232, row.y + 10, 90, 34), "Give", Styles.Button)) GiveItem(m, it);
                GUI.enabled = true;
                int price = Items.PriceOf(it);
                if (GUI.Button(new Rect(row.xMax - 134, row.y + 10, 128, 34), price > 0 ? $"Buy (${price})" : "Get (free)", Styles.Button)) BuyItem(it);
            }
            GUI.EndScrollView();
        }

        private void BuyItem(ItemInfo it)
        {
            if (!TryPay(Items.PriceOf(it), $"buy a {it.Name}")) return;
            FishMonStorage.AddToBag(it.Id);
            ShowToast($"Bought a {it.Name}! ({FishMonStorage.BagCount(it.Id)} in your bag)", new Color(1f, 0.85f, 0.4f), 3f);
            try { AudioManager.PlayGlobalClip("Hitmarker"); } catch { }
        }

        private void GiveItem(FishMon m, ItemInfo it)
        {
            if (!FishMonStorage.TakeFromBag(it.Id)) return;
            var old = m.ItemInfo;
            if (old != null) FishMonStorage.AddToBag(old.Id); // swap: the old one goes back in the bag
            m.Item = it.Id;
            FishMonStorage.Save();
            ShowToast(old != null ? $"{m.DisplayName} is now holding a {it.Name}. The {old.Name} went back in your bag."
                                  : $"{m.DisplayName} is now holding a {it.Name}.", new Color(0.7f, 0.9f, 1f), 3.5f);
        }

        private void TakeItem(FishMon m)
        {
            var old = m.ItemInfo;
            if (old == null) return;
            m.Item = HeldItem.None;
            FishMonStorage.AddToBag(old.Id);
            ShowToast($"Took the {old.Name} from {m.DisplayName}.", new Color(0.7f, 0.9f, 1f), 3f);
        }
    }
}
