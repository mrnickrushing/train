using System.Linq;
using Ironhorse.Core;
using NUnit.Framework;

namespace Ironhorse.Tests
{
    public class MonetizationTests
    {
        ManualClock _clock;
        GameSession _s;

        [SetUp]
        public void SetUp()
        {
            _clock = new ManualClock(Balance.SeasonEpoch + 3 * 86400); // a Thursday in season 0
            _s = new GameSession(GameSession.NewGame(_clock.Now, 7), _clock);
        }

        TrainState Starter { get { return _s.State.trains[0]; } }

        [Test]
        public void NewGame_StartsWithFullFuel()
        {
            Assert.AreEqual(_s.MaxFuel, _s.Fuel);
        }

        [Test]
        public void Dispatch_BurnsFuel_ThenRegenerates()
        {
            int before = _s.Fuel;
            int cost = _s.FuelCost(Starter, GameDatabase.Destination("pinecrest"));
            Assert.IsTrue(_s.Dispatch(Starter.uid, "pinecrest").Ok);
            Assert.AreEqual(before - cost, _s.Fuel);
            _clock.Advance(Balance.FuelRegenSeconds * cost + 1);
            Assert.AreEqual(before, _s.Fuel);
            _clock.Advance(100000);
            Assert.AreEqual(_s.MaxFuel, _s.Fuel, "regen stops at max");
        }

        [Test]
        public void Dispatch_BlockedWithoutFuel()
        {
            _s.State.fuel = 1; _s.State.fuelAt = _clock.Now;
            var r = _s.Dispatch(Starter.uid, "pinecrest");
            Assert.IsFalse(r.Ok);
            StringAssert.Contains("fuel", r.Message);
        }

        [Test]
        public void Refill_CostsNuggets()
        {
            _s.State.fuel = 0; _s.State.fuelAt = _clock.Now;
            int nuggets = _s.Nuggets;
            int cost = _s.RefillCost;
            Assert.Greater(cost, 0);
            Assert.IsTrue(_s.RefillFuelWithNuggets().Ok);
            Assert.AreEqual(nuggets - cost, _s.Nuggets);
            Assert.AreEqual(_s.MaxFuel, _s.Fuel);
            Assert.IsFalse(_s.RefillFuelWithNuggets().Ok);
        }

        [Test]
        public void PurchasedFuel_CanOverfillTheTender()
        {
            Assert.IsTrue(_s.GrantPurchase(Catalog.FuelTank, "t1").Ok);
            Assert.AreEqual(Balance.MaxFuel(1, true), _s.MaxFuel);
            Assert.Greater(_s.Fuel, _s.MaxFuel);
        }

        [Test]
        public void GrantPurchase_IsIdempotentPerTransaction()
        {
            int n = _s.Nuggets;
            Assert.IsTrue(_s.GrantPurchase("ironhorse.nuggets_550", "tx-1").Ok);
            Assert.IsFalse(_s.GrantPurchase("ironhorse.nuggets_550", "tx-1").Ok);
            Assert.AreEqual(n + 550, _s.Nuggets);
            Assert.IsTrue(_s.GrantPurchase("ironhorse.nuggets_550", "tx-2").Ok);
            Assert.AreEqual(n + 1100, _s.Nuggets);
        }

        [Test]
        public void StarterPack_OnlyOnce_AndGivesLocomotive()
        {
            Assert.IsTrue(_s.StarterAvailable);
            Assert.IsTrue(_s.GrantPurchase(Catalog.StarterPack, "a").Ok);
            Assert.AreEqual(2, _s.State.trains.Count);
            Assert.IsFalse(_s.StarterAvailable);
            Assert.IsFalse(_s.GrantPurchase(Catalog.StarterPack, "b").Ok);
        }

        [Test]
        public void LocomotiveGrant_WithFullPlatforms_PaysNuggets()
        {
            while (_s.State.trains.Count < _s.FleetCapacity)
                _s.State.trains.Add(new TrainState { uid = 900 + _s.State.trains.Count, defId = "dusty", platform = _s.State.trains.Count });
            int n = _s.Nuggets;
            var r = _s.GrantPurchase("ironhorse.bundle_thunderhead", "x");
            Assert.IsTrue(r.Ok);
            Assert.AreEqual(_s.FleetCapacity, _s.State.trains.Count);
            Assert.Greater(_s.Nuggets, n + 800);
        }

