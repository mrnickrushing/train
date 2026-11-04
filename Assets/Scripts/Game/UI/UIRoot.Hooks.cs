using System;
using System.Collections.Generic;
using System.Linq;
using Ironhorse.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ironhorse.Game
{
    /// <summary>
    /// The daily hooks: the payday calendar, the "while you were away" card, mystery crates, express telegrams,
    /// map events (travelers, tumbleweeds, gold strikes, bandits) and the Roundhouse album in the Depot.
    /// </summary>
    public sealed partial class UIRoot
    {
        GameObject _payday;
        Text _paydayBadge, _paydayCaption, _depotBadge;
        RectTransform _eventRail;
        string _railKey;
        readonly List<Action> _railLive = new List<Action>();
        readonly Queue<Action> _welcome = new Queue<Action>();
        float _welcomeWait;
        int _eventUid;

        // ================================================================ HUD

        void BuildHooksHud()
        {
            // Payday, above the Foreman on the left.
            _payday = UIKit.Node("Payday", _safe);
            UIKit.Place(_payday, new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 372), new Vector2(150, 150));
            var tile = HudTile(_payday.transform, "Daily", t => UIKit.Icon(t, "payday", 116), 140);
            _paydayCaption = tile.GetComponentInChildren<Text>();
            Clickable(_payday, () => OpenDaily(S.CanClaimPayday || S.DailyClaimable == 0 ? "payday" : "tasks"));
            _paydayBadge = UIKit.Badge(tile.transform, "!", UIKit.Red);

            // Telegram and territory events, down the right under the fuel gauge.
            var rail = UIKit.Node("Events", _safe);
            UIKit.Place(rail, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -180), new Vector2(360, 320));
            var v = UIKit.VStack(rail, 12, 0, TextAnchor.UpperRight);
            v.childForceExpandWidth = false;
            v.childForceExpandHeight = false;
            _eventRail = UIKit.RT(rail);
        }

        void RefreshHooksHud()
        {
            if (_payday == null) return;
            bool on = S.State.settings.tutorialDone || S.Level >= 3;
            _payday.SetActive(on);
            bool can = S.CanClaimPayday || S.DailyClaimable > 0;
            _paydayBadge.text = S.CanClaimPayday ? "!" : S.DailyClaimable.ToString();
            _paydayBadge.transform.parent.gameObject.SetActive(can);
            _paydayCaption.text = can ? "DAILY" : Fmt.Time(S.NextPaydayIn);
            if (can) _payday.transform.localScale = Vector3.one * (1 + Mathf.Sin(Time.unscaledTime * 5) * 0.03f);
            else _payday.transform.localScale = Vector3.one;
            if (_depotBadge != null)
            {
                int n = S.AlbumClaimable;
                _depotBadge.text = n.ToString();
                _depotBadge.transform.parent.gameObject.SetActive(n > 0);
            }
            RefreshRail();
        }

        void RefreshRail()
        {
            var tg = S.Telegram;
            var events = S.State.events.Where(e => (e.kind == MapEventKind.GoldStrike || e.kind == MapEventKind.Bandits) && S.EventLeft(e) > 0).ToList();
            bool peddler = S.PeddlerHere;
            string key = (peddler ? "P" : "") + (tg != null ? tg.uid.ToString() : "-") + ":" + string.Join(",", events.Select(e => e.uid));
            if (key != _railKey)
            {
                _railKey = key;
                _railLive.Clear();
                ClearChildren(_eventRail);
                if (tg != null) TelegramChip(_eventRail, tg);
                if (peddler) PeddlerChip(_eventRail);
                foreach (var e in events.Take(peddler && tg != null ? 1 : 2)) EventChip(_eventRail, e);
            }
            foreach (var a in _railLive) a();
        }

        /// <summary>A dark HUD strip with an icon, two lines and a timer on the right.</summary>
        Text RailStrip(Transform p, string icon, string top, Color topColor, string line, Action onClick, float h = 88)
        {
            var bg = UIKit.KitImage(p, "tile_dark", "Strip", 64);
            bg.color = new Color(1, 1, 1, 0.95f);
            UIKit.Size(bg.gameObject, 360, h);
            HudShadow(bg.gameObject);
            Row(bg.gameObject, 10, new RectOffset(6, 14, 4, 4));
            var ic = HBox(bg.transform, 0, 0, TextAnchor.MiddleCenter, h - 6, h - 6);
            UIKit.Icon(ic.transform, icon, h - 10);
            var col = VBox(bg.transform, 0, 0, TextAnchor.MiddleLeft, -1, -1, 1);
            col.GetComponent<VerticalLayoutGroup>().childForceExpandHeight = false;
            UIKit.Size(T1(col.transform, top, 22, topColor, TextAnchor.MiddleLeft, true).gameObject, -1, 26);
            UIKit.Size(T1(col.transform, line, 26, Color.white, TextAnchor.MiddleLeft, true).gameObject, -1, 32);
            var timer = T1(col.transform, "", 22, BLabel, TextAnchor.MiddleLeft, true);
            UIKit.Size(timer.gameObject, -1, 26);
            Clickable(bg.gameObject, onClick);
            return timer;
        }

        void TelegramChip(Transform p, ContractState c)
        {
            var r = c.requirements[0];
            var timer = RailStrip(p, "telegram", "TELEGRAM · TRIPLE PAY", BGold, c.client + ": " + r.amount + " " + ResName(r.id), () => { _ordersTab = "orders"; OpenContracts(); }, 100);
            int uid = c.uid;
            _railLive.Add(() =>
            {
                var x = S.State.contracts.FirstOrDefault(k => k.uid == uid);
                if (x == null) return;
                bool can = S.CanDeliver(x);
                timer.text = can ? "Ready to deliver!" : Fmt.Time(S.TelegramLeft(x)) + " left · have " + S.Amount(r.id) + "/" + r.amount;
                timer.color = can ? BGood : S.TelegramLeft(x) < 900 ? BBad : BLabel;
            });
        }

        void PeddlerChip(Transform p)
        {
            var timer = RailStrip(p, "peddler", "TRAVELING PEDDLER", BGold, "3 deals at the station", OpenPeddler);
            _railLive.Add(() => timer.text = "Leaves in " + Fmt.Time(S.PeddlerLeft));
        }

        void EventChip(Transform p, MapEventState e)
        {
            var d = GameDatabase.Destination(e.destinationId);
            bool gold = e.kind == MapEventKind.GoldStrike;
            int uid = e.uid;
            var timer = RailStrip(p, gold ? "gold_ore" : "bandit", gold ? "GOLD STRIKE · ×2 CARGO" : "BANDITS ON THE LINE", gold ? BGold : BBad, d.Name, () => OpenEvent(uid));
            _railLive.Add(() =>
            {
                var x = S.MapEvent(uid);
                if (x != null) timer.text = Fmt.Time(S.EventLeft(x)) + " left";
            });
        }

        // ================================================================ welcome sequence

        /// <summary>Queued on launch: the away summary, then the payday calendar, then any featured offer.</summary>
        public void QueueWelcome()
        {
            if (S.Away != null) _welcome.Enqueue(ShowAway);
            _welcome.Enqueue(() => { if (S.CanClaimPayday && S.State.settings.tutorialDone) OpenPayday(); });
            _welcomeWait = 0.6f;
        }

        void UpdateWelcome()
        {
            if (_welcome.Count == 0 || WindowOpen || Celebrations.Instance == null || Celebrations.Instance.Busy) { _welcomeWait = 0.6f; return; }
            _welcomeWait -= Time.unscaledDeltaTime;
            if (_welcomeWait > 0) return;
            _welcomeWait = 0.6f;
            _welcome.Dequeue()();
        }

        // ================================================================ Payday

        public void OpenPayday() { OpenDaily("payday"); }

        void PaydayBoard(Transform body, Transform right)
        {
            int today = S.PaydayDay, streak = S.PaydayStreak;
            bool can = S.CanClaimPayday;
            _boardSub = streak > 0 ? streak + " day streak" : "Start a streak";
            var pill = Pill(right, tr => ClockIcon(tr, 40), "", 30);
            var pt = pill.GetComponentInChildren<Text>();
            Action live = () => pt.text = S.CanClaimPayday ? "Today's pay is ready" : "Next payday in " + Fmt.Time(S.NextPaydayIn);
            live(); Live(live);

            var panel = Box(body, BDeep, -1, -1, "Panel");
            UIKit.Stretch(panel.gameObject);
            Stack(panel.gameObject, 16, new RectOffset(20, 20, 18, 20), TextAnchor.UpperCenter);
            string note = S.PaydayStreakLost && can
                ? "You missed a day, so the streak starts over. Come back tomorrow to keep it going!"
                : "Come back every day for your pay. Day 7 pays locomotive blueprints. Miss a day and the streak starts over.";
            T1(panel.transform, note, 32, BLabel, TextAnchor.MiddleCenter, false, UIKit.Heading);

            var row = HBox(panel.transform, 14, 0, TextAnchor.MiddleCenter, -1, -1, -1, 1);
            var hl = row.GetComponent<HorizontalLayoutGroup>();
            hl.childForceExpandHeight = true;
            for (int day = 1; day <= Hooks.PaydayDays; day++) DayCard(row.transform, day, today, can);

            var bar = HBox(panel.transform, 0, 0, TextAnchor.MiddleCenter, -1, 104);
            if (can)
                Big(bar.transform, "Collect Day " + today, UIKit.Green, () =>
                {
                    var r = S.ClaimPayday();
                    Result(r, Sfx.LevelUp);
                    if (r.Ok) Celebrations.Instance.Confetti();
                }, 520, 100, true, 46);
            else Big(bar.transform, "Come back tomorrow", UIKit.WoodLight, CloseWindow, 520, 100, true, 40);
        }

        void DayCard(Transform p, int day, int today, bool can)
        {
            bool done = day < today || (day == today && !can);
            bool now = day == today && can;
            bool seven = day == Hooks.PaydayDays;
            var g = S.PaydayGrant(day);
            var card = UIKit.Panel(p, now ? BGold : seven ? Palette.Hex(0x3B3426) : BCard, "Day", UIKit.RoundedGrad);
            card.pixelsPerUnitMultiplier = 1.6f;
            card.raycastTarget = false;
            UIKit.Size(card.gameObject, -1, -1, seven ? 1.7f : 1);
            if (seven && !now)
            {
                var o = card.gameObject.AddComponent<Outline>();
                o.effectColor = BGold;
                o.effectDistance = new Vector2(3, -3);
            }
            Stack(card.gameObject, 6, new RectOffset(8, 8, 14, 14), TextAnchor.UpperCenter);
            T(card.transform, "DAY " + day, 38, now ? Palette.Hex(0x4A3306) : seven ? BGold : Color.white, TextAnchor.MiddleCenter, !now);

            var art = UIKit.Node("Art", card.transform);
            UIKit.Size(art, -1, -1, 1, 1);
            if (now || seven) Overlay(art.transform, Rays(), new Color(1, 1, 1, now ? 0.9f : 0.5f));
            var holder = UIKit.Node("Icon", art.transform);
            UIKit.Stretch(holder);
            UIKit.HStack(holder, -8, 0, TextAnchor.MiddleCenter);
            string caption = GrantArt(holder.transform, g, seven ? 150 : 120);
            var cap = Box(card.transform, now ? new Color(0, 0, 0, 0.3f) : BChip, -1, 54, "Amount");
            var ct = T1(cap.transform, caption, 30, Color.white, TextAnchor.MiddleCenter, true);
            UIKit.Stretch(ct.gameObject, 6);

            if (done)
            {
                var dim = UIKit.Panel(card.transform, new Color(0.04f, 0.05f, 0.08f, 0.6f), "Done", UIKit.RoundedGrad);
                dim.pixelsPerUnitMultiplier = 1.6f;
                dim.raycastTarget = false;
                UIKit.Stretch(dim.gameObject);
                dim.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
                var ck = CheckBadge(dim.transform, 86);
                UIKit.Place(ck, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(86, 86));
            }
        }

        /// <summary>Big icons for a reward, and its caption ("+1,000", "2 Sundown plans").</summary>
        static string GrantArt(Transform p, Grant g, float size)
        {
            if (g.BlueprintLoco != null && g.Blueprints > 0)
            {
                var loco = GameDatabase.Locomotive(g.BlueprintLoco);
                LocoArt(p, loco, size * 1.5f, size * 0.8f);
                return g.Blueprints + " " + ShortLoco(loco) + (g.Blueprints == 1 ? " plan" : " plans");
            }
            var parts = new List<string>();
            if (g.Coins > 0) { if (UIKit.Icon(p, "shop_coins", size) == null) UIKit.CoinIcon(p, size); parts.Add(Fmt.Num(g.Coins)); }
            if (g.Nuggets > 0) { if (UIKit.Icon(p, g.Nuggets >= 10 ? "shop_nuggets_2" : "shop_nuggets_1", g.Fuel > 0 ? size * 0.75f : size) == null) UIKit.NuggetIcon(p, size); parts.Add(g.Nuggets + " nuggets"); }
            if (g.Fuel > 0) { if (UIKit.Icon(p, g.Nuggets > 0 ? "fuel" : "shop_fuel", g.Nuggets > 0 ? size * 0.75f : size) == null) FuelIcon(p, size); parts.Add(g.Fuel + " fuel"); }
            foreach (var r in g.Resources) { UIKit.ResIcon(p, r.id, size * 0.85f); parts.Add(r.amount + " " + ResName(r.id)); }
            return string.Join(" + ", parts);
        }

        // ================================================================ While you were away

        public void ShowAway()
        {
            if (S.Away == null) return;
            ShowBoard("While You Were Away", AwayBoard, 1760, 800);
        }

        void AwayBoard(Transform body, Transform right)
        {
            var a = S.Away;
            if (a == null) { CloseWindow(); return; }
            _boardSub = "Gone " + Fmt.Time(a.Seconds);
            var panel = Box(body, BDeep, -1, -1, "Panel");
            UIKit.Stretch(panel.gameObject);
            Stack(panel.gameObject, 16, new RectOffset(20, 20, 20, 20), TextAnchor.UpperCenter);
            T1(panel.transform, "Your railroad kept busy while you were gone.", 34, BLabel, TextAnchor.MiddleCenter, false, UIKit.Heading);

            var row = HBox(panel.transform, 16, 0, TextAnchor.MiddleCenter, -1, -1, -1, 1);
            var hl = row.GetComponent<HorizontalLayoutGroup>();
            hl.childForceExpandHeight = true;
            hl.childForceExpandWidth = true;
            if (a.Trains > 0)
            {
                var col = AwayTile(row.transform, "nav_trains", a.Trains + (a.Trains > 1 ? " trains arrived" : " train arrived"));
                var wells = HBox(col, 10, 0, TextAnchor.MiddleCenter, -1, 110);
                foreach (var r in a.Cargo.Take(4)) { var id = r.id; Well(wells.transform, 104, tr => UIKit.ResIcon(tr, id, 70), "+" + Fmt.Num(r.amount)); }
                if (a.Fares > 0) Well(wells.transform, 104, tr => UIKit.CoinIcon(tr, 70), "+" + Fmt.Num(a.Fares));
            }
            if (a.Rent > 0)
            {
                var col = AwayTile(row.transform, "shop_coins", Fmt.Num(a.Rent) + " rent collected");
                T1(col, "from " + S.State.buildings.Count(b => GameDatabase.Building(b.defId).Category == BuildingCategory.Residential) + " homes in " + S.State.townName, 28, BLabel, TextAnchor.MiddleCenter, false, UIKit.Heading);
            }
            if (a.Goods.Count > 0)
            {
                var col = AwayTile(row.transform, a.Goods[0].id, a.Goods.Total() + " goods made");
                var wells = HBox(col, 10, 0, TextAnchor.MiddleCenter, -1, 110);
                foreach (var r in a.Goods.Take(4)) { var id = r.id; Well(wells.transform, 104, tr => UIKit.ResIcon(tr, id, 70), "+" + Fmt.Num(r.amount)); }
            }

            var extras = new List<string>();
            if (a.Crates > 0) extras.Add(a.Crates == 1 ? "A mystery crate is waiting on the platform!" : a.Crates + " mystery crates are waiting on the platform!");
            if (a.Robbed > 0) extras.Add("Bandits made off with " + a.Robbed + " cargo.");
            foreach (var c in a.Curios) extras.Add("Found a curio: " + c.Name + "!");
            if (extras.Count > 0) T1(panel.transform, string.Join("  ", extras), 30, a.Robbed > 0 && a.Crates == 0 ? BBad : BGold, TextAnchor.MiddleCenter, true);

            var bar = HBox(panel.transform, 16, 0, TextAnchor.MiddleCenter, -1, 104);
            Big(bar.transform, "Collect All", UIKit.Green, () =>
            {
                var r = S.CollectAll();
                Result(r, Sfx.Coin);
                if (r.Ok) Fx.Burst(CameraRig.Instance.Focus + Vector3.up * 10, 60);
                CloseWindow();
            }, 520, 100, true, 46);
        }

        Transform AwayTile(Transform p, string icon, string title)
        {
            var tile = Box(p, BCard, -1, -1, "Tile");
            UIKit.Size(tile.gameObject, -1, -1, 1);
            Stack(tile.gameObject, 12, new RectOffset(16, 16, 20, 20), TextAnchor.UpperCenter);
            var art = UIKit.Node("Art", tile.transform);
            UIKit.Size(art, -1, 190);
            Overlay(art.transform, Rays(), new Color(1, 1, 1, 0.45f));
            var ic = UIKit.Node("Icon", art.transform);
            UIKit.Stretch(ic);
            UIKit.HStack(ic, 0, 0, TextAnchor.MiddleCenter);
            if (UIKit.Icon(ic.transform, icon, 170) == null) UIKit.ResIcon(ic.transform, icon, 150);
            T1(tile.transform, title, 44, Color.white, TextAnchor.MiddleCenter, true);
            return tile.transform;
        }

        // ================================================================ Mystery crates

        void OpenCrateOn(int trainUid, Vector3 at)
        {
            var r = S.OpenCrate(trainUid);
            if (!r.Ok) { Result(r); return; }
            AudioManager.Play(Sfx.LevelUp);
            Fx.Burst(at, 50);
            var g = S.LastCrate;
            ShowBoard("Mystery Crate!", (body, right) => CrateBoard(body, g), 1100, 760);
        }

        void CrateBoard(Transform body, Grant g)
        {
            _boardSub = "Found on the platform";
            var panel = Box(body, BDeep, -1, -1, "Panel");
            UIKit.Stretch(panel.gameObject);
            Stack(panel.gameObject, 12, new RectOffset(20, 20, 16, 20), TextAnchor.UpperCenter);
            var art = UIKit.Node("Art", panel.transform);
            UIKit.Size(art, -1, -1, 1, 1);
            Overlay(art.transform, Rays(), Color.white);
            var holder = UIKit.Node("Icon", art.transform);
            UIKit.Stretch(holder);
            UIKit.HStack(holder, 0, 0, TextAnchor.MiddleCenter);
            string caption;
            if (S.LastCrateShards != null && S.LastShardsEngineer != null)
            {
                EngineerPortrait(holder.transform, S.LastShardsEngineer, 230);
                caption = S.LastCrateShards.TrimStart('+');
            }
            else caption = GrantArt(holder.transform, g, 230);
            if (g.Coins > 0 && g.Nuggets == 0 && g.Fuel == 0) caption += " coins";
            bool jackpot = g.Coins > 0 || g.Blueprints > 0;
            T1(panel.transform, jackpot ? "Jackpot!" : "Inside the crate:", 36, BGold, TextAnchor.MiddleCenter, true);
            T1(panel.transform, caption, 54, Color.white, TextAnchor.MiddleCenter, true);
            var bar = HBox(panel.transform, 0, 0, TextAnchor.MiddleCenter, -1, 100);
            Big(bar.transform, "Collect", UIKit.Green, CloseWindow, 420, 96, true, 44);
            if (jackpot) Celebrations.Instance.Confetti();
        }

        /// <summary>Where a train's crate marker floats: over the middle of the consist.</summary>
        static Vector3 CratePosition(TrainView view) { return view.MidPosition + Vector3.up * 7; }

        // ================================================================ Express telegrams

        void OnTelegram(ContractState c)
        {
            AudioManager.Play(Sfx.Bell);
            Toast("Telegram! " + GameSession.TelegramText(c), true);
        }

        /// <summary>Header for an express order card: a red telegram band with the countdown.</summary>
        void TelegramHead(Transform card, ContractState c)
        {
            var head = Photo(card, BackdropFor(c.requirements[0].id), -1, 140, new Color(0.75f, 0.6f, 0.5f));
            Overlay(head.transform, Fade(false), new Color(0.32f, 0.09f, 0.06f, 0.97f));
            var kind = T(head.transform, "Express telegram · triple pay", 26, BGold, TextAnchor.UpperLeft, true, UIKit.Heading);
            UIKit.Anchor(kind.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(20, -46), new Vector2(-150, -12));
            var who = T1(head.transform, c.client, 40, Color.white, TextAnchor.UpperLeft, true);
            UIKit.Anchor(who.gameObject, new Vector2(0, 0), new Vector2(1, 1), new Vector2(20, 50), new Vector2(-150, -50));
            var timer = T(head.transform, "", 28, Color.white, TextAnchor.LowerLeft, true);
            UIKit.Anchor(timer.gameObject, new Vector2(0, 0), new Vector2(1, 0), new Vector2(20, 10), new Vector2(-150, 46));
            int uid = c.uid;
            Action live = () =>
            {
                var x = S.State.contracts.FirstOrDefault(k => k.uid == uid);
                if (x != null) timer.text = Fmt.Time(S.TelegramLeft(x)) + " left";
            };
            live(); Live(live);
            var big = UIKit.Icon(head.transform, "telegram", 118);
            if (big != null) UIKit.Place(big, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(124, 124));
        }

        // ================================================================ Map events

        void OnMapEvent(MapEventState e)
        {
            var d = GameDatabase.Destination(e.destinationId);
            switch (e.kind)
            {
                case MapEventKind.GoldStrike: AudioManager.Play(Sfx.Bell); Toast("Gold strike rumor at " + d.Name + "! Double cargo from there for an hour.", true); break;
                case MapEventKind.Bandits: Toast("Bandits spotted on the " + d.Name + " line!", false); break;
                case MapEventKind.Traveler: Toast("A stranded traveler is waiting at the station.", true); break;
                default: Toast("Tumbleweeds are blowing through town. Tap them for coins!", true); break;
            }
        }

        void OpenEvent(int uid)
        {
            var e = S.MapEvent(uid);
            if (e == null) return;
            _eventUid = uid;
            ShowBoard(Hooks.EventName(e.kind), EventBoard, 1640, 800);
        }

        void EventBoard(Transform body, Transform right)
        {
            var e = S.MapEvent(_eventUid);
            if (e == null || S.EventLeft(e) <= 0) { CloseWindow(); return; }
            var d = GameDatabase.Destination(e.destinationId);
            bool gold = e.kind == MapEventKind.GoldStrike;
            _boardSub = d.Name;
            var pill = Pill(right, tr => ClockIcon(tr, 40), "", 30);
            var pt = pill.GetComponentInChildren<Text>();
            int uid = e.uid;
            Action live = () => { var x = S.MapEvent(uid); if (x != null) pt.text = "Ends in " + Fmt.Time(S.EventLeft(x)); };
            live(); Live(live);

            var panel = Box(body, BDeep, -1, -1, "Panel");
            UIKit.Stretch(panel.gameObject);
            Row(panel.gameObject, 20, new RectOffset(20, 20, 20, 20)).childForceExpandHeight = true;
            var ph = Photo(panel.transform, Postcard(d.Id), 760, -1, gold ? (Color?)null : new Color(0.75f, 0.65f, 0.6f));
            Overlay(ph.transform, Fade(true), new Color(0.08f, 0.1f, 0.15f, 0.85f));
            var big = UIKit.Icon(ph.transform, gold ? "gold_ore" : "bandit", 260);
            if (big != null) UIKit.Place(big, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(260, 260));

            var col = VBox(panel.transform, 18, 0, TextAnchor.UpperLeft, -1, -1, 1);
            col.GetComponent<VerticalLayoutGroup>().childForceExpandHeight = false;
            var head = T(col.transform, gold ? "Strike it rich!" : "Trouble on the line", 52, gold ? BGold : BBad, TextAnchor.MiddleLeft, true);
            UIKit.Size(head.gameObject, -1, 70);
            var txt = T(col.transform, GameSession.EventText(e), 34, Color.white, TextAnchor.UpperLeft, false, UIKit.Heading);
            UIKit.Size(txt.gameObject, -1, 130);
            string more = gold
                ? "Send every train you can spare to " + d.Name + " before the rush is over."
                : "Each train that runs it has a " + Mathf.RoundToInt((float)Hooks.BanditChance * 100) + "% chance of losing " + Mathf.RoundToInt(Hooks.BanditLoss * 100) + "% of its cargo. Hire the marshal's posse to clear them out.";
            var mt = T(col.transform, more, 30, BLabel, TextAnchor.UpperLeft, false, UIKit.Heading);
            UIKit.Size(mt.gameObject, -1, 130);
            Flex(col.transform);
            var btns = HBox(col.transform, 16, 0, TextAnchor.MiddleLeft, -1, 104);
            string dest = d.Id;
            if (gold) Big(btns.transform, "Send a train", UIKit.Green, () => OpenTrainTo(dest), 460, 100, true, 44);
            else
            {
                int cost = S.EscortCost;
                PriceBtn(btns.transform, "Hire escort", UIKit.Green, tr => UIKit.CoinIcon(tr, 50), Fmt.Num(cost), () =>
                {
                    var r = S.PayEscort(uid);
                    Result(r, Sfx.Deliver);
                    if (r.Ok) CloseWindow();
                }, 500, 100, S.Coins >= cost);
                Big(btns.transform, "Risk it", UIKit.WoodLight, () => OpenTrainTo(dest), 260, 100, true, 40);
            }
        }

        /// <summary>World markers for the traveler at the station and tumbleweeds rolling through town.</summary>
        void UpdateEventMarkers()
        {
            foreach (var e in S.State.events)
            {
                if (S.EventLeft(e) <= 0) continue;
                int uid = e.uid;
                if (e.kind == MapEventKind.Traveler)
                {
                    var pos = Station.DepotPosition + new Vector3(-14 + (e.spot % 5) * 7, 13, -3);
                    ShowMarker(pos, "traveler", h => UIKit.Icon(h, "traveler", 64), Fmt.Time(S.EventLeft(e)), -1, () => ClaimMapEvent(uid, pos), true);
                }
                else if (e.kind == MapEventKind.Tumbleweed)
                {
                    var pos = TumbleweedPosition(e);
                    ShowMarker(pos, "tumbleweed", h => UIKit.Icon(h, "tumbleweed", 64), "", -1, () => ClaimMapEvent(uid, pos), false);
                    var m = _markerPool[_markerUsed - 1];
                    if (m.Icon != null) m.Icon.transform.localRotation = Quaternion.Euler(0, 0, -Time.unscaledTime * 140);
                }
            }
        }

        void UpdatePeddlerMarker()
        {
            if (!S.PeddlerHere) return;
            ShowMarker(PeddlerPosition, "peddler", h => UIKit.Icon(h, "peddler", 66), Fmt.Time(S.PeddlerLeft), -1, OpenPeddler, true);
        }

        void ClaimMapEvent(int uid, Vector3 at)
        {
            var r = S.ClaimEvent(uid);
            Result(r, Sfx.Coin);
            if (r.Ok) Fx.Burst(at, 30);
        }

        /// <summary>Tumbleweeds bounce across town with the wind, looping every 50 seconds.</summary>
        static Vector3 TumbleweedPosition(MapEventState e)
        {
            var town = WorldLayout.TownRect;
            float lane = (e.spot % 97) / 97f;
            float k = Mathf.Repeat((float)(Time.unscaledTimeAsDouble / 50.0) + (e.spot % 13) / 13f, 1);
            float x = Mathf.Lerp(town.xMin - 20, town.xMax + 20, k);
            float z = Mathf.Lerp(town.yMin + 20, town.yMax - 20, lane) + Mathf.Sin(k * 9) * 6;
            float hop = Mathf.Abs(Mathf.Sin(k * 60)) * 2.5f;
            return new Vector3(x, ValleyTerrain.Height(x, z) + 3 + hop, z);
        }

        // ================================================================ Roundhouse album

        void AlbumDepot(Transform body)
        {
            int total = Hooks.AlbumSets.Sum(s => s.Count);
            int owned = Hooks.AlbumSets.Sum(s => S.AlbumOwned(s));
            _boardSub = "Album · " + owned + " / " + total;
            var panel = Box(body, BDeep, -1, -1, "Panel");
            UIKit.Stretch(panel.gameObject);
            Stack(panel.gameObject, 0, new RectOffset(16, 16, 16, 16));
            var cards = Scroller(panel.transform, "album", true, 16);
            foreach (var s in Hooks.AlbumSets.OrderBy(x => S.IsAlbumClaimed(x) ? 2 : S.IsAlbumComplete(x) ? 0 : 1)) AlbumCard(cards, s);
        }

        void AlbumCard(Transform p, AlbumSetDef s)
        {
            int have = S.AlbumOwned(s);
            bool done = S.IsAlbumComplete(s), claimed = S.IsAlbumClaimed(s);
            var card = Box(p, done && !claimed ? Palette.Hex(0x3B3426) : BCard, 600, -1, "Set");
            if (done && !claimed)
            {
                var o = card.gameObject.AddComponent<Outline>();
                o.effectColor = BGold;
                o.effectDistance = new Vector2(4, -4);
            }
            Stack(card.gameObject, 10, new RectOffset(20, 20, 18, 18), TextAnchor.UpperLeft);
            var top = HBox(card.transform, 10, 0, TextAnchor.MiddleLeft, -1, 56);
            UIKit.Size(T1(top.transform, s.Name, 40, BGold, TextAnchor.MiddleLeft, true).gameObject, -1, -1, 1);
            if (claimed) CheckBadge(top.transform, 50);
            T1(card.transform, s.Description, 26, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);

            var grid = VBox(card.transform, 10, 0, TextAnchor.UpperLeft, -1, -1, -1, 1);
            var ids = s.Members.ToList();
            for (int i = 0; i < ids.Count; i += 3)
            {
                var row = HBox(grid.transform, 10, 0, TextAnchor.UpperLeft, -1, 150);
                for (int j = i; j < Mathf.Min(ids.Count, i + 3); j++) AlbumSlot(row.transform, ids[j]);
            }

            var prog = HBox(card.transform, 10, 0, TextAnchor.MiddleLeft, -1, 40);
            var tr = HBox(prog.transform, 0, 0, TextAnchor.MiddleLeft, -1, 26, 1);
            UIKit.SetBar(Track(tr.transform, 24, done ? BGreen : BGold), have / (float)s.Count);
            T(prog.transform, have + " / " + s.Count, 30, Color.white, TextAnchor.MiddleRight, true);

            var perk = HBox(card.transform, 10, 0, TextAnchor.MiddleLeft, -1, 54);
            Pill(perk.transform, Ico("xp", 40), s.PerkText + " forever", 28, BGold, BChip);
            Pill(perk.transform, tr2 => UIKit.NuggetIcon(tr2, 40), "+" + s.Nuggets, 28, null, BChip);

            var btn = HBox(card.transform, 0, 0, TextAnchor.MiddleCenter, -1, 90);
            string id = s.Id;
            if (claimed) T(btn.transform, "Bonus active", 36, BGood, TextAnchor.MiddleCenter, true);
            else if (done) Big(btn.transform, "Claim set", UIKit.Green, () => { var r = S.ClaimAlbumSet(id); Result(r, Sfx.LevelUp); if (r.Ok) Celebrations.Instance.Confetti(); }, 380, 86, true, 42);
            else T1(btn.transform, AlbumHint(s), 28, BLabel, TextAnchor.MiddleCenter, false, UIKit.Heading);
        }

        void AlbumSlot(Transform p, string defId)
        {
            var loco = GameDatabase.Locomotive(defId);
            var wagon = loco == null ? GameDatabase.Wagon(defId) : null;
            bool got = S.InAlbum(defId);
            var slot = Box(p, got ? BTile : BChip, 176, 150, "Slot");
            Stack(slot.gameObject, 2, new RectOffset(6, 6, 6, 6), TextAnchor.UpperCenter);
            var art = HBox(slot.transform, 0, 0, TextAnchor.MiddleCenter, -1, 100);
            var g = loco != null ? LocoArt(art.transform, loco, 160, 90) : WagonArt(art.transform, wagon, 150, 90);
            if (!got) g.color = new Color(0, 0, 0, 0.7f);
            string name = loco != null ? ShortLoco(loco) : wagon.Name;
            T1(slot.transform, got ? name : "?", 24, got ? Color.white : BLabel, TextAnchor.MiddleCenter, got);
            if (loco != null)
            {
                string id = loco.Id;
                slot.raycastTarget = true;
                Clickable(slot.gameObject, () => { _depotTab = "locos"; _depotLoco = id; RebuildWindow(); });
            }
        }

        /// <summary>A locomotive's name without its wheel arrangement ("Copper Queen 4-6-0" is "Copper Queen").</summary>
        static string ShortLoco(LocomotiveDef d)
        {
            return string.Join(" ", d.Name.Split(' ').Where(w => !(w.Length > 2 && char.IsDigit(w[0]) && w.Contains("-"))));
        }

        /// <summary>How to get the next missing piece of a set.</summary>
        string AlbumHint(AlbumSetDef s)
        {
            var next = s.Members.FirstOrDefault(m => !S.InAlbum(m));
            if (next == null) return "";
            var loco = GameDatabase.Locomotive(next);
            if (loco != null)
            {
                if (loco.Blueprints > 0) return "Next: " + ShortLoco(loco) + " · " + S.BlueprintsOf(loco.Id) + "/" + loco.Blueprints + " plans";
                return S.IsLocoUnlocked(loco) ? "Next: buy the " + ShortLoco(loco) : "Next: " + ShortLoco(loco) + " at level " + loco.UnlockLevel;
            }
            var w = GameDatabase.Wagon(next);
            return S.IsWagonUnlocked(w) ? "Next: buy a " + w.Name : "Next: " + w.Name + " at level " + w.UnlockLevel;
        }
    }

    /// <summary>Gently bobs a UI element up and down (event pins on the map).</summary>
    public sealed class Bob : MonoBehaviour
    {
        Vector2 _home;
        bool _set;
        void Update()
        {
            var rt = (RectTransform)transform;
            if (!_set) { _home = rt.anchoredPosition; _set = true; }
            rt.anchoredPosition = _home + new Vector2(0, Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3)) * 8);
        }
    }
}
