using System;
using Ironhorse.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Ironhorse.Game
{
    /// <summary>Theme and widget factory for the code-built uGUI interface.</summary>
    public static class UIKit
    {
        // ---------------------------------------------------------------- theme
        public static readonly Color Wood = Palette.Hex(0x3B2618);
        public static readonly Color WoodMid = Palette.Hex(0x5A3A24);
        public static readonly Color WoodLight = Palette.Hex(0x7A5234);
        public static readonly Color Parchment = Palette.Hex(0xF3E6C8);
        public static readonly Color ParchmentDark = Palette.Hex(0xE2CFA6);
        public static readonly Color Ink = Palette.Hex(0x3A2616);
        public static readonly Color InkSoft = Palette.Hex(0x7A6048);
        public static readonly Color Brass = Palette.Hex(0xD9A93F);
        public static readonly Color BrassDark = Palette.Hex(0x9C7426);
        public static readonly Color Red = Palette.Hex(0xB5442E);
        public static readonly Color Green = Palette.Hex(0x4E8A3A);
        public static readonly Color Blue = Palette.Hex(0x3C6E8F);
        public static readonly Color Gold = Palette.Hex(0xF2C14E);
        public static readonly Color Cream = Palette.Hex(0xFFF6E0);
        public static readonly Color Disabled = Palette.Hex(0x9A8E80);
        public static readonly Color Dim = new Color(0.08f, 0.05f, 0.03f, 0.62f);

        public static Color RarityColor(Rarity r)
        {
            switch (r)
            {
                case Rarity.Uncommon: return Palette.Hex(0x4E8A3A);
                case Rarity.Rare: return Palette.Hex(0x3C6E8F);
                case Rarity.Epic: return Palette.Hex(0x7E4BA6);
                case Rarity.Legendary: return Palette.Hex(0xD08A1E);
            }
            return Palette.Hex(0x7A6A5A);
        }

        // ---------------------------------------------------------------- fonts & sprites

        // Bundled OFL faces (Resources/Fonts, Barlow by Jeremy Tribby) so the UI looks the same on
        // every device; OS fonts are only a fallback if the files are missing.
        static Font _body, _bodyBold, _heading, _display;
        public static Font Body { get { return _body ?? (_body = Load("BarlowSemiCondensed-Medium", "Avenir Next", "Segoe UI", "Helvetica Neue", "Arial")); } }
        public static Font BodyBold { get { return _bodyBold ?? (_bodyBold = Load("BarlowSemiCondensed-SemiBold", "Avenir Next", "Segoe UI", "Helvetica Neue", "Arial")); } }
        public static Font Heading { get { return _heading ?? (_heading = Load("BarlowCondensed-SemiBold", "Avenir Next Condensed", "Bahnschrift", "Arial Narrow")); } }
        /// <summary>Condensed bold face for titles, buttons and big numbers.</summary>
        public static Font Display { get { return _display ?? (_display = Load("BarlowCondensed-Bold", "Avenir Next Condensed", "AvenirNextCondensed-Bold", "Bahnschrift", "Arial Narrow")); } }

        static Font Load(string resource, params string[] osFallback)
        {
            var f = Resources.Load<Font>("Fonts/" + resource);
            return f != null ? f : MakeFont(osFallback);
        }

        static Font MakeFont(string[] names)
        {
            Font f = null;
            try { f = Font.CreateDynamicFontFromOSFont(names, 32); } catch { }
            if (f == null)
            {
                try { f = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); } catch { }
            }
            if (f == null) f = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return f;
        }

        static Sprite _rounded, _circle, _roundedSmall, _roundedGrad;
        /// <summary>Rounded rect with a top-to-bottom sheen; tint it like any panel.</summary>
        public static Sprite RoundedGrad { get { return _roundedGrad ?? (_roundedGrad = MakeRounded(96, 26, true, 0.78f)); } }
        public static Sprite Rounded { get { return _rounded ?? (_rounded = MakeRounded(96, 30)); } }
        public static Sprite RoundedSmall { get { return _roundedSmall ?? (_roundedSmall = MakeRounded(48, 14)); } }
        public static Sprite Circle { get { return _circle ?? (_circle = MakeRounded(128, 64, false)); } }

        static Sprite MakeRounded(int size, int radius, bool sliced = true, float bottomShade = 1f)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius), cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    // Gradient lives in the slice centre rows so it stretches with the panel.
                    float g = Mathf.Lerp(bottomShade, 1f, (y + 0.5f) / size);
                    if (y > size - 5 && bottomShade < 1f) g = 1.12f; // top highlight lip
                    byte v = (byte)Mathf.Clamp(g * 235f, 0, 255);
                    px[y * size + x] = bottomShade < 1f ? new Color32(v, v, v, (byte)(a * 255)) : new Color32(255, 255, 255, (byte)(a * 255));
                }
            tex.SetPixels32(px);
            tex.Apply();
            var border = sliced ? new Vector4(radius, radius, radius, radius) : Vector4.zero;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, border);
        }

        // ---------------------------------------------------------------- layout helpers

        public static RectTransform RT(GameObject go) { var r = go.GetComponent<RectTransform>(); return r != null ? r : go.AddComponent<RectTransform>(); }

        public static GameObject Node(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        public static RectTransform Anchor(GameObject go, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            var r = RT(go);
            r.anchorMin = min; r.anchorMax = max; r.offsetMin = offMin; r.offsetMax = offMax;
            return r;
        }

        public static RectTransform Stretch(GameObject go, float pad = 0) { return Anchor(go, Vector2.zero, Vector2.one, new Vector2(pad, pad), new Vector2(-pad, -pad)); }

        public static RectTransform Place(GameObject go, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var r = RT(go);
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = pivot;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            return r;
        }

        public static LayoutElement Size(GameObject go, float w = -1, float h = -1, float flexW = -1, float flexH = -1)
        {
            var le = go.GetComponent<LayoutElement>();
            if (le == null) le = go.AddComponent<LayoutElement>();
            if (w >= 0) { le.preferredWidth = w; le.minWidth = w; }
            if (h >= 0) { le.preferredHeight = h; le.minHeight = h; }
            if (flexW >= 0) le.flexibleWidth = flexW;
            if (flexH >= 0) le.flexibleHeight = flexH;
            return le;
        }

        public static VerticalLayoutGroup VStack(GameObject go, float spacing = 12, int pad = 0, TextAnchor align = TextAnchor.UpperCenter, bool fit = false)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(pad, pad, pad, pad);
            v.childAlignment = align;
            v.childControlWidth = true; v.childControlHeight = true;
            v.childForceExpandWidth = true; v.childForceExpandHeight = false;
            if (fit) { var f = go.AddComponent<ContentSizeFitter>(); f.verticalFit = ContentSizeFitter.FitMode.PreferredSize; }
            return v;
        }

        public static HorizontalLayoutGroup HStack(GameObject go, float spacing = 12, int pad = 0, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = new RectOffset(pad, pad, pad, pad);
            h.childAlignment = align;
            h.childControlWidth = true; h.childControlHeight = true;
            h.childForceExpandWidth = false; h.childForceExpandHeight = false;
            return h;
        }

        public static GameObject Row(Transform parent, float height, float spacing = 12, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var go = Node("Row", parent);
            HStack(go, spacing, 0, align);
            Size(go, -1, height);
            return go;
        }

        public static GameObject Spacer(Transform parent, float w = -1, float h = -1)
        {
            var go = Node("Spacer", parent);
            Size(go, w, h, w < 0 ? 1 : -1, -1);
            return go;
        }

        // ---------------------------------------------------------------- widgets

        public static Image Panel(Transform parent, Color c, string name = "Panel", Sprite sprite = null, bool shadow = false)
        {
            var go = Node(name, parent);
            var img = go.AddComponent<Image>();
            img.sprite = sprite ?? Rounded;
            img.type = Image.Type.Sliced;
            img.color = c;
            if (shadow)
            {
                var s = go.AddComponent<Shadow>();
                s.effectColor = new Color(0, 0, 0, 0.35f);
                s.effectDistance = new Vector2(0, -6);
            }
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft, bool heading = false, FontStyle style = FontStyle.Normal)
        {
            var go = Node("Label", parent);
            var t = go.AddComponent<Text>();
            bool bold = style == FontStyle.Bold;
            t.font = heading ? Heading : bold ? BodyBold : Body; // real bold faces, not synthesized
            if (bold || heading) style = FontStyle.Normal;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            return t;
        }

        public static Button Btn(Transform parent, string text, Color bg, Action onClick, int fontSize = 26, float w = -1, float h = 64, bool enabled = true)
        {
            // Chunky "3D" button: a darker base slab with the gradient face sitting on top of it.
            var go = Node("Button " + text, parent);
            Size(go, w, h);
            Color face = enabled ? bg : Disabled;
            var baseImg = Panel(go.transform, MeshBuilder.Darken(face, 0.55f), "Base", Rounded);
            Stretch(baseImg.gameObject);
            var drop = baseImg.gameObject.AddComponent<Shadow>();
            drop.effectColor = new Color(0, 0, 0, 0.3f);
            drop.effectDistance = new Vector2(0, -4);
            var img = Panel(go.transform, face, "Face", RoundedGrad);
            Anchor(img.gameObject, Vector2.zero, Vector2.one, new Vector2(0, 6), Vector2.zero);
            var b = go.AddComponent<Button>();
            b.targetGraphic = img;
            var colors = b.colors;
            colors.highlightedColor = new Color(1.06f, 1.06f, 1.06f);
            colors.pressedColor = new Color(0.85f, 0.85f, 0.85f);
            colors.disabledColor = Color.white;
            b.colors = colors;
            b.interactable = enabled;
            var t = Label(img.transform, text.ToUpperInvariant(), Mathf.RoundToInt(fontSize * 1.08f), enabled ? Color.white : new Color(0.95f, 0.93f, 0.9f, 0.9f), TextAnchor.MiddleCenter, false, FontStyle.Bold);
            t.font = Display;
            t.fontStyle = FontStyle.Normal; // the face is already bold
            t.resizeTextForBestFit = true;
            t.resizeTextMinSize = 12;
            t.resizeTextMaxSize = t.fontSize;
            Stretch(t.gameObject, 8);
            var sh = t.gameObject.AddComponent<Outline>();
            sh.effectColor = new Color(0, 0, 0, 0.35f);
            sh.effectDistance = new Vector2(1.5f, -1.5f);
            go.AddComponent<ButtonFx>();
            if (onClick != null) b.onClick.AddListener(() => { AudioManager.Play(Sfx.Click); onClick(); });
            return b;
        }

        /// <summary>Two-image progress bar; returns the fill image (set its RT anchorMax.x via SetBar).</summary>
        public static Image Bar(Transform parent, Color fill, float height = 16, Color? back = null)
        {
            var bg = Panel(parent, back ?? new Color(0, 0, 0, 0.25f), "Bar", RoundedSmall);
            Size(bg.gameObject, -1, height, 1);
            var f = Panel(bg.transform, fill, "Fill", RoundedSmall);
            Anchor(f.gameObject, Vector2.zero, new Vector2(0, 1), Vector2.zero, Vector2.zero);
            return f;
        }

        public static void SetBar(Image fill, float t)
        {
            var r = fill.rectTransform;
            r.anchorMax = new Vector2(Mathf.Clamp01(t), 1);
            fill.enabled = t > 0.001f;
        }

        /// <summary>Round resource icon with its glyph, optionally with an amount label.</summary>
        static readonly System.Collections.Generic.Dictionary<string, Sprite> _icons = new System.Collections.Generic.Dictionary<string, Sprite>();

        /// <summary>Painted icon from Resources/UI/Icons (Tools/art/ai_icons.py), or null when it isn't there.</summary>
        public static Sprite IconSprite(string id)
        {
            Sprite s;
            if (_icons.TryGetValue(id, out s)) return s;
            var tex = Resources.Load<Texture2D>("UI/Icons/" + id);
            if (tex != null) s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100);
            _icons[id] = s;
            return s;
        }

        /// <summary>An Image showing a painted icon at size × size, or null so callers can draw a fallback.</summary>
        public static GameObject Icon(Transform parent, string id, float size)
        {
            var sp = IconSprite(id);
            if (sp == null) return null;
            var go = Node("Icon " + id, parent);
            var img = go.AddComponent<Image>();
            img.sprite = sp;
            img.preserveAspect = true;
            img.raycastTarget = false;
            Size(go, size, size);
            ((RectTransform)go.transform).sizeDelta = new Vector2(size, size);
            return go;
        }

        public static GameObject ResIcon(Transform parent, string resId, float size = 44)
        {
            var painted = Icon(parent, resId, size * 1.15f);
            if (painted != null) { painted.AddComponent<ResourceInfoTap>().ResourceId = resId; return painted; }
            var res = GameDatabase.Resource(resId);
            var img = Panel(parent, Palette.Hex(res.ColorRgb), "Icon " + resId, Circle);
            Size(img.gameObject, size, size);
            var ring = img.gameObject.AddComponent<Outline>();
            ring.effectColor = new Color(0, 0, 0, 0.45f);
            ring.effectDistance = new Vector2(2, -2);
            var lum = 0.3f * img.color.r + 0.59f * img.color.g + 0.11f * img.color.b;
            var t = Label(img.transform, res.Glyph, Mathf.RoundToInt(size * 0.42f), lum > 0.55f ? Ink : Cream, TextAnchor.MiddleCenter, true, FontStyle.Bold);
            Stretch(t.gameObject);
            img.gameObject.AddComponent<ResourceInfoTap>().ResourceId = resId;
            return img.gameObject;
        }

        public static GameObject Amount(Transform parent, string resId, string text, Color textColor, float iconSize = 40, int fontSize = 24)
        {
            var row = Node("Amount", parent);
            HStack(row, 6);
            ResIcon(row.transform, resId, iconSize);
            var l = Label(row.transform, text, fontSize, textColor);
            Size(l.gameObject, -1, iconSize);
            var fit = row.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            return row;
        }

        public static GameObject CoinIcon(Transform parent, float size = 40)
        {
            var painted = Icon(parent, "coin", size * 1.1f);
            if (painted != null) return painted;
            var img = Panel(parent, Gold, "Coin", Circle);
            Size(img.gameObject, size, size);
            var inner = Panel(img.transform, BrassDark, "Inner", Circle);
            Stretch(inner.gameObject, size * 0.18f);
            var inner2 = Panel(inner.transform, Gold, "Inner2", Circle);
            Stretch(inner2.gameObject, size * 0.06f);
            var t = Label(inner2.transform, "$", Mathf.RoundToInt(size * 0.42f), BrassDark, TextAnchor.MiddleCenter, true, FontStyle.Bold);
            Stretch(t.gameObject);
            return img.gameObject;
        }

        public static GameObject NuggetIcon(Transform parent, float size = 40)
        {
            var painted = Icon(parent, "nugget", size * 1.1f);
            if (painted != null) return painted;
            var holder = Node("Nugget", parent);
            Size(holder, size, size);
            var img = Panel(holder.transform, Palette.Hex(0xFFD34D), "Gem", RoundedSmall);
            var r = Place(img.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.66f, size * 0.66f));
            r.localRotation = Quaternion.Euler(0, 0, 45);
            var o = img.gameObject.AddComponent<Outline>();
            o.effectColor = BrassDark;
            o.effectDistance = new Vector2(2, -2);
            var shine = Panel(img.transform, new Color(1, 1, 1, 0.6f), "Shine", Circle);
            Place(shine.gameObject, new Vector2(0.3f, 0.7f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size * 0.18f, size * 0.18f));
            return holder;
        }

        /// <summary>Renders a cost line: coins + resources, red where unaffordable.</summary>
        public static GameObject Cost(Transform parent, GameSession s, int coins, ResourceAmount[] res, int nuggets = 0, int fontSize = 22)
        {
            var row = Node("Cost", parent);
            HStack(row, 14);
            Size(row, -1, 40);
            if (coins > 0)
            {
                var c = Node("Coins", row.transform); HStack(c, 6);
                CoinIcon(c.transform, 30);
                Label(c.transform, Fmt.Num(coins), fontSize, s.Coins >= coins ? Ink : Red);
            }
            if (nuggets > 0)
            {
                var n = Node("Nuggets", row.transform); HStack(n, 6);
                NuggetIcon(n.transform, 30);
                Label(n.transform, Fmt.Num(nuggets), fontSize, s.Nuggets >= nuggets ? Ink : Red);
            }
            if (res != null)
                foreach (var r in res)
                    Amount(row.transform, r.id, s.Amount(r.id) + "/" + r.amount, s.Amount(r.id) >= r.amount ? Ink : Red, 30, fontSize);
            return row;
        }

        public static Text Badge(Transform parent, string text, Color c)
        {
            var img = Panel(parent, c, "Badge", Circle);
            Place(img.gameObject, new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(-10, -10), new Vector2(38, 38));
            var t = Label(img.transform, text, 20, Cream, TextAnchor.MiddleCenter, true, FontStyle.Bold);
            Stretch(t.gameObject);
            return t;
        }
    }

    /// <summary>Signature widgets: ribbon banners, "Rewards" dividers, notepad slips and level chips.</summary>
    public static class UIDeco
    {
        /// <summary>A ribbon banner with folded tails, the classic reward/achievement header.</summary>
        public static GameObject Ribbon(Transform parent, string text, Color color, float width = 900, float height = 110)
        {
            var root = UIKit.Node("Ribbon", parent);
            UIKit.Size(root, width + 160, height + 20);
            for (int s = -1; s <= 1; s += 2)
            {
                var tail = UIKit.Panel(root.transform, MeshBuilder.Darken(color, 0.72f), "Tail", UIKit.RoundedSmall);
                UIKit.Place(tail.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(s * (width / 2 + 20), -14), new Vector2(170, height * 0.72f));
                var notch = UIKit.Panel(tail.transform, MeshBuilder.Darken(color, 0.5f), "Fold", UIKit.RoundedSmall);
                UIKit.Place(notch.gameObject, new Vector2(s < 0 ? 1 : 0, 1), new Vector2(0.5f, 0.5f), new Vector2(s * -20, -6), new Vector2(40, 24)).localRotation = Quaternion.Euler(0, 0, s * 35);
            }
            var band = UIKit.Panel(root.transform, color, "Band", UIKit.RoundedGrad, true);
            UIKit.Place(band.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, height));
            var trim = UIKit.Panel(band.transform, new Color(UIKit.Brass.r, UIKit.Brass.g, UIKit.Brass.b, 0.9f), "Trim", UIKit.RoundedSmall);
            UIKit.Anchor(trim.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(18, -14), new Vector2(-18, -10));
            var trim2 = UIKit.Panel(band.transform, new Color(UIKit.Brass.r, UIKit.Brass.g, UIKit.Brass.b, 0.9f), "Trim", UIKit.RoundedSmall);
            UIKit.Anchor(trim2.gameObject, new Vector2(0, 0), new Vector2(1, 0), new Vector2(18, 10), new Vector2(-18, 14));
            var t = UIKit.Label(band.transform, text.ToUpperInvariant(), Mathf.RoundToInt(height * 0.5f), Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            t.font = UIKit.Display;
            t.fontStyle = FontStyle.Normal; // the face is already bold
            UIKit.Stretch(t.gameObject, 12);
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.45f);
            sh.effectDistance = new Vector2(0, -3);
            return root;
        }

        /// <summary>"—— Rewards ——" divider line with a centered caption.</summary>
        public static void Divider(Transform parent, string caption, Color color)
        {
            var row = UIKit.Row(parent, 30, 12, TextAnchor.MiddleCenter);
            var l = UIKit.Panel(row.transform, new Color(color.r, color.g, color.b, 0.45f), "Line", UIKit.RoundedSmall);
            UIKit.Size(l.gameObject, -1, 3, 1);
            var t = UIKit.Label(row.transform, caption, 22, color, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            t.font = UIKit.Display;
            t.fontStyle = FontStyle.Normal; // the face is already bold
            UIKit.Size(t.gameObject, caption.Length * 13 + 20, 30);
            var r = UIKit.Panel(row.transform, new Color(color.r, color.g, color.b, 0.45f), "Line", UIKit.RoundedSmall);
            UIKit.Size(r.gameObject, -1, 3, 1);
        }

        /// <summary>A row of big reward icons with amounts underneath.</summary>
        public static void Rewards(Transform parent, GameSession s, int coins, int xp, int nuggets, ResourceAmount[] items = null)
        {
            var row = UIKit.Row(parent, 80, 8, TextAnchor.MiddleCenter);
            if (coins > 0) Reward(row.transform, t => UIKit.CoinIcon(t, 44), Fmt.Num(coins));
            if (xp > 0) Reward(row.transform, t => XpIcon(t, 44), xp.ToString());
            if (nuggets > 0) Reward(row.transform, t => UIKit.NuggetIcon(t, 44), nuggets.ToString());
            if (items != null) foreach (var i in items) { var id = i.id; Reward(row.transform, t => UIKit.ResIcon(t, id, 44), i.amount.ToString()); }
        }

        static void Reward(Transform parent, Action<Transform> icon, string amount)
        {
            var col = UIKit.Node("Reward", parent);
            UIKit.VStack(col, 2, 0, TextAnchor.MiddleCenter);
            UIKit.Size(col, 72, 78);
            var ih = UIKit.Node("Icon", col.transform);
            UIKit.Size(ih, -1, 46);
            UIKit.HStack(ih, 0, 0, TextAnchor.MiddleCenter);
            icon(ih.transform);
            var t = UIKit.Label(col.transform, amount, 22, UIKit.Ink, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            t.font = UIKit.Display;
            t.fontStyle = FontStyle.Normal; // the face is already bold
            UIKit.Size(t.gameObject, -1, 30);
        }

        public static GameObject XpIcon(Transform parent, float size)
        {
            var painted = UIKit.Icon(parent, "xp", size * 1.1f);
            if (painted != null) return painted;
            var img = UIKit.Panel(parent, Palette.Hex(0xF29B2C), "XP", UIKit.RoundedSmall);
            UIKit.Size(img.gameObject, size, size);
            img.transform.localRotation = Quaternion.identity;
            var o = img.gameObject.AddComponent<Outline>();
            o.effectColor = Palette.Hex(0xA8571A);
            o.effectDistance = new Vector2(2, -2);
            var t = UIKit.Label(img.transform, "XP", Mathf.RoundToInt(size * 0.46f), Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            t.font = UIKit.Display;
            t.fontStyle = FontStyle.Normal; // the face is already bold
            UIKit.Stretch(t.gameObject);
            t.gameObject.AddComponent<Outline>().effectColor = Palette.Hex(0xA8571A);
            return img.gameObject;
        }

        /// <summary>Notepad slip with a perforated top edge, used for delivery requirements.</summary>
        public static Transform Slip(Transform parent, float height)
        {
            var slip = UIKit.Panel(parent, UIKit.Parchment, "Slip", UIKit.Rounded, true);
            UIKit.Size(slip.gameObject, -1, height);
            var holes = UIKit.Node("Perforation", slip.transform);
            UIKit.Anchor(holes, new Vector2(0, 1), new Vector2(1, 1), new Vector2(14, -14), new Vector2(-14, 4));
            UIKit.HStack(holes, 0, 0, TextAnchor.MiddleCenter).childForceExpandWidth = true;
            for (int i = 0; i < 14; i++)
            {
                var h = UIKit.Panel(holes.transform, new Color(0, 0, 0, 0.55f), "Hole", UIKit.Circle);
                UIKit.Size(h.gameObject, 12, 12);
            }
            var body = UIKit.Node("Body", slip.transform);
            UIKit.Stretch(body, 12);
            UIKit.RT(body).offsetMax = new Vector2(-12, -22);
            UIKit.VStack(body, 4, 0, TextAnchor.MiddleCenter);
            return body.transform;
        }

        /// <summary>The "LVL 4 → 5" chip.</summary>
        public static void LevelChip(Transform parent, int from, int to)
        {
            var chip = UIKit.Panel(parent, UIKit.Gold, "LevelChip", UIKit.RoundedGrad, true);
            UIKit.Size(chip.gameObject, 260, 64);
            UIKit.HStack(chip.gameObject, 10, 8, TextAnchor.MiddleCenter);
            var a = UIKit.Label(chip.transform, "LVL", 30, UIKit.Wood, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            a.font = UIKit.Display;
            a.fontStyle = FontStyle.Normal; // the face is already bold UIKit.Size(a.gameObject, 70, 50);
            var b = UIKit.Label(chip.transform, from + " → " + to, 36, Color.white, TextAnchor.MiddleCenter, false, FontStyle.Bold);
            b.font = UIKit.Display;
            b.fontStyle = FontStyle.Normal; // the face is already bold UIKit.Size(b.gameObject, 150, 50);
            b.gameObject.AddComponent<Outline>().effectColor = UIKit.BrassDark;
        }
    }

    /// <summary>Squash-and-stretch press feedback for buttons.</summary>
    public sealed class ButtonFx : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        float _target = 1, _scale = 1;
        public void OnPointerDown(PointerEventData e) { _target = 0.93f; }
        public void OnPointerUp(PointerEventData e) { _target = 1; }
        public void OnPointerExit(PointerEventData e) { _target = 1; }
        void Update()
        {
            _scale = Mathf.Lerp(_scale, _target, 1 - Mathf.Exp(-Time.unscaledDeltaTime * 20));
            transform.localScale = new Vector3(_scale, _scale, 1);
        }
    }

    public static class Fmt
    {
        public static string Num(long n)
        {
            if (n >= 10_000_000) return (n / 1_000_000.0).ToString("0.#") + "M";
            if (n >= 100_000) return (n / 1000.0).ToString("0") + "K";
            return n.ToString("#,0");
        }

        public static string Time(double seconds)
        {
            if (seconds <= 0) return "Ready";
            var s = (int)Math.Ceiling(seconds);
            if (s >= 3600) return (s / 3600) + "h " + (s % 3600 / 60).ToString("00") + "m";
            if (s >= 60) return (s / 60) + "m " + (s % 60).ToString("00") + "s";
            return s + "s";
        }
    }
}
