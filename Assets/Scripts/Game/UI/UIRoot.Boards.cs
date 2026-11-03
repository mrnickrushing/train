using System;
using System.Collections.Generic;
using Ironhorse.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ironhorse.Game
{
    /// <summary>
    /// Board windows: fixed-layout slate sheets (yellow title, header extras, a light rim around dark panels,
    /// side tabs, card rows) for the main game screens, plus the widgets they share. Sizes are canvas units of
    /// the 1920x1080-reference canvas, which is about 2230x1030 on a landscape iPhone.
    /// </summary>
    public sealed partial class UIRoot
    {
        static readonly Color BDeep = Palette.Hex(0x1D2535);
        static readonly Color BCard = Palette.Hex(0x283144);
        static readonly Color BCardDeep = Palette.Hex(0x1F2737);
        static readonly Color BTile = Palette.Hex(0x3A4459);
        static readonly Color BChip = Palette.Hex(0x141A26);
        static readonly Color BLabel = Palette.Hex(0xB9C3D4);
        static readonly Color BLine = Palette.Hex(0x4A566D);
        static readonly Color BGood = Palette.Hex(0x9BE35A);
        static readonly Color BBad = Palette.Hex(0xFF8A7A);
        static readonly Color BGold = Palette.Hex(0xF6C744);
        static readonly Color BBlue = Palette.Hex(0x4F7FE6);
        static readonly Color BGreen = Palette.Hex(0x5CAB26);
        static readonly Color BOutline = new Color(0.055f, 0.075f, 0.11f, 0.95f);

        Action<Transform, Transform> _boardBuilder;
        string _boardSub;
        Transform _boardBody, _boardRight;
        Text _boardSubLabel, _boardCoins, _boardNuggets;
        GameObject _boardSep;
        readonly Dictionary<string, Vector2> _scrollMemo = new Dictionary<string, Vector2>();
        readonly List<KeyValuePair<string, ScrollRect>> _boardScrolls = new List<KeyValuePair<string, ScrollRect>>();

        /// <summary>
        /// Opens a board. The builder gets the body (inside the rim) and the header's right-hand slot, and runs
        /// again whenever game state changes; scroll positions survive the rebuild.
        /// </summary>
        public void ShowBoard(string title, Action<Transform, Transform> build, float width = 1940, float height = 860)
        {
            CloseWindow();
            AudioManager.Play(Sfx.Open);
            SetHudVisible(false);
            _windowTitle = title;
            _boardBuilder = build;
            _scrollMemo.Clear();

            var dim = UIKit.Panel(_windowLayer, new Color(0.04f, 0.05f, 0.08f, 0.9f), "Dim");
            dim.sprite = null;
            UIKit.Stretch(dim.gameObject);
            _window = dim.gameObject;
            Clickable(_window, CloseWindow);
            _window.GetComponent<ButtonFx>().enabled = false;
            var sheet = UIKit.Node("Sheet", _window.transform);
            UIKit.Stretch(sheet);
            sheet.AddComponent<SafeArea>();

            // Money above the frame (the HUD is hidden while a window is open).
            var money = UIKit.Node("Money", sheet.transform);
            UIKit.Place(money, new Vector2(0.5f, 1), new Vector2(1, 1), new Vector2(width / 2 - 20, -22), new Vector2(900, 72));
            UIKit.HStack(money, 16, 0, TextAnchor.MiddleRight);
            _boardCoins = Chip(money.transform, t => UIKit.CoinIcon(t, 50), 250, () => { });
            _boardNuggets = Chip(money.transform, t => UIKit.NuggetIcon(t, 50), 240, () => OpenShop("nuggets"), true);

            var frame = UIKit.KitImage(sheet.transform, "frame_dark", "Frame");
            UIKit.Place(frame.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -46), new Vector2(width, height));
            frame.gameObject.AddComponent<Button>().transition = Selectable.Transition.None; // swallow clicks
            frame.gameObject.AddComponent<TapSwallow>();
            frame.gameObject.AddComponent<WindowIntro>();
            var fdrop = frame.gameObject.AddComponent<Shadow>();
            fdrop.effectColor = new Color(0, 0, 0, 0.5f);
            fdrop.effectDistance = new Vector2(0, -10);

            var header = UIKit.Node("Header", frame.transform);
            UIKit.Anchor(header, new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -112), new Vector2(-136, -10));
            UIKit.HStack(header, 24, 0, TextAnchor.MiddleLeft);
            var tl = T(header.transform, title, 64, UIKit.Title, TextAnchor.MiddleLeft, true);
            tl.horizontalOverflow = HorizontalWrapMode.Overflow;
            _boardSep = UIKit.Panel(header.transform, BLine, "Sep", UIKit.RoundedSmall).gameObject;
            UIKit.Size(_boardSep, 4, 58);
            _boardSubLabel = T(header.transform, "", 46, BLabel, TextAnchor.MiddleLeft, false, UIKit.Heading);
            _boardSubLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            UIKit.Spacer(header.transform);
            var right = UIKit.Node("Right", header.transform);
            UIKit.HStack(right, 16, 0, TextAnchor.MiddleRight);

            var close = UIKit.KitImage(frame.transform, "btn_red_sq", "Close");
            UIKit.Place(close.gameObject, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-24, -16), new Vector2(88, 88));
            var cx = T(close.transform, "X", 48, Color.white, TextAnchor.MiddleCenter);
            UIKit.Anchor(cx.gameObject, Vector2.zero, Vector2.one, new Vector2(0, 10), Vector2.zero);
            UIKit.Stroke(cx, new Color(0.35f, 0.05f, 0.03f, 0.95f), 2f);
            Clickable(close.gameObject, CloseWindow);

            var rim = UIKit.KitImage(frame.transform, "inset", "Rim").gameObject;
            UIKit.Anchor(rim, Vector2.zero, Vector2.one, new Vector2(22, 22), new Vector2(-22, -118));
            var body = UIKit.Node("Body", rim.transform);
            UIKit.Stretch(body, 10);
            _boardBody = body.transform;
            _boardRight = right.transform;
            FillBoard();
        }

        void FillBoard()
        {
            _live.Clear();
            _boardScrolls.Clear();
            ClearChildren(_boardBody);
            ClearChildren(_boardRight);
            _boardSub = null;
            try { _boardBuilder(_boardBody, _boardRight); }
            catch (Exception e) { Debug.LogException(e); T(_boardBody, "Oops: " + e.Message, 30, BBad); }
            if (_window == null || _boardSubLabel == null) return; // the builder closed the window
            _boardSubLabel.text = _boardSub ?? "";
            _boardSep.SetActive(!string.IsNullOrEmpty(_boardSub));
            _boardCoins.text = Fmt.Num(S.Coins);
            _boardNuggets.text = Fmt.Num(S.Nuggets);
            if (_boardScrolls.Count == 0) return;
            Canvas.ForceUpdateCanvases();
            foreach (var kv in _boardScrolls)
            {
                Vector2 v;
                if (_scrollMemo.TryGetValue(kv.Key, out v)) kv.Value.normalizedPosition = v;
            }
        }

        static void ClearChildren(Transform t)
        {
            for (int i = t.childCount - 1; i >= 0; i--)
            {
                var c = t.GetChild(i).gameObject;
                c.SetActive(false);
                Destroy(c);
            }
        }

        // ================================================================ widgets

        /// <summary>Display-face label at an exact canvas pixel size, optionally with the thick game outline.</summary>
        static Text T(Transform p, string s, float px, Color c, TextAnchor a = TextAnchor.MiddleLeft, bool outline = false, Font font = null)
        {
            var t = UIKit.Label(p, s, 20, c, a);
            t.font = font ?? UIKit.Display;
            t.fontStyle = FontStyle.Normal; // the faces are already bold
            t.fontSize = Mathf.RoundToInt(px);
            if (outline) UIKit.Stroke(t, BOutline, Mathf.Max(1.5f, px / 20f));
            return t;
        }

        /// <summary>One-line label that shrinks to fit its width.</summary>
        static Text T1(Transform p, string s, float px, Color c, TextAnchor a = TextAnchor.MiddleLeft, bool outline = false, Font font = null)
        {
            var t = T(p, s, px, c, a, outline, font);
            UIKit.OneLine(t, Mathf.RoundToInt(px * 0.55f));
            return t;
        }

        /// <summary>Flat rounded panel (card, tile, chip) with a corner radius that suits its size.</summary>
        static Image Box(Transform p, Color c, float w = -1, float h = -1, string name = "Box")
        {
            bool small = (h > 0 && h < 90) || (w > 0 && w < 90);
            var img = UIKit.Panel(p, c, name, small ? UIKit.RoundedSmall : UIKit.Rounded);
            if (!small) img.pixelsPerUnitMultiplier = 1.6f;
            img.raycastTarget = false;
            UIKit.Size(img.gameObject, w, h, w > 0 ? 0 : -1); // a fixed width never stretches with flexible children
            return img;
        }

        static GameObject VBox(Transform p, float spacing, int pad = 0, TextAnchor a = TextAnchor.UpperLeft, float w = -1, float h = -1, float flexW = -1, float flexH = -1)
        {
            var go = UIKit.Node("V", p);
            UIKit.VStack(go, spacing, pad, a);
            UIKit.Size(go, w, h, w > 0 && flexW < 0 ? 0 : flexW, flexH);
            return go;
        }

        static GameObject HBox(Transform p, float spacing, int pad = 0, TextAnchor a = TextAnchor.MiddleLeft, float w = -1, float h = -1, float flexW = -1, float flexH = -1)
        {
            var go = UIKit.Node("H", p);
            UIKit.HStack(go, spacing, pad, a);
            UIKit.Size(go, w, h, w > 0 && flexW < 0 ? 0 : flexW, flexH);
            return go;
        }

        static VerticalLayoutGroup Stack(GameObject go, float spacing, RectOffset pad, TextAnchor a = TextAnchor.UpperLeft)
        {
            var v = UIKit.VStack(go, spacing, 0, a);
            v.padding = pad;
            return v;
        }

        static HorizontalLayoutGroup Row(GameObject go, float spacing, RectOffset pad, TextAnchor a = TextAnchor.MiddleLeft)
        {
            var h = UIKit.HStack(go, spacing, 0, a);
            h.padding = pad;
            return h;
        }

        static readonly Dictionary<string, Sprite> _art = new Dictionary<string, Sprite>();

        /// <summary>A UI picture from Resources/UI/&lt;path&gt; (Tools/art/ui_cutouts.py), or null.</summary>
        static Sprite ArtSprite(string path)
        {
            Sprite s;
            if (_art.TryGetValue(path, out s)) return s;
            var tex = Resources.Load<Texture2D>("UI/" + path);
            if (tex != null) s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100);
            _art[path] = s;
            return s;
        }

        static Image Pic(Transform p, Sprite sp, float w, float h)
        {
            var img = UIKit.Node("Pic", p).AddComponent<Image>();
            img.sprite = sp;
            img.preserveAspect = true;
            img.raycastTarget = false;
            UIKit.Size(img.gameObject, w, h);
            ((RectTransform)img.transform).sizeDelta = new Vector2(w, h);
            return img;
        }

        /// <summary>Locomotive cut-out (falls back to the rendered 3D model).</summary>
        static Graphic LocoArt(Transform p, LocomotiveDef d, float w, float h)
        {
            var sp = ArtSprite("Locos/" + d.Id);
            if (sp != null) return Pic(p, sp, w, h);
            var raw = ModelIcons.Image(p, ModelIcons.Locomotive(d), w, h);
            UIKit.Size(raw.gameObject, w, h);
            return raw;
        }

        static Graphic WagonArt(Transform p, WagonDef d, float w, float h)
        {
            var sp = ArtSprite("Wagons/" + d.Id);
            if (sp != null) return Pic(p, sp, w, h);
            var raw = ModelIcons.Image(p, ModelIcons.Wagon(d), w, h);
            UIKit.Size(raw.gameObject, w, h);
            return raw;
        }

        static Graphic BuildingArt(Transform p, BuildingDef d, float w, float h)
        {
            var sp = ArtSprite("Buildings/" + d.Id);
            if (sp != null) return Pic(p, sp, w, h);
            var tex = ModelIcons.Building(d);
            if (tex != null) { var raw = ModelIcons.Image(p, tex, w, h); UIKit.Size(raw.gameObject, w, h); return raw; }
            return BuildingSwatch(p, d).GetComponent<Image>();
        }

        /// <summary>A photo (postcard, scene) filling a rounded, masked w×h panel.</summary>
        static Image Photo(Transform p, Texture tex, float w, float h, Color? tint = null)
        {
            var frame = UIKit.Panel(p, Color.white, "Photo", UIKit.Rounded);
            frame.pixelsPerUnitMultiplier = 1.6f;
            UIKit.Size(frame.gameObject, w, h);
            frame.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            if (tex != null)
            {
                var raw = UIKit.Node("Art", frame.transform).AddComponent<RawImage>();
                raw.texture = tex;
                raw.raycastTarget = false;
                raw.color = tint ?? Color.white;
                UIKit.Stretch(raw.gameObject);
                var fit = raw.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = tex.width / (float)tex.height;
            }
            return frame;
        }

        static Texture Postcard(string destId) { return Resources.Load<Texture2D>("UI/Destinations/" + destId); }

        static Sprite _fadeV, _fadeH, _sky, _rays;

        /// <summary>Transparent→opaque gradient (bottom opaque when vertical, left opaque when horizontal).</summary>
        static Sprite Fade(bool vertical)
        {
            if (vertical ? _fadeV != null : _fadeH != null) return vertical ? _fadeV : _fadeH;
            var tex = new Texture2D(vertical ? 4 : 64, vertical ? 64 : 4, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < tex.height; y++)
                for (int x = 0; x < tex.width; x++)
                {
                    float k = vertical ? 1 - y / 63f : 1 - x / 63f;
                    tex.SetPixel(x, y, new Color(1, 1, 1, Mathf.SmoothStep(0, 1, k)));
                }
            tex.Apply();
            var s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100);
            if (vertical) _fadeV = s; else _fadeH = s;
            return s;
        }

        static Image Overlay(Transform p, Sprite s, Color c)
        {
            var img = UIKit.Node("Fade", p).AddComponent<Image>();
            img.sprite = s;
            img.color = c;
            img.raycastTarget = false;
            UIKit.Stretch(img.gameObject);
            img.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            return img;
        }

        /// <summary>Dusk sky behind building art on build cards.</summary>
        static Sprite Sky()
        {
            if (_sky != null) return _sky;
            var tex = new Texture2D(4, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            Color top = Palette.Hex(0x4C5F80), mid = Palette.Hex(0x8A9CB6), low = Palette.Hex(0xD7B57A);
            for (int y = 0; y < 64; y++)
            {
                float k = y / 63f; // 0 bottom, 1 top
                var c = k < 0.45f ? Color.Lerp(low, mid, k / 0.45f) : Color.Lerp(mid, top, (k - 0.45f) / 0.55f);
                for (int x = 0; x < 4; x++) tex.SetPixel(x, y, c);
            }
            tex.Apply();
            return _sky = Sprite.Create(tex, new Rect(0, 0, 4, 64), new Vector2(0.5f, 0.5f), 100);
        }

        /// <summary>Soft golden sunburst behind reward icons.</summary>
        static Sprite Rays()
        {
            if (_rays != null) return _rays;
            const int n = 256;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2 - 1, dy = (y + 0.5f) / n * 2 - 1;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx) / (2 * Mathf.PI) * 40; // 40 rays
                    float ray = Mathf.Clamp01(1 - Mathf.Abs(ang - Mathf.Round(ang)) * 6f);
                    float fall = Mathf.Clamp01(1 - r);
                    float glow = Mathf.Clamp01(1 - r / 0.55f);
                    float a = ray * fall * fall * 0.8f + glow * glow * 0.75f;
                    tex.SetPixel(x, y, new Color(1f, 0.9f, 0.62f, Mathf.Clamp01(a)));
                }
            tex.Apply();
            return _rays = Sprite.Create(tex, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100);
        }

        /// <summary>Dark chip with an optional icon and outlined text ("1m 30s", "+20").</summary>
        static GameObject Pill(Transform p, Action<Transform> icon, string text, float px = 30, Color? textColor = null, Color? bg = null)
        {
            var b = Box(p, bg ?? BChip, -1, px * 1.5f, "Pill");
            Row(b.gameObject, 6, new RectOffset(icon != null ? 6 : 14, 14, 3, 3), TextAnchor.MiddleCenter);
            if (icon != null) icon(b.transform);
            var t = T(b.transform, text, px, textColor ?? Color.white, TextAnchor.MiddleCenter, true);
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return b.gameObject;
        }

        static void ClockIcon(Transform p, float size)
        {
            var h = UIKit.Node("Clock", p);
            UIKit.Size(h, size, size);
            var ring = UIKit.Panel(h.transform, BGold, "Ring", UIKit.Circle);
            ring.raycastTarget = false;
            UIKit.Place(ring.gameObject, new Vector2(0.5f, 0.45f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.86f, size * 0.86f));
            var face = UIKit.Panel(ring.transform, BChip, "Face", UIKit.Circle);
            face.raycastTarget = false;
            UIKit.Stretch(face.gameObject, size * 0.12f);
            var knob = UIKit.Panel(h.transform, BGold, "Knob", UIKit.RoundedSmall);
            knob.raycastTarget = false;
            UIKit.Place(knob.gameObject, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(size * 0.3f, size * 0.14f));
            var hand = UIKit.Panel(face.transform, BGold, "Hand", UIKit.RoundedSmall);
            hand.raycastTarget = false;
            UIKit.Place(hand.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0), Vector2.zero, new Vector2(size * 0.1f, size * 0.3f));
        }

        /// <summary>Green disc with a tick.</summary>
        static GameObject CheckBadge(Transform p, float size)
        {
            var c = UIKit.KitImage(p, "round_green", "Check");
            UIKit.Size(c.gameObject, size, size);
            c.raycastTarget = false;
            var a = UIKit.Panel(c.transform, Color.white, "A", UIKit.RoundedSmall);
            a.raycastTarget = false;
            UIKit.Place(a.gameObject, new Vector2(0.42f, 0.42f), new Vector2(0.5f, 0.5f), new Vector2(-size * 0.06f, 0), new Vector2(size * 0.14f, size * 0.36f)).localRotation = Quaternion.Euler(0, 0, 45);
            var b = UIKit.Panel(c.transform, Color.white, "B", UIKit.RoundedSmall);
            b.raycastTarget = false;
            UIKit.Place(b.gameObject, new Vector2(0.58f, 0.52f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.14f, size * 0.58f)).localRotation = Quaternion.Euler(0, 0, -40);
            return c.gameObject;
        }

        /// <summary>Segmented stat bar: <paramref name="on"/> gold segments, then <paramref name="bonus"/> blue ones.</summary>
        static GameObject Segs(Transform p, int total, int on, int bonus = 0, float h = 22)
        {
            var row = HBox(p, 5, 0, TextAnchor.MiddleLeft, -1, h, 1);
            row.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
            for (int i = 0; i < total; i++)
            {
                var c = i < on ? BGold : i < on + bonus ? Palette.Hex(0x6FB2F2) : Palette.Hex(0x0F141E);
                var s = Box(row.transform, c, -1, h, "Seg");
                UIKit.Size(s.gameObject, -1, h, 1);
            }
            return row;
        }

        /// <summary>Thin progress bar on a dark track; returns the fill.</summary>
        static Image Track(Transform p, float h, Color fill, float w = -1)
        {
            var bar = UIKit.Bar(p, fill, h, Palette.Hex(0x0F141E));
            if (w > 0) UIKit.Size(bar.transform.parent.gameObject, w, h, 0);
            return bar;
        }

        /// <summary>Vertical column of big side tabs (icon over caption), the active one light.</summary>
        void SideTabs(Transform p, float w, float h, (string label, Action<Transform> icon, bool on, Action pick)[] tabs)
        {
            var col = VBox(p, 12, 0, TextAnchor.UpperCenter, w);
            col.GetComponent<VerticalLayoutGroup>().padding = new RectOffset(0, 0, 4, 0);
            foreach (var tb in tabs)
            {
                var tab = UIKit.Panel(col.transform, tb.on ? UIKit.Parchment : UIKit.WoodLight, "Tab", UIKit.RoundedGrad);
                tab.pixelsPerUnitMultiplier = 1.4f;
                UIKit.Size(tab.gameObject, w, h);
                Stack(tab.gameObject, 2, new RectOffset(6, 6, 10, 10), TextAnchor.MiddleCenter);
                var ic = HBox(tab.transform, 0, 0, TextAnchor.MiddleCenter, -1, h * 0.55f);
                tb.icon(ic.transform);
                var t = T1(tab.transform, tb.label.ToUpperInvariant(), 30, tb.on ? UIKit.Ink : Color.white, TextAnchor.MiddleCenter, !tb.on);
                var pick = tb.pick;
                if (!tb.on) Clickable(tab.gameObject, () => { pick(); RebuildWindow(); });
                t.raycastTarget = false;
            }
        }

        static Action<Transform> Ico(string id, float size) { return t => { if (UIKit.Icon(t, id, size) == null) UIKit.ResIcon(t, id, size); }; }

        /// <summary>A scroll area that fills its slot; returns the content to add cards to.</summary>
        Transform Scroller(Transform p, string key, bool horizontal, float spacing, RectOffset pad = null)
        {
            var go = UIKit.Node("Scroll " + key, p);
            UIKit.Size(go, -1, -1, 1, 1);
            var hit = go.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            var vp = UIKit.Node("Viewport", go.transform);
            UIKit.Stretch(vp);
            vp.AddComponent<RectMask2D>();
            var content = UIKit.Node("Content", vp.transform);
            var rt = UIKit.RT(content);
            if (horizontal)
            {
                rt.anchorMin = new Vector2(0, 0); rt.anchorMax = new Vector2(0, 1); rt.pivot = new Vector2(0, 0.5f);
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                var h = Row(content, spacing, pad ?? new RectOffset(0, 0, 0, 0), TextAnchor.UpperLeft);
                h.childForceExpandHeight = true;
                content.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            else
            {
                rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(1, 1); rt.pivot = new Vector2(0.5f, 1);
                rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
                Stack(content, spacing, pad ?? new RectOffset(0, 0, 0, 0), TextAnchor.UpperCenter);
                content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            var sr = go.AddComponent<ScrollRect>();
            sr.viewport = UIKit.RT(vp);
            sr.content = rt;
            sr.horizontal = horizontal;
            sr.vertical = !horizontal;
            sr.movementType = ScrollRect.MovementType.Elastic;
            sr.scrollSensitivity = 30;
            _boardScrolls.Add(new KeyValuePair<string, ScrollRect>(key, sr));
            sr.onValueChanged.AddListener(v => _scrollMemo[key] = v);
            return content.transform;
        }

        /// <summary>Kit button whose caption sits beside a darker price chip (icon + amount).</summary>
        static Button PriceBtn(Transform p, string label, Color c, Action<Transform> icon, string amount, Action onClick, float w, float h, bool enabled = true, int px = 40)
        {
            var b = UIKit.Btn(p, "", c, onClick, 26, w, h, enabled);
            var face = b.transform.Find("Face") ?? b.transform;
            var row = UIKit.Node("Price", face);
            UIKit.Anchor(row, Vector2.zero, Vector2.one, new Vector2(8, 14), new Vector2(-8, -4));
            Row(row, 8, new RectOffset(0, 0, 0, 0), TextAnchor.MiddleCenter);
            var chip = Box(row.transform, new Color(0, 0, 0, 0.28f), -1, h * 0.62f, "Chip");
            Row(chip.gameObject, 2, new RectOffset(2, 12, 0, 0), TextAnchor.MiddleCenter);
            icon(chip.transform);
            T(chip.transform, amount, px * 0.85f, Color.white, TextAnchor.MiddleCenter, true).horizontalOverflow = HorizontalWrapMode.Overflow;
            if (!string.IsNullOrEmpty(label))
            {
                var t = T(row.transform, label.ToUpperInvariant(), px, Color.white, TextAnchor.MiddleCenter, true);
                t.horizontalOverflow = HorizontalWrapMode.Overflow;
                UIKit.Size(t.gameObject, -1, -1, 1);
            }
            return b;
        }

        /// <summary>A big kit button with an exact caption size.</summary>
        static Button Big(Transform p, string text, Color c, Action onClick, float w, float h, bool enabled = true, int px = 42)
        {
            var b = UIKit.Btn(p, text, c, onClick, 26, w, h, enabled);
            var t = b.GetComponentInChildren<Text>();
            t.fontSize = px;
            t.resizeTextMaxSize = px;
            return b;
        }

        /// <summary>Square icon well: a tile with a centred icon and an optional amount in the corner.</summary>
        static Image Well(Transform p, float size, Action<Transform> icon, string corner = null, Color? bg = null, Color? cornerColor = null)
        {
            var w = Box(p, bg ?? BTile, size, size, "Well");
            var ic = UIKit.Node("Icon", w.transform);
            UIKit.Stretch(ic, size * 0.1f);
            UIKit.HStack(ic, 0, 0, TextAnchor.MiddleCenter);
            icon(ic.transform);
            if (corner != null)
            {
                var t = T(w.transform, corner, size * 0.3f, cornerColor ?? Color.white, TextAnchor.LowerRight, true);
                UIKit.Anchor(t.gameObject, Vector2.zero, Vector2.one, new Vector2(4, 2), new Vector2(-8, 0));
            }
            return w;
        }
    }
}
