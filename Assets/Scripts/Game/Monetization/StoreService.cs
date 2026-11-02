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
    }

    /// <summary>
    /// Picks the store implementation:
    ///  • RevenueCat (App Store) when the RevenueCat Unity SDK is installed and the scripting define
    ///    IRONHORSE_REVENUECAT is set (see docs/MONETIZATION.md).
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
                _store = RevenueCatStore.Create();
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
        public const string ApiKey = "appl_REPLACE_ME";

        Purchases _purchases;
        readonly Dictionary<string, string> _prices = new Dictionary<string, string>();
        public bool Ready { get; private set; }

        public static RevenueCatStore Create()
        {
            var s = new RevenueCatStore();
            var go = new GameObject("RevenueCat");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.SetActive(false);
            s._purchases = go.AddComponent<Purchases>();
            s._purchases.revenueCatAPIKeyApple = ApiKey;
            s._purchases.useRuntimeSetup = false;
            go.SetActive(true);
            s.LoadPrices();
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
            _purchases.PurchaseProduct(productId, (id, info, cancelled, error) =>
            {
                if (cancelled) { done(false, "Purchase cancelled."); return; }
                if (error != null) { done(false, error.Message); return; }
                var tx = info.NonSubscriptionTransactions.Where(t => t.ProductIdentifier == productId)
                    .OrderByDescending(t => t.PurchaseDate).FirstOrDefault();
                done(true, tx != null ? tx.TransactionIdentifier : "rc-" + Guid.NewGuid().ToString("N"));
            }, "inapp");
        }

        public void Restore(Action<int> done)
        {
            _purchases.RestorePurchases((info, error) =>
            {
                if (error != null) { done(0); return; }
                done(G.Session.RestoreNonConsumables(info.AllPurchasedProductIdentifiers));
            });
        }
    }
#endif
}
