using System;
using System.Collections.Generic;
using System.Linq;
using Ironhorse.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ironhorse.Game
{
    /// <summary>
    /// Territory projects on the map: a pin over every site (the character who wants the goods, the project's name and
    /// what it still needs), and the site view: the camera flies to the project, its stage card and character appear
    /// over the map, and a row of hauler cards along the bottom sends wagons and boats with the goods. The Depot's
    /// Haulers tab buys, upgrades and sells them.
    /// </summary>
    public sealed partial class UIRoot
    {
        // ================================================================ names and faces

        static readonly Dictionary<string, string> ShortNames = new Dictionary<string, string>
        {
            { "telegraph", "Fort Calder Wire" }, { "trestle", "Crooked River Trestle" }, { "mineshaft", "Deep Shaft" },
            { "resupply", "Fort Resupply" }, { "dam", "Silver Lake Dam" }, { "goldenspike", "The Golden Spike" },
            { "lighthouse", "Eagle Point Light" }, { "lodge", "Eagle Lake Lodge" },
            { "winery", "Valley Winery" }, { "mission", "Old Mission" }, { "opera", "Opera House" }, { "capitol", "The Capitol" },
        };

        static string ShortName(ProjectDef d) { string n; return ShortNames.TryGetValue(d.Id, out n) ? n : d.Name; }

        // Where each character's face sits in their full-length art: centre x, centre y from the top, square size (of height).
        static readonly Dictionary<string, Vector3> Faces = new Dictionary<string, Vector3>
        {
            { "foreman", new Vector3(0.59f, 0.13f, 0.25f) }, { "engineer", new Vector3(0.42f, 0.11f, 0.22f) },
            { "investor", new Vector3(0.37f, 0.13f, 0.24f) }, { "rival", new Vector3(0.59f, 0.11f, 0.22f) },
        };

        static readonly Dictionary<string, Sprite> _faceCache = new Dictionary<string, Sprite>();

        /// <summary>The character's face cropped from their portrait, for round badges.</summary>
        static Sprite Face(string character)
        {
            Sprite s;
            if (_faceCache.TryGetValue(character, out s)) return s;
            var tex = Resources.Load<Texture2D>("UI/Story/char_" + character);
            if (tex == null) return null;
            Vector3 f;
            if (!Faces.TryGetValue(character, out f)) f = new Vector3(0.5f, 0.12f, 0.24f);
            float size = f.z * tex.height;
            float cx = f.x * tex.width, cy = (1 - f.y) * tex.height;
            var r = new Rect(Mathf.Clamp(cx - size / 2, 0, tex.width - size), Mathf.Clamp(cy - size / 2, 0, tex.height - size), Mathf.Min(size, tex.width), size);
            s = Sprite.Create(tex, r, new Vector2(0.5f, 0.5f));
            _faceCache[character] = s;
            return s;
        }

        /// <summary>A round portrait in a gold rim.</summary>
        static GameObject FaceBadge(Transform p, string character, float size, bool grey = false)
        {
            var rim = UIKit.Node("Face", p);
            var ri = rim.AddComponent<Image>();
            ri.sprite = UIKit.Circle;
            ri.color = grey ? Palette.Hex(0x8A8F99) : BGold;
            ri.raycastTarget = false;
            UIKit.Size(rim, size, size);
            var inner = UIKit.Node("Mask", rim.transform);
            var mi = inner.AddComponent<Image>();
            mi.sprite = UIKit.Circle;
            mi.color = Palette.Hex(0x3A4459);
            mi.raycastTarget = false;
            inner.AddComponent<Mask>().showMaskGraphic = true;
            UIKit.Stretch(inner, size * 0.06f);
            var face = Face(character);
            if (face != null)
            {
                var fi = UIKit.Node("Art", inner.transform).AddComponent<Image>();
                fi.sprite = face;
                fi.raycastTarget = false;
                if (grey) fi.color = new Color(0.55f, 0.55f, 0.6f);
                UIKit.Stretch(fi.gameObject);
            }
            return rim;
        }

        // ================================================================ map pins

        sealed class Pin
        {
            public GameObject Go;
            public string Key;
        }

        readonly Dictionary<string, Pin> _pins = new Dictionary<string, Pin>();

        /// <summary>One pin per territory project the player can see: unlocked ones say what they need, the next
        /// locked one when it opens. Called every frame from UpdateMarkers.</summary>
        void UpdateProjectPins(bool show)
        {
            var sites = ProjectSites.Instance;
            var seen = new HashSet<string>();
            if (show && sites != null && S.HaulageUnlocked)
            {
                var cam = CameraRig.Instance.Cam;
                bool nextLockedShown = false;
                foreach (var def in DepthContent.Projects.OrderBy(d => d.UnlockLevel))
                {
                    var pr = S.Progress(def.Id);
                    bool locked = !S.IsProjectUnlocked(def);
                    if (pr.done) continue;
                    if (def.Requires != null && !S.Progress(def.Requires).done) continue; // hidden under the cloud bank
                    if (locked) { if (nextLockedShown) continue; nextLockedShown = true; }
                    var at = sites.PinAt(def.Id);
                    if (at == null) continue;
                    var sp = cam.WorldToScreenPoint(at.Value);
                    if (sp.z < 0) continue;
                    Vector2 local;
                    if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_markers, sp, null, out local)) continue;
                    seen.Add(def.Id);
                    string key = PinKey(def, locked);
                    Pin pin;
                    if (!_pins.TryGetValue(def.Id, out pin) || pin.Key != key)
                    {
                        if (pin != null) Destroy(pin.Go);
                        pin = new Pin { Go = BuildPin(def, locked), Key = key };
                        _pins[def.Id] = pin;
                    }
                    pin.Go.SetActive(true);
                    var rt = UIKit.RT(pin.Go);
                    float scale = Mathf.Lerp(1.0f, 0.72f, Mathf.InverseLerp(60, 320, CameraRig.Instance.Distance));
                    rt.localScale = Vector3.one * scale;
                    rt.anchoredPosition = local;
                }
            }
            foreach (var kv in _pins) if (!seen.Contains(kv.Key)) kv.Value.Go.SetActive(false);
        }

        string PinKey(ProjectDef def, bool locked)
        {
            if (locked) return "L" + S.Level;
            if (StoryDirector.Instance != null && StoryDirector.Instance.IsNew(def.Id)) return "NEW";
            var pr = S.Progress(def.Id);
            int need = S.StillToSend(def.Id);
            int moving = S.HaulersTo(def.Id).Length;
            return pr.stage + ":" + need + ":" + moving;
        }

        GameObject BuildPin(ProjectDef def, bool locked)
        {
            var route = Haulage.Route(def.Id);
            var go = UIKit.Node("Pin " + def.Id, _markers);
            var rt = UIKit.RT(go);
            rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(300, 250);
            var col = UIKit.VStack(go, -14, 0, TextAnchor.LowerCenter);
            col.childForceExpandWidth = false;
            col.childForceExpandHeight = false;
            col.reverseArrangement = false;

            var face = FaceBadge(go.transform, route != null ? route.Character : "foreman", 132, locked);
            HudShadow(face);
            bool isNew = !locked && StoryDirector.Instance != null && StoryDirector.Instance.IsNew(def.Id);
            if (isNew)
            {
                // The storyline hasn't started: a NEW flag, and the pin bobs to draw the eye.
                var flag = Box(face.transform, UIKit.Red, 86, 38, "New");
                Row(flag.gameObject, 0, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);
                T(flag.transform, "NEW", 26, Color.white, TextAnchor.MiddleCenter, true);
                Ignore(flag);
                UIKit.Place(flag.gameObject, new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-14, -12), new Vector2(86, 38));
                go.AddComponent<PinBob>();
            }
            var ribbon = Box(go.transform, Palette.Hex(0x1F2737), -1, 48, "Name");
            ribbon.color = new Color(0.1f, 0.12f, 0.17f, 0.92f);
            Row(ribbon.gameObject, 0, new RectOffset(18, 18, 0, 0), TextAnchor.MiddleCenter);
            ribbon.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            T(ribbon.transform, ShortName(def), 30, Color.white, TextAnchor.MiddleCenter, true).horizontalOverflow = HorizontalWrapMode.Overflow;

            var need = Box(go.transform, Palette.Hex(0x2B3345), -1, 46, "Need");
            need.color = new Color(0.16f, 0.19f, 0.26f, 0.95f);
            Row(need.gameObject, 8, new RectOffset(12, 16, 2, 2), TextAnchor.MiddleCenter);
            need.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            if (isNew)
            {
                T(need.transform, "Tap to hear the story", 26, BGold, TextAnchor.MiddleCenter, true).horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            else if (locked)
            {
                UIKit.Icon(need.transform, "lock", 32);
                T(need.transform, S.Level >= def.UnlockLevel ? "Locked" : "Level " + def.UnlockLevel, 28, Color.white, TextAnchor.MiddleCenter, true).horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            else
            {
                var pr = S.Progress(def.Id);
                var stage = def.Stages[pr.stage];
                int toSend = S.StillToSend(def.Id);
                var moving = S.HaulersTo(def.Id);
                if (toSend <= 0 && moving.Length > 0)
                {
                    ClockIcon(need.transform, 32);
                    var t = T(need.transform, "", 28, Color.white, TextAnchor.MiddleCenter, true);
                    t.horizontalOverflow = HorizontalWrapMode.Overflow;
                    var pid = def.Id;
                    go.AddComponent<PinTimer>().Tick = () =>
                    {
                        var m = S.HaulersTo(pid);
                        t.text = m.Length == 0 ? "" : Fmt.Time(m.Max(h => S.HaulRemaining(h)));
                    };
                }
                else
                {
                    UIKit.ResIcon(need.transform, stage.ResourceId, 36);
                    T(need.transform, Fmt.Num(toSend), 30, S.Amount(stage.ResourceId) > 0 ? Color.white : BBad, TextAnchor.MiddleCenter, true).horizontalOverflow = HorizontalWrapMode.Overflow;
                    if (moving.Length > 0) Pill(need.transform, null, moving.Length + " en route", 22, BGood, new Color(0, 0, 0, 0.25f));
                }
            }
            string id = def.Id;
            var hit = go.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            Clickable(go, () => { if (locked) Toast(def.Name + ": " + S.ProjectLockText(def) + ".", false); else OpenProjectSite(id); });
            return go;
        }

        sealed class PinBob : MonoBehaviour
        {
            void LateUpdate()
            {
                var rt = (RectTransform)transform;
                rt.anchoredPosition += new Vector2(0, Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f)) * 14f);
            }
        }

        /// <summary>Per-frame label updates on a pin.</summary>
        sealed class PinTimer : MonoBehaviour
        {
            public Action Tick;
            float _t;
            void Update() { _t -= Time.unscaledDeltaTime; if (_t > 0) return; _t = 0.25f; Tick?.Invoke(); }
        }

        // ================================================================ destination pins

        readonly Dictionary<string, Pin> _destPins = new Dictionary<string, Pin>();

        /// <summary>A pin over every destination on the map: its name and cargo (a lock and level while closed);
        /// tapping it sends a train there.</summary>
        void UpdateDestinationPins(bool show)
        {
            var seen = new HashSet<string>();
            if (show && CameraRig.Instance != null)
            {
                var cam = CameraRig.Instance.Cam;
                foreach (var d in GameDatabase.Destinations)
                {
                    var at = DestinationSites.PinAt(d.Id);
                    if (at == null) continue;
                    var sp = cam.WorldToScreenPoint(at.Value);
                    if (sp.z < 0) continue;
                    Vector2 local;
                    if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_markers, sp, null, out local)) continue;
                    seen.Add(d.Id);
                    bool locked = !S.IsDestinationUnlocked(d);
                    string key = locked ? "L" : "U";
                    Pin pin;
                    if (!_destPins.TryGetValue(d.Id, out pin) || pin.Key != key)
                    {
                        if (pin != null) Destroy(pin.Go);
                        pin = new Pin { Go = BuildDestPin(d, locked), Key = key };
                        _destPins[d.Id] = pin;
                    }
                    pin.Go.SetActive(true);
                    var rt = UIKit.RT(pin.Go);
                    rt.localScale = Vector3.one * Mathf.Lerp(0.95f, 0.66f, Mathf.InverseLerp(60, 320, CameraRig.Instance.Distance));
                    rt.anchoredPosition = local;
                }
            }
            foreach (var kv in _destPins) if (!seen.Contains(kv.Key)) kv.Value.Go.SetActive(false);
        }

        GameObject BuildDestPin(DestinationDef d, bool locked)
        {
            var go = UIKit.Node("Dest " + d.Id, _markers);
            go.transform.SetAsFirstSibling(); // under the project pins
            var rt = UIKit.RT(go);
            rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(300, 140);
            var col = UIKit.VStack(go, -10, 0, TextAnchor.LowerCenter);
            col.childForceExpandWidth = false;
            col.childForceExpandHeight = false;
            var badge = UIKit.Node("Badge", go.transform);
            var bi = badge.AddComponent<Image>();
            bi.sprite = UIKit.Circle;
            bi.color = locked ? Palette.Hex(0x8A8F99) : Color.white;
            UIKit.Size(badge, 84, 84);
            HudShadow(badge);
            var holder = UIKit.Node("Icon", badge.transform);
            UIKit.Stretch(holder, 10);
            UIKit.HStack(holder, 0, 0, TextAnchor.MiddleCenter);
            if (locked) UIKit.Icon(holder.transform, "lock", 52);
            else if (d.IsTown) UIKit.Icon(holder.transform, "people", 60);
            else UIKit.ResIcon(holder.transform, d.ResourceId, 58);
            var ribbon = Box(go.transform, BCardDeep, -1, 44, "Name");
            ribbon.color = new Color(0.1f, 0.12f, 0.17f, 0.9f);
            Row(ribbon.gameObject, 6, new RectOffset(14, 14, 0, 0), TextAnchor.MiddleCenter);
            ribbon.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            T(ribbon.transform, locked ? d.Name + "  ·  Lv " + d.UnlockLevel : d.Name, 26, locked ? BLabel : Color.white, TextAnchor.MiddleCenter, true).horizontalOverflow = HorizontalWrapMode.Overflow;
            var hit = go.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            string id = d.Id;
            Clickable(go, () => { if (locked) Toast(d.Name + " opens at level " + d.UnlockLevel + ".", false); else OpenTrainTo(id); });
            return go;
        }

        // ================================================================ the site view

        string _siteId;
        string _siteFilter = "all";

        /// <summary>"Get More Resources": where the goods come from (Go) and any finished batch waiting at the factory
        /// that makes them (Claim).</summary>
        void GetMore(string resId)
        {
            var res = GameDatabase.Resource(resId);
            var maker = MakerOf(resId);
            var src = SourceOf(resId);
            var b = maker != null ? S.State.buildings.FirstOrDefault(x => x.defId == maker.Id) : null;
            int waiting = 0;
            if (b != null && S.IsProductionDone(b)) waiting = maker.Recipe.Output.amount * S.FinishedJobs(b);
            var dim = UIKit.Panel(_popupLayer, new Color(0, 0, 0, 0.75f), "GetMore");
            dim.sprite = null;
            UIKit.Stretch(dim.gameObject);
            var card = Box(dim.transform, BCardDeep, 900, 560, "Card");
            UIKit.Place(card.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900, 560));
            card.gameObject.AddComponent<WindowIntro>();
            Stack(card.gameObject, 16, new RectOffset(28, 28, 22, 26), TextAnchor.UpperCenter);
            var top = HBox(card.transform, 0, 0, TextAnchor.MiddleLeft, -1, 64);
            UIKit.Size(T(top.transform, "Get More Resources", 44, BGold, TextAnchor.MiddleLeft, true).gameObject, -1, -1, 1);
            var go = dim.gameObject;
            Big(top.transform, "X", UIKit.Red, () => Destroy(go), 76, 64, true, 40);
            var head = Box(card.transform, Palette.Hex(0xC9D3E3), 440, 130, "Res");
            Row(head.gameObject, 16, new RectOffset(18, 18, 0, 0), TextAnchor.MiddleCenter);
            UIKit.ResIcon(head.transform, resId, 96);
            T(head.transform, res.Name, 38, Palette.Hex(0x1F2737), TextAnchor.MiddleLeft, true);
            Pill(head.transform, tr => UIKit.Icon(tr, "crate", 34), Fmt.Num(S.Amount(resId)), 30, null, BTile);
            var row = HBox(card.transform, 24, 0, TextAnchor.MiddleCenter, -1, 230);
            var a = VBox(row.transform, 10, 0, TextAnchor.MiddleCenter, 380);
            T(a.transform, "Get more", 30, BLabel, TextAnchor.MiddleCenter, false, UIKit.Heading);
            string where = src != null ? src.Name : maker != null ? maker.Name : "the territory";
            var place = Box(a.transform, BTile, 360, 110, "Where");
            Row(place.gameObject, 10, new RectOffset(14, 14, 0, 0), TextAnchor.MiddleCenter);
            UIKit.Icon(place.transform, "nav_territory", 54);
            T1(place.transform, where, 30, Color.white, TextAnchor.MiddleLeft, true);
            Big(a.transform, "Go", BBlue, () => { Destroy(go); CloseProjectSite(); GoGet(resId); }, 360, 84, true, 40);
            if (maker != null)
            {
                var c = VBox(row.transform, 10, 0, TextAnchor.MiddleCenter, 380);
                T(c.transform, "Factory produce", 30, BLabel, TextAnchor.MiddleCenter, false, UIKit.Heading);
                var wait = Box(c.transform, BTile, 360, 110, "Waiting");
                Row(wait.gameObject, 10, new RectOffset(14, 14, 0, 0), TextAnchor.MiddleCenter);
                UIKit.ResIcon(wait.transform, resId, 54);
                T(wait.transform, waiting.ToString(), 40, Color.white, TextAnchor.MiddleCenter, true);
                int uid = b != null ? b.uid : 0;
                Big(c.transform, "Claim", waiting > 0 ? UIKit.Green : UIKit.Disabled, () => { var r = S.CollectProduction(uid); Result(r); if (r.Ok) Destroy(go); }, 360, 84, waiting > 0, 40);
            }
        }
        GameObject _site;
        readonly List<Action> _siteLive = new List<Action>();
        float _siteTick;

        public bool SiteOpen { get { return _site != null; } }

        /// <summary>Flies to a project on the map and opens its delivery view.</summary>
        public void OpenProjectSite(string projectId)
        {
            var def = DepthContent.Project(projectId);
            if (def == null) return;
            CloseWindow();
            if (!S.IsProjectUnlocked(def)) { Toast(def.Name + ": " + S.ProjectLockText(def) + ".", false); return; }
            if (ProjectSites.Instance != null) ProjectSites.Instance.Show(projectId);
            // A storyline's first visit: the characters tell what they want built, then the site opens.
            if (StoryDirector.Instance != null && StoryDirector.Instance.IsNew(projectId))
            {
                StoryDirector.Instance.Open(projectId, () => OpenProjectSite(projectId));
                return;
            }
            _siteId = projectId;
            BuildSiteView();
        }

        public void CloseProjectSite()
        {
            if (_site != null) Destroy(_site);
            _site = null;
            _siteId = null;
            _siteLive.Clear();
            SetHudVisible(true);
        }

        /// <summary>Rebuilds the open site view (on any change) and runs its live timers.</summary>
        void UpdateSiteView(bool dirty)
        {
            if (_siteId == null) return;
            if (WindowOpen) { CloseProjectSite(); return; }
            if (dirty) BuildSiteView();
            _siteTick -= Time.unscaledDeltaTime;
            if (_siteTick > 0) return;
            _siteTick = 0.25f;
            foreach (var a in _siteLive.ToList()) a();
        }

        void BuildSiteView()
        {
            var def = DepthContent.Project(_siteId);
            var pr = S.Progress(_siteId);
            float scroll = -1;
            if (_site != null)
            {
                var old = _site.GetComponentInChildren<ScrollRect>();
                if (old != null) scroll = old.horizontalNormalizedPosition;
                Destroy(_site);
            }
            _siteLive.Clear();
            SetHudVisible(false);
            var route = Haulage.Route(_siteId);

            var root = UIKit.Node("Site", _popupLayer);
            UIKit.Stretch(root);
            _site = root;
            var block = root.AddComponent<Image>();
            block.color = new Color(0, 0, 0, 0); // the map stays visible but doesn't take taps while the view is up
            var safe = UIKit.Node("Safe", root.transform);
            UIKit.Stretch(safe);
            safe.AddComponent<SafeArea>();

            // Bottom band behind the hauler cards.
            var band = UIKit.Node("Band", safe.transform).AddComponent<Image>();
            band.sprite = Fade(true);
            band.color = new Color(0.05f, 0.07f, 0.1f, 0.88f);
            band.raycastTarget = false;
            UIKit.Anchor(band.gameObject, new Vector2(0, 0), new Vector2(1, 0), new Vector2(-200, -200), new Vector2(200, 520));

            // The character on the right.
            var tex = route != null ? Resources.Load<Texture2D>("UI/Story/char_" + route.Character) : null;
            if (tex != null)
            {
                var who = UIKit.Node("Character", safe.transform).AddComponent<Image>();
                who.sprite = Sprite.Create(tex, new Rect(0, tex.height * 0.38f, tex.width, tex.height * 0.62f), new Vector2(0.5f, 0));
                who.preserveAspect = true;
                who.raycastTarget = false;
                float h = 640;
                UIKit.Place(who.gameObject, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-40, 330), new Vector2(h * tex.width / (tex.height * 0.62f), h));
                who.gameObject.AddComponent<SlideIn>();
                var tag = Box(safe.transform, BCardDeep, -1, 50, "Who");
                tag.color = new Color(0.1f, 0.12f, 0.17f, 0.92f);
                Row(tag.gameObject, 0, new RectOffset(18, 18, 0, 0), TextAnchor.MiddleCenter);
                tag.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                T(tag.transform, route.CharacterName, 30, BGold, TextAnchor.MiddleCenter, true).horizontalOverflow = HorizontalWrapMode.Overflow;
                UIKit.Place(tag.gameObject, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-120, 350), new Vector2(300, 50));
            }

            StageCard(safe.transform, def, pr);
            TopRight(safe.transform);
            HaulerStrip(safe.transform, def, pr, scroll);
        }

        void TopRight(Transform p)
        {
            var row = HBox(p, 14, 0, TextAnchor.MiddleRight);
            UIKit.Place(row, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-30, -24), new Vector2(900, 84));
            Pill(row.transform, tr => UIKit.CoinIcon(tr, 46), Fmt.Num(S.Coins), 36, null, new Color(0.08f, 0.1f, 0.14f, 0.92f));
            Pill(row.transform, tr => UIKit.NuggetIcon(tr, 46), Fmt.Num(S.Nuggets), 36, null, new Color(0.08f, 0.1f, 0.14f, 0.92f));
            Big(row.transform, "X", UIKit.Red, CloseProjectSite, 90, 84, true, 46);
        }

        void StageCard(Transform p, ProjectDef def, ProjectProgress pr)
        {
            var card = Box(p, BCardDeep, 620, -1, "Stage");
            card.color = new Color(0.12f, 0.15f, 0.21f, 0.94f);
            UIKit.Place(card.gameObject, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -24), new Vector2(620, 520));
            Stack(card.gameObject, 12, new RectOffset(26, 26, 22, 22), TextAnchor.UpperLeft);
            T(card.transform, "Stage " + Mathf.Min(pr.stage + 1, def.Stages.Length) + " / " + def.Stages.Length, 28, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);
            T1(card.transform, ShortName(def), 52, BGold, TextAnchor.MiddleLeft, true);
            var stage = def.Stages[pr.stage];
            var res = GameDatabase.Resource(stage.ResourceId);
            int inFlight = S.InFlight(def.Id), toSend = S.StillToSend(def.Id);

            var goods = Box(card.transform, Palette.Hex(0xC9D3E3), -1, 190, "Goods");
            Row(goods.gameObject, 16, new RectOffset(14, 16, 14, 14), TextAnchor.MiddleLeft);
            var art = Box(goods.transform, Color.white, 160, 160, "Art");
            Row(art.gameObject, 0, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);
            UIKit.ResIcon(art.transform, stage.ResourceId, 120);
            var col = VBox(goods.transform, 8, 0, TextAnchor.MiddleLeft, -1, -1, 1);
            T(col.transform, res.Name, 30, Palette.Hex(0x2B3345), TextAnchor.MiddleLeft, false, UIKit.Heading);
            if (toSend <= 0 && inFlight > 0)
            {
                T1(col.transform, "Haulers finishing the stage", 32, Palette.Hex(0x1F2737), TextAnchor.MiddleLeft);
                var trk = HBox(col.transform, 8, 0, TextAnchor.MiddleLeft, -1, 40);
                ClockIcon(trk.transform, 38);
                var fill = Track(trk.transform, 30, BGold);
                var left = T(fill.transform.parent, "", 26, Color.white, TextAnchor.MiddleRight, true);
                UIKit.Anchor(left.gameObject, Vector2.zero, Vector2.one, new Vector2(8, 0), new Vector2(-10, 0));
                string pid = def.Id;
                Action live = () =>
                {
                    var m = S.HaulersTo(pid);
                    if (m.Length == 0) return;
                    var last = m.OrderByDescending(h => h.arrivesAt).First();
                    UIKit.SetBar(fill, S.HaulProgress(last));
                    left.text = Fmt.Time(S.HaulRemaining(last));
                };
                live(); _siteLive.Add(live);
            }
            else
            {
                var line = HBox(col.transform, 6, 0, TextAnchor.MiddleLeft, -1, 44);
                var dl = Box(line.transform, Color.white, -1, 44, "Deliver");
                UIKit.Size(dl.gameObject, -1, 44, 1);
                Row(dl.gameObject, 6, new RectOffset(14, 14, 0, 0), TextAnchor.MiddleLeft);
                UIKit.Size(T(dl.transform, "Deliver:", 30, Palette.Hex(0x1F2737), TextAnchor.MiddleLeft).gameObject, -1, -1, 1);
                T(dl.transform, Fmt.Num(stage.Amount - pr.delivered), 32, Palette.Hex(0x1F2737), TextAnchor.MiddleRight).horizontalOverflow = HorizontalWrapMode.Overflow;
                var bar = Track(col.transform, 22, BGreen);
                UIKit.SetBar(bar, (pr.delivered + inFlight) / (float)stage.Amount);
                var have = HBox(col.transform, 8, 0, TextAnchor.MiddleRight, -1, 40);
                Flex(have.transform);
                UIKit.Icon(have.transform, "crate", 36);
                T(have.transform, Fmt.Num(S.Amount(stage.ResourceId)), 30, S.Amount(stage.ResourceId) > 0 ? BGreen : BBad, TextAnchor.MiddleRight, true).horizontalOverflow = HorizontalWrapMode.Overflow;
            }

            T(card.transform, "Rewards", 30, BGold, TextAnchor.MiddleLeft, false, UIKit.Heading);
            var rw = HBox(card.transform, 14, 0, TextAnchor.MiddleLeft, -1, 72);
            if (stage.Xp > 0) Pill(rw.transform, t => UIDeco.XpIcon(t, 50), stage.Xp.ToString(), 32, null, BTile);
            if (stage.Coins > 0) Pill(rw.transform, t => UIKit.CoinIcon(t, 50), Fmt.Num(stage.Coins), 32, null, BTile);
            if (pr.stage == def.Stages.Length - 1) Pill(rw.transform, null, def.PerkText, 26, BGood, BTile);
        }

        void HaulerStrip(Transform p, ProjectDef def, ProjectProgress pr, float scroll)
        {
            var area = UIKit.Node("Haulers", p);
            UIKit.Anchor(area, new Vector2(0, 0), new Vector2(1, 0), new Vector2(30, 20), new Vector2(-30, 400));
            Stack(area, 12, new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft);

            // Info strip: what to do next.
            var stage = def.Stages[pr.stage];
            var res = GameDatabase.Resource(stage.ResourceId);
            int toSend = S.StillToSend(def.Id);
            var info = HBox(area.transform, 12, 0, TextAnchor.MiddleLeft, -1, 64);
            var chip = Box(info.transform, BCardDeep, -1, 60, "Info");
            chip.color = new Color(0.1f, 0.12f, 0.17f, 0.94f);
            Row(chip.gameObject, 12, new RectOffset(14, 22, 0, 0), TextAnchor.MiddleLeft);
            chip.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var ib = Box(chip.transform, BBlue, 44, 44, "i");
            Row(ib.gameObject, 0, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);
            T(ib.transform, "i", 32, Color.white, TextAnchor.MiddleCenter, true);
            string msg = toSend <= 0 ? "The last batch is on its way."
                : S.Amount(stage.ResourceId) <= 0 ? "No " + res.Name + " in the warehouse yet."
                : "Tap a hauler to deliver the " + res.Name + ".";
            T(chip.transform, msg, 32, Color.white, TextAnchor.MiddleLeft, false, UIKit.Heading).horizontalOverflow = HorizontalWrapMode.Overflow;
            if (toSend > 0 && S.Amount(stage.ResourceId) <= 0)
            {
                string rid = stage.ResourceId;
                Big(info.transform, "Get " + res.Name, BBlue, () => GetMore(rid), 300, 60, true, 30);
            }
            var route = Haulage.Route(def.Id);
            if (route != null && route.WaterMeters > 0) Pill(info.transform, null, "On the river: boats can reach it", 26, BGood, new Color(0.1f, 0.12f, 0.17f, 0.9f));

            // Filter by how they travel, like the depot.
            var tabs = HBox(info.transform, 8, 0, TextAnchor.MiddleRight, -1, 60, 1);
            foreach (var f in new[] { ("all", "All"), ("road", "Road"), ("water", "River") })
            {
                string key = f.Item1;
                Big(tabs.transform, f.Item2, _siteFilter == key ? UIKit.Gold : UIKit.WoodLight, () => { _siteFilter = key; BuildSiteView(); }, 150, 56, true, 28);
            }
            var holder = HBox(area.transform, 0, 0, TextAnchor.UpperLeft, -1, 310);
            var list = Scroller(holder.transform, "sitehaulers", true, 18);
            // Haulers at work here first, then the idle ones, then the rest of the yard, then ones to buy.
            var owned = S.State.haulers
                .OrderBy(h => h.projectId == def.Id ? 0 : h.IsIdle ? 1 : 2)
                .ThenBy(h => { int l; double sec; return S.CantHaul(h, def.Id, out l, out sec) == null ? 0 : 1; })
                .ThenByDescending(h => S.HaulCapacity(h)).ToList();
            System.Func<HaulerDef, bool> pass = d => _siteFilter == "all" || (_siteFilter == "water") == d.OnWater;
            foreach (var h in owned) if (pass(Haulage.Hauler(h.defId))) HaulerCard(list, h, def);
            foreach (var d in Haulage.Haulers)
            {
                if (!pass(d)) continue;
                bool reach = Haulage.Distance(d, route) > 0;
                if (!reach || S.HaulersOwned(d.Id) > 0) continue;
                BuyHaulerCard(list, d);
            }
            if (scroll >= 0) { var sr = holder.GetComponentInChildren<ScrollRect>(); if (sr != null) { Canvas.ForceUpdateCanvases(); sr.horizontalNormalizedPosition = scroll; } }
        }

        static Color RarityTop(Rarity r)
        {
            switch (r)
            {
                case Rarity.Uncommon: return Palette.Hex(0x5C8A4A);
                case Rarity.Rare: return Palette.Hex(0x4F6FB8);
                case Rarity.Epic: return Palette.Hex(0x8A55C0);
                case Rarity.Legendary: return Palette.Hex(0xC08A2A);
                default: return Palette.Hex(0x8A8F99);
            }
        }

        GameObject CardShell(Transform p, HaulerDef d, out Transform artZone, out Transform foot, bool highlight)
        {
            var card = Box(p, BCardDeep, 330, 300, "Hauler " + d.Id);
            card.color = new Color(0.13f, 0.16f, 0.22f, 0.97f);
            if (highlight)
            {
                var o = card.gameObject.AddComponent<Outline>();
                o.effectColor = BGold;
                o.effectDistance = new Vector2(4, -4);
            }
            Stack(card.gameObject, 8, new RectOffset(12, 12, 10, 12), TextAnchor.UpperLeft);
            T1(card.transform, d.Name, 30, Color.white, TextAnchor.MiddleLeft, false, UIKit.Heading);
            var art = Box(card.transform, RarityTop(d.Rarity), -1, 170, "Art");
            UIKit.Size(art.gameObject, -1, 170);
            var tex = ModelIcons.Hauler(d);
            if (tex != null)
            {
                var img = ModelIcons.Image(art.transform, tex, 290, 168);
                UIKit.Place(img.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(290, 168));
            }
            artZone = art.transform;
            var f = HBox(card.transform, 8, 0, TextAnchor.MiddleLeft, -1, 74);
            foot = f.transform;
            return card.gameObject;
        }

        void HaulerCard(Transform p, HaulerState h, ProjectDef def)
        {
            var d = Haulage.Hauler(h.defId);
            int load; double secs;
            string why = S.CantHaul(h, def.Id, out load, out secs);
            bool here = h.projectId == def.Id;
            Transform art, foot;
            var card = CardShell(p, d, out art, out foot, why == null);
            int uid = h.uid;

            if (here || !h.IsIdle)
            {
                // Out on a delivery: a timer over the art, Speed up underneath.
                var shade = Box(art, new Color(0.05f, 0.07f, 0.1f, 0.55f), -1, -1, "Busy");
                UIKit.Stretch(shade.gameObject);
                var tag = Pill(art, null, here ? "Delivering" : "On a delivery", 28, Color.white, new Color(0.1f, 0.12f, 0.17f, 0.9f));
                Ignore(tag);
                UIKit.Place(tag, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 26), new Vector2(240, 46));
                var trk = HBox(art, 8, 0, TextAnchor.MiddleLeft, 260, 36);
                Ignore(trk);
                UIKit.Place(trk, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(260, 36));
                ClockIcon(trk.transform, 34);
                var fill = Track(trk.transform, 28, BGold);
                var left = T(fill.transform.parent, "", 24, Color.white, TextAnchor.MiddleRight, true);
                UIKit.Anchor(left.gameObject, Vector2.zero, Vector2.one, new Vector2(8, 0), new Vector2(-10, 0));
                Action live = () => { var x = S.Hauler(uid); if (x == null || x.IsIdle) return; UIKit.SetBar(fill, S.HaulProgress(x)); left.text = Fmt.Time(S.HaulRemaining(x)); };
                live(); _siteLive.Add(live);
                int cost = S.HaulRushCost(h);
                PriceBtn(foot, "Speed up", BBlue, tr => UIKit.NuggetIcon(tr, 40), cost.ToString(), () => Result(S.RushHauler(uid), Sfx.Deliver), 306, 70, S.Nuggets >= cost, 32);
                return;
            }

            Pill(foot, tr => UIKit.Icon(tr, "crate", 34), (why == null ? load : S.HaulCapacity(h)) + " / " + S.HaulCapacity(h), 28, null, BTile);
            if (secs > 0) Pill(foot, tr => ClockIcon(tr, 32), Fmt.Time(secs), 28, null, BTile);
            if (h.level > 1) Pill(foot, null, "Lv " + h.level, 26, BGold, BTile);
            if (why == null)
            {
                string pid = def.Id;
                card.GetComponent<Image>().raycastTarget = true;
                Clickable(card, () => SendFromSite(uid, pid));
                var go = Big(art, "Send", UIKit.Green, () => SendFromSite(uid, pid), 150, 64, true, 34);
                Ignore(go);
                UIKit.Place(go.gameObject, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-8, 8), new Vector2(150, 64));
            }
            else
            {
                var shade = Box(art, new Color(0.05f, 0.07f, 0.1f, 0.5f), -1, -1, "No");
                UIKit.Stretch(shade.gameObject);
                var msg = T(art, why, 24, Color.white, TextAnchor.MiddleCenter, true);
                UIKit.Anchor(msg.gameObject, Vector2.zero, Vector2.one, new Vector2(10, 10), new Vector2(-10, -10));
            }
        }

        /// <summary>Takes a widget out of its parent's layout (it is placed by hand).</summary>
        static void Ignore(Component c) { Ignore(c.gameObject); }
        static void Ignore(GameObject go)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            le.ignoreLayout = true;
        }

        void SendFromSite(int uid, string projectId)
        {
            var r = S.SendHauler(uid, projectId);
            Result(r, Sfx.Deliver);
            if (r.Ok) Haptics.Play(Haptic.Light);
        }

        void BuyHaulerCard(Transform p, HaulerDef d)
        {
            Transform art, foot;
            bool locked = !S.IsHaulerUnlocked(d);
            CardShell(p, d, out art, out foot, false);
            var shade = Box(art, new Color(0.05f, 0.07f, 0.1f, locked ? 0.65f : 0.35f), -1, -1, "Shade");
            UIKit.Stretch(shade.gameObject);
            var tag = Pill(art, locked ? (Action<Transform>)(tr => UIKit.Icon(tr, "lock", 30)) : null, locked ? "Level " + d.UnlockLevel : "Not owned", 28, Color.white, new Color(0.1f, 0.12f, 0.17f, 0.9f));
            Ignore(tag);
            UIKit.Place(tag, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(220, 46));
            if (locked)
            {
                Pill(foot, tr => UIKit.Icon(tr, "crate", 34), Haulage.CapacityAt(d, 1).ToString(), 28, BLabel, BTile);
                PriceTag(foot, d, false);
                return;
            }
            PriceBtnFor(foot, d, 306, 70);
        }

        void PriceTag(Transform p, HaulerDef d, bool can)
        {
            bool nug = d.PriceNuggets > 0;
            Pill(p, tr => { if (nug) UIKit.NuggetIcon(tr, 38); else UIKit.CoinIcon(tr, 38); }, Fmt.Num(nug ? d.PriceNuggets : d.PriceCoins), 28, can ? Color.white : BBad, BTile);
        }

        void PriceBtnFor(Transform p, HaulerDef d, float w, float h)
        {
            bool nug = d.PriceNuggets > 0;
            int price = nug ? d.PriceNuggets : d.PriceCoins;
            bool can = (nug ? S.Nuggets : S.Coins) >= price;
            string id = d.Id;
            PriceBtn(p, "Buy", can ? UIKit.Green : UIKit.Disabled, tr => { if (nug) UIKit.NuggetIcon(tr, 40); else UIKit.CoinIcon(tr, 40); }, Fmt.Num(price),
                () => Result(S.BuyHauler(id), Sfx.Bell), w, h, true, 32);
        }

        // ================================================================ Depot: Haulers tab

        void HaulerDepot(Transform body)
        {
            _boardSub = S.State.haulers.Count + " in the freight yard";
            var panel = Box(body, BTile, -1, -1, "Panel");
            UIKit.Stretch(panel.gameObject);
            Stack(panel.gameObject, 10, new RectOffset(14, 14, 12, 14));
            if (!S.HaulageUnlocked)
            {
                T(panel.transform, "Haulers carry goods by road and river to territory projects. They arrive with the first project at level " + Haulage.UnlockLevel + ".", 32, Color.white, TextAnchor.MiddleCenter, false, UIKit.Heading);
                return;
            }
            T1(panel.transform, "Wagons take the roads, boats the lake and the river. Tap a project's pin on the map to send them.", 30, Color.white, TextAnchor.MiddleLeft, false, UIKit.Heading);
            var cards = Scroller(panel.transform, "haulershop", true, 16);
            foreach (var d in Haulage.Haulers)
            {
                var mine = S.State.haulers.Where(h => h.defId == d.Id).ToList();
                if (mine.Count == 0) { YardBuyCard(cards, d); continue; }
                foreach (var h in mine) YardCard(cards, h, d);
            }
        }

        void YardBuyCard(Transform p, HaulerDef d)
        {
            var card = Box(p, BCardDeep, 380, -1, "Buy " + d.Id);
            Stack(card.gameObject, 10, new RectOffset(16, 16, 14, 16), TextAnchor.UpperLeft);
            bool locked = !S.IsHaulerUnlocked(d);
            T1(card.transform, d.Name, 34, BGold, TextAnchor.MiddleLeft, true);
            T1(card.transform, (d.OnWater ? "River & lake" : d.Kind == HaulKind.Truck ? "Road · motor" : "Road · horse-drawn") + " · " + d.Rarity, 24, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);
            var art = Box(card.transform, RarityTop(d.Rarity), -1, 190, "Art");
            var tex = ModelIcons.Hauler(d);
            if (tex != null) { var img = ModelIcons.Image(art.transform, tex, 330, 188); UIKit.Place(img.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(330, 188)); if (locked) img.color = new Color(0.2f, 0.2f, 0.25f); }
            var stats = HBox(card.transform, 8, 0, TextAnchor.MiddleLeft, -1, 52);
            Pill(stats.transform, tr => UIKit.Icon(tr, "crate", 34), Haulage.CapacityAt(d, 1) + " per trip", 26, null, BTile);
            Pill(stats.transform, null, d.Speed.ToString("0.0") + " m/s", 26, null, BTile);
            var blurb = T(card.transform, "“" + d.Blurb + "”", 24, BLabel, TextAnchor.UpperLeft, false, UIKit.Body);
            blurb.fontStyle = FontStyle.Italic;
            Flex(card.transform);
            if (locked) Big(card.transform, Locks.Text(d.UnlockLevel), UIKit.Disabled, null, -1, 80, false, 30);
            else PriceBtnFor(card.transform, d, -1, 80);
        }

        void YardCard(Transform p, HaulerState h, HaulerDef d)
        {
            var card = Box(p, BCardDeep, 380, -1, "Own " + d.Id);
            Stack(card.gameObject, 10, new RectOffset(16, 16, 14, 16), TextAnchor.UpperLeft);
            var top = HBox(card.transform, 8, 0, TextAnchor.MiddleLeft, -1, 48);
            UIKit.Size(T1(top.transform, d.Name, 34, BGold, TextAnchor.MiddleLeft, true).gameObject, -1, -1, 1);
            Pill(top.transform, null, "Lv " + h.level, 26, Color.white, BTile);
            var art = Box(card.transform, RarityTop(d.Rarity), -1, 190, "Art");
            var tex = ModelIcons.Hauler(d);
            if (tex != null) { var img = ModelIcons.Image(art.transform, tex, 330, 188); UIKit.Place(img.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(330, 188)); }
            var status = Pill(art.transform, null, h.IsIdle ? (d.OnWater ? "At the landing" : "In the yard") : "Hauling to " + ShortName(DepthContent.Project(h.projectId)), 24, h.IsIdle ? BGood : Color.white, new Color(0.06f, 0.08f, 0.11f, 0.85f));
            Ignore(status);
            UIKit.Place(status, new Vector2(0, 1), new Vector2(0, 1), new Vector2(8, -8), new Vector2(300, 42));
            var stats = HBox(card.transform, 8, 0, TextAnchor.MiddleLeft, -1, 52);
            Pill(stats.transform, tr => UIKit.Icon(tr, "crate", 34), S.HaulCapacity(h) + " per trip", 26, null, BTile);
            Pill(stats.transform, null, d.Speed.ToString("0.0") + " m/s", 26, null, BTile);
            Flex(card.transform);
            int uid = h.uid;
            if (h.level < Haulage.MaxLevel)
            {
                var need = HBox(card.transform, 8, 0, TextAnchor.MiddleLeft, -1, 84);
                foreach (var r in S.HaulerUpgradeResources(h))
                {
                    string id = r.id;
                    Well(need.transform, 80, tr => UIKit.ResIcon(tr, id, 50), S.Amount(id) + "/" + r.amount, null, S.Amount(id) >= r.amount ? Color.white : BBad);
                }
                int coins = S.HaulerUpgradeCoins(h);
                Pill(need.transform, tr => UIKit.CoinIcon(tr, 38), Fmt.Num(coins), 28, S.Coins >= coins ? Color.white : BBad, BTile);
                Big(card.transform, "Upgrade: carry " + Haulage.CapacityAt(d, h.level + 1), UIKit.Green, () => Result(S.UpgradeHauler(uid), Sfx.Build), -1, 76, true, 30);
            }
            else T(card.transform, "Fully upgraded", 30, BGood, TextAnchor.MiddleLeft, true);
            int sell = Haulage.SellPrice(d, h.level);
            PriceBtn(card.transform, "Sell", UIKit.Red, tr => UIKit.CoinIcon(tr, 36), Fmt.Num(sell), () =>
            {
                string why = S.CantSellHauler(S.Hauler(uid));
                if (why != null) { Toast(why, false); return; }
                Result(S.SellHauler(uid), Sfx.Coin);
            }, -1, 70, true, 30);
        }
    }
}
