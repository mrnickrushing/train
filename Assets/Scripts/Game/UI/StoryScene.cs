using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Ironhorse.Game
{
    /// <summary>One line of dialogue: who says it and what they say.</summary>
    public struct StoryLine
    {
        public string Speaker, Text;
        public StoryLine(string speaker, string text) { Speaker = speaker; Text = text; }
    }

    /// <summary>
    /// Full-screen story scene: a backdrop, characters standing at the left and right edges, speech
    /// bubbles with name tabs, typewriter text. Tap finishes the line, then advances; Skip ends it.
    /// </summary>
    public static class StoryScene
    {
        public static readonly Dictionary<string, (string name, string art)> Cast = new Dictionary<string, (string, string)>
        {
            { "hank", ("Hank", "char_foreman") },
            { "clara", ("Clara Whitlock", "char_engineer") },
            { "silas", ("Mr. Pemberton", "char_investor") },
            { "cyrus", ("Cyrus Blackwell", "char_rival") },
        };

        /// <summary>Queues the scene behind any other full-screen moment.</summary>
        public static void Play(string backdrop, StoryLine[] lines)
        {
            if (Celebrations.Instance == null || lines == null || lines.Length == 0) return;
            Celebrations.Instance.Enqueue(() => Run(backdrop, lines));
        }

        static Texture2D LoadBackdrop(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var folder in new[] { "UI/Story/", "UI/Cinematics/", "UI/" })
            {
                var t = Resources.Load<Texture2D>(folder + id);
                if (t != null) return t;
            }
            return null;
        }

        static IEnumerator Run(string backdrop, StoryLine[] lines)
        {
            var layer = Celebrations.Instance.transform;
            if (UIRoot.Instance != null) UIRoot.Instance.SetHudVisible(false);
            var root = UIKit.Panel(layer, new Color(0.04f, 0.03f, 0.02f, 1f), "Story");
            root.sprite = null;
            UIKit.Stretch(root.gameObject);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;

            var bg = LoadBackdrop(backdrop);
            if (bg != null)
            {
                var raw = UIKit.Node("Backdrop", root.transform).AddComponent<RawImage>();
                raw.texture = bg;
                raw.raycastTarget = false;
                UIKit.Stretch(raw.gameObject);
                var fit = raw.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = bg.width / (float)bg.height;
                var shade = UIKit.Panel(root.transform, new Color(0, 0, 0, 0.25f), "Shade");
                shade.sprite = null;
                shade.raycastTarget = false;
                UIKit.Stretch(shade.gameObject);
            }

            // Cast: first speaker stands left, the second right.
            var sides = new Dictionary<string, int>();
            var figures = new Dictionary<string, Image>();
            foreach (var l in lines)
            {
                if (sides.ContainsKey(l.Speaker) || sides.Count >= 2) continue;
                int side = sides.Count == 0 ? -1 : 1;
                sides[l.Speaker] = side;
                var tex = Resources.Load<Texture2D>("UI/Story/" + Cast[l.Speaker].art);
                if (tex == null) continue;
                var img = UIKit.Node("Figure " + l.Speaker, root.transform).AddComponent<Image>();
                img.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0));
                img.preserveAspect = true;
                img.raycastTarget = false;
                float h = 1000, w = h * tex.width / tex.height;
                UIKit.Place(img.gameObject, new Vector2(side < 0 ? 0 : 1, 0), new Vector2(side < 0 ? 0 : 1, 0), new Vector2(side * -60, -40), new Vector2(w, h));
                img.rectTransform.pivot = new Vector2(side < 0 ? 0 : 1, 0);
                figures[l.Speaker] = img;
            }

            // Speech bubble
            var bubble = UIKit.Panel(root.transform, new Color(0.95f, 0.92f, 0.85f, 0.97f), "Bubble", UIKit.Rounded, true);
            var brt = UIKit.Place(bubble.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(980, 230));
            var tab = UIKit.Panel(bubble.transform, UIKit.Blue, "NameTab", UIKit.RoundedSmall);
            var trt = UIKit.Place(tab.gameObject, new Vector2(0, 1), new Vector2(0, 0), new Vector2(30, -6), new Vector2(300, 56));
            var nameText = UIKit.Label(tab.transform, "", 30, Color.white, TextAnchor.MiddleCenter);
            nameText.font = UIKit.Display;
            UIKit.Stretch(nameText.gameObject, 6);
            var body = UIKit.Label(bubble.transform, "", 36, UIKit.Ink, TextAnchor.UpperLeft);
            UIKit.Stretch(body.gameObject, 34);
            var hint = UIKit.Label(root.transform, "Tap anywhere to continue", 28, new Color(1, 1, 1, 0.85f), TextAnchor.MiddleCenter);
            UIKit.Place(hint.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 50), new Vector2(800, 50));
            hint.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.8f);

            bool tapped = false, skip = false;
            var tap = root.gameObject.AddComponent<Button>();
            tap.transition = Selectable.Transition.None;
            tap.onClick.AddListener(() => tapped = true);
            var skipBtn = UIKit.Btn(root.transform, "Skip", UIKit.WoodLight, () => skip = true, 26, 170, 72);
            UIKit.Place(skipBtn.gameObject, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -30), new Vector2(170, 72));

            for (float t = 0; t < 1; t += Time.unscaledDeltaTime * 3f) { group.alpha = t; yield return null; }
            group.alpha = 1;

            foreach (var line in lines)
            {
                if (skip) break;
                int side = sides.ContainsKey(line.Speaker) ? sides[line.Speaker] : -1;
                foreach (var kv in figures)
                {
                    bool speaking = kv.Key == line.Speaker;
                    kv.Value.color = speaking ? Color.white : new Color(0.55f, 0.55f, 0.6f, 1f);
                    kv.Value.rectTransform.localScale = Vector3.one * (speaking ? 1.03f : 0.97f);
                }
                brt.anchoredPosition = new Vector2(side * 90, 150);
                trt.anchorMin = trt.anchorMax = new Vector2(side < 0 ? 0 : 1, 1);
                trt.pivot = new Vector2(side < 0 ? 0 : 1, 0);
                trt.anchoredPosition = new Vector2(side < 0 ? 30 : -30, -6);
                nameText.text = StoryScene.Cast[line.Speaker].name;
                tab.color = line.Speaker == "cyrus" ? Palette.Hex(0x7A1E22) : UIKit.Blue;
                AudioManager.Play(Sfx.Click, 0.4f);

                // Typewriter
                tapped = false;
                float shown = 0;
                while (shown < line.Text.Length && !tapped && !skip)
                {
                    shown += Time.unscaledDeltaTime * 48f;
                    body.text = line.Text.Substring(0, Mathf.Min(line.Text.Length, (int)shown));
                    yield return null;
                }
                body.text = line.Text;
                tapped = false;
                while (!tapped && !skip) yield return null;
            }

            for (float t = 1; t > 0; t -= Time.unscaledDeltaTime * 4f) { group.alpha = t; yield return null; }
            Object.Destroy(root.gameObject);
            if (UIRoot.Instance != null) UIRoot.Instance.SetHudVisible(!UIRoot.Instance.WindowOpen);
        }
    }
}
