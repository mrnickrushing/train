using System;
using System.Linq;
using Ironhorse.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ironhorse.Game
{
    /// <summary>Shop, featured offers, fuel, Rail Season Pass and rewarded-ad screens.</summary>
    public sealed partial class UIRoot
    {
        string _shopTab = "offers";
        Text _fuelText, _shopBadge, _passBadge, _shopTimer;
        Image _fuelFill;

        static readonly Color Purple = Palette.Hex(0x7E4BA6);
        static readonly Color PurpleLight = Palette.Hex(0xA77BD0);
        static readonly Color FuelOrange = Palette.Hex(0xE0752D);

        // ================================================================ HUD pieces

        void BuildMoneyHud()
        {
            // Fuel gauge, placed left of the currency chips.
            var chip = UIKit.KitImage(_safe, "pill", "Fuel", 64);
            HudShadow(chip.gameObject);
            UIKit.Place(chip.gameObject, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -98), new Vector2(330, 64));
            UIKit.HStack(chip.gameObject, 10, 8, TextAnchor.MiddleLeft);
            FuelIcon(chip.transform, 48);
            var holder = UIKit.Node("Gauge", chip.transform);
            UIKit.Size(holder, -1, 40, 1);
            UIKit.HStack(holder, 0);
            _fuelFill = UIKit.Bar(holder.transform, FuelOrange, 34);
            _fuelText = UIKit.Label(_fuelFill.transform.parent, "", 22, UIKit.Cream, TextAnchor.MiddleCenter, true, FontStyle.Bold);
            UIKit.Stretch(_fuelText.gameObject);
            _fuelText.gameObject.AddComponent<Outline>().effectColor = new Color(0, 0, 0, 0.6f);
            var plus = UIKit.KitImage(chip.transform, "round_green", "Plus");
            UIKit.Size(plus.gameObject, 52, 52);
            var pl = UIKit.Label(plus.transform, "+", 40, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            pl.font = UIKit.Display; pl.fontStyle = FontStyle.Normal;
            UIKit.Stretch(pl.gameObject);
            UIKit.Stroke(pl, new Color(0.05f, 0.25f, 0.05f, 0.9f), 2f);
            Clickable(chip.gameObject, OpenFuel);

            // Shop and Season Pass buttons, bottom-right like a storefront sign.
            var side = UIKit.Node("SideButtons", _safe);
            UIKit.Place(side, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 190), new Vector2(170, 330));
            UIKit.VStack(side, 14, 0, TextAnchor.LowerCenter);
            _passBadge = SideButton(side.transform, "Season", Purple, () => OpenPass(), PassGlyph, out _);
            _shopBadge = SideButton(side.transform, "Shop", UIKit.Red, () => OpenShop(), ShopGlyph, out _shopTimer);
        }

        Text SideButton(Transform parent, string label, Color c, Action onClick, Action<Transform> glyph, out Text timer)
        {
            var holder = UIKit.Node(label, parent);
            UIKit.Size(holder, 150, 150);
            var circle = HudCircle(holder.transform, c, 112);
            var gl = UIKit.Node("Glyph", circle.transform);
            UIKit.Stretch(gl, 24);
            UIKit.HStack(gl, 0, 0, TextAnchor.MiddleCenter);
            glyph(gl.transform);
            timer = HudPlate(holder.transform, label, 150, 42, 22);
            Clickable(holder, onClick);
            var badge = UIKit.Badge(circle.transform, "", UIKit.Red);
            badge.transform.parent.gameObject.SetActive(false);
            return badge;
        }

        static void FuelIcon(Transform t, float size)
        {
            if (UIKit.Icon(t, "fuel", size * 1.1f) != null) return;
            // A lump of coal with a flame: "Coal & Water".
            var h = UIKit.Node("FuelIcon", t);
            UIKit.Size(h, size, size);
            var flame = UIKit.Panel(h.transform, FuelOrange, "Flame", UIKit.RoundedSmall);
            UIKit.Place(flame.gameObject, new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.5f, size * 0.5f)).localRotation = Quaternion.Euler(0, 0, 45);
            var core = UIKit.Panel(h.transform, UIKit.Gold, "Core", UIKit.Circle);
            UIKit.Place(core.gameObject, new Vector2(0.5f, 0.52f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.26f, size * 0.26f));
            var coal = UIKit.Panel(h.transform, Palette.Hex(0x2B2B2E), "Coal", UIKit.RoundedSmall);
            UIKit.Place(coal.gameObject, new Vector2(0.5f, 0.22f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.8f, size * 0.34f));
        }

        static void ShopGlyph(Transform t)
        {
            var h = UIKit.Node("Shop", t); UIKit.Size(h, 58, 56);
            Shape(h, UIKit.Cream, new Vector2(0.5f, 0.38f), new Vector2(46, 34));
            Shape(h, UIKit.Gold, new Vector2(0.5f, 0.78f), new Vector2(54, 16));
            Shape(h, UIKit.Red, new Vector2(0.5f, 0.3f), new Vector2(14, 20));
        }

        static void PassGlyph(Transform t)
        {
            var h = UIKit.Node("Pass", t); UIKit.Size(h, 56, 56);
            Shape(h, UIKit.Cream, new Vector2(0.5f, 0.5f), new Vector2(50, 34));
            Shape(h, UIKit.Gold, new Vector2(0.5f, 0.5f), new Vector2(20, 20), UIKit.Circle);
            Shape(h, Purple, new Vector2(0.18f, 0.5f), new Vector2(8, 34));
            Shape(h, Purple, new Vector2(0.82f, 0.5f), new Vector2(8, 34));
        }

        void RefreshMoneyHud()
        {
            if (_fuelText == null) return;
            int fuel = S.Fuel, max = S.MaxFuel;
            _fuelText.text = fuel >= max ? fuel + "/" + max : fuel + "/" + max + "  " + Fmt.Time(S.NextFuelIn);
            UIKit.SetBar(_fuelFill, fuel / (float)max);
            int claim = S.ClaimablePassRewards;
            _passBadge.text = claim.ToString();
            _passBadge.transform.parent.gameObject.SetActive(claim > 0);
            var weekly = S.WeeklyOffer;
            bool deal = S.StarterAvailable || weekly != null;
            _shopBadge.text = "!";
            _shopBadge.transform.parent.gameObject.SetActive(deal);
            _shopTimer.text = !S.StarterAvailable && weekly != null ? Fmt.Time(S.WeekEndsIn) : "SHOP";
        }

        // ================================================================ Shop

        public void OpenShop(string tab = null)
        {
            if (tab != null) _shopTab = tab;
            ShowWindow("Trading Post", content =>
            {
                Tabs(content, new (string, bool, Action)[]
                {
                    ("Offers", _shopTab == "offers", () => _shopTab = "offers"),
                    ("Nuggets", _shopTab == "nuggets", () => _shopTab = "nuggets"),
                    ("Supplies", _shopTab == "supplies", () => _shopTab = "supplies"),
                });
                if (_shopTab == "offers")
                {
                    foreach (var o in S.ActiveOffers().Where(o => o.ProductId != null)) OfferCard(content, o);
                    if (!S.HasFuelTank) ProductCard(content, Catalog.Product(Catalog.FuelTank), "Raise your fuel limit by " + Balance.FuelTankBonus + " forever, plus 200 fuel now.");
                    if (!S.State.passPremium) UIKit.Btn(content, "Rail Season Pass: unlock premium rewards", Purple, () => OpenPass(), 24, -1, 70);
                }
                else if (_shopTab == "nuggets")
                {
                    Line(content, "Gold nuggets rush timers, top up fuel and buy supplies.", 20, UIKit.InkSoft);
                    var grid = UIKit.Node("Grid", content);
                    var g = grid.AddComponent<GridLayoutGroup>();
                    g.cellSize = new Vector2(360, 330);
                    g.spacing = new Vector2(18, 18);
                    g.childAlignment = TextAnchor.UpperCenter;
                    var fit = grid.AddComponent<ContentSizeFitter>();
                    fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                    int i = 0;
                    foreach (var p in Catalog.Products.Where(p => p.Id.StartsWith("ironhorse.nuggets_"))) NuggetCard(grid.transform, p, i++);
                    WatchAdCard(content, AdReward.Nuggets, "Free nuggets", "+" + Balance.AdNuggets + " nuggets for watching a short video");
                    UIKit.Btn(content, "Restore purchases", UIKit.WoodLight, () => Store.Current.Restore(n => Toast(n > 0 ? "Restored " + n + " purchase(s)" : "Nothing to restore", true)), 20, -1, 52);
                }
                else
                {
                    Line(content, "Spend nuggets on deliveries straight to your warehouse.", 20, UIKit.InkSoft);
                    foreach (var o in S.ActiveOffers().Where(o => o.ProductId == null))
                    {
                        var c = Card(content, 110);
                        var col = Col(c.transform);
                        Line(col.transform, o.Title, 26, UIKit.Ink, true);
                        Line(col.transform, o.Subtitle, 20, UIKit.InkSoft);
                        var price = UIKit.Btn(c.transform, "", UIKit.Green, () => Result(S.BuyNuggetOffer(o.Id)), 24, 200, 64, S.Nuggets >= o.NuggetPrice);
                        PriceLabelWithNugget(price, o.NuggetPrice);
                    }
                }
            }, 1300, 900);
        }

        void PriceLabelWithNugget(Button b, int amount)
        {
            var label = b.GetComponentInChildren<Text>();
            label.text = "";
            var row = UIKit.Node("Price", label.transform.parent);
            UIKit.Stretch(row, 6);
            UIKit.HStack(row, 8, 0, TextAnchor.MiddleCenter);
            UIKit.NuggetIcon(row.transform, 34);
            UIKit.Label(row.transform, amount.ToString(), 26, UIKit.Cream, TextAnchor.MiddleLeft, true, FontStyle.Bold);
        }

        // ---------------------------------------------------------------- store cards

        /// <summary>Item tile: painted icon on a light slot with the amount printed over its corner.</summary>
        static GameObject GrantTile(Transform parent, Action<Transform> icon, string amount, float size)
        {
            var tile = UIKit.KitImage(parent, "tile", "Tile", size);
            UIKit.Size(tile.gameObject, size, size);
            var ic = UIKit.Node("Icon", tile.transform);
            UIKit.Stretch(ic, size * 0.12f);
            UIKit.HStack(ic, 0, 0, TextAnchor.MiddleCenter);
            icon(ic.transform);
            var t = UIKit.Label(tile.transform, amount, Mathf.RoundToInt(size * 0.27f), Color.white, TextAnchor.LowerRight, false, FontStyle.Bold);
            t.font = UIKit.Display;
            t.fontStyle = FontStyle.Normal;
            UIKit.Anchor(t.gameObject, Vector2.zero, Vector2.one, new Vector2(2, 0), new Vector2(-6, 0));
            UIKit.Stroke(t, new Color(0.15f, 0.08f, 0.03f, 0.95f), 2f);
            return tile.gameObject;
        }

        /// <summary>A grant as a row of item tiles (nuggets, coins, fuel, goods).</summary>
        static void GrantTiles(Transform parent, Grant g, float size)
        {
            var row = UIKit.Row(parent, size, 10);
            float ic = size * 0.72f;
            if (g.Nuggets > 0) GrantTile(row.transform, t => UIKit.NuggetIcon(t, ic), Fmt.Num(g.Nuggets), size);
            if (g.Coins > 0) GrantTile(row.transform, t => UIKit.CoinIcon(t, ic), Fmt.Num(g.Coins), size);
            if (g.Fuel > 0) GrantTile(row.transform, t => FuelIcon(t, ic * 0.92f), g.Fuel.ToString(), size);
            if (g.FuelTank) GrantTile(row.transform, t => FuelIcon(t, ic * 0.92f), "MAX +" + Balance.FuelTankBonus, size);
            if (g.Resources != null)
                foreach (var r in g.Resources) { var id = r.id; GrantTile(row.transform, t => UIKit.ResIcon(t, id, ic), r.amount.ToString(), size); }
        }

        /// <summary>Rotated starburst seal ("+400% VALUE") pinned to a card's corner.</summary>
        static void ValueSeal(Transform card, string badge)
        {
            var seal = UIKit.Node("Seal", card);
            seal.AddComponent<LayoutElement>().ignoreLayout = true;
            UIKit.Place(seal, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(84, -34), new Vector2(140, 140));
            for (int k = 0; k < 3; k++)
            {
                var sq = UIKit.Panel(seal.transform, k == 0 ? Palette.Hex(0x7A1E12) : Palette.Hex(0xE0482E), "Burst", UIKit.RoundedSmall);
                UIKit.Place(sq.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(k == 0 ? 126 : 116, k == 0 ? 126 : 116)).localRotation = Quaternion.Euler(0, 0, k * 30);
            }
            var bl = UIKit.Label(seal.transform, badge.Replace(" ", "\n"), 28, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            bl.font = UIKit.Display;
            bl.fontStyle = FontStyle.Normal; // the face is already bold
            UIKit.Stretch(bl.gameObject, 18);
            bl.transform.localRotation = Quaternion.Euler(0, 0, 12);
            UIKit.Stroke(bl, new Color(0.35f, 0.05f, 0.02f, 0.95f), 2f);
        }

        /// <summary>Small coloured tag ("ENDS IN 3d", "ONE TIME ONLY").</summary>
        static Text Tag(Transform parent, string text, Color c, float w)
        {
            var tag = UIKit.KitImage(parent, UIKit.KitFor(c) ?? "btn_red", "Tag", 44);
            UIKit.Size(tag.gameObject, w, 44);
            var t = UIKit.Label(tag.transform, text, 22, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            t.font = UIKit.Display;
            t.fontStyle = FontStyle.Normal;
            UIKit.Anchor(t.gameObject, Vector2.zero, Vector2.one, new Vector2(8, 8), new Vector2(-8, -2));
            UIKit.Stroke(t, new Color(0, 0, 0, 0.6f), 1.5f);
            return t;
        }

        /// <summary>Big coloured hero card for a featured real-money offer.</summary>
        void OfferCard(Transform content, OfferDef o)
        {
            var p = Catalog.Product(o.ProductId);
            string hero = o.Badge == "LEGENDARY" ? "hero_purple" : o.Schedule == OfferSchedule.Starter ? "hero_red" : "hero_blue";
            var card = UIKit.KitImage(content, hero, "Offer", 300);
            UIKit.Size(card.gameObject, -1, 300);
            var h = UIKit.HStack(card.gameObject, 20, 22, TextAnchor.MiddleLeft);
            h.padding = new RectOffset(26, 26, 22, 22);
            HudShadow(card.gameObject);

            if (p.Grant.LocomotiveId != null) LocoSwatch(card.transform, GameDatabase.Locomotive(p.Grant.LocomotiveId), 170);
            else { var n = UIKit.Node("Gems", card.transform); UIKit.Size(n, 240, 200); UIKit.HStack(n, 0, 0, TextAnchor.MiddleCenter); UIKit.NuggetIcon(n.transform, 190); }

            var col = Col(card.transform);
            var title = UIKit.Label(col.transform, o.Title.ToUpperInvariant(), 40, Color.white, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            title.font = UIKit.Display;
            title.fontStyle = FontStyle.Normal;
            UIKit.OneLine(title, 24);
            UIKit.Stroke(title, new Color(0, 0, 0, 0.75f), 2.5f);
            var sub = UIKit.Label(col.transform, o.Subtitle, 22, new Color(1, 0.96f, 0.86f), TextAnchor.MiddleLeft);
            UIKit.Size(sub.gameObject, -1, 30);
            sub.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.6f);
            GrantTiles(col.transform, p.Grant, 92);

            var buyCol = UIKit.Node("Buy", card.transform);
            UIKit.VStack(buyCol, 10, 0, TextAnchor.MiddleCenter);
            UIKit.Size(buyCol, 250, -1);
            if (o.Schedule == OfferSchedule.Weekly)
            {
                var tl = Tag(buyCol.transform, "", UIKit.Red, 230);
                Action live = () => tl.text = "ENDS IN " + Fmt.Time(S.WeekEndsIn).ToUpperInvariant();
                live(); Live(live);
            }
            else if (o.Schedule == OfferSchedule.Starter) Tag(buyCol.transform, "ONE TIME ONLY", UIKit.Green, 230);
            UIKit.Btn(buyCol.transform, Store.PriceOf(p.Id), UIKit.Green, () => BuyProduct(p.Id), 40, 250, 100);
            if (!string.IsNullOrEmpty(o.Badge)) ValueSeal(card.transform, o.Badge);
        }

        void ProductCard(Transform content, ProductDef p, string desc)
        {
            var card = UIKit.KitImage(content, "hero_green", "Product", 200);
            UIKit.Size(card.gameObject, -1, 200);
            var h = UIKit.HStack(card.gameObject, 20, 20, TextAnchor.MiddleLeft);
            h.padding = new RectOffset(26, 26, 18, 18);
            HudShadow(card.gameObject);
            var art = UIKit.Node("Art", card.transform); UIKit.Size(art, 150, 150); UIKit.HStack(art, 0, 0, TextAnchor.MiddleCenter);
            FuelIcon(art.transform, 130);
            var col = Col(card.transform);
            var title = UIKit.Label(col.transform, p.Title.ToUpperInvariant(), 36, Color.white, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            title.font = UIKit.Display;
            title.fontStyle = FontStyle.Normal;
            UIKit.OneLine(title, 22);
            UIKit.Stroke(title, new Color(0, 0, 0, 0.75f), 2.5f);
            var sub = UIKit.Label(col.transform, desc, 22, new Color(1, 0.96f, 0.86f), TextAnchor.MiddleLeft);
            UIKit.Size(sub.gameObject, -1, 56);
            sub.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.6f);
            UIKit.Btn(card.transform, Store.PriceOf(p.Id), UIKit.Gold, () => BuyProduct(p.Id), 36, 230, 92);
        }

        void NuggetCard(Transform grid, ProductDef p, int index)
        {
            bool premium = index >= 3;
            var card = UIKit.KitImage(grid, premium ? "hero_purple" : "card", "Nuggets");
            var v = UIKit.VStack(card.gameObject, 4, 14, TextAnchor.MiddleCenter);
            v.padding = new RectOffset(16, 16, 26, 16);
            HudShadow(card.gameObject);
            if (index == 2 || index == 4)
            {
                var rib = UIKit.KitImage(card.transform, index == 4 ? "ribbon" : "ribbon_green", "Tag");
                rib.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                UIKit.Place(rib.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -4), new Vector2(300, 74));
                rib.pixelsPerUnitMultiplier = 1.7f;
                var rt = UIKit.Label(rib.transform, index == 4 ? "BEST VALUE" : "MOST POPULAR", 24, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
                rt.font = UIKit.Display; rt.fontStyle = FontStyle.Normal;
                UIKit.Anchor(rt.gameObject, Vector2.zero, Vector2.one, new Vector2(60, 18), new Vector2(-60, -2));
                UIKit.Stroke(rt, new Color(0, 0, 0, 0.6f), 1.5f);
            }
            var icons = UIKit.Node("Icons", card.transform);
            UIKit.Size(icons, -1, 130);
            UIKit.HStack(icons, -58, 0, TextAnchor.MiddleCenter);
            for (int k = 0; k <= Mathf.Min(index, 3); k++) UIKit.NuggetIcon(icons.transform, Mathf.Min(110, 92 + index * 5));
            var amount = UIKit.Label(card.transform, p.Grant.Nuggets.ToString("#,0"), 44, UIKit.Gold, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            amount.font = UIKit.Display;
            amount.fontStyle = FontStyle.Normal;
            UIKit.Size(amount.gameObject, -1, 50);
            UIKit.Stroke(amount, new Color(0.25f, 0.12f, 0.02f, 0.95f), 2.5f);
            var title = UIKit.Label(card.transform, p.Title, 18, premium ? new Color(1, 0.94f, 1f) : UIKit.InkSoft, TextAnchor.MiddleCenter);
            UIKit.Size(title.gameObject, -1, 24);
            UIKit.Btn(card.transform, Store.PriceOf(p.Id), UIKit.Green, () => BuyProduct(p.Id), 30, -1, 70);
        }

        void BuyProduct(string productId)
        {
            Store.Buy(productId, r =>
            {
                Result(r, Sfx.LevelUp);
                if (r.Ok) Fx.Burst(CameraRig.Instance.Focus + Vector3.up * 10, 80);
            });
        }

        void WatchAdCard(Transform content, AdReward reward, string title, string desc, int trainUid = 0)
        {
            var c = Card(content, 96, Palette.Hex(0xDCE8C0));
            var col = Col(c.transform);
            Line(col.transform, title, 24, UIKit.Ink, true);
            Line(col.transform, desc + " · " + S.AdsLeft(reward) + " left today", 20, UIKit.InkSoft);
            UIKit.Btn(c.transform, "Watch", UIKit.Green, () => Ads.Watch(reward, trainUid, r => Result(r, Sfx.Coin)), 24, 170, 62, S.AdsLeft(reward) > 0 && Ads.Current.Available);
        }

        // ================================================================ Featured offer popup

        void MaybeShowFeaturedOffer()
        {
            if (WindowOpen) return;
            var o = S.OfferToShow();
            if (o != null) ShowOfferPopup(o);
        }

        public void ShowOfferPopup(OfferDef o)
        {
            ShowWindow("Special Offer", content =>
            {
                // Revalidate on every rebuild: once bought (or expired) the offer must not stay purchasable.
                if (!S.ActiveOffers().Any(x => x.Id == o.Id)) { CloseWindow(); return; }
                OfferCard(content, o);
            }, 1300, 460);
        }

        // ================================================================ Fuel

        public void OpenFuel()
        {
            ShowWindow("Coal & Water", content =>
            {
                var top = Card(content, 130);
                FuelIcon(top.transform, 96);
                var col = Col(top.transform);
                var amount = Line(col.transform, "", 30, UIKit.Ink, true);
                var bar = UIKit.Bar(col.transform, FuelOrange, 24);
                var timer = Line(col.transform, "", 20, UIKit.InkSoft);
                Action live = () =>
                {
                    amount.text = S.Fuel + " / " + S.MaxFuel + " fuel";
                    UIKit.SetBar(bar, S.Fuel / (float)S.MaxFuel);
                    timer.text = S.Fuel >= S.MaxFuel ? "Tender is full" : "+1 every " + (int)Balance.FuelRegenSeconds + "s · full in " + Fmt.Time(S.FullFuelIn);
                };
                live(); Live(live);
                Line(content, "Every dispatch burns coal and water. Heavier trains and farther outposts burn more. Leveling up refills the tender.", 20, UIKit.InkSoft);

                var refill = Card(content, 100);
                var rc = Col(refill.transform);
                Line(rc.transform, "Top up the tender", 24, UIKit.Ink, true);
                Line(rc.transform, "Fill to " + S.MaxFuel + " right now", 20, UIKit.InkSoft);
                if (S.RefillCost > 0)
                {
                    var b = UIKit.Btn(refill.transform, "", UIKit.Green, () => Result(S.RefillFuelWithNuggets()), 24, 200, 64, S.Nuggets >= S.RefillCost);
                    PriceLabelWithNugget(b, S.RefillCost);
                }
                else Line(refill.transform, "Full!", 24, UIKit.Green, true).GetComponent<LayoutElement>().preferredWidth = 120;

                WatchAdCard(content, AdReward.Fuel, "Free coal", "+" + Balance.AdFuel + " fuel for watching a short video");
                var supply = Catalog.Offer("n_coal");
                var sc = Card(content, 100);
                var scc = Col(sc.transform);
                Line(scc.transform, supply.Title, 24, UIKit.Ink, true);
                Line(scc.transform, supply.Subtitle + " Can overfill your tender.", 20, UIKit.InkSoft);
                var sb = UIKit.Btn(sc.transform, "", UIKit.Green, () => Result(S.BuyNuggetOffer(supply.Id)), 24, 200, 64, S.Nuggets >= supply.NuggetPrice);
                PriceLabelWithNugget(sb, supply.NuggetPrice);
                if (!S.HasFuelTank) ProductCard(content, Catalog.Product(Catalog.FuelTank), "+" + Balance.FuelTankBonus + " max fuel forever, plus 200 fuel now.");
            }, 1000, 860);
        }

        // ================================================================ Rail Season Pass

        public void OpenPass()
        {
            ShowWindow("Rail Season Pass", content =>
            {
                var head = Card(content, 150, Palette.Hex(0xD9C6E8));
                var hc = Col(head.transform);
                Line(hc.transform, "Season " + (S.CurrentSeason + 1) + " · Tier " + S.PassTier + "/" + Balance.PassTiers, 30, UIKit.Ink, true);
                var bar = UIKit.Bar(hc.transform, Purple, 22);
                UIKit.SetBar(bar, S.PassTier >= Balance.PassTiers ? 1 : S.PassTierProgress);
                var ends = Line(hc.transform, "", 20, UIKit.InkSoft);
                Action live = () => ends.text = (S.State.passXp % Balance.PassXpPerTier) + "/" + Balance.PassXpPerTier + " season XP to next tier · season ends in " + Fmt.Time(S.SeasonEndsIn);
                live(); Live(live);
                if (!S.State.passPremium)
                    UIKit.Btn(head.transform, "Unlock Premium " + Store.PriceOf(Catalog.SeasonPass), Purple, () => BuyProduct(Catalog.SeasonPass), 24, 300, 80);
                else Line(head.transform, "PREMIUM ACTIVE", 24, Purple, true).GetComponent<LayoutElement>().preferredWidth = 240;

                Line(content, "Earn season XP by dispatching trains (+" + Balance.PassXpDispatch + "), finishing contracts (+" + Balance.PassXpContract + "), producing goods and building. Premium reward at tier 30: the Sundown Express!", 20, UIKit.InkSoft);

                var hdr = UIKit.Row(content, 36, 14);
                UIKit.Size(UIKit.Label(hdr.transform, "TIER", 20, UIKit.InkSoft, TextAnchor.MiddleCenter, true).gameObject, 90, 30);
                UIKit.Size(UIKit.Label(hdr.transform, "FREE", 20, UIKit.InkSoft, TextAnchor.MiddleCenter, true).gameObject, -1, 30, 1);
                UIKit.Size(UIKit.Label(hdr.transform, "PREMIUM", 20, Purple, TextAnchor.MiddleCenter, true).gameObject, -1, 30, 1);

                foreach (var t in Catalog.PassTiers)
                {
                    bool reached = t.Tier <= S.PassTier;
                    var row = Card(content, 96, reached ? Palette.Hex(0xEADFB8) : (Color?)null);
                    var tb = UIKit.Panel(row.transform, reached ? UIKit.Brass : UIKit.Disabled, "Tier", UIKit.Circle);
                    UIKit.Size(tb.gameObject, 70, 70);
                    var tl = UIKit.Label(tb.transform, t.Tier.ToString(), 28, UIKit.Wood, TextAnchor.MiddleCenter, true, FontStyle.Bold);
                    UIKit.Stretch(tl.gameObject);
                    PassCell(row.transform, t, false);
                    PassCell(row.transform, t, true);
                }
            }, 1300, 900);
        }

        void PassCell(Transform row, PassTier t, bool premium)
        {
            var g = premium ? t.Premium : t.Free;
            bool claimed = (premium ? S.State.passClaimedPremium : S.State.passClaimedFree).Contains(t.Tier);
            var cell = UIKit.Panel(row, premium ? new Color(0.62f, 0.45f, 0.78f, 0.35f) : new Color(1, 1, 1, 0.35f), "Cell", UIKit.Rounded);
            UIKit.Size(cell.gameObject, -1, 76, 1);
            UIKit.HStack(cell.gameObject, 10, 8, TextAnchor.MiddleLeft);
            var label = UIKit.Label(cell.transform, g.ToString(), 19, UIKit.Ink, TextAnchor.MiddleLeft, false, g.LocomotiveId != null ? FontStyle.Bold : FontStyle.Normal);
            UIKit.Size(label.gameObject, -1, 60, 1);
            if (claimed) { var c = UIKit.Label(cell.transform, "Claimed", 20, UIKit.Green, TextAnchor.MiddleCenter, true, FontStyle.Bold); UIKit.Size(c.gameObject, 110, 60); }
            else if (S.CanClaimPass(t.Tier, premium))
                UIKit.Btn(cell.transform, "Claim", premium ? Purple : UIKit.Green, () => Result(S.ClaimPass(t.Tier, premium), Sfx.Coin), 20, 120, 54);
            else if (premium && !S.State.passPremium)
            {
                var lockIcon = UIKit.Panel(cell.transform, UIKit.Disabled, "Lock", UIKit.RoundedSmall);
                UIKit.Size(lockIcon.gameObject, 34, 30);
            }
        }

        // ================================================================ Fake ad (editor / dev builds)

        public void ShowFakeAd(Action done)
        {
            var dim = UIKit.Panel(_popupLayer, new Color(0, 0, 0, 0.92f), "TestAd");
            dim.sprite = null;
            UIKit.Stretch(dim.gameObject);
            var t = UIKit.Label(dim.transform, "", 40, UIKit.Cream, TextAnchor.MiddleCenter, true);
            UIKit.Stretch(t.gameObject);
            StartCoroutine(FakeAd(dim.gameObject, t, done));
        }

        System.Collections.IEnumerator FakeAd(GameObject go, Text t, Action done)
        {
            for (int i = 3; i > 0; i--)
            {
                t.text = "TEST AD\n(a real rewarded video plays here)\n\n" + i;
                yield return new WaitForSecondsRealtime(1f);
            }
            Destroy(go);
            done();
        }
    }
}
