using System.Collections.Generic;
using UnityEngine;

namespace Ironhorse.Game
{
    public enum Sfx { Click, Coin, Build, Error, LevelUp, Deliver, Bell, Open }

    /// <summary>
    /// All sound is synthesized at startup — no audio assets required. Swap any clip for a recorded
    /// asset later by assigning it in <see cref="_clips"/>.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        static AudioManager _i;
        const int Rate = 22050;

        readonly Dictionary<Sfx, AudioClip> _clips = new Dictionary<Sfx, AudioClip>();
        AudioClip _chuff, _whistle, _wind, _music, _bird, _cricket, _ambDay, _ambNight;
        AudioSource _ui, _ambient, _ambientNight, _musicSrc, _chuffSrc, _whistleSrc;
        bool _recordedBeds;

        static AudioClip Load(string name) { return Resources.Load<AudioClip>("Audio/" + name); }

        void Recorded(Sfx s, string name)
        {
            var c = Load(name);
            if (c != null) _clips[s] = c;
        }
        System.Random _rng = new System.Random(5);

        ConsistView _chuffOwner;
        float _chuffSpeed, _chuffDist = float.MaxValue, _chuffTimer;
        bool _chuffSteam;
        float _critterTimer = 3;

        public static bool SfxOn = true, MusicOn = true;

        void Awake()
        {
            _i = this;
            _ui = Src(false);
            _ambient = Src(true);
            _musicSrc = Src(true);
            _chuffSrc = Src(false);
            _whistleSrc = Src(false);

            _clips[Sfx.Click] = Make("click", 0.05f, t => Mathf.Sin(2 * Mathf.PI * 1400 * t) * Env(t, 0.002f, 0.04f) * 0.5f);
            _clips[Sfx.Open] = Make("open", 0.12f, t => Mathf.Sin(2 * Mathf.PI * Mathf.Lerp(500, 900, t / 0.12f) * t) * Env(t, 0.005f, 0.1f) * 0.35f);
            _clips[Sfx.Coin] = Make("coin", 0.5f, t => (Mathf.Sin(2 * Mathf.PI * 1318.5f * t) * Env(t, 0.002f, 0.12f) + (t > 0.08f ? Mathf.Sin(2 * Mathf.PI * 1760 * t) * Env(t - 0.08f, 0.002f, 0.35f) : 0)) * 0.4f);
            _clips[Sfx.Error] = Make("error", 0.35f, t => Mathf.Sign(Mathf.Sin(2 * Mathf.PI * (t < 0.15f ? 220 : 165) * t)) * Env(t % 0.17f, 0.005f, 0.14f) * 0.18f);
            _clips[Sfx.Build] = Make("build", 0.7f, t => Thump(t) + Thump(t - 0.22f) * 0.8f + Thump(t - 0.44f) * 0.9f);
            _clips[Sfx.Bell] = Make("bell", 1.6f, t => (Mathf.Sin(2 * Mathf.PI * 880 * t) + 0.5f * Mathf.Sin(2 * Mathf.PI * 2390 * t) + 0.3f * Mathf.Sin(2 * Mathf.PI * 1420 * t)) * Env(t, 0.002f, 1.4f) * 0.25f);
            _clips[Sfx.LevelUp] = Pluck("levelup", new[] { 392f, 493.9f, 587.3f, 784f, 987.8f }, 0.11f, 1.8f, 0.6f);
            _clips[Sfx.Deliver] = Pluck("deliver", new[] { 587.3f, 784f, 1174.7f }, 0.09f, 1.2f, 0.5f);
            _chuff = Make("chuff", 0.22f, t => Noise() * Env(t, 0.004f, 0.18f) * 0.55f, lowpass: 0.25f);
            _whistle = Make("whistle", 1.8f, Whistle);
            _wind = MakeLoop("wind", 8f, t => Noise() * (0.5f + 0.5f * Mathf.Sin(t * 0.8f) * Mathf.Sin(t * 0.33f + 1)) * 0.25f, lowpass: 0.03f);
            _bird = Make("bird", 0.5f, t => Mathf.Sin(2 * Mathf.PI * (2600 + 1600 * Mathf.Sin(t * 40)) * t) * Env(t % 0.12f, 0.005f, 0.08f) * (t < 0.36f ? 1 : 0) * 0.12f);
            _cricket = Make("cricket", 0.6f, t => Mathf.Sin(2 * Mathf.PI * 4600 * t) * (Mathf.Sin(2 * Mathf.PI * 30 * t) > 0 ? 1 : 0) * Env(t, 0.01f, 0.55f) * 0.06f);
            _music = Music();

            // Recorded audio (Resources/Audio, Tools/art/audio.py) replaces the synthesized sounds where present.
            Recorded(Sfx.Click, "click"); Recorded(Sfx.Open, "click"); Recorded(Sfx.Coin, "coin"); Recorded(Sfx.Error, "error");
            Recorded(Sfx.Build, "build"); Recorded(Sfx.Bell, "bell"); Recorded(Sfx.LevelUp, "levelup"); Recorded(Sfx.Deliver, "deliver");
            _chuff = Load("chuff") ?? _chuff;
            _whistle = Load("whistle") ?? _whistle;
            var music = Load("music");
            _ambDay = Load("amb_day");
            _ambNight = Load("amb_night");
            _recordedBeds = _ambDay != null && _ambNight != null;
            if (_recordedBeds)
            {
                _ambient.clip = _ambDay; _ambient.loop = true; _ambient.volume = 0; _ambient.Play();
                _ambientNight = Src(true);
                _ambientNight.clip = _ambNight; _ambientNight.volume = 0; _ambientNight.Play();
            }
            else { _ambient.clip = _wind; _ambient.loop = true; _ambient.volume = 0.25f; _ambient.Play(); }
            _musicSrc.clip = music ?? _music; _musicSrc.loop = true; _musicSrc.volume = music != null ? 0.32f : 0.22f; _musicSrc.Play();
        }

