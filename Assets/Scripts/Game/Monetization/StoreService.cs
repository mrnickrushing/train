using System;
using System.Collections.Generic;
using System.Linq;
using Ironhorse.Core;
using UnityEngine;

namespace Ironhorse.Game
{
    /// <summary>Real-money store abstraction. The game only talks to this interface.</summary>
    public interface IStore
    {
        bool Ready { get; }
        /// <summary>Localized price ("$4.99", "4,99 €") or null if unknown.</summary>
        string Price(string productId);
        void Purchase(string productId, Action<bool, string> done);
        void Restore(Action<int> done);
        /// <summary>Ties purchases to a signed-in account (Sign in with Apple user id), or null to go anonymous.</summary>
        void Identify(string appUserId);
    }

    /// <summary>
    /// Picks the store implementation:
    ///  • RevenueCat (App Store) on device when the RevenueCat Unity SDK is installed (which defines
    ///    IRONHORSE_REVENUECAT) and RevenueCatStore.ApiKey is set (see docs/MONETIZATION.md).
    ///  • TestStore otherwise: instant fake purchases in the editor and development builds only.
    ///    In a release build without RevenueCat, purchases are disabled, never free.
    /// </summary>
    public static class Store
    {
        static IStore _store;
        public static IStore Current
        {
            get
            {
                if (_store != null) return _store;
#if IRONHORSE_REVENUECAT
                // The RevenueCat package is installed (Ironhorse.Game.asmdef versionDefines); it's used once the
                // project's Apple SDK key is filled in, and on device only (the Editor has no StoreKit).
                _store = RevenueCatStore.Configured && !Application.isEditor ? RevenueCatStore.Create() : (IStore)new TestStore();
#else
                _store = new TestStore();
#endif
                return _store;
            }
        }

        public static string PriceOf(string productId)
        {
            var p = Current.Price(productId);
            if (!string.IsNullOrEmpty(p)) return p;
            var def = Catalog.Product(productId);
            return def != null ? def.FallbackPrice : "?";
        }

        /// <summary>Buys a product and grants it through the simulation (idempotent per transaction).</summary>
        public static void Buy(string productId, Action<ActionResult> done = null)
        {
            Current.Purchase(productId, (ok, txOrError) =>
            {
                ActionResult r = ok ? G.Session.GrantPurchase(productId, txOrError) : ActionResult.Fail(txOrError);
                if (ok && r.Ok) GameRoot.Instance.Save(); // persist immediately after a paid grant
                done?.Invoke(r);
            });
        }
    }

    public sealed class TestStore : IStore
    {
        static bool Allowed { get { return Application.isEditor || Debug.isDebugBuild; } }
        public bool Ready { get { return Allowed; } }
        public string Price(string productId) { return null; }

        public void Purchase(string productId, Action<bool, string> done)
        {
            if (!Allowed) { done(false, "The store isn't available yet."); return; }
            Debug.Log("[TestStore] Simulated purchase of " + productId);
            done(true, "test-" + Guid.NewGuid().ToString("N"));
        }

        public void Restore(Action<int> done) { done(0); }
        public void Identify(string appUserId) { }
    }

#if IRONHORSE_REVENUECAT
    /// <summary>
    /// App Store purchases via RevenueCat. Requires the RevenueCat Unity SDK (Purchases) and your
    /// Apple public SDK key in <see cref="ApiKey"/>. Product ids must match Catalog.Products and
    /// App Store Connect. Compiled only when IRONHORSE_REVENUECAT is defined.
    /// </summary>
    public sealed class RevenueCatStore : IStore
    {
        /// <summary>RevenueCat → Project → API keys → Apple App Store public key (appl_...).</summary>
        public const string ApiKey = "appl_hOYAoqSxxCnnylmiftOJFDxdFmi"; // public by design (ships in the app)
        public static bool Configured { get { return ApiKey.StartsWith("appl_") && !ApiKey.Contains("REPLACE"); } }

        Purchases _purchases;
        bool _booted; // Purchases configures itself in its Start(); calls before that hit an unset native wrapper
        string _pendingUser; bool _hasPendingUser; // Identify() before boot
        readonly Dictionary<string, string> _prices = new Dictionary<string, string>();
        public bool Ready { get; private set; }

        sealed class Boot : MonoBehaviour
        {
            public RevenueCatStore Store;
            System.Collections.IEnumerator Start()
            {
                yield return null;
                Store._booted = true;
                if (Store._hasPendingUser) Store.Identify(Store._pendingUser);
                Store.LoadPrices();
            }
        }

        public static RevenueCatStore Create()
        {
            var s = new RevenueCatStore();
            var go = new GameObject("RevenueCat");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.SetActive(false);
            s._purchases = go.AddComponent<Purchases>();
            s._purchases.revenueCatAPIKeyApple = ApiKey;
            s._purchases.useRuntimeSetup = false;
            s._purchases.productIdentifiers = Catalog.Products.Select(p => p.Id).ToArray();
            go.AddComponent<Boot>().Store = s; // load prices one frame after Purchases.Start has configured the SDK
            go.SetActive(true);
            return s;
        }

        void LoadPrices()
        {
            var ids = Catalog.Products.Select(p => p.Id).ToArray();
            _purchases.GetProducts(ids, (products, error) =>
            {
                if (error != null) { Debug.LogWarning("[RevenueCat] " + error.Message); return; }
                foreach (var p in products) _prices[p.Identifier] = p.PriceString;
                Ready = true;
            }, "inapp");
        }

        public string Price(string productId) { string p; return _prices.TryGetValue(productId, out p) ? p : null; }

        public void Purchase(string productId, Action<bool, string> done)
        {
            if (!_booted) { done(false, "The store is still loading. Try again in a moment."); return; }
            _purchases.PurchaseProduct(productId, result =>
            {
                if (result.UserCancelled) { done(false, "Purchase cancelled."); return; }
                if (result.Error != null) { done(false, result.Error.Message); return; }
                var tx = result.StoreTransaction;
                done(true, tx != null ? tx.TransactionIdentifier : "rc-" + Guid.NewGuid().ToString("N"));
            }, "inapp");
        }

        public void Identify(string appUserId)
        {
            if (!_booted) { _pendingUser = appUserId; _hasPendingUser = true; return; }
            _hasPendingUser = false;
            if (!string.IsNullOrEmpty(appUserId))
                _purchases.LogIn(appUserId, (info, created, error) =>
                {
                    if (error != null) { Debug.LogWarning("[RevenueCat] log in: " + error.Message); return; }
                    G.Session.RestoreNonConsumables(info.AllPurchasedProductIdentifiers); // one-time items follow the account
                });
            else
                _purchases.LogOut((info, error) => { if (error != null) Debug.LogWarning("[RevenueCat] log out: " + error.Message); });
        }

        public void Restore(Action<int> done)
        {
            if (!_booted) { done(0); return; }
            _purchases.RestorePurchases((info, error) =>
            {
                if (error != null) { done(0); return; }
                done(G.Session.RestoreNonConsumables(info.AllPurchasedProductIdentifiers));
            });
        }
    }
#endif
}
