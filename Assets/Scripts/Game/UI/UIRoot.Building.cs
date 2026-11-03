using System;
using System.Linq;
using Ironhorse.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ironhorse.Game
{
    /// <summary>
    /// One building as a board: the building's art and upgrade on the left; on the right what it does:
    /// a factory's recipe and conveyor, a home's rent, a civic building's effect, or construction progress.
    /// </summary>
    public sealed partial class UIRoot
    {
        public void OpenBuilding(int uid)
        {
            var b0 = S.Building(uid);
            if (b0 == null) return;
            CameraRig.Instance.FlyTo(WorldLayout.Plots[b0.plot], 60);
            ShowBoard(GameDatabase.Building(b0.defId).Name, (body, right) => BuildingBoard(body, right, uid));
        }

        void BuildingBoard(Transform body, Transform right, int uid)
        {
            var b = S.Building(uid);
            if (b == null) { CloseWindow(); return; }
            var d = GameDatabase.Building(b.defId);
            bool ready = S.IsReady(b);
            _boardSub = ready ? "Level " + b.level : "Under construction";

            if (ready && d.Category == BuildingCategory.Production)
            {
                var crew = Pill(right, t => CrewIcon(t), "Crew " + S.CrewFree + " / " + S.CrewTotal + " free", 32);
                crew.name = "Crew";
                if (S.State.hiredCrew < Balance.MaxHiredCrew)
                    PriceBtn(right, "Hire", Premium, t => UIKit.NuggetIcon(t, 40), S.HireCrewCost.ToString(), () => Result(S.HireCrew(), Sfx.Build), 290, 80, S.Nuggets >= S.HireCrewCost, 34);
            }
            else if (ready && d.Category == BuildingCategory.Residential)
                Big(right, "Collect all rent", UIKit.Gold, () =>
                {
                    int got = S.CollectAllRent();
                    if (got > 0) { AudioManager.Play(Sfx.Coin); Toast("+" + got + " coins collected", true); }
                }, 380, 80, true, 34);

            var row = UIKit.Node("Row", body);
            UIKit.Stretch(row);
            Row(row, 12, new RectOffset(0, 0, 0, 0)).childForceExpandHeight = true;
            BuildingHero(row.transform, b, d, ready);
            var panel = Box(row.transform, BTile, -1, -1, "Panel");
            UIKit.Size(panel.gameObject, -1, -1, 1);
            Stack(panel.gameObject, 14, new RectOffset(26, 26, 20, 22));

            if (!ready) ConstructionPanel(panel.transform, b, uid);
            else if (d.Category == BuildingCategory.Production) FactoryPanel(panel.transform, b, d, uid);
            else if (d.Category == BuildingCategory.Residential) RentPanel(panel.transform, b, d, uid);
            else EffectPanel(panel.transform, b, d);
        }

        // ---------------------------------------------------------------- left: art, level, upgrade

        void BuildingHero(Transform p, BuildingState b, BuildingDef d, bool ready)
        {
            var card = Box(p, BCardDeep, 580, -1, "Hero");
            Stack(card.gameObject, 12, new RectOffset(18, 18, 16, 18), TextAnchor.UpperLeft);
            var sky = UIKit.Node("Sky", card.transform).AddComponent<Image>();
            sky.sprite = Sky();
            sky.raycastTarget = false;
            UIKit.Size(sky.gameObject, -1, 260);
            var art = BuildingArt(sky.transform, d, 390, 244);
            UIKit.Place(art.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(390, 244));
            if (!ready) art.color = new Color(0.62f, 0.6f, 0.58f);

            var lv = HBox(card.transform, 12, 0, TextAnchor.MiddleLeft, -1, 40);
            T(lv.transform, "Level", 32, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading).horizontalOverflow = HorizontalWrapMode.Overflow;
            Segs(lv.transform, Balance.MaxBuildingLevel, b.level, 0, 24);
            T(lv.transform, b.level + " / " + Balance.MaxBuildingLevel, 32, Color.white, TextAnchor.MiddleRight, true).horizontalOverflow = HorizontalWrapMode.Overflow;
            string about = d.Category == BuildingCategory.Residential ? "Home to " + Mathf.RoundToInt(d.Population * Balance.BuildingLevelMultiplier(b.level)) + " settlers who pay rent." : d.Blurb;
            var blurb = T(card.transform, about, 30, BLabel, TextAnchor.UpperLeft, false, UIKit.Heading);
            UIKit.Size(blurb.gameObject, -1, 76);
            Flex(card.transform);

            if (!ready) return;
            if (!S.CanUpgradeBuilding(b))
            {
                if (d.Category != BuildingCategory.Decoration) Big(card.transform, "Max level", UIKit.Disabled, null, -1, 88, false, 38);
                return;
            }
            var up = Box(card.transform, BChip, -1, -1, "Upgrade");
            Stack(up.gameObject, 10, new RectOffset(16, 16, 12, 14), TextAnchor.UpperLeft);
            T1(up.transform, "Upgrade to level " + (b.level + 1), 34, BGold, TextAnchor.MiddleLeft, true);
            string what = d.Category == BuildingCategory.Residential ? "+25% settlers and rent" : d.Category == BuildingCategory.Production ? "+25% speed and +1 batch" : "+25% effect";
            T1(up.transform, what, 30, Color.white, TextAnchor.MiddleLeft, false, UIKit.Heading);
            var res = S.BuildingUpgradeResources(b).Where(r => r.amount > 0).ToArray();
            if (res.Length > 0)
            {
                var cost = HBox(up.transform, 8, 0, TextAnchor.MiddleLeft, -1, 50);
                foreach (var r in res) Pill(cost.transform, Ico(r.id, 36), S.Amount(r.id) + " / " + r.amount, 28, S.Amount(r.id) >= r.amount ? Color.white : BBad);
            }
            int coins = S.BuildingUpgradeCoins(b);
            bool can = S.Coins >= coins && res.All(r => S.Amount(r.id) >= r.amount);
            int uid = b.uid;
            PriceBtn(up.transform, "Upgrade", can ? UIKit.Green : UIKit.Disabled, t => UIKit.CoinIcon(t, 44), Fmt.Num(coins), () => Result(S.UpgradeBuilding(uid), Sfx.Build), -1, 88, true, 38);
        }

        // ---------------------------------------------------------------- factory: recipe and conveyor

        void FactoryPanel(Transform p, BuildingState b, BuildingDef d, int uid)
        {
            var r = d.Recipe;
            var outRes = GameDatabase.Resource(r.Output.id);
            T(p, "Recipe", 34, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);
            var rec = HBox(p, 14, 0, TextAnchor.MiddleLeft, -1, 176);
            foreach (var i in r.Inputs) RecipeTile(rec.transform, i.id, S.Amount(i.id) + " / " + i.amount, S.Amount(i.id) >= i.amount ? BGood : BBad, BCardDeep, true);
            T(rec.transform, "»", 70, BGold, TextAnchor.MiddleCenter, true);
            RecipeTile(rec.transform, r.Output.id, "+" + r.Output.amount, Color.white, Palette.Hex(0x4B5A3C), false);
            Flex(rec.transform);
            var info = VBox(rec.transform, 10, 0, TextAnchor.MiddleRight, 250);
            Pill(info.transform, t => ClockIcon(t, 36), Fmt.Time(S.ProductionSeconds(b, 1)) + " a batch", 30);
            Pill(info.transform, Ico(r.Output.id, 40), "Have " + Fmt.Num(S.Amount(r.Output.id)), 30);

            int slots = S.QueueSlots(b);
            var hdr = HBox(p, 12, 0, TextAnchor.MiddleLeft, -1, 44);
            T(hdr.transform, "Production", 34, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading).horizontalOverflow = HorizontalWrapMode.Overflow;
            Flex(hdr.transform);
            T(hdr.transform, b.queue.Count + " / " + slots + " slots in use", 30, Color.white, TextAnchor.MiddleRight, true).horizontalOverflow = HorizontalWrapMode.Overflow;

            int shown = slots + (b.extraSlots < Balance.MaxExtraSlots ? 1 : 0);
            float size = Mathf.Min(176, (1210 - (shown - 1) * 12) / (float)shown);
            var belt = Box(p, BChip, -1, size + 28, "Conveyor");
            Row(belt.gameObject, 12, new RectOffset(14, 14, 14, 14), TextAnchor.MiddleLeft);
            for (int i = 0; i < shown; i++)
            {
                if (i < b.queue.Count) JobSlot(belt.transform, b.queue[i], r.Output.id, size);
                else if (i < slots)
                {
                    var box = Box(belt.transform, new Color(1, 1, 1, 0.06f), size, size, "Empty");
                    box.raycastTarget = true;
                    var plus = T(box.transform, "+", size * 0.5f, new Color(1, 1, 1, 0.45f), TextAnchor.MiddleCenter, true);
                    UIKit.Stretch(plus.gameObject);
                    Clickable(box.gameObject, () => Result(S.Enqueue(uid), Sfx.Build));
                }
                else
                {
                    var box = Box(belt.transform, Premium, size, size, "Buy slot");
                    box.raycastTarget = true;
                    Stack(box.gameObject, 4, new RectOffset(6, 6, 14, 14), TextAnchor.MiddleCenter);
                    T1(box.transform, "ADD SLOT", 30, Color.white, TextAnchor.MiddleCenter, true);
                    var pr = HBox(box.transform, 4, 0, TextAnchor.MiddleCenter, -1, 50);
                    UIKit.NuggetIcon(pr.transform, 42);
                    T(pr.transform, S.ExtraSlotCost(b).ToString(), 34, Color.white, TextAnchor.MiddleLeft, true).horizontalOverflow = HorizontalWrapMode.Overflow;
                    Clickable(box.gameObject, () => Result(S.BuySlot(uid), Sfx.Build));
                }
            }

            Flex(p);
            int afford = S.AffordableBatches(b);
            if (afford == 0 && b.queue.Count < slots)
            {
                string why = S.CrewFree == 0 ? "All hands are busy. Collect finished goods or hire more crew." : "Not enough materials. Send trains to gather them.";
                var hint = HBox(p, 12, 0, TextAnchor.MiddleLeft, -1, 50);
                T1(hint.transform, why, 30, BBad, TextAnchor.MiddleLeft, false, UIKit.Heading);
                if (S.CrewFree > 0)
                {
                    var need = r.Inputs.Where(i => S.Amount(i.id) < i.amount).Select(i => i.id).FirstOrDefault();
                    if (need != null) Big(hint.transform, "Get " + GameDatabase.Resource(need).Name, BBlue, () => GoGet(need), 320, 70, true, 32);
                }
            }
            var acts = HBox(p, 14, 0, TextAnchor.MiddleLeft, -1, 100);
            Big(acts.transform, "Queue 1", UIKit.Green, () => Result(S.Enqueue(uid), Sfx.Build), 230, 96, afford > 0, 40);
            if (afford > 1) Big(acts.transform, "Fill all (" + afford + ")", UIKit.Green, () => Result(S.StartProduction(uid, afford), Sfx.Build), 270, 96, true, 40);
            Flex(acts.transform);
            int done = S.FinishedJobs(b);
            if (done > 0) PriceBtn(acts.transform, "Collect", UIKit.Gold, Ico(r.Output.id, 44), "+" + r.Output.amount * done, () => Result(S.CollectProduction(uid)), 300, 96, true, 40);
            if (S.IsProducing(b))
            {
                int rush = S.RushProductionCost(b);
                PriceBtn(acts.transform, "Speed up", BBlue, t => UIKit.NuggetIcon(t, 44), rush.ToString(), () => Result(S.RushProduction(uid), Sfx.Click), 300, 96, S.Nuggets >= rush, 38);
            }
        }

        void RecipeTile(Transform p, string resId, string amount, Color amountColor, Color bg, bool tapToGet)
        {
            var tile = Box(p, bg, 200, 176, "Res " + resId);
            Stack(tile.gameObject, 2, new RectOffset(8, 8, 10, 10), TextAnchor.MiddleCenter);
            var ic = HBox(tile.transform, 0, 0, TextAnchor.MiddleCenter, -1, 90);
            UIKit.ResIcon(ic.transform, resId, 80);
            T1(tile.transform, amount, 40, amountColor, TextAnchor.MiddleCenter, true);
            T1(tile.transform, GameDatabase.Resource(resId).Name, 26, BLabel, TextAnchor.MiddleCenter, false, UIKit.Heading);
            if (tapToGet)
            {
                tile.raycastTarget = true;
                Clickable(tile.gameObject, () => GoGet(resId));
            }
        }

        void JobSlot(Transform p, ProductionJob job, string outId, float size)
        {
            bool finished = job.endsAt <= S.Now;
            var box = Box(p, finished ? Palette.Hex(0x4B5A3C) : BCardDeep, size, size, "Job");
            Stack(box.gameObject, 4, new RectOffset(10, 10, 10, 12), TextAnchor.MiddleCenter);
            var ic = HBox(box.transform, 0, 0, TextAnchor.MiddleCenter, -1, size * 0.48f);
            UIKit.ResIcon(ic.transform, outId, size * 0.42f);
            if (finished)
            {
                var chk = CheckBadge(box.transform, 40);
                var le = chk.GetComponent<LayoutElement>();
                if (le == null) le = chk.AddComponent<LayoutElement>();
                le.ignoreLayout = true;
                UIKit.Place(chk, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-6, -6), new Vector2(40, 40));
            }
            var lbl = T(box.transform, "", size * 0.2f, finished ? BGood : Color.white, TextAnchor.MiddleCenter, true);
            UIKit.Size(lbl.gameObject, -1, size * 0.22f);
            var bar = Track(box.transform, 14, BGreen);
            Action live = () =>
            {
                double left = job.endsAt - S.Now;
                bool queued = S.Now < job.startsAt;
                lbl.text = left <= 0 ? "Ready" : queued ? "Queued" : Fmt.Time(left);
                double total = job.endsAt - job.startsAt;
                UIKit.SetBar(bar, left <= 0 ? 1 : queued || total <= 0 ? 0 : (float)((S.Now - job.startsAt) / total));
            };
            live(); Live(live);
        }

        // ---------------------------------------------------------------- home: rent

        void RentPanel(Transform p, BuildingState b, BuildingDef d, int uid)
        {
            var stats = HBox(p, 14, 0, TextAnchor.MiddleLeft, -1, 64);
            Pill(stats.transform, Ico("people", 50), Mathf.RoundToInt(d.Population * Balance.BuildingLevelMultiplier(b.level)) + " settlers", 34);
            Pill(stats.transform, t => UIKit.CoinIcon(t, 46), S.RentPerMinute(b).ToString("0.0") + " a minute", 34);

            var card = Box(p, BCardDeep, -1, -1, "Rent");
            Stack(card.gameObject, 14, new RectOffset(24, 24, 20, 24), TextAnchor.UpperLeft);
            T(card.transform, "Rent waiting", 34, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);
            var amt = HBox(card.transform, 14, 0, TextAnchor.MiddleLeft, -1, 110);
            UIKit.CoinIcon(amt.transform, 100);
            var big = T(amt.transform, "", 84, Color.white, TextAnchor.MiddleLeft, true);
            big.horizontalOverflow = HorizontalWrapMode.Overflow;
            var bar = Track(card.transform, 34, BGold);
            var note = T(card.transform, "", 30, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);
            Action live = () =>
            {
                var bb = S.Building(uid);
                if (bb == null) return;
                big.text = Fmt.Num(S.PendingRent(bb));
                UIKit.SetBar(bar, S.RentFill(bb));
                note.text = S.RentFill(bb) >= 1 ? "The strongbox is full. Collect before rent goes to waste." : "Settlers pay into the strongbox for up to " + Fmt.Time(S.RentStorageSeconds) + ".";
            };
            live(); Live(live);
            Flex(p);
            var foot = HBox(p, 0, 0, TextAnchor.MiddleCenter, -1, 100);
            Big(foot.transform, "Collect", UIKit.Green, () => Result(S.CollectRent(uid)), 520, 100, S.PendingRent(b) > 0, 44);
        }

        // ---------------------------------------------------------------- civic and decor: effect

        void EffectPanel(Transform p, BuildingState b, BuildingDef d)
        {
            var card = Box(p, BCardDeep, -1, -1, "Effect");
            Stack(card.gameObject, 14, new RectOffset(24, 24, 20, 24), TextAnchor.UpperLeft);
            T(card.transform, d.Category == BuildingCategory.Decoration ? "Town pride" : "Town effect", 34, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);
            string now = EffectText(d, b.level);
            T(card.transform, now ?? d.Blurb, now != null ? 64 : 40, BGold, TextAnchor.MiddleLeft, true);
            if (now != null) T(card.transform, d.Blurb, 32, Color.white, TextAnchor.MiddleLeft, false, UIKit.Heading);
            if (now != null && S.CanUpgradeBuilding(b)) T(card.transform, "Level " + (b.level + 1) + ": " + EffectText(d, b.level + 1), 32, BGood, TextAnchor.MiddleLeft, false, UIKit.Heading);
            if (d.Category == BuildingCategory.Decoration) T(card.transform, "Decorations make the town nicer to look at and count toward its population goals.", 32, Color.white, TextAnchor.MiddleLeft, false, UIKit.Heading);
            Flex(p);
        }

        /// <summary>"+15% train speed" for percentage effects at a level; null for fixed or no effect.</summary>
        static string EffectText(BuildingDef d, int level)
        {
            int pct = Mathf.RoundToInt(d.EffectValue * Balance.BuildingLevelMultiplier(level) * 100);
            switch (d.Effect)
            {
                case CivicEffect.TrainSpeed: return "+" + pct + "% train speed";
                case CivicEffect.Income: return "+" + pct + "% town income";
                case CivicEffect.Xp: return "+" + pct + "% XP";
                case CivicEffect.Yield: return "+" + pct + "% cargo per trip";
                default: return null;
            }
        }

        // ---------------------------------------------------------------- under construction

        void ConstructionPanel(Transform p, BuildingState b, int uid)
        {
            var card = Box(p, BCardDeep, -1, -1, "Construction");
            Stack(card.gameObject, 16, new RectOffset(24, 24, 22, 26), TextAnchor.UpperLeft);
            T(card.transform, "Crews are hard at work", 48, BGold, TextAnchor.MiddleLeft, true);
            var tr = HBox(card.transform, 12, 0, TextAnchor.MiddleLeft, -1, 50);
            ClockIcon(tr.transform, 46);
            var bar = Track(tr.transform, 36, BGold);
            var left = T(card.transform, "", 40, Color.white, TextAnchor.MiddleLeft, true);
            Action live = () =>
            {
                var bb = S.Building(uid);
                if (bb == null) return;
                UIKit.SetBar(bar, S.ConstructionProgress(bb));
                left.text = Fmt.Time(S.ConstructionRemaining(bb)) + " left";
            };
            live(); Live(live);
            Flex(p);
            int cost = Balance.RushCost(S.ConstructionRemaining(b));
            PriceBtn(p, "Finish now", BBlue, t => UIKit.NuggetIcon(t, 44), cost.ToString(), () => Result(S.RushConstruction(uid), Sfx.Build), 460, 100, S.Nuggets >= cost, 40);
        }
    }
}
