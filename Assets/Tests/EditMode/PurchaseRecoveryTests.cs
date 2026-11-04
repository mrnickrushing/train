using System.Collections.Generic;
using System.Linq;
using Ironhorse.Core;
using NUnit.Framework;

namespace Ironhorse.Tests
{
    /// <summary>A paid purchase is delivered exactly once, even when the app dies between the charge and the save.</summary>
    public class PurchaseRecoveryTests
    {
        const string Nuggets = "ironhorse.nuggets_100";
        ManualClock _clock;
        GameSession _s;

        [SetUp]
        public void SetUp()
        {
            _clock = new ManualClock();
            _s = new GameSession(GameSession.NewGame(_clock.Now, 5), _clock);
        }

        StoreReceipt Receipt(string tx, double secondsAfterNow, string product = Nuggets) { return new StoreReceipt(product, tx, _clock.Now + secondsAfterNow); }

        /// <summary>What the game finds after a crash: the state as last saved, in a new session.</summary>
        GameSession Relaunch() { return new GameSession(_s.State, _clock); }

        [Test]
        public void CrashAfterCharge_IsDeliveredOnNextLaunch_Once()
        {
            int n = _s.Nuggets;
            _s.BeginPurchase(Nuggets); // saved before the purchase sheet opens
            _clock.Advance(20);        // the player pays; the app dies before the reward is saved
            var s = Relaunch();
            Assert.IsTrue(s.HasPendingPurchases);

            var got = s.ReconcilePurchases(new[] { Receipt("2000000123", -5) });
            Assert.AreEqual(1, got.Count);
            Assert.Greater(s.Nuggets, n);
            Assert.IsFalse(s.HasPendingPurchases);

            int after = s.Nuggets;
            Assert.IsEmpty(s.ReconcilePurchases(new[] { Receipt("2000000123", -5) }));
            Assert.IsFalse(s.GrantPurchase(Nuggets, "2000000123").Ok, "a late store callback for the same transaction");
            Assert.AreEqual(after, s.Nuggets);
        }

        [Test]
        public void NormalPurchase_ClearsTheMarker_AndReconcileGivesNothingMore()
        {
            _s.BeginPurchase(Nuggets);
            _clock.Advance(10);
            Assert.IsTrue(_s.GrantPurchase(Nuggets, "tx-1").Ok);
            int after = _s.Nuggets;
            Assert.IsFalse(_s.HasPendingPurchases);
            // The store may list the same purchase under another id: nothing is pending, so nothing is granted.
            Assert.IsEmpty(_s.ReconcilePurchases(new[] { Receipt("rc-other-id", -2) }));
            Assert.AreEqual(after, _s.Nuggets);
        }

        [Test]
        public void Cancelled_NothingRecovered()
        {
            _s.BeginPurchase(Nuggets);
            _s.EndPurchase(Nuggets);
            int n = _s.Nuggets;
            Assert.IsEmpty(_s.ReconcilePurchases(new[] { Receipt("tx-1", 5) }));
            Assert.AreEqual(n, _s.Nuggets);
        }

        [Test]
        public void OnlyPurchasesMadeAfterTheMarker_AndOnlyThisProduct()
        {
            _s.BeginPurchase(Nuggets);
            int n = _s.Nuggets;
            var older = Receipt("tx-old", -(GameSession.PurchaseClockSlack + 120)); // bought earlier (or on another device)
            var otherProduct = Receipt("tx-x", 5, "ironhorse.nuggets_550");
            Assert.IsEmpty(_s.ReconcilePurchases(new[] { older, otherProduct }));
            Assert.AreEqual(n, _s.Nuggets);
            Assert.IsTrue(_s.HasPendingPurchases, "still watching: the store may not have the purchase yet");
        }

        [Test]
        public void NoMarker_PurchasesFromOtherDevicesAreNotDelivered()
        {
            int n = _s.Nuggets;
            Assert.IsEmpty(_s.ReconcilePurchases(new[] { Receipt("tx-a", 0), Receipt("tx-b", 30) }));
            Assert.AreEqual(n, _s.Nuggets);
        }

        [Test]
        public void SeveralMatches_DeliverOnePurchase()
        {
            _s.BeginPurchase(Nuggets);
            int n = _s.Nuggets;
            _clock.Advance(60);
            var got = _s.ReconcilePurchases(new[] { Receipt("tx-1", -40), Receipt("tx-2", -10) });
            Assert.AreEqual(1, got.Count);
            int one = _s.Nuggets - n;
            Assert.Greater(one, 0);
            CollectionAssert.Contains(_s.State.transactions, "tx-2", "the newest transaction is the one delivered");
        }

        [Test]
        public void AlreadyDeliveredTransaction_IsSkipped()
        {
            Assert.IsTrue(_s.GrantPurchase(Nuggets, "tx-1").Ok);
            _s.BeginPurchase(Nuggets);
            int n = _s.Nuggets;
            Assert.IsEmpty(_s.ReconcilePurchases(new[] { Receipt("tx-1", 1) }));
            Assert.AreEqual(n, _s.Nuggets);
        }

        [Test]
        public void StaleMarkers_Expire()
        {
            _s.BeginPurchase(Nuggets);
            _clock.Advance(GameSession.PendingPurchaseSeconds + 1);
            Assert.IsEmpty(_s.ReconcilePurchases(new StoreReceipt[0]));
            Assert.IsFalse(_s.HasPendingPurchases);
        }

        [Test]
        public void DamagedMarkers_AreDroppedOnLoad()
        {
            _s.State.pendingPurchases = null;
            var s = Relaunch();
            Assert.IsNotNull(s.State.pendingPurchases);
            s.State.pendingPurchases.Add(null);
            s.State.pendingPurchases.Add(new PendingPurchase { productId = "ironhorse.removed_pack", startedAt = _clock.Now });
            s.State.pendingPurchases.Add(new PendingPurchase { productId = Nuggets, startedAt = _clock.Now });
            s = new GameSession(s.State, _clock);
            Assert.AreEqual(1, s.State.pendingPurchases.Count);
        }

        [Test]
        public void TransactionLedger_IsNeverTrimmed()
        {
            Assert.IsTrue(_s.GrantPurchase(Nuggets, "first").Ok);
            for (int i = 0; i < 2500; i++) _s.State.transactions.Add("t" + i);
            var s = Relaunch();
            Assert.IsFalse(s.GrantPurchase(Nuggets, "first").Ok);
            Assert.IsTrue(s.GrantPurchase(Nuggets, "new").Ok);
            CollectionAssert.Contains(s.State.transactions, "first");
        }
    }
}
