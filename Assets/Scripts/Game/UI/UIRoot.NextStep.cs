using Ironhorse.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ironhorse.Game
{
    /// <summary>
    /// The next-step button: one obvious action over the world, just above the bottom bar ("Unload Dusty",
    /// "Fill Mayor Quill's order", "Lay platform 4"), picked by <see cref="GameSession.NextAction"/>. Collecting
    /// happens right there; anything that spends opens the screen where it's done. Hidden during the guide,
    /// cinematics and building placement, and when there's nothing to do but wait.
    /// </summary>
    public sealed partial class UIRoot
    {
        GameObject _next;
        RectTransform _nextRect, _nextArrow;
        Text _nextText, _nextDetail;
        Transform _nextIcon;
        string _nextIconKey;
        Advice _advice;

        void BuildNextStep()
        {
            var bg = UIKit.KitImage(_safe, "tile_dark", "NextStep", 64);
            _next = bg.gameObject;
            _nextRect = UIKit.Place(_next, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 196), new Vector2(640, 104));
            HudShadow(_next);
            Row(_next, 10, new RectOffset(10, 12, 8, 8));

            var disc = UIKit.KitImage(_next.transform, "round_gold", "Icon");
            UIKit.Size(disc.gameObject, 78, 78);
            _nextIcon = UIKit.Node("Glyph", disc.transform).transform;
            UIKit.Stretch(_nextIcon.gameObject, 12);
            UIKit.HStack(_nextIcon.gameObject, 0, 0, TextAnchor.MiddleCenter);

            var col = VBox(_next.transform, 0, 0, TextAnchor.MiddleLeft, -1, -1, 1);
            col.GetComponent<VerticalLayoutGroup>().childForceExpandHeight = false;
            _nextText = UIKit.Label(col.transform, "", 32, Color.white, TextAnchor.MiddleLeft);
            _nextText.font = UIKit.Display;
            _nextText.fontStyle = FontStyle.Normal; // the face is already bold
            UIKit.Size(_nextText.gameObject, -1, 44);
            UIKit.OneLine(_nextText, 22);
            UIKit.Stroke(_nextText, new Color(0, 0, 0, 0.8f), 2f);
            _nextDetail = UIKit.Label(col.transform, "", 22, UIKit.Gold, TextAnchor.MiddleLeft);
            UIKit.Size(_nextDetail.gameObject, -1, 28);
            UIKit.OneLine(_nextDetail, 16);

            var go = UIKit.KitImage(_next.transform, "round_green", "Go");
            UIKit.Size(go.gameObject, 64, 64);
            var chev = UIKit.Label(go.transform, "›", 56, Color.white, TextAnchor.MiddleCenter);
            chev.font = UIKit.Display;
            UIKit.Stretch(chev.gameObject);
            UIKit.Anchor(chev.gameObject, Vector2.zero, Vector2.one, new Vector2(4, 6), Vector2.zero);
            UIKit.Stroke(chev, new Color(0.05f, 0.25f, 0.05f, 0.9f), 2f);
            _nextArrow = (RectTransform)chev.transform;

            Clickable(_next, ActOnAdvice);
            Tag(_next, "next.step");
            _next.SetActive(false);
        }

        void RefreshNextStep()
        {
            if (_next == null) return;
            bool blocked = !S.State.settings.tutorialDone || _coach != null || _banner.activeSelf
                           || (Celebrations.Instance != null && Celebrations.Instance.Busy) || _popupLayer.Find("LevelUp") != null
                           || (TownView.Instance != null && TownView.Instance.PlacingDefId != null);
            _advice = blocked ? null : S.NextAction();
            _next.SetActive(_advice != null);
            if (_advice == null) return;

            // Sit above the Full Steam gauge when it's showing.
            float y = _combo != null && _combo.activeSelf ? 292 : 196;
            if (Mathf.Abs(_nextRect.anchoredPosition.y - y) > 0.5f) _nextRect.anchoredPosition = new Vector2(0, y);
            // Fit between the side button columns (Foreman/Daily on the left, the shop stack on the right).
            float w = Mathf.Clamp(_safe.rect.width - 2 * 200, 400, 600);
            if (Mathf.Abs(_nextRect.sizeDelta.x - w) > 0.5f) _nextRect.sizeDelta = new Vector2(w, _nextRect.sizeDelta.y);
            _nextText.text = _advice.Text;
            _nextDetail.text = _advice.Detail;
            _nextDetail.gameObject.SetActive(!string.IsNullOrEmpty(_advice.Detail));
            string key = _advice.Icon ?? _advice.Kind.ToString();
            if (key != _nextIconKey)
            {
                _nextIconKey = key;
                for (int i = _nextIcon.childCount - 1; i >= 0; i--) Destroy(_nextIcon.GetChild(i).gameObject);
                AdviceIcon(_nextIcon, _advice, 56);
            }
        }

        static void AdviceIcon(Transform t, Advice a, float size)
        {
            if (a.Icon != null) { UIKit.ResIcon(t, a.Icon, size); return; }
            switch (a.Kind)
            {
                case AdviceKind.Payday:
                case AdviceKind.CollectTruck:
                case AdviceKind.Rent: UIKit.CoinIcon(t, size); break;
                case AdviceKind.WarehouseFull: UIKit.Icon(t, "crate", size); break;
                case AdviceKind.DailyTask: UIKit.Icon(t, "nav_contracts", size); break;
                case AdviceKind.LayPlatform: UIKit.Icon(t, "nav_build", size); break;
                default: UIKit.Icon(t, "nav_trains", size); break;
            }
        }

        /// <summary>A gentle pulse and a nudging chevron, every frame while it shows.</summary>
        void AnimateNextStep()
        {
            if (_next == null || !_next.activeSelf) return;
            float t = Time.unscaledTime;
            _next.transform.localScale = Vector3.one * (1 + Mathf.Sin(t * 3.2f) * 0.015f);
            _nextArrow.anchoredPosition = new Vector2(4 + Mathf.Abs(Mathf.Sin(t * 4f)) * 8, 0);
        }

        // ================================================================ departure banner

        GameObject _departure;

        /// <summary>A ribbon that pops in over the world when a train leaves, then fades. Never blocks taps.</summary>
        void DepartureBanner(string title, string sub)
        {
            if (_departure != null) Destroy(_departure);
            var root = UIKit.Node("Departure", _popupLayer);
            _departure = root;
            UIKit.Place(root, new Vector2(0.5f, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -330), new Vector2(860, 180));
            var group = root.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var rib = UIKit.KitImage(root.transform, "ribbon", "Ribbon", 120);
            UIKit.Place(rib.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(860, 120));
            rib.raycastTarget = false;
            var t = UIKit.Label(rib.transform, title.ToUpperInvariant(), 46, Color.white, TextAnchor.MiddleCenter);
            t.font = UIKit.Display;
            t.fontStyle = FontStyle.Normal;
            UIKit.Anchor(t.gameObject, Vector2.zero, Vector2.one, new Vector2(110, 18), new Vector2(-110, -8));
            UIKit.OneLine(t, 26);
            UIKit.Stroke(t, new Color(0.36f, 0.2f, 0.02f, 0.95f), 2.5f);
            var s = UIKit.Label(root.transform, sub, 28, UIKit.Cream, TextAnchor.MiddleCenter);
            UIKit.Place(s.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(820, 44));
            UIKit.OneLine(s, 18);
            UIKit.Stroke(s, new Color(0, 0, 0, 0.85f), 2f);
            foreach (var g in root.GetComponentsInChildren<Graphic>()) g.raycastTarget = false;
            root.AddComponent<PopBanner>();
        }

        /// <summary>Punches in with a little overshoot, holds, then fades out and removes itself.</summary>
        sealed class PopBanner : MonoBehaviour
        {
            const float In = 0.28f, Hold = 2.1f, Out = 0.45f;
            float _t;
            CanvasGroup _g;
            void Awake() { _g = GetComponent<CanvasGroup>(); transform.localScale = Vector3.one * 0.5f; }
            void Update()
            {
                _t += Time.unscaledDeltaTime;
                if (_t < In)
                {
                    float u = _t / In;
                    float back = 1 + 2.2f * Mathf.Pow(u - 1, 3) + 1.2f * Mathf.Pow(u - 1, 2); // ease-out-back
                    transform.localScale = Vector3.one * Mathf.LerpUnclamped(0.5f, 1, back);
                    _g.alpha = Mathf.Clamp01(u * 2);
                }
                else if (_t < In + Hold) { transform.localScale = Vector3.one; _g.alpha = 1; }
                else if (_t < In + Hold + Out)
                {
                    float u = (_t - In - Hold) / Out;
                    _g.alpha = 1 - u;
                    transform.localPosition += Vector3.up * Time.unscaledDeltaTime * 60;
                }
                else Destroy(gameObject);
            }
        }

        void ActOnAdvice()
        {
            var a = _advice;
            if (a == null) return;
            switch (a.Kind)
            {
                case AdviceKind.Unload:
                {
                    var r = S.Unload(a.Uid);
                    Result(r);
                    var view = TrainManager.Instance != null ? TrainManager.Instance.View(a.Uid) : null;
                    if (r.Ok && view != null) Fx.Burst(view.LeadPosition + Vector3.up * 5, 30);
                    break;
                }
                case AdviceKind.CollectGoods:
                {
                    var r = S.CollectProduction(a.Uid);
                    Result(r);
                    var view = TownView.Instance != null ? TownView.Instance.View(a.Uid) : null;
                    if (r.Ok && view != null) Fx.Burst(view.transform.position + Vector3.up * (view.Height + 3), 30);
                    break;
                }
                case AdviceKind.Rent:
                {
                    int got = S.CollectAllRent();
                    if (got > 0) { AudioManager.Play(Sfx.Coin); Toast("+" + got + " coins collected", true); }
                    break;
                }
                case AdviceKind.Deliver: _ordersTab = "orders"; OpenContracts(); break;
                case AdviceKind.WarehouseFull: OpenWarehouse(); break;
                case AdviceKind.Payday: OpenPayday(); break;
                case AdviceKind.DailyTask: OpenDaily("tasks"); break;
                case AdviceKind.LayPlatform: OpenFleet(); break;
                case AdviceKind.Dispatch: OpenTrain(a.Uid); break;
                case AdviceKind.CollectTruck: CollectTruck(a.Uid); break;
                case AdviceKind.SendTruck: OpenGarage("orders"); break;
            }
        }
    }
}
