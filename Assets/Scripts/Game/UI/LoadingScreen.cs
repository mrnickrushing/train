using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Ironhorse.Game
{
    /// <summary>
    /// Full-screen key art with the title, a brass progress bar and a rotating tip while the world builds;
    /// fades out once the game is ready. Lives on its own top-most canvas.
    /// </summary>
    public sealed class LoadingScreen : MonoBehaviour
    {
        static readonly string[] Tips =
        {
            "Match wagons to cargo: flatcars haul timber, hoppers haul coal and ore.",
            "Factories keep working while you're away. Come back to full warehouses.",
            "Contracts pay best when you deliver the whole order.",
            "Upgrade locomotives at the depot to haul more on every run.",
            "Gold nuggets rush a train home in an instant.",
            "Civic buildings boost your whole town. Build a saloon early!",
        };

        Image _fill;
        Text _status;
        CanvasGroup _group;
        float _shown;

        public static LoadingScreen Show()
        {
            var go = new GameObject("Loading Screen", typeof(RectTransform));
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.75f;
            go.AddComponent<GraphicRaycaster>(); // swallow taps while loading
            var ls = go.AddComponent<LoadingScreen>();
            ls.Build();
            return ls;
        }

        void Build()
        {
            _group = gameObject.AddComponent<CanvasGroup>();
            var bg = UIKit.Panel(transform, Palette.Hex(0x1E140C), "Background");
            bg.sprite = null;
            UIKit.Stretch(bg.gameObject);

            var art = Resources.Load<Texture2D>("UI/LoadingArt");
            if (art != null)
            {
                var raw = UIKit.Node("Art", transform).AddComponent<RawImage>();
                raw.texture = art;
                UIKit.Stretch(raw.gameObject);
                var fit = raw.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent; // cover the screen, crop the overflow
                fit.aspectRatio = art.width / (float)art.height;
                raw.gameObject.AddComponent<SlowZoom>();
            }

            // Darken the bottom for the bar and text.
            var shade = UIKit.Panel(transform, new Color(0, 0, 0, 0.55f), "Shade", UIKit.RoundedGrad);
            UIKit.Anchor(shade.gameObject, Vector2.zero, new Vector2(1, 0), new Vector2(-40, -40), new Vector2(40, 300));

            var title = UIKit.Label(transform, "IRONHORSE FRONTIER", 118, Color.white, TextAnchor.MiddleLeft);
            title.font = UIKit.Display;
            title.fontStyle = FontStyle.Normal;
            UIKit.Anchor(title.gameObject, new Vector2(0, 1), new Vector2(1, 1), new Vector2(80, -230), new Vector2(-80, -60));
            var o = title.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0.25f, 0.13f, 0.05f, 0.9f);
            o.effectDistance = new Vector2(3, -3);
            var sh = title.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.6f);
            sh.effectDistance = new Vector2(0, -8);
            var bar = UIKit.Panel(transform, UIKit.Brass, "Underline", UIKit.RoundedSmall);
            UIKit.Place(bar.gameObject, new Vector2(0, 1), new Vector2(0, 0.5f), new Vector2(84, -222), new Vector2(560, 8));

            var track = UIKit.Panel(transform, new Color(0.08f, 0.05f, 0.03f, 0.9f), "Track", UIKit.Rounded);
            UIKit.Place(track.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(1100, 34));
            track.gameObject.AddComponent<Outline>().effectColor = UIKit.BrassDark;
            _fill = UIKit.Panel(track.transform, UIKit.Brass, "Fill", UIKit.Rounded);
            UIKit.Anchor(_fill.gameObject, Vector2.zero, new Vector2(0, 1), new Vector2(4, 4), new Vector2(-4, -4));

            _status = UIKit.Label(transform, "Laying track...", 30, UIKit.Cream, TextAnchor.MiddleCenter, true);
            UIKit.Place(_status.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 200), new Vector2(1200, 44));
            var tip = UIKit.Label(transform, "TIP: " + Tips[Random.Range(0, Tips.Length)], 26, new Color(1, 1, 1, 0.85f), TextAnchor.MiddleCenter);
            UIKit.Place(tip.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 92), new Vector2(1500, 40));
            _shown = Time.realtimeSinceStartup;
        }

        public void SetProgress(float p, string status)
        {
            var rt = (RectTransform)_fill.transform;
            rt.anchorMax = new Vector2(Mathf.Clamp01(p), 1);
            if (status != null) _status.text = status;
        }

        /// <summary>Fades out after a short minimum display (so it never just flickers) and destroys itself.</summary>
        public IEnumerator Finish()
        {
            SetProgress(1, "All aboard!");
            while (Time.realtimeSinceStartup - _shown < 1.6f) yield return null;
            for (float t = 1; t > 0; t -= Time.unscaledDeltaTime * 2.2f)
            {
                _group.alpha = t;
                yield return null;
            }
            Destroy(gameObject);
        }

        sealed class SlowZoom : MonoBehaviour
        {
            void Update() { transform.localScale = Vector3.one * (1.02f + Mathf.Min(Time.realtimeSinceStartup, 12f) * 0.004f); }
        }
    }
}
