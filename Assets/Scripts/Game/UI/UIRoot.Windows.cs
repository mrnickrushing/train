using System;
using System.Linq;
using Ironhorse.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ironhorse.Game
{
    public sealed partial class UIRoot
    {
        string _depotTab = "locos";
        BuildingCategory _buildTab = BuildingCategory.Residential;
        int _selectedTrain;

        // ---------------------------------------------------------------- shared bits

        static GameObject Card(Transform parent, float height, Color? color = null)
        {
            var c = UIKit.Panel(parent, color ?? UIKit.ParchmentDark, "Card");
            UIKit.Size(c.gameObject, -1, height);
            UIKit.HStack(c.gameObject, 18, 14, TextAnchor.MiddleLeft);
            return c.gameObject;
        }

        static GameObject Col(Transform parent, float flex = 1, float width = -1)
        {
            var v = UIKit.Node("Col", parent);
            UIKit.VStack(v, 4, 0, TextAnchor.MiddleLeft);
            UIKit.Size(v, width, -1, width < 0 ? flex : -1);
            return v;
        }

        static Text Line(Transform parent, string text, int size = 22, Color? c = null, bool heading = false)
        {
            var t = UIKit.Label(parent, text, size, c ?? UIKit.Ink, TextAnchor.MiddleLeft, heading);
            UIKit.Size(t.gameObject, -1, size + 10);
            return t;
        }

        static string Plural(string noun) { return noun.EndsWith("ch") ? noun + "es" : noun + "s"; }

        static void Heading(Transform parent, string text)
        {
            var t = UIKit.Label(parent, text.ToUpperInvariant(), 24, UIKit.BrassDark, TextAnchor.MiddleLeft, true, FontStyle.Bold);
            UIKit.Size(t.gameObject, -1, 40);
        }

        void Tabs(Transform parent, (string label, bool active, Action onClick)[] tabs)
        {
            var row = UIKit.Row(parent, 60, 10, TextAnchor.MiddleCenter);
            foreach (var t in tabs)
                UIKit.Btn(row.transform, t.label, t.active ? UIKit.BrassDark : UIKit.WoodLight, () => { t.onClick(); RebuildWindow(); }, 22, 200, 54);
        }

        static GameObject LocoSwatch(Transform parent, LocomotiveDef d, float size = 90)
        {
            var bg = UIKit.Panel(parent, UIKit.RarityColor(d.Rarity), "Swatch", UIKit.Rounded);
            UIKit.Size(bg.gameObject, size, size);
            var body = UIKit.Panel(bg.transform, Palette.Hex(d.PrimaryRgb), "Body", UIKit.RoundedSmall);
            UIKit.Place(body.gameObject, new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.7f, size * 0.36f));
            var trim = UIKit.Panel(bg.transform, Palette.Hex(d.TrimRgb), "Trim", UIKit.RoundedSmall);
            UIKit.Place(trim.gameObject, new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.7f, size * 0.06f));
            bool steam = d.Style != LocoStyle.DieselStreamliner && d.Style != LocoStyle.DieselTwin;
            if (steam)
            {
                var stack = UIKit.Panel(bg.transform, UIKit.Wood, "Stack", UIKit.RoundedSmall);
                UIKit.Place(stack.gameObject, new Vector2(0.7f, 0.7f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.1f, size * 0.18f));
            }
            for (int i = 0; i < 3; i++)
            {
                var w = UIKit.Panel(bg.transform, UIKit.Wood, "Wheel", UIKit.Circle);
                UIKit.Place(w.gameObject, new Vector2(0.28f + i * 0.22f, 0.22f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.16f, size * 0.16f));
            }
            return bg.gameObject;
        }

        // ================================================================ fleet

        public void OpenFleet()
        {
            ShowWindow("Your Fleet", content =>
            {
                var trains = S.State.trains.OrderBy(t => t.platform).ToList();
                Line(content, trains.Count + " of " + S.FleetCapacity + " platforms in use. Tap a train for details & dispatch.", 22, UIKit.InkSoft);
                foreach (var t in trains) FleetCard(content, t);
                if (trains.Count < S.FleetCapacity) UIKit.Btn(content, "Buy another locomotive", UIKit.BrassDark, () => OpenDepot(), 24, -1, 64);
            });
        }

        void FleetCard(Transform content, TrainState t)
        {
            var d = GameDatabase.Locomotive(t.defId);
            var card = Card(content, 130);
            LocoSwatch(card.transform, d);
            var col = Col(card.transform);
            Line(col.transform, GameSession.Name(t) + "   Lv " + t.level, 26, UIKit.Ink, true);
            var wagons = S.WagonsOf(t);
            Line(col.transform, wagons.Count + "/" + d.Power + " wagons · " + string.Join(", ", wagons.GroupBy(w => GameDatabase.Wagon(w.defId).Name).Select(g => g.Count() + "× " + g.Key)), 20, UIKit.InkSoft);
            var status = Line(col.transform, "", 22, UIKit.Ink);
            var barRow = UIKit.Row(col.transform, 20);
            var bar = UIKit.Bar(barRow.transform, UIKit.Green, 16);
            int uid = t.uid;
            Action live = () =>
            {
                var tt = S.Train(uid);
                if (tt == null) return;
                if (tt.IsIdle) { status.text = "Waiting at platform " + (tt.platform + 1); status.color = UIKit.Green; barRow.SetActive(false); }
                else if (S.HasArrived(tt)) { status.text = "Arrived with " + tt.cargoAmount + " " + GameDatabase.Resource(tt.cargoId).Name + "!"; status.color = UIKit.BrassDark; barRow.SetActive(false); }
                else
                {
                    status.text = "Bound for " + GameDatabase.Destination(tt.destinationId).Name + " · " + Fmt.Time(S.Remaining(tt));
                    status.color = UIKit.Ink;
                    barRow.SetActive(true);
                    UIKit.SetBar(bar, S.TripProgress(tt));
                }
            };
            live(); Live(live);

            if (S.HasArrived(t)) UIKit.Btn(card.transform, "Unload", UIKit.Green, () => Result(S.Unload(uid)), 24, 180, 64);
            else if (t.IsIdle) UIKit.Btn(card.transform, "Dispatch", UIKit.Red, () => OpenTrain(uid), 24, 180, 64);
            else
            {
                int cost = S.TrainRushCost(t);
                UIKit.Btn(card.transform, "Rush " + cost, UIKit.Blue, () => Result(S.RushTrain(uid)), 22, 160, 64);
                if (S.AdsLeft(AdReward.RushTrain) > 0 && Ads.Current.Available)
                    UIKit.Btn(card.transform, "Ad: -30m", UIKit.Green, () => Ads.Watch(AdReward.RushTrain, uid, r => Result(r, Sfx.Bell)), 20, 150, 64);
            }
            UIKit.Btn(card.transform, "Details", UIKit.WoodLight, () => OpenTrain(uid), 22, 150, 64);
        }

        // ================================================================ one train: dispatch + consist

        public void OpenTrain(int uid)
        {
            _selectedTrain = uid;
            var t0 = S.Train(uid);
            if (t0 == null) return;
            var view = TrainManager.Instance.View(uid);
            if (view != null && view.IsShownAtStation) CameraRig.Instance.FlyTo(view.LeadPosition, 70);
            ShowWindow(GameSession.Name(t0), content =>
            {
                var t = S.Train(_selectedTrain);
                if (t == null) { CloseWindow(); return; }
                var d = GameDatabase.Locomotive(t.defId);

                // Header card
                var head = Card(content, 150);
                LocoSwatch(head.transform, d, 120);
                var col = Col(head.transform);
                Line(col.transform, d.Name, 30, UIKit.Ink, true);
                Line(col.transform, d.Rarity + " · " + d.Era + " era · Level " + t.level + "/" + Balance.MaxLocoLevel, 20, UIKit.RarityColor(d.Rarity));
                Line(col.transform, "Pulls " + d.Power + " wagons · Speed " + Mathf.RoundToInt(d.Speed * Balance.LocoSpeedMultiplier(t.level)) + " · Cargo ×" + Balance.LocoCapacityMultiplier(t.level).ToString("0.00"), 20, UIKit.InkSoft);
                Line(col.transform, d.Blurb, 18, UIKit.InkSoft);

                if (S.HasArrived(t))
                {
                    UIKit.Btn(content, "Unload " + t.cargoAmount + " " + GameDatabase.Resource(t.cargoId).Name, UIKit.Green, () => Result(S.Unload(t.uid)), 28, -1, 74);
                }
                else if (!t.IsIdle)
                {
                    var row = Card(content, 90);
                    var status = Line(row.transform, "", 24, UIKit.Ink, true);
                    UIKit.Size(status.gameObject, -1, 40, 1);
                    var bar = UIKit.Bar(row.transform, UIKit.Green, 20);
                    UIKit.Size(bar.transform.parent.gameObject, 260, 20);
                    Action live = () =>
                    {
                        var tt = S.Train(_selectedTrain);
                        if (tt == null || tt.IsIdle) return;
                        status.text = "En route to " + GameDatabase.Destination(tt.destinationId).Name + " · " + Fmt.Time(S.Remaining(tt));
                        UIKit.SetBar(bar, S.TripProgress(tt));
                    };
                    live(); Live(live);
                    UIKit.Btn(row.transform, "Rush " + S.TrainRushCost(t), UIKit.Blue, () => Result(S.RushTrain(t.uid)), 22, 170, 60);
                }
                else
                {
                    Heading(content, "Dispatch to");
                    foreach (var dest in GameDatabase.Destinations)
                    {
                        var p = S.PreviewDispatch(t, dest);
                        var res = GameDatabase.Resource(dest.ResourceId);
                        bool locked = !S.IsDestinationUnlocked(dest);
                        var c = Card(content, 100, locked ? new Color(0.8f, 0.76f, 0.68f) : (Color?)null);
                        UIKit.ResIcon(c.transform, dest.ResourceId, 64);
                        var dc = Col(c.transform);
                        Line(dc.transform, dest.Name, 24, locked ? UIKit.InkSoft : UIKit.Ink, true);
                        string what = dest.IsTown ? "Passenger town · up to " + Balance.PassengerDemand(S.Population) + " riders from your town · fare x" + dest.Yield.ToString("0.0") : res.Name;
                        Line(dc.transform, locked ? "Unlocks at level " + dest.UnlockLevel : what + " · needs " + Plural(GameSession.CargoWagonName(res.Cargo)) + " · " + Fmt.Time(p.Seconds) + " · " + p.Fuel + " fuel", 20, UIKit.InkSoft);
                        if (!locked)
                        {
                            string id = dest.Id;
                            if (p.Possible) UIKit.Btn(c.transform, "Go! +" + p.Amount, UIKit.Red, () => { var r = S.Dispatch(t.uid, id); Result(r, Sfx.Click); if (r.Ok) CloseWindow(); }, 24, 190, 62);
                            else if (p.Reason.Contains("fuel")) UIKit.Btn(c.transform, "Get fuel", FuelOrange, () => OpenFuel(), 22, 190, 62);
                            else Line(c.transform, p.Reason, 20, UIKit.Red).GetComponent<LayoutElement>().preferredWidth = 220;
                        }
                    }
                }

                // Consist
                Heading(content, "Consist (" + S.WagonsOf(t).Count + "/" + d.Power + ")");
                foreach (var w in S.WagonsOf(t))
                {
                    var wd = GameDatabase.Wagon(w.defId);
                    var c = Card(content, 76);
                    WagonSwatch(c.transform, wd);
                    Line(c.transform, wd.Name + " · carries " + wd.Capacity + " " + wd.Cargo.ToString().ToLowerInvariant() + " cargo", 22).GetComponent<LayoutElement>().flexibleWidth = 1;
                    int wuid = w.uid;
                    if (t.IsIdle) UIKit.Btn(c.transform, "Uncouple", UIKit.WoodLight, () => Result(S.DetachWagon(wuid), Sfx.Click), 20, 160, 52);
                }
                var depot = S.DepotWagons();
                if (t.IsIdle && depot.Count > 0 && S.WagonsOf(t).Count < d.Power)
                {
                    Heading(content, "In the depot");
                    foreach (var w in depot)
                    {
                        var wd = GameDatabase.Wagon(w.defId);
                        var c = Card(content, 76);
                        WagonSwatch(c.transform, wd);
                        Line(c.transform, wd.Name + " · " + wd.Capacity + " " + wd.Cargo.ToString().ToLowerInvariant(), 22).GetComponent<LayoutElement>().flexibleWidth = 1;
                        int wuid = w.uid;
                        UIKit.Btn(c.transform, "Couple", UIKit.Green, () => Result(S.AttachWagon(wuid, t.uid), Sfx.Click), 20, 160, 52);
                    }
                }
                if (t.IsIdle && S.WagonsOf(t).Count < d.Power)
                    UIKit.Btn(content, "Buy wagons at the Depot", UIKit.BrassDark, () => OpenDepot("wagons"), 22, -1, 60);

                // Upgrade
                Heading(content, "Workshop");
                if (t.level < Balance.MaxLocoLevel)
                {
                    var u = Card(content, 96);
                    var uc = Col(u.transform);
                    Line(uc.transform, "Overhaul to level " + (t.level + 1) + ": +12% cargo, +4% speed", 22);
                    UIKit.Cost(uc.transform, S, S.LocoUpgradeCoins(t), S.LocoUpgradeResources(t));
                    UIKit.Btn(u.transform, "Upgrade", UIKit.Green, () => Result(S.UpgradeLocomotive(t.uid), Sfx.Build), 22, 180, 62);
                }
                else Line(content, "This locomotive is fully overhauled.", 22, UIKit.Green);
            }, 1200, 900);
        }

        /// <summary>Crew gauge, conveyor belt of job slots, and the controls to queue, collect, rush and extend.</summary>
        void ConveyorPanel(Transform content, BuildingState b, int uid)
        {
            var r = GameDatabase.Building(b.defId).Recipe;
            var outRes = GameDatabase.Resource(r.Output.id);

            var crewRow = Card(content, 80);
            CrewIcon(crewRow.transform);
            var crewText = Line(crewRow.transform, "Crew " + S.CrewFree + "/" + S.CrewTotal + " free · each job needs 1 hand until you collect it", 22);
            crewText.GetComponent<LayoutElement>().flexibleWidth = 1;
            if (S.State.hiredCrew < Balance.MaxHiredCrew)
            {
                var hire = UIKit.Btn(crewRow.transform, "", Purple, () => Result(S.HireCrew(), Sfx.Build), 22, 220, 60, S.Nuggets >= S.HireCrewCost);
                PriceLabelWithNugget(hire, S.HireCrewCost);
                var hl = UIKit.Label(hire.transform, "HIRE", 14, UIKit.Cream, TextAnchor.UpperCenter, true);
                UIKit.Anchor(hl.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -16), new Vector2(0, 2));
            }

            // The conveyor belt: one box per slot.
            var belt = UIKit.Panel(content, Palette.Hex(0x2E2A28), "Conveyor", null, true);
            UIKit.Size(belt.gameObject, -1, 170);
            UIKit.HStack(belt.gameObject, 14, 16, TextAnchor.MiddleLeft);
            int slots = UIRootSlotsShown(b);
            for (int i = 0; i < slots; i++)
            {
                bool filled = i < b.queue.Count;
                var box = UIKit.Panel(belt.transform, filled ? UIKit.ParchmentDark : new Color(1, 1, 1, 0.08f), "Slot", UIKit.Rounded);
                UIKit.Size(box.gameObject, 130, 130);
                UIKit.VStack(box.gameObject, 2, 8, TextAnchor.MiddleCenter);
                if (filled)
                {
                    var job = b.queue[i];
                    UIKit.ResIcon(box.transform, r.Output.id, 64);
                    var lbl = UIKit.Label(box.transform, "", 18, UIKit.Ink, TextAnchor.MiddleCenter, true);
                    UIKit.Size(lbl.gameObject, -1, 24);
                    var bar = UIKit.Bar(box.transform, UIKit.Green, 10);
                    Action live = () =>
                    {
                        double left = job.endsAt - S.Now;
                        bool queued = S.Now < job.startsAt;
                        lbl.text = left <= 0 ? "Done!" : queued ? "Queued" : Fmt.Time(left);
                        double total = job.endsAt - job.startsAt;
                        UIKit.SetBar(bar, left <= 0 ? 1 : queued || total <= 0 ? 0 : (float)((S.Now - job.startsAt) / total));
                    };
                    live(); Live(live);
                }
                else if (i < S.QueueSlots(b))
                {
                    var plus = UIKit.Label(box.transform, "+", 54, new Color(1, 1, 1, 0.5f), TextAnchor.MiddleCenter, true);
                    UIKit.Size(plus.gameObject, -1, 80);
                    Clickable(box.gameObject, () => Result(S.Enqueue(uid), Sfx.Build));
                }
                else
                {
                    // A locked slot you can buy.
                    box.color = Purple;
                    var add = UIKit.Label(box.transform, "ADD SLOT", 18, UIKit.Cream, TextAnchor.MiddleCenter, true, FontStyle.Bold);
                    UIKit.Size(add.gameObject, -1, 30);
                    var price = UIKit.Node("Price", box.transform);
                    UIKit.HStack(price, 6, 0, TextAnchor.MiddleCenter);
                    UIKit.Size(price, -1, 40);
                    UIKit.NuggetIcon(price.transform, 30);
                    UIKit.Label(price.transform, S.ExtraSlotCost(b).ToString(), 22, UIKit.Cream, TextAnchor.MiddleLeft, true);
                    Clickable(box.gameObject, () => Result(S.BuySlot(uid), Sfx.Build));
                }
            }

            var row = UIKit.Row(content, 76, 14, TextAnchor.MiddleCenter);
            int afford = S.AffordableBatches(b);
            UIKit.Btn(row.transform, "Queue 1 · " + Fmt.Time(S.ProductionSeconds(b, 1)), UIKit.Green, () => Result(S.Enqueue(uid), Sfx.Build), 22, 260, 68, afford > 0);
            if (afford > 1) UIKit.Btn(row.transform, "Fill conveyor (" + afford + ")", UIKit.Green, () => Result(S.StartProduction(uid, afford), Sfx.Build), 22, 280, 68);
            int done = S.FinishedJobs(b);
            if (done > 0) UIKit.Btn(row.transform, "Collect " + r.Output.amount * done + " " + outRes.Name, UIKit.BrassDark, () => Result(S.CollectProduction(uid)), 22, 320, 68);
            if (S.IsProducing(b)) UIKit.Btn(row.transform, "Rush all " + S.RushProductionCost(b), UIKit.Blue, () => Result(S.RushProduction(uid), Sfx.Click), 22, 220, 68);
            if (afford == 0 && b.queue.Count < S.QueueSlots(b))
                Line(content, S.CrewFree == 0 ? "All hands are busy. Collect finished goods or hire more crew." : "Not enough materials. Dispatch trains to gather them.", 20, UIKit.Red);
        }

        int UIRootSlotsShown(BuildingState b) { return S.QueueSlots(b) + (b.extraSlots < Balance.MaxExtraSlots ? 1 : 0); }

        static void CrewIcon(Transform t)
        {
            var h = UIKit.Node("Crew", t);
            UIKit.Size(h, 56, 56);
            var helmet = UIKit.Panel(h.transform, UIKit.Gold, "Hat", UIKit.Circle);
            UIKit.Place(helmet.gameObject, new Vector2(0.5f, 0.78f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 18));
            var head = UIKit.Panel(h.transform, Palette.Hex(0xE0B48A), "Head", UIKit.Circle);
            UIKit.Place(head.gameObject, new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 22));
            var body = UIKit.Panel(h.transform, UIKit.Blue, "Body", UIKit.RoundedSmall);
            UIKit.Place(body.gameObject, new Vector2(0.5f, 0.2f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(38, 24));
        }

        static void WagonSwatch(Transform parent, WagonDef wd)
        {
            var bg = UIKit.Panel(parent, Palette.Hex(wd.ColorRgb), "Wagon", UIKit.RoundedSmall);
            UIKit.Size(bg.gameObject, 76, 46);
            for (int i = 0; i < 2; i++)
            {
                var w = UIKit.Panel(bg.transform, UIKit.Wood, "Wheel", UIKit.Circle);
                UIKit.Place(w.gameObject, new Vector2(0.25f + i * 0.5f, 0), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16, 16));
            }
        }

        // ================================================================ depot (shop)

        public void OpenDepot(string tab = null)
        {
            if (tab != null) _depotTab = tab;
            ShowWindow("Rolling Stock Depot", content =>
            {
                Tabs(content, new (string, bool, Action)[]
                {
                    ("Locomotives", _depotTab == "locos", () => _depotTab = "locos"),
                    ("Wagons", _depotTab == "wagons", () => _depotTab = "wagons"),
                });
                if (_depotTab == "locos")
                {
                    Line(content, "Platforms: " + S.State.trains.Count + "/" + S.FleetCapacity, 22, UIKit.InkSoft);
                    foreach (var d in GameDatabase.Locomotives)
                    {
                        if (d.PriceCoins == 0 && d.PriceNuggets == 0) continue;
                        bool locked = !S.IsLocoUnlocked(d);
                        var c = Card(content, 140, locked ? new Color(0.8f, 0.76f, 0.68f) : (Color?)null);
                        LocoSwatch(c.transform, d, 110);
                        var col = Col(c.transform);
                        Line(col.transform, d.Name, 26, UIKit.Ink, true);
                        Line(col.transform, d.Rarity + " · " + d.Era + " · Pulls " + d.Power + " · Speed " + d.Speed, 20, UIKit.RarityColor(d.Rarity));
                        Line(col.transform, d.Blurb, 18, UIKit.InkSoft);
                        BlueprintRow(col.transform, d);
                        if (locked) Line(c.transform, "Level " + d.UnlockLevel, 24, UIKit.InkSoft, true).GetComponent<LayoutElement>().preferredWidth = 150;
                        else
                        {
                            var bc = Col(c.transform, -1, 220);
                            UIKit.Cost(bc.transform, S, d.PriceCoins, null, d.PriceNuggets);
                            string id = d.Id;
                            UIKit.Btn(bc.transform, "Buy", UIKit.Green, () => Result(S.BuyLocomotive(id), Sfx.Bell), 24, -1, 56);
                        }
                    }
                }
                else
                {
                    var idle = S.State.trains.Where(t => t.IsIdle && S.WagonsOf(t).Count < GameDatabase.Locomotive(t.defId).Power).ToList();
                    Line(content, idle.Count > 0 ? "New wagons couple to the first idle train with room: " + GameSession.Name(idle[0]) : "New wagons wait in the depot until a train has room.", 20, UIKit.InkSoft);
                    foreach (var d in GameDatabase.Wagons)
                    {
                        bool locked = !S.IsWagonUnlocked(d);
                        var c = Card(content, 96, locked ? new Color(0.8f, 0.76f, 0.68f) : (Color?)null);
                        WagonSwatch(c.transform, d);
                        var col = Col(c.transform);
                        Line(col.transform, d.Name, 24, UIKit.Ink, true);
                        var hauls = GameDatabase.Resources.Where(r => r.Cargo == d.Cargo && !r.Manufactured).Select(r => r.Name);
                        Line(col.transform, "Capacity " + d.Capacity + " · hauls " + string.Join(", ", hauls), 20, UIKit.InkSoft);
                        if (locked) Line(c.transform, "Level " + d.UnlockLevel, 24, UIKit.InkSoft, true).GetComponent<LayoutElement>().preferredWidth = 150;
                        else
                        {
                            var bc = Col(c.transform, -1, 220);
                            UIKit.Cost(bc.transform, S, d.PriceCoins, null);
                            string id = d.Id;
                            int target = idle.Count > 0 ? idle[0].uid : 0;
                            UIKit.Btn(bc.transform, "Buy", UIKit.Green, () => Result(S.BuyWagon(id, target)), 22, -1, 52);
                        }
                    }
                }
            });
        }

        // ================================================================ contracts

        public void OpenContracts()
        {
            ShowWindow("Contract Board", content =>
            {
                Tabs(content, new (string, bool, Action)[]
                {
                    ("Orders", _ordersTab == "orders", () => _ordersTab = "orders"),
                    ("Projects", _ordersTab == "projects", () => _ordersTab = "projects"),
                });
                if (_ordersTab == "projects") { ProjectsTab(content); return; }
                Line(content, "Townsfolk and outfits post orders here. Deliver goods for coins, XP, workshop parts and sometimes gold nuggets.", 20, UIKit.InkSoft);
                foreach (var c in S.State.contracts.ToList())
                {
                    bool can = S.CanDeliver(c);
                    var card = Card(content, 150, can ? Palette.Hex(0xDCE8C0) : (Color?)null);
                    var col = Col(card.transform);
                    Line(col.transform, c.client, 26, UIKit.Ink, true);
                    var req = UIKit.Row(col.transform, 46, 22);
                    foreach (var r in c.requirements)
                        UIKit.Amount(req.transform, r.id, S.Amount(r.id) + "/" + r.amount + " " + GameDatabase.Resource(r.id).Name, S.Amount(r.id) >= r.amount ? UIKit.Green : UIKit.Red, 40, 22);
                    var rew = UIKit.Row(col.transform, 40, 14);
                    UIKit.Label(rew.transform, "Reward:", 20, UIKit.InkSoft);
                    UIKit.CoinIcon(rew.transform, 30); UIKit.Label(rew.transform, Fmt.Num(c.rewardCoins), 22, UIKit.Ink, TextAnchor.MiddleLeft, true);
                    UIKit.Label(rew.transform, "+" + c.rewardXp + " XP", 22, UIKit.Blue, TextAnchor.MiddleLeft, true);
                    if (c.rewardNuggets > 0) { UIKit.NuggetIcon(rew.transform, 30); UIKit.Label(rew.transform, "+" + c.rewardNuggets, 22, UIKit.Ink, TextAnchor.MiddleLeft, true); }
                    if (c.rewardParts != null) foreach (var p in c.rewardParts) UIKit.Amount(rew.transform, p.id, "+" + p.amount, UIKit.Ink, 30, 22);
                    var bc = Col(card.transform, -1, 200);
                    int uid = c.uid;
                    UIKit.Btn(bc.transform, "Deliver", UIKit.Green, () => Result(S.DeliverContract(uid), Sfx.Deliver), 24, -1, 60, can);
                    UIKit.Btn(bc.transform, "Tear up", UIKit.WoodLight, () => Result(S.DiscardContract(uid), Sfx.Click), 18, -1, 44);
                }
                if (S.State.contracts.Count < S.ContractSlots)
                {
                    var t = Line(content, "", 22, UIKit.InkSoft);
                    Action live = () => t.text = "Next order arrives in " + Fmt.Time(S.NextContractIn);
                    live(); Live(live);
                }
                Line(content, "Build a Telegraph Office for an extra contract slot.", 18, UIKit.InkSoft);
            });
        }

        // ================================================================ build menu

        public void OpenBuildMenu()
        {
            ShowWindow("Build", content =>
            {
                Tabs(content, new (string, bool, Action)[]
                {
                    ("Homes", _buildTab == BuildingCategory.Residential, () => _buildTab = BuildingCategory.Residential),
                    ("Industry", _buildTab == BuildingCategory.Production, () => _buildTab = BuildingCategory.Production),
                    ("Civic", _buildTab == BuildingCategory.Civic, () => _buildTab = BuildingCategory.Civic),
                    ("Decor", _buildTab == BuildingCategory.Decoration, () => _buildTab = BuildingCategory.Decoration),
                });
                Line(content, "Free plots: " + S.FreePlots().Count + " · Owned land: " + S.State.unlockedPlots + "/" + Balance.TotalPlots, 20, UIKit.InkSoft);
                foreach (var d in GameDatabase.Buildings.Where(b => b.Category == _buildTab))
                {
                    bool locked = !S.IsBuildingUnlocked(d);
                    var c = Card(content, 140, locked ? new Color(0.8f, 0.76f, 0.68f) : (Color?)null);
                    BuildingSwatch(c.transform, d);
                    var col = Col(c.transform);
                    Line(col.transform, d.Name + "   (" + S.CountBuilt(d.Id) + "/" + d.MaxCount + ")", 26, UIKit.Ink, true);
                    string info = d.Blurb;
                    if (d.Recipe != null)
                        info = string.Join(" + ", d.Recipe.Inputs.Select(i => i.amount + " " + GameDatabase.Resource(i.id).Name)) + "  →  " + d.Recipe.Output.amount + " " + GameDatabase.Resource(d.Recipe.Output.id).Name + "  (" + Fmt.Time(d.Recipe.Seconds) + ")";
                    Line(col.transform, info, 20, UIKit.InkSoft);
                    if (!locked) UIKit.Cost(col.transform, S, d.CostCoins, d.CostResources);
                    if (locked) Line(c.transform, "Level " + d.UnlockLevel, 24, UIKit.InkSoft, true).GetComponent<LayoutElement>().preferredWidth = 150;
                    else
                    {
                        var check = S.CanConstruct(d.Id);
                        string id = d.Id;
                        UIKit.Btn(c.transform, "Build", UIKit.Green, () =>
                        {
                            var r = S.CanConstruct(id);
                            if (r.Ok) TownView.Instance.BeginPlacement(id);
                            else Toast(r.Message, false);
                        }, 24, 170, 62, check.Ok);
                    }
                }
                if (S.CanBuyPlot) UIKit.Btn(content, "Survey more land", UIKit.BrassDark, () => OpenLand(), 22, -1, 60);
            });
        }

        static void BuildingSwatch(Transform parent, BuildingDef d)
        {
            Color c;
            switch (d.Category)
            {
                case BuildingCategory.Residential: c = Palette.Hex(0xC8A27A); break;
                case BuildingCategory.Production: c = Palette.Brick; break;
                case BuildingCategory.Civic: c = Palette.PaintBlue; break;
                default: c = Palette.PaintGreen; break;
            }
            var bg = UIKit.Panel(parent, c, "Swatch", UIKit.Rounded);
            UIKit.Size(bg.gameObject, 96, 96);
            var house = UIKit.Panel(bg.transform, UIKit.Cream, "House", UIKit.RoundedSmall);
            UIKit.Place(house.gameObject, new Vector2(0.5f, 0.38f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(52, 38));
            var roof = UIKit.Panel(bg.transform, UIKit.Wood, "Roof", UIKit.RoundedSmall);
            UIKit.Place(roof.gameObject, new Vector2(0.5f, 0.66f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(42, 42)).localRotation = Quaternion.Euler(0, 0, 45);
            var door = UIKit.Panel(bg.transform, UIKit.Wood, "Door", UIKit.RoundedSmall);
            UIKit.Place(door.gameObject, new Vector2(0.5f, 0.3f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(12, 20));
        }

        // ================================================================ one building

        public void OpenBuilding(int uid)
        {
            var b0 = S.Building(uid);
            if (b0 == null) return;
            CameraRig.Instance.FlyTo(WorldLayout.Plots[b0.plot], 60);
            ShowWindow(GameDatabase.Building(b0.defId).Name, content =>
            {
                var b = S.Building(uid);
                if (b == null) { CloseWindow(); return; }
                var d = GameDatabase.Building(b.defId);
                var head = Card(content, 120);
                BuildingSwatch(head.transform, d);
                var col = Col(head.transform);
                Line(col.transform, d.Name + " · Level " + b.level, 28, UIKit.Ink, true);
                Line(col.transform, d.Blurb, 20, UIKit.InkSoft);

                if (!S.IsReady(b))
                {
                    var c = Card(content, 90);
                    var t = Line(c.transform, "", 24, UIKit.Ink, true);
                    UIKit.Size(t.gameObject, -1, 40, 1);
                    var bar = UIKit.Bar(c.transform, UIKit.BrassDark, 20);
                    UIKit.Size(bar.transform.parent.gameObject, 260, 20);
                    Action live = () => { var bb = S.Building(uid); if (bb == null) return; t.text = "Under construction · " + Fmt.Time(S.ConstructionRemaining(bb)); UIKit.SetBar(bar, S.ConstructionProgress(bb)); };
                    live(); Live(live);
                    UIKit.Btn(c.transform, "Rush " + Balance.RushCost(S.ConstructionRemaining(b)), UIKit.Blue, () => Result(S.RushConstruction(uid), Sfx.Build), 22, 170, 60);
                    return;
                }

                switch (d.Category)
                {
                    case BuildingCategory.Residential:
                    {
                        var c = Card(content, 110);
                        var cc = Col(c.transform);
                        Line(cc.transform, Mathf.RoundToInt(d.Population * Balance.BuildingLevelMultiplier(b.level)) + " settlers · " + S.RentPerMinute(b).ToString("0.0") + " coins/min", 22);
                        var t = Line(cc.transform, "", 22, UIKit.InkSoft);
                        var bar = UIKit.Bar(cc.transform, UIKit.Gold, 16);
                        Action live = () => { var bb = S.Building(uid); if (bb == null) return; t.text = "Rent waiting: " + S.PendingRent(bb) + " (storage fills in " + Fmt.Time(S.RentStorageSeconds) + ")"; UIKit.SetBar(bar, S.RentFill(bb)); };
                        live(); Live(live);
                        UIKit.Btn(c.transform, "Collect", UIKit.Green, () => Result(S.CollectRent(uid)), 24, 180, 62);
                        UIKit.Btn(content, "Collect rent from every home", UIKit.BrassDark, () =>
                        {
                            int got = S.CollectAllRent();
                            if (got > 0) { AudioManager.Play(Sfx.Coin); Toast("+" + got + " coins collected", true); }
                        }, 22, -1, 56);
                        break;
                    }
                    case BuildingCategory.Production:
                    {
                        var r = d.Recipe;
                        var recipe = Card(content, 80);
                        foreach (var i in r.Inputs) UIKit.Amount(recipe.transform, i.id, i.amount + " (have " + S.Amount(i.id) + ")", S.Amount(i.id) >= i.amount ? UIKit.Ink : UIKit.Red, 44, 22);
                        UIKit.Label(recipe.transform, "→", 36, UIKit.Ink, TextAnchor.MiddleCenter, true);
                        UIKit.Amount(recipe.transform, r.Output.id, r.Output.amount + " " + GameDatabase.Resource(r.Output.id).Name, UIKit.Ink, 44, 22);
                        ConveyorPanel(content, b, uid);
                        break;
                    }
                    default:
                        Line(content, "Effect: " + d.Blurb, 22);
                        break;
                }

                if (S.CanUpgradeBuilding(b))
                {
                    Heading(content, "Improve");
                    var u = Card(content, 100);
                    var uc = Col(u.transform);
                    string what = d.Category == BuildingCategory.Residential ? "+25% settlers and rent" : d.Category == BuildingCategory.Production ? "+25% speed and +1 batch" : "+25% effect";
                    Line(uc.transform, "Upgrade to level " + (b.level + 1) + ": " + what, 22);
                    UIKit.Cost(uc.transform, S, S.BuildingUpgradeCoins(b), S.BuildingUpgradeResources(b));
                    UIKit.Btn(u.transform, "Upgrade", UIKit.Green, () => Result(S.UpgradeBuilding(uid), Sfx.Build), 22, 180, 62);
                }
            }, 1100, 780);
        }

        // ================================================================ warehouse

        public void OpenWarehouse()
        {
            ShowWindow("Warehouse", content =>
            {
                var cap = Card(content, 110);
                var cc = Col(cap.transform);
                Line(cc.transform, "Stored " + S.WarehouseUsed + " of " + S.WarehouseCapacity + " · Level " + S.State.warehouseLevel, 26, UIKit.Ink, true);
                var bar = UIKit.Bar(cc.transform, S.WarehouseFree < S.WarehouseCapacity * 0.1f ? UIKit.Red : UIKit.Green, 22);
                UIKit.SetBar(bar, S.WarehouseUsed / (float)S.WarehouseCapacity);
                if (S.State.warehouseLevel < Balance.MaxWarehouseLevel)
                {
                    var uc = Col(cap.transform, -1, 360);
                    UIKit.Cost(uc.transform, S, Balance.WarehouseUpgradeCoins(S.State.warehouseLevel), Balance.WarehouseUpgradeResources(S.State.warehouseLevel));
                    UIKit.Btn(uc.transform, "Expand (+150)", UIKit.Green, () => Result(S.UpgradeWarehouse(), Sfx.Build), 22, -1, 54);
                }
                if (S.State.inventory.Count == 0) Line(content, "Empty! Send trains out to the territory to haul goods home.", 22, UIKit.InkSoft);
                var parts = S.State.inventory.Where(r => GameDatabase.Resource(r.id).IsPart).ToList();
                if (parts.Count > 0)
                {
                    Heading(content, "Workshop parts (no warehouse space needed)");
                    var pr = UIKit.Row(content, 60, 24);
                    foreach (var p in parts) UIKit.Amount(pr.transform, p.id, GameDatabase.Resource(p.id).Name + " " + p.amount, UIKit.Ink, 46, 22);
                    Heading(content, "Goods");
                }
                foreach (var r in S.State.inventory.Where(r => !GameDatabase.Resource(r.id).IsPart).OrderBy(r => GameDatabase.Resource(r.id).Manufactured).ThenBy(r => r.id))
                {
                    var res = GameDatabase.Resource(r.id);
                    var c = Card(content, 80);
                    UIKit.ResIcon(c.transform, r.id, 56);
                    Line(c.transform, res.Name, 24, UIKit.Ink, true).GetComponent<LayoutElement>().flexibleWidth = 1;
                    Line(c.transform, Fmt.Num(r.amount), 28, UIKit.Ink, true).GetComponent<LayoutElement>().preferredWidth = 120;
                    string id = r.id;
                    int sell = Mathf.Min(10, r.amount);
                    UIKit.Btn(c.transform, "Sell " + sell + " (" + Mathf.Max(1, (int)(res.Value * sell * 0.6f)) + ")", UIKit.WoodLight, () => Result(S.Sell(id, sell)), 20, 220, 54);
                }
            }, 1000, 820);
        }

        // ================================================================ land

        public void OpenLand()
        {
            ShowWindow("Land Office", content =>
            {
                Line(content, "You own " + S.State.unlockedPlots + " of " + Balance.TotalPlots + " town lots.", 24, UIKit.Ink, true);
                if (!S.CanBuyPlot) { Line(content, "Every lot in the valley is yours!", 22, UIKit.Green); return; }
                var c = Card(content, 110);
                var col = Col(c.transform);
                Line(col.transform, "Next lot: survey and stake a claim", 22);
                if (S.Level < S.NextPlotLevel) Line(col.transform, "Requires level " + S.NextPlotLevel, 22, UIKit.Red);
                UIKit.Cost(col.transform, S, S.NextPlotCost, null);
                UIKit.Btn(c.transform, "Buy lot", UIKit.Green, () => Result(S.BuyPlot(), Sfx.Build), 24, 180, 62, S.Level >= S.NextPlotLevel);
            }, 900, 520);
        }

        // ================================================================ territory map

        public void OpenTerritory()
        {
            ShowWindow("The Territory", content =>
            {
                var map = UIKit.Panel(content, Palette.Hex(0xE8D3A2), "Map");
                UIKit.Size(map.gameObject, -1, 560);
                var rt = map.rectTransform;
                // Terrain blotches
                var rng = new System.Random(3);
                for (int i = 0; i < 26; i++)
                {
                    var blob = UIKit.Panel(map.transform, i % 3 == 0 ? Palette.Hex(0xC9A86A) : i % 3 == 1 ? Palette.Hex(0xA8B07A) : Palette.Hex(0xD49A6A), "Blob", UIKit.Circle);
                    blob.color = new Color(blob.color.r, blob.color.g, blob.color.b, 0.35f);
                    blob.raycastTarget = false;
                    float s = 60 + (float)rng.NextDouble() * 140;
                    UIKit.Place(blob.gameObject, new Vector2((float)rng.NextDouble(), (float)rng.NextDouble()), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(s, s * 0.7f));
                }
                // Home station
                var home = new Vector2(0.5f, 0.5f);
                foreach (var d in GameDatabase.Destinations)
                {
                    var p = new Vector2(d.MapX, d.MapY);
                    // dotted track
                    for (int k = 1; k < 14; k++)
                    {
                        var dot = UIKit.Panel(map.transform, UIKit.InkSoft, "Dot", UIKit.Circle);
                        dot.raycastTarget = false;
                        UIKit.Place(dot.gameObject, Vector2.Lerp(home, p, k / 14f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(7, 7));
                    }
                }
                foreach (var d in GameDatabase.Destinations)
                {
                    bool locked = !S.IsDestinationUnlocked(d);
                    var pin = UIKit.Node("Pin " + d.Id, map.transform);
                    UIKit.Place(pin, new Vector2(d.MapX, d.MapY), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200, 110));
                    UIKit.VStack(pin, 2, 0, TextAnchor.MiddleCenter);
                    var icon = UIKit.ResIcon(pin.transform, d.ResourceId, 56);
                    if (locked) icon.GetComponent<Image>().color = UIKit.Disabled;
                    var lbl = UIKit.Label(pin.transform, locked ? d.Name + " (Lv " + d.UnlockLevel + ")" : d.Name, 18, locked ? UIKit.InkSoft : UIKit.Ink, TextAnchor.MiddleCenter, true);
                    UIKit.Size(lbl.gameObject, 200, 40);
                    var dd = d;
                    Clickable(pin, () => Toast(dd.Name + ": " + dd.Description + (locked ? " Unlocks at level " + dd.UnlockLevel + "." : " Produces " + GameDatabase.Resource(dd.ResourceId).Name + "."), true));
                }
                var hb = UIKit.Panel(map.transform, UIKit.Red, "Home", UIKit.Circle);
                UIKit.Place(hb.gameObject, home, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46, 46));
                hb.gameObject.AddComponent<Outline>().effectColor = UIKit.Cream;
                var hl = UIKit.Label(map.transform, S.State.townName, 22, UIKit.Red, TextAnchor.MiddleCenter, true, FontStyle.Bold);
                UIKit.Place(hl.gameObject, home, new Vector2(0.5f, 1), new Vector2(0, -26), new Vector2(240, 30));

                // Trains on the road
                foreach (var t in S.State.trains.Where(t => !t.IsIdle))
                {
                    var d = GameDatabase.Destination(t.destinationId);
                    var marker = UIKit.Panel(map.transform, UIKit.Wood, "Train", UIKit.RoundedSmall);
                    marker.gameObject.AddComponent<Outline>().effectColor = UIKit.Brass;
                    var mrt = UIKit.Place(marker.gameObject, home, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 20));
                    int uid = t.uid;
                    Action live = () =>
                    {
                        var tt = S.Train(uid);
                        if (tt == null || tt.IsIdle) return;
                        float p = S.TripProgress(tt);
                        float there = p < 0.5f ? p * 2 : (1 - p) * 2; // out and back
                        var at = Vector2.Lerp(home, new Vector2(d.MapX, d.MapY), there);
                        mrt.anchorMin = mrt.anchorMax = at;
                    };
                    live(); Live(live);
                }
                Line(content, "Tap a pin to learn about an outpost. Send trains from the Trains menu.", 20, UIKit.InkSoft);
            }, 1300, 880);
        }

        // ================================================================ progress & settings

        void OpenProgress()
        {
            ShowWindow("Railroad Ledger", content =>
            {
                var st = S.State.stats;
                Line(content, "Level " + S.Level + " · " + (S.IsGoldenAge ? "Golden Age" : "Frontier era (Golden Age at level " + Balance.GoldenAgeLevel + ")"), 26, UIKit.Ink, true);
                Line(content, "Population " + S.Population + " · Town income x" + (1 + S.CivicBonus(CivicEffect.Income)).ToString("0.00"), 22);
                Line(content, "Trains dispatched: " + st.dispatches, 22);
                Line(content, "Contracts completed: " + st.contractsCompleted, 22);
                Line(content, "Coins earned: " + Fmt.Num(st.coinsEarned), 22);
                Heading(content, "Hauled home");
                foreach (var r in st.collected) Line(content, GameDatabase.Resource(r.id).Name + ": " + Fmt.Num(r.amount), 20, UIKit.InkSoft);
                Heading(content, "Civic bonuses");
                Line(content, "Train speed +" + Mathf.RoundToInt(S.CivicBonus(CivicEffect.TrainSpeed) * 100) + "% · Cargo +" + Mathf.RoundToInt(S.CivicBonus(CivicEffect.Yield) * 100) + "% · XP +" + Mathf.RoundToInt(S.CivicBonus(CivicEffect.Xp) * 100) + "%", 20, UIKit.InkSoft);
            }, 1000, 760);
        }

        void OpenSettings()
        {
            ShowWindow("Settings", content =>
            {
                var set = S.State.settings;
                UIKit.Btn(content, "Music: " + (set.music ? "On" : "Off"), UIKit.WoodLight, () => { set.music = !set.music; AudioManager.MusicOn = set.music; RebuildWindow(); }, 24, -1, 64);
                UIKit.Btn(content, "Sound effects: " + (set.sfx ? "On" : "Off"), UIKit.WoodLight, () => { set.sfx = !set.sfx; AudioManager.SfxOn = set.sfx; RebuildWindow(); }, 24, -1, 64);
                UIKit.Btn(content, "Restore purchases", UIKit.WoodLight, () => Store.Current.Restore(n => Toast(n > 0 ? "Restored " + n + " purchase(s)" : "Nothing to restore", true)), 24, -1, 64);
                UIKit.Btn(content, "Save now", UIKit.Green, () => { GameRoot.Instance.Save(); Toast("Game saved", true); }, 24, -1, 64);
                if (Debug.isDebugBuild)
                {
                    Heading(content, "Developer tools");
                    UIKit.Btn(content, "+5,000 coins, +50 nuggets", UIKit.Blue, () => { S.State.coins += 5000; S.State.nuggets += 50; _dirty = true; }, 22, -1, 56);
                    UIKit.Btn(content, "+100 of every raw resource", UIKit.Blue, () => { foreach (var r in GameDatabase.Resources.Where(x => !x.Manufactured)) S.State.inventory.Change(r.Id, 100); _dirty = true; }, 22, -1, 56);
                    UIKit.Btn(content, "Gain a level", UIKit.Blue, () => { S.AddXp(Balance.XpForNextLevel(S.Level) - S.State.xp); _dirty = true; }, 22, -1, 56);
                    UIKit.Btn(content, "Skip ahead 10 minutes", UIKit.Blue, () => { GameRoot.Instance.Clock.Offset += 600; S.Tick(); _dirty = true; }, 22, -1, 56);
                }
                Heading(content, "Danger zone");
                UIKit.Btn(content, "Start a new railroad (erase save)", UIKit.Red, () => ConfirmReset(), 22, -1, 60);
                Line(content, "Ironhorse Frontier · prototype build", 18, UIKit.InkSoft);
            }, 900, 820);
        }

        void ConfirmReset()
        {
            ShowWindow("Are you sure?", content =>
            {
                Line(content, "This erases all progress and starts over. It cannot be undone.", 24, UIKit.Red, true);
                var row = UIKit.Row(content, 70, 20, TextAnchor.MiddleCenter);
                UIKit.Btn(row.transform, "Keep playing", UIKit.Green, () => CloseWindow(), 24, 300, 64);
                UIKit.Btn(row.transform, "Erase everything", UIKit.Red, () => { CloseWindow(); GameRoot.Instance.ResetGame(); }, 24, 300, 64);
            }, 820, 360);
        }
    }
}
