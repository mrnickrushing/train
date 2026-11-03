using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Ironhorse.Game
{
    public enum Haptic { Light, Medium, Heavy, Success, Error }

    /// <summary>
    /// Taptic feedback on iPhone (Plugins/iOS/IronhorseHaptics.mm); does nothing elsewhere. AudioManager
    /// triggers it with the matching sound, so every tap, collect and reward is felt as well as heard.
    /// </summary>
    public static class Haptics
    {
        public static bool On = true;
        static float _last = -1;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void IronhorseHaptic(int kind);
#endif

        public static void Play(Haptic kind)
        {
            if (!On) return;
            float now = Time.unscaledTime;
            if (kind < Haptic.Success && now - _last < 0.06f) return; // a burst of coins is one buzz, not a rattle
            _last = now;
#if UNITY_IOS && !UNITY_EDITOR
            IronhorseHaptic((int)kind);
#endif
        }
    }
}
