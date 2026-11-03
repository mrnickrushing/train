using System;
using System.Linq;
using Ironhorse.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ironhorse.Game
{
    /// <summary>The smaller screens as boards: Foreman's Orders, the Territory map, the Ledger, Settings and the Land Office.</summary>
    public sealed partial class UIRoot
    {
        // ================================================================ Foreman's Orders

        public void OpenForeman() { ShowBoard("Foreman's Orders", ForemanBoard); }

        void ForemanBoard(Transform body, Transform right)
        {
            var set = S.CurrentTaskSet;
            _boardSub = "Set " + (set.Index + 1);
            Pill(right, null, S.TasksDone + " / " + set.Tasks.Length + " done", 32);

            var row = UIKit.Node("Row", body);
            UIKit.Stretch(row);
            Row(row, 12, new RectOffset(0, 0, 0, 0)).childForceExpandHeight = true;

            var hero = Box(row.transform, BCardDeep, 470, -1, "Hank");
            var sky = UIKit.Node("Sky", hero.transform).AddComponent<Image>();
            sky.sprite = Sky();
            sky.raycastTarget = false;
            UIKit.Stretch(sky.gameObject, 14);
            sky.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            var tex = Resources.Load<Texture2D>("UI/Story/char_foreman");
            if (tex != null)
            {
                var img = UIKit.Node("Portrait", sky.transform).AddComponent<Image>();
                img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0));
                img.preserveAspect = true;
                img.raycastTarget = false;
                UIKit.Place(img.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, -150), new Vector2(760 * tex.width / (float)tex.height, 760));
            }
            var tag = UIKit.KitImage(hero.transform, "btn_gold", "Name", 64);
            UIKit.Place(tag.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(400, 70));
            var tn = T(tag.transform, "HANK · STATIONMASTER", 32, Palette.Hex(0x3B2A05), TextAnchor.MiddleCenter);
            UIKit.Anchor(tn.gameObject, Vector2.zero, Vector2.one, new Vector2(8, 10), new Vector2(-8, -2));

            var panel = Box(row.transform, BTile, -1, -1, "Panel");
            UIKit.Size(panel.gameObject, -1, -1, 1);
            Stack(panel.gameObject, 14, new RectOffset(24, 24, 20, 20));
            T1(panel.transform, set.Title, 48, BGold, TextAnchor.MiddleLeft, true);
            var bubble = UIKit.Panel(panel.transform, UIKit.Parchment, "Quote", UIKit.RoundedGrad);
            bubble.pixelsPerUnitMultiplier = 1.4f;
            UIKit.Size(bubble.gameObject, -1, 96);
            var q = T(bubble.transform, "“" + set.Quote + "”", 32, UIKit.Ink, TextAnchor.MiddleLeft, false, UIKit.BodyBold);
            UIKit.Stretch(q.gameObject, 18);

            for (int i = 0; i < set.Tasks.Length; i++)
            {
                bool done = S.IsTaskDone(i);
                var tr = Box(panel.transform, done ? Palette.Hex(0x3E5A33) : BCardDeep, -1, 92, "Task");
                Row(tr.gameObject, 16, new RectOffset(18, 20, 0, 0));
                if (done) CheckBadge(tr.transform, 56);
                else { var ring = Box(tr.transform, BChip, 56, 56, "Open"); ring.sprite = UIKit.Circle; }
                var lbl = T1(tr.transform, DepthContent.Describe(set.Tasks[i]), 36, Color.white, TextAnchor.MiddleLeft, true);
                UIKit.Size(lbl.gameObject, -1, -1, 1);
                var bar = Track(tr.transform, 40, done ? BGood : BGold, 330);
                var bt = T(bar.transform.parent, "", 30, Color.white, TextAnchor.MiddleCenter, true);
                UIKit.Stretch(bt.gameObject);
                int idx = i;
                Action live = () =>
                {
                    int p = S.TaskProgress(idx), c = S.CurrentTaskSet.Tasks[idx].Count;
                    UIKit.SetBar(bar, p / (float)c);
                    bt.text = Fmt.Num(Mathf.Min(p, c)) + " / " + Fmt.Num(c);
                };
                live(); Live(live);
            }

            Flex(panel.transform);
            var foot = Box(panel.transform, BChip, -1, 112, "Reward");
            Row(foot.gameObject, 12, new RectOffset(20, 14, 0, 0));
            T(foot.transform, "Reward for all three", 32, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading).horizontalOverflow = HorizontalWrapMode.Overflow;
            GrantPills(foot.transform, set.Reward, 60 + 30 * set.Index);
            Flex(foot.transform);
            Big(foot.transform, "Claim", S.CanClaimTaskSet ? UIKit.Green : UIKit.Disabled, () => Result(S.ClaimTaskSet(), Sfx.LevelUp), 260, 88, S.CanClaimTaskSet, 40);
        }

        /// <summary>A grant's coins, nuggets, fuel and goods (plus XP) as small pills.</summary>
        static void GrantPills(Transform p, Grant g, int xp)
        {
            if (g.Coins > 0) Pill(p, t => UIKit.CoinIcon(t, 42), Fmt.Num(g.Coins), 30, null, BTile);
            if (g.Nuggets > 0) Pill(p, t => UIKit.NuggetIcon(t, 42), Fmt.Num(g.Nuggets), 30, null, BTile);
            if (g.Fuel > 0) Pill(p, Ico("fuel", 42), Fmt.Num(g.Fuel), 30, null, BTile);
            foreach (var r in g.Resources) Pill(p, Ico(r.id, 42), Fmt.Num(r.amount), 30, null, BTile);
            if (g.Blueprints > 0 && g.BlueprintLoco != null)
            {
                var loco = GameDatabase.Locomotive(g.BlueprintLoco);
                Pill(p, t => LocoArt(t, loco, 64, 36), g.Blueprints + " plans", 30, null, BTile);
            }
            if (xp > 0) Pill(p, t => UIDeco.XpIcon(t, 42), xp.ToString(), 30, null, BTile);
        }

        // ================================================================ Territory

        public void OpenTerritory() { ShowBoard("The Territory", TerritoryBoard); }

        void TerritoryBoard(Transform body, Transform right)
        {
            int open = GameDatabase.Destinations.Count(d => S.IsDestinationUnlocked(d));
            _boardSub = open + " of " + GameDatabase.Destinations.Count + " outposts";
            Big(right, "All trains", UIKit.WoodLight, OpenFleet, 280, 80, true, 34);

            var map = UIKit.Panel(body, Palette.Hex(0xE3CC97), "Map", UIKit.RoundedGrad);
            map.pixelsPerUnitMultiplier = 1.6f;
            UIKit.Stretch(map.gameObject);
            map.gameObject.AddComponent<RectMask2D>();
            var area = UIKit.Node("Area", map.transform);
            UIKit.Stretch(area, 60);
            var rng = new System.Random(3);
            for (int i = 0; i < 30; i++)
            {
                var blob = UIKit.Panel(map.transform, i % 3 == 0 ? Palette.Hex(0xC29A5C) : i % 3 == 1 ? Palette.Hex(0x9DAA6C) : Palette.Hex(0xD08E5C), "Blob", UIKit.Circle);
                blob.color = new Color(blob.color.r, blob.color.g, blob.color.b, 0.32f);
                blob.raycastTarget = false;
                float s = 90 + (float)rng.NextDouble() * 220;
                UIKit.Place(blob.gameObject, new Vector2((float)rng.NextDouble(), (float)rng.NextDouble()), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(s, s * 0.65f));
            }
            area.transform.SetAsLastSibling();

            var home = new Vector2(0.5f, 0.5f);
            foreach (var d in GameDatabase.Destinations)
            {
                bool locked = !S.IsDestinationUnlocked(d);
                var p = new Vector2(d.MapX, d.MapY);
                for (int k = 2; k < 20; k++)
                {
                    var dot = UIKit.Panel(area.transform, locked ? new Color(0.35f, 0.28f, 0.2f, 0.35f) : Palette.Hex(0x5B3F22), "Tie", UIKit.Circle);
                    dot.raycastTarget = false;
                    UIKit.Place(dot.gameObject, Vector2.Lerp(home, p, k / 20f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(9, 9));
                }
            }
            foreach (var t in S.State.trains.Where(t => !t.IsIdle))
            {
                var d = GameDatabase.Destination(t.destinationId);
                var holder = UIKit.Node("Train", area.transform);
                var mrt = UIKit.Place(holder, home, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(120, 60));
                var art = LocoArt(holder.transform, GameDatabase.Locomotive(t.defId), 120, 60);
                UIKit.Stretch(art.gameObject);
                int uid = t.uid;
                Action live = () =>
                {
                    var tt = S.Train(uid);
                    if (tt == null || tt.IsIdle) return;
                    float pr = S.TripProgress(tt);
                    float there = pr < 0.5f ? pr * 2 : (1 - pr) * 2; // out and back
                    mrt.anchorMin = mrt.anchorMax = Vector2.Lerp(home, new Vector2(d.MapX, d.MapY), there);
                };
                live(); Live(live);
            }
            foreach (var d in GameDatabase.Destinations) MapPin(area.transform, d);

            var hb = UIKit.KitImage(area.transform, "round_gold", "Home");
            UIKit.Place(hb.gameObject, home, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84, 84));
            var hi = UIKit.Icon(hb.transform, "nav_trains", 60);
            if (hi != null) UIKit.Place(hi, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60, 60));
            var hl = Box(area.transform, Palette.Hex(0x8E2A1C), -1, -1, "Town");
            UIKit.Place(hl.gameObject, home, new Vector2(0.5f, 1), new Vector2(0, -48), new Vector2(260, 52));
            var ht = T(hl.transform, S.State.townName, 32, Color.white, TextAnchor.MiddleCenter, true);
            UIKit.Stretch(ht.gameObject);

            var hint = Box(map.transform, BChip, -1, -1, "Hint");
            UIKit.Place(hint.gameObject, new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 18), new Vector2(760, 56));
            var htx = T(hint.transform, "Tap an outpost to send a train there.", 30, Color.white, TextAnchor.MiddleCenter, false, UIKit.Heading);
            UIKit.Stretch(htx.gameObject);
        }

        void MapPin(Transform area, DestinationDef d)
        {
            bool locked = !S.IsDestinationUnlocked(d);
            var pin = UIKit.Node("Pin " + d.Id, area);
            UIKit.Place(pin, new Vector2(d.MapX, d.MapY), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260, 160));
            var ring = UIKit.Panel(pin.transform, locked ? Palette.Hex(0x6B6F78) : BGold, "Ring", UIKit.Circle);
            UIKit.Place(ring.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(104, 104));
            ring.gameObject.AddComponent<Outline>().effectColor = BOutline;
            var ph = Photo(ring.transform, Postcard(d.Id), 92, 92, locked ? new Color(0.45f, 0.45f, 0.48f) : (Color?)null);
            ph.sprite = UIKit.Circle;
            ph.type = Image.Type.Simple;
            ph.pixelsPerUnitMultiplier = 1;
            UIKit.Place(ph.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(92, 92));
            var badge = UIKit.Node("Res", ring.transform);
            UIKit.Place(badge, new Vector2(1, 0), new Vector2(0.5f, 0.5f), new Vector2(-10, 12), new Vector2(46, 46));
            UIKit.HStack(badge, 0, 0, TextAnchor.MiddleCenter);
            if (locked) UIKit.Icon(badge.transform, "lock", 44);
            else Ico(d.IsTown ? "people" : d.ResourceId, 44)(badge.transform);
            bool boom = S.BoomAt(d.Id), bandits = S.BanditsOn(d.Id);
            if (!locked && (boom || bandits))
            {
                var ev = UIKit.Node("Event", ring.transform);
                UIKit.Place(ev, new Vector2(0, 1), new Vector2(0.5f, 0.5f), new Vector2(6, -6), new Vector2(64, 64));
                UIKit.HStack(ev, 0, 0, TextAnchor.MiddleCenter);
                UIKit.Icon(ev.transform, boom ? "gold_ore" : "bandit", 64);
                ev.AddComponent<Bob>();
            }
            var chip = Box(pin.transform, locked ? Palette.Hex(0x4A4F5C) : BChip, -1, -1, "Name");
            UIKit.Place(chip.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(256, 50));
            var nm = T1(chip.transform, locked ? "Level " + d.UnlockLevel : d.Name, 28, locked ? BLabel : Color.white, TextAnchor.MiddleCenter, true);
            UIKit.Stretch(nm.gameObject, 6);
            string id = d.Id;
            Clickable(pin, () =>
            {
                if (locked) Toast(d.Name + " opens at level " + d.UnlockLevel + ". " + d.Description, false);
                else OpenTrainTo(id);
            });
        }

        // ================================================================ Ledger

        void OpenProgress() { ShowBoard("Railroad Ledger", LedgerBoard); }

        void LedgerBoard(Transform body, Transform right)
        {
            var st = S.State.stats;
            _boardSub = S.State.townName;
            var row = UIKit.Node("Row", body);
            UIKit.Stretch(row);
            Row(row, 12, new RectOffset(0, 0, 0, 0)).childForceExpandHeight = true;

            var lv = Box(row.transform, BCardDeep, 520, -1, "Level");
            Stack(lv.gameObject, 14, new RectOffset(24, 24, 24, 24), TextAnchor.UpperCenter);
            var dh = HBox(lv.transform, 0, 0, TextAnchor.MiddleCenter, -1, 170);
            var disc = UIKit.KitImage(dh.transform, "round_gold", "Disc");
            UIKit.Size(disc.gameObject, 170, 170);
            var dn = T(disc.transform, S.Level.ToString(), 96, Color.white, TextAnchor.MiddleCenter, true);
            UIKit.Stretch(dn.gameObject);
            T1(lv.transform, S.IsGoldenAge ? "Golden Age" : "Frontier era", 48, BGold, TextAnchor.MiddleCenter, true);
            var xp = Track(lv.transform, 34, BGold);
            UIKit.SetBar(xp, S.LevelProgress);
            T1(lv.transform, Fmt.Num(S.State.xp) + " / " + Fmt.Num(Balance.XpForNextLevel(S.Level)) + " XP", 32, Color.white, TextAnchor.MiddleCenter, true);
            if (!S.IsGoldenAge) T(lv.transform, "The Golden Age begins at level " + Balance.GoldenAgeLevel + ".", 30, BLabel, TextAnchor.MiddleCenter, false, UIKit.Heading);
            Flex(lv.transform);
            T(lv.transform, "Civic bonuses", 30, BLabel, TextAnchor.MiddleCenter, false, UIKit.Heading);
            var bon = HBox(lv.transform, 8, 0, TextAnchor.MiddleCenter, -1, 50);
            Pill(bon.transform, null, "Speed +" + Mathf.RoundToInt(S.CivicBonus(CivicEffect.TrainSpeed) * 100) + "%", 28);
            Pill(bon.transform, null, "Cargo +" + Mathf.RoundToInt(S.CivicBonus(CivicEffect.Yield) * 100) + "%", 28);
            Pill(bon.transform, null, "XP +" + Mathf.RoundToInt(S.CivicBonus(CivicEffect.Xp) * 100) + "%", 28);

            var panel = Box(row.transform, BTile, -1, -1, "Panel");
            UIKit.Size(panel.gameObject, -1, -1, 1);
            Stack(panel.gameObject, 14, new RectOffset(22, 22, 20, 20));
            var tiles = HBox(panel.transform, 14, 0, TextAnchor.MiddleLeft, -1, 190);
            tiles.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            StatTile(tiles.transform, Ico("nav_trains", 76), Fmt.Num(st.dispatches), "Trains sent");
            StatTile(tiles.transform, Ico("nav_contracts", 76), Fmt.Num(st.contractsCompleted), "Contracts done");
            StatTile(tiles.transform, t => UIKit.CoinIcon(t, 76), Fmt.Num(st.coinsEarned), "Coins earned");
            StatTile(tiles.transform, Ico("people", 76), Fmt.Num(S.Population), "Townsfolk · income x" + (1 + S.CivicBonus(CivicEffect.Income)).ToString("0.00"));

            T(panel.transform, "Hauled home", 34, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);
            var grid = UIKit.Node("Grid", panel.transform);
            UIKit.Size(grid, -1, -1, 1, 1);
            var g = grid.AddComponent<GridLayoutGroup>();
            g.cellSize = new Vector2(150, 150);
            g.spacing = new Vector2(12, 12);
            foreach (var r in st.collected.OrderByDescending(x => x.amount))
            {
                var id = r.id;
                var w = Well(grid.transform, 150, tr => UIKit.ResIcon(tr, id, 80), Fmt.Num(r.amount), BCardDeep);
                w.name = id;
            }
        }

        static void StatTile(Transform p, Action<Transform> icon, string value, string label)
        {
            var t = Box(p, BCardDeep, -1, 190, "Stat");
            UIKit.Size(t.gameObject, -1, 190, 1);
            Stack(t.gameObject, 2, new RectOffset(10, 10, 14, 14), TextAnchor.MiddleCenter);
            var ic = HBox(t.transform, 0, 0, TextAnchor.MiddleCenter, -1, 80);
            icon(ic.transform);
            T1(t.transform, value, 52, Color.white, TextAnchor.MiddleCenter, true);
            T1(t.transform, label, 26, BLabel, TextAnchor.MiddleCenter, false, UIKit.Heading);
        }

        // ================================================================ Settings

        void OpenSettings() { ShowBoard("Settings", SettingsBoard, 1700, 860); }

        void SettingsBoard(Transform body, Transform right)
        {
            var set = S.State.settings;
            var row = UIKit.Node("Row", body);
            UIKit.Stretch(row);
            Row(row, 12, new RectOffset(0, 0, 0, 0)).childForceExpandHeight = true;

            var left = Box(row.transform, BTile, 760, -1, "Sound");
            Stack(left.gameObject, 14, new RectOffset(22, 22, 20, 20));
            T(left.transform, "Sound and alerts", 34, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);
            Toggle(left.transform, "Music", null, set.music, () => { set.music = !set.music; AudioManager.MusicOn = set.music; });
            Toggle(left.transform, "Sound effects", null, set.sfx, () => { set.sfx = !set.sfx; AudioManager.SfxOn = set.sfx; });
            Toggle(left.transform, "Vibration", null, set.haptics, () => { set.haptics = !set.haptics; Haptics.On = set.haptics; if (set.haptics) Haptics.Play(Haptic.Medium); });
            Toggle(left.transform, "Reminders", "When trains return, fuel is full or goods are ready", set.notifications, () => { set.notifications = !set.notifications; if (set.notifications) Reminders.AskOnce(); });
            T(left.transform, "Game Center", 34, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);
            var gc = HBox(left.transform, 14, 0, TextAnchor.MiddleLeft, -1, 88);
            Big(gc.transform, "Achievements", BBlue, GameCenter.ShowAchievements, 340, 84, true, 34);
            Big(gc.transform, "Leaderboards", BBlue, GameCenter.ShowLeaderboards, 340, 84, true, 34);
            if (!GameCenter.Supported) T(left.transform, "Game Center is available on iPhone and iPad.", 28, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);

            var panel = Box(row.transform, BTile, -1, -1, "Account");
            UIKit.Size(panel.gameObject, -1, -1, 1);
            Stack(panel.gameObject, 0, new RectOffset(10, 10, 10, 10));
            var list = Scroller(panel.transform, "settings", false, 14, new RectOffset(12, 12, 10, 16));
            T(list, "Account", 34, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);
            if (AppleAccount.SignedIn)
            {
                T(list, "Signed in with Apple" + (AppleAccount.DisplayName.Length > 0 ? " as " + AppleAccount.DisplayName : "") + ". Purchases follow your Apple ID to any device.", 30, Color.white, TextAnchor.MiddleLeft, false, UIKit.Heading);
                Big(list, "Sign out", UIKit.WoodLight, () => { AppleAccount.SignOut(); Toast("Signed out", true); RebuildWindow(); }, -1, 84, true, 34);
            }
            else
            {
                AppleSignInButton(list, () => AppleAccount.SignIn((ok, err) =>
                {
                    if (ok) { Toast("Signed in with Apple", true); RebuildWindow(); }
                    else if (err != null) Toast(err, true);
                }));
                T(list, "Optional. Keeps your purchases with your Apple ID on every device.", 28, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);
            }
            var acts = HBox(list, 14, 0, TextAnchor.MiddleLeft, -1, 88);
            acts.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            Big(acts.transform, "Restore purchases", UIKit.WoodLight, () => Store.Current.Restore(n => Toast(n > 0 ? "Restored " + n + " purchase(s)" : "Nothing to restore", true)), -1, 84, true, 32);
            Big(acts.transform, "Save now", UIKit.Green, () => { GameRoot.Instance.Save(); Toast("Game saved", true); }, -1, 84, true, 32);
            if (Debug.isDebugBuild)
            {
                T(list, "Developer tools", 34, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);
                Big(list, "+5,000 coins, +50 nuggets", BBlue, () => { S.State.coins += 5000; S.State.nuggets += 50; _dirty = true; }, -1, 76, true, 30);
                Big(list, "+100 of every raw resource", BBlue, () => { foreach (var r in GameDatabase.Resources.Where(x => !x.Manufactured)) S.State.inventory.Change(r.Id, 100); _dirty = true; }, -1, 76, true, 30);
                Big(list, "Gain a level", BBlue, () => { S.AddXp(Balance.XpForNextLevel(S.Level) - S.State.xp); _dirty = true; }, -1, 76, true, 30);
                Big(list, "Skip ahead 10 minutes", BBlue, () => { GameRoot.Instance.Clock.Offset += 600; S.Tick(); _dirty = true; }, -1, 76, true, 30);
            }
            T(list, "Danger zone", 34, BBad, TextAnchor.MiddleLeft, false, UIKit.Heading);
            Big(list, "Start a new railroad (erase save)", UIKit.Red, ConfirmReset, -1, 80, true, 32);
            T(list, "Ironhorse Frontier · version " + Application.version, 26, BLabel, TextAnchor.MiddleCenter, false, UIKit.Heading);
        }

        void Toggle(Transform p, string label, string sub, bool on, Action flip)
        {
            var r = Box(p, BCardDeep, -1, sub != null ? 120 : 96, "Toggle " + label);
            Row(r.gameObject, 14, new RectOffset(22, 16, 0, 0));
            var col = VBox(r.transform, 2, 0, TextAnchor.MiddleLeft, -1, -1, 1);
            T1(col.transform, label, 40, Color.white, TextAnchor.MiddleLeft, true);
            if (sub != null) T(col.transform, sub, 26, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);
            Big(r.transform, on ? "On" : "Off", on ? UIKit.Green : UIKit.WoodLight, () => { flip(); RebuildWindow(); }, 170, 76, true, 36);
        }

        // ================================================================ Land Office

        public void OpenLand() { ShowBoard("Land Office", LandBoard, 1400, 700); }

        void LandBoard(Transform body, Transform right)
        {
            _boardSub = S.State.unlockedPlots + " / " + Balance.TotalPlots + " lots";
            var panel = Box(body, BTile, -1, -1, "Panel");
            UIKit.Stretch(panel.gameObject);
            Stack(panel.gameObject, 18, new RectOffset(26, 26, 22, 24));
            var own = HBox(panel.transform, 14, 0, TextAnchor.MiddleLeft, -1, 50);
            T(own.transform, "Town lots owned", 34, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading).horizontalOverflow = HorizontalWrapMode.Overflow;
            var bar = Track(own.transform, 34, BGold);
            UIKit.Size(bar.transform.parent.gameObject, -1, 34, 1);
            UIKit.SetBar(bar, S.State.unlockedPlots / (float)Balance.TotalPlots);
            T(own.transform, S.State.unlockedPlots + " / " + Balance.TotalPlots, 34, Color.white, TextAnchor.MiddleRight, true).horizontalOverflow = HorizontalWrapMode.Overflow;

            var card = Box(panel.transform, BCardDeep, -1, -1, "Next");
            UIKit.Size(card.gameObject, -1, -1, -1, 1);
            Row(card.gameObject, 24, new RectOffset(24, 24, 20, 20));
            var sky = UIKit.Node("Sky", card.transform).AddComponent<Image>();
            sky.sprite = Sky();
            sky.raycastTarget = false;
            UIKit.Size(sky.gameObject, 340, 300);
            var art = BuildingArt(sky.transform, GameDatabase.Building("cabin"), 300, 230);
            UIKit.Place(art.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 8), new Vector2(300, 230));
            art.color = new Color(0.55f, 0.55f, 0.6f, 0.6f); // the lot you could build on
            var col = VBox(card.transform, 12, 0, TextAnchor.MiddleLeft, -1, -1, 1);
            if (!S.CanBuyPlot)
            {
                T(col.transform, "Every lot in the valley is yours!", 48, BGold, TextAnchor.MiddleLeft, true);
                return;
            }
            T(col.transform, "Survey the next lot", 48, BGold, TextAnchor.MiddleLeft, true);
            T(col.transform, "Stake a claim on fresh land at the edge of town for one more building.", 32, Color.white, TextAnchor.MiddleLeft, false, UIKit.Heading);
            bool lvOk = S.Level >= S.NextPlotLevel;
            if (!lvOk) T(col.transform, "Requires level " + S.NextPlotLevel, 34, BBad, TextAnchor.MiddleLeft, true);
            var buy = HBox(col.transform, 0, 0, TextAnchor.MiddleLeft, -1, 100);
            PriceBtn(buy.transform, "Buy lot", lvOk && S.Coins >= S.NextPlotCost ? UIKit.Green : UIKit.Disabled, t => UIKit.CoinIcon(t, 46), Fmt.Num(S.NextPlotCost), () => Result(S.BuyPlot(), Sfx.Build), 440, 96, true, 40);
        }
    }
}
