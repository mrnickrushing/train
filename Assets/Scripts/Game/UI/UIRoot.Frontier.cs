using System;
using System.Collections.Generic;
using System.Linq;
using Ironhorse.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ironhorse.Game
{
    /// <summary>
    /// The dispatcher's board (in the Daily board beside payday), engineers, the traveling peddler,
    /// the Full Steam gauge, the curio museum and new-territory charters.
    /// </summary>
    public sealed partial class UIRoot
    {
        string _dailyTab = "payday";
        int _crewFor;
        bool _charterArmed;
        GameObject _combo;
        Text _comboText;
        Image _comboFill;

        // ================================================================ Daily board (payday + tasks)

        public void OpenDaily(string tab)
        {
            _dailyTab = tab;
            ShowBoard("Daily", DailyBoard);
        }

        void DailyBoard(Transform body, Transform right)
        {
            var row = UIKit.Node("Row", body);
            UIKit.Stretch(row);
            Row(row, 12, new RectOffset(0, 0, 0, 0)).childForceExpandHeight = true;
            int tasks = S.DailyClaimable;
            SideTabs(row.transform, 200, 200, new (string, Action<Transform>, bool, Action)[]
            {
                ("Payday", Ico("payday", 110), _dailyTab == "payday", () => _dailyTab = "payday"),
                ("Tasks" + (tasks > 0 ? " (" + tasks + ")" : ""), Ico("nav_contracts", 110), _dailyTab == "tasks", () => _dailyTab = "tasks"),
            });
            var holder = UIKit.Node("Holder", row.transform);
            UIKit.Size(holder, -1, -1, 1, 1);
            if (_dailyTab == "tasks") TasksBoard(holder.transform, right);
            else PaydayBoard(holder.transform, right);
        }

        static string DailyIcon(DailyKind k)
        {
            switch (k)
            {
                case DailyKind.Dispatch: return "nav_trains";
                case DailyKind.Contracts: return "nav_contracts";
                case DailyKind.Produce: return "planks";
                case DailyKind.Collect: return "crate";
                case DailyKind.Passengers: return "people";
                case DailyKind.Crates: return "crate_mystery";
                case DailyKind.Events: return "tumbleweed";
                case DailyKind.Rent: return "coin";
                case DailyKind.EarnCoins: return "shop_coins";
                default: return "nav_territory";
            }
        }

        void TasksBoard(Transform body, Transform right)
        {
            _boardSub = "Dispatcher's board";
            var pill = Pill(right, tr => ClockIcon(tr, 40), "", 30);
            var pt = pill.GetComponentInChildren<Text>();
            Action live = () => pt.text = "New jobs in " + Fmt.Time(S.NextPaydayIn);
            live(); Live(live);

            var panel = Box(body, BDeep, -1, -1, "Panel");
            UIKit.Stretch(panel.gameObject);
            Row(panel.gameObject, 18, new RectOffset(18, 18, 18, 18)).childForceExpandHeight = true;

            var left = VBox(panel.transform, 12, 0, TextAnchor.UpperLeft, -1, -1, 1);
            left.GetComponent<VerticalLayoutGroup>().childForceExpandHeight = false;
            UIKit.Size(T1(left.transform, "Today's jobs  ·  " + Frontier.DailyTaskPoints + " points each", 34, Color.white, TextAnchor.MiddleLeft, true).gameObject, -1, 48);
            for (int i = 0; i < S.State.dailyTasks.Count; i++) DailyRow(left.transform, i);

            var col = VBox(panel.transform, 14, 0, TextAnchor.UpperLeft, 640);
            col.GetComponent<VerticalLayoutGroup>().childForceExpandHeight = false;
            ChestTrack(col.transform, "Daily chests", S.State.dailyPoints, Frontier.DailyChestAt, false, null);
            ChestTrack(col.transform, "Weekly chests", S.State.weeklyPoints, Frontier.WeeklyChestAt, true, "Resets in " + Fmt.Time(S.WeeklyResetIn));
        }

        void DailyRow(Transform p, int i)
        {
            var t = S.State.dailyTasks[i];
            bool done = S.IsDailyDone(t);
            var row = Box(p, t.claimed ? Palette.Hex(0x2B3A2A) : done ? Palette.Hex(0x3E5A33) : BCardDeep, -1, 118, "Job");
            Row(row.gameObject, 16, new RectOffset(12, 16, 10, 10));
            string ic = DailyIcon(t.kind);
            Well(row.transform, 98, tr => { if (UIKit.Icon(tr, ic, 84) == null) UIKit.ResIcon(tr, ic, 70); });
            var col = VBox(row.transform, 8, 0, TextAnchor.MiddleLeft, -1, -1, 1);
            col.GetComponent<VerticalLayoutGroup>().childForceExpandHeight = false;
            UIKit.Size(T1(col.transform, Frontier.DailyText(t), 34, Color.white, TextAnchor.MiddleLeft, true).gameObject, -1, 44);
            var bar = Track(col.transform, 34, done ? BGood : BGold);
            var bt = T(bar.transform.parent, "", 26, Color.white, TextAnchor.MiddleCenter, true);
            UIKit.Stretch(bt.gameObject);
            int idx = i;
            Action live = () =>
            {
                if (idx >= S.State.dailyTasks.Count) return;
                var x = S.State.dailyTasks[idx];
                int pr = S.DailyProgress(x);
                UIKit.SetBar(bar, pr / (float)x.count);
                bt.text = Fmt.Num(pr) + " / " + Fmt.Num(x.count);
            };
            live(); Live(live);
            var btn = HBox(row.transform, 0, 0, TextAnchor.MiddleCenter, 200, 98);
            if (t.claimed) CheckBadge(btn.transform, 70);
            else if (done) Big(btn.transform, "+" + Frontier.DailyTaskPoints, UIKit.Green, () => Result(S.ClaimDailyTask(idx), Sfx.Coin), 190, 90, true, 40);
            else Pill(btn.transform, null, "+" + Frontier.DailyTaskPoints, 32, BLabel);
        }

        void ChestTrack(Transform p, string title, int points, int[] steps, bool weekly, string note)
        {
            var card = Box(p, BCard, -1, -1, "Chests");
            Stack(card.gameObject, 10, new RectOffset(18, 18, 14, 16));
            var head = HBox(card.transform, 10, 0, TextAnchor.MiddleLeft, -1, 44);
            UIKit.Size(T(head.transform, title, 34, BGold, TextAnchor.MiddleLeft, true).gameObject, -1, -1, 1);
            if (note != null) T(head.transform, note, 24, BLabel, TextAnchor.MiddleRight, false, UIKit.Heading).horizontalOverflow = HorizontalWrapMode.Overflow;
            int max = steps[steps.Length - 1];
            var meter = HBox(card.transform, 0, 0, TextAnchor.MiddleLeft, -1, 30);
            var fill = Track(meter.transform, 28, BGold);
            UIKit.SetBar(fill, Mathf.Min(1, points / (float)max));
            var mt = T(fill.transform.parent, Mathf.Min(points, max) + " / " + max, 24, Color.white, TextAnchor.MiddleCenter, true);
            UIKit.Stretch(mt.gameObject);
            var chests = HBox(card.transform, 10, 0, TextAnchor.MiddleCenter, -1, weekly ? 200 : 250);
            chests.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            foreach (int at in steps)
            {
                bool claimed = weekly ? S.State.weeklyChests.Contains(at) : S.State.dailyChests.Contains(at);
                bool can = weekly ? S.CanClaimWeeklyChest(at) : S.CanClaimDailyChest(at);
                var g = weekly ? S.WeeklyChestGrant(at) : S.DailyChestGrant(at);
                int shards = Frontier.ChestShards(at, weekly);
                var tile = Box(chests.transform, can ? Palette.Hex(0x5C4A1C) : BCardDeep, -1, -1, "Chest");
                UIKit.Size(tile.gameObject, -1, -1, 1);
                Stack(tile.gameObject, 4, new RectOffset(6, 6, 8, 8), TextAnchor.UpperCenter);
                var art = HBox(tile.transform, 0, 0, TextAnchor.MiddleCenter, -1, weekly ? 78 : 100);
                UIKit.Icon(art.transform, at == steps[steps.Length - 1] ? "crate_mystery" : "shop_supplies", weekly ? 78 : 100);
                var parts = new List<string>();
                if (g.Coins > 0) parts.Add(Fmt.Num(g.Coins) + " coins");
                if (g.Nuggets > 0) parts.Add(g.Nuggets + " nuggets");
                if (g.Fuel > 0) parts.Add(g.Fuel + " fuel");
                foreach (var r in g.Resources) parts.Add(r.amount + " " + ResName(r.id));
                if (g.Blueprints > 0) parts.Add(g.Blueprints + " plans");
                if (shards > 0 && S.CrewUnlocked) parts.Add(shards + " crew shards");
                var gt = T(tile.transform, string.Join(", ", parts), 22, Color.white, TextAnchor.MiddleCenter, false, UIKit.Heading);
                UIKit.Size(gt.gameObject, -1, weekly ? 50 : 56);
                int a = at;
                if (claimed) { var ck = HBox(tile.transform, 0, 0, TextAnchor.MiddleCenter, -1, 56); CheckBadge(ck.transform, 50); }
                else if (can) Big(tile.transform, "Open", UIKit.Green, () =>
                {
                    var r = weekly ? S.ClaimWeeklyChest(a) : S.ClaimDailyChest(a);
                    Result(r, Sfx.LevelUp);
                    if (r.Ok) Celebrations.Instance.Confetti();
                }, 150, 56, true, 30);
                else Pill(tile.transform, null, at + " pts", 26, BLabel);
            }
        }

        // ================================================================ Engineers

        static void EngineerPortrait(Transform parent, string id, float size, bool dark = false)
        {
            var mask = UIKit.Panel(parent, Palette.Hex(0x3C6E91), "Portrait", UIKit.Circle);
            UIKit.Size(mask.gameObject, size, size);
            mask.raycastTarget = false;
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var sp = UIKit.IconSprite("eng_" + id);
            if (sp == null) return;
            var pic = UIKit.Node("Face", mask.transform).AddComponent<Image>();
            pic.sprite = sp;
            pic.preserveAspect = true;
            pic.raycastTarget = false;
            if (dark) pic.color = new Color(0, 0, 0, 0.75f);
            UIKit.Stretch(pic.gameObject, -size * 0.04f);
        }

        public void OpenCrew(int forTrain = 0)
        {
            _crewFor = forTrain;
            ShowBoard("Crew", CrewBoard);
        }

        void CrewBoard(Transform body, Transform right)
        {
            var target = _crewFor != 0 ? S.Train(_crewFor) : null;
            _boardSub = target != null ? "Pick an engineer for " + GameSession.Name(target) : "Engineers ride your locomotives";
            var panel = Box(body, BDeep, -1, -1, "Panel");
            UIKit.Stretch(panel.gameObject);
            Stack(panel.gameObject, 0, new RectOffset(16, 16, 16, 16));
            if (!S.CrewUnlocked)
            {
                var t = T(panel.transform, "Engineers sign on at level " + Frontier.CrewLevel + ". Each one rides a locomotive and makes its trips better.", 40, Color.white, TextAnchor.MiddleCenter, true);
                UIKit.Size(t.gameObject, -1, -1, 1, 1);
                return;
            }
            Pill(right, null, S.State.engineers.Count(e => e.level > 0) + " / " + Frontier.Engineers.Count + " signed on", 30);
            var cards = Scroller(panel.transform, "crew", true, 16);
            foreach (var d in Frontier.Engineers.OrderBy(x => { var e = S.Crew(x.Id); return e != null && e.level > 0 ? 0 : 1; })) EngineerCard(cards, d, target);
        }

        void EngineerCard(Transform p, EngineerDef d, TrainState target)
        {
            var e = S.Crew(d.Id);
            int level = e != null ? e.level : 0, shards = e != null ? e.shards : 0;
            int need = S.TrainingShards(e);
            bool hired = level > 0, maxed = level >= Frontier.MaxEngineerLevel;
            var card = Box(p, BCard, 420, -1, "Engineer");
            Stack(card.gameObject, 8, new RectOffset(16, 16, 16, 16), TextAnchor.UpperCenter);
            var top = UIKit.Node("Top", card.transform);
            UIKit.Size(top, -1, 220);
            Overlay(top.transform, Rays(), new Color(1, 1, 1, hired ? 0.45f : 0.12f));
            var ph = UIKit.Node("Pic", top.transform);
            UIKit.Stretch(ph);
            UIKit.HStack(ph, 0, 0, TextAnchor.MiddleCenter);
            EngineerPortrait(ph.transform, d.Id, 200, !hired);
            var rar = Pill(top.transform, null, d.Rarity.ToString().ToUpperInvariant(), 22, null, UIKit.RarityColor(d.Rarity));
            Float(rar, new Vector2(0, 1), new Vector2(0, 0));
            T1(card.transform, d.Name, 36, BGold, TextAnchor.MiddleCenter, true);
            T1(card.transform, d.Title + (hired ? " · level " + level : ""), 26, BLabel, TextAnchor.MiddleCenter, false, UIKit.Heading);
            var perk = Box(card.transform, BChip, -1, 56, "Perk");
            var pt = T1(perk.transform, d.PerkText(Mathf.Max(1, level)) + (hired && !maxed ? "  →  " + d.PerkText(level + 1).Split(' ')[0] : ""), 28, BGood, TextAnchor.MiddleCenter, true);
            UIKit.Stretch(pt.gameObject, 8);
            var bl = T(card.transform, d.Blurb, 24, BLabel, TextAnchor.UpperCenter, false, UIKit.Heading);
            UIKit.Size(bl.gameObject, -1, 62);
            Flex(card.transform);
            if (!maxed)
            {
                var sh = HBox(card.transform, 8, 0, TextAnchor.MiddleLeft, -1, 34);
                var fill = Track(sh.transform, 30, shards >= need ? BGood : Palette.Hex(0x6FB2F2));
                UIKit.SetBar(fill, need > 0 ? Mathf.Min(1, shards / (float)need) : 1);
                var st = T(fill.transform.parent, shards + " / " + need + " shards", 22, Color.white, TextAnchor.MiddleCenter, true);
                UIKit.Stretch(st.gameObject);
            }
            string id = d.Id;
            var btns = HBox(card.transform, 10, 0, TextAnchor.MiddleCenter, -1, 84);
            if (!maxed && shards >= need) Big(btns.transform, hired ? "Train up" : "Recruit", UIKit.Green, () => Result(S.TrainEngineer(id), Sfx.LevelUp), 360, 80, true, 36);
            else if (hired)
            {
                var on = e.trainUid != 0 ? S.Train(e.trainUid) : null;
                if (target != null && (on == null || on.uid != target.uid))
                    Big(btns.transform, "Ride " + GameSession.Name(target).Split(' ')[0], BBlue, () => { Result(S.AssignEngineer(id, target.uid), Sfx.Click); CloseWindow(); }, 360, 80, true, 32);
                else if (on != null)
                {
                    T1(btns.transform, "Riding " + GameSession.Name(on).Split(' ')[0], 28, Color.white, TextAnchor.MiddleCenter, true);
                    Big(btns.transform, "Rest", UIKit.WoodLight, () => Result(S.AssignEngineer(id, 0), Sfx.Click), 130, 72, true, 28);
                }
                else
                {
                    var free = S.State.trains.Where(t => S.EngineerOn(t) == null).OrderBy(t => t.platform).FirstOrDefault();
                    if (free != null) Big(btns.transform, "Ride " + GameSession.Name(free).Split(' ')[0], BBlue, () => Result(S.AssignEngineer(id, free.uid), Sfx.Click), 360, 80, true, 32);
                    else T1(btns.transform, "In the bunkhouse", 28, BLabel, TextAnchor.MiddleCenter, false, UIKit.Heading);
                }
            }
            else T1(btns.transform, "Shards come from chests, crates and the peddler", 24, BLabel, TextAnchor.MiddleCenter, false, UIKit.Heading);
        }

        /// <summary>The engineer chip on the dispatch board's locomotive card.</summary>
        void EngineerChip(Transform p, TrainState t)
        {
            var e = S.EngineerOn(t);
            var chip = Box(p, BChip, -1, 54, "Engineer");
            UIKit.Size(chip.gameObject, -1, 54, 1);
            chip.raycastTarget = true;
            Row(chip.gameObject, 8, new RectOffset(4, 12, 3, 3));
            int uid = t.uid;
            if (e != null)
            {
                var d = Frontier.Engineer(e.id);
                EngineerPortrait(chip.transform, e.id, 48);
                UIKit.Size(T1(chip.transform, d.Name.Split(' ')[0] + " · " + d.PerkText(e.level), 24, BGood, TextAnchor.MiddleLeft, true).gameObject, -1, -1, 1);
            }
            else UIKit.Size(T1(chip.transform, "+ Add an engineer", 26, BLabel, TextAnchor.MiddleCenter, true).gameObject, -1, -1, 1);
            Clickable(chip.gameObject, () => OpenCrew(uid));
        }

        // ================================================================ Traveling peddler

        public void OpenPeddler() { ShowBoard("Traveling Peddler", PeddlerBoard, 1700, 820); }

        void PeddlerBoard(Transform body, Transform right)
        {
            if (!S.PeddlerHere) { CloseWindow(); Toast("The peddler has moved on. He'll be back in a few hours.", true); return; }
            _boardSub = "Three deals, one hour";
            var pill = Pill(right, tr => ClockIcon(tr, 40), "", 30);
            var pt = pill.GetComponentInChildren<Text>();
            Action live = () => pt.text = "Leaves in " + Fmt.Time(S.PeddlerLeft);
            live(); Live(live);
            var panel = Box(body, BDeep, -1, -1, "Panel");
            UIKit.Stretch(panel.gameObject);
            Row(panel.gameObject, 18, new RectOffset(18, 18, 18, 18)).childForceExpandHeight = true;
            var hero = Box(panel.transform, BCardDeep, 360, -1, "Peddler");
            Stack(hero.gameObject, 10, new RectOffset(16, 16, 20, 20), TextAnchor.MiddleCenter);
            var art = UIKit.Node("Art", hero.transform);
            UIKit.Size(art, -1, 300);
            Overlay(art.transform, Rays(), new Color(1, 1, 1, 0.5f));
            var ic = UIKit.Node("Icon", art.transform);
            UIKit.Stretch(ic);
            UIKit.HStack(ic, 0, 0, TextAnchor.MiddleCenter);
            UIKit.Icon(ic.transform, "peddler", 280);
            var q = T(hero.transform, "“Finest wares west of the Missouri, friend. Today only!”", 30, Color.white, TextAnchor.MiddleCenter, false, UIKit.Heading);
            UIKit.Size(q.gameObject, -1, 130);
            for (int i = 0; i < S.State.deals.Count; i++) DealCard(panel.transform, i);
        }

        void DealCard(Transform p, int i)
        {
            var d = S.State.deals[i];
            var card = Box(p, BCard, -1, -1, "Deal");
            UIKit.Size(card.gameObject, -1, -1, 1);
            Stack(card.gameObject, 10, new RectOffset(16, 16, 16, 16), TextAnchor.UpperCenter);
            var art = UIKit.Node("Art", card.transform);
            UIKit.Size(art, -1, 260);
            Overlay(art.transform, Rays(), new Color(1, 1, 1, d.sold ? 0.1f : 0.6f));
            var ic = UIKit.Node("Icon", art.transform);
            UIKit.Stretch(ic);
            UIKit.HStack(ic, 0, 0, TextAnchor.MiddleCenter);
            switch (d.kind)
            {
                case DealKind.Blueprint: LocoArt(ic.transform, GameDatabase.Locomotive(d.id), 330, 170); break;
                case DealKind.Shards: EngineerPortrait(ic.transform, d.id, 200); break;
                case DealKind.Fuel: UIKit.Icon(ic.transform, "shop_fuel", 200); break;
                default: UIKit.ResIcon(ic.transform, d.id, 170); break;
            }
            T1(card.transform, GameSession.DealText(d), 34, Color.white, TextAnchor.MiddleCenter, true);
            string tag = d.kind == DealKind.Goods ? "20% under market" : d.kind == DealKind.Parts ? "Hard to find" : d.kind == DealKind.Blueprint ? "Rare plans" : d.kind == DealKind.Shards ? "Crew contract" : "Full bunker";
            T1(card.transform, tag, 26, BGold, TextAnchor.MiddleCenter, false, UIKit.Heading);
            Flex(card.transform);
            int idx = i;
            if (d.sold) T(card.transform, "SOLD", 44, BGood, TextAnchor.MiddleCenter, true);
            else if (d.nuggets > 0) PriceBtn(card.transform, "Buy", UIKit.Green, tr => UIKit.NuggetIcon(tr, 46), d.nuggets.ToString(), () => Result(S.BuyDeal(idx), Sfx.Coin), 320, 92, S.Nuggets >= d.nuggets);
            else PriceBtn(card.transform, "Buy", UIKit.Green, tr => UIKit.CoinIcon(tr, 46), Fmt.Num(d.coins), () => Result(S.BuyDeal(idx), Sfx.Coin), 320, 92, S.Coins >= d.coins);
        }

        static Vector3 PeddlerPosition { get { return Station.DepotPosition + new Vector3(30, 10, -2); } }

        // ================================================================ Full Steam gauge

        void BuildComboHud()
        {
            var bg = UIKit.KitImage(_safe, "tile_dark", "FullSteam", 64);
            _combo = bg.gameObject;
            UIKit.Place(_combo, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 196), new Vector2(560, 86));
            HudShadow(_combo);
            Row(_combo, 10, new RectOffset(8, 18, 6, 6));
            UIKit.Icon(_combo.transform, "nav_trains", 70);
            var col = VBox(_combo.transform, 2, 0, TextAnchor.MiddleLeft, -1, -1, 1);
            col.GetComponent<VerticalLayoutGroup>().childForceExpandHeight = false;
            _comboText = T1(col.transform, "", 28, BGold, TextAnchor.MiddleLeft, true);
            UIKit.Size(_comboText.gameObject, -1, 36);
            var tr = HBox(col.transform, 0, 0, TextAnchor.MiddleLeft, -1, 20);
            _comboFill = Track(tr.transform, 18, BGold);
            Clickable(_combo, () => { var next = S.State.trains.Where(t => t.IsIdle && !S.HasArrived(t) && S.WagonsOf(t).Count > 0).OrderBy(t => t.platform).FirstOrDefault(); if (next != null) OpenTrain(next.uid); });
            _combo.SetActive(false);
        }

        void RefreshComboHud()
        {
            if (_combo == null) return;
            bool idle = S.State.trains.Any(t => t.IsIdle && !S.HasArrived(t) && S.WagonsOf(t).Count > 0);
            bool on = S.ComboLeft > 0 && S.Level >= Frontier.ComboLevel && idle;
            _combo.SetActive(on);
            if (!on) return;
            float now = S.ComboNow, next = S.NextCombo;
            _comboText.text = S.State.comboCount <= 1
                ? "Send another train for FULL STEAM ×" + next.ToString("0.0")
                : "FULL STEAM ×" + now.ToString("0.0") + "  ·  next train ×" + next.ToString("0.0");
            UIKit.SetBar(_comboFill, (float)(S.ComboLeft / Frontier.ComboWindow));
        }

        // ================================================================ Museum

        public void OpenMuseum() { ShowBoard("Frontier Museum", MuseumBoard); }

        void MuseumBoard(Transform body, Transform right)
        {
            _boardSub = S.State.curios.Count + " / " + Frontier.Curios.Count + " curios";
            Pill(right, null, "Each outpost hides one rare find", 30);
            var panel = Box(body, BDeep, -1, -1, "Panel");
            UIKit.Stretch(panel.gameObject);
            Row(panel.gameObject, 16, new RectOffset(16, 16, 16, 16)).childForceExpandHeight = true;
            foreach (var s in Frontier.Shelves) ShelfCard(panel.transform, s);
        }

        void ShelfCard(Transform p, ShelfDef s)
        {
            bool done = S.IsShelfComplete(s), claimed = S.IsShelfClaimed(s);
            var card = Box(p, done && !claimed ? Palette.Hex(0x3B3426) : BCard, -1, -1, "Shelf");
            UIKit.Size(card.gameObject, -1, -1, 1);
            if (done && !claimed) { var o = card.gameObject.AddComponent<Outline>(); o.effectColor = BGold; o.effectDistance = new Vector2(4, -4); }
            Stack(card.gameObject, 10, new RectOffset(16, 16, 16, 16));
            var top = HBox(card.transform, 10, 0, TextAnchor.MiddleLeft, -1, 52);
            UIKit.Size(T1(top.transform, s.Name, 38, BGold, TextAnchor.MiddleLeft, true).gameObject, -1, -1, 1);
            if (claimed) CheckBadge(top.transform, 48);
            var grid = VBox(card.transform, 10, 0, TextAnchor.UpperCenter, -1, -1, -1, 1);
            var list = s.Curios.ToList();
            for (int i = 0; i < list.Count; i += 2)
            {
                var row = HBox(grid.transform, 10, 0, TextAnchor.UpperCenter, -1, 216);
                row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
                for (int j = i; j < Mathf.Min(list.Count, i + 2); j++) CurioSlot(row.transform, list[j]);
            }
            var perk = HBox(card.transform, 8, 0, TextAnchor.MiddleCenter, -1, 50);
            Pill(perk.transform, Ico("xp", 36), s.PerkText, 24, BGold, BChip);
            Pill(perk.transform, tr => UIKit.NuggetIcon(tr, 36), "+" + s.Nuggets, 24, null, BChip);
            var btn = HBox(card.transform, 0, 0, TextAnchor.MiddleCenter, -1, 84);
            string id = s.Id;
            if (claimed) T(btn.transform, "Bonus active", 34, BGood, TextAnchor.MiddleCenter, true);
            else if (done) Big(btn.transform, "Claim shelf", UIKit.Green, () => { var r = S.ClaimShelf(id); Result(r, Sfx.LevelUp); if (r.Ok) Celebrations.Instance.Confetti(); }, 340, 80, true, 38);
            else T1(btn.transform, S.ShelfOwned(s) + " / " + list.Count + " found", 30, BLabel, TextAnchor.MiddleCenter, false, UIKit.Heading);
        }

        void CurioSlot(Transform p, CurioDef c)
        {
            bool got = S.HasCurio(c.Id);
            var d = GameDatabase.Destination(c.DestinationId);
            var slot = Box(p, got ? BTile : BChip, -1, 216, "Curio");
            UIKit.Size(slot.gameObject, -1, 216, 1);
            Stack(slot.gameObject, 2, new RectOffset(6, 6, 8, 8), TextAnchor.UpperCenter);
            var art = HBox(slot.transform, 0, 0, TextAnchor.MiddleCenter, -1, 120);
            var ic = UIKit.Icon(art.transform, c.Id, 116);
            if (ic != null && !got) ic.GetComponent<Image>().color = new Color(0, 0, 0, 0.7f);
            T1(slot.transform, got ? c.Name : "?", 24, got ? Color.white : BLabel, TextAnchor.MiddleCenter, got);
            T1(slot.transform, got ? c.Blurb : "Found at " + d.Name, 20, BLabel, TextAnchor.MiddleCenter, false, UIKit.Heading);
            if (!got && S.IsDestinationUnlocked(d))
            {
                slot.raycastTarget = true;
                string dest = d.Id;
                Clickable(slot.gameObject, () => OpenTrainTo(dest));
            }
        }

        void OnCurioFound(CurioDef c)
        {
            AudioManager.Play(Sfx.LevelUp);
            if (WindowOpen) return;
            ShowBoard("A Curio!", (body, right) =>
            {
                _boardSub = "From " + GameDatabase.Destination(c.DestinationId).Name;
                var panel = Box(body, BDeep, -1, -1, "Panel");
                UIKit.Stretch(panel.gameObject);
                Stack(panel.gameObject, 10, new RectOffset(20, 20, 16, 20), TextAnchor.UpperCenter);
                var art = UIKit.Node("Art", panel.transform);
                UIKit.Size(art, -1, -1, 1, 1);
                Overlay(art.transform, Rays(), Color.white);
                var h = UIKit.Node("Icon", art.transform);
                UIKit.Stretch(h);
                UIKit.HStack(h, 0, 0, TextAnchor.MiddleCenter);
                UIKit.Icon(h.transform, c.Id, 260);
                T1(panel.transform, c.Name, 52, BGold, TextAnchor.MiddleCenter, true);
                T1(panel.transform, c.Blurb, 30, BLabel, TextAnchor.MiddleCenter, false, UIKit.Heading);
                var bar = HBox(panel.transform, 16, 0, TextAnchor.MiddleCenter, -1, 96);
                Big(bar.transform, "To the museum", UIKit.Green, OpenMuseum, 420, 92, true, 38);
            }, 1100, 780);
        }

        // ================================================================ New territory

        public void OpenCharter()
        {
            _charterArmed = false;
            ShowBoard("New Territory", CharterBoard, 1800, 820);
        }

        void CharterBoard(Transform body, Transform right)
        {
            _boardSub = "Charter " + S.NextTownName;
            var panel = Box(body, BDeep, -1, -1, "Panel");
            UIKit.Stretch(panel.gameObject);
            Row(panel.gameObject, 20, new RectOffset(20, 20, 20, 20)).childForceExpandHeight = true;
            var ph = Photo(panel.transform, Resources.Load<Texture2D>("UI/LoadingArt"), 700, -1);
            Overlay(ph.transform, Fade(true), new Color(0.08f, 0.1f, 0.15f, 0.9f));
            var nm = T(ph.transform, S.NextTownName, 64, BGold, TextAnchor.LowerCenter, true);
            UIKit.Anchor(nm.gameObject, Vector2.zero, Vector2.one, new Vector2(10, 20), new Vector2(-10, -10));

            var col = VBox(panel.transform, 12, 0, TextAnchor.UpperLeft, -1, -1, 1);
            col.GetComponent<VerticalLayoutGroup>().childForceExpandHeight = false;
            int now = Mathf.RoundToInt(Frontier.CharterBonus * 100 * (S.State.territory - 1));
            int next = now + Mathf.RoundToInt(Frontier.CharterBonus * 100);
            UIKit.Size(T(col.transform, "The spike is driven. Head west and found a new town.", 38, Color.white, TextAnchor.MiddleLeft, true).gameObject, -1, 56);
            CharterLine(col.transform, BGood, "Forever bonus", "+" + next + "% income, cargo and XP" + (now > 0 ? " (now +" + now + "%)" : "") + ", plus " + Frontier.CharterNuggets + " nuggets");
            CharterLine(col.transform, Color.white, "You keep", "every locomotive and wagon, nuggets, blueprints, engineers, the album, the museum and your purchases");
            CharterLine(col.transform, BBad, "Starts over", "your town, level, coins, goods, orders, chapters and territory projects");
            Flex(col.transform);
            var btns = HBox(col.transform, 16, 0, TextAnchor.MiddleLeft, -1, 104);
            if (!_charterArmed) Big(btns.transform, "Charter " + S.NextTownName, UIKit.Gold, () => { _charterArmed = true; RebuildWindow(); }, 560, 100, true, 42);
            else
            {
                Big(btns.transform, "Yes, start over", UIKit.Red, () =>
                {
                    var r = S.CharterNewTerritory();
                    if (!r.Ok) { Result(r); return; }
                    GameRoot.PendingWelcome = r.Message;
                    CloseWindow();
                    GameRoot.Instance.Reload();
                }, 460, 100, true, 42);
                Big(btns.transform, "Not yet", UIKit.WoodLight, () => { _charterArmed = false; RebuildWindow(); }, 260, 100, true, 38);
            }
        }

        static void CharterLine(Transform p, Color c, string head, string text)
        {
            var row = Box(p, BCard, -1, 112, "Line");
            Stack(row.gameObject, 2, new RectOffset(20, 20, 10, 10));
            UIKit.Size(T(row.transform, head, 30, c, TextAnchor.MiddleLeft, true).gameObject, -1, 40);
            var t = T(row.transform, text, 28, BLabel, TextAnchor.UpperLeft, false, UIKit.Heading);
            UIKit.Size(t.gameObject, -1, 56);
        }
    }
}
