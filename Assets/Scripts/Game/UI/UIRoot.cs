using System;
using System.Collections.Generic;
using System.Linq;
using Ironhorse.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ironhorse.Game
{
    /// <summary>
    /// Root of the code-built interface: HUD, goal tracker, bottom bar, world markers, modal windows,
    /// toasts and popups. Window contents live in UIRoot.Windows.cs.
    /// </summary>
    public sealed partial class UIRoot : MonoBehaviour
    {
        public static UIRoot Instance { get; private set; }

        Canvas _canvas;
        RectTransform _safe, _markers, _windowLayer, _toasts, _popupLayer;
        GameSession S { get { return G.Session; } }

        // HUD widgets
        Text _level, _xpText, _coins, _nuggets, _pop, _storage, _goalText, _goalProgress, _contractsBadge, _trainsBadge;
        Image _xpFill, _goalFill;
        Button _goalClaim;
        GameObject _goalCard, _banner;
        Text _bannerText;

        // Window state
        GameObject _window;
        string _windowTitle;
        Action<Transform> _windowBuilder;
        ScrollRect _windowScroll;
        readonly List<Action> _live = new List<Action>();
        bool _dirty;
        int _lastFuel = -1;
        float _liveTimer;

        void Awake()
        {
            Instance = this;
            var events = FindAnyObjectByType<EventSystem>();
            if (events == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(transform, false);
                events = es.GetComponent<EventSystem>();
            }
            // Touch only: no keyboard/gamepad "submit" re-pressing the last tapped button (Editor focus changes send one).
            events.sendNavigationEvents = false;
            var cgo = new GameObject("Canvas", typeof(RectTransform));
            cgo.transform.SetParent(transform, false);
            _canvas = cgo.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10;
            var scaler = cgo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.75f;
            cgo.AddComponent<GraphicRaycaster>();

            _markers = UIKit.Stretch(UIKit.Node("Markers", cgo.transform));
            var safe = UIKit.Node("SafeArea", cgo.transform);
            _safe = UIKit.Stretch(safe);
            safe.AddComponent<SafeArea>();
            BuildHud();
            BuildBottomBar();
            BuildGoal();
            BuildChapterHud();
            BuildBanner();
            BuildMoneyHud();
            BuildDepthHud();
            BuildHooksHud();
            BuildComboHud();
            BuildNextStep();
            _windowLayer = UIKit.Stretch(UIKit.Node("Windows", cgo.transform));
            _popupLayer = UIKit.Stretch(UIKit.Node("Popups", cgo.transform));
            _toasts = UIKit.Anchor(UIKit.Node("Toasts", cgo.transform), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-420, -360), new Vector2(420, -150));
            UIKit.VStack(_toasts.gameObject, 10, 0, TextAnchor.UpperCenter);
            Celebrations.Init(cgo.transform); // above windows, popups and toasts
            gameObject.AddComponent<StoryDirector>();
        }

        void Start()
        {
            S.Changed += () => _dirty = true;
            S.LeveledUp += OnLevelUp;
            S.Notify += msg => Toast(msg, true);
            S.TrainArrived += t => AudioManager.Play(Sfx.Bell, 0.5f);
            S.ContractCompleted += OnContractCompleted;
            S.ChapterCompleted += OnChapterCompleted;
            S.ProjectCompleted += p =>
            {
                StoryDirector.Instance.OnProject(p); // the characters' finale scene, then the cinematic
                Celebrations.Instance.Play("project", "Project Complete!", p.Name + " is finished. The whole territory feels it.", force: true);
            };
            S.PlatformOpened += i => { AudioManager.Play(Sfx.LevelUp); Celebrations.Instance.Confetti(); };
            S.LocomotiveAdded += t => Celebrations.Instance.Play("newloco", "New Locomotive!", GameDatabase.Locomotive(t.defId).Name + " joins your fleet.", force: true);
            S.TaskSetCompleted += ts => Celebrations.Instance.Confetti();
            S.TelegramArrived += OnTelegram;
            S.MapEventStarted += OnMapEvent;
            S.AlbumSetCompleted += a => AudioManager.Play(Sfx.LevelUp);
            S.CurioFound += OnCurioFound;
            S.PeddlerArrived += () => AudioManager.Play(Sfx.Bell);
            S.EngineerJoined += e => AudioManager.Play(Sfx.LevelUp);
            RefreshHud();
            Invoke(nameof(QueueOffer), 4f);
        }

        void Update()
        {
            bool wasDirty = _dirty;
            if (_dirty)
            {
                _dirty = false;
                RefreshHud();
                if (_window != null) RebuildWindow();
            }
            _liveTimer -= Time.unscaledDeltaTime;
            if (_liveTimer <= 0)
            {
                _liveTimer = 0.25f;
                // Fuel regenerates lazily (no Changed event), so refresh open windows when it ticks.
                int fuel = S.Fuel;
                if (fuel != _lastFuel) { if (_lastFuel >= 0 && WindowOpen) _dirty = true; _lastFuel = fuel; }
                RefreshHud();
                foreach (var a in _live.ToList()) a();
                UpdateCoach();
            }
            UpdateWelcome();
            UpdateSiteView(wasDirty);
            UpdateMarkers();
            AnimateNextStep();
        }

        // ================================================================ HUD

        void BuildHud()
        {
            // Level badge + XP
            var left = UIKit.KitImage(_safe, "tile_dark", "LevelPanel");
            UIKit.Place(left.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(66, -22), new Vector2(390, 96));
            HudShadow(left.gameObject);
            var badge = UIKit.KitImage(left.transform, "round_gold", "Badge");
            UIKit.Place(badge.gameObject, new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -2), new Vector2(116, 116));
            HudShadow(badge.gameObject);
            _level = UIKit.Label(badge.transform, "1", 52, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            _level.font = UIKit.Display;
            _level.fontStyle = FontStyle.Normal; // the face is already bold
            UIKit.Stretch(_level.gameObject, 6);
            UIKit.Stroke(_level, new Color(0.36f, 0.2f, 0.02f, 0.95f), 2.5f);
            var town = UIKit.Label(left.transform, S.State.townName.ToUpperInvariant(), 28, UIKit.Gold, TextAnchor.UpperLeft, false, FontStyle.Bold);
            town.font = UIKit.Display;
            town.fontStyle = FontStyle.Normal;
            UIKit.Stroke(town, new Color(0, 0, 0, 0.7f), 1.5f);
            UIKit.Anchor(town.gameObject, new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(68, 0), new Vector2(-14, -10));
            var barHolder = UIKit.Node("XP", left.transform);
            UIKit.Anchor(barHolder, new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(68, 14), new Vector2(-16, -4));
            UIKit.HStack(barHolder, 0);
            _xpFill = UIKit.Bar(barHolder.transform, UIKit.Gold, 26, new Color(0, 0, 0, 0.55f));
            _xpText = UIKit.Label(_xpFill.transform.parent, "", 18, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            UIKit.Stretch(_xpText.gameObject);
            UIKit.Stroke(_xpText, new Color(0, 0, 0, 0.75f), 1.5f);
            Clickable(left.gameObject, () => OpenProgress());

            // Currency chips (top right)
            var chips = UIKit.Node("Chips", _safe);
            UIKit.Place(chips, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -20), new Vector2(1100, 70));
            UIKit.HStack(chips, 14, 0, TextAnchor.MiddleRight);
            _coins = Chip(chips.transform, t => UIKit.CoinIcon(t, 50), 210, () => OpenWarehouse());
            _nuggets = Chip(chips.transform, t => UIKit.NuggetIcon(t, 50), 190, () => OpenShop("nuggets"), true);
            _pop = Chip(chips.transform, t => PeopleIcon(t), 170, () => OpenProgress());
            _storage = Chip(chips.transform, t => CrateIcon(t), 210, () => OpenWarehouse());
            var gear = RoundButton(chips.transform, "round_wood", "•••", () => OpenSettings(), 78, "nav_settings");
            gear.name = "Settings";
        }

        Text Chip(Transform parent, Action<Transform> icon, float width, Action onClick, bool plus = false)
        {
            var bg = UIKit.KitImage(parent, "pill_slate", "Chip", 64);
            UIKit.Size(bg.gameObject, width, 64);
            HudShadow(bg.gameObject);
            var h = UIKit.HStack(bg.gameObject, 8, 4, TextAnchor.MiddleLeft);
            h.padding = new RectOffset(6, plus ? 4 : 18, 2, 2);
            icon(bg.transform);
            var t = UIKit.Label(bg.transform, "0", 34, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            t.font = UIKit.Display;
            t.fontStyle = FontStyle.Normal; // the face is already bold
            UIKit.Size(t.gameObject, -1, 50, 1);
            UIKit.Stroke(t, new Color(0, 0, 0, 0.8f), 2f);
            if (plus)
            {
                var p = UIKit.KitImage(bg.transform, "round_green", "Plus");
                UIKit.Size(p.gameObject, 52, 52);
                var pl = UIKit.Label(p.transform, "+", 40, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
                pl.font = UIKit.Display; pl.fontStyle = FontStyle.Normal;
                UIKit.Stretch(pl.gameObject);
                UIKit.Stroke(pl, new Color(0.05f, 0.25f, 0.05f, 0.9f), 2f);
            }
            Clickable(bg.gameObject, onClick);
            return t;
        }

        static void HudShadow(GameObject go)
        {
            var s = go.AddComponent<Shadow>();
            s.effectColor = new Color(0, 0, 0, 0.35f);
            s.effectDistance = new Vector2(0, -5);
        }

        /// <summary>Kit round button art for a colour (brass-rimmed glossy disc).</summary>
        static string RoundKit(Color c)
        {
            var k = UIKit.KitFor(c);
            if (k == null) return "round_brown";
            k = "round_" + k.Substring(4);
            if (k == "round_orange") k = "round_gold";
            if (k == "round_grey") k = "round_brown";
            return UIKit.Kit(k) != null ? k : "round_brown";
        }

        /// <summary>HUD disc (top-centred in its holder) for the bottom-bar and side buttons.</summary>
        static Image HudCircle(Transform holder, Color c, float size)
        {
            var img = UIKit.KitImage(holder, RoundKit(c), "Circle");
            UIKit.Place(img.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(size, size));
            HudShadow(img.gameObject);
            return img;
        }

        /// <summary>
        /// Reference-style HUD button: dark rounded-square tile, a big painted icon that rises out of its top edge,
        /// and an outlined caption along the bottom. Returns the tile (badges attach to it).
        /// </summary>
        static Image HudTile(Transform holder, string label, Action<Transform> glyph, float size)
        {
            var tile = UIKit.KitImage(holder, "tile_dark", "Tile", size);
            UIKit.Place(tile.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(size, size));
            tile.color = new Color(1, 1, 1, 0.94f);
            HudShadow(tile.gameObject);
            var gl = UIKit.Node("Glyph", tile.transform);
            UIKit.Place(gl, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, 22), new Vector2(size * 0.95f, size * 0.8f));
            UIKit.HStack(gl, 0, 0, TextAnchor.MiddleCenter);
            glyph(gl.transform);
            foreach (var le in gl.GetComponentsInChildren<LayoutElement>())
                if (le.minWidth > 0 && le.minWidth < size * 0.7f) { float k = size * 0.74f / le.minWidth; le.minWidth *= k; le.preferredWidth *= k; le.minHeight *= k; le.preferredHeight *= k; }
            var t = UIKit.Label(tile.transform, label.ToUpperInvariant(), 26, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            t.font = UIKit.Display;
            t.fontStyle = FontStyle.Normal;
            UIKit.Anchor(t.gameObject, Vector2.zero, new Vector2(1, 0), new Vector2(4, 6), new Vector2(-4, 40));
            UIKit.OneLine(t, 14).GetComponent<LayoutElement>().ignoreLayout = true;
            UIKit.Stroke(t, new Color(0, 0, 0, 0.85f), 2f);
            return tile;
        }

        /// <summary>Dark pill name plate under a HUD disc, with outlined caption.</summary>
        static Text HudPlate(Transform holder, string label, float w, float h = 44, int size = 24)
        {
            var plate = UIKit.KitImage(holder, "pill", "Plate", h);
            UIKit.Place(plate.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(w, h));
            var t = UIKit.Label(plate.transform, label.ToUpperInvariant(), size, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            t.font = UIKit.Display;
            t.fontStyle = FontStyle.Normal; // the face is already bold
            UIKit.Stretch(t.gameObject, 4);
            UIKit.OneLine(t, 14).GetComponent<LayoutElement>().ignoreLayout = true;
            UIKit.Stroke(t, new Color(0, 0, 0, 0.8f), 1.5f);
            return t;
        }

        static void PeopleIcon(Transform t)
        {
            if (UIKit.Icon(t, "people", 56) != null) return;
            var holder = UIKit.Node("People", t);
            UIKit.Size(holder, 50, 50);
            var head = UIKit.Panel(holder.transform, UIKit.Parchment, "Head", UIKit.Circle);
            UIKit.Place(head.gameObject, new Vector2(0.5f, 0.68f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20, 20));
            var body = UIKit.Panel(holder.transform, UIKit.Parchment, "Body", UIKit.RoundedSmall);
            UIKit.Place(body.gameObject, new Vector2(0.5f, 0.22f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 20));
        }

        static void CrateIcon(Transform t)
        {
            if (UIKit.Icon(t, "crate", 54) != null) return;
            var img = UIKit.Panel(t, Palette.WoodPale, "Crate", UIKit.RoundedSmall);
            UIKit.Size(img.gameObject, 46, 46);
            var x1 = UIKit.Panel(img.transform, Palette.Wood, "X", UIKit.RoundedSmall);
            UIKit.Place(x1.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54, 7)).localRotation = Quaternion.Euler(0, 0, 45);
            var x2 = UIKit.Panel(img.transform, Palette.Wood, "X", UIKit.RoundedSmall);
            UIKit.Place(x2.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54, 7)).localRotation = Quaternion.Euler(0, 0, -45);
        }

        /// <summary>Round brass-rimmed button (kit round_* art) with a glyph or a short caption.</summary>
        static Button RoundButton(Transform parent, string kit, string caption, Action onClick, float size, string icon = null)
        {
            var img = UIKit.KitImage(parent, kit, "Round");
            UIKit.Size(img.gameObject, size, size);
            var drop = img.gameObject.AddComponent<Shadow>();
            drop.effectColor = new Color(0, 0, 0, 0.35f);
            drop.effectDistance = new Vector2(0, -5);
            if (icon != null && UIKit.IconSprite(icon) != null)
            {
                var ic = UIKit.Node("Icon", img.transform);
                var ii = ic.AddComponent<Image>();
                ii.sprite = UIKit.IconSprite(icon);
                ii.preserveAspect = true;
                ii.raycastTarget = false;
                UIKit.Stretch(ic, size * 0.16f);
            }
            else if (caption != null)
            {
                var t = UIKit.Label(img.transform, caption, Mathf.RoundToInt(size * 0.42f), Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
                t.font = UIKit.Display;
                t.fontStyle = FontStyle.Normal;
                UIKit.Stretch(t.gameObject, 4);
                UIKit.Stroke(t, new Color(0.25f, 0.05f, 0.03f, 0.9f), 2f);
            }
            var b = img.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => { AudioManager.Play(Sfx.Click); onClick(); });
            img.gameObject.AddComponent<ButtonFx>();
            return b;
        }

        static void Clickable(GameObject go, Action onClick)
        {
            var b = go.GetComponent<Button>();
            if (b == null) b = go.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => { AudioManager.Play(Sfx.Click); onClick(); });
            if (go.GetComponent<ButtonFx>() == null) go.AddComponent<ButtonFx>();
        }

        void BuildBottomBar()
        {
            var bar = UIKit.Node("BottomBar", _safe);
            UIKit.Place(bar, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(1300, 170));
            UIKit.HStack(bar, 16, 0, TextAnchor.LowerCenter);
            _trainsBadge = BarButton(bar.transform, "Trains", UIKit.Red, () => OpenFleet(), TrainGlyph);
            BarButton(bar.transform, "Territory", UIKit.Blue, () => OpenTerritory(), MapGlyph);
            _contractsBadge = BarButton(bar.transform, "Contracts", UIKit.Green, () => OpenContracts(), ScrollGlyph);
            BarButton(bar.transform, "Build", UIKit.BrassDark, () => OpenBuildMenu(), HammerGlyph);
            _depotBadge = BarButton(bar.transform, "Depot", UIKit.WoodLight, () => OpenDepot(S.AlbumClaimable > 0 ? "album" : null), WheelGlyph);
            BarButton(bar.transform, "Warehouse", UIKit.WoodMid, () => OpenWarehouse(), CrateIcon);
        }

        Text BarButton(Transform parent, string label, Color c, Action onClick, Action<Transform> glyph)
        {
            var holder = UIKit.Node(label, parent);
            UIKit.Size(holder, 154, 170);
            _nav[label] = (RectTransform)holder.transform;
            var circle = HudTile(holder.transform, label, glyph, 150);
            Clickable(holder, onClick);
            var badge = UIKit.Badge(circle.transform, "", UIKit.Red);
            badge.transform.parent.gameObject.SetActive(false);
            return badge;
        }

        // Tiny vector-ish glyphs made of UI shapes.
        static void TrainGlyph(Transform t)
        {
            if (UIKit.Icon(t, "nav_trains", 84) != null) return;
            var h = UIKit.Node("Loco", t); UIKit.Size(h, 60, 46);
            Shape(h, UIKit.Cream, new Vector2(0.38f, 0.5f), new Vector2(34, 22));
            Shape(h, UIKit.Cream, new Vector2(0.78f, 0.62f), new Vector2(18, 30));
            Shape(h, UIKit.Cream, new Vector2(0.22f, 0.86f), new Vector2(8, 12));
            Shape(h, UIKit.Wood, new Vector2(0.25f, 0.12f), new Vector2(14, 14), UIKit.Circle);
            Shape(h, UIKit.Wood, new Vector2(0.55f, 0.12f), new Vector2(14, 14), UIKit.Circle);
            Shape(h, UIKit.Wood, new Vector2(0.82f, 0.12f), new Vector2(14, 14), UIKit.Circle);
        }

        static void MapGlyph(Transform t)
        {
            if (UIKit.Icon(t, "nav_territory", 84) != null) return;
            var h = UIKit.Node("Map", t); UIKit.Size(h, 56, 48);
            Shape(h, UIKit.Parchment, new Vector2(0.5f, 0.5f), new Vector2(54, 44));
            Shape(h, UIKit.Red, new Vector2(0.3f, 0.6f), new Vector2(12, 12), UIKit.Circle);
            Shape(h, UIKit.Red, new Vector2(0.72f, 0.35f), new Vector2(12, 12), UIKit.Circle);
            Shape(h, UIKit.InkSoft, new Vector2(0.5f, 0.48f), new Vector2(26, 4)).localRotation = Quaternion.Euler(0, 0, -30);
        }

        static void ScrollGlyph(Transform t)
        {
            if (UIKit.Icon(t, "nav_contracts", 84) != null) return;
            var h = UIKit.Node("Scroll", t); UIKit.Size(h, 48, 56);
            Shape(h, UIKit.Parchment, new Vector2(0.5f, 0.5f), new Vector2(40, 52));
            for (int i = 0; i < 4; i++) Shape(h, UIKit.InkSoft, new Vector2(0.5f, 0.72f - i * 0.15f), new Vector2(26, 4));
        }

        static void HammerGlyph(Transform t)
        {
            if (UIKit.Icon(t, "nav_build", 84) != null) return;
            var h = UIKit.Node("Hammer", t); UIKit.Size(h, 56, 56);
            Shape(h, Palette.WoodPale, new Vector2(0.5f, 0.42f), new Vector2(9, 44)).localRotation = Quaternion.Euler(0, 0, -35);
            Shape(h, UIKit.Cream, new Vector2(0.62f, 0.72f), new Vector2(34, 14)).localRotation = Quaternion.Euler(0, 0, -35);
        }

        static void WheelGlyph(Transform t)
        {
            if (UIKit.Icon(t, "nav_depot", 84) != null) return;
            var h = UIKit.Node("Wheel", t); UIKit.Size(h, 56, 56);
            Shape(h, UIKit.Cream, new Vector2(0.5f, 0.5f), new Vector2(52, 52), UIKit.Circle);
            Shape(h, UIKit.WoodLight, new Vector2(0.5f, 0.5f), new Vector2(38, 38), UIKit.Circle);
            for (int i = 0; i < 4; i++) Shape(h, UIKit.Cream, new Vector2(0.5f, 0.5f), new Vector2(40, 5)).localRotation = Quaternion.Euler(0, 0, i * 45);
            Shape(h, UIKit.Cream, new Vector2(0.5f, 0.5f), new Vector2(12, 12), UIKit.Circle);
        }

        static RectTransform Shape(GameObject parent, Color c, Vector2 at, Vector2 size, Sprite sprite = null)
        {
            var img = UIKit.Panel(parent.transform, c, "Shape", sprite ?? UIKit.RoundedSmall);
            img.raycastTarget = false;
            return UIKit.Place(img.gameObject, at, new Vector2(0.5f, 0.5f), Vector2.zero, size);
        }

        void BuildGoal()
        {
            var card = UIKit.KitImage(_safe, "tile_dark", "Goal");
            _goalCard = card.gameObject;
            UIKit.Place(_goalCard, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -138), new Vector2(430, 150));
            HudShadow(_goalCard);
            Clickable(_goalCard, GoToGoal);
            var v = UIKit.Node("V", card.transform);
            UIKit.Stretch(v, 14);
            UIKit.VStack(v, 6, 0, TextAnchor.UpperLeft);
            var hdr = UIKit.Label(v.transform, "TERRITORY GOAL", 18, UIKit.Gold, TextAnchor.MiddleLeft);
            hdr.font = UIKit.Display;
            UIKit.Size(hdr.gameObject, -1, 22);
            _goalText = UIKit.Label(v.transform, "", 22, UIKit.Cream);
            UIKit.Size(_goalText.gameObject, -1, 52);
            var row = UIKit.Row(v.transform, 32, 10);
            _goalFill = UIKit.Bar(row.transform, UIKit.Green, 20, new Color(0, 0, 0, 0.4f));
            _goalProgress = UIKit.Label(row.transform, "", 20, new Color(0.78f, 0.82f, 0.88f), TextAnchor.MiddleRight);
            UIKit.Size(_goalProgress.gameObject, 90, 30);
            _goalClaim = UIKit.Btn(card.transform, "Claim!", UIKit.Green, () =>
            {
                var r = S.ClaimGoal();
                if (r.Ok) { AudioManager.Play(Sfx.Coin); Fx.Burst(CameraRig.Instance.Focus + Vector3.up * 10, 50); }
                Toast(r.Message, r.Ok);
            }, 24, 140, 56);
            // Inside the card's bottom-right corner (over the "3/3" it replaces), not hanging over the chapter card below.
            UIKit.Place(_goalClaim.gameObject, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-10, 8), new Vector2(150, 54));
        }

        /// <summary>Tapping the goal card opens the screen where that goal gets done.</summary>
        void GoToGoal()
        {
            var g = S.CurrentGoal;
            if (g == null) return;
            switch (g.Type)
            {
                case GoalType.DispatchTrains: OpenFleet(); break;
                case GoalType.CollectResource:
                case GoalType.ProduceResource: GoGet(g.Target); break;
                case GoalType.ConstructBuilding:
                    var d = GameDatabase.Building(g.Target);
                    if (d != null) _buildTab = d.Category;
                    OpenBuildMenu();
                    break;
                case GoalType.ReachPopulation: _buildTab = BuildingCategory.Residential; OpenBuildMenu(); break;
                case GoalType.CompleteContracts: _ordersTab = "orders"; OpenContracts(); break;
                case GoalType.OwnLocomotives: OpenDepot("locos"); break;
                default: OpenTimeline(); break;
            }
        }

        void BuildBanner()
        {
            var b = UIKit.Panel(_safe, UIKit.Wood, "PlacementBanner", null, true);
            _banner = b.gameObject;
            UIKit.Place(_banner, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(760, 90));
            UIKit.HStack(_banner, 16, 14, TextAnchor.MiddleCenter);
            _bannerText = UIKit.Label(_banner.transform, "", 26, UIKit.Cream, TextAnchor.MiddleLeft, true);
            UIKit.Size(_bannerText.gameObject, 520, 60);
            UIKit.Btn(_banner.transform, "Cancel", UIKit.Red, () => TownView.Instance.CancelPlacement(), 24, 170, 60);
            _banner.SetActive(false);
        }

        public void ShowPlacementBanner(string building)
        {
            CloseWindow();
            _bannerText.text = "Tap a glowing plot to build the " + building;
            _banner.SetActive(true);
            ShiftToasts(true); // toasts drop below the banner instead of covering it
        }

        public void HidePlacementBanner()
        {
            _banner.SetActive(false);
            ShiftToasts(false);
        }

        bool _toastsShifted;

        void ShiftToasts(bool down)
        {
            if (down == _toastsShifted) return;
            _toastsShifted = down;
            _toasts.anchoredPosition += new Vector2(0, down ? -110 : 110);
        }

        void RefreshHud()
        {
            if (_level == null) return;
            RefreshMoneyHud();
            RefreshDepthHud();
            RefreshChapterHud();
            RefreshHooksHud();
            RefreshComboHud();
            RefreshNextStep();
            _level.text = S.Level.ToString();
            UIKit.SetBar(_xpFill, S.LevelProgress);
            _xpText.text = Fmt.Num(S.State.xp) + " / " + Fmt.Num(Balance.XpForNextLevel(S.Level)) + " XP";
            _coins.text = Fmt.Num(S.Coins);
            _nuggets.text = Fmt.Num(S.Nuggets);
            _pop.text = Fmt.Num(S.Population);
            _storage.text = S.WarehouseUsed + "/" + S.WarehouseCapacity;
            _storage.color = S.WarehouseFree < S.WarehouseCapacity * 0.1f ? Palette.Hex(0xFF8A6A) : UIKit.Cream;

            var g = S.CurrentGoal;
            _goalCard.SetActive(g != null);
            if (g != null)
            {
                int p = S.GoalProgressValue(g);
                _goalText.text = g.Text;
                _goalProgress.text = Mathf.Min(p, g.Count) + "/" + g.Count;
                UIKit.SetBar(_goalFill, p / (float)g.Count);
                bool done = p >= g.Count;
                _goalClaim.gameObject.SetActive(done);
                _goalProgress.enabled = !done;
                if (done) _goalClaim.transform.localScale = Vector3.one * (1 + Mathf.Sin(Time.unscaledTime * 6) * 0.04f);
            }

            int deliverable = S.State.contracts.Count(c => S.CanDeliver(c)) + S.ProjectsReadyToDeliver;
            _contractsBadge.text = deliverable.ToString();
            _contractsBadge.transform.parent.gameObject.SetActive(deliverable > 0);
            int ready = S.State.trains.Count(t => S.HasArrived(t) || (t.IsIdle && S.WagonsOf(t).Count > 0));
            _trainsBadge.text = ready.ToString();
            _trainsBadge.transform.parent.gameObject.SetActive(ready > 0);
        }

        // ================================================================ windows

        /// <summary>Opens a modal parchment window. The builder runs again whenever game state changes.</summary>
        /// <param name="back">When set, a Back button returns to the window this one was opened from.</param>
        public void ShowWindow(string title, Action<Transform> build, float width = 1180, float height = 820, Action back = null)
        {
            CloseWindow();
            AudioManager.Play(Sfx.Open);
            SetHudVisible(false); // the sheet has its own title bar; the HUD would show through the dim
            _windowTitle = title;
            _windowBuilder = build;

            // Full-screen sheet over the dimmed world: big title top-left, square close button top-right.
            var dim = UIKit.Panel(_windowLayer, new Color(0.06f, 0.04f, 0.03f, 0.74f), "Dim");
            dim.sprite = null;
            UIKit.Stretch(dim.gameObject);
            _window = dim.gameObject;
            Clickable(_window, CloseWindow);
            _window.GetComponent<ButtonFx>().enabled = false;

            var sheet = UIKit.Node("Sheet", _window.transform);
            UIKit.Stretch(sheet);
            sheet.AddComponent<SafeArea>();

            var frame = UIKit.KitImage(sheet.transform, "frame_dark", "Frame");
            UIKit.Place(frame.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -6), new Vector2(width + 120, height + 20));
            frame.gameObject.AddComponent<Button>().transition = Selectable.Transition.None; // swallow clicks
            frame.gameObject.AddComponent<TapSwallow>();
            frame.gameObject.AddComponent<WindowIntro>();
            var fdrop = frame.gameObject.AddComponent<Shadow>();
            fdrop.effectColor = new Color(0, 0, 0, 0.5f);
            fdrop.effectDistance = new Vector2(0, -10);

            // Header bar: bold yellow title on the left, red square close on the right (back button beside it).
            float left = back != null ? 230 : 40;
            var title_ = UIKit.Label(frame.transform, title, 54, UIKit.Title, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            title_.font = UIKit.Display;
            title_.fontStyle = FontStyle.Normal; // the face is already bold
            UIKit.Anchor(title_.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(left, -104), new Vector2(-150, -14));
            UIKit.OneLine(title_, 28).GetComponent<LayoutElement>().ignoreLayout = true;
            UIKit.Stroke(title_, new Color(0.05f, 0.06f, 0.1f, 0.95f), 2.5f);

            var close = UIKit.KitImage(frame.transform, "btn_red_sq", "Close");
            UIKit.Place(close.gameObject, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-26, -18), new Vector2(86, 86));
            var cx = UIKit.Label(close.transform, "X", 44, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            cx.font = UIKit.Display; cx.fontStyle = FontStyle.Normal;
            UIKit.Anchor(cx.gameObject, Vector2.zero, Vector2.one, new Vector2(0, 10), Vector2.zero);
            UIKit.Stroke(cx, new Color(0.35f, 0.05f, 0.03f, 0.95f), 2f);
            var cb = close.gameObject.AddComponent<Button>();
            cb.transition = Selectable.Transition.None;
            cb.onClick.AddListener(() => { AudioManager.Play(Sfx.Click); CloseWindow(); });
            close.gameObject.AddComponent<ButtonFx>();
            if (back != null)
            {
                var b = UIKit.Btn(frame.transform, "< Back", UIKit.WoodLight, () => back(), 28, 170, 76);
                UIKit.Place(b.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(34, -22), new Vector2(170, 76));
            }

            var paper = UIKit.KitImage(frame.transform, "inset", "Paper").gameObject;
            UIKit.Anchor(paper, Vector2.zero, Vector2.one, new Vector2(22, 22), new Vector2(-22, -116));

            // Scrollable content
            var scrollGo = UIKit.Node("Scroll", paper.transform);
            UIKit.Stretch(scrollGo, 6);
            var viewport = UIKit.Node("Viewport", scrollGo.transform);
            UIKit.Stretch(viewport);
            viewport.AddComponent<RectMask2D>();
            var content = UIKit.Node("Content", viewport.transform);
            var crt = UIKit.RT(content);
            crt.anchorMin = new Vector2(0, 1); crt.anchorMax = new Vector2(1, 1); crt.pivot = new Vector2(0.5f, 1);
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
            UIKit.VStack(content, 14, 18, TextAnchor.UpperCenter, true);
            _windowScroll = scrollGo.AddComponent<ScrollRect>();
            _windowScroll.viewport = UIKit.RT(viewport);
            _windowScroll.content = crt;
            _windowScroll.horizontal = false;
            _windowScroll.movementType = ScrollRect.MovementType.Elastic;
            _windowScroll.scrollSensitivity = 30;
            var hit = scrollGo.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            Fill(content.transform);
        }

        void Fill(Transform content)
        {
            _live.Clear();
            try { _windowBuilder(content); }
            catch (Exception e) { Debug.LogException(e); UIKit.Label(content, "Oops: " + e.Message, 22, UIKit.Red); }
        }

        void RebuildWindow()
        {
            if (_window != null && _boardBuilder != null) { FillBoard(); return; }
            if (_window == null || _windowScroll == null) return;
            float scroll = _windowScroll.verticalNormalizedPosition;
            var content = _windowScroll.content;
            for (int i = content.childCount - 1; i >= 0; i--)
            {
                var c = content.GetChild(i).gameObject;
                c.SetActive(false);
                Destroy(c);
            }
            Fill(content);
            if (_windowScroll == null) return; // the builder closed the window
            Canvas.ForceUpdateCanvases();
            _windowScroll.verticalNormalizedPosition = scroll;
        }

        public void CloseWindow()
        {
            if (_window != null) Destroy(_window);
            _window = null;
            SetHudVisible(true);
            _windowBuilder = null;
            _windowScroll = null;
            _boardBuilder = null;
            _boardBody = _boardRight = null;
            _boardSubLabel = null;
            _live.Clear();
        }

        public bool WindowOpen { get { return _window != null; } }

        /// <summary>A full-body character standing at the right edge of the open sheet, in front of the frame.</summary>
        void WindowFigure(string art)
        {
            var tex = Resources.Load<Texture2D>("UI/Story/" + art);
            if (_window == null || tex == null) return;
            var img = UIKit.Node("Figure", _window.transform.Find("Sheet")).AddComponent<Image>();
            img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0));
            img.preserveAspect = true;
            img.raycastTarget = false;
            float h = 860;
            UIKit.Place(img.gameObject, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, -60), new Vector2(h * tex.width / tex.height, h));
            img.gameObject.AddComponent<WindowIntro>();
        }

        public void SetHudVisible(bool on)
        {
            foreach (var layer in new[] { _safe, _markers })
            {
                if (layer == null) continue;
                var g = layer.GetComponent<CanvasGroup>();
                if (g == null) g = layer.gameObject.AddComponent<CanvasGroup>(); // not ??: Unity's fake-null
                g.alpha = on ? 1 : 0;
                g.blocksRaycasts = on;
            }
        }

        /// <summary>Registers a per-tick updater for the open window (timers, progress bars).</summary>
        void Live(Action a) { _live.Add(a); }

        // ================================================================ toasts & popups

        public void Toast(string message, bool good)
        {
            if (string.IsNullOrEmpty(message)) return;
            var t = UIKit.Panel(_toasts, good ? new Color(0.13f, 0.09f, 0.06f, 0.94f) : new Color(0.55f, 0.16f, 0.1f, 0.95f), "Toast", UIKit.RoundedGrad, true);
            t.gameObject.AddComponent<Outline>().effectColor = good ? new Color(UIKit.Brass.r, UIKit.Brass.g, UIKit.Brass.b, 0.8f) : new Color(1, 0.6f, 0.5f, 0.7f);
            UIKit.HStack(t.gameObject, 0, 16, TextAnchor.MiddleCenter);
            var fit = t.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var l = UIKit.Label(t.transform, message, 26, good ? UIKit.Cream : Color.white, TextAnchor.MiddleCenter, true);
            UIKit.Size(l.gameObject, 780, -1);
            t.gameObject.AddComponent<ToastFade>();
            if (!good) AudioManager.Play(Sfx.Error);
            while (_toasts.childCount > 4) DestroyImmediate(_toasts.GetChild(0).gameObject);
        }

        /// <summary>Shows an ActionResult: green toast on success, red on failure.</summary>
        void Result(ActionResult r, Sfx success = Sfx.Coin)
        {
            if (r.Ok) AudioManager.Play(success);
            if (!string.IsNullOrEmpty(r.Message)) Toast(r.Message, r.Ok);
        }

        void OnLevelUp(int level)
        {
            if (level == Balance.GoldenAgeLevel) Celebrations.Instance.Play("goldenage", "The Golden Age!", "Streamliners, steel and silver. A new era begins.", force: true);
            else Celebrations.Instance.Play("levelup", "Level " + level + "!", G.Session.State.townName + " is growing.", force: level <= 3);
            StoryDirector.Instance.OnLevel(level);
            AudioManager.Play(Sfx.LevelUp);
            Fx.Burst(CameraRig.Instance.Focus + Vector3.up * 12, 80);
            var unlocks = new List<string>();
            unlocks.AddRange(GameDatabase.Destinations.Where(d => d.UnlockLevel == level).Select(d => "Route: " + d.Name));
            unlocks.AddRange(GameDatabase.Locomotives.Where(d => d.UnlockLevel == level).Select(d => "Locomotive: " + d.Name));
            unlocks.AddRange(GameDatabase.Wagons.Where(d => d.UnlockLevel == level).Select(d => "Wagon: " + d.Name));
            unlocks.AddRange(GameDatabase.Buildings.Where(d => d.UnlockLevel == level).Select(d => "Building: " + d.Name));
            unlocks.AddRange(DepthContent.Projects.Where(d => d.UnlockLevel == level).Select(d => "Project: " + d.Name));
            // Daily features, named as they open so new players know what just appeared.
            if (level == Hooks.EventLevel) unlocks.Insert(0, "New: map events (travelers and tumbleweeds)");
            if (level == Hooks.ExpressLevel) unlocks.Insert(0, "New: express telegrams and Full Steam combos");
            if (level == Frontier.CrewLevel) unlocks.Insert(0, "New: engineers ride your locomotives");
            if (level == Frontier.PeddlerLevel) unlocks.Insert(0, "New: the traveling peddler visits");
            if (level == Haulage.UnlockLevel) unlocks.Insert(0, "New: haulers carry goods to projects on the map");
            if (!G.Session.StationComplete && !G.Session.LayingPlatform && level == G.Session.NextPlatformLevel)
                unlocks.Insert(0, "New: lay platform " + (G.Session.PlatformsBuilt + 1) + " for another locomotive");
            if (level == Balance.GoldenAgeLevel) unlocks.Insert(0, "THE GOLDEN AGE BEGINS!");

            // Several levels at once (a big reward): the newest popup replaces the older ones, whose rewards are already paid.
            foreach (Transform old in _popupLayer) if (old.name == "LevelUp") Destroy(old.gameObject);
            var dim = UIKit.Panel(_popupLayer, UIKit.Dim, "LevelUp");
            dim.sprite = null;
            UIKit.Stretch(dim.gameObject);
            var stack = UIKit.Node("Stack", dim.transform);
            UIKit.Place(stack, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 760));
            UIKit.VStack(stack, 0, 0, TextAnchor.UpperCenter).childForceExpandWidth = false; // children keep their preferred widths (ribbon, card)
            stack.AddComponent<WindowIntro>();
            UIDeco.Ribbon(stack.transform, level == Balance.GoldenAgeLevel ? "The Golden Age!" : "Level Up!", Palette.Hex(0x7A2E1E), 760, 120);
            var card = UIKit.Panel(stack.transform, new Color(0.13f, 0.09f, 0.06f, 0.92f), "Card", null, true);
            int lines = Mathf.Min(unlocks.Count, 5);
            UIKit.Size(card.gameObject, 820, 560 + 32 * Mathf.Max(0, lines - 2));
            UIKit.RT(stack).sizeDelta = new Vector2(1000, 760 + 32 * Mathf.Max(0, lines - 2));
            card.gameObject.AddComponent<Outline>().effectColor = UIKit.Brass;
            var v = UIKit.Node("V", card.transform);
            UIKit.Stretch(v, 30);
            UIKit.RT(v).offsetMax = new Vector2(-30, -20);
            UIKit.VStack(v, 12, 0, TextAnchor.UpperCenter);
            var chipRow = UIKit.Row(v.transform, 70, 0, TextAnchor.MiddleCenter);
            UIDeco.LevelChip(chipRow.transform, level - 1, level);
            UIDeco.Divider(v.transform, "Rewards", UIKit.Cream);
            var rewardsPanel = UIKit.Panel(v.transform, UIKit.Parchment, "Rewards", UIKit.Rounded);
            UIKit.Size(rewardsPanel.gameObject, -1, 110);
            UIKit.VStack(rewardsPanel.gameObject, 0, 8, TextAnchor.MiddleCenter);
            UIDeco.Rewards(rewardsPanel.transform, S, Balance.LevelUpCoins(level), 0, Balance.LevelUpNuggets(level));
            if (unlocks.Count > 0)
            {
                UIDeco.Divider(v.transform, "New this level", UIKit.Cream);
                foreach (var u in unlocks.Take(5))
                {
                    var l = UIKit.Label(v.transform, u, 24, UIKit.Cream, TextAnchor.MiddleCenter);
                    UIKit.Size(l.gameObject, -1, 30);
                }
            }
            var nextUp = S.NextUnlocks();
            if (nextUp.Count > 0)
            {
                var nl = UIKit.Label(v.transform, "Coming up at level " + nextUp[0].Level + ": " + string.Join(", ", nextUp.Take(3).Select(u => u.Name)), 22, UIKit.Gold, TextAnchor.MiddleCenter);
                UIKit.OneLine(nl, 14); // one line, shrinking, so it never runs into the unlock list
            }
            UIKit.Spacer(v.transform, -1, 6);
            var go = dim.gameObject;
            var claimRow = UIKit.Row(v.transform, 90, 0, TextAnchor.MiddleCenter);
            UIKit.Btn(claimRow.transform, "Claim", UIKit.Blue, () => Destroy(go), 34, 320, 84);
        }

        // ================================================================ world markers

        sealed class Marker
        {
            public GameObject Go;
            public Image Bg;
            public GameObject Icon;
            public string IconKey;
            public Text Label;
            public Image Bar;
            public Action OnClick;
        }

        readonly List<Marker> _markerPool = new List<Marker>();
        int _markerUsed;

        Marker NextMarker()
        {
            if (_markerUsed < _markerPool.Count) return _markerPool[_markerUsed++];
            var m = new Marker();
            var bg = UIKit.KitImage(_markers, "card_light", "Marker");
            bg.pixelsPerUnitMultiplier = 1.4f;
            m.Go = bg.gameObject;
            m.Bg = bg;
            UIKit.RT(m.Go).sizeDelta = new Vector2(96, 96);
            HudShadow(bg.gameObject);
            var lbl = UIKit.Label(m.Go.transform, "", 22, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            lbl.font = UIKit.Display;
            lbl.fontStyle = FontStyle.Normal;
            UIKit.Anchor(lbl.gameObject, new Vector2(0, 0), new Vector2(1, 0), new Vector2(-30, -32), new Vector2(30, 0));
            UIKit.Stroke(lbl, new Color(0.2f, 0.12f, 0.05f, 0.95f), 2f);
            m.Label = lbl;
            var barHolder = UIKit.Node("BarHolder", m.Go.transform);
            UIKit.Anchor(barHolder, new Vector2(0, 0), new Vector2(1, 0), new Vector2(8, 8), new Vector2(-8, 22));
            UIKit.HStack(barHolder, 0);
            m.Bar = UIKit.Bar(barHolder.transform, UIKit.Green, 14);
            var b = m.Go.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            b.onClick.AddListener(() => { m.OnClick?.Invoke(); });
            m.Go.AddComponent<ButtonFx>();
            _markerPool.Add(m);
            _markerUsed++;
            return m;
        }

        void ShowMarker(Vector3 world, string iconKey, Action<Transform> icon, string label, float progress, Action onClick, bool bob)
        {
            var cam = CameraRig.Instance.Cam;
            var sp = cam.WorldToScreenPoint(world);
            if (sp.z < 0) return;
            Vector2 local;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_markers, sp, null, out local)) return;
            var m = NextMarker();
            m.Go.SetActive(true);
            float size = Mathf.Lerp(104, 64, Mathf.InverseLerp(40, 220, CameraRig.Instance.Distance));
            var rt = UIKit.RT(m.Go);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = local + new Vector2(0, bob ? Mathf.Sin(Time.unscaledTime * 3 + world.x) * 6 : 0);
            if (m.IconKey != iconKey)
            {
                if (m.Icon != null) Destroy(m.Icon);
                var holder = UIKit.Node("Icon", m.Go.transform);
                UIKit.Stretch(holder, 12);
                UIKit.HStack(holder, 0, 0, TextAnchor.MiddleCenter);
                icon(holder.transform);
                m.Icon = holder;
                m.IconKey = iconKey;
            }
            m.Label.text = label ?? "";
            m.Bar.transform.parent.parent.gameObject.SetActive(progress >= 0);
            if (progress >= 0) UIKit.SetBar(m.Bar, progress);
            m.OnClick = onClick;
            m.Bg.color = progress >= 0 ? new Color(1, 1, 1, 0.88f) : Color.white;
        }

        void UpdateMarkers()
        {
            _markerUsed = 0;
            bool mapView = S != null && !WindowOpen && !SiteOpen && TownView.Instance != null && TownView.Instance.PlacingDefId == null;
            UpdateDestinationPins(mapView);
            UpdateProjectPins(mapView);
            if (mapView)
            {
                foreach (var t in S.State.trains)
                {
                    var view = TrainManager.Instance != null ? TrainManager.Instance.View(t.uid) : null;
                    if (view == null) continue;
                    if (S.HasArrived(t) && view.Current == TrainView.Phase.Arrived)
                    {
                        int uid = t.uid;
                        string res = t.cargoId;
                        ShowMarker(view.LeadPosition + Vector3.up * 7, "res:" + res, h => UIKit.ResIcon(h, res, 56), "+" + t.cargoAmount, -1, () =>
                        {
                            var r = S.Unload(uid);
                            Result(r);
                            if (r.Ok) Fx.Burst(view.LeadPosition + Vector3.up * 5, 30);
                        }, true);
                    }
                }
                foreach (var t in S.State.trains)
                {
                    if (!t.crate || S.IsEnRoute(t)) continue;
                    var view = TrainManager.Instance != null ? TrainManager.Instance.View(t.uid) : null;
                    if (view == null || !view.IsShownAtStation) continue;
                    int uid = t.uid;
                    var at = CratePosition(view);
                    ShowMarker(at, "crate", h => UIKit.Icon(h, "crate_mystery", 64), "?", -1, () => OpenCrateOn(uid, at), true);
                }
                foreach (var t in S.State.trucks)
                {
                    if (!S.TruckBack(t)) continue;
                    var view = TruckManager.Instance != null ? TruckManager.Instance.View(t.uid) : null;
                    if (view == null || !view.Visible) continue;
                    int uid = t.uid;
                    ShowMarker(view.Top, "coin", h => UIKit.CoinIcon(h, 56), "+" + Fmt.Num(t.job.rewardCoins), -1, () => CollectTruck(uid), true);
                }
                UpdateEventMarkers();
                UpdatePeddlerMarker();
                foreach (var b in S.State.buildings)
                {
                    var view = TownView.Instance.View(b.uid);
                    if (view == null) continue;
                    var pos = view.transform.position + Vector3.up * (view.Height + 3);
                    var def = GameDatabase.Building(b.defId);
                    int uid = b.uid;
                    if (!S.IsReady(b))
                        ShowMarker(pos, "build", h => HammerGlyph(h), Fmt.Time(S.ConstructionRemaining(b)), S.ConstructionProgress(b), () => OpenBuilding(uid), false);
                    else if (S.IsProductionDone(b))
                    {
                        string res = def.Recipe.Output.id;
                        ShowMarker(pos, "res:" + res, h => UIKit.ResIcon(h, res, 56), "+" + def.Recipe.Output.amount * S.FinishedJobs(b), -1, () =>
                        {
                            var r = S.CollectProduction(uid);
                            Result(r);
                            if (r.Ok) Fx.Burst(pos, 30);
                        }, true);
                    }
                    else if (S.IsProducing(b))
                        ShowMarker(pos, "work:" + def.Recipe.Output.id, h => UIKit.ResIcon(h, def.Recipe.Output.id, 48), Fmt.Time(S.QueueRemaining(b)), S.ProductionProgress(b), () => OpenBuilding(uid), false);
                    else if (def.Category == BuildingCategory.Residential && S.RentFill(b) > 0.15f)
                    {
                        int coins = S.PendingRent(b);
                        ShowMarker(pos, "coin", h => UIKit.CoinIcon(h, 56), Fmt.Num(coins), -1, () =>
                        {
                            var r = S.CollectRent(uid);
                            Result(r);
                            if (r.Ok) Fx.Burst(pos, 20);
                        }, S.RentFill(b) > 0.5f);
                    }
                }
            }
            for (int i = _markerUsed; i < _markerPool.Count; i++) _markerPool[i].Go.SetActive(false);
        }
    }

    /// <summary>Pop-in animation for windows and popups.</summary>
    public sealed class WindowIntro : MonoBehaviour
    {
        float _t;
        void OnEnable() { _t = 0; transform.localScale = Vector3.one * 0.85f; }
        void Update()
        {
            if (_t >= 1) return;
            _t = Mathf.Min(1, _t + Time.unscaledDeltaTime * 5f);
            float k = 1 + 2.2f * Mathf.Pow(_t - 1, 3) + 1.2f * Mathf.Pow(_t - 1, 2); // ease-out-back
            transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, k);
        }
    }

    public sealed class ToastFade : MonoBehaviour
    {
        float _age;
        CanvasGroup _g;
        void Awake() { _g = gameObject.AddComponent<CanvasGroup>(); _g.blocksRaycasts = false; }
        void Update()
        {
            _age += Time.unscaledDeltaTime;
            _g.alpha = Mathf.Clamp01(_age * 6) * Mathf.Clamp01((3.4f - _age) * 2);
            if (_age > 3.4f) Destroy(gameObject);
        }
    }

    /// <summary>Keeps UI inside the iPhone notch / home-indicator safe area.</summary>
    public sealed class SafeArea : MonoBehaviour
    {
        Rect _last;
        void Update()
        {
            var sa = Screen.safeArea;
            if (sa == _last || Screen.width == 0) return;
            _last = sa;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            rt.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
