using System;
using System.Collections;
using System.Linq;
using Ironhorse.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ironhorse.Game
{
    /// <summary>The main game screens as boards: dispatch, fleet, contracts, build, depot and warehouse.</summary>
    public sealed partial class UIRoot
    {
        string _selDest, _depotLoco, _whTab = "raw", _whSel;
        bool _selByUser;
        int _wagonPick; // train whose wagon picker is open on the dispatch board (0 = none)
        int _openBuilding;

        static GameObject Flex(Transform p)
        {
            var go = UIKit.Node("Flex", p);
            UIKit.Size(go, -1, -1, 1, 1);
            return go;
        }

        static string ResName(string id) { return GameDatabase.Resource(id).Name; }

        /// <summary>Where a raw cargo comes from (null for goods and parts).</summary>
        static DestinationDef SourceOf(string resId) { return GameDatabase.Destinations.FirstOrDefault(d => d.ResourceId == resId); }

        static BuildingDef MakerOf(string resId) { return GameDatabase.Buildings.FirstOrDefault(b => b.Recipe != null && b.Recipe.Output.id == resId); }

        /// <summary>A postcard that fits a resource: its source, or the source of its maker's first input.</summary>
        static Texture BackdropFor(string resId)
        {
            var src = SourceOf(resId);
            var maker = MakerOf(resId);
            if (src == null && maker != null) src = SourceOf(maker.Recipe.Inputs[0].id);
            return Postcard(src != null ? src.Id : "blackhollow");
        }

        /// <summary>Opens the dispatch board for an idle train with a destination preselected.</summary>
        void OpenTrainTo(string destId)
        {
            var idle = S.State.trains.Where(t => t.IsIdle && !S.HasArrived(t)).OrderBy(t => t.platform).FirstOrDefault();
            if (idle == null) { Toast("Every train is out on the line. Wait for one to come home.", false); OpenFleet(); return; }
            OpenTrain(idle.uid);
            _selDest = destId;
            _selByUser = true;
            ScrollToDestination(destId);
            RebuildWindow();
        }

        /// <summary>Takes the player to wherever a resource is gathered or made.</summary>
        void GoGet(string resId)
        {
            var res = GameDatabase.Resource(resId);
            if (res.IsPart) { Toast("Workshop parts come from contract rewards and the Foreman's tasks.", true); return; }
            var src = SourceOf(resId);
            if (src != null)
            {
                if (!S.IsDestinationUnlocked(src)) { Toast(src.Name + " opens at level " + src.UnlockLevel + ".", false); return; }
                OpenTrainTo(src.Id);
                return;
            }
            var maker = MakerOf(resId);
            if (maker == null) return;
            var b = S.State.buildings.FirstOrDefault(x => x.defId == maker.Id);
            if (b != null) { OpenBuilding(b.uid); return; }
            _buildTab = BuildingCategory.Production;
            OpenBuildMenu();
            Toast("Build a " + maker.Name + " to make " + res.Name + ".", true);
        }

        // ================================================================ 1 · Send a Train

        public void OpenTrain(int uid)
        {
            var t0 = S.Train(uid);
            if (t0 == null) return;
            _selectedTrain = uid;
            _selDest = null;
            _selByUser = false;
            _wagonPick = 0;
            var view = TrainManager.Instance.View(uid);
            if (view != null && view.IsShownAtStation) CameraRig.Instance.FlyTo(view.LeadPosition, 70);
            ShowBoard("Send a Train", DispatchBoard);
        }

        void DispatchBoard(Transform body, Transform right)
        {
            var t = S.Train(_selectedTrain) ?? S.State.trains.OrderBy(x => x.platform).FirstOrDefault();
            if (t == null) { CloseWindow(); return; }
            _selectedTrain = t.uid;
            var d = GameDatabase.Locomotive(t.defId);
            _boardSub = "Platform " + (t.platform + 1);
            int away = S.State.trains.Count(x => !x.IsIdle);
            Pill(right, Ico("nav_trains", 58), away + " / " + S.State.trains.Count + " on the line", 34);

            var panel = Box(body, BDeep, -1, -1, "Panel");
            UIKit.Stretch(panel.gameObject);
            Row(panel.gameObject, 18, new RectOffset(16, 16, 16, 16)).childForceExpandHeight = true;

            // Your trains
            var colA = VBox(panel.transform, 0, 0, TextAnchor.UpperCenter, 172);
            var list = Scroller(colA.transform, "trains", false, 12);
            foreach (var tr in S.State.trains.OrderBy(x => x.platform)) TrainTile(list, tr, tr.uid == t.uid);
            bool room = S.State.trains.Count < S.FleetCapacity;
            if (room || !S.StationComplete)
            {
                var add = Box(list, new Color(1, 1, 1, 0.06f), 172, 118, "Add");
                add.raycastTarget = true;
                var pl = T(add.transform, "+", 70, BGood, TextAnchor.MiddleCenter, true);
                UIKit.Stretch(pl.gameObject);
                // A full station sends the player to lay the next platform.
                Clickable(add.gameObject, () => { if (room) OpenDepot("locos"); else { OpenFleet(); Toast("Every platform is taken. Lay a new one for another engine.", true); } });
            }

            LocoPanel(panel.transform, t, d);

            var colC = VBox(panel.transform, 14, 0, TextAnchor.UpperLeft, -1, -1, 1);
            if (S.HasArrived(t)) ArrivedPanel(colC.transform, t, d);
            else if (!t.IsIdle) EnRoutePanel(colC.transform, t, d);
            else if (_wagonPick == t.uid) WagonPicker(colC.transform, t, d);
            else DestinationPicker(colC.transform, t, d);
        }

        void TrainTile(Transform p, TrainState tr, bool on)
        {
            var ld = GameDatabase.Locomotive(tr.defId);
            var tile = UIKit.Panel(p, on ? BGold : BTile, "Train", UIKit.RoundedGrad);
            tile.pixelsPerUnitMultiplier = 1.6f;
            UIKit.Size(tile.gameObject, 172, 118);
            var art = LocoArt(tile.transform, ld, 156, 72);
            UIKit.Place(art.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -6), new Vector2(156, 72));
            int uid = tr.uid;
            var tag = T(tile.transform, "", 26, Color.white, TextAnchor.LowerLeft, true);
            UIKit.Anchor(tag.gameObject, Vector2.zero, Vector2.one, new Vector2(10, 4), new Vector2(-6, 0));
            Action live = () =>
            {
                var x = S.Train(uid);
                if (x == null) return;
                bool arrived = S.HasArrived(x);
                tag.text = arrived ? "Arrived!" : x.IsIdle ? "Ready" : Fmt.Time(S.Remaining(x));
                tag.color = arrived || x.IsIdle ? BGood : Color.white;
            };
            live(); Live(live);
            if (!on) Clickable(tile.gameObject, () => { _selectedTrain = uid; _selDest = null; _selByUser = false; _wagonPick = 0; RebuildWindow(); });
        }

        void LocoPanel(Transform p, TrainState t, LocomotiveDef d)
        {
            var card = Box(p, BCard, 540, -1, "Loco");
            Stack(card.gameObject, 14, new RectOffset(24, 24, 20, 22), TextAnchor.UpperCenter);
            T1(card.transform, d.Name, 46, BGold, TextAnchor.MiddleCenter, true);
            Box(card.transform, BLine, -1, 3, "Line");
            var hero = Box(card.transform, BTile, -1, 176, "Hero");
            Row(hero.gameObject, 8, new RectOffset(8, 14, 8, 8));
            LocoArt(hero.transform, d, 290, 150);
            var stats = VBox(hero.transform, 10, 0, TextAnchor.MiddleRight, -1, -1, 1);
            stats.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = false;
            Pill(stats.transform, Ico("nav_depot", 40), "Pulls " + d.Power, 30);
            Pill(stats.transform, null, Mathf.RoundToInt(d.Speed * Balance.LocoSpeedMultiplier(t.level)) + " mph", 30);
            Pill(stats.transform, null, "Level " + t.level, 30);

            var wagons = S.WagonsOf(t);
            T(card.transform, "Wagons  " + wagons.Count + " / " + d.Power, 32, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);
            var slotsHolder = HBox(card.transform, 0, 0, TextAnchor.MiddleLeft, -1, 110);
            var slots = Scroller(slotsHolder.transform, "wagons", true, 12);
            var depot = S.DepotWagons();
            foreach (var w in wagons)
            {
                var wd = GameDatabase.Wagon(w.defId);
                var well = Well(slots, 108, tr => WagonArt(tr, wd, 96, 60));
                int wuid = w.uid;
                if (t.IsIdle && !S.HasArrived(t))
                {
                    well.raycastTarget = true;
                    Clickable(well.gameObject, () => Result(S.DetachWagon(wuid), Sfx.Click));
                }
            }
            for (int i = wagons.Count; i < d.Power; i++)
            {
                var empty = Box(slots, new Color(1, 1, 1, 0.06f), 108, 108, "Empty");
                var plus = T(empty.transform, "+", 56, i == wagons.Count ? BGood : new Color(1, 1, 1, 0.25f), TextAnchor.MiddleCenter, true);
                UIKit.Stretch(plus.gameObject);
                if (i != wagons.Count || !t.IsIdle) continue;
                empty.raycastTarget = true;
                int tuid = t.uid;
                // Opens the wagon picker in place of the destinations: the player chooses which wagon to couple.
                Clickable(empty.gameObject, () => { _wagonPick = tuid; RebuildWindow(); });
                Tag(empty.gameObject, "wagon.add");
            }
            if (t.IsIdle && !S.HasArrived(t) && wagons.Count > 0)
                T(card.transform, "Tap a wagon to uncouple it", 24, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);

            Flex(card.transform);
            var fuel = HBox(card.transform, 10, 0, TextAnchor.MiddleLeft, -1, 54);
            if (S.CrewUnlocked) EngineerChip(fuel.transform, t);
            else UIKit.Size(T(fuel.transform, "Fuel", 32, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading).gameObject, -1, -1, 1);
            Pill(fuel.transform, tr => FuelIcon(tr, 40), Fmt.Num(S.Fuel), 30);
            var btns = HBox(card.transform, 12, 0, TextAnchor.MiddleCenter, -1, 84);
            string defId = t.defId;
            Big(btns.transform, "Workshop", UIKit.WoodLight, () => OpenDepotLoco(defId), 240, 80, true, 34);
            Big(btns.transform, "All trains", UIKit.WoodLight, () => OpenFleet(), 240, 80, true, 34);
        }

        void DestinationPicker(Transform col, TrainState t, LocomotiveDef d)
        {
            var dests = GameDatabase.Destinations.ToList();
            var sel = _selDest != null ? GameDatabase.Destination(_selDest) : null;
            // Keep the player's own pick; otherwise choose the first run this train can actually make.
            if (sel == null || !S.IsDestinationUnlocked(sel) || (!_selByUser && !S.PreviewDispatch(t, sel).Possible))
            {
                sel = dests.FirstOrDefault(x => S.IsDestinationUnlocked(x) && S.PreviewDispatch(t, x).Possible) ?? dests.FirstOrDefault(x => S.IsDestinationUnlocked(x));
                _selDest = sel != null ? sel.Id : null;
                if (sel != null) ScrollToDestination(sel.Id);
            }

            var head = HBox(col, 14, 0, TextAnchor.MiddleLeft, -1, 68);
            T(head.transform, "Choose a destination", 42, Color.white, TextAnchor.MiddleLeft, true);
            Flex(head.transform);
            T(head.transform, dests.Count(x => S.IsDestinationUnlocked(x)) + " of " + dests.Count + " unlocked  ·  swipe for more", 30, BLabel, TextAnchor.MiddleRight, false, UIKit.Heading);

            // One swipeable strip of postcards; the fourth peeks in at the edge so it's clear there's more.
            var grid = HBox(col, 0, 0, TextAnchor.UpperLeft, -1, 430);
            grid.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;
            var strip = Scroller(grid.transform, "dests", true, DestGap);
            foreach (var dd in dests) DestCard(strip, t, dd);
            Flex(col);

            var bar = Box(col, BCard, -1, 124, "Summary");
            Row(bar.gameObject, 18, new RectOffset(18, 14, 12, 12));
            LocoArt(bar.transform, d, 160, 76);
            T(bar.transform, "»", 64, BGold, TextAnchor.MiddleCenter, true);
            var info = VBox(bar.transform, 2, 0, TextAnchor.MiddleLeft, -1, -1, 1);
            if (sel == null) { T1(info.transform, "No destination open yet", 36, Color.white, TextAnchor.MiddleLeft, true); return; }
            var p = S.PreviewDispatch(t, sel);
            T1(info.transform, sel.Name, 38, Color.white, TextAnchor.MiddleLeft, true);
            string cargo = sel.IsTown ? "riders" : ResName(sel.ResourceId);
            string extra = p.Boom ? " (gold strike: double!)" : p.Bandits ? " · bandits on this line!" : "";
            if (p.Combo > 1.001f) extra += " · Full Steam ×" + p.Combo.ToString("0.0#");
            T1(info.transform, p.Possible ? "Back in " + Fmt.Time(p.Seconds) + " with " + p.Amount + " " + cargo + extra : p.Reason, 28, p.Possible ? (p.Bandits ? BBad : p.Boom ? BGold : BLabel) : BBad, TextAnchor.MiddleLeft, false, UIKit.Heading);
            string id = sel.Id;
            int uid = t.uid;
            if (p.Possible)
                Tag(Big(bar.transform, "Send Train", UIKit.Green, () =>
                {
                    var r = S.Dispatch(uid, id);
                    if (!r.Ok) { Result(r, Sfx.Click); return; }
                    AudioManager.Play(Sfx.Click);
                    var sent = S.Train(uid);
                    var dest = GameDatabase.Destination(id);
                    DepartureBanner(GameSession.ShortName(sent) + " departs!", (dest != null ? "for " + dest.Name + " · " : "") + "back in " + Fmt.Time(sent.arrivesAt - S.Now));
                    int dot = r.Message.IndexOf(". ", System.StringComparison.Ordinal);
                    if (dot > 0) Toast(r.Message.Substring(dot + 2), true); // a mystery crate rode along
                    var next = S.State.trains.Where(x => x.IsIdle && !S.HasArrived(x) && S.WagonsOf(x).Count > 0).OrderBy(x => x.platform).FirstOrDefault();
                    if (next == null) { CloseWindow(); return; }
                    _selectedTrain = next.uid;
                    _selDest = null;
                    _selByUser = false;
                    RebuildWindow();
                }, 380, 100, true, 46), "dispatch.send");
            else if (p.Reason != null && p.Reason.Contains("fuel")) Big(bar.transform, "Get fuel", FuelOrange, () => OpenFuel(), 380, 100, true, 44);
            else Big(bar.transform, "Get wagons", UIKit.WoodLight, () => OpenDepot("wagons"), 380, 100, true, 44);
        }

        void DestCard(Transform p, TrainState t, DestinationDef dest)
        {
            bool locked = !S.IsDestinationUnlocked(dest);
            bool sel = dest.Id == _selDest;
            var pv = S.PreviewDispatch(t, dest);
            var card = Box(p, BCard, DestWidth, -1, "Dest " + dest.Id);
            Tag(card.gameObject, "dest." + dest.Id);
            Stack(card.gameObject, 10, new RectOffset(0, 0, 0, 14), TextAnchor.UpperCenter);
            if (sel)
            {
                var ring = card.gameObject.AddComponent<Outline>();
                ring.effectColor = BGold;
                ring.effectDistance = new Vector2(5, -5);
            }
            var ph = Photo(card.transform, Postcard(dest.Id), -1, 270, locked ? new Color(0.42f, 0.42f, 0.45f) : (Color?)null);
            Overlay(ph.transform, Fade(true), new Color(0.08f, 0.1f, 0.15f, 0.95f));
            var nm = T(ph.transform, dest.Name, 36, Color.white, TextAnchor.LowerLeft, true);
            UIKit.Anchor(nm.gameObject, Vector2.zero, Vector2.one, new Vector2(14, 10), new Vector2(-14, -10));
            string resId = dest.IsTown ? "people" : dest.ResourceId;
            string cargo = dest.IsTown ? "Riders" : ResName(dest.ResourceId);
            var chip = Pill(ph.transform, Ico(resId, 44), cargo, 26, null, new Color(0.055f, 0.075f, 0.11f, 0.82f));
            Float(chip, new Vector2(0, 1), new Vector2(10, -10));
            if (!locked && (pv.Boom || pv.Bandits))
            {
                var tag = Pill(ph.transform, Ico(pv.Boom ? "gold_ore" : "bandit", 44), pv.Boom ? "GOLD STRIKE ×2" : "BANDITS!", 26, pv.Boom ? BGold : Color.white,
                    pv.Boom ? new Color(0.24f, 0.16f, 0.02f, 0.92f) : new Color(0.5f, 0.1f, 0.06f, 0.92f));
                Float(tag, new Vector2(0, 1), new Vector2(10, -64));
            }
            if (sel && !locked) Float(CheckBadge(ph.transform, 54), new Vector2(1, 1), new Vector2(-10, -10));
            if (locked)
            {
                var lk = UIKit.Icon(ph.transform, "lock", 92);
                if (lk != null) UIKit.Place(lk, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(92, 92));
            }

            var r1 = HBox(card.transform, 8, 0, TextAnchor.MiddleLeft, -1, 46);
            r1.GetComponent<HorizontalLayoutGroup>().padding = new RectOffset(16, 16, 0, 0);
            if (locked)
            {
                T1(r1.transform, "Unlocks at level " + dest.UnlockLevel, 30, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);
                return;
            }
            UIKit.Size(T(r1.transform, "Haul", 28, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading).gameObject, -1, -1, 1);
            T(r1.transform, pv.Amount > 0 ? "+" + pv.Amount + " " + cargo : "No wagon", 32, pv.Amount > 0 ? BGood : BBad, TextAnchor.MiddleRight, true);
            var r2 = HBox(card.transform, 8, 0, TextAnchor.MiddleCenter, -1, 50);
            Pill(r2.transform, tr => ClockIcon(tr, 32), Fmt.Time(pv.Seconds), 26);
            Pill(r2.transform, Ico("xp", 30), "+" + pv.Xp, 26);
            Pill(r2.transform, tr => FuelIcon(tr, 32), pv.Fuel.ToString(), 26);
            card.raycastTarget = true;
            string id = dest.Id;
            if (!sel) Clickable(card.gameObject, () => { _selDest = id; _selByUser = true; RebuildWindow(); });
        }

        // ================================================================ Wagon picker

        /// <summary>Choose the wagon to couple: every type, what it hauls, how many wait in the depot, couple or buy.</summary>
        void WagonPicker(Transform col, TrainState t, LocomotiveDef d)
        {
            int have = S.WagonsOf(t).Count;
            var head = HBox(col, 14, 0, TextAnchor.MiddleLeft, -1, 68);
            T(head.transform, "Couple a wagon", 42, Color.white, TextAnchor.MiddleLeft, true);
            Flex(head.transform);
            T(head.transform, have + " / " + d.Power + " wagons", 30, BLabel, TextAnchor.MiddleRight, false, UIKit.Heading);
            Big(head.transform, "Done", BBlue, () => { _wagonPick = 0; RebuildWindow(); }, 170, 64, true, 34);

            var grid = HBox(col, 0, 0, TextAnchor.UpperLeft, -1, 470);
            grid.GetComponent<HorizontalLayoutGroup>().childForceExpandHeight = true;
            var strip = Scroller(grid.transform, "wagonpick", true, DestGap);
            // Usable wagons first (the ones for the chosen destination leading), locked ones last.
            var want = _selDest != null ? GameDatabase.Destination(_selDest) : null;
            var cargo = want != null ? GameDatabase.Resource(want.ResourceId).Cargo : (CargoClass?)null;
            foreach (var wd in GameDatabase.Wagons.OrderBy(w => S.IsWagonUnlocked(w) ? 0 : 1).ThenBy(w => cargo.HasValue && w.Cargo == cargo.Value ? 0 : 1).ThenBy(w => w.UnlockLevel))
                WagonPickCard(strip, t, d, wd, want);
            Flex(col);
            var tip = Box(col, BCard, -1, 70, "Tip");
            Row(tip.gameObject, 12, new RectOffset(20, 20, 0, 0));
            T1(tip.transform, have >= d.Power ? d.Name.Split(' ')[0] + " can't pull more. Tap a wagon on the train to uncouple it." : "Tap a wagon on the train to uncouple it and send it to the depot.", 28, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);
        }

        void WagonPickCard(Transform p, TrainState t, LocomotiveDef d, WagonDef wd, DestinationDef want)
        {
            bool locked = !S.IsWagonUnlocked(wd), full = S.WagonsOf(t).Count >= d.Power;
            var inDepot = S.DepotWagons().Where(w => w.defId == wd.Id).ToList();
            bool fits = want != null && GameDatabase.Resource(want.ResourceId).Cargo == wd.Cargo;
            var card = Box(p, fits ? Palette.Hex(0x3B3426) : BCard, 300, -1, "Wagon " + wd.Id);
            if (fits) { var o = card.gameObject.AddComponent<Outline>(); o.effectColor = BGold; o.effectDistance = new Vector2(4, -4); }
            Stack(card.gameObject, 8, new RectOffset(12, 12, 12, 14), TextAnchor.UpperCenter);
            var art = Box(card.transform, BTile, -1, 140, "Art");
            UIKit.HStack(art.gameObject, 0, 0, TextAnchor.MiddleCenter);
            var g = WagonArt(art.transform, wd, 260, 120);
            if (locked) g.color = new Color(0, 0, 0, 0.6f);
            T1(card.transform, wd.Name, 32, fits ? BGold : Color.white, TextAnchor.MiddleCenter, true);
            // What it carries, as icons and names.
            var goods = GameDatabase.Resources.Where(r => r.Cargo == wd.Cargo && !r.IsPart).ToList();
            var icons = HBox(card.transform, 4, 0, TextAnchor.MiddleCenter, -1, 44);
            foreach (var r in goods.Take(4)) { var id = r.IsPassenger ? "people" : r.Id; Ico(id, 40)(icons.transform); }
            T1(card.transform, string.Join(", ", goods.Take(3).Select(r => r.Name)), 22, BLabel, TextAnchor.MiddleCenter, false, UIKit.Heading);
            var stats = HBox(card.transform, 8, 0, TextAnchor.MiddleCenter, -1, 44);
            Pill(stats.transform, null, "Holds " + wd.Capacity, 24);
            int own = S.WagonsOwned(wd.Id);
            if (own > 0) Pill(stats.transform, null, "Own " + own, 24, BGood);
            if (inDepot.Count > 0) Pill(stats.transform, null, inDepot.Count + " spare", 24, BGood);
            if (fits) T1(card.transform, "Right for " + want.Name.Split(' ')[0], 22, BGold, TextAnchor.MiddleCenter, false, UIKit.Heading);
            Flex(card.transform);
            int tuid = t.uid;
            string id2 = wd.Id;
            Action after = () => { if (S.WagonsOf(S.Train(tuid)).Count >= d.Power) _wagonPick = 0; }; // a full train goes back to the destinations
            if (locked) T1(card.transform, "Unlocks at level " + wd.UnlockLevel, 26, BLabel, TextAnchor.MiddleCenter, false, UIKit.Heading);
            else if (inDepot.Count > 0)
            {
                int wuid = inDepot[0].uid;
                Tag(Big(card.transform, "Couple", UIKit.Green, () => { var r = S.AttachWagon(wuid, tuid); Result(r, Sfx.Click); if (r.Ok) after(); }, 260, 80, !full, 36), "wagon.couple." + wd.Id);
            }
            else Tag(PriceBtn(card.transform, "Buy", UIKit.Green, tr => UIKit.CoinIcon(tr, 40), Fmt.Num(wd.PriceCoins), () => { var r = S.BuyWagon(id2, tuid); Result(r, Sfx.Coin); if (r.Ok) after(); }, 260, 80, !full && S.Coins >= wd.PriceCoins, 32), "wagon.buy." + wd.Id);
        }

        const float DestWidth = 330, DestGap = 18;

        /// <summary>Scrolls the destination strip so a destination's card is in view.</summary>
        void ScrollToDestination(string destId)
        {
            var list = GameDatabase.Destinations;
            int i = Mathf.Max(0, list.FindIndex(d => d.Id == destId));
            float content = list.Count * (DestWidth + DestGap) - DestGap, view = 3 * (DestWidth + DestGap);
            _scrollMemo["dests"] = new Vector2(Mathf.Clamp01((i - 1) * (DestWidth + DestGap) / Mathf.Max(1, content - view)), 0);
        }

        /// <summary>Takes a free-floating widget out of layout and pins it to a corner of its parent.</summary>
        static void Float(GameObject go, Vector2 corner, Vector2 offset)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>(); // not ??: Unity's fake-null
            le.ignoreLayout = true;
            var fit = go.GetComponent<ContentSizeFitter>();
            if (fit == null) fit = go.AddComponent<ContentSizeFitter>();
            if (go.GetComponent<LayoutGroup>() != null)
            {
                fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            var rt = UIKit.RT(go);
            rt.anchorMin = rt.anchorMax = corner;
            rt.pivot = corner;
            rt.anchoredPosition = offset;
            if (le.preferredWidth > 0) rt.sizeDelta = new Vector2(le.preferredWidth, le.preferredHeight > 0 ? le.preferredHeight : le.minHeight);
        }

        void EnRoutePanel(Transform col, TrainState t, LocomotiveDef d)
        {
            var dest = GameDatabase.Destination(t.destinationId);
            T1(col, "On the way to " + dest.Name, 42, Color.white, TextAnchor.MiddleLeft, true);
            var ph = Photo(col, Postcard(dest.Id), -1, 400);
            Overlay(ph.transform, Fade(true), new Color(0.08f, 0.1f, 0.15f, 0.9f));
            var art = LocoArt(ph.transform, d, 520, 190);
            UIKit.Place(art.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(520, 190));
            Flex(col);
            var bar = Box(col, BCard, -1, 124, "Trip");
            Row(bar.gameObject, 16, new RectOffset(22, 14, 12, 12));
            ClockIcon(bar.transform, 58);
            var track = HBox(bar.transform, 0, 0, TextAnchor.MiddleLeft, -1, 44, 1);
            var fill = Track(track.transform, 40, BGold);
            var left = T(fill.transform.parent, "", 30, Color.white, TextAnchor.MiddleRight, true);
            UIKit.Anchor(left.gameObject, Vector2.zero, Vector2.one, new Vector2(10, 0), new Vector2(-14, 0));
            int uid = t.uid;
            Action live = () =>
            {
                var x = S.Train(uid);
                if (x == null || x.IsIdle) return;
                UIKit.SetBar(fill, S.TripProgress(x));
                left.text = Fmt.Time(S.Remaining(x));
            };
            live(); Live(live);
            PriceBtn(bar.transform, "Speed up", BBlue, tr => UIKit.NuggetIcon(tr, 50), S.TrainRushCost(t).ToString(), () => Result(S.RushTrain(uid)), 360, 100, S.Nuggets >= S.TrainRushCost(t));
            if (S.AdsLeft(AdReward.RushTrain) > 0 && Ads.Current.Available)
                Big(bar.transform, "Ad: -30m", UIKit.Green, () => Ads.Watch(AdReward.RushTrain, uid, r => Result(r, Sfx.Bell)), 210, 100, true, 34);
        }

        void ArrivedPanel(Transform col, TrainState t, LocomotiveDef d)
        {
            var dest = GameDatabase.Destination(t.destinationId);
            T1(col, "Home from " + dest.Name + "!", 42, Color.white, TextAnchor.MiddleLeft, true);
            var ph = Photo(col, Postcard(dest.Id), -1, 400);
            Overlay(ph.transform, Fade(true), new Color(0.08f, 0.1f, 0.15f, 0.9f));
            var art = LocoArt(ph.transform, d, 520, 190);
            UIKit.Place(art.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(520, 190));
            Flex(col);
            var bar = Box(col, BCard, -1, 124, "Cargo");
            Row(bar.gameObject, 16, new RectOffset(22, 14, 12, 12));
            string res = dest.IsTown ? "people" : t.cargoId;
            Pill(bar.transform, Ico(res, 64), "+" + t.cargoAmount + " " + (dest.IsTown ? "riders" : ResName(t.cargoId)), 40);
            Flex(bar.transform);
            int uid = t.uid;
            Tag(Big(bar.transform, "Unload", UIKit.Green, () => Result(S.Unload(uid)), 380, 100, true, 46), "dispatch.unload");
        }

        // ================================================================ 2 · Trains on the Line

        public void OpenFleet() { ShowBoard("Trains on the Line", FleetBoard); }

        void FleetBoard(Transform body, Transform right)
        {
            var trains = S.State.trains.OrderBy(t => S.HasArrived(t) ? 0 : t.IsIdle ? 2 : 1).ThenBy(t => t.platform).ToList();
            var cap = Box(right, BChip, 440, 66, "Capacity");
            Row(cap.gameObject, 6, new RectOffset(0, 16, 0, 0));
            UIKit.Icon(cap.transform, "nav_trains", 70);
            var tr = HBox(cap.transform, 0, 0, TextAnchor.MiddleLeft, -1, 40, 1);
            var fill = Track(tr.transform, 38, BGold);
            UIKit.SetBar(fill, trains.Count(t => !t.IsIdle) / (float)Mathf.Max(1, S.FleetCapacity));
            var ct = T(fill.transform.parent, trains.Count(t => !t.IsIdle) + " / " + S.FleetCapacity + " on the line", 30, Color.white, TextAnchor.MiddleCenter, true);
            UIKit.Stretch(ct.gameObject);

            var panel = Box(body, BTile, -1, -1, "Panel");
            UIKit.Stretch(panel.gameObject);
            Stack(panel.gameObject, 0, new RectOffset(12, 12, 12, 12));
            var list = Scroller(panel.transform, "fleet", false, 12);
            foreach (var t in trains) FleetRow(list, t);
            for (int i = trains.Count; i < S.FleetCapacity; i++) FreeRow(list, i);
            if (!S.StationComplete) PlatformRow(list);
        }

        void FleetRow(Transform p, TrainState t)
        {
            var d = GameDatabase.Locomotive(t.defId);
            bool arrived = S.HasArrived(t), idle = t.IsIdle && !arrived;
            int uid = t.uid;
            var row = UIKit.Panel(p, arrived ? BGold : BCardDeep, "Train", arrived ? UIKit.RoundedGrad : UIKit.Rounded);
            row.pixelsPerUnitMultiplier = 1.6f;
            UIKit.Size(row.gameObject, -1, 150);
            Row(row.gameObject, 22, new RectOffset(0, 18, 0, 0));
            Clickable(row.gameObject, () => OpenTrain(uid));
            row.GetComponent<ButtonFx>().enabled = false;

            var zone = UIKit.Node("Zone", row.transform);
            UIKit.Size(zone, 380, 150);
            if (!arrived) Overlay(zone.transform, Fade(false), idle ? BTile : Palette.Hex(0x5D7A58));
            var art = LocoArt(zone.transform, d, 330, 124);
            UIKit.Place(art.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(330, 124));

            var nc = VBox(row.transform, 6, 0, TextAnchor.MiddleLeft, 460);
            T1(nc.transform, GameSession.Name(t) + " · Lv " + t.level, 28, arrived ? Palette.Hex(0x4A3306) : BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);
            var mid = HBox(row.transform, 10, 0, TextAnchor.MiddleCenter, -1, -1, 1);
            var rc = VBox(row.transform, 10, 0, TextAnchor.MiddleCenter, 420);
            rc.GetComponent<VerticalLayoutGroup>().childForceExpandWidth = false;

            if (arrived)
            {
                T1(nc.transform, "Arrived at the station!", 44, Color.white, TextAnchor.MiddleLeft, true);
                var dest = GameDatabase.Destination(t.destinationId);
                Pill(mid.transform, Ico(dest.IsTown ? "people" : t.cargoId, 56), "+" + t.cargoAmount, 38);
                Tag(Big(rc.transform, "Collect", UIKit.Green, () => Result(S.Unload(uid)), 400, 100, true, 46), "fleet.collect." + uid);
            }
            else if (idle)
            {
                T1(nc.transform, "Waiting at platform " + (t.platform + 1), 40, Color.white, TextAnchor.MiddleLeft, true);
                Pill(mid.transform, null, S.WagonsOf(t).Count + " / " + d.Power + " wagons", 30);
                Tag(Big(rc.transform, "Send", UIKit.Green, () => OpenTrain(uid), 400, 100, true, 46), "fleet.send." + uid);
            }
            else
            {
                var dest = GameDatabase.Destination(t.destinationId);
                var pin = HBox(nc.transform, 12, 0, TextAnchor.MiddleLeft, -1, 60);
                var pb = Box(pin.transform, UIKit.WoodLight, 58, 58, "Pin");
                var pi = UIKit.Icon(pb.transform, "nav_territory", 50);
                if (pi != null) UIKit.Place(pi, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(50, 50));
                T1(pin.transform, dest.Name, 38, Color.white, TextAnchor.MiddleLeft, true);
                var route = Box(mid.transform, Palette.Hex(0x2F384B), -1, 96, "Route");
                Row(route.gameObject, 8, new RectOffset(12, 12, 8, 8), TextAnchor.MiddleCenter);
                if (dest.IsTown) UIKit.Icon(route.transform, "people", 66); else UIKit.ResIcon(route.transform, t.cargoId, 58);
                T(route.transform, "»", 56, Color.white, TextAnchor.MiddleCenter, true);
                var pc = Photo(route.transform, Postcard(dest.Id), 80, 80);
                pc.sprite = UIKit.Circle;
                pc.type = Image.Type.Simple;
                pc.pixelsPerUnitMultiplier = 1;

                var trk = HBox(rc.transform, 8, 0, TextAnchor.MiddleLeft, 400, 40);
                ClockIcon(trk.transform, 40);
                var fill = Track(trk.transform, 32, BGold);
                var left = T(fill.transform.parent, "", 26, Color.white, TextAnchor.MiddleRight, true);
                UIKit.Anchor(left.gameObject, Vector2.zero, Vector2.one, new Vector2(8, 0), new Vector2(-10, 0));
                Action live = () =>
                {
                    var x = S.Train(uid);
                    if (x == null || x.IsIdle) return;
                    UIKit.SetBar(fill, S.TripProgress(x));
                    left.text = Fmt.Time(S.Remaining(x));
                };
                live(); Live(live);
                int cost = S.TrainRushCost(t);
                PriceBtn(rc.transform, "Speed up", BBlue, tr => UIKit.NuggetIcon(tr, 44), cost.ToString(), () => Result(S.RushTrain(uid)), 400, 82, S.Nuggets >= cost, 36);
            }
        }

        void FreeRow(Transform p, int index)
        {
            var row = Box(p, new Color(1, 1, 1, 0.05f), -1, 150, "Free");
            var o = row.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.56f, 0.63f, 0.73f, 0.7f);
            o.effectDistance = new Vector2(3, -3);
            Row(row.gameObject, 22, new RectOffset(40, 18, 0, 0));
            var col = VBox(row.transform, 4, 0, TextAnchor.MiddleLeft, -1, -1, 1);
            T1(col.transform, "Platform " + (index + 1) + " is free", 42, Color.white, TextAnchor.MiddleLeft, true);
            T1(col.transform, "A new locomotive could be earning here", 28, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);
            Big(row.transform, "Buy a locomotive", UIKit.Green, () => OpenDepot("locos"), 420, 96, true, 40);
        }

        /// <summary>The next platform: what laying it takes, or the track crew's progress.</summary>
        void PlatformRow(Transform p)
        {
            int n = S.PlatformsBuilt + 1;
            var row = Box(p, Palette.Hex(0x3B3324), -1, 150, "NewPlatform");
            var o = row.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(BGold.r, BGold.g, BGold.b, 0.6f);
            o.effectDistance = new Vector2(3, -3);
            Tag(row.gameObject, "platform.row");
            Row(row.gameObject, 18, new RectOffset(40, 18, 0, 0));
            var col = VBox(row.transform, 6, 0, TextAnchor.MiddleLeft, -1, -1, 1);
            if (S.LayingPlatform)
            {
                T1(col.transform, "Laying platform " + n, 42, Color.white, TextAnchor.MiddleLeft, true);
                var trk = HBox(col.transform, 8, 0, TextAnchor.MiddleLeft, 560, 40);
                ClockIcon(trk.transform, 40);
                var fill = Track(trk.transform, 32, BGold);
                var left = T(fill.transform.parent, "", 26, Color.white, TextAnchor.MiddleRight, true);
                UIKit.Anchor(left.gameObject, Vector2.zero, Vector2.one, new Vector2(8, 0), new Vector2(-10, 0));
                Action live = () => { UIKit.SetBar(fill, S.PlatformProgress); left.text = Fmt.Time(S.PlatformRemaining); };
                live(); Live(live);
                int cost = S.PlatformRushCost;
                PriceBtn(row.transform, "Speed up", BBlue, tr => UIKit.NuggetIcon(tr, 44), cost.ToString(), () => Result(S.RushPlatform(), Sfx.Build), 400, 96, S.Nuggets >= cost, 36);
                return;
            }
            bool locked = S.Level < S.NextPlatformLevel;
            T1(col.transform, "Lay platform " + n, 42, Color.white, TextAnchor.MiddleLeft, true);
            T1(col.transform, locked ? "Room for one more locomotive · opens at level " + S.NextPlatformLevel
                : "Room for one more locomotive · the crew needs " + Fmt.Time(S.NextPlatformSeconds), 28, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);
            foreach (var r in S.NextPlatformResources)
            {
                string id = r.id;
                bool have = S.Amount(id) >= r.amount;
                var well = Well(row.transform, 104, tr => UIKit.ResIcon(tr, id, 62), S.Amount(id) + "/" + r.amount, null, have ? Color.white : BBad);
                if (!have) { well.raycastTarget = true; Clickable(well.gameObject, () => GoGet(id)); }
            }
            int coins = S.NextPlatformCoins;
            Pill(row.transform, tr => UIKit.CoinIcon(tr, 44), Fmt.Num(coins), 34, S.Coins >= coins ? Color.white : BBad);
            bool can = S.CanLayPlatform().Ok;
            Tag(Big(row.transform, locked ? Locks.Text(S.NextPlatformLevel) : "Lay track", can ? UIKit.Green : UIKit.Disabled,
                () => Result(S.LayPlatform(), Sfx.Build), 300, 96, true, locked ? 30 : 40), "platform.lay");
        }

        // ================================================================ 3 · Contracts

        public void OpenContracts() { ShowBoard("Contracts", ContractsBoard); }

        void ContractsBoard(Transform body, Transform right)
        {
            var row = UIKit.Node("Row", body);
            UIKit.Stretch(row);
            Row(row, 12, new RectOffset(0, 0, 0, 0)).childForceExpandHeight = true;
            int ready = S.State.contracts.Count(c => S.CanDeliver(c));
            SideTabs(row.transform, 220, 200, new (string, Action<Transform>, bool, Action)[]
            {
                ("Orders", Ico("nav_contracts", 110), _ordersTab != "projects", () => _ordersTab = "orders"),
                ("Projects", Ico("nav_territory", 110), _ordersTab == "projects", () => _ordersTab = "projects"),
            });
            var panel = Box(row.transform, BTile, -1, -1, "Panel");
            UIKit.Size(panel.gameObject, -1, -1, 1);
            Stack(panel.gameObject, 0, new RectOffset(14, 14, 14, 14));

            if (_ordersTab == "projects")
            {
                _boardSub = "Territory projects";
                Pill(right, null, "Deliver any amount · each stage pays out", 30);
                var pcards = Scroller(panel.transform, "projects", true, 16);
                foreach (var def in DepthContent.Projects) ProjectCard(pcards, def);
                return;
            }

            _boardSub = ready > 0 ? ready + " ready to deliver" : "Town orders";
            if (S.RegularContracts < S.ContractSlots)
            {
                var timer = Pill(right, tr => ClockIcon(tr, 40), "", 30);
                var tt = timer.GetComponentInChildren<Text>();
                Action live = () => tt.text = "New order in " + Fmt.Time(S.NextContractIn);
                live(); Live(live);
            }
            var cards = Scroller(panel.transform, "orders", true, 16);
            foreach (var c in S.State.contracts.OrderBy(x => x.express ? 0 : 1).ToList()) OrderCard(cards, c);
            for (int i = S.RegularContracts; i < S.ContractSlots; i++) EmptyOrder(cards);
            if (S.CountBuilt("telegraph") == 0) TelegraphHint(cards);
        }

        void OrderCard(Transform p, ContractState c)
        {
            bool can = S.CanDeliver(c);
            int uid = c.uid;
            var card = Box(p, c.express ? Palette.Hex(0x2E2420) : BCardDeep, 540, -1, "Order");
            Stack(card.gameObject, 10, new RectOffset(16, 16, 16, 16), TextAnchor.UpperLeft);
            if (c.express)
            {
                var ring = card.gameObject.AddComponent<Outline>();
                ring.effectColor = BGold;
                ring.effectDistance = new Vector2(4, -4);
                TelegramHead(card.transform, c);
            }
            else
            {
                var head = Photo(card.transform, BackdropFor(c.requirements[0].id), -1, 140, new Color(0.7f, 0.72f, 0.78f));
                Overlay(head.transform, Fade(false), new Color(0.12f, 0.15f, 0.22f, 0.97f));
                var kind = T(head.transform, "Town order", 26, BLabel, TextAnchor.UpperLeft, false, UIKit.Heading);
                UIKit.Anchor(kind.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -46), new Vector2(-150, -12));
                var who = T(head.transform, c.client, 40, BGold, TextAnchor.UpperLeft, true);
                UIKit.Anchor(who.gameObject, new Vector2(0, 0), new Vector2(1, 1), new Vector2(20, 8), new Vector2(-150, -50));
                var big = UIKit.ResIcon(head.transform, c.requirements[0].id, 104);
                UIKit.Place(big, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-14, 0), new Vector2(118, 118));
            }

            float h = c.requirements.Count == 1 ? 130 : c.requirements.Count == 2 ? 96 : 76;
            foreach (var r in c.requirements) ReqRow(card.transform, r, h);
            Flex(card.transform);
            Box(card.transform, BLine, -1, 3, "Line");
            T(card.transform, "Rewards", 32, BGold, TextAnchor.MiddleLeft, false, UIKit.Heading);
            var rw = HBox(card.transform, 12, 0, TextAnchor.MiddleCenter, -1, 100);
            rw.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            if (c.rewardCoins > 0) RewardCell(rw.transform, tr => UIKit.CoinIcon(tr, 60), Fmt.Num(c.rewardCoins));
            if (c.rewardXp > 0) RewardCell(rw.transform, tr => UIDeco.XpIcon(tr, 60), c.rewardXp.ToString());
            if (c.rewardNuggets > 0) RewardCell(rw.transform, tr => UIKit.NuggetIcon(tr, 60), c.rewardNuggets.ToString());
            if (c.rewardParts != null) foreach (var rp in c.rewardParts) { var id = rp.id; RewardCell(rw.transform, tr => UIKit.ResIcon(tr, id, 58), rp.amount.ToString()); }

            var btns = HBox(card.transform, 12, 0, TextAnchor.MiddleCenter, -1, 84);
            if (can) Tag(Big(btns.transform, "Deliver", UIKit.Green, () => Result(S.DeliverContract(uid), Sfx.Deliver), 340, 82, true, 44), "contract.deliver");
            else
            {
                string mid = c.requirements.Where(r => S.Amount(r.id) < r.amount).Select(r => r.id).FirstOrDefault() ?? c.requirements[0].id;
                Big(btns.transform, "Go", BBlue, () => GoGet(mid), 340, 82, true, 44);
            }
            Big(btns.transform, c.express ? "Pass" : "Skip", UIKit.WoodLight, () => Result(S.DiscardContract(uid), Sfx.Click), 146, 82, true, 32);
        }

        void ReqRow(Transform p, ResourceAmount r, float h)
        {
            int have = S.Amount(r.id);
            bool ok = have >= r.amount;
            var box = Box(p, UIKit.Parchment, -1, h, "Need");
            Row(box.gameObject, 14, new RectOffset(8, 16, 8, 8));
            var well = Box(box.transform, Color.white, h - 16, h - 16, "Well");
            UIKit.HStack(well.gameObject, 0, 0, TextAnchor.MiddleCenter);
            UIKit.ResIcon(well.transform, r.id, (h - 16) * 0.7f);
            if (ok) Float(CheckBadge(well.transform, 34), new Vector2(1, 0), new Vector2(4, -4));
            var col = VBox(box.transform, 4, 0, TextAnchor.MiddleLeft, -1, -1, 1);
            T1(col.transform, ResName(r.id), 26, UIKit.InkSoft, TextAnchor.MiddleLeft, false, UIKit.Heading);
            var line = HBox(col.transform, 0, 0, TextAnchor.MiddleLeft, -1, 40);
            UIKit.Size(T(line.transform, "Deliver", 34, UIKit.Ink).gameObject, -1, -1, 1);
            T(line.transform, Fmt.Num(have) + " / " + Fmt.Num(r.amount), 34, ok ? UIKit.Green : UIKit.Ink, TextAnchor.MiddleRight);
            UIKit.SetBar(Track(col.transform, 16, ok ? BGreen : BGold), have / (float)r.amount);
        }

        static void RewardCell(Transform p, Action<Transform> icon, string amount)
        {
            var col = VBox(p, 4, 0, TextAnchor.MiddleCenter, -1, 100, 1);
            var ic = HBox(col.transform, 0, 0, TextAnchor.MiddleCenter, -1, 56);
            icon(ic.transform);
            var plate = Box(col.transform, BChip, -1, 40, "Plate");
            var t = T(plate.transform, amount, 32, Color.white, TextAnchor.MiddleCenter, true);
            UIKit.Stretch(t.gameObject);
        }

        void EmptyOrder(Transform p)
        {
            var card = Box(p, new Color(1, 1, 1, 0.05f), 420, -1, "Empty");
            var o = card.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.56f, 0.63f, 0.73f, 0.6f);
            o.effectDistance = new Vector2(3, -3);
            Stack(card.gameObject, 14, new RectOffset(24, 24, 24, 24), TextAnchor.MiddleCenter);
            ClockIcon(card.transform, 96);
            T(card.transform, "A new order is on its way", 36, Color.white, TextAnchor.MiddleCenter, true);
            var t = T(card.transform, "", 32, BLabel, TextAnchor.MiddleCenter, false, UIKit.Heading);
            Action live = () => t.text = Fmt.Time(S.NextContractIn);
            live(); Live(live);
        }

        void TelegraphHint(Transform p)
        {
            var d = GameDatabase.Building("telegraph");
            if (d == null) return;
            var card = Box(p, new Color(0, 0, 0, 0.18f), 380, -1, "Hint");
            Stack(card.gameObject, 12, new RectOffset(20, 20, 20, 20), TextAnchor.MiddleCenter);
            BuildingArt(card.transform, d, 300, 260);
            T(card.transform, "Build a Telegraph Office for one more order slot", 32, BLabel, TextAnchor.MiddleCenter, false, UIKit.Heading);
        }

        // ================================================================ 4 · Build

        public void OpenBuildMenu() { ShowBoard("Build", BuildBoard); }

        void BuildBoard(Transform body, Transform right)
        {
            string[] names = { "Homes", "Industry", "Civic", "Decor" };
            var cats = new[] { BuildingCategory.Residential, BuildingCategory.Production, BuildingCategory.Civic, BuildingCategory.Decoration };
            _boardSub = names[Array.IndexOf(cats, _buildTab)];
            Pill(right, null, "Free plots " + S.FreePlots().Count, 32);
            if (S.CanBuyPlot) Big(right, "Buy land", UIKit.WoodLight, () => OpenLand(), 240, 76, true, 34);

            var row = UIKit.Node("Row", body);
            UIKit.Stretch(row);
            Row(row, 12, new RectOffset(0, 0, 0, 0)).childForceExpandHeight = true;
            SideTabs(row.transform, 220, 158, new (string, Action<Transform>, bool, Action)[]
            {
                ("Homes", BuildingIcon("cabin"), _buildTab == BuildingCategory.Residential, () => _buildTab = BuildingCategory.Residential),
                ("Industry", BuildingIcon("sawmill"), _buildTab == BuildingCategory.Production, () => _buildTab = BuildingCategory.Production),
                ("Civic", BuildingIcon("saloon"), _buildTab == BuildingCategory.Civic, () => _buildTab = BuildingCategory.Civic),
                ("Decor", BuildingIcon("fountain"), _buildTab == BuildingCategory.Decoration, () => _buildTab = BuildingCategory.Decoration),
            });
            var panel = Box(row.transform, BTile, -1, -1, "Panel");
            UIKit.Size(panel.gameObject, -1, -1, 1);
            Stack(panel.gameObject, 0, new RectOffset(14, 14, 14, 14));
            var cards = Scroller(panel.transform, "build " + _buildTab, true, 16);
            foreach (var d in GameDatabase.Buildings.Where(b => b.Category == _buildTab)) BuildCard(cards, d);
        }

        static Action<Transform> BuildingIcon(string id)
        {
            return t =>
            {
                var sp = ArtSprite("Buildings/" + id);
                if (sp != null) Pic(t, sp, 100, 86);
            };
        }

        void BuildCard(Transform p, BuildingDef d)
        {
            bool locked = !S.IsBuildingUnlocked(d);
            int built = S.CountBuilt(d.Id);
            bool maxed = built >= d.MaxCount;
            var card = Box(p, BCardDeep, 470, -1, "Build " + d.Id);
            Stack(card.gameObject, 12, new RectOffset(18, 18, 16, 18), TextAnchor.UpperLeft);
            var top = HBox(card.transform, 8, 0, TextAnchor.MiddleLeft, -1, 56);
            UIKit.Size(T1(top.transform, d.Name, 40, BGold, TextAnchor.MiddleLeft, true).gameObject, -1, -1, 1);
            Pill(top.transform, null, built + " / " + d.MaxCount, 28);

            var sky = UIKit.Node("Sky", card.transform).AddComponent<Image>();
            sky.sprite = Sky();
            sky.raycastTarget = false;
            sky.color = locked ? new Color(0.45f, 0.45f, 0.5f) : Color.white;
            UIKit.Size(sky.gameObject, -1, 270);
            var art = BuildingArt(sky.transform, d, 320, 250);
            UIKit.Place(art.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(320, 250));
            if (locked)
            {
                art.color = new Color(0.35f, 0.35f, 0.38f);
                var lk = UIKit.Icon(sky.transform, "lock", 96);
                if (lk != null) UIKit.Place(lk, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 26), new Vector2(96, 96));
                var lv = T(sky.transform, "Unlocks at level " + d.UnlockLevel, 34, Color.white, TextAnchor.MiddleCenter, true);
                UIKit.Anchor(lv.gameObject, new Vector2(0, 0), new Vector2(1, 0.4f), Vector2.zero, Vector2.zero);
            }

            if (d.Recipe != null)
            {
                var rec = HBox(card.transform, 8, 0, TextAnchor.MiddleCenter, -1, 96);
                foreach (var i in d.Recipe.Inputs) { var id = i.id; Well(rec.transform, 86, tr => UIKit.ResIcon(tr, id, 56), i.amount.ToString()); }
                T(rec.transform, "»", 54, BGold, TextAnchor.MiddleCenter, true);
                var oid = d.Recipe.Output.id;
                Well(rec.transform, 86, tr => UIKit.ResIcon(tr, oid, 56), d.Recipe.Output.amount.ToString(), Palette.Hex(0x4B5A3C));
                Pill(rec.transform, tr => ClockIcon(tr, 30), Fmt.Time(d.Recipe.Seconds), 26);
            }
            else
            {
                string info = d.Category == BuildingCategory.Residential ? "Houses " + d.Population + " settlers who pay rent" : d.Blurb;
                var it = T(card.transform, info, 30, BLabel, TextAnchor.MiddleCenter, false, UIKit.Heading);
                UIKit.Size(it.gameObject, -1, 96);
            }
            Flex(card.transform);

            if (locked) { Big(card.transform, "Locked", UIKit.Disabled, null, -1, 90, false, 38); return; }
            var firstUid = S.State.buildings.Where(b => b.defId == d.Id).Select(b => b.uid).DefaultIfEmpty(0).First();
            if (!maxed && d.CostResources != null && d.CostResources.Length > 0)
            {
                var cost = HBox(card.transform, 8, 0, TextAnchor.MiddleCenter, -1, 50);
                foreach (var r in d.CostResources)
                {
                    if (r.amount <= 0) continue;
                    var id = r.id;
                    Pill(cost.transform, Ico(id, 36), r.amount.ToString(), 28, S.Amount(id) >= r.amount ? Color.white : BBad);
                }
            }
            var btns = HBox(card.transform, 12, 0, TextAnchor.MiddleCenter, -1, 92);
            if (maxed) Big(btns.transform, "Built " + built + " / " + d.MaxCount, UIKit.Disabled, null, firstUid != 0 ? 300 : 434, 88, false, 36);
            else
            {
                var check = S.CanConstruct(d.Id);
                string id = d.Id;
                Tag(PriceBtn(btns.transform, "Build", check.Ok ? UIKit.Green : UIKit.Disabled, tr => UIKit.CoinIcon(tr, 46), Fmt.Num(d.CostCoins), () =>
                {
                    var r = S.CanConstruct(id);
                    if (r.Ok) TownView.Instance.BeginPlacement(id);
                    else Toast(r.Message, false);
                }, firstUid != 0 ? 300 : 434, 88, true, 38), "build." + id);
            }
            if (firstUid != 0) Big(btns.transform, "Go", BBlue, () => OpenBuilding(firstUid), 122, 88, true, 38);
        }

        // ================================================================ 5 · Depot

        public void OpenDepot(string tab = null)
        {
            if (tab != null) _depotTab = tab;
            ShowBoard("Depot", DepotBoard);
        }

        public void OpenDepotLoco(string defId)
        {
            _depotTab = "locos";
            _depotLoco = defId;
            ShowBoard("Depot", DepotBoard);
        }

        void DepotBoard(Transform body, Transform right)
        {
            Big(right, "Locomotives", _depotTab == "locos" ? UIKit.Gold : UIKit.WoodLight, () => { _depotTab = "locos"; RebuildWindow(); }, 270, 76, true, 34);
            Big(right, "Wagons", _depotTab == "wagons" ? UIKit.Gold : UIKit.WoodLight, () => { _depotTab = "wagons"; RebuildWindow(); }, 200, 76, true, 34);
            var album = Big(right, "Album", _depotTab == "album" ? UIKit.Gold : UIKit.WoodLight, () => { _depotTab = "album"; RebuildWindow(); }, 180, 76, true, 34);
            if (S.CrewUnlocked) Big(right, "Crew", UIKit.WoodLight, () => OpenCrew(), 150, 76, true, 34);
            if (S.AlbumClaimable > 0) UIKit.Badge(album.transform, S.AlbumClaimable.ToString(), UIKit.Red);
            if (_depotTab == "album") AlbumDepot(body);
            else if (_depotTab == "wagons") WagonDepot(body);
            else LocoDepot(body);
        }

        void LocoDepot(Transform body)
        {
            var locos = GameDatabase.Locomotives.ToList();
            var d = _depotLoco != null ? GameDatabase.Locomotive(_depotLoco) : null;
            if (d == null)
            {
                var first = S.State.trains.OrderBy(x => x.platform).FirstOrDefault();
                d = first != null ? GameDatabase.Locomotive(first.defId) : locos[0];
                _depotLoco = d.Id;
            }
            var owned = S.State.trains.Where(x => x.defId == d.Id).OrderBy(x => x.platform).ToList();
            var train = owned.FirstOrDefault(x => x.uid == _selectedTrain) ?? owned.FirstOrDefault();
            bool locked = !S.IsLocoUnlocked(d);
            _boardSub = train != null ? "Level " + train.level : locked ? "Locked" : "For sale";

            var panel = Box(body, BDeep, -1, -1, "Panel");
            UIKit.Stretch(panel.gameObject);
            Row(panel.gameObject, 18, new RectOffset(16, 16, 16, 16)).childForceExpandHeight = true;

            // Every locomotive
            var colA = VBox(panel.transform, 0, 0, TextAnchor.UpperCenter, 172);
            var list = Scroller(colA.transform, "locos", false, 10);
            foreach (var ld in locos)
            {
                bool on = ld.Id == d.Id, lk = !S.IsLocoUnlocked(ld);
                int count = S.State.trains.Count(x => x.defId == ld.Id);
                var bg = on ? BGold : lk ? Palette.Hex(0x2B3345) : ld.Rarity >= Rarity.Epic ? Palette.Hex(0x6E4A1E) : BTile;
                var tile = UIKit.Panel(list, bg, "Loco", UIKit.RoundedGrad);
                tile.pixelsPerUnitMultiplier = 1.6f;
                UIKit.Size(tile.gameObject, 172, 96);
                var art = LocoArt(tile.transform, ld, 156, 76);
                UIKit.Place(art.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(156, 76));
                if (lk)
                {
                    art.color = new Color(0, 0, 0, 0.55f);
                    var li = UIKit.Icon(tile.transform, "lock", 50);
                    if (li != null) UIKit.Place(li, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(50, 50));
                }
                if (count > 0)
                {
                    var c = T(tile.transform, count > 1 ? "×" + count : "OWNED", 22, BGood, TextAnchor.LowerRight, true);
                    UIKit.Anchor(c.gameObject, Vector2.zero, Vector2.one, new Vector2(4, 2), new Vector2(-8, 0));
                }
                string id = ld.Id;
                if (!on) Clickable(tile.gameObject, () => { _depotLoco = id; RebuildWindow(); });
            }

            // Stats and the upgrade or purchase
            var card = Box(panel.transform, BCard, 540, -1, "Stats");
            Stack(card.gameObject, 14, new RectOffset(24, 24, 20, 22), TextAnchor.UpperLeft);
            var top = HBox(card.transform, 10, 0, TextAnchor.MiddleLeft, -1, 56);
            UIKit.Size(T1(top.transform, d.Name, 42, BGold, TextAnchor.MiddleLeft, true).gameObject, -1, -1, 1);
            Pill(top.transform, null, d.Rarity.ToString().ToUpperInvariant(), 24, null, UIKit.RarityColor(d.Rarity));
            T1(card.transform, (d.Era == Era.GoldenAge ? "Golden Age" : "Frontier") + " era · unlocks at level " + d.UnlockLevel, 28, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);
            Box(card.transform, BLine, -1, 3, "Line");
            int lvl = train != null ? train.level : 1;
            StatRow(card.transform, "Pulls", 10, d.Power, 0, d.Power + " wagons");
            int speed = Mathf.RoundToInt(d.Speed * Balance.LocoSpeedMultiplier(lvl));
            StatRow(card.transform, "Speed", 10, Mathf.Clamp(d.Speed / 9, 1, 10), Mathf.Clamp(speed / 9 - d.Speed / 9, 0, 10), speed + " mph");
            StatRow(card.transform, "Cargo", 10, Mathf.Clamp(Mathf.RoundToInt(Balance.LocoCapacityMultiplier(lvl) * 4), 1, 10), 0, "×" + Balance.LocoCapacityMultiplier(lvl).ToString("0.00"));
            var blurb = T(card.transform, "“" + d.Blurb + "”", 28, BLabel, TextAnchor.UpperLeft, false, UIKit.Body);
            blurb.fontStyle = FontStyle.Italic;
            Flex(card.transform);

            if (train != null && train.level < Balance.MaxLocoLevel)
            {
                T(card.transform, "Next level needs", 30, BGold, TextAnchor.MiddleLeft, false, UIKit.Heading);
                var need = HBox(card.transform, 10, 0, TextAnchor.MiddleLeft, -1, 96);
                foreach (var r in S.LocoUpgradeResources(train) ?? new ResourceAmount[0])
                {
                    var id = r.id;
                    Well(need.transform, 92, tr => UIKit.ResIcon(tr, id, 58), S.Amount(id) + "/" + r.amount, null, S.Amount(id) >= r.amount ? Color.white : BBad);
                }
                int coins = S.LocoUpgradeCoins(train);
                Pill(need.transform, tr => UIKit.CoinIcon(tr, 44), Fmt.Num(coins), 32, S.Coins >= coins ? Color.white : BBad);
                int tuid = train.uid;
                Big(card.transform, "Upgrade to level " + (train.level + 1), UIKit.Green, () => Result(S.UpgradeLocomotive(tuid), Sfx.Build), -1, 90, true, 40);
            }
            else if (train != null) T(card.transform, "Fully overhauled", 34, BGood, TextAnchor.MiddleLeft, true);
            if (train == null || train.level >= Balance.MaxLocoLevel && S.State.trains.Count < S.FleetCapacity) BuyLocoRow(card.transform, d, locked, train != null);

            // The locomotive itself
            var colC = VBox(panel.transform, 14, 0, TextAnchor.UpperLeft, -1, -1, 1);
            var scene = Resources.Load<Texture2D>("UI/Story/" + (d.Era == Era.GoldenAge ? "scene_goldenspike" : "scene_trestle"));
            var hero = Photo(colC.transform, scene, -1, 390);
            Overlay(hero.transform, Fade(true), new Color(0.11f, 0.15f, 0.21f, 0.96f));
            var big = LocoArt(hero.transform, d, 760, 280);
            UIKit.Place(big.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 14), new Vector2(760, 280));
            if (locked) big.color = new Color(0.15f, 0.15f, 0.18f, 0.9f);
            int idx = locos.FindIndex(x => x.Id == d.Id);
            var prev = Big(hero.transform, "<", UIKit.WoodLight, () => { _depotLoco = locos[(idx + locos.Count - 1) % locos.Count].Id; RebuildWindow(); }, 76, 140, true, 54);
            UIKit.Place(prev.gameObject, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 20), new Vector2(76, 140));
            var next = Big(hero.transform, ">", UIKit.WoodLight, () => { _depotLoco = locos[(idx + 1) % locos.Count].Id; RebuildWindow(); }, 76, 140, true, 54);
            UIKit.Place(next.gameObject, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-14, 20), new Vector2(76, 140));
            string status = train == null ? (locked ? "Locked" : "Not in your fleet") : S.HasArrived(train) ? "Arrived home" : train.IsIdle ? "At platform " + (train.platform + 1) : "Out on the line";
            Float(Pill(hero.transform, null, status, 30, train != null && train.IsIdle ? BGood : Color.white, new Color(0.055f, 0.075f, 0.11f, 0.85f)), new Vector2(0, 1), new Vector2(14, -14));

            if (train != null)
            {
                var wagons = S.WagonsOf(train);
                T(colC.transform, "Wagons  " + wagons.Count + " / " + d.Power, 32, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);
                var wh = HBox(colC.transform, 0, 0, TextAnchor.MiddleLeft, -1, 116);
                var slots = Scroller(wh.transform, "depotwagons", true, 12);
                foreach (var w in wagons) { var wd = GameDatabase.Wagon(w.defId); Well(slots, 114, tr => WagonArt(tr, wd, 100, 64)); }
                for (int i = wagons.Count; i < d.Power; i++)
                {
                    var e = Box(slots, new Color(1, 1, 1, 0.06f), 114, 114, "Empty");
                    var pl = T(e.transform, "+", 56, new Color(1, 1, 1, 0.3f), TextAnchor.MiddleCenter, true);
                    UIKit.Stretch(pl.gameObject);
                    e.raycastTarget = true;
                    Clickable(e.gameObject, () => { _depotTab = "wagons"; RebuildWindow(); });
                }
                Flex(colC.transform);
                var bottom = HBox(colC.transform, 16, 0, TextAnchor.MiddleLeft, -1, 92);
                T(bottom.transform, "Engine level", 34, Color.white, TextAnchor.MiddleLeft, true);
                Segs(bottom.transform, Balance.MaxLocoLevel, train.level);
                T(bottom.transform, train.level + " / " + Balance.MaxLocoLevel, 34, Color.white, TextAnchor.MiddleLeft, true);
                int tuid = train.uid;
                Tag(Big(bottom.transform, "Sell", UIKit.Red, () => ConfirmSellLoco(tuid), 170, 88, true, 38), "loco.sell");
                Big(bottom.transform, "Send Train", UIKit.Green, () => OpenTrain(tuid), 300, 88, true, 40);
            }
            else
            {
                T(colC.transform, "Pulls up to " + d.Power + " wagons at " + d.Speed + " mph", 34, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);
                Flex(colC.transform);
            }
        }

        /// <summary>Asks before selling a locomotive and says where its wagons and engineer go.</summary>
        void ConfirmSellLoco(int uid)
        {
            var t = S.Train(uid);
            string why = S.CantSellReason(t);
            if (why != null) { Toast(why, false); return; }
            var d = GameDatabase.Locomotive(t.defId);
            int coins = GameSession.LocoSellPrice(t);
            int wagons = S.WagonsOf(t).Count;
            var eng = S.EngineerOn(t);
            var notes = new System.Collections.Generic.List<string>();
            if (wagons > 0) notes.Add((wagons == 1 ? "Its wagon goes" : "Its " + wagons + " wagons go") + " back to the depot");
            if (eng != null) notes.Add(Frontier.Engineer(eng.id).Name + " heads to the bunkhouse");
            notes.Add("platform " + (t.platform + 1) + " is free for a better engine");
            string last = notes[notes.Count - 1];
            notes.RemoveAt(notes.Count - 1);
            string body = (notes.Count > 0 ? string.Join(", ", notes) + " and " : "") + last + ".";
            body = char.ToUpperInvariant(body[0]) + body.Substring(1);
            ShowBoard("Sell this engine?", (b, right) =>
            {
                var panel = Box(b, BCardDeep, -1, -1, "Panel");
                UIKit.Stretch(panel.gameObject);
                Stack(panel.gameObject, 16, new RectOffset(30, 30, 24, 30), TextAnchor.MiddleCenter);
                var art = LocoArt(panel.transform, d, 520, 190);
                UIKit.Size(art.gameObject, 520, 190);
                T(panel.transform, "Sell " + GameSession.Name(t) + " (level " + t.level + ") for " + Fmt.Num(coins) + " coins?", 44, BGold, TextAnchor.MiddleCenter, true);
                T(panel.transform, body, 32, Color.white, TextAnchor.MiddleCenter, false, UIKit.Heading);
                Flex(panel.transform);
                var row = HBox(panel.transform, 24, 0, TextAnchor.MiddleCenter, -1, 100);
                Big(row.transform, "Keep it", UIKit.Green, () => OpenDepotLoco(d.Id), 380, 96, true, 40);
                PriceBtn(row.transform, "Sell", UIKit.Red, tr => UIKit.CoinIcon(tr, 46), Fmt.Num(coins), () =>
                {
                    var r = S.SellLocomotive(uid);
                    Result(r, Sfx.Coin);
                    if (r.Ok) { _selectedTrain = 0; OpenDepot("locos"); }
                }, 420, 96, true, 40);
            }, 1300, 760);
        }

        void StatRow(Transform p, string label, int total, int on, int bonus, string value)
        {
            var row = HBox(p, 12, 0, TextAnchor.MiddleLeft, -1, 40);
            UIKit.Size(T(row.transform, label, 30, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading).gameObject, 100, -1);
            Segs(row.transform, total, on, bonus);
            UIKit.Size(T(row.transform, value, 30, Color.white, TextAnchor.MiddleRight, true).gameObject, 120, -1);
        }

        void BuyLocoRow(Transform p, LocomotiveDef d, bool locked, bool another)
        {
            if (locked) { Big(p, Locks.Text(d.UnlockLevel), UIKit.Disabled, null, -1, 84, false, 32); return; }
            BlueprintRow(p, d);
            if (d.PriceCoins == 0 && d.PriceNuggets == 0)
            {
                if (d.Blueprints <= 0) T(p, "Your first engine. Every empire starts somewhere.", 28, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);
                return;
            }
            if (S.State.trains.Count >= S.FleetCapacity)
            {
                if (S.StationComplete) Big(p, "All platforms full · sell an engine first", UIKit.Disabled, null, -1, 84, false, 30);
                else Big(p, "All platforms full · lay a new one", UIKit.WoodLight, () => OpenFleet(), -1, 84, true, 32);
                return;
            }
            string id = d.Id;
            bool nug = d.PriceNuggets > 0;
            int price = nug ? d.PriceNuggets : d.PriceCoins;
            bool can = (nug ? S.Nuggets : S.Coins) >= price;
            PriceBtn(p, another ? "Buy another" : "Buy", can ? UIKit.Green : UIKit.Disabled, tr => { if (nug) UIKit.NuggetIcon(tr, 46); else UIKit.CoinIcon(tr, 46); }, Fmt.Num(price),
                () => Result(S.BuyLocomotive(id), Sfx.Bell), -1, another ? 76 : 90, true, another ? 32 : 40);
        }

        void WagonDepot(Transform body)
        {
            _boardSub = "Wagons";
            var idle = S.State.trains.Where(t => t.IsIdle && S.WagonsOf(t).Count < GameDatabase.Locomotive(t.defId).Power).ToList();
            var panel = Box(body, BTile, -1, -1, "Panel");
            UIKit.Stretch(panel.gameObject);
            Stack(panel.gameObject, 10, new RectOffset(14, 14, 12, 14));
            T1(panel.transform, idle.Count > 0 ? "New wagons couple to " + GameSession.Name(idle[0]) + " (the first idle train with room)." : "New wagons wait in the depot until a train has room.",
                30, Color.white, TextAnchor.MiddleLeft, false, UIKit.Heading);
            var cards = Scroller(panel.transform, "wagonshop", true, 16);
            int target = idle.Count > 0 ? idle[0].uid : 0;
            foreach (var d in GameDatabase.Wagons)
            {
                bool locked = !S.IsWagonUnlocked(d);
                var card = Box(cards, BCardDeep, 380, -1, "Wagon " + d.Id);
                Stack(card.gameObject, 12, new RectOffset(18, 18, 16, 18), TextAnchor.UpperLeft);
                T1(card.transform, d.Name, 38, BGold, TextAnchor.MiddleLeft, true);
                var hero = Box(card.transform, BTile, -1, 170, "Hero");
                UIKit.HStack(hero.gameObject, 0, 0, TextAnchor.MiddleCenter);
                var art = WagonArt(hero.transform, d, 300, 150);
                if (locked) art.color = new Color(0.3f, 0.3f, 0.33f);
                var cap = HBox(card.transform, 8, 0, TextAnchor.MiddleLeft, -1, 50);
                Pill(cap.transform, null, "Carries " + d.Capacity, 28);
                // What you own: total, spare in the depot, and coupled to trains.
                int own = S.WagonsOwned(d.Id), spare = S.WagonsInDepot(d.Id);
                var owned = Box(card.transform, own > 0 ? Palette.Hex(0x2E4A2A) : BChip, -1, 56, "Owned");
                var ot = T1(owned.transform, own == 0 ? "You don't own any yet" : "Own " + own + "  ·  " + spare + " spare  ·  " + (own - spare) + " on trains",
                    26, own > 0 ? BGood : BLabel, TextAnchor.MiddleCenter, own > 0, UIKit.Heading);
                UIKit.Stretch(ot.gameObject, 8);
                var hauls = GameDatabase.Resources.Where(r => r.Cargo == d.Cargo && !r.Manufactured && !r.IsPart).ToList();
                var hr = HBox(card.transform, 6, 0, TextAnchor.MiddleLeft, -1, 70);
                foreach (var r in hauls.Take(5)) { if (r.IsPassenger) UIKit.Icon(hr.transform, "people", 60); else UIKit.ResIcon(hr.transform, r.Id, 56); }
                Flex(card.transform);
                if (locked) { Big(card.transform, "Unlocks at level " + d.UnlockLevel, UIKit.Disabled, null, -1, 86, false, 32); continue; }
                string id = d.Id;
                // Buy and, for spare wagons in the depot, sell back for half price (coupled ones must be uncoupled first).
                var btns = HBox(card.transform, 10, 0, TextAnchor.MiddleCenter, -1, 86);
                float bw = own > 0 ? 167 : 344;
                PriceBtn(btns.transform, "Buy", S.Coins >= d.PriceCoins ? UIKit.Green : UIKit.Disabled, tr => UIKit.CoinIcon(tr, 40), Fmt.Num(d.PriceCoins), () => Result(S.BuyWagon(id, target)), bw, 84, true, 30);
                if (own > 0)
                    Tag(PriceBtn(btns.transform, "Sell", spare > 0 ? UIKit.Red : UIKit.Disabled, tr => UIKit.CoinIcon(tr, 40),
                        "+" + Fmt.Num(GameSession.WagonSellPrice(d)), () => Result(S.SellWagon(id), Sfx.Coin), bw, 84, spare > 0, 30), "wagon.sell." + d.Id);
                if (own > 0 && spare == 0) T1(card.transform, "Uncouple one from a train to sell it", 22, BLabel, TextAnchor.MiddleCenter, false, UIKit.Heading);
            }
        }

        // ================================================================ 6 · Warehouse

        public void OpenWarehouse() { ShowBoard("Warehouse", WarehouseBoard); }

        void WarehouseBoard(Transform body, Transform right)
        {
            _boardSub = "Level " + S.State.warehouseLevel;
            var cap = Box(right, BChip, 520, 66, "Capacity");
            UIKit.HStack(cap.gameObject, 0, 6, TextAnchor.MiddleCenter);
            var fill = Track(cap.transform, 50, S.WarehouseFree < S.WarehouseCapacity * 0.1f ? Palette.Hex(0xE3412F) : BGold);
            UIKit.SetBar(fill, S.WarehouseUsed / (float)S.WarehouseCapacity);
            var ct = T(fill.transform.parent, S.WarehouseUsed + " / " + S.WarehouseCapacity, 36, Color.white, TextAnchor.MiddleCenter, true);
            UIKit.Stretch(ct.gameObject);
            if (S.State.warehouseLevel < Balance.MaxWarehouseLevel)
                PriceBtn(right, "Expand", UIKit.Green, tr => UIKit.CoinIcon(tr, 42), Fmt.Num(Balance.WarehouseUpgradeCoins(S.State.warehouseLevel)), () => Result(S.UpgradeWarehouse(), Sfx.Build), 330, 80, true, 34);

            var row = UIKit.Node("Row", body);
            UIKit.Stretch(row);
            Row(row, 12, new RectOffset(0, 0, 0, 0)).childForceExpandHeight = true;
            SideTabs(row.transform, 220, 190, new (string, Action<Transform>, bool, Action)[]
            {
                ("Raw", Ico("iron_ore", 100), _whTab == "raw", () => _whTab = "raw"),
                ("Goods", Ico("steel", 100), _whTab == "goods", () => _whTab = "goods"),
                ("Parts", Ico("bearings", 100), _whTab == "parts", () => _whTab = "parts"),
            });
            var panel = Box(row.transform, BTile, -1, -1, "Panel");
            UIKit.Size(panel.gameObject, -1, -1, 1);
            Row(panel.gameObject, 14, new RectOffset(14, 14, 14, 14)).childForceExpandHeight = true;

            var known = S.ObtainableResources();
            var items = GameDatabase.Resources.Where(r => !r.IsPassenger && (_whTab == "parts" ? r.IsPart : _whTab == "goods" ? r.Manufactured && !r.IsPart : !r.Manufactured && !r.IsPart))
                .Where(r => S.Amount(r.Id) > 0 || known.Contains(r.Id)).ToList();
            if (_whSel == null || !items.Any(r => r.Id == _whSel)) _whSel = (items.FirstOrDefault(r => S.Amount(r.Id) > 0) ?? items.FirstOrDefault())?.Id;

            var gridHolder = VBox(panel.transform, 0, 0, TextAnchor.UpperLeft, -1, -1, 1);
            var scroll = Scroller(gridHolder.transform, "wh " + _whTab, false, 0);
            var gridGo = UIKit.Node("Grid", scroll);
            var grid = gridGo.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(272, 172);
            grid.spacing = new Vector2(14, 14);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
            foreach (var r in items) ResourceCard(gridGo.transform, r);
            if (items.Count == 0) T(scroll, "Nothing here yet. Send trains out to haul goods home.", 32, Color.white, TextAnchor.MiddleLeft, false, UIKit.Heading);
            if (_whSel != null) ResourceDetail(panel.transform, GameDatabase.Resource(_whSel));
        }

        void ResourceCard(Transform p, ResourceDef r)
        {
            int amount = S.Amount(r.Id);
            var card = Box(p, BCardDeep, -1, -1, "Res " + r.Id);
            card.raycastTarget = true;
            Stack(card.gameObject, 6, new RectOffset(14, 14, 10, 14));
            if (r.Id == _whSel)
            {
                var ring = card.gameObject.AddComponent<Outline>();
                ring.effectColor = BGold;
                ring.effectDistance = new Vector2(4, -4);
            }
            T1(card.transform, r.Name, 30, Color.white, TextAnchor.MiddleLeft, false, UIKit.Heading);
            var well = Box(card.transform, BTile, -1, 100, "Well");
            Row(well.gameObject, 4, new RectOffset(4, 16, 0, 0));
            var ic = UIKit.ResIcon(well.transform, r.Id, 84);
            if (amount == 0) foreach (var g in ic.GetComponentsInChildren<Graphic>()) g.color = new Color(1, 1, 1, 0.35f);
            UIKit.Size(T(well.transform, Fmt.Num(amount), 50, Color.white, TextAnchor.MiddleRight, true).gameObject, -1, -1, 1);
            string id = r.Id;
            Clickable(card.gameObject, () => { _whSel = id; RebuildWindow(); });
        }

        void ResourceDetail(Transform p, ResourceDef r)
        {
            int amount = S.Amount(r.Id);
            var card = Box(p, BCardDeep, 470, -1, "Detail");
            Stack(card.gameObject, 12, new RectOffset(20, 20, 18, 20), TextAnchor.UpperCenter);
            T1(card.transform, r.Name, 50, BGold, TextAnchor.MiddleCenter, true);
            Box(card.transform, BLine, -1, 3, "Line");
            var box = Box(card.transform, BTile, -1, -1, "Box");
            Stack(box.gameObject, 8, new RectOffset(14, 14, 12, 16), TextAnchor.UpperCenter);
            var ih = HBox(box.transform, 18, 0, TextAnchor.MiddleCenter, -1, 112);
            UIKit.ResIcon(ih.transform, r.Id, 96);
            T(ih.transform, Fmt.Num(amount), 60, Color.white, TextAnchor.MiddleCenter, true);
            var src = SourceOf(r.Id);
            var maker = MakerOf(r.Id);
            string from = r.IsPart ? "Contract rewards" : src != null ? src.Name : maker != null ? maker.Name : "";
            var pin = Box(box.transform, BChip, -1, 54, "From");
            Row(pin.gameObject, 8, new RectOffset(8, 14, 0, 0), TextAnchor.MiddleCenter);
            UIKit.Icon(pin.transform, "nav_territory", 48);
            T1(pin.transform, from, 32, Color.white, TextAnchor.MiddleCenter, true);
            string id = r.Id;
            Big(box.transform, "Get More", BBlue, () => GoGet(id), -1, 78, !r.IsPart, 40);

            var users = GameDatabase.Buildings.Where(b => b.Recipe != null && b.Recipe.Inputs.Any(i => i.id == r.Id)).Take(3).ToList();
            if (users.Count > 0)
            {
                T(card.transform, "Used by", 30, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);
                var ur = HBox(card.transform, 10, 0, TextAnchor.MiddleLeft, -1, 88);
                foreach (var u in users) { var ud = u; Well(ur.transform, 88, tr => BuildingArt(tr, ud, 80, 74)); }
            }
            Flex(card.transform);
            if (!r.IsPart && amount > 0)
            {
                int sell = Mathf.Min(10, amount);
                int value = Mathf.Max(1, (int)(r.Value * sell * 0.6f));
                PriceBtn(card.transform, "Sell " + sell, UIKit.WoodLight, tr => UIKit.CoinIcon(tr, 42), Fmt.Num(value), () => Result(S.Sell(id, sell)), -1, 80, true, 34);
            }
        }

        // ================================================================ 7 · Contract complete

        void OnContractCompleted(ContractState c)
        {
            AudioManager.Play(Sfx.Deliver);
            Toast(c.client + ": " + StoryDirector.ClientThanks(c.client), true);
            StoryDirector.Instance.OnContract();
            var parts = c.rewardParts != null ? c.rewardParts.ToArray() : new ResourceAmount[0];
            Celebrations.Instance.Enqueue(() => RewardMoment(c.client, "Contract Complete!", c.rewardCoins, c.rewardXp, c.rewardNuggets, parts));
        }

        /// <summary>Full-screen reward card: ribbon, glowing reward icons, XP bar; tap to collect.</summary>
        IEnumerator RewardMoment(string who, string title, int coins, int xp, int nuggets, ResourceAmount[] parts)
        {
            var root = UIKit.Panel(_popupLayer, new Color(0.03f, 0.04f, 0.07f, 0.96f), "Reward");
            SetHudVisible(false);
            root.sprite = null;
            UIKit.Stretch(root.gameObject);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;
            bool done = false;
            var btn = root.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => done = true);

            var name = T(root.transform, who, 56, BGold, TextAnchor.MiddleCenter, true);
            UIKit.Place(name.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(1600, 80));
            var ribbon = UIKit.Node("Ribbon", root.transform);
            UIKit.Place(ribbon, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -160), new Vector2(1500, 160));
            for (int s = -1; s <= 1; s += 2)
            {
                var tail = UIKit.Panel(ribbon.transform, Palette.Hex(0x1A2131), "Tail", UIKit.RoundedSmall);
                UIKit.Place(tail.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(s * 700, -26), new Vector2(200, 130));
            }
            var band = UIKit.Panel(ribbon.transform, Palette.Hex(0x2B3549), "Band", UIKit.RoundedGrad, true);
            UIKit.Place(band.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1400, 160));
            for (int s = 0; s < 2; s++)
            {
                var line = UIKit.Panel(band.transform, new Color(0.73f, 0.77f, 0.83f, 0.8f), "Trim", UIKit.RoundedSmall);
                UIKit.Anchor(line.gameObject, new Vector2(0.06f, s), new Vector2(0.94f, s), new Vector2(0, s == 0 ? 16 : -20), new Vector2(0, s == 0 ? 20 : -16));
            }
            var tt = T(band.transform, title.ToUpperInvariant(), 104, Palette.Hex(0xDDE4EE), TextAnchor.MiddleCenter, true);
            UIKit.Stretch(tt.gameObject, 10);

            var row = UIKit.Node("Rewards", root.transform);
            UIKit.Place(row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(1700, 400));
            UIKit.HStack(row, 40, 0, TextAnchor.MiddleCenter);
            var cells = new System.Collections.Generic.List<Transform>();
            if (coins > 0) cells.Add(Burst(row.transform, t => UIKit.CoinIcon(t, 190), Fmt.Num(coins)));
            if (xp > 0) cells.Add(Burst(row.transform, t => UIDeco.XpIcon(t, 190), xp.ToString()));
            if (nuggets > 0) cells.Add(Burst(row.transform, t => UIKit.NuggetIcon(t, 190), nuggets.ToString()));
            foreach (var pr in parts) { var id = pr.id; cells.Add(Burst(row.transform, t => UIKit.ResIcon(t, id, 170), pr.amount.ToString())); }

            var foot = UIKit.Node("Foot", root.transform);
            UIKit.Place(foot, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(1200, 150));
            UIKit.VStack(foot, 12, 0, TextAnchor.MiddleCenter).childForceExpandWidth = false;
            var xpRow = HBox(foot.transform, 14, 0, TextAnchor.MiddleCenter, -1, 70);
            var disc = UIKit.KitImage(xpRow.transform, "round_gold", "Level");
            UIKit.Size(disc.gameObject, 70, 70);
            var lv = T(disc.transform, S.Level.ToString(), 40, Color.white, TextAnchor.MiddleCenter, true);
            UIKit.Stretch(lv.gameObject);
            var xf = Track(xpRow.transform, 34, BGold, 560);
            UIKit.SetBar(xf, S.LevelProgress);
            T(xpRow.transform, Fmt.Num(S.State.xp) + " / " + Fmt.Num(Balance.XpForNextLevel(S.Level)) + " XP", 32, Color.white, TextAnchor.MiddleLeft, true);
            var tap = T(foot.transform, "Tap anywhere to continue", 40, Color.white, TextAnchor.MiddleCenter, true);

            Celebrations.Instance.Confetti();
            for (float t = 0; t < 1; t += Time.unscaledDeltaTime * 4)
            {
                group.alpha = t;
                for (int i = 0; i < cells.Count; i++) cells[i].localScale = Vector3.one * Pop(t * 1.4f - i * 0.15f);
                yield return null;
            }
            group.alpha = 1;
            foreach (var c in cells) c.localScale = Vector3.one;
            float shown = 0;
            while (!done || shown < 0.5f)
            {
                shown += Time.unscaledDeltaTime;
                tap.color = new Color(1, 1, 1, 0.65f + 0.35f * Mathf.Sin(shown * 4));
                yield return null;
            }
            if (!WindowOpen) SetHudVisible(true); // the coins fly to the HUD chips
            var cel = Celebrations.Instance;
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (coins > 0) cel.FlyTo(center, (RectTransform)_coins.transform.parent, t => UIKit.CoinIcon(t, 48), 10);
            if (nuggets > 0) cel.FlyTo(center, (RectTransform)_nuggets.transform.parent, t => UIKit.NuggetIcon(t, 48), 5);
            for (float t = 1; t > 0; t -= Time.unscaledDeltaTime * 5) { group.alpha = t; yield return null; }
            Destroy(root.gameObject);
        }

        static float Pop(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.7f ? Mathf.Lerp(0, 1.12f, t / 0.7f) : Mathf.Lerp(1.12f, 1f, (t - 0.7f) / 0.3f);
        }

        static Transform Burst(Transform p, Action<Transform> icon, string amount)
        {
            var cell = UIKit.Node("Burst", p);
            UIKit.Size(cell, 380, 400);
            var rays = UIKit.Node("Rays", cell.transform).AddComponent<Image>();
            rays.sprite = Rays();
            rays.raycastTarget = false;
            UIKit.Place(rays.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 30), new Vector2(420, 420));
            rays.gameObject.AddComponent<UISpin>();
            var ic = UIKit.Node("Icon", cell.transform);
            UIKit.Place(ic, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(220, 220));
            UIKit.HStack(ic, 0, 0, TextAnchor.MiddleCenter);
            icon(ic.transform);
            var plate = UIKit.Panel(cell.transform, BChip, "Plate", UIKit.Rounded);
            UIKit.Place(plate.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(230, 86));
            plate.gameObject.AddComponent<Outline>().effectColor = new Color(0.055f, 0.075f, 0.11f, 1);
            var t = T(plate.transform, amount, 60, Color.white, TextAnchor.MiddleCenter, true);
            UIKit.Stretch(t.gameObject);
            return cell.transform;
        }
    }

    /// <summary>Slow constant rotation (reward sunbursts).</summary>
    public sealed class UISpin : MonoBehaviour
    {
        public float DegreesPerSecond = 12;
        void Update() { transform.Rotate(0, 0, -DegreesPerSecond * Time.unscaledDeltaTime); }
    }
}
