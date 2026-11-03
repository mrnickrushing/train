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
            var c = color.HasValue ? UIKit.Panel(parent, color.Value, "Card") : UIKit.KitImage(parent, "card_light", "Card", height);
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
            var t = UIKit.Label(parent, text.ToUpperInvariant(), 26, Palette.Hex(0x2B3D5C), TextAnchor.MiddleLeft, false, FontStyle.Bold);
            t.font = UIKit.Display;
            t.fontStyle = FontStyle.Normal;
            UIKit.Size(t.gameObject, -1, 42);
            t.gameObject.AddComponent<Shadow>().effectColor = new Color(1, 1, 1, 0.5f);
        }

        void Tabs(Transform parent, (string label, bool active, Action onClick)[] tabs)
        {
            var row = UIKit.Row(parent, 60, 10, TextAnchor.MiddleCenter);
            foreach (var t in tabs)
                UIKit.Btn(row.transform, t.label, t.active ? UIKit.Brass : UIKit.WoodLight, () => { t.onClick(); RebuildWindow(); }, 24, 220, 62);
        }

        /// <summary>The locomotive's rendered 3D model on a rarity-colored tile (1.5:1).</summary>
        static GameObject LocoSwatch(Transform parent, LocomotiveDef d, float size = 90)
        {
            var bg = UIKit.Panel(parent, UIKit.RarityColor(d.Rarity), "Swatch", UIKit.RoundedGrad);
            UIKit.Size(bg.gameObject, size * 1.5f, size);
            var img = ModelIcons.Image(bg.transform, ModelIcons.Locomotive(d), size * 1.5f, size * 0.94f);
            UIKit.Place(img.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -size * 0.02f), new Vector2(size * 1.5f, size * 0.94f));
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

                // Header: the locomotive on a hero card with its stats as chips.
                var head = UIKit.KitImage(content, d.Rarity >= Rarity.Epic ? "hero_purple" : d.Rarity == Rarity.Rare ? "hero_red" : "hero_blue", "Head", 210);
                UIKit.Size(head.gameObject, -1, 210);
                var hh = UIKit.HStack(head.gameObject, 24, 22, TextAnchor.MiddleLeft);
                hh.padding = new RectOffset(26, 26, 20, 20);
                HudShadow(head.gameObject);
                LocoSwatch(head.transform, d, 140);
                var col = Col(head.transform);
                var name = UIKit.Label(col.transform, d.Name.ToUpperInvariant(), 40, Color.white, TextAnchor.MiddleLeft, false, FontStyle.Bold);
                name.font = UIKit.Display;
                name.fontStyle = FontStyle.Normal;
                UIKit.OneLine(name, 24);
                UIKit.Stroke(name, new Color(0, 0, 0, 0.75f), 2.5f);
                var chips = UIKit.Row(col.transform, 40, 8);
                StatChip(chips.transform, "LEVEL " + t.level + "/" + Balance.MaxLocoLevel);
                StatChip(chips.transform, "PULLS " + d.Power);
                StatChip(chips.transform, "SPEED " + Mathf.RoundToInt(d.Speed * Balance.LocoSpeedMultiplier(t.level)));
                StatChip(chips.transform, "CARGO ×" + Balance.LocoCapacityMultiplier(t.level).ToString("0.00"));
                var blurb = UIKit.Label(col.transform, d.Blurb, 20, new Color(1, 0.95f, 0.85f), TextAnchor.MiddleLeft);
                UIKit.Size(blurb.gameObject, -1, 30);
                blurb.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.6f);

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
                    Heading(content, "Choose a destination");
                    var gridGo = UIKit.Node("Destinations", content);
                    var grid = gridGo.AddComponent<GridLayoutGroup>();
                    grid.cellSize = new Vector2(404, 352);
                    grid.spacing = new Vector2(16, 18);
                    grid.childAlignment = TextAnchor.UpperCenter;
                    grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                    grid.constraintCount = 3;
                    gridGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                    foreach (var dest in GameDatabase.Destinations) DestinationCard(gridGo.transform, t, dest);
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
            }, 1200, 900, OpenFleet);
        }

        /// <summary>
        /// Postcard-style destination: painted scene, name banner, cargo badge and expected haul, trip time and
        /// fuel, and one action (go, get fuel, get the right wagon, or the unlock level).
        /// </summary>
        void DestinationCard(Transform parent, TrainState t, DestinationDef dest)
        {
            var p = S.PreviewDispatch(t, dest);
            bool locked = !S.IsDestinationUnlocked(dest);
            var card = UIKit.KitImage(parent, "card_slate", "Dest " + dest.Id);
            HudShadow(card.gameObject);

            var pic = UIKit.Panel(card.transform, Color.white, "Picture", UIKit.Rounded);
            UIKit.Anchor(pic.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(10, -216), new Vector2(-10, -10));
            pic.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            var tex = Resources.Load<Texture2D>("UI/Destinations/" + dest.Id);
            if (tex != null)
            {
                var raw = UIKit.Node("Art", pic.transform).AddComponent<RawImage>();
                raw.texture = tex;
                raw.raycastTarget = false;
                raw.color = locked ? new Color(0.42f, 0.4f, 0.38f) : Color.white;
                UIKit.Stretch(raw.gameObject);
                var fit = raw.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = tex.width / (float)tex.height;
            }
            var strip = UIKit.Panel(pic.transform, new Color(0.05f, 0.03f, 0.02f, 0.62f), "Banner");
            strip.sprite = null;
            strip.raycastTarget = false;
            UIKit.Anchor(strip.gameObject, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 52));
            var nm = UIKit.Label(strip.transform, dest.Name.ToUpperInvariant(), 28, Color.white, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            nm.font = UIKit.Display;
            nm.fontStyle = FontStyle.Normal;
            UIKit.Anchor(nm.gameObject, Vector2.zero, Vector2.one, new Vector2(14, 0), new Vector2(-10, 0));
            UIKit.OneLine(nm, 16).GetComponent<LayoutElement>().ignoreLayout = true;
            UIKit.Stroke(nm, new Color(0, 0, 0, 0.8f), 1.5f);

            var badge = UIKit.KitImage(pic.transform, "tile", "Cargo", 74);
            UIKit.Place(badge.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(10, -10), new Vector2(74, 74));
            var bi = UIKit.Node("Icon", badge.transform);
            UIKit.Stretch(bi, 8);
            UIKit.HStack(bi, 0, 0, TextAnchor.MiddleCenter);
            if (dest.IsTown && UIKit.Icon(bi.transform, "people", 58) != null) { } else UIKit.ResIcon(bi.transform, dest.ResourceId, 58);
            if (locked)
            {
                var lk = UIKit.Icon(pic.transform, "lock", 96);
                if (lk != null)
                {
                    UIKit.Place(lk, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 34), new Vector2(90, 90));
                    var lv = UIKit.Label(lk.transform, "LV " + dest.UnlockLevel, 28, Color.white, TextAnchor.LowerCenter, false, FontStyle.Bold);
                    lv.font = UIKit.Display; lv.fontStyle = FontStyle.Normal;
                    UIKit.Anchor(lv.gameObject, new Vector2(0, 0), new Vector2(1, 0), new Vector2(-20, -28), new Vector2(20, 10));
                    UIKit.Stroke(lv, new Color(0, 0, 0, 0.85f), 2f);
                }
            }
            else if (p.Amount > 0)
            {
                var haul = UIKit.Label(pic.transform, "+" + p.Amount, 44, UIKit.Gold, TextAnchor.UpperRight, false, FontStyle.Bold);
                haul.font = UIKit.Display;
                haul.fontStyle = FontStyle.Normal;
                UIKit.Anchor(haul.gameObject, new Vector2(0.5f, 1), new Vector2(1, 1), new Vector2(0, -64), new Vector2(-14, -8));
                UIKit.Stroke(haul, new Color(0.22f, 0.1f, 0.02f, 0.95f), 2.5f);
            }

            // Trip facts
            var facts = UIKit.Node("Facts", card.transform);
            UIKit.Anchor(facts, new Vector2(0, 0), new Vector2(1, 0), new Vector2(12, 86), new Vector2(-12, 128));
            UIKit.HStack(facts, 8, 0, TextAnchor.MiddleLeft);
            FactPill(facts.transform, null, Fmt.Time(p.Seconds));
            FactPill(facts.transform, tr => FuelIcon(tr, 30), p.Fuel.ToString());
            var res = GameDatabase.Resource(dest.ResourceId);
            var need = UIKit.Label(facts.transform, dest.IsTown ? "Riders · fare ×" + dest.Yield.ToString("0.0") : "Needs " + Plural(GameSession.CargoWagonName(res.Cargo)), 19, new Color(0.8f, 0.85f, 0.92f), TextAnchor.MiddleLeft);
            UIKit.Size(need.gameObject, -1, 40, 1);

            // Action
            var act = UIKit.Node("Action", card.transform);
            UIKit.Anchor(act, new Vector2(0, 0), new Vector2(1, 0), new Vector2(12, 12), new Vector2(-12, 80));
            UIKit.HStack(act, 0, 0, TextAnchor.MiddleCenter).childForceExpandWidth = true;
            string id = dest.Id;
            if (locked) UIKit.Btn(act.transform, "Unlocks at level " + dest.UnlockLevel, UIKit.Disabled, null, 24, -1, 68, false);
            else if (p.Possible) UIKit.Btn(act.transform, "Go!  +" + p.Amount, UIKit.Green, () => { var r = S.Dispatch(t.uid, id); Result(r, Sfx.Click); if (r.Ok) CloseWindow(); }, 30, -1, 68);
            else if (p.Reason.Contains("fuel")) UIKit.Btn(act.transform, "Get fuel", FuelOrange, () => OpenFuel(), 28, -1, 68);
            else UIKit.Btn(act.transform, p.Reason, UIKit.WoodLight, () => OpenDepot("wagons"), 24, -1, 68);
            foreach (Transform c in act.transform) UIKit.Size(c.gameObject, -1, 68, 1);
        }

        /// <summary>Dark info pill with an optional icon ("1m 24s", fuel 5).</summary>
        static void FactPill(Transform parent, Action<Transform> icon, string text)
        {
            var pill = UIKit.KitImage(parent, "pill", "Fact", 40);
            var h = UIKit.HStack(pill.gameObject, 4, 0, TextAnchor.MiddleCenter);
            h.padding = new RectOffset(icon != null ? 6 : 14, 14, 2, 2);
            pill.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            UIKit.Size(pill.gameObject, -1, 40);
            if (icon != null) icon(pill.transform);
            var t = UIKit.Label(pill.transform, text, 22, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            t.font = UIKit.Display;
            t.fontStyle = FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            UIKit.Size(t.gameObject, -1, 34);
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

        /// <summary>Depot card: big swatch on a rarity backdrop, name plate, stat chips and a buy button.</summary>
        void LocoCard(Transform grid, LocomotiveDef d)
        {
            bool locked = !S.IsLocoUnlocked(d);
            var card = UIKit.Panel(grid, UIKit.RarityColor(d.Rarity), "Loco", UIKit.RoundedGrad, true);
            UIKit.VStack(card.gameObject, 8, 12, TextAnchor.UpperCenter);
            var art = UIKit.Row(card.transform, 170, 0, TextAnchor.MiddleCenter);
            var sw = LocoSwatch(art.transform, d, 170);
            if (locked) Locks.Badge(sw, d.UnlockLevel, 44);
            var plate = UIKit.Panel(card.transform, UIKit.Wood, "Plate", UIKit.Rounded);
            UIKit.Size(plate.gameObject, -1, 60);
            var name = UIKit.Label(plate.transform, d.Name.ToUpperInvariant(), 26, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            name.font = UIKit.Display;
            name.fontStyle = FontStyle.Normal; // the face is already bold
            name.resizeTextForBestFit = true; name.resizeTextMinSize = 14; name.resizeTextMaxSize = 26;
            UIKit.Stretch(name.gameObject, 8);
            var chips = UIKit.Row(card.transform, 44, 8, TextAnchor.MiddleCenter);
            StatChip(chips.transform, "PULLS " + d.Power);
            StatChip(chips.transform, "SPEED " + d.Speed);
            StatChip(chips.transform, d.Rarity.ToString().ToUpperInvariant());
            var blurb = UIKit.Label(card.transform, d.Blurb, 18, new Color(1, 1, 1, 0.9f), TextAnchor.MiddleCenter);
            UIKit.Size(blurb.gameObject, -1, 46);
            BlueprintRow(card.transform, d);
            if (locked)
            {
                var l = UIKit.Label(card.transform, Locks.Text(d.UnlockLevel).ToUpperInvariant(), 24, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
                l.font = UIKit.Display;
                l.fontStyle = FontStyle.Normal; // the face is already bold
                UIKit.Size(l.gameObject, -1, 60);
                card.color = MeshBuilder.Lerp(card.color, UIKit.Disabled, 0.6f);
                return;
            }
            string id = d.Id;
            var buy = UIKit.Btn(card.transform, "", UIKit.Green, () => Result(S.BuyLocomotive(id), Sfx.Bell), 26, -1, 64);
            var label = buy.GetComponentInChildren<Text>();
            label.text = "";
            var row = UIKit.Node("Price", label.transform.parent);
            UIKit.Stretch(row, 6);
            UIKit.HStack(row, 8, 0, TextAnchor.MiddleCenter);
            if (d.PriceNuggets > 0) UIKit.NuggetIcon(row.transform, 34); else UIKit.CoinIcon(row.transform, 34);
            var price = UIKit.Label(row.transform, Fmt.Num(d.PriceNuggets > 0 ? d.PriceNuggets : d.PriceCoins), 28, Color.white, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            price.font = UIKit.Display;
            price.fontStyle = FontStyle.Normal; // the face is already bold
        }

        static void StatChip(Transform parent, string text)
        {
            var chip = UIKit.Panel(parent, new Color(0, 0, 0, 0.35f), "Chip", UIKit.RoundedSmall);
            UIKit.Size(chip.gameObject, 108, 38);
            var t = UIKit.Label(chip.transform, text, 18, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            t.font = UIKit.Display;
            t.fontStyle = FontStyle.Normal; // the face is already bold
            UIKit.Stretch(t.gameObject, 4);
            UIKit.OneLine(t, 12); // "UNCOMMON" shrinks to fit rather than wrapping
        }

        static GameObject WagonSwatch(Transform parent, WagonDef wd)
        {
            var bg = UIKit.Panel(parent, UIKit.ParchmentDark * 0.92f, "Wagon", UIKit.RoundedSmall);
            UIKit.Size(bg.gameObject, 120, 76);
            var img = ModelIcons.Image(bg.transform, ModelIcons.Wagon(wd), 120, 75);
            UIKit.Stretch(img.gameObject);
            return bg.gameObject;
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
                    var grid = UIKit.Node("Locomotives", content);
                    var g = grid.AddComponent<GridLayoutGroup>();
                    g.cellSize = new Vector2(380, 500);
                    g.spacing = new Vector2(20, 20);
                    g.childAlignment = TextAnchor.UpperCenter;
                    grid.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                    foreach (var d in GameDatabase.Locomotives)
                    {
                        if (d.PriceCoins == 0 && d.PriceNuggets == 0) continue;
                        LocoCard(grid.transform, d);
                    }
                }
                else
                {
                    var idle = S.State.trains.Where(t => t.IsIdle && S.WagonsOf(t).Count < GameDatabase.Locomotive(t.defId).Power).ToList();
                    Line(content, idle.Count > 0 ? "New wagons couple to the first idle train with room: " + GameSession.Name(idle[0]) : "New wagons wait in the depot until a train has room.", 20, UIKit.InkSoft);
                    foreach (var d in GameDatabase.Wagons)
                    {
                        bool locked = !S.IsWagonUnlocked(d);
                        var c = Card(content, 96, locked ? new Color(0.74f, 0.77f, 0.82f) : (Color?)null);
                        var ws = WagonSwatch(c.transform, d);
                        if (locked) Locks.Badge(ws, d.UnlockLevel, 30);
                        var col = Col(c.transform);
                        Line(col.transform, d.Name, 24, UIKit.Ink, true);
                        var hauls = GameDatabase.Resources.Where(r => r.Cargo == d.Cargo && !r.Manufactured).Select(r => r.Name);
                        Line(col.transform, "Capacity " + d.Capacity + " · hauls " + string.Join(", ", hauls), 20, UIKit.InkSoft);
                        if (locked) Line(c.transform, Locks.Text(d.UnlockLevel), 22, UIKit.InkSoft, true).GetComponent<LayoutElement>().preferredWidth = 230;
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
                Line(content, "Townsfolk post orders here. Deliver for coins, XP, parts and gold nuggets.", 20, UIKit.InkSoft);
                var grid = UIKit.Node("Orders", content);
                var g = grid.AddComponent<GridLayoutGroup>();
                g.cellSize = new Vector2(370, 610);
                g.spacing = new Vector2(20, 20);
                g.childAlignment = TextAnchor.UpperCenter;
                grid.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                foreach (var c in S.State.contracts.ToList()) OrderCard(grid.transform, c);
                if (S.State.contracts.Count < S.ContractSlots)
                {
                    var t = Line(content, "", 22, UIKit.InkSoft);
                    Action live = () => t.text = "Next order arrives in " + Fmt.Time(S.NextContractIn);
                    live(); Live(live);
                }
                Line(content, "Build a Telegraph Office for an extra contract slot.", 18, UIKit.InkSoft);
            });
        }

        /// <summary>An order as a tall job card: client header, notepad slip of goods, rewards and a Deliver button.</summary>
        void OrderCard(Transform grid, ContractState c)
        {
            bool can = S.CanDeliver(c);
            var card = UIKit.Panel(grid, can ? Palette.Hex(0x5E7F48) : Palette.Hex(0x4A5A6E), "Order", UIKit.RoundedGrad, true);
            UIKit.VStack(card.gameObject, 8, 14, TextAnchor.UpperCenter);
            var head = UIKit.Label(card.transform, c.client, 26, Color.white, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            head.font = UIKit.Display;
            head.fontStyle = FontStyle.Normal; // the face is already bold
            UIKit.Size(head.gameObject, -1, 34);
            head.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.4f);

            // Big icon(s) of what's wanted
            var icons = UIKit.Row(card.transform, 110, -18, TextAnchor.MiddleCenter);
            foreach (var r in c.requirements) UIKit.ResIcon(icons.transform, r.id, c.requirements.Count > 1 ? 86 : 104);

            var slip = UIDeco.Slip(card.transform, 300);
            var deliver = UIKit.Label(slip, "DELIVER", 22, UIKit.InkSoft, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            deliver.font = UIKit.Display;
            deliver.fontStyle = FontStyle.Normal; // the face is already bold
            UIKit.Size(deliver.gameObject, -1, 26);
            foreach (var r in c.requirements)
            {
                int have = S.Amount(r.id);
                var amt = UIKit.Label(slip, r.amount + " " + GameDatabase.Resource(r.id).Name, 28, UIKit.Ink, TextAnchor.MiddleCenter, false, FontStyle.Bold);
                amt.font = UIKit.Display;
                amt.fontStyle = FontStyle.Normal; // the face is already bold
                UIKit.Size(amt.gameObject, -1, 32);
                var bar = UIKit.Bar(slip, have >= r.amount ? UIKit.Green : Palette.Hex(0xC9862E), 14, new Color(0.25f, 0.18f, 0.12f, 0.5f));
                UIKit.SetBar(bar, have / (float)r.amount);
            }
            UIDeco.Divider(slip, "Rewards", UIKit.InkSoft);
            var parts = c.rewardParts != null ? c.rewardParts.ToArray() : null;
            UIDeco.Rewards(slip, S, c.rewardCoins, c.rewardXp, c.rewardNuggets, parts);

            int uid = c.uid;
            UIKit.Btn(card.transform, "Deliver", can ? UIKit.Green : UIKit.Disabled, () => Result(S.DeliverContract(uid), Sfx.Deliver), 28, -1, 70, can);
            var tear = UIKit.Label(card.transform, "Tear up order", 18, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleCenter);
            UIKit.Size(tear.gameObject, -1, 26);
            tear.raycastTarget = true;
            Clickable(tear.gameObject, () => Result(S.DiscardContract(uid), Sfx.Click));
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
                    var c = Card(content, 140, locked ? new Color(0.74f, 0.77f, 0.82f) : (Color?)null);
                    var tex = ModelIcons.Building(d);
                    GameObject pic;
                    if (tex != null)
                    {
                        var bg = UIKit.Panel(c.transform, Palette.Hex(0xCFBB92), "Pic", UIKit.Rounded);
                        UIKit.Size(bg.gameObject, 170, 110);
                        UIKit.Stretch(ModelIcons.Image(bg.transform, tex, 170, 106).gameObject);
                        pic = bg.gameObject;
                    }
                    else pic = BuildingSwatch(c.transform, d);
                    if (locked) Locks.Badge(pic, d.UnlockLevel, 36);
                    var col = Col(c.transform);
                    Line(col.transform, d.Name + "   (" + S.CountBuilt(d.Id) + "/" + d.MaxCount + ")", 26, UIKit.Ink, true);
                    string info = d.Blurb;
                    if (d.Recipe != null)
                        info = string.Join(" + ", d.Recipe.Inputs.Select(i => i.amount + " " + GameDatabase.Resource(i.id).Name)) + "  →  " + d.Recipe.Output.amount + " " + GameDatabase.Resource(d.Recipe.Output.id).Name + "  (" + Fmt.Time(d.Recipe.Seconds) + ")";
                    Line(col.transform, info, 20, UIKit.InkSoft);
                    if (!locked) UIKit.Cost(col.transform, S, d.CostCoins, d.CostResources);
                    if (locked) Line(c.transform, Locks.Text(d.UnlockLevel), 22, UIKit.InkSoft, true).GetComponent<LayoutElement>().preferredWidth = 230;
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

        static GameObject BuildingSwatch(Transform parent, BuildingDef d)
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
            return bg.gameObject;
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
                    if (locked) Locks.Badge(icon, d.UnlockLevel, 28);
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

        static Font _systemFont;

        /// <summary>
        /// Black "Sign in with Apple" button per Apple's guidelines: mixed case and the Apple logo, drawn in the
        /// iOS system font (which carries the logo glyph); elsewhere the game font without the logo.
        /// </summary>
        void AppleSignInButton(Transform parent, Action onClick)
        {
            var b = UIKit.Btn(parent, "Sign in with Apple", Color.black, onClick, 26, -1, 72);
            var t = b.GetComponentInChildren<Text>();
            if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
                if (_systemFont == null) _systemFont = Font.CreateDynamicFontFromOSFont(new[] { "Helvetica Neue", "Helvetica" }, 30);
                t.font = _systemFont;
                t.text = "\uF8FF  Sign in with Apple";
            }
            else t.text = "Sign in with Apple";
            var o = t.GetComponent<Outline>();
            if (o != null) o.enabled = false;
        }

        void OpenSettings()
        {
            ShowWindow("Settings", content =>
            {
                var set = S.State.settings;
                UIKit.Btn(content, "Music: " + (set.music ? "On" : "Off"), UIKit.WoodLight, () => { set.music = !set.music; AudioManager.MusicOn = set.music; RebuildWindow(); }, 24, -1, 64);
                UIKit.Btn(content, "Sound effects: " + (set.sfx ? "On" : "Off"), UIKit.WoodLight, () => { set.sfx = !set.sfx; AudioManager.SfxOn = set.sfx; RebuildWindow(); }, 24, -1, 64);
                UIKit.Btn(content, "Reminders: " + (set.notifications ? "On" : "Off"), UIKit.WoodLight, () => { set.notifications = !set.notifications; if (set.notifications) Reminders.AskOnce(); RebuildWindow(); }, 24, -1, 64);
                Line(content, "Notifications when trains return, fuel is full or goods are ready.", 18, UIKit.InkSoft);

                Heading(content, "Game Center");
                var gc = UIKit.Row(content, 64, 16);
                UIKit.Btn(gc.transform, "Achievements", UIKit.Blue, GameCenter.ShowAchievements, 24, 300, 64);
                UIKit.Btn(gc.transform, "Leaderboards", UIKit.Blue, GameCenter.ShowLeaderboards, 24, 300, 64);
                if (!GameCenter.Supported) Line(content, "Game Center is available on iPhone and iPad.", 18, UIKit.InkSoft);

                Heading(content, "Account");
                if (AppleAccount.SignedIn)
                {
                    Line(content, "Signed in with Apple" + (AppleAccount.DisplayName.Length > 0 ? " as " + AppleAccount.DisplayName : "") + ". Purchases follow your Apple ID to any device.", 20, UIKit.Ink);
                    UIKit.Btn(content, "Sign out", UIKit.WoodLight, () => { AppleAccount.SignOut(); Toast("Signed out", true); RebuildWindow(); }, 24, -1, 64);
                }
                else
                {
                    AppleSignInButton(content, () => AppleAccount.SignIn((ok, err) =>
                    {
                        if (ok) { Toast("Signed in with Apple", true); RebuildWindow(); }
                        else if (err != null) Toast(err, true);
                    }));
                    Line(content, "Optional. Keeps your purchases with your Apple ID on every device.", 18, UIKit.InkSoft);
                }
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
