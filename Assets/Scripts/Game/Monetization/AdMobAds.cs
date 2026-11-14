#if UNITY_IOS && !UNITY_EDITOR
using System;
using GoogleMobileAds.Api;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>
    /// Rewarded adverts from Google AdMob. Only ever shown because the player tapped a Watch button. Requests are
    /// non-personalised ("npa"), so nothing is tracked and iOS never asks for tracking permission. The SDK starts the
    /// first time a screen asks whether an advert is available, not at launch.
    /// </summary>
    public sealed class AdMobAds : IAds
    {
        // The rewarded unit from the AdMob account. Until it exists this is Google's public test unit, which only
        // ever serves test adverts.
        const string RewardedUnit = "ca-app-pub-3940256099942544/1712485313";

        RewardedAd _ad;
        bool _loading;
        float _retryAt;

        public AdMobAds()
        {
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            MobileAds.SetiOSAppPauseOnBackground(true);
            MobileAds.SetRequestConfiguration(new RequestConfiguration { MaxAdContentRating = MaxAdContentRating.G });      // family-safe adverts only
            MobileAds.Initialize(_ => Load());
        }

        public bool Available
        {
            get
            {
                if (_ad != null && _ad.CanShowAd()) return true;
                // A load that failed is tried again a minute later, the next time someone asks.
                if (_ad == null && !_loading && _retryAt > 0 && Time.unscaledTime >= _retryAt) Load();
                return false;
            }
        }

        void Load()
        {
            if (_loading) return;
            _loading = true; _retryAt = 0;
            var request = new AdRequest();
            request.Extras.Add("npa", "1");
            RewardedAd.Load(RewardedUnit, request, (ad, error) =>
            {
                _loading = false;
                if (error != null || ad == null) { _retryAt = Time.unscaledTime + 60; return; }
                _ad = ad;
            });
        }

        public void ShowRewarded(Action<bool> done)
        {
            if (_ad == null || !_ad.CanShowAd()) { done(false); return; }
            var ad = _ad; _ad = null;
            bool earned = false, told = false;
            Action finish = () => { if (told) return; told = true; ad.Destroy(); Load(); done(earned); };
            ad.OnAdFullScreenContentClosed += finish;
            ad.OnAdFullScreenContentFailed += _ => finish();
            ad.Show(_ => earned = true);
        }
    }
}
#endif
