using System;
using System.Linq;
using Ironhorse.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ironhorse.Game
{
    /// <summary>
    /// The Motor Garage board: the local delivery board (customers' orders, sent by truck) and the trucks
    /// themselves (status, pay to collect, upgrades, buying more). Opened by tapping the garage or a truck,
    /// or from the next-step button.
    /// </summary>
    public sealed partial class UIRoot
    {
        string _garageTab = "orders";

        public void OpenGarage(string tab = null)
        {
            if (tab != null) _garageTab = tab;
            var g = S.Garage;
            if (g == null) { OpenBuildMenu(); return; }
            if (!S.IsReady(g)) { OpenBuilding(g.uid); return; }
            CameraRig.Instance.FlyTo(WorldLayout.Plots[g.plot], 60);
            ShowBoard("Motor Garage", GarageBoard);
        }

        void GarageBoard(Transform body, Transform right)
        {
            var g = S.Garage;
            if (g == null || !S.IsReady(g)) { CloseWindow(); return; }
            int guid = g.uid;
            Pill(right, tr => UIKit.Icon(tr, "nav_trains", 40), "Bays " + S.State.trucks.Count + " / " + S.GarageBays, 30);
            Big(right, "Garage", UIKit.WoodLight, () => OpenBuilding(guid), 200, 76, true, 34);

            var row = UIKit.Node("Row", body);
            UIKit.Stretch(row);
            Row(row, 12, new RectOffset(0, 0, 0, 0)).childForceExpandHeight = true;
            int back = S.State.trucks.Count(S.TruckBack);
            SideTabs(row.transform, 220, 200, new (string, Action<Transform>, bool, Action)[]
            {
                ("Orders", Ico("nav_contracts", 110), _garageTab != "trucks", () => _garageTab = "orders"),
                ("Trucks", Ico("nav_trains", 110), _garageTab == "trucks", () => _garageTab = "trucks"),
            });
            var panel = Box(row.transform, BTile, -1, -1, "Panel");
            UIKit.Size(panel.gameObject, -1, -1, 1);
            Stack(panel.gameObject, 0, new RectOffset(14, 14, 14, 14));

            if (_garageTab == "trucks") { TrucksPanel(panel.transform); return; }

            int ready = S.State.deliveries.Count(d => S.State.trucks.Any(t => S.CanSendTruck(t, d).Ok));
            _boardSub = back > 0 ? back + " truck" + (back > 1 ? "s" : "") + " back with pay" : ready > 0 ? ready + " ready to send" : "Local deliveries";
            if (S.State.deliveries.Count < Motor.DeliverySlots)
            {
                var timer = Pill(right, tr => ClockIcon(tr, 40), "", 30);
                var tt = timer.GetComponentInChildren<Text>();
                Action live = () => tt.text = "New order in " + Fmt.Time(S.NextDeliveryIn);
                live(); Live(live);
            }
            var cards = Scroller(panel.transform, "deliveries", true, 16);
            foreach (var t in S.State.trucks.Where(S.TruckBack).OrderBy(t => t.returnsAt).ToList()) TruckBackCard(cards, t);
            foreach (var d in S.State.deliveries.ToList()) DeliveryCard(cards, d);
            for (int i = S.State.deliveries.Count; i < Motor.DeliverySlots; i++) EmptyDelivery(cards);
        }

        /// <summary>The truck that would take this order: the smallest idle one with room.</summary>
        TruckState TruckFor(DeliveryState d)
        {
            return S.State.trucks.Where(t => !t.OnJob && S.TruckLoad(t) >= d.amount).OrderBy(S.TruckLoad).FirstOrDefault();
        }

        void DeliveryCard(Transform p, DeliveryState d)
        {
            var c = Motor.Customer(d.customerId);
            int uid = d.uid;
            var card = Box(p, BCardDeep, 500, -1, "Delivery");
            Stack(card.gameObject, 10, new RectOffset(16, 16, 16, 16), TextAnchor.UpperLeft);
            var head = Photo(card.transform, BackdropFor(d.resourceId), -1, 140, new Color(0.72f, 0.7f, 0.66f));
            Overlay(head.transform, Fade(false), new Color(0.12f, 0.15f, 0.22f, 0.97f));
            var kind = T(head.transform, "Truck order", 26, BLabel, TextAnchor.UpperLeft, false, UIKit.Heading);
            UIKit.Anchor(kind.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -46), new Vector2(-150, -12));
            var who = T(head.transform, c.Name, 36, BGold, TextAnchor.UpperLeft, true);
            UIKit.Anchor(who.gameObject, new Vector2(0, 0), new Vector2(1, 1), new Vector2(20, 8), new Vector2(-150, -50));
            var big = UIKit.ResIcon(head.transform, d.resourceId, 104);
            UIKit.Place(big, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(118, 118));

            ReqRow(card.transform, new ResourceAmount(d.resourceId, d.amount), 116);
            var truck = TruckFor(d);
            string drive = truck != null ? Fmt.Time(S.TruckTripSeconds(truck, c)) : Fmt.Time(c.Seconds);
            Pill(card.transform, tr => ClockIcon(tr, 36), "Round trip " + drive + (truck != null ? " · " + GameSession.TruckName(truck) : ""), 26);
            Flex(card.transform);
            Box(card.transform, BLine, -1, 3, "Line");
            var rw = HBox(card.transform, 12, 0, TextAnchor.MiddleCenter, -1, 100);
            rw.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            RewardCell(rw.transform, tr => UIKit.CoinIcon(tr, 60), Fmt.Num(d.rewardCoins));
            RewardCell(rw.transform, tr => UIDeco.XpIcon(tr, 60), d.rewardXp.ToString());
            foreach (var rp in d.rewardParts) { var id = rp.id; RewardCell(rw.transform, tr => UIKit.ResIcon(tr, id, 58), rp.amount.ToString()); }

            var btns = HBox(card.transform, 12, 0, TextAnchor.MiddleCenter, -1, 84);
            bool haveGoods = S.Amount(d.resourceId) >= d.amount;
            if (truck != null && haveGoods)
            {
                int tuid = truck.uid;
                Tag(Big(btns.transform, "Send truck", UIKit.Green, () => SendTruck(tuid, uid), 320, 82, true, 40), "garage.send");
            }
            else if (!haveGoods) Big(btns.transform, "Go", BBlue, () => GoGet(d.resourceId), 320, 82, true, 44);
            else
            {
                bool anyFits = S.State.trucks.Any(t => S.TruckLoad(t) >= d.amount);
                Big(btns.transform, anyFits ? "Trucks are out" : "Needs a bigger truck", UIKit.Disabled, null, 320, 82, false, 30);
            }
            Big(btns.transform, "Skip", UIKit.WoodLight, () => Result(S.SkipDelivery(uid), Sfx.Click), 146, 82, true, 32);
        }

        void SendTruck(int truckUid, int deliveryUid)
        {
            var r = S.SendTruck(truckUid, deliveryUid);
            if (!r.Ok) { Result(r, Sfx.Click); return; }
            AudioManager.Play(Sfx.Deliver);
            Haptics.Play(Haptic.Light);
            var t = S.Truck(truckUid);
            var c = t != null ? Motor.Customer(t.job.customerId) : null;
            if (t != null && c != null) DepartureBanner(GameSession.TruckName(t) + " rolls out!", "to " + c.Name + " · back in " + Fmt.Time(S.TruckRemaining(t)));
            // Like the dispatch board: stay open while another truck can go, otherwise watch it pull out.
            bool more = S.State.deliveries.Any(d => TruckFor(d) != null && S.Amount(d.resourceId) >= d.amount);
            if (!more) CloseWindow();
        }

        void TruckBackCard(Transform p, TruckState t)
        {
            int uid = t.uid;
            var card = Box(p, Palette.Hex(0x23301F), 380, -1, "Back");
            var ring = card.gameObject.AddComponent<Outline>();
            ring.effectColor = BGood;
            ring.effectDistance = new Vector2(4, -4);
            Stack(card.gameObject, 12, new RectOffset(18, 18, 18, 18), TextAnchor.UpperCenter);
            T(card.transform, "Back from " + (Motor.Customer(t.job.customerId)?.Short ?? "a delivery"), 30, BGood, TextAnchor.MiddleCenter, false, UIKit.Heading);
            var art = ModelIcons.Image(card.transform, ModelIcons.Truck(Motor.Truck(t.defId)), 340, 212);
            UIKit.Size(art.gameObject, 340, 212);
            T(card.transform, GameSession.TruckName(t), 36, Color.white, TextAnchor.MiddleCenter, true);
            Flex(card.transform);
            var rw = HBox(card.transform, 12, 0, TextAnchor.MiddleCenter, -1, 100);
            rw.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            RewardCell(rw.transform, tr => UIKit.CoinIcon(tr, 60), Fmt.Num(t.job.rewardCoins));
            RewardCell(rw.transform, tr => UIDeco.XpIcon(tr, 60), t.job.rewardXp.ToString());
            Tag(Big(card.transform, "Collect", UIKit.Gold, () => CollectTruck(uid), -1, 84, true, 42), "garage.collect");
        }

        void CollectTruck(int uid)
        {
            var view = TruckManager.Instance != null ? TruckManager.Instance.View(uid) : null;
            var r = S.CollectTruck(uid);
            Result(r, Sfx.Coin);
            if (r.Ok && view != null && view.Visible) Fx.Burst(view.Top, 30);
        }

        void EmptyDelivery(Transform p)
        {
            var card = Box(p, new Color(1, 1, 1, 0.05f), 380, -1, "Empty");
            var o = card.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.56f, 0.63f, 0.73f, 0.6f);
            o.effectDistance = new Vector2(3, -3);
            Stack(card.gameObject, 14, new RectOffset(24, 24, 24, 24), TextAnchor.MiddleCenter);
            ClockIcon(card.transform, 96);
            T(card.transform, "A customer will call soon", 34, Color.white, TextAnchor.MiddleCenter, true);
            var t = T(card.transform, "", 32, BLabel, TextAnchor.MiddleCenter, false, UIKit.Heading);
            Action live = () => t.text = Fmt.Time(S.NextDeliveryIn);
            live(); Live(live);
        }

        // ---------------------------------------------------------------- trucks

        void TrucksPanel(Transform panel)
        {
            _boardSub = S.State.trucks.Count + " truck" + (S.State.trucks.Count == 1 ? "" : "s");
            var cards = Scroller(panel, "trucks", true, 16);
            foreach (var t in S.State.trucks.OrderBy(x => x.bay).ToList()) OwnedTruckCard(cards, t);
            foreach (var d in Motor.Trucks.OrderBy(x => x.UnlockLevel)) ShopTruckCard(cards, d);
        }

        void OwnedTruckCard(Transform p, TruckState t)
        {
            var d = Motor.Truck(t.defId);
            int uid = t.uid;
            var card = Box(p, BCardDeep, 460, -1, "Truck");
            Stack(card.gameObject, 10, new RectOffset(18, 18, 16, 18), TextAnchor.UpperLeft);
            var top = HBox(card.transform, 10, 0, TextAnchor.MiddleLeft, -1, 50);
            UIKit.Size(T1(top.transform, d.Name, 36, BGold, TextAnchor.MiddleLeft, true).gameObject, -1, -1, 1);
            Pill(top.transform, null, "LV " + t.level, 24, null, UIKit.RarityColor(d.Rarity));
            var art = ModelIcons.Image(card.transform, ModelIcons.Truck(d), 420, 200);
            UIKit.Size(art.gameObject, -1, 200);
            T1(card.transform, "Carries " + S.TruckLoad(t) + " · speed " + Mathf.RoundToInt(d.Speed * Motor.SpeedMultiplier(t.level)), 28, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);

            if (S.TruckBack(t))
                Big(card.transform, "Collect " + Fmt.Num(t.job.rewardCoins), UIKit.Gold, () => CollectTruck(uid), -1, 80, true, 36);
            else if (S.TruckOut(t))
            {
                var c = Motor.Customer(t.job.customerId);
                var st = T1(card.transform, "", 28, Color.white, TextAnchor.MiddleLeft, false, UIKit.Heading);
                var bar = Track(card.transform, 16, BGold);
                Action live = () => { st.text = "To " + (c != null ? c.Short : "a customer") + " · back in " + Fmt.Time(S.TruckRemaining(t)); UIKit.SetBar(bar, S.TruckProgress(t)); };
                live(); Live(live);
                int cost = S.TruckRushCost(t);
                PriceBtn(card.transform, "Rush", Premium, tr => UIKit.NuggetIcon(tr, 40), cost.ToString(), () => Result(S.RushTruck(uid), Sfx.Coin), -1, 76, S.Nuggets >= cost, 32);
            }
            else T1(card.transform, "In bay " + (t.bay + 1) + " · ready to go", 28, BGood, TextAnchor.MiddleLeft, false, UIKit.Heading);

            Flex(card.transform);
            if (t.level < Motor.MaxTruckLevel)
            {
                var need = HBox(card.transform, 10, 0, TextAnchor.MiddleLeft, -1, 84);
                foreach (var r in S.TruckUpgradeResources(t))
                {
                    var id = r.id;
                    Well(need.transform, 80, tr => UIKit.ResIcon(tr, id, 50), S.Amount(id) + "/" + r.amount, null, S.Amount(id) >= r.amount ? Color.white : BBad);
                }
                int coins = S.TruckUpgradeCoins(t);
                Pill(need.transform, tr => UIKit.CoinIcon(tr, 40), Fmt.Num(coins), 30, S.Coins >= coins ? Color.white : BBad);
                Big(card.transform, "Upgrade to level " + (t.level + 1), UIKit.Green, () => Result(S.UpgradeTruck(uid), Sfx.Build), -1, 80, true, 34);
            }
            else T(card.transform, "Fully tuned", 32, BGood, TextAnchor.MiddleLeft, true);
        }

        void ShopTruckCard(Transform p, TruckDef d)
        {
            bool locked = !S.IsTruckUnlocked(d);
            int owned = S.TrucksOwned(d.Id);
            var card = Box(p, d.Rarity >= Rarity.Epic ? Palette.Hex(0x3A2C18) : BCard, 400, -1, "Shop");
            Stack(card.gameObject, 10, new RectOffset(18, 18, 16, 18), TextAnchor.UpperLeft);
            var top = HBox(card.transform, 10, 0, TextAnchor.MiddleLeft, -1, 50);
            UIKit.Size(T1(top.transform, d.Name, 34, Color.white, TextAnchor.MiddleLeft, true).gameObject, -1, -1, 1);
            Pill(top.transform, null, d.Rarity.ToString().ToUpperInvariant(), 22, null, UIKit.RarityColor(d.Rarity));
            var art = ModelIcons.Image(card.transform, ModelIcons.Truck(d), 360, 170);
            UIKit.Size(art.gameObject, -1, 170);
            if (locked) art.color = new Color(0.15f, 0.15f, 0.18f, 0.9f);
            T1(card.transform, "Carries " + d.Capacity + " · speed " + d.Speed + (owned > 0 ? " · own " + owned : ""), 28, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);
            var blurb = T(card.transform, "“" + d.Blurb + "”", 26, BLabel, TextAnchor.UpperLeft, false, UIKit.Body);
            blurb.fontStyle = FontStyle.Italic;
            UIKit.Size(blurb.gameObject, -1, 70);
            Flex(card.transform);
            if (locked) { Big(card.transform, Locks.Text(d.UnlockLevel), UIKit.Disabled, null, -1, 80, false, 30); return; }
            if (S.GarageFull)
            {
                var g = S.Garage;
                int guid = g.uid;
                if (g.level < Balance.MaxBuildingLevel) Big(card.transform, "Bays full · upgrade the garage", UIKit.WoodLight, () => OpenBuilding(guid), -1, 80, true, 28);
                else Big(card.transform, "Every bay is taken", UIKit.Disabled, null, -1, 80, false, 28);
                return;
            }
            string id = d.Id;
            bool nug = d.PriceNuggets > 0;
            int price = nug ? d.PriceNuggets : d.PriceCoins;
            bool can = (nug ? S.Nuggets : S.Coins) >= price;
            PriceBtn(card.transform, owned > 0 ? "Buy another" : "Buy", can ? UIKit.Green : UIKit.Disabled, tr => { if (nug) UIKit.NuggetIcon(tr, 42); else UIKit.CoinIcon(tr, 42); }, Fmt.Num(price),
                () => Result(S.BuyTruck(id), Sfx.Bell), -1, 82, true, 36);
        }
    }
}