        [Test]
        public void WeeklyOffer_RotatesAndHidesAfterPurchase()
        {
            _s.State.level = 12;
            var thisWeek = _s.WeeklyOffer;
            Assert.IsNotNull(thisWeek);
            Assert.IsTrue(_s.GrantPurchase(thisWeek.ProductId, "w1").Ok);
            Assert.IsNull(_s.WeeklyOffer, "bought this week");
            _clock.Advance(Balance.WeekSeconds);
            Assert.IsNotNull(_s.WeeklyOffer);
            Assert.AreNotEqual(thisWeek.Id, _s.WeeklyOffer.Id);
        }

        [Test]
        public void NuggetOffer_SpendsNuggets()
        {
            _s.State.nuggets = 100;
            Assert.IsTrue(_s.BuyNuggetOffer("n_lumber").Ok);
            Assert.AreEqual(75, _s.Nuggets);
            Assert.AreEqual(90, _s.Amount("timber")); // 10 starting + 80
            _s.State.nuggets = 0;
            Assert.IsFalse(_s.BuyNuggetOffer("n_lumber").Ok);
        }

        [Test]
        public void Pass_TiersFromPlay_FreeClaimable_PremiumNeedsPurchase()
        {
            _s.AddPassXp(Balance.PassXpPerTier * 2);
            Assert.AreEqual(2, _s.PassTier);
            Assert.IsTrue(_s.ClaimPass(1, false).Ok);
            Assert.IsFalse(_s.ClaimPass(1, false).Ok, "already claimed");
            Assert.IsFalse(_s.ClaimPass(3, false).Ok, "tier not reached");
            Assert.IsFalse(_s.ClaimPass(1, true).Ok, "premium locked");
            Assert.IsTrue(_s.GrantPurchase(Catalog.SeasonPass, "p").Ok);
            Assert.IsTrue(_s.ClaimPass(1, true).Ok);
            Assert.AreEqual(2, _s.ClaimablePassRewards); // tier 2 free + premium
        }

        [Test]
        public void Pass_ResetsNextSeason()
        {
            _s.GrantPurchase(Catalog.SeasonPass, "p");
            _s.AddPassXp(5000);
            _clock.Advance(Balance.SeasonSeconds);
            _s.Tick();
            Assert.AreEqual(0, _s.State.passXp);
            Assert.IsFalse(_s.State.passPremium);
        }

        [Test]
        public void Pass_FinalPremiumTier_HasExclusiveLocomotive()
        {
            Assert.AreEqual("sundown", Catalog.PassTiers.Last().Premium.LocomotiveId);
            Assert.AreEqual(Balance.PassTiers, Catalog.PassTiers.Count);
        }

        [Test]
        public void Dispatch_EarnsPassXp()
        {
            _s.Dispatch(Starter.uid, "pinecrest");
            Assert.AreEqual(Balance.PassXpDispatch, _s.State.passXp);
        }

        [Test]
        public void Ads_HaveDailyLimits()
        {
            for (int i = 0; i < Balance.AdDailyLimit(AdReward.Nuggets); i++) Assert.IsTrue(_s.RewardAd(AdReward.Nuggets).Ok);
            Assert.IsFalse(_s.RewardAd(AdReward.Nuggets).Ok);
            _clock.Advance(86400);
            Assert.IsTrue(_s.RewardAd(AdReward.Nuggets).Ok);
        }

        [Test]
        public void AdRush_ShortensTrip()
        {
            _s.Dispatch(Starter.uid, "pinecrest");
            Assert.IsTrue(_s.RewardAd(AdReward.RushTrain, Starter.uid).Ok);
            Assert.IsTrue(_s.HasArrived(Starter));
        }

        [Test]
        public void LevelUp_RefillsFuel()
        {
            _s.State.fuel = 0; _s.State.fuelAt = _clock.Now;
            _s.AddXp(Balance.XpForNextLevel(1));
            Assert.AreEqual(_s.MaxFuel, _s.Fuel);
        }

        [Test]
        public void Catalog_IsConsistent()
        {
            foreach (var o in Catalog.Offers)
            {
                if (o.ProductId != null) Assert.IsNotNull(Catalog.Product(o.ProductId), o.Id);
                else Assert.IsNotNull(o.Grant, o.Id);
            }
            foreach (var p in Catalog.Products)
                if (p.Grant.LocomotiveId != null) Assert.IsNotNull(GameDatabase.Locomotive(p.Grant.LocomotiveId), p.Id);
            Assert.AreEqual(Catalog.Products.Count, Catalog.Products.Select(p => p.Id).Distinct().Count());
        }
    }
}
