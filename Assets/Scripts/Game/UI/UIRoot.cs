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
            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                es.transform.SetParent(transform, false);
            }
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
            BuildBanner();
            BuildMoneyHud();
            BuildDepthHud();
            _windowLayer = UIKit.Stretch(UIKit.Node("Windows", cgo.transform));
            _popupLayer = UIKit.Stretch(UIKit.Node("Popups", cgo.transform));
            _toasts = UIKit.Anchor(UIKit.Node("Toasts", cgo.transform), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-420, -360), new Vector2(420, -150));
            UIKit.VStack(_toasts.gameObject, 10, 0, TextAnchor.UpperCenter);
            Celebrations.Init(cgo.transform); // above windows, popups and toasts
        }

        void Start()
        {
            S.Changed += () => _dirty = true;
            S.LeveledUp += OnLevelUp;
            S.Notify += msg => Toast(msg, true);
            S.TrainArrived += t => AudioManager.Play(Sfx.Bell, 0.5f);
            S.ContractCompleted += OnContractCompleted;
            S.ProjectCompleted += p => Celebrations.Instance.Play("project", "Project Complete!", p.Name + " is finished. The whole territory feels it.");
            S.LocomotiveAdded += t => Celebrations.Instance.Play("newloco", "New Locomotive!", GameDatabase.Locomotive(t.defId).Name + " joins your fleet.", force: true);
            S.TaskSetCompleted += ts => Celebrations.Instance.Confetti();
            RefreshHud();
            Invoke(nameof(MaybeShowFeaturedOffer), 4f);
        }

        void Update()
        {
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
            }
            UpdateMarkers();
        }

        // ================================================================ HUD

        void BuildHud()
        {
            // Level badge + XP
            var left = UIKit.Panel(_safe, new Color(UIKit.Wood.r, UIKit.Wood.g, UIKit.Wood.b, 0.92f), "LevelPanel", null, true);
            UIKit.Place(left.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -20), new Vector2(430, 104));
            var badge = UIKit.Panel(left.transform, UIKit.Brass, "Badge", UIKit.Circle);
            UIKit.Place(badge.gameObject, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(10, 0), new Vector2(86, 86));
            badge.gameObject.AddComponent<Outline>().effectColor = UIKit.BrassDark;
            _level = UIKit.Label(badge.transform, "1", 46, UIKit.Wood, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            _level.font = UIKit.Display;
            _level.fontStyle = FontStyle.Normal; // the face is already bold
            UIKit.Stretch(_level.gameObject);
            var town = UIKit.Label(left.transform, S.State.townName.ToUpperInvariant(), 26, UIKit.Brass, TextAnchor.UpperLeft, true);
            UIKit.Anchor(town.gameObject, new Vector2(0, 0.5f), new Vector2(1, 1), new Vector2(108, 0), new Vector2(-14, -12));
            var barHolder = UIKit.Node("XP", left.transform);
            UIKit.Anchor(barHolder, new Vector2(0, 0), new Vector2(1, 0.5f), new Vector2(108, 16), new Vector2(-16, -6));
            UIKit.HStack(barHolder, 0);
            _xpFill = UIKit.Bar(barHolder.transform, UIKit.Brass, 26);
            _xpText = UIKit.Label(_xpFill.transform.parent, "", 18, UIKit.Cream, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            UIKit.Stretch(_xpText.gameObject);
            Clickable(left.gameObject, () => OpenProgress());

            // Currency chips (top right)
            var chips = UIKit.Node("Chips", _safe);
            UIKit.Place(chips, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -20), new Vector2(1100, 70));
            UIKit.HStack(chips, 14, 0, TextAnchor.MiddleRight);
            _coins = Chip(chips.transform, t => UIKit.CoinIcon(t, 50), 210, () => OpenWarehouse());
            _nuggets = Chip(chips.transform, t => UIKit.NuggetIcon(t, 50), 150, () => Toast("Gold nuggets come from level-ups, goals and lucky contracts.", true));
            _pop = Chip(chips.transform, t => PeopleIcon(t), 170, () => OpenProgress());
            _storage = Chip(chips.transform, t => CrateIcon(t), 210, () => OpenWarehouse());
            var gear = UIKit.Btn(chips.transform, "•••", UIKit.WoodMid, () => OpenSettings(), 36, 70, 70);
            gear.name = "Settings";
        }

        Text Chip(Transform parent, Action<Transform> icon, float width, Action onClick)
        {
            var bg = UIKit.Panel(parent, new Color(UIKit.Wood.r, UIKit.Wood.g, UIKit.Wood.b, 0.92f), "Chip", null, true);
            UIKit.Size(bg.gameObject, width, 64);
            UIKit.HStack(bg.gameObject, 10, 8, TextAnchor.MiddleLeft);
            icon(bg.transform);
            var t = UIKit.Label(bg.transform, "0", 34, UIKit.Cream, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            t.font = UIKit.Display;
            t.fontStyle = FontStyle.Normal; // the face is already bold
            UIKit.Size(t.gameObject, -1, 50, 1);
            Clickable(bg.gameObject, onClick);
            return t;
        }

        static void PeopleIcon(Transform t)
        {
            var holder = UIKit.Node("People", t);
            UIKit.Size(holder, 50, 50);
            var head = UIKit.Panel(holder.transform, UIKit.Parchment, "Head", UIKit.Circle);
            UIKit.Place(head.gameObject, new Vector2(0.5f, 0.68f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(20, 20));
            var body = UIKit.Panel(holder.transform, UIKit.Parchment, "Body", UIKit.RoundedSmall);
            UIKit.Place(body.gameObject, new Vector2(0.5f, 0.22f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 20));
        }

        static void CrateIcon(Transform t)
        {
            var img = UIKit.Panel(t, Palette.WoodPale, "Crate", UIKit.RoundedSmall);
            UIKit.Size(img.gameObject, 46, 46);
            var x1 = UIKit.Panel(img.transform, Palette.Wood, "X", UIKit.RoundedSmall);
            UIKit.Place(x1.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54, 7)).localRotation = Quaternion.Euler(0, 0, 45);
            var x2 = UIKit.Panel(img.transform, Palette.Wood, "X", UIKit.RoundedSmall);
            UIKit.Place(x2.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54, 7)).localRotation = Quaternion.Euler(0, 0, -45);
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
            UIKit.Place(bar, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 18), new Vector2(1300, 150));
            UIKit.HStack(bar, 18, 0, TextAnchor.LowerCenter);
            _trainsBadge = BarButton(bar.transform, "Trains", UIKit.Red, () => OpenFleet(), TrainGlyph);
            BarButton(bar.transform, "Territory", UIKit.Blue, () => OpenTerritory(), MapGlyph);
            _contractsBadge = BarButton(bar.transform, "Contracts", UIKit.Green, () => OpenContracts(), ScrollGlyph);
            BarButton(bar.transform, "Build", UIKit.BrassDark, () => OpenBuildMenu(), HammerGlyph);
            BarButton(bar.transform, "Depot", UIKit.WoodLight, () => OpenDepot(), WheelGlyph);
            BarButton(bar.transform, "Warehouse", UIKit.WoodMid, () => OpenWarehouse(), CrateIcon);
        }

        Text BarButton(Transform parent, string label, Color c, Action onClick, Action<Transform> glyph)
        {
            var holder = UIKit.Node(label, parent);
            UIKit.Size(holder, 170, 150);
            var circle = UIKit.Panel(holder.transform, c, "Circle", UIKit.Circle, true);
            UIKit.Place(circle.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(108, 108));
            var ring = circle.gameObject.AddComponent<Outline>();
            ring.effectColor = UIKit.Brass;
            ring.effectDistance = new Vector2(3, -3);
            var gl = UIKit.Node("Glyph", circle.transform);
            UIKit.Stretch(gl, 22);
            UIKit.HStack(gl, 0, 0, TextAnchor.MiddleCenter);
            glyph(gl.transform);
            var plate = UIKit.Panel(holder.transform, UIKit.Wood, "Plate", UIKit.RoundedSmall, true);
            UIKit.Place(plate.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(166, 40));
            var t = UIKit.Label(plate.transform, label.ToUpperInvariant(), 24, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            t.font = UIKit.Display;
            t.fontStyle = FontStyle.Normal; // the face is already bold
            UIKit.Stretch(t.gameObject);
            Clickable(holder, onClick);
            var badge = UIKit.Badge(circle.transform, "", UIKit.Red);
            badge.transform.parent.gameObject.SetActive(false);
            return badge;
        }

        // Tiny vector-ish glyphs made of UI shapes.
        static void TrainGlyph(Transform t)
        {
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
            var h = UIKit.Node("Map", t); UIKit.Size(h, 56, 48);
            Shape(h, UIKit.Parchment, new Vector2(0.5f, 0.5f), new Vector2(54, 44));
            Shape(h, UIKit.Red, new Vector2(0.3f, 0.6f), new Vector2(12, 12), UIKit.Circle);
            Shape(h, UIKit.Red, new Vector2(0.72f, 0.35f), new Vector2(12, 12), UIKit.Circle);
            Shape(h, UIKit.InkSoft, new Vector2(0.5f, 0.48f), new Vector2(26, 4)).localRotation = Quaternion.Euler(0, 0, -30);
        }

        static void ScrollGlyph(Transform t)
        {
            var h = UIKit.Node("Scroll", t); UIKit.Size(h, 48, 56);
            Shape(h, UIKit.Parchment, new Vector2(0.5f, 0.5f), new Vector2(40, 52));
            for (int i = 0; i < 4; i++) Shape(h, UIKit.InkSoft, new Vector2(0.5f, 0.72f - i * 0.15f), new Vector2(26, 4));
        }

        static void HammerGlyph(Transform t)
        {
            var h = UIKit.Node("Hammer", t); UIKit.Size(h, 56, 56);
            Shape(h, Palette.WoodPale, new Vector2(0.5f, 0.42f), new Vector2(9, 44)).localRotation = Quaternion.Euler(0, 0, -35);
            Shape(h, UIKit.Cream, new Vector2(0.62f, 0.72f), new Vector2(34, 14)).localRotation = Quaternion.Euler(0, 0, -35);
        }

        static void WheelGlyph(Transform t)
        {
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
            var card = UIKit.Panel(_safe, UIKit.Parchment, "Goal", null, true);
            _goalCard = card.gameObject;
            UIKit.Place(_goalCard, new Vector2(0, 1), new Vector2(0, 1), new Vector2(24, -138), new Vector2(430, 150));
            card.gameObject.AddComponent<Outline>().effectColor = UIKit.BrassDark;
            var v = UIKit.Node("V", card.transform);
            UIKit.Stretch(v, 14);
            UIKit.VStack(v, 6, 0, TextAnchor.UpperLeft);
            var hdr = UIKit.Label(v.transform, "TERRITORY GOAL", 18, UIKit.BrassDark, TextAnchor.MiddleLeft, true, FontStyle.Bold);
            UIKit.Size(hdr.gameObject, -1, 22);
            _goalText = UIKit.Label(v.transform, "", 22, UIKit.Ink);
            UIKit.Size(_goalText.gameObject, -1, 52);
            var row = UIKit.Row(v.transform, 32, 10);
            _goalFill = UIKit.Bar(row.transform, UIKit.Green, 20);
            _goalProgress = UIKit.Label(row.transform, "", 20, UIKit.InkSoft, TextAnchor.MiddleRight);
            UIKit.Size(_goalProgress.gameObject, 90, 30);
            _goalClaim = UIKit.Btn(card.transform, "Claim!", UIKit.Green, () =>
            {
                var r = S.ClaimGoal();
                if (r.Ok) { AudioManager.Play(Sfx.Coin); Fx.Burst(CameraRig.Instance.Focus + Vector3.up * 10, 50); }
                Toast(r.Message, r.Ok);
            }, 24, 140, 56);
            UIKit.Place(_goalClaim.gameObject, new Vector2(1, 0), new Vector2(1, 0.5f), new Vector2(-10, 0), new Vector2(140, 56));
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
        }

        public void HidePlacementBanner() { _banner.SetActive(false); }

        void RefreshHud()
        {
            if (_level == null) return;
            RefreshMoneyHud();
            RefreshDepthHud();
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

            var title_ = UIKit.Label(sheet.transform, title.ToUpperInvariant(), 64, Color.white, TextAnchor.MiddleLeft, false, FontStyle.Bold);
            title_.font = UIKit.Display;
            title_.fontStyle = FontStyle.Normal; // the face is already bold
            UIKit.Anchor(title_.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(70, -120), new Vector2(-200, -20));
            var sh = title_.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.6f);
            sh.effectDistance = new Vector2(0, -4);
            var underline = UIKit.Panel(sheet.transform, UIKit.Brass, "Underline", UIKit.RoundedSmall);
            UIKit.Place(underline.gameObject, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(72, -118), new Vector2(Mathf.Min(560, title.Length * 30 + 40), 6));

            var close = UIKit.Btn(sheet.transform, "X", Palette.Hex(0x2A1C12), CloseWindow, 40, 92, 92);
            UIKit.Place(close.gameObject, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -24), new Vector2(92, 92));
            if (back != null)
            {
                var b = UIKit.Btn(sheet.transform, "< Back", UIKit.WoodLight, () => back(), 30, 200, 92);
                UIKit.Place(b.gameObject, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-150, -24), new Vector2(200, 92));
            }

            var frame = UIKit.Panel(sheet.transform, UIKit.Parchment, "Frame", null, true);
            UIKit.Place(frame.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(width + 120, height - 40));
            frame.gameObject.AddComponent<Button>().transition = Selectable.Transition.None; // swallow clicks
            frame.gameObject.AddComponent<WindowIntro>();
            var edge = frame.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(UIKit.Brass.r, UIKit.Brass.g, UIKit.Brass.b, 0.85f);
            edge.effectDistance = new Vector2(3, -3);

            var paper = UIKit.Panel(frame.transform, UIKit.Parchment, "Paper");
            UIKit.Anchor(paper.gameObject, Vector2.zero, Vector2.one, new Vector2(10, 10), new Vector2(-10, -10));

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
            _live.Clear();
        }

        public bool WindowOpen { get { return _window != null; } }

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

        void OnContractCompleted(ContractState c)
        {
            AudioManager.Play(Sfx.Deliver);
            var cel = Celebrations.Instance;
            cel.Confetti();
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.45f);
            if (c.rewardCoins > 0) cel.FlyTo(center, (RectTransform)_coins.transform.parent, t => UIKit.CoinIcon(t, 48), 10);
            if (c.rewardNuggets > 0) cel.FlyTo(center, (RectTransform)_nuggets.transform.parent, t => UIKit.NuggetIcon(t, 48), 5);
            cel.Play("contract", "Contract Fulfilled!", c.client + " paid " + Fmt.Num(c.rewardCoins) + " coins.");
        }

        void OnLevelUp(int level)
        {
            if (level == Balance.GoldenAgeLevel) Celebrations.Instance.Play("goldenage", "The Golden Age!", "Streamliners, steel and silver. A new era begins.", force: true);
            else Celebrations.Instance.Play("levelup", "Level " + level + "!", G.Session.State.townName + " is growing.", force: level <= 3);
            AudioManager.Play(Sfx.LevelUp);
            Fx.Burst(CameraRig.Instance.Focus + Vector3.up * 12, 80);
            var unlocks = new List<string>();
            unlocks.AddRange(GameDatabase.Destinations.Where(d => d.UnlockLevel == level).Select(d => "Route: " + d.Name));
            unlocks.AddRange(GameDatabase.Locomotives.Where(d => d.UnlockLevel == level).Select(d => "Locomotive: " + d.Name));
            unlocks.AddRange(GameDatabase.Wagons.Where(d => d.UnlockLevel == level).Select(d => "Wagon: " + d.Name));
            unlocks.AddRange(GameDatabase.Buildings.Where(d => d.UnlockLevel == level).Select(d => "Building: " + d.Name));
            unlocks.AddRange(DepthContent.Projects.Where(d => d.UnlockLevel == level).Select(d => "Project: " + d.Name));
            if (level == Balance.GoldenAgeLevel) unlocks.Insert(0, "THE GOLDEN AGE BEGINS!");

            var dim = UIKit.Panel(_popupLayer, UIKit.Dim, "LevelUp");
            dim.sprite = null;
            UIKit.Stretch(dim.gameObject);
            var stack = UIKit.Node("Stack", dim.transform);
            UIKit.Place(stack, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000, 760));
            UIKit.VStack(stack, 0, 0, TextAnchor.UpperCenter).childForceExpandWidth = false; // children keep their preferred widths (ribbon, card)
            stack.AddComponent<WindowIntro>();
            UIDeco.Ribbon(stack.transform, level == Balance.GoldenAgeLevel ? "The Golden Age!" : "Level Up!", Palette.Hex(0x7A2E1E), 760, 120);
            var card = UIKit.Panel(stack.transform, new Color(0.13f, 0.09f, 0.06f, 0.92f), "Card", null, true);
            UIKit.Size(card.gameObject, 820, 560);
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
            var bg = UIKit.Panel(_markers, UIKit.Parchment, "Marker", UIKit.Rounded, true);
            m.Go = bg.gameObject;
            m.Bg = bg;
            UIKit.RT(m.Go).sizeDelta = new Vector2(96, 96);
            bg.gameObject.AddComponent<Outline>().effectColor = UIKit.BrassDark;
            var lbl = UIKit.Label(m.Go.transform, "", 20, UIKit.Ink, TextAnchor.MiddleCenter, true, FontStyle.Bold);
            UIKit.Anchor(lbl.gameObject, new Vector2(0, 0), new Vector2(1, 0), new Vector2(-30, -30), new Vector2(30, 2));
            lbl.gameObject.AddComponent<Outline>().effectColor = UIKit.Cream;
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
            m.Bg.color = progress >= 0 ? new Color(UIKit.Parchment.r, UIKit.Parchment.g, UIKit.Parchment.b, 0.85f) : UIKit.Parchment;
        }

        void UpdateMarkers()
        {
            _markerUsed = 0;
            if (S != null && !WindowOpen && TownView.Instance != null && TownView.Instance.PlacingDefId == null)
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
