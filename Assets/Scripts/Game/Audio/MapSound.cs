using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>
    /// A looping sound that belongs to a spot on the map (a sawmill, a mine, a wagon on the road). It plays louder the
    /// closer the camera is and the further it's zoomed in, fades out past its radius, and pauses when silent so dozens
    /// cost nothing. <see cref="Active"/> lets a vehicle or the turntable switch it on only while it moves.
    /// </summary>
    public sealed class MapSound : MonoBehaviour
    {
        public float Radius = 90f, Volume = 0.5f;
        /// <summary>Plays only while true (e.g. a wagon on the move).</summary>
        public bool Active = true;
        AudioSource _src;
        float _level;

        /// <summary>Adds a looping sound from Resources/Audio/<paramref name="clip"/> on <paramref name="host"/>, or null when
        /// the recording isn't there.</summary>
        public static MapSound Attach(GameObject host, string clip, float radius, float volume)
        {
            var c = Resources.Load<AudioClip>("Audio/" + clip);
            if (c == null) return null;
            var m = host.AddComponent<MapSound>();
            m.Radius = radius; m.Volume = volume;
            m._src = host.AddComponent<AudioSource>();
            m._src.clip = c; m._src.loop = true; m._src.playOnAwake = false; m._src.spatialBlend = 0; m._src.volume = 0;
            m._src.time = Random.value * c.length; // so neighbours don't play in step
            return m;
        }

        /// <summary>An ambience bed at a fixed spot (its own object under <paramref name="parent"/>).</summary>
        public static MapSound At(Transform parent, Vector3 pos, string clip, float radius, float volume)
        {
            var go = new GameObject("Sound " + clip);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var m = Attach(go, clip, radius, volume);
            if (m == null) Destroy(go);
            return m;
        }

        void Update()
        {
            var cam = CameraRig.Instance;
            float want = 0;
            if (cam != null && Active && AudioManager.SfxOn)
            {
                // Distance from the point the camera looks at, and how close the camera is (zoomed out = quieter).
                var p = transform.position;
                float d = new Vector2(p.x - cam.Focus.x, p.z - cam.Focus.z).magnitude;
                float near = Mathf.Clamp01(1 - d / Radius);
                float zoom = Mathf.Clamp01(1.25f - cam.Distance / 180f);
                want = Volume * near * near * zoom;
            }
            _level = Mathf.MoveTowards(_level, want, Time.unscaledDeltaTime * 0.8f);
            _src.volume = _level;
            if (_level > 0.002f) { if (!_src.isPlaying) _src.Play(); }
            else if (_src.isPlaying) _src.Pause();
        }
    }
}
