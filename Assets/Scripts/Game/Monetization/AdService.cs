using System;
using Ironhorse.Core;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>Rewarded-video abstraction: Google AdMob on the phone (<c>AdMobAds</c>), a simulated advert in the Editor.</summary>
    public interface IAds
    {
        /// <summary>The game can show adverts at all (the Watch buttons are drawn).</summary>
        bool Available { get; }
        /// <summary>An advert is loaded and can play this moment.</summary>
        bool Loaded { get; }
        /// <summary>Shows a rewarded ad; calls back true only if it was watched to completion.</summary>
        void ShowRewarded(Action<bool> done);
    }

    public static class Ads
    {
        static IAds _ads;
        public static IAds Current
        {
            get
            {
                if (_ads != null) return _ads;
#if UNITY_IOS && !UNITY_EDITOR
                _ads = new AdMobAds();
#else
                _ads = new TestAds();
#endif
                return _ads;
            }
        }

        /// <summary>Starts the advert network early, so the first advert is loaded by the time a Watch button is tapped.</summary>
        public static void Warm() { var started = Current; }

        /// <summary>Shows an ad and, if completed, grants the reward through the simulation.</summary>
        public static void Watch(AdReward reward, int trainUid = 0, Action<ActionResult> done = null)
        {
            if (G.Session.AdsLeft(reward) <= 0) { done?.Invoke(ActionResult.Fail("No more of these today. Come back tomorrow!")); return; }
            if (!Current.Available) { done?.Invoke(ActionResult.Fail("No video available right now.")); return; }
            if (!Current.Loaded) { done?.Invoke(ActionResult.Fail("No advert is ready just now. Try again in a moment.")); return; }
            // One reward per ad, even if a network calls its completion handler more than once.
            bool finished = false;
            Current.ShowRewarded(ok =>
            {
                if (finished) return;
                finished = true;
                done?.Invoke(ok ? G.Session.RewardAd(reward, trainUid) : ActionResult.Fail("Watch the whole video to earn the reward."));
            });
        }
    }

    /// <summary>Simulated 3-second "ad" in the editor and development builds.</summary>
    public sealed class TestAds : IAds
    {
        public bool Available { get { return Application.isEditor || Debug.isDebugBuild; } }
        public bool Loaded { get { return true; } }

        public void ShowRewarded(Action<bool> done)
        {
            UIRoot.Instance.ShowFakeAd(() => done(true));
        }
    }
}
