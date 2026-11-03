using System.Collections.Generic;
using Ironhorse.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ironhorse.Game
{
    /// <summary>
    /// First-launch coach: Hank the foreman walks a new player through the first five goals, dimming the map
    /// around the one HUD button to press. It never blocks input, hides while a window or cinematic is up, and
    /// ends for good when the player skips it or claims the fifth goal.
    /// </summary>
    public sealed partial class UIRoot
    {
        readonly Dictionary<string, RectTransform> _nav = new Dictionary<string, RectTransform>();
        readonly HashSet<string> _coachDismissed = new HashSet<string>();
        GameObject _coach;
        string _coachKey;
        RectTransform _coachTarget, _coachRing, _coachArrow, _coachBubble;
        RectTransform[] _coachDim;

        static readonly string[] CoachGoals = { "g1", "g2", "g3", "g4", "g5" };

        void UpdateCoach()
        {
            string key = null, text = null;
            RectTransform target = null;
            var st = S.State;
            var g = S.CurrentGoal;
            int step = g != null ? System.Array.IndexOf(CoachGoals, g.Id) : -1;
            if (!st.settings.tutorialDone && step < 0) st.settings.tutorialDone = true; // past the opening goals (or every goal claimed)
            bool blocked = WindowOpen || (_banner != null && _banner.activeSelf) || (Celebrations.Instance != null && Celebrations.Instance.Busy);
            if (!st.settings.tutorialDone && !blocked && step >= 0)
            {
                bool done = S.GoalProgressValue(g) >= g.Count;
                key = g.Id + (done ? "+" : "");
                if (done) { target = (RectTransform)_goalClaim.transform; text = "Nice work, boss! Claim your reward on the goal card."; }
                else switch (g.Id)
                {
                    case "g1": target = Nav("Trains"); text = "Howdy, boss! I'm Hank, your foreman. Old Dusty is fired up at the platform. Tap <b>Trains</b> and send her up to Pinecrest Logging Camp for timber."; break;
                    case "g2": target = Nav("Trains"); text = "Trains bring their cargo home. When one is back, tap <b>Trains</b> and collect the load. Keep hauling until you have 20 timber."; break;
                    case "g3": target = Nav("Build"); text = "Time to put that timber to work. Tap <b>Build</b> and raise a Sawmill on a free plot in town."; break;
                    case "g4": text = "Tap your new sawmill in town and queue a batch. It turns timber into planks while you do other things."; break;
                    default: target = Nav("Contracts"); text = "Townsfolk post orders on the <b>Contracts</b> board. Deliver one for coins, XP and gold nuggets."; break;
                }
                if (_coachDismissed.Contains(key)) key = null;
            }
            if (key != _coachKey)
            {
                if (_coach != null) Destroy(_coach);
                _coach = null;
                _coachKey = key;
                if (key != null) BuildCoach(text, target, step);
            }
            if (_coach != null) PlaceCoachHole();
        }

        RectTransform Nav(string label) { RectTransform r; return _nav.TryGetValue(label, out r) ? r : null; }

        void BuildCoach(string text, RectTransform target, int step)
        {
            _coach = UIKit.Node("Coach", _popupLayer);
            _coach.transform.SetAsFirstSibling();
            UIKit.Stretch(_coach);
            _coachTarget = target;
            _coachDim = new RectTransform[4];
            for (int i = 0; i < 4; i++)
            {
                var d = UIKit.Panel(_coach.transform, new Color(0.03f, 0.04f, 0.07f, target != null ? 0.9f : 0.6f), "Dim");
                d.sprite = null;
                d.raycastTarget = false;
                _coachDim[i] = d.rectTransform;
                _coachDim[i].anchorMin = _coachDim[i].anchorMax = new Vector2(0.5f, 0.5f);
                _coachDim[i].pivot = new Vector2(0, 0);
            }
            if (target != null)
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
            var tex = Resources.Load<Texture2D>("UI/Story/char_foreman");
            if (tex != null)
            {
                var hank = UIKit.Node("Hank", safe.transform).AddComponent<Image>();
                hank.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0));
                hank.preserveAspect = true;
                hank.raycastTarget = false;
                UIKit.Place(hank.gameObject, new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, -170), new Vector2(620 * tex.width / (float)tex.height, 620));
            }

            var bubble = UIKit.Panel(safe.transform, UIKit.Parchment, "Bubble", UIKit.RoundedGrad, true);
            bubble.pixelsPerUnitMultiplier = 1.4f;
            _coachBubble = UIKit.Place(bubble.gameObject, new Vector2(0, 0), new Vector2(0, 0), new Vector2(330, 420), new Vector2(900, 270));
            bubble.gameObject.AddComponent<Outline>().effectColor = BOutline;
            bubble.gameObject.AddComponent<WindowIntro>();
            var tag = UIKit.KitImage(bubble.transform, "btn_gold", "Name", 64);
            UIKit.Place(tag.gameObject, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(28, 0), new Vector2(390, 64));
            var tn = T(tag.transform, "HANK · YOUR FOREMAN", 32, Palette.Hex(0x3B2A05), TextAnchor.MiddleCenter);
            UIKit.Anchor(tn.gameObject, Vector2.zero, Vector2.one, new Vector2(8, 10), new Vector2(-8, -2));
            var body = T(bubble.transform, text, 40, UIKit.Ink, TextAnchor.UpperLeft, false, UIKit.BodyBold);
            UIKit.Anchor(body.gameObject, Vector2.zero, Vector2.one, new Vector2(34, 74), new Vector2(-30, -42));
            var foot = UIKit.Node("Foot", bubble.transform);
            UIKit.Anchor(foot, Vector2.zero, new Vector2(1, 0), new Vector2(34, 12), new Vector2(-20, 70));
            UIKit.HStack(foot, 10, 0, TextAnchor.MiddleLeft);
            for (int i = 0; i < CoachGoals.Length; i++)
            {
                var dot = UIKit.Panel(foot.transform, i == step ? BGold : UIKit.WoodLight, "Dot", UIKit.RoundedSmall);
                UIKit.Size(dot.gameObject, i == step ? 40 : 16, 16);
            }
            UIKit.Spacer(foot.transform);
            string key = _coachKey;
            if (target == null) UIKit.Btn(foot.transform, "Got it", UIKit.Green, () => { _coachDismissed.Add(key); UpdateCoach(); }, 26, 190, 58);
            var skip = UIKit.Btn(foot.transform, "Skip tutorial", UIKit.WoodLight, () => { S.State.settings.tutorialDone = true; UpdateCoach(); }, 24, 250, 58);
            skip.name = "Skip";
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

        /// <summary>Keeps the dim's hole, the ring and the arrow on the target (the HUD can move with the safe area).</summary>
        void PlaceCoachHole()
        {
            var root = (RectTransform)_coach.transform;
            Vector2 size = root.rect.size;
            Rect hole = new Rect(0, 0, 0, 0);
            bool has = _coachTarget != null && _coachTarget.gameObject.activeInHierarchy;
            if (has)
            {
                var c = new Vector3[4];
                _coachTarget.GetWorldCorners(c);
                Vector2 a = root.InverseTransformPoint(c[0]), b = root.InverseTransformPoint(c[2]);
                hole = Rect.MinMaxRect(a.x - 14, a.y - 14, b.x + 14, b.y + 14);
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
                bool below = hole.center.y > 0; // a target near the top gets the arrow underneath, pointing up
                _coachArrow.localRotation = Quaternion.Euler(0, 0, below ? 180 : 0);
                _coachArrow.anchoredPosition = new Vector2(hole.center.x, below ? hole.yMin - 70 : hole.yMax + 70);
            }
            // Clear of the pointer: low for a target in the top HUD, raised above the bottom bar otherwise.
            _coachBubble.anchoredPosition = new Vector2(330, has && hole.center.y > 0 ? 150 : 420);
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
}
