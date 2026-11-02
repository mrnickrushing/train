using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

namespace Ironhorse.Game
{
    /// <summary>
    /// Big-moment celebrations: a full-screen cinematic clip (StreamingAssets/Videos/{id}.mp4) on a
    /// brass-framed card with a ribbon title, queued so moments never overlap, plus quick in-game
    /// flourishes (confetti, coins flying into the HUD) for the everyday wins.
    /// </summary>
    public sealed class Celebrations : MonoBehaviour
    {
        public static Celebrations Instance { get; private set; }

        /// <summary>A clip plays at most this often (seconds), except the first time it's earned.</summary>
        public const float RepeatCooldown = 600f;

        struct Request { public string Video, Title, Subtitle; }

        readonly Queue<Request> _queue = new Queue<Request>();
        readonly Dictionary<string, float> _lastPlayed = new Dictionary<string, float>();
        RectTransform _layer;
        bool _playing;

        public static void Init(Transform canvas)
        {
            var go = new GameObject("Celebrations", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            UIKit.Stretch(go);
            Instance = go.AddComponent<Celebrations>();
            Instance._layer = (RectTransform)go.transform;
        }

        static string VideoPath(string id)
        {
            // Linux Editors can't decode H.264; Tools/art/ai_media.py install keeps VP8 copies in Library/.
            if (Application.platform == RuntimePlatform.LinuxEditor)
            {
                string webm = Path.Combine(Application.dataPath, "..", "Library", "EditorVideos", id + ".webm");
                if (File.Exists(webm)) return webm;
            }
            return Path.Combine(Application.streamingAssetsPath, "Videos", id + ".mp4");
        }

        static bool HasMedia(string id)
        {
            // Android packs StreamingAssets into the APK, so File.Exists can't see it; trust the build there.
            return Application.platform == RuntimePlatform.Android || File.Exists(VideoPath(id)) || Resources.Load<Texture2D>("UI/Cinematics/" + id) != null;
        }

        /// <summary>Queues a cinematic. Returns false (and plays nothing) when it's on cooldown or missing.</summary>
        public bool Play(string video, string title, string subtitle, bool force = false)
        {
            float last;
            if (!force && _lastPlayed.TryGetValue(video, out last) && Time.realtimeSinceStartup - last < RepeatCooldown) return false;
            if (!HasMedia(video)) return false;
            _lastPlayed[video] = Time.realtimeSinceStartup;
            _queue.Enqueue(new Request { Video = video, Title = title, Subtitle = subtitle });
            if (!_playing) StartCoroutine(Run());
            return true;
        }

        IEnumerator Run()
        {
            _playing = true;
            while (_queue.Count > 0)
            {
                var r = _queue.Dequeue();
                yield return Show(r);
            }
            _playing = false;
        }

        IEnumerator Show(Request r)
        {
            var root = UIKit.Panel(_layer, new Color(0.03f, 0.02f, 0.01f, 0), "Cinematic");
            root.sprite = null;
            UIKit.Stretch(root.gameObject);
            var group = root.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0;
            if (UIRoot.Instance != null) UIRoot.Instance.SetHudVisible(false);

            // 16:9 brass-framed screen
            var frame = UIKit.Panel(root.transform, UIKit.Brass, "Frame", UIKit.Rounded, true);
            UIKit.Place(frame.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(1312, 750));
            var screen = UIKit.Node("Screen", frame.transform);
            UIKit.Stretch(screen, 16);
            var still = Resources.Load<Texture2D>("UI/Cinematics/" + r.Video);
            if (still != null)
            {
                // Shown until the clip's first frame (and instead of it if the clip can't play), slowly zooming.
                var mask = screen.AddComponent<RectMask2D>();
                var st = UIKit.Node("Still", screen.transform).AddComponent<RawImage>();
                st.texture = still;
                UIKit.Stretch(st.gameObject);
                st.gameObject.AddComponent<KenBurns>();
            }
            var vidGo = UIKit.Node("Video", screen.transform);
            UIKit.Stretch(vidGo);
            var raw = vidGo.AddComponent<RawImage>();
            raw.color = still != null ? new Color(1, 1, 1, 0) : Color.black;
            var rt = new RenderTexture(1280, 720, 0, RenderTextureFormat.ARGB32) { name = "Cinematic" };
            raw.texture = rt;

            var ribbon = UIDeco.Ribbon(root.transform, r.Title, Palette.Hex(0x7A2E1E), 820, 110);
            UIKit.Place(ribbon, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -26), new Vector2(980, 130));
            if (!string.IsNullOrEmpty(r.Subtitle))
            {
                var sub = UIKit.Label(root.transform, r.Subtitle, 34, UIKit.Cream, TextAnchor.MiddleCenter, true);
                UIKit.Place(sub.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 70), new Vector2(1400, 60));
                sub.gameObject.AddComponent<Shadow>().effectColor = new Color(0, 0, 0, 0.7f);
            }
            var hint = UIKit.Label(root.transform, "TAP TO CONTINUE", 26, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleCenter);
            hint.font = UIKit.Display;
            UIKit.Place(hint.gameObject, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(600, 40));

