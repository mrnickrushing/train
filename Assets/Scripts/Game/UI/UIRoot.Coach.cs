using System.Collections.Generic;
using System.Linq;
using Ironhorse.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ironhorse.Game
{
    /// <summary>
    /// First-launch guide. Hank the foreman (trains, orders) and Clara the engineer (building, the sawmill) walk a
    /// new player through the first five goals one tap at a time: a gold ring and a bouncing arrow on the exact button
    /// to press, on the HUD, inside a board or on the map, with their lines typed out. It never blocks input, follows
    /// the player into every screen, steps aside for cinematics and level-ups, and ends for good on the Daily board
    /// or when skipped.
    /// </summary>
    public sealed partial class UIRoot
    {
        readonly Dictionary<string, RectTransform> _nav = new Dictionary<string, RectTransform>();
        readonly HashSet<string> _coachDismissed = new HashSet<string>();
        GameObject _coach;
        string _coachKey, _coachTargetId;
        RectTransform _coachTarget, _coachRing, _coachArrow, _coachBubble, _coachPortrait;
        Vector3? _coachWorld;
        RectTransform[] _coachDim;

        static readonly string[] CoachGoals = { "g1", "g2", "g3", "g4", "g5" };

        /// <summary>One step of the guide: who speaks, what they say and where they point.</summary>
        sealed class CoachStep
        {
            public string Key, Text, Target;
            public RectTransform Rect;
            public Vector3? World;
            public bool Clara, Info;
        }

        /// <summary>Marks a widget the guide can point at.</summary>
        static T Tag<T>(T c, string id) where T : Component { Tag(c.gameObject, id); return c; }
        static void Tag(GameObject go, string id)
        {
            var t = go.GetComponent<CoachTag>();
            if (t == null) t = go.AddComponent<CoachTag>();
            t.Id = id;
        }

        RectTransform FindTag(string id)
        {
            if (id == null || _canvas == null) return null;
            foreach (var t in _canvas.GetComponentsInChildren<CoachTag>(false)) if (t.Id == id) return (RectTransform)t.transform;
            return null;
        }

        CoachStep Step(string key, string text, string target = null, bool clara = false) { return new CoachStep { Key = key, Text = text, Target = target, Clara = clara }; }
        CoachStep Point(string key, string text, RectTransform rect, bool clara = false) { return new CoachStep { Key = key, Text = text, Rect = rect, Clara = clara }; }
        CoachStep World(string key, string text, Vector3 at, bool clara = false) { return new CoachStep { Key = key, Text = text, World = at, Clara = clara }; }
        CoachStep Info(string key, string text, bool clara = false) { return new CoachStep { Key = key, Text = text, Info = true, Clara = clara }; }

        /// <summary>The guide's current step, worked out from the goal, the open screen and what's on the map.</summary>
        CoachStep CoachNow()
        {
            var st = S.State;
            var g = S.CurrentGoal;
            int step = g != null ? System.Array.IndexOf(CoachGoals, g.Id) : -1;
            if (step < 0)
            {
                // Finale: point at the Daily board, then the guide is done.
                if (WindowOpen && _windowTitle == "Daily") { st.settings.tutorialDone = true; return null; }
                if (WindowOpen) return null;
                return Point("end", "That's the railroad, boss! Trains haul, mills make goods, and folks pay for 'em. Come back every day: your <b>Daily</b> board has pay and jobs waiting. Tap it now!", _payday != null ? (RectTransform)_payday.transform : null);
            }
            string title = WindowOpen ? _windowTitle : null;
            var dusty = st.trains.OrderBy(t => t.platform).FirstOrDefault();
            if (S.GoalProgressValue(g) >= g.Count)
                return title != null ? Step(g.Id + "+close", "Nice work! Close this and claim your reward on the goal card.", "board.close")
                    : Point(g.Id + "+", "Nice work, boss! Claim your reward on the goal card.", (RectTransform)_goalClaim.transform);

            switch (g.Id)
            {
                case "g1":
                case "g2":
                    return TrainSteps(g.Id, dusty, title);
                case "g3":
                {
                    if (TownView.Instance != null && TownView.Instance.PlacingDefId == "sawmill")
                    {
                        var plots = S.FreePlots();
                        if (plots.Count > 0) return World("g3.plot", "See the glowing plots? Tap one and the crew starts building.", WorldLayout.Plots[plots[0]] + Vector3.up * 2, true);
                    }
                    if (title == "Build")
                        return _buildTab != BuildingCategory.Production
                            ? Step("g3.tab", "Mills and smelters are under <b>Industry</b>. Tap that tab.", "tab.Industry", true)
                            : Step("g3.card", "There's our <b>Sawmill</b>. Tap <b>Build</b>: it turns timber into planks.", "build.sawmill", true);
                    if (title != null) return Step("g3.close", "Let's close this for now.", "board.close", true);
                    return Point("g3", "Clara Whitlock, territory engineer. That timber's no good as logs. Tap <b>Build</b> and let's raise a Sawmill.", Nav("Build"), true);
                }
                case "g4":
                    return MillSteps(title);
                default:
                {
                    if (title == "Contracts")
                        return S.State.contracts.Any(S.CanDeliver)
                            ? Step("g5.deliver", "Mayor Quill needs goods you've got in the warehouse. Tap <b>Deliver</b> and collect your pay!", "contract.deliver")
                            : Step("g5.wait", "No order you can fill yet. Close this and keep hauling.", "board.close");
                    if (title != null) return Step("g5.close", "Let's close this for now.", "board.close");
                    S.PostTutorialOrder(); // make sure there's an order the player can fill
                    return Point("g5", "Townsfolk post orders on the <b>Contracts</b> board. Fill one for coins, XP and gold nuggets. Tap <b>Contracts</b>.", Nav("Contracts"));
                }
            }
        }

        /// <summary>Send Dusty for timber and unload her when she's home (goals 1 and 2).</summary>
        CoachStep TrainSteps(string goal, TrainState dusty, string title)
        {
            if (dusty == null) return null;
            bool first = goal == "g1";
            if (S.HasArrived(dusty))
            {
                if (title == "Send a Train") return Step(goal + ".unload", "She's home with a full load. Tap <b>Unload</b>!", "dispatch.unload");
                if (title == "Trains on the Line") return Step(goal + ".collect", "Tap <b>Collect</b> to unload Dusty's timber into the warehouse.", "fleet.collect." + dusty.uid);
                if (title != null) return Step(goal + ".close2", "Dusty's back! Close this and we'll unload her.", "board.close");
                var view = TrainManager.Instance != null ? TrainManager.Instance.View(dusty.uid) : null;
                if (view != null && view.Current == TrainView.Phase.Arrived)
                    return World(goal + ".marker", "Dusty's home! Tap the timber floating over her to unload it.", view.LeadPosition + Vector3.up * 7);
                return Info(goal + ".pulling", "Here she comes, pulling into the station now...");
            }
            if (S.IsEnRoute(dusty))
            {
                if (title != null) return null;
                return Info(goal + ".wait", "Dusty's on her way to Pinecrest. Every trip shows a timer, and she'll whistle when she's back. Hang tight!");
            }
            // Idle at the platform: send her (again).
            string go = first ? "Howdy, boss! I'm Hank, your foreman. Old Dusty's fired up at the platform. Tap <b>Trains</b> to send her out."
                              : "One load won't build a town. Let's send Dusty back to Pinecrest. Tap <b>Trains</b>.";
            if (title == "Trains on the Line") return Step(goal + ".send", "This is your fleet. Tap <b>Send</b> on Dusty.", "fleet.send." + dusty.uid);
            if (title == "Send a Train")
            {
                if (_selectedTrain != dusty.uid) return Step(goal + ".close3", "Let's close this and pick Dusty.", "board.close");
                if (_selDest != "pinecrest") return Step(goal + ".dest", "<b>Pinecrest Logging Camp</b> has the timber we need. Tap its postcard.", "dest.pinecrest");
                return Step(goal + ".go", "Dusty's flatcar hauls logs. Tap <b>Send Train</b> and off she goes!", "dispatch.send");
            }
            if (title != null) return Step(goal + ".close", "Let's close this for now.", "board.close");
            return Point(goal, go, Nav("Trains"));
        }

        /// <summary>Open the sawmill, fill its belt and collect the planks (goal 4).</summary>
        CoachStep MillSteps(string title)
        {
            var mill = S.State.buildings.FirstOrDefault(b => b.defId == "sawmill");
            if (mill == null) return null;
            var view = TownView.Instance != null ? TownView.Instance.View(mill.uid) : null;
            Vector3 at = view != null ? view.transform.position + Vector3.up * (view.Height + 3) : WorldLayout.Plots[mill.plot];
            if (!S.IsReady(mill)) return title != null ? null : World("g4.build", "The crew's raising your sawmill. It'll be done in a few seconds!", at, true);
            if (title != null && WindowOpen && _openBuilding == mill.uid && _windowTitle == "Sawmill")
            {
                if (S.FinishedJobs(mill) > 0) return Step("g4.collect", "Fresh planks! Tap <b>Collect</b> to stack them in the warehouse.", "factory.collect", true);
                if (S.AffordableBatches(mill) > 0) return Step("g4.fill", "Each batch turns 6 timber into 3 planks. Tap to start the saws!", "factory.fill", true);
                if (S.IsProducing(mill)) return Step("g4.close", "The saws are running. Close this; the mill keeps working while you do other things.", "board.close", true);
                return Step("g4.timber", "We're out of timber. Close this and send Dusty to Pinecrest for more.", "board.close", true);
            }
            if (title != null) return Step("g4.close2", "Let's close this for now.", "board.close", true);
            if (S.FinishedJobs(mill) > 0) return World("g4.ready", "Planks are ready! Tap them over the sawmill to collect.", at, true);
            if (S.IsProducing(mill)) return Info("g4.wait", "The sawmill's busy. Batches take a minute, and you can tap the mill any time to check on it.", true);
            if (S.AffordableBatches(mill) == 0 && S.State.trains.Any(t => t.IsIdle && !S.HasArrived(t)))
                return Point("g4.haul", "The mill needs 6 timber a batch. Tap <b>Trains</b> and send Dusty to Pinecrest.", Nav("Trains"), true);
            return World("g4", "Your sawmill's up! Tap it to open it.", at, true);
        }

        void UpdateCoach()
        {
            var st = S.State;
            CoachStep step = null;
            bool blocked = (Celebrations.Instance != null && Celebrations.Instance.Busy) || _popupLayer.Find("LevelUp") != null;
            if (!st.settings.tutorialDone && !blocked) step = CoachNow();
            else if (!blocked) step = TipNow();
            if (step != null && _coachDismissed.Contains(step.Key)) step = null;
            string key = step != null ? step.Key : null;
            if (key != _coachKey)
            {
                if (_coach != null) Destroy(_coach);
                _coach = null;
                _coachKey = key;
                if (step != null)
                {
                    BuildCoach(step);
                    AudioManager.Play(Sfx.Click, 0.4f);
                    // Glide the camera to a spot on the map so it's centred, clear of the HUD.
                    if (step.World.HasValue && CameraRig.Instance != null) CameraRig.Instance.FlyTo(step.World.Value, Mathf.Min(CameraRig.Instance.Distance, 95));
                }
            }
            if (step != null)
            {
                _coachTargetId = step.Target;
                _coachTarget = step.Rect != null ? step.Rect : FindTag(step.Target);
                _coachWorld = step.World;
            }
            if (_coach != null) PlaceCoachHole();
        }

        RectTransform Nav(string label) { RectTransform r; return _nav.TryGetValue(label, out r) ? r : null; }

        void BuildCoach(CoachStep step)
        {
            _coach = UIKit.Node("Coach", _popupLayer);
            _coach.transform.SetAsFirstSibling();
            UIKit.Stretch(_coach);
            bool point = !step.Info;
            _coachDim = new RectTransform[4];
            float dim = !point ? 0 : WindowOpen ? 0.9f : 0.88f; // linear colour space: ~0.9 reads about two-thirds dark
            for (int i = 0; i < 4; i++)
            {
                var d = UIKit.Panel(_coach.transform, new Color(0.03f, 0.04f, 0.07f, dim), "Dim");
                d.sprite = null;
                d.raycastTarget = false;
                _coachDim[i] = d.rectTransform;
                _coachDim[i].anchorMin = _coachDim[i].anchorMax = new Vector2(0.5f, 0.5f);
                _coachDim[i].pivot = new Vector2(0, 0);
            }
            _coachRing = _coachArrow = null;
            if (point)
            {
                var ring = UIKit.Panel(_coach.transform, BGold, "Ring", UIKit.Rounded);
                ring.fillCenter = false;
                ring.pixelsPerUnitMultiplier = 3.5f;
                ring.raycastTarget = false;
                ring.gameObject.AddComponent<Outline>().effectColor = new Color(1, 0.85f, 0.4f, 0.45f);
                _coachRing = ring.rectTransform;
                _coachRing.anchorMin = _coachRing.anchorMax = new Vector2(0.5f, 0.5f);
                _coachArrow = CoachArrow(_coach.transform);
            }

            var safe = UIKit.Node("Safe", _coach.transform);
            UIKit.Stretch(safe);
            safe.AddComponent<SafeArea>();
            _coachPortrait = null;
            var tex = Resources.Load<Texture2D>(step.Clara ? "UI/Story/char_engineer" : "UI/Story/char_foreman");
            if (tex != null)
            {
                var who = UIKit.Node("Speaker", safe.transform).AddComponent<Image>();
                who.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0));
                who.preserveAspect = true;
                who.raycastTarget = false;
                _coachPortrait = UIKit.Place(who.gameObject, new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, -170), new Vector2(620 * tex.width / (float)tex.height, 620));
                who.gameObject.AddComponent<SlideIn>();
            }

            var bubble = UIKit.Panel(safe.transform, UIKit.Parchment, "Bubble", UIKit.RoundedGrad, true);
            bubble.pixelsPerUnitMultiplier = 1.4f;
            _coachBubble = UIKit.Place(bubble.gameObject, new Vector2(0, 0), new Vector2(0, 0), new Vector2(330, 420), new Vector2(900, 270));
            bubble.gameObject.AddComponent<Outline>().effectColor = BOutline;
            bubble.gameObject.AddComponent<WindowIntro>();
            var tag = UIKit.KitImage(bubble.transform, step.Clara ? "btn_blue" : "btn_gold", "Name", 64);
            UIKit.Place(tag.gameObject, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(28, 0), new Vector2(420, 64));
            var tn = T(tag.transform, step.Clara ? "CLARA · TERRITORY ENGINEER" : "HANK · YOUR FOREMAN", 30, step.Clara ? Color.white : Palette.Hex(0x3B2A05), TextAnchor.MiddleCenter, step.Clara);
            UIKit.Anchor(tn.gameObject, Vector2.zero, Vector2.one, new Vector2(8, 10), new Vector2(-8, -2));
            var body = T(bubble.transform, "", step.Text.Length > 125 ? 34 : 40, UIKit.Ink, TextAnchor.UpperLeft, false, UIKit.BodyBold);
            UIKit.Anchor(body.gameObject, Vector2.zero, Vector2.one, new Vector2(34, 74), new Vector2(-30, -42));
            var typer = body.gameObject.AddComponent<Typewriter>();
            typer.Full = step.Text;
            Clickable(bubble.gameObject, () => typer.Finish()); // tap the bubble to show the whole line
            bubble.GetComponent<ButtonFx>().enabled = false;
            var foot = UIKit.Node("Foot", bubble.transform);
            UIKit.Anchor(foot, Vector2.zero, new Vector2(1, 0), new Vector2(34, 12), new Vector2(-20, 70));
            UIKit.HStack(foot, 10, 0, TextAnchor.MiddleLeft);
            if (step.Key.StartsWith("tip."))
            {
                // A feature tip: no tutorial progress dots, just Got it.
                string tip = step.Key;
                UIKit.Spacer(foot.transform);
                UIKit.Btn(foot.transform, "Got it", UIKit.Green, () => { MarkTip(tip); UpdateCoach(); }, 26, 190, 58);
                return;
            }
            int at = System.Array.IndexOf(CoachGoals, S.CurrentGoal != null ? S.CurrentGoal.Id : "");
            if (at < 0) at = CoachGoals.Length;
            for (int i = 0; i <= CoachGoals.Length; i++)
            {
                var dot = UIKit.Panel(foot.transform, i == at ? BGold : i < at ? UIKit.Green : UIKit.WoodLight, "Dot", UIKit.RoundedSmall);
                UIKit.Size(dot.gameObject, i == at ? 40 : 16, 16);
            }
            UIKit.Spacer(foot.transform);
            string key = step.Key;
            if (step.Info) UIKit.Btn(foot.transform, "Got it", UIKit.Green, () => { _coachDismissed.Add(key); UpdateCoach(); }, 26, 190, 58);
            if (key == "end") UIKit.Btn(foot.transform, "Got it", UIKit.Green, () => { S.State.settings.tutorialDone = true; UpdateCoach(); }, 26, 190, 58);
            else
            {
                var skip = UIKit.Btn(foot.transform, "Skip tutorial", UIKit.WoodLight, () => { S.State.settings.tutorialDone = true; UpdateCoach(); }, 24, 250, 58);
                skip.name = "Skip";
            }
        }

        static RectTransform CoachArrow(Transform parent)
        {
            var root = UIKit.Node("Arrow", parent);
            var rt = UIKit.RT(root);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(96, 111);
            var img = UIKit.Node("Shape", root.transform).AddComponent<Image>();
            img.sprite = ArrowSprite();
            img.raycastTarget = false;
            UIKit.Stretch(img.gameObject);
            root.AddComponent<UIBob>();
            return rt;
        }

        static Sprite _arrowSprite;

        /// <summary>Downward pointer: gold shaft and head with a dark outline (the tutorial mockup's arrow).</summary>
        static Sprite ArrowSprite()
        {
            if (_arrowSprite != null) return _arrowSprite;
            const int W = 128, H = 148;
            // Outline in a 64x74 box, y down: shaft 20..44 wide to y 38, head to the tip at (32, 70).
            var poly = new[] { new Vector2(20, 4), new Vector2(44, 4), new Vector2(44, 38), new Vector2(60, 38), new Vector2(32, 70), new Vector2(4, 38), new Vector2(20, 38) };
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[W * H];
            Color gold = BGold, hi = new Color(1, 0.93f, 0.62f), ink = BOutline;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    var q = new Vector2((x + 0.5f) * 64f / W, (H - 1 - y + 0.5f) * 74f / H);
                    bool inside = false;
                    float d = float.MaxValue;
                    for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
                    {
                        Vector2 a = poly[i], b = poly[j];
                        if ((a.y > q.y) != (b.y > q.y) && q.x < (b.x - a.x) * (q.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
                        var ab = b - a;
                        float t = Mathf.Clamp01(Vector2.Dot(q - a, ab) / ab.sqrMagnitude);
                        d = Mathf.Min(d, Vector2.Distance(q, a + ab * t));
                    }
                    float sd = inside ? -d : d;                    // signed distance, 64-unit space
                    float fill = Mathf.Clamp01(0.5f - (sd + 2.4f) * 4); // gold inside the stroke
                    float all = Mathf.Clamp01(0.5f - (sd - 2.4f) * 4);  // stroke straddles the edge
                    var c = Color.Lerp(gold, hi, Mathf.Clamp01(1 - q.y / 40f) * (q.x < 32 ? 0.7f : 0.2f));
                    c = Color.Lerp(ink, c, all > 0 ? fill / all : 0);
                    c.a = all;
                    px[y * W + x] = c;
                }
            tex.SetPixels32(px);
            tex.Apply();
            return _arrowSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f));
        }

        /// <summary>Keeps the dim's hole, the ring and the arrow on the target (a HUD button, a board widget or a spot on the map).</summary>
        void PlaceCoachHole()
        {
            var root = (RectTransform)_coach.transform;
            Vector2 size = root.rect.size;
            Rect hole = new Rect(0, 0, 0, 0);
            if (_coachTarget == null && _coachTargetId != null) _coachTarget = FindTag(_coachTargetId); // boards rebuild their widgets
            bool has = false;
            if (_coachTarget != null && _coachTarget.gameObject.activeInHierarchy)
            {
                var c = new Vector3[4];
                _coachTarget.GetWorldCorners(c);
                Vector2 a = root.InverseTransformPoint(c[0]), b = root.InverseTransformPoint(c[2]);
                hole = Rect.MinMaxRect(a.x - 14, a.y - 14, b.x + 14, b.y + 14);
                has = true;
            }
            else if (_coachWorld.HasValue && CameraRig.Instance != null)
            {
                var sp = CameraRig.Instance.Cam.WorldToScreenPoint(_coachWorld.Value);
                Vector2 local;
                if (sp.z > 0 && RectTransformUtility.ScreenPointToLocalPointInRectangle(root, sp, null, out local))
                {
                    hole = new Rect(local.x - 80, local.y - 80, 160, 160);
                    has = true;
                }
            }
            float L = -size.x / 2, R = size.x / 2, B = -size.y / 2, Tp = size.y / 2;
            if (!has) hole = new Rect(L, B, 0, 0);
            SetRect(_coachDim[0], L, B, R, hole.yMin);              // below
            SetRect(_coachDim[1], L, hole.yMax, R, Tp);             // above
            SetRect(_coachDim[2], L, hole.yMin, hole.xMin, hole.yMax);
            SetRect(_coachDim[3], hole.xMax, hole.yMin, R, hole.yMax);
            if (_coachRing != null)
            {
                _coachRing.gameObject.SetActive(has);
                _coachArrow.gameObject.SetActive(has);
                _coachRing.anchoredPosition = hole.center;
                _coachRing.sizeDelta = hole.size;
                float s = 1 + Mathf.Sin(Time.unscaledTime * 5) * 0.03f;
                _coachRing.localScale = new Vector3(s, s, 1);
                bool below = hole.center.y > 0; // a target in the top half gets the arrow underneath, pointing up
                _coachArrow.localRotation = Quaternion.Euler(0, 0, below ? 180 : 0);
                _coachArrow.anchoredPosition = new Vector2(hole.center.x, below ? hole.yMin - 70 : hole.yMax + 70);
            }
            // The speech bubble takes the first spot clear of the hole and the arrow: low, middle or top.
            var keepOut = has ? new Rect(hole.xMin - 40, hole.yMin - 190, hole.width + 80, hole.height + 380) : new Rect(L, B, 0, 0);
            float[] ys = { 150, 420, size.y - 430 };
            float y = ys[0], least = float.MaxValue;
            foreach (float cand in ys)
            {
                var r = new Rect(L + 330, B + cand, 900, 270);
                float ox = Mathf.Max(0, Mathf.Min(r.xMax, keepOut.xMax) - Mathf.Max(r.xMin, keepOut.xMin));
                float oy = Mathf.Max(0, Mathf.Min(r.yMax, keepOut.yMax) - Mathf.Max(r.yMin, keepOut.yMin));
                if (ox * oy < least - 1) { least = ox * oy; y = cand; } // the spot covering the least of the target
            }
            _coachBubble.anchoredPosition = new Vector2(330, y);
            if (_coachPortrait != null) _coachPortrait.gameObject.SetActive(!(has && new Rect(L, B, 400, 460).Overlaps(hole)));
        }

        static void SetRect(RectTransform r, float x0, float y0, float x1, float y1)
        {
            r.anchoredPosition = new Vector2(x0, y0);
            r.sizeDelta = new Vector2(Mathf.Max(0, x1 - x0), Mathf.Max(0, y1 - y0));
        }
    }

    /// <summary>Gentle bounce for pointers: moves the children, so the root can be placed and rotated freely.</summary>
    public sealed class UIBob : MonoBehaviour
    {
        readonly Dictionary<RectTransform, Vector2> _base = new Dictionary<RectTransform, Vector2>();

        void Update()
        {
            float y = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3.2f)) * 16;
            foreach (Transform c in transform)
            {
                var r = (RectTransform)c;
                Vector2 b;
                if (!_base.TryGetValue(r, out b)) { b = r.anchoredPosition; _base[r] = b; }
                r.anchoredPosition = b + new Vector2(0, y);
            }
        }
    }

    /// <summary>Lets the guide find a widget to point at.</summary>
    public sealed class CoachTag : MonoBehaviour { public string Id; }

    /// <summary>Types a line out letter by letter (rich-text tags appear whole); tap to show it all.</summary>
    public sealed class Typewriter : MonoBehaviour
    {
        public string Full = "";
        float _shown;
        Text _t;
        void Awake() { _t = GetComponent<Text>(); }
        public void Finish() { _shown = Full.Length; Show(); }
        void Update()
        {
            if (_shown >= Full.Length) return;
            _shown += Time.unscaledDeltaTime * 55;
            Show();
        }
        void Show()
        {
            int n = Mathf.Min(Full.Length, (int)_shown), i = 0, visible = 0;
            var sb = new System.Text.StringBuilder();
            bool bold = false;
            while (i < Full.Length && visible < n)
            {
                if (Full[i] == '<')
                {
                    int close = Full.IndexOf('>', i);
                    if (close < 0) break;
                    string tag = Full.Substring(i, close - i + 1);
                    bold = tag == "<b>" || (bold && tag != "</b>");
                    sb.Append(tag);
                    i = close + 1;
                    continue;
                }
                sb.Append(Full[i++]);
                visible++;
            }
            if (bold) sb.Append("</b>");
            _t.text = sb.ToString();
        }
    }

    /// <summary>Slides a speaker portrait in from the left.</summary>
    public sealed class SlideIn : MonoBehaviour
    {
        float _t;
        Vector2 _home;
        RectTransform _rt;
        void Start() { _rt = (RectTransform)transform; _home = _rt.anchoredPosition; _rt.anchoredPosition = _home - new Vector2(260, 0); }
        void Update()
        {
            if (_rt == null || _t >= 1) return;
            _t = Mathf.Min(1, _t + Time.unscaledDeltaTime * 4);
            float k = 1 - Mathf.Pow(1 - _t, 3);
            _rt.anchoredPosition = _home - new Vector2(260 * (1 - k), 0);
        }
    }
}
