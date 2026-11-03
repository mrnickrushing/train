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

        /// <summary>Premium (season pass, nugget spends): brass gold, which reads as "premium" and fits the frontier palette.</summary>
        static readonly Color Premium = Palette.Hex(0xD49A22);
        static readonly Color Legendary = Palette.Hex(0xB8402C);
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
            UIKit.Place(side, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-24, 200), new Vector2(170, 380));
            UIKit.VStack(side, 14, 0, TextAnchor.LowerCenter);
            _passBadge = SideButton(side.transform, "Season", Premium, () => OpenPass(), PassGlyph, out _);
            _shopBadge = SideButton(side.transform, "Shop", UIKit.Red, () => OpenShop(), ShopGlyph, out _shopTimer);
        }

        Text SideButton(Transform parent, string label, Color c, Action onClick, Action<Transform> glyph, out Text timer)
        {
            var holder = UIKit.Node(label, parent);
            UIKit.Size(holder, 150, 172);
            var circle = HudTile(holder.transform, label, glyph, 140);
            timer = circle.GetComponentInChildren<Text>();
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
            if (UIKit.Icon(t, "shop_nuggets_4", 100) != null) return;
            var h = UIKit.Node("Shop", t); UIKit.Size(h, 58, 56);
            Shape(h, UIKit.Cream, new Vector2(0.5f, 0.38f), new Vector2(46, 34));
            Shape(h, UIKit.Gold, new Vector2(0.5f, 0.78f), new Vector2(54, 16));
            Shape(h, UIKit.Red, new Vector2(0.5f, 0.3f), new Vector2(14, 20));
        }

        static void PassGlyph(Transform t)
        {
            if (UIKit.Icon(t, "xp", 92) != null) return;
            var h = UIKit.Node("Pass", t); UIKit.Size(h, 56, 56);
            Shape(h, UIKit.Cream, new Vector2(0.5f, 0.5f), new Vector2(50, 34));
            Shape(h, UIKit.Gold, new Vector2(0.5f, 0.5f), new Vector2(20, 20), UIKit.Circle);
            Shape(h, Premium, new Vector2(0.18f, 0.5f), new Vector2(8, 34));
            Shape(h, Premium, new Vector2(0.82f, 0.5f), new Vector2(8, 34));
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
            ShowBoard("Trading Post", ShopBoard);
        }

        void ShopBoard(Transform body, Transform right)
        {
            string[] names = { "Offers", "Nuggets", "Supplies", "Season" };
            string[] ids = { "offers", "nuggets", "supplies", "season" };
            _boardSub = names[Mathf.Max(0, Array.IndexOf(ids, _shopTab))];
            if (_shopTab == "offers")
            {
                var timer = Pill(right, tr => ClockIcon(tr, 40), "", 30);
                var tt = timer.GetComponentInChildren<Text>();
                Action live = () => tt.text = "New deals in " + Fmt.Time(S.WeekEndsIn);
                live(); Live(live);
            }
            else if (_shopTab == "nuggets")
                Big(right, "Restore purchases", UIKit.WoodLight, () => Store.Current.Restore(n => Toast(n > 0 ? "Restored " + n + " purchase(s)" : "Nothing to restore", true)), 380, 80, true, 32);

            var row = UIKit.Node("Row", body);
            UIKit.Stretch(row);
            Row(row, 12, new RectOffset(0, 0, 0, 0)).childForceExpandHeight = true;
            SideTabs(row.transform, 220, 158, new (string, Action<Transform>, bool, Action)[]
            {
                ("Offers", Ico("shop_nuggets_4", 96), _shopTab == "offers", () => _shopTab = "offers"),
                ("Nuggets", Ico("shop_nuggets_3", 96), _shopTab == "nuggets", () => _shopTab = "nuggets"),
                ("Supplies", Ico("shop_supplies", 96), _shopTab == "supplies", () => _shopTab = "supplies"),
                ("Season", Ico("xp", 96), _shopTab == "season", () => _shopTab = "season"),
            });
            var panel = Box(row.transform, BTile, -1, -1, "Panel");
            UIKit.Size(panel.gameObject, -1, -1, 1);
            Stack(panel.gameObject, 0, new RectOffset(14, 14, 14, 14));
            var ct = Scroller(panel.transform, "shop " + _shopTab, true, 16);

            if (_shopTab == "offers")
            {
                int n = 0;
                foreach (var o in S.ActiveOffers().Where(o => o.ProductId != null)) { var oo = o; InCell(ct, t => OfferCard(t, oo)); n++; }
                if (!S.HasFuelTank)
                {
                    var fp = Catalog.Product(Catalog.FuelTank);
                    InCell(ct, t => ShopCard(t, fp.Title, "FOREVER", UIKit.Green, null, "blackhollow", x => Art(x, "shop_fuel", 230), null, fp.Grant, "Fuel limit +" + Balance.FuelTankBonus,
                        Store.PriceOf(fp.Id), UIKit.Green, () => BuyProduct(fp.Id)));
                    n++;
                }
                if (n == 0) NoDeals(ct);
            }
            else if (_shopTab == "nuggets")
            {
                int i = 0;
                foreach (var p in Catalog.Products.Where(p => p.Id.StartsWith("ironhorse.nuggets_"))) { var pp = p; int k = i++; InCell(ct, t => NuggetCard(t, pp, k)); }
                bool ad = S.AdsLeft(AdReward.Nuggets) > 0 && Ads.Current.Available;
                InCell(ct, t => ShopCard(t, "Free Nuggets", "FREE", UIKit.Blue, null, null, x => Art(x, "shop_nuggets_1", 190), null, new Grant { Nuggets = Balance.AdNuggets },
                    S.AdsLeft(AdReward.Nuggets) + " left today", "Watch", UIKit.Green, ad ? (Action)(() => Ads.Watch(AdReward.Nuggets, 0, r => { Result(r, Sfx.Coin); RebuildWindow(); })) : null));
            }
            else if (_shopTab == "supplies")
            {
                foreach (var o in S.ActiveOffers().Where(o => o.ProductId == null))
                {
                    var oo = o;
                    InCell(ct, t =>
                    {
                        var b = ShopCard(t, oo.Title, null, UIKit.Blue, null, null, x => Art(x, SupplyArt(oo.Grant), 200), null, oo.Grant, oo.Subtitle,
                            oo.NuggetPrice.ToString(), UIKit.Green, S.Nuggets >= oo.NuggetPrice ? (Action)(() => { Result(S.BuyNuggetOffer(oo.Id)); RebuildWindow(); }) : null);
                        PriceLabelWithNugget(b, oo.NuggetPrice);
                    });
                }
            }
            else
            {
                var sp = Catalog.Product(Catalog.SeasonPass);
                var final = GameDatabase.Locomotive("sundown");
                InCell(ct, t => ShopCard(t, "Rail Season Pass", S.State.passPremium ? "ACTIVE" : "PREMIUM", Premium, null, "silversprings",
                    x => { if (final != null) LocoArt(x, final); }, null, new Grant(), "30 tiers of premium rewards and the Sundown Express",
                    S.State.passPremium ? "View pass" : Store.PriceOf(sp.Id), S.State.passPremium ? UIKit.Blue : UIKit.Green,
                    () => { if (S.State.passPremium) OpenPass(); else BuyProduct(sp.Id); }));
                InCell(ct, t => ShopCard(t, "Season Rewards", null, UIKit.Blue, null, null, x => Art(x, "xp", 200), null, new Grant(), "Tier " + S.PassTier + "/" + Balance.PassTiers + " · see every reward",
                    "Open", UIKit.Blue, () => OpenPass()));
            }
        }

        /// <summary>A store card in a fixed-width cell of the board's card row.</summary>
        static void InCell(Transform p, Action<Transform> make, float w = 340)
        {
            var cell = UIKit.Node("Cell", p);
            UIKit.Size(cell, w, -1);
            make(cell.transform);
            foreach (Transform c in cell.transform) UIKit.Stretch(c.gameObject);
        }

        /// <summary>Offers tab once every deal is bought: when the next arrive, and where to look meanwhile.</summary>
        void NoDeals(Transform p)
        {
            var card = Box(p, BCardDeep, 900, -1, "NoDeals");
            Stack(card.gameObject, 16, new RectOffset(30, 30, 30, 30), TextAnchor.MiddleCenter);
            var ic = HBox(card.transform, 0, 0, TextAnchor.MiddleCenter, -1, 230);
            Art(ic.transform, "shop_nuggets_4", 220);
            T(card.transform, "You've snapped up every deal!", 52, BGold, TextAnchor.MiddleCenter, true);
            var tl = T(card.transform, "", 36, Color.white, TextAnchor.MiddleCenter, false, UIKit.Heading);
            Action live = () => tl.text = "A fresh bundle arrives in " + Fmt.Time(S.WeekEndsIn) + ".";
            live(); Live(live);
            Flex(card.transform);
            var btns = HBox(card.transform, 16, 0, TextAnchor.MiddleCenter, -1, 96);
            Big(btns.transform, "Nuggets", UIKit.Green, () => { _shopTab = "nuggets"; RebuildWindow(); }, 300, 92, true, 40);
            Big(btns.transform, "Supplies", BBlue, () => { _shopTab = "supplies"; RebuildWindow(); }, 300, 92, true, 40);
        }

        /// <summary>Left-hand category tab: painted icon and caption on a dark tile (light when selected).</summary>
        void ShopTab(Transform parent, string id, string label, string icon, bool alert)
        {
            bool on = _shopTab == id;
            var tile = UIKit.KitImage(parent, on ? "tab_on" : "tile_dark", "Tab " + id, 150);
            UIKit.Size(tile.gameObject, 196, 150);
            var ic = UIKit.Icon(tile.transform, icon, 92);
            if (ic != null) UIKit.Place(ic, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(92, 92));
            var t = UIKit.Label(tile.transform, label.ToUpperInvariant(), 26, on ? UIKit.Ink : Color.white, TextAnchor.LowerCenter, false, FontStyle.Bold);
            t.font = UIKit.Display; t.fontStyle = FontStyle.Normal;
            UIKit.Anchor(t.gameObject, Vector2.zero, new Vector2(1, 0), new Vector2(4, 8), new Vector2(-4, 44));
            if (!on) UIKit.Stroke(t, new Color(0, 0, 0, 0.7f), 1.5f);
            if (alert)
            {
                var bang = UIKit.Badge(tile.transform, "!", UIKit.Red);
                UIKit.Place(bang.transform.parent.gameObject, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(14, -10), new Vector2(40, 40));
            }
            Clickable(tile.gameObject, () => { _shopTab = id; RebuildWindow(); });
        }

        static string SupplyArt(Grant g)
        {
            if (g.Fuel > 0) return "shop_fuel";
            if (g.Coins > 0) return "shop_coins";
            if (g.Resources != null && g.Resources.Length >= 1 && g.Resources.Length <= 2) return g.Resources[0].id;
            return "shop_supplies";
        }

        static void Art(Transform t, string icon, float size)
        {
            var go = UIKit.Icon(t, icon, size);
            if (go != null) go.GetComponent<LayoutElement>().ignoreLayout = false;
        }

        static void LocoArt(Transform t, LocomotiveDef d)
        {
            var img = ModelIcons.Image(t, ModelIcons.Locomotive(d), 300, 200);
            UIKit.Size(img.gameObject, 300, 200);
        }

        /// <summary>
        /// Tall store card (reference: big painted art over a scene): title, ribbon tag, art with value seal,
        /// contents as item tiles, a note line and the price button. Returns the price button.
        /// </summary>
        Button ShopCard(Transform parent, string title, string tag, Color tagColor, string seal, string scene, Action<Transform> art,
            string unused, Grant grant, string note, string price, Color priceColor, Action onBuy)
        {
            var card = UIKit.KitImage(parent, "card_slate", "ShopCard");
            HudShadow(card.gameObject);

            var tl = UIKit.Label(card.transform, title, 28, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            tl.font = UIKit.Display; tl.fontStyle = FontStyle.Normal;
            UIKit.Anchor(tl.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(12, -58), new Vector2(-12, -10));
            UIKit.OneLine(tl, 18).GetComponent<LayoutElement>().ignoreLayout = true;
            UIKit.Stroke(tl, new Color(0, 0, 0, 0.75f), 2f);
            var rule = UIKit.Panel(card.transform, new Color(1, 1, 1, 0.14f), "Rule", UIKit.RoundedSmall);
            UIKit.Anchor(rule.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(18, -62), new Vector2(-18, -60));

            // Art window
            var win = UIKit.Panel(card.transform, new Color(0.12f, 0.15f, 0.22f), "ArtWindow", UIKit.Rounded);
            UIKit.Anchor(win.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -330), new Vector2(-10, -70));
            win.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var tex = scene != null ? Resources.Load<Texture2D>("UI/Destinations/" + scene) : null;
            if (tex != null)
            {
                var raw = UIKit.Node("Scene", win.transform).AddComponent<RawImage>();
                raw.texture = tex;
                raw.color = new Color(0.62f, 0.62f, 0.66f);
                raw.raycastTarget = false;
                UIKit.Stretch(raw.gameObject);
                var fit = raw.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = tex.width / (float)tex.height;
            }
            else
            {
                var glow = UIKit.Node("Glow", win.transform).AddComponent<RawImage>();
                glow.texture = Materials.SoftCircle;
                glow.color = new Color(1f, 0.82f, 0.4f, 0.5f);
                glow.raycastTarget = false;
                UIKit.Place(glow.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 300));
            }
            var holder = UIKit.Node("Art", win.transform);
            UIKit.Stretch(holder, 6);
            UIKit.HStack(holder, 0, 0, TextAnchor.MiddleCenter);
            art(holder.transform);
            if (!string.IsNullOrEmpty(tag))
            {
                var tg = Tag(win.transform, tag, tagColor, 200);
                UIKit.Place(tg.transform.parent.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(-8, -10), new Vector2(200, 44));
            }
            if (!string.IsNullOrEmpty(seal))
            {
                ValueSeal(win.transform, seal);
                var sl = win.transform.Find("Seal");
                UIKit.Place(sl.gameObject, new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-50, -56), new Vector2(124, 124));
            }

            // Contents
            var tiles = UIKit.Node("Tiles", card.transform);
            UIKit.Anchor(tiles, new Vector2(0, 0), new Vector2(1, 0), new Vector2(12, 170), new Vector2(-12, 262));
            var tg2 = tiles.AddComponent<GridLayoutGroup>();
            tg2.cellSize = new Vector2(68, 68);
            tg2.spacing = new Vector2(6, 6);
            tg2.childAlignment = TextAnchor.MiddleCenter;
            int kinds = grant == null ? 0 : (grant.Nuggets > 0 ? 1 : 0) + (grant.Coins > 0 ? 1 : 0) + (grant.Fuel > 0 ? 1 : 0) + (grant.Resources != null ? grant.Resources.Length : 0);
            if (kinds == 1 && grant.Resources.Length == 0)
            {
                // One currency: a big amount pill (icon + number) like the reference stores.
                UnityEngine.Object.Destroy(tg2);
                var pill = UIKit.KitImage(tiles.transform, "pill_slate", "Amount", 70);
                UIKit.Place(pill.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240, 70));
                var ph = UIKit.HStack(pill.gameObject, 8, 0, TextAnchor.MiddleCenter);
                ph.padding = new RectOffset(8, 16, 2, 2);
                if (grant.Nuggets > 0) UIKit.NuggetIcon(pill.transform, 52); else if (grant.Coins > 0) UIKit.CoinIcon(pill.transform, 52); else FuelIcon(pill.transform, 48);
                var at = UIKit.Label(pill.transform, Fmt.Num(grant.Nuggets + grant.Coins + grant.Fuel), 40, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
                at.font = UIKit.Display; at.fontStyle = FontStyle.Normal;
                at.horizontalOverflow = HorizontalWrapMode.Overflow;
                UIKit.Size(at.gameObject, -1, 60);
                UIKit.Stroke(at, new Color(0, 0, 0, 0.8f), 2f);
            }
            else if (grant != null)
            {
                float ic = 50;
                if (grant.Nuggets > 0) GrantTile(tiles.transform, t => UIKit.NuggetIcon(t, ic), Fmt.Num(grant.Nuggets), 68);
                if (grant.Coins > 0) GrantTile(tiles.transform, t => UIKit.CoinIcon(t, ic), Fmt.Num(grant.Coins), 68);
                if (grant.Fuel > 0) GrantTile(tiles.transform, t => FuelIcon(t, ic * 0.92f), grant.Fuel.ToString(), 68);
                if (grant.Resources != null) foreach (var r in grant.Resources) { var id = r.id; GrantTile(tiles.transform, t => UIKit.ResIcon(t, id, ic), r.amount.ToString(), 68); }
            }

            var nl = UIKit.Label(card.transform, note ?? "", 19, new Color(0.78f, 0.84f, 0.92f), TextAnchor.MiddleCenter);
            nl.resizeTextForBestFit = true; nl.resizeTextMinSize = 13; nl.resizeTextMaxSize = nl.fontSize;
            nl.verticalOverflow = VerticalWrapMode.Truncate;
            UIKit.Anchor(nl.gameObject, new Vector2(0, 0), new Vector2(1, 0), new Vector2(14, 98), new Vector2(-14, 166));
            var pb = UIKit.Btn(card.transform, price, onBuy != null ? priceColor : UIKit.Disabled, onBuy, 36, -1, 84, onBuy != null);
            UIKit.Anchor(pb.gameObject, new Vector2(0, 0), new Vector2(1, 0), new Vector2(18, 14), new Vector2(-18, 96));
            return pb;
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
            bl.resizeTextForBestFit = true; bl.resizeTextMinSize = 12; bl.resizeTextMaxSize = 28;
            bl.verticalOverflow = VerticalWrapMode.Truncate;
            bl.horizontalOverflow = HorizontalWrapMode.Overflow;
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

        static readonly System.Collections.Generic.Dictionary<string, string> OfferScenes = new System.Collections.Generic.Dictionary<string, string>
        {
            { "starter", "willowflats" }, { "w_ironbison", "ironback" }, { "w_prospector", "eldorado" },
            { "w_silverarrow", "prosperity" }, { "w_thunderhead", "capitaljct" },
        };

        /// <summary>Featured real-money offer as a tall store card.</summary>
        void OfferCard(Transform content, OfferDef o)
        {
            var p = Catalog.Product(o.ProductId);
            string scene;
            OfferScenes.TryGetValue(o.Id, out scene);
            string tag = o.Schedule == OfferSchedule.Starter ? "ONE TIME ONLY" : o.Badge == "LEGENDARY" ? "LEGENDARY" : "LIMITED OFFER";
            Color tagColor = o.Schedule == OfferSchedule.Starter ? UIKit.Blue : o.Badge == "LEGENDARY" ? Legendary : UIKit.Green;
            var loco = p.Grant.LocomotiveId != null ? GameDatabase.Locomotive(p.Grant.LocomotiveId) : null;
            string note = o.Schedule == OfferSchedule.Weekly ? "Ends in " + Fmt.Time(S.WeekEndsIn) + " · 1 available" : "1 available";
            ShopCard(content, o.Title, tag, tagColor, o.Badge == "LEGENDARY" ? null : o.Badge, scene,
                t => { if (loco != null) LocoArt(t, loco); else Art(t, "shop_nuggets_4", 230); }, null, p.Grant, note,
                Store.PriceOf(p.Id), UIKit.Green, () => BuyProduct(p.Id));
        }

        void NuggetCard(Transform grid, ProductDef p, int index)
        {
            string tag = index == 2 ? "MOST POPULAR" : index == 4 ? "BEST VALUE" : null;
            float size = 150 + index * 18;
            ShopCard(grid, p.Title, tag, index == 4 ? UIKit.Red : UIKit.Green, null, null, t => Art(t, "shop_nuggets_" + (index + 1), size), null,
                new Grant { Nuggets = p.Grant.Nuggets }, index == 0 ? "A handful to get you started" : "Rush timers and buy supplies",
                Store.PriceOf(p.Id), UIKit.Green, () => BuyProduct(p.Id));
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

        /// <summary>After the welcome cards when they're showing; otherwise only if nothing else is open.</summary>
        void QueueOffer()
        {
            if (_welcome.Count > 0 || (WindowOpen && (_windowTitle == "While You Were Away" || _windowTitle == "Payday"))) _welcome.Enqueue(MaybeShowFeaturedOffer);
            else MaybeShowFeaturedOffer();
        }

        void MaybeShowFeaturedOffer()
        {
            if (WindowOpen) return;
            var o = S.OfferToShow();
            if (o != null) ShowOfferPopup(o);
        }

        public void ShowOfferPopup(OfferDef o)
        {
            ShowBoard("Special Offer", (body, right) => OfferBoard(body, right, o), 1800, 820);
        }

        void OfferBoard(Transform body, Transform right, OfferDef o)
        {
            // Revalidate on every rebuild: once bought (or expired) the offer must not stay purchasable.
            if (!S.ActiveOffers().Any(x => x.Id == o.Id)) { CloseWindow(); return; }
            var p = Catalog.Product(o.ProductId);
            var loco = p.Grant.LocomotiveId != null ? GameDatabase.Locomotive(p.Grant.LocomotiveId) : null;
            _boardSub = o.Schedule == OfferSchedule.Starter ? "One time only" : o.Badge == "LEGENDARY" ? "Legendary" : "This week only";
            if (o.Schedule == OfferSchedule.Weekly)
            {
                var timer = Pill(right, tr => ClockIcon(tr, 40), "", 30);
                var tt = timer.GetComponentInChildren<Text>();
                Action live = () => tt.text = "Ends in " + Fmt.Time(S.WeekEndsIn);
                live(); Live(live);
            }

            var row = UIKit.Node("Row", body);
            UIKit.Stretch(row);
            Row(row, 12, new RectOffset(0, 0, 0, 0)).childForceExpandHeight = true;
            string scene;
            OfferScenes.TryGetValue(o.Id, out scene);
            var hero = Photo(row.transform, scene != null ? Postcard(scene) : null, -1, -1);
            UIKit.Size(hero.gameObject, -1, -1, 1);
            if (scene == null) hero.color = Palette.Hex(0x2B3549);
            Overlay(hero.transform, Fade(true), new Color(0.05f, 0.07f, 0.11f, 0.75f));
            var art = UIKit.Node("Art", hero.transform);
            UIKit.Anchor(art, new Vector2(0, 0), new Vector2(1, 0.85f), new Vector2(20, 10), new Vector2(-20, 0));
            UIKit.HStack(art, 0, 0, TextAnchor.LowerCenter);
            if (loco != null) LocoArt(art.transform, loco, 820, 400);
            else Art(art.transform, "shop_nuggets_4", 380);
            if (!string.IsNullOrEmpty(o.Badge) && o.Badge != "LEGENDARY")
            {
                ValueSeal(hero.transform, o.Badge);
                var sl = hero.transform.Find("Seal");
                UIKit.Place(sl.gameObject, new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-96, -96), new Vector2(170, 170));
            }

            var col = Box(row.transform, BCardDeep, 640, -1, "Details");
            Stack(col.gameObject, 14, new RectOffset(26, 26, 24, 24), TextAnchor.UpperLeft);
            T(col.transform, o.Title, 52, BGold, TextAnchor.MiddleLeft, true);
            T(col.transform, o.Subtitle, 32, Color.white, TextAnchor.UpperLeft, false, UIKit.Heading);
            T(col.transform, "You get", 32, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);
            if (loco != null)
            {
                var lr = Box(col.transform, BChip, -1, 84, "Loco");
                Row(lr.gameObject, 12, new RectOffset(10, 16, 0, 0));
                LocoArt(lr.transform, loco, 130, 70);
                T1(lr.transform, loco.Name, 36, Color.white, TextAnchor.MiddleLeft, true);
            }
            GrantTiles(col.transform, p.Grant, 104);
            Flex(col.transform);
            Big(col.transform, Store.PriceOf(p.Id), UIKit.Green, () => BuyProduct(p.Id), -1, 112, true, 56);
        }

        // ================================================================ Fuel

        public void OpenFuel() { ShowBoard("Coal & Water", FuelBoard, 1700, 860); }

        void FuelBoard(Transform body, Transform right)
        {
            _boardSub = "The tender";
            var row = UIKit.Node("Row", body);
            UIKit.Stretch(row);
            Row(row, 12, new RectOffset(0, 0, 0, 0)).childForceExpandHeight = true;

            var gauge = Box(row.transform, BCardDeep, 600, -1, "Gauge");
            Stack(gauge.gameObject, 16, new RectOffset(26, 26, 26, 26), TextAnchor.UpperCenter);
            var ic = HBox(gauge.transform, 0, 0, TextAnchor.MiddleCenter, -1, 200);
            FuelIcon(ic.transform, 190);
            var amount = T1(gauge.transform, "", 64, Color.white, TextAnchor.MiddleCenter, true);
            var bar = Track(gauge.transform, 40, FuelOrange);
            var timer = T1(gauge.transform, "", 32, BLabel, TextAnchor.MiddleCenter, false, UIKit.Heading);
            Action live = () =>
            {
                amount.text = Fmt.Num(S.Fuel) + " / " + S.MaxFuel;
                UIKit.SetBar(bar, S.Fuel / (float)S.MaxFuel);
                timer.text = S.Fuel >= S.MaxFuel ? (S.Fuel > S.MaxFuel ? "Overfilled · no refill until it drops" : "The tender is full") : "+1 every " + (int)Balance.FuelRegenSeconds + "s · full in " + Fmt.Time(S.FullFuelIn);
            };
            live(); Live(live);
            Flex(gauge.transform);
            T(gauge.transform, "Every trip burns coal and water. Heavier trains and farther outposts burn more. Levelling up refills the tender.", 30, BLabel, TextAnchor.MiddleCenter, false, UIKit.Heading);

            var panel = Box(row.transform, BTile, -1, -1, "Options");
            UIKit.Size(panel.gameObject, -1, -1, 1);
            Stack(panel.gameObject, 14, new RectOffset(20, 20, 20, 20));
            var refill = FuelOption(panel.transform, t => FuelIcon(t, 80), "Top up the tender", "Fill to " + S.MaxFuel + " right now");
            if (S.RefillCost > 0) PriceBtn(refill, "Fill", UIKit.Green, t => UIKit.NuggetIcon(t, 40), S.RefillCost.ToString(), () => Result(S.RefillFuelWithNuggets()), 280, 84, S.Nuggets >= S.RefillCost, 36);
            else Pill(refill, null, "Full", 34, BGood);

            int ads = S.AdsLeft(AdReward.Fuel);
            var ad = FuelOption(panel.transform, Ico("shop_fuel", 80), "Free coal", "+" + Balance.AdFuel + " fuel for a short video · " + ads + " left today");
            Big(ad, "Watch", UIKit.Green, () => Ads.Watch(AdReward.Fuel, 0, r => Result(r, Sfx.Coin)), 280, 84, ads > 0 && Ads.Current.Available, 36);

            var supply = Catalog.Offer("n_coal");
            var sc = FuelOption(panel.transform, Ico("coal", 80), supply.Title, supply.Subtitle + " Can overfill your tender.");
            PriceBtn(sc, "Buy", UIKit.Green, t => UIKit.NuggetIcon(t, 40), supply.NuggetPrice.ToString(), () => Result(S.BuyNuggetOffer(supply.Id)), 280, 84, S.Nuggets >= supply.NuggetPrice, 36);

            if (!S.HasFuelTank)
            {
                var tank = FuelOption(panel.transform, Ico("shop_fuel", 80), "Big Tender", "+" + Balance.FuelTankBonus + " max fuel forever");
                Big(tank, Store.PriceOf(Catalog.FuelTank), UIKit.Gold, () => OpenShop("offers"), 280, 84, true, 36);
            }
        }

        /// <summary>One fuel option row: icon, title and note; returns the slot for its button.</summary>
        static Transform FuelOption(Transform p, Action<Transform> icon, string title, string note)
        {
            var r = Box(p, BCardDeep, -1, 132, "Option");
            Row(r.gameObject, 18, new RectOffset(18, 18, 0, 0));
            var ic = HBox(r.transform, 0, 0, TextAnchor.MiddleCenter, 96, 96);
            icon(ic.transform);
            var col = VBox(r.transform, 4, 0, TextAnchor.MiddleLeft, -1, -1, 1);
            T1(col.transform, title, 40, Color.white, TextAnchor.MiddleLeft, true);
            T1(col.transform, note, 28, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);
            return r.transform;
        }

        // ================================================================ Rail Season Pass

        public void OpenPass() { ShowBoard("Rail Season Pass", PassBoard); }

        void PassBoard(Transform body, Transform right)
        {
            _boardSub = "Season " + (S.CurrentSeason + 1);
            var panel = Box(body, Palette.Hex(0xE6E0D4), -1, -1, "Panel");
            UIKit.Stretch(panel.gameObject);
            Stack(panel.gameObject, 10, new RectOffset(16, 16, 14, 10));
            var top = panel.transform;
            // Hero header: season, tier, progress and the premium unlock.
            var head = UIKit.KitImage(top, "hero_brass", "Head", 200);
            UIKit.Size(head.gameObject, -1, 200);
            var hh = UIKit.HStack(head.gameObject, 22, 22, TextAnchor.MiddleLeft);
            hh.padding = new RectOffset(24, 26, 20, 20);
            HudShadow(head.gameObject);
            var medal = UIKit.KitImage(head.transform, "round_gold", "Tier");
            UIKit.Size(medal.gameObject, 140, 140);
            var mt = UIKit.Label(medal.transform, S.PassTier.ToString(), 60, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            mt.font = UIKit.Display; mt.fontStyle = FontStyle.Normal;
            UIKit.Stretch(mt.gameObject, 8);
            UIKit.Stroke(mt, new Color(0.36f, 0.2f, 0.02f, 0.95f), 3f);
            var hc = Col(head.transform);
            var title = UIKit.Label(hc.transform, "SEASON " + (S.CurrentSeason + 1) + " · TIER " + S.PassTier + "/" + Balance.PassTiers, 40, Color.white, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            title.font = UIKit.Display; title.fontStyle = FontStyle.Normal;
            UIKit.OneLine(title, 24);
            UIKit.Stroke(title, new Color(0.22f, 0.12f, 0.02f, 0.9f), 2.5f);
            var bar = UIKit.Bar(hc.transform, UIKit.Gold, 28, new Color(0, 0, 0, 0.45f));
            UIKit.SetBar(bar, S.PassTier >= Balance.PassTiers ? 1 : S.PassTierProgress);
            var ends = UIKit.Label(hc.transform, "", 22, new Color(1, 0.96f, 0.86f), TextAnchor.MiddleLeft);
            UIKit.Size(ends.gameObject, -1, 30);
            ends.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.6f);
            Action live = () => ends.text = (S.State.passXp % Balance.PassXpPerTier) + "/" + Balance.PassXpPerTier + " season XP to the next tier · ends in " + Fmt.Time(S.SeasonEndsIn);
            live(); Live(live);
            var buyCol = UIKit.Node("Buy", head.transform);
            UIKit.VStack(buyCol, 8, 0, TextAnchor.MiddleCenter);
            UIKit.Size(buyCol, 280, -1);
            if (!S.State.passPremium)
            {
                Tag(buyCol.transform, "PREMIUM TRACK", UIKit.Gold, 250);
                UIKit.Btn(buyCol.transform, Store.PriceOf(Catalog.SeasonPass), UIKit.Green, () => BuyProduct(Catalog.SeasonPass), 40, 270, 96);
            }
            else Tag(buyCol.transform, "PREMIUM ACTIVE", UIKit.Green, 260);

            Line(top, "Earn season XP by dispatching trains (+" + Balance.PassXpDispatch + "), finishing contracts (+" + Balance.PassXpContract + "), producing goods and building. Premium tier " + Balance.PassTiers + ": the Sundown Express!", 20, UIKit.InkSoft);

            var hdr = UIKit.Row(top, 44, 14);
            UIKit.Size(UIKit.Node("Pad", hdr.transform), 110, 30);
            TrackHeader(hdr.transform, "FREE", UIKit.Green);
            TrackHeader(hdr.transform, "PREMIUM", Premium);

            // Open at the current tier, where the next claims are (1 = top of the list).
            if (!_scrollMemo.ContainsKey("pass")) _scrollMemo["pass"] = new Vector2(0, 1 - Mathf.Clamp01((S.PassTier - 2) / (float)(Balance.PassTiers - 3)));
            var list = Scroller(top, "pass", false, 10);
            foreach (var t in Catalog.PassTiers)
            {
                bool reached = t.Tier <= S.PassTier;
                var row = UIKit.Row(list, 128, 14);
                var tb = UIKit.KitImage(row.transform, reached ? "round_gold" : "round_brown", "Tier");
                UIKit.Size(tb.gameObject, 110, 110);
                var tl = UIKit.Label(tb.transform, t.Tier.ToString(), 44, reached ? Color.white : new Color(0.85f, 0.8f, 0.74f), TextAnchor.MiddleCenter, false, FontStyle.Bold);
                tl.font = UIKit.Display; tl.fontStyle = FontStyle.Normal;
                UIKit.Stretch(tl.gameObject, 6);
                UIKit.Stroke(tl, new Color(0.25f, 0.13f, 0.03f, 0.95f), 2.5f);
                PassCell(row.transform, t, false);
                PassCell(row.transform, t, true);
            }
        }

        static void TrackHeader(Transform parent, string text, Color c)
        {
            var h = UIKit.Node(text, parent);
            UIKit.Size(h, -1, 44, 1);
            UIKit.HStack(h, 0, 0, TextAnchor.MiddleCenter);
            Tag(h.transform, text, c, 220);
        }

        void PassCell(Transform row, PassTier t, bool premium)
        {
            var g = premium ? t.Premium : t.Free;
            bool claimed = (premium ? S.State.passClaimedPremium : S.State.passClaimedFree).Contains(t.Tier);
            bool locked = premium && !S.State.passPremium;
            bool reached = t.Tier <= S.PassTier;
            var cell = UIKit.KitImage(row, premium ? "hero_brass" : "card_light", "Cell", 120);
            if (!reached) cell.color = new Color(0.82f, 0.8f, 0.78f);
            UIKit.Size(cell.gameObject, -1, 120, 1);
            var h = UIKit.HStack(cell.gameObject, 10, 10, TextAnchor.MiddleLeft);
            h.padding = new RectOffset(14, 14, 10, 12);
            var tiles = UIKit.Node("Tiles", cell.transform);
            UIKit.Size(tiles, -1, 92, 1);
            UIKit.HStack(tiles, 8, 0, TextAnchor.MiddleLeft);
            if (g.LocomotiveId != null) LocoSwatch(tiles.transform, GameDatabase.Locomotive(g.LocomotiveId), 62);
            GrantTiles(tiles.transform, g, 84);
            if (g.PassPremium || (g.LocomotiveId == null && g.Nuggets == 0 && g.Coins == 0 && g.Fuel == 0 && (g.Resources == null || g.Resources.Length == 0)))
            {
                var l = UIKit.Label(tiles.transform, g.ToString(), 20, premium ? Color.white : UIKit.Ink, TextAnchor.MiddleLeft);
                UIKit.Size(l.gameObject, -1, 80, 1);
            }
            var act = UIKit.Node("Act", cell.transform);
            UIKit.Size(act, 150, 92);
            UIKit.HStack(act, 0, 0, TextAnchor.MiddleCenter);
            if (claimed)
            {
                var c = UIKit.Label(act.transform, "✓ CLAIMED", 26, UIKit.Green, TextAnchor.MiddleCenter, false, FontStyle.Bold);
                c.font = UIKit.Display; c.fontStyle = FontStyle.Normal;
                UIKit.Size(c.gameObject, 150, 60);
                UIKit.Stroke(c, new Color(1, 1, 1, 0.7f), 1.5f);
            }
            else if (S.CanClaimPass(t.Tier, premium))
                UIKit.Btn(act.transform, "Claim", premium ? UIKit.Gold : UIKit.Green, () => Result(S.ClaimPass(t.Tier, premium), Sfx.Coin), 30, 150, 76);
            else if (locked && UIKit.Icon(act.transform, "lock", 64) != null) { }
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
