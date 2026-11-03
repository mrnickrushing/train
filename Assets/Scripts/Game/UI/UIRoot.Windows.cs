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

        static GameObject WagonSwatch(Transform parent, WagonDef wd)
        {
            var bg = UIKit.Panel(parent, UIKit.ParchmentDark * 0.92f, "Wagon", UIKit.RoundedSmall);
            UIKit.Size(bg.gameObject, 120, 76);
            var img = ModelIcons.Image(bg.transform, ModelIcons.Wagon(wd), 120, 75);
            UIKit.Stretch(img.gameObject);
            return bg.gameObject;
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