        AudioSource Src(bool loop)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.loop = loop;
            s.spatialBlend = 0;
            return s;
        }

        void Update()
        {
            _musicSrc.mute = !MusicOn;
            _ambient.mute = !SfxOn;
            if (_recordedBeds)
            {
                // Prairie wind and birds by day, crickets by night, crossfaded with the light.
                var cycle = DayNightCycle.Instance;
                float night = cycle != null ? Mathf.Clamp01(cycle.Night) : 0;
                _ambient.volume = Mathf.MoveTowards(_ambient.volume, 0.42f * (1 - night), Time.unscaledDeltaTime * 0.3f);
                _ambientNight.volume = Mathf.MoveTowards(_ambientNight.volume, 0.42f * night, Time.unscaledDeltaTime * 0.3f);
                _ambientNight.mute = !SfxOn;
            }

            // Locomotive chuffs: the closest moving steam train to the camera sets the rhythm.
            if (_chuffOwner != null && _chuffSteam && _chuffSpeed > 0.6f && SfxOn)
            {
                _chuffTimer -= Time.deltaTime;
                float perSecond = Mathf.Clamp(_chuffSpeed * 0.5f, 1.2f, 9f);
                if (_chuffTimer <= 0)
                {
                    _chuffTimer = 1f / perSecond;
                    float vol = Mathf.Clamp01(1.2f - _chuffDist / 160f) * 0.7f;
                    _chuffSrc.pitch = 0.9f + (float)_rng.NextDouble() * 0.2f;
                    if (vol > 0.02f) _chuffSrc.PlayOneShot(_chuff, vol);
                }
            }
            _chuffOwner = null;
            _chuffDist = float.MaxValue;

            _critterTimer -= Time.deltaTime;
            if (_critterTimer <= 0 && SfxOn && !_recordedBeds) // the recorded beds already carry birds and crickets
            {
                _critterTimer = 2.5f + (float)_rng.NextDouble() * 6f;
                var dn = DayNightCycle.Instance;
                bool night = dn != null && dn.Night > 0.5f;
                _ui.PlayOneShot(night ? _cricket : _bird, 0.6f);
            }
        }

        // ---------------------------------------------------------------- API

        public static void Play(Sfx s, float vol = 1)
        {
            switch (s)
            {
                case Sfx.Click: Haptics.Play(Haptic.Light); break;
                case Sfx.Coin: case Sfx.Build: case Sfx.Deliver: Haptics.Play(Haptic.Medium); break;
                case Sfx.LevelUp: case Sfx.Bell: Haptics.Play(Haptic.Success); break;
                case Sfx.Error: Haptics.Play(Haptic.Error); break;
            }
            if (_i == null || !SfxOn) return;
            AudioClip c;
            if (_i._clips.TryGetValue(s, out c)) _i._ui.PlayOneShot(c, vol);
        }

        public static void Whistle(Vector3 at, float vol = 1)
        {
            if (_i == null || !SfxOn || CameraRig.Instance == null) return;
            float d = Vector3.Distance(CameraRig.Instance.transform.position, at);
            float v = Mathf.Clamp01(1.3f - d / 250f) * vol * 0.55f;
            if (v > 0.03f) _i._whistleSrc.PlayOneShot(_i._whistle, v);
        }

        public static void Chuff(ConsistView owner, float speed, bool steam)
        {
            if (_i == null || CameraRig.Instance == null) return;
            float d = Vector3.Distance(CameraRig.Instance.transform.position, owner.LeadPosition);
            if (speed > 0.6f && d < _i._chuffDist)
            {
                _i._chuffDist = d;
                _i._chuffOwner = owner;
                _i._chuffSpeed = speed;
                _i._chuffSteam = steam;
            }
        }

        // ---------------------------------------------------------------- synthesis

        float Noise() { return (float)_rng.NextDouble() * 2 - 1; }

        static float Env(float t, float attack, float decay)
        {
            if (t < 0) return 0;
            if (t < attack) return t / attack;
            return Mathf.Exp(-(t - attack) / decay * 4f);
        }

        float Thump(float t)
        {
            if (t < 0) return 0;
            return (Mathf.Sin(2 * Mathf.PI * 95 * t) * 0.8f + Noise() * 0.25f) * Env(t, 0.002f, 0.12f) * 0.6f;
        }

        float Whistle(float t)
        {
            float a = Mathf.Clamp01(t / 0.08f) * Mathf.Clamp01((1.8f - t) / 0.4f);
            float bend = 1 + 0.015f * Mathf.Clamp01(t / 0.2f);
            float s = Mathf.Sin(2 * Mathf.PI * 392f * bend * t) + Mathf.Sin(2 * Mathf.PI * 493.9f * bend * t) * 0.9f + Mathf.Sin(2 * Mathf.PI * 587.3f * bend * t) * 0.8f
                      + Mathf.Sin(2 * Mathf.PI * 784f * bend * t) * 0.25f;
            return (s * 0.22f + Noise() * 0.06f) * a;
        }

        AudioClip Make(string name, float seconds, System.Func<float, float> f, float lowpass = 1f)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            float y = 0;
            for (int i = 0; i < n; i++)
            {
                float x = f(i / (float)Rate);
                y += (x - y) * lowpass;
                data[i] = Mathf.Clamp(y, -1, 1);
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        AudioClip MakeLoop(string name, float seconds, System.Func<float, float> f, float lowpass)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            var data = new float[n];
            float y = 0;
            for (int i = 0; i < n; i++) { y += (f(i / (float)Rate) - y) * lowpass; data[i] = y * 4f; }
            // crossfade the last half second into the start so the loop is seamless
            int fade = Rate / 2;
            for (int i = 0; i < fade; i++)
            {
                float k = i / (float)fade;
                data[i] = Mathf.Lerp(data[n - fade + i], data[i], k);
            }
            var clip = AudioClip.Create(name, n - fade, 1, Rate, false);
            var trimmed = new float[n - fade];
            System.Array.Copy(data, trimmed, n - fade);
            clip.SetData(trimmed, 0);
            return clip;
        }

        /// <summary>Karplus–Strong plucked string — sounds like a guitar/banjo.</summary>
        void PluckInto(float[] buf, int start, float freq, float amp, float damping = 0.996f)
        {
            int period = Mathf.Max(2, Mathf.RoundToInt(Rate / freq));
            var ring = new float[period];
            for (int i = 0; i < period; i++) ring[i] = Noise() * amp;
            int idx = 0;
            int len = Mathf.Min(buf.Length - start, Rate * 2);
            for (int i = 0; i < len; i++)
            {
                int next = (idx + 1) % period;
                float v = ring[idx];
                ring[idx] = (ring[idx] + ring[next]) * 0.5f * damping;
                buf[start + i] += v;
                idx = next;
            }
        }

        AudioClip Pluck(string name, float[] notes, float step, float seconds, float amp)
        {
            var buf = new float[Mathf.CeilToInt(seconds * Rate)];
            for (int i = 0; i < notes.Length; i++) PluckInto(buf, Mathf.RoundToInt(i * step * Rate), notes[i], amp);
            var clip = AudioClip.Create(name, buf.Length, 1, Rate, false);
            clip.SetData(buf, 0);
            return clip;
        }

        /// <summary>A gentle 16-bar fingerpicked folk loop (G–C–D–G / Em–C–D–G).</summary>
        AudioClip Music()
        {
            const float bpm = 96;
            float beat = 60f / bpm;
            int bars = 16;
            var buf = new float[Mathf.CeilToInt(bars * 4 * beat * Rate)];
            float G = 98f, C = 130.81f, D = 146.83f, E = 82.41f;
            float[][] chords =
            {
                new[] { G, 123.47f, 146.83f, 196f }, new[] { C, 164.81f, 196f, 261.63f },
                new[] { D, 185f, 220f, 293.66f }, new[] { G, 123.47f, 146.83f, 196f },
                new[] { E, 123.47f, 164.81f, 196f }, new[] { C, 164.81f, 196f, 261.63f },
                new[] { D, 185f, 220f, 293.66f }, new[] { G, 146.83f, 196f, 246.94f },
            };
            int[] pattern = { 0, 2, 1, 3, 0, 2, 1, 2 };
            float[] melody = { 392f, 440f, 493.9f, 587.3f, 523.3f, 493.9f, 440f, 392f, 329.6f, 392f, 440f, 493.9f, 440f, 392f, 370f, 392f };
            for (int bar = 0; bar < bars; bar++)
            {
                var ch = chords[bar % chords.Length];
                for (int eighth = 0; eighth < 8; eighth++)
                {
                    int at = Mathf.RoundToInt((bar * 4 + eighth * 0.5f) * beat * Rate);
                    float swing = eighth % 2 == 1 ? 0.06f * beat * Rate : 0;
                    PluckInto(buf, at + (int)swing, ch[pattern[eighth]] * (pattern[eighth] == 0 ? 1 : 2), eighth == 0 ? 0.32f : 0.2f, 0.994f);
                }
                if (bar >= 8 && bar % 2 == 0)
                    PluckInto(buf, Mathf.RoundToInt((bar * 4 + 1) * beat * Rate), melody[(bar - 8) % melody.Length] , 0.22f, 0.997f);
                if (bar >= 8)
                    PluckInto(buf, Mathf.RoundToInt((bar * 4 + 3) * beat * Rate), melody[(bar * 2 + 1) % melody.Length], 0.18f, 0.997f);
            }
            float max = 0.0001f;
            foreach (var v in buf) max = Mathf.Max(max, Mathf.Abs(v));
            for (int i = 0; i < buf.Length; i++) buf[i] = buf[i] / max * 0.8f;
            var clip = AudioClip.Create("music", buf.Length, 1, Rate, false);
            clip.SetData(buf, 0);
            return clip;
        }
    }
}
