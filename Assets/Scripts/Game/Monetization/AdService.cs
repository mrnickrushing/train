using System;
using Ironhorse.Core;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>Rewarded-video abstraction. Plug an ad network (Unity LevelPlay, AdMob) in here.</summary>
    public interface IAds
    {
        bool Available { get; }
        /// <summary>Shows a rewarded ad; calls back true only if it was watched to completion.</summary>
        void ShowRewarded(Action<bool> done);
    }

    public static class Ads
    {
        static IAds _ads;
        public static IAds Current { get { return _ads ?? (_ads = new TestAds()); } }

        /// <summary>Shows an ad and, if completed, grants the reward through the simulation.</summary>
        public static void Watch(AdReward reward, int trainUid = 0, Action<ActionResult> done = null)
        {
            if (G.Session.AdsLeft(reward) <= 0) { done?.Invoke(ActionResult.Fail("No more of these today. Come back tomorrow!")); return; }
            if (!Current.Available) { done?.Invoke(ActionResult.Fail("No video available right now.")); return; }
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

    /// <summary>Simulated 3-second "ad" in the editor and development builds; unavailable in release.</summary>
    public sealed class TestAds : IAds
    {
        public bool Available { get { return Application.isEditor || Debug.isDebugBuild; } }

        public void ShowRewarded(Action<bool> done)
        {
            UIRoot.Instance.ShowFakeAd(() => done(true));
        }
    }
}