            bool done = false;
            var btn = root.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => done = true);

            var vp = root.gameObject.AddComponent<VideoPlayer>();
            vp.playOnAwake = false;
            vp.source = VideoSource.Url;
            vp.url = VideoPath(r.Video);
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.targetTexture = rt;
            vp.isLooping = false;
            vp.skipOnDrop = true;
            vp.audioOutputMode = AudioManager.SfxOn ? VideoAudioOutputMode.Direct : VideoAudioOutputMode.None;
            bool failed = false;
            vp.errorReceived += (p, msg) => { Debug.LogWarning("[Celebrations] " + r.Video + ": " + msg); failed = true; };
            vp.loopPointReached += p => done = true;
            vp.Prepare();

            AudioManager.Play(Sfx.LevelUp);
            Confetti(root.transform, 90);
            float t = 0;
            while (t < 1)
            {
                t += Time.unscaledDeltaTime * 3f;
                group.alpha = t;
                root.color = new Color(0.03f, 0.02f, 0.01f, 0.86f * t);
                if (!failed && vp.isPrepared && !vp.isPlaying) { raw.color = Color.white; vp.Play(); }
                yield return null;
            }
            float waited = 0;
            while (!done)
            {
                if (!failed && vp.isPrepared && !vp.isPlaying) { raw.color = Color.white; vp.Play(); }
                waited += Time.unscaledDeltaTime;
                // No playable clip: hold the still for a few seconds instead.
                if ((failed || !vp.isPrepared) && waited > (still != null ? 4.5f : 1f)) break;
                yield return null;
            }
            vp.Stop();
            for (t = 1; t > 0; t -= Time.unscaledDeltaTime * 4f)
            {
                group.alpha = t;
                yield return null;
            }
            rt.Release();
            Destroy(rt);
            Destroy(root.gameObject);
            if (UIRoot.Instance != null) UIRoot.Instance.SetHudVisible(!UIRoot.Instance.WindowOpen);
        }

        // ------------------------------------------------------------------ everyday flourishes

        /// <summary>Burst of falling confetti over the whole screen.</summary>
        public void Confetti(Transform parent = null, int count = 70)
        {
            if (parent == null) parent = _layer;
            Color[] cols = { UIKit.Brass, Palette.Hex(0xC0392B), Palette.Hex(0x2E86C1), Palette.Hex(0x27AE60), UIKit.Cream, Palette.Hex(0xF39C12) };
            for (int i = 0; i < count; i++)
            {
                var p = UIKit.Panel(parent, cols[i % cols.Length], "Confetti", UIKit.RoundedSmall);
                p.raycastTarget = false;
                var rt = (RectTransform)p.transform;
                rt.anchorMin = rt.anchorMax = new Vector2(UnityEngine.Random.value, 1.05f);
                rt.sizeDelta = new Vector2(UnityEngine.Random.Range(10f, 18f), UnityEngine.Random.Range(18f, 30f));
                p.gameObject.AddComponent<ConfettiPiece>();
            }
        }

        /// <summary>Icons fly from a screen point into a HUD element, which pulses when they land.</summary>
        public void FlyTo(Vector2 fromScreen, RectTransform target, Action<Transform> icon, int count = 8)
        {
            if (target == null) return;
            for (int i = 0; i < count; i++)
            {
                var holder = UIKit.Node("Flyer", _layer);
                var rt = (RectTransform)holder.transform;
                rt.sizeDelta = new Vector2(56, 56);
                UIKit.HStack(holder, 0, 0, TextAnchor.MiddleCenter);
                icon(holder.transform);
                var f = holder.AddComponent<Flyer>();
                f.From = fromScreen + UnityEngine.Random.insideUnitCircle * 90f;
                f.Target = target;
                f.Delay = i * 0.06f;
            }
        }

        sealed class KenBurns : MonoBehaviour
        {
            float _t;
            void Update() { _t += Time.unscaledDeltaTime; transform.localScale = Vector3.one * (1.02f + _t * 0.025f); }
        }

        sealed class ConfettiPiece : MonoBehaviour
        {
            Vector2 _v;
            float _spin, _life;
            RectTransform _rt;

            void Start()
            {
                _rt = (RectTransform)transform;
                _v = new Vector2(UnityEngine.Random.Range(-120f, 120f), UnityEngine.Random.Range(-260f, -120f));
                _spin = UnityEngine.Random.Range(-540f, 540f);
                _life = UnityEngine.Random.Range(2.4f, 3.6f);
                _rt.anchoredPosition = new Vector2(0, UnityEngine.Random.Range(0f, 200f));
            }

            void Update()
            {
                float dt = Time.unscaledDeltaTime;
                _v.y -= 160f * dt;
                _v.x += Mathf.Sin(Time.unscaledTime * 3 + _spin) * 60f * dt;
                _rt.anchoredPosition += _v * dt;
                _rt.localRotation = Quaternion.Euler(0, Mathf.Sin(Time.unscaledTime * 6 + _spin) * 70f, _rt.localEulerAngles.z + _spin * dt);
                _life -= dt;
                if (_life <= 0) Destroy(gameObject);
            }
        }

        sealed class Flyer : MonoBehaviour
        {
            public Vector2 From;
            public RectTransform Target;
            public float Delay;
            float _t;

            void Start()
            {
                transform.position = From;
                transform.localScale = Vector3.zero;
            }

            void Update()
            {
                if (Target == null) { Destroy(gameObject); return; }
                if (Delay > 0) { Delay -= Time.unscaledDeltaTime; return; }
                _t += Time.unscaledDeltaTime / 0.75f;
                float e = _t * _t * (3 - 2 * _t);
                Vector3 to = Target.position;
                Vector3 mid = (Vector3)From + Vector3.up * 220f;
                // quadratic arc
                transform.position = Vector3.Lerp(Vector3.Lerp(From, mid, e), Vector3.Lerp(mid, to, e), e);
                transform.localScale = Vector3.one * Mathf.Min(1, _t * 6) * Mathf.Lerp(1.1f, 0.6f, e);
                if (_t >= 1)
                {
                    var pulse = Target.GetComponent<Pulse>();
                    if (pulse == null) pulse = Target.gameObject.AddComponent<Pulse>(); // not ??: Unity fake-null
                    pulse.Kick();
                    AudioManager.Play(Sfx.Coin);
                    Destroy(gameObject);
                }
            }
        }

        sealed class Pulse : MonoBehaviour
        {
            float _k;
            public void Kick() { _k = 1; }
            void Update()
            {
                _k = Mathf.Max(0, _k - Time.unscaledDeltaTime * 4);
                transform.localScale = Vector3.one * (1 + 0.12f * Mathf.Sin(_k * Mathf.PI));
            }
        }
    }
}
