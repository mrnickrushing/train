using System.Linq;
using Ironhorse.Core;
using NUnit.Framework;

namespace Ironhorse.Tests
{
    public class HaulageTests
    {
        ManualClock _clock;
        GameSession _s;

        [SetUp]
        public void SetUp()
        {
            _clock = new ManualClock();
            _s = new GameSession(GameSession.NewGame(_clock.Now, 3), _clock);
            _s.State.settings.tutorialDone = true;
        }

        void Level(int l) { _s.State.level = l; _s.Tick(); }
        HaulerState Wagon() { return _s.State.haulers.First(h => h.defId == Haulage.StarterId); }

        [Test]
        public void Content_EveryProjectHasARouteAndEveryHaulerCanReachSomething()
        {
            foreach (var p in DepthContent.Projects) Assert.IsNotNull(Haulage.Route(p.Id), p.Id);
            foreach (var h in Haulage.Haulers)
                Assert.IsTrue(Haulage.Routes.Any(r => Haulage.Distance(h, r) > 0), h.Id);
            Assert.AreEqual(Haulage.Haulers.Count, Haulage.Haulers.Select(h => h.Id).Distinct().Count());
        }

        [Test]
        public void WorkTrain_RunsOnlyToRailProjects()
        {
            Level(4);
            _s.State.coins = 100000;
            Assert.IsTrue(_s.BuyHauler("worktrain").Ok);
            var wt = _s.State.haulers.First(h => h.defId == "worktrain");
            var d = Haulage.Hauler("worktrain");
            Assert.IsTrue(d.OnRail);
            foreach (var r in Haulage.Routes)
                Assert.AreEqual(r.ProjectId == "trestle", Haulage.Distance(d, r) > 0, r.ProjectId);
            _s.State.inventory.Change("timber", 100);
            int load; double secs;
            StringAssert.Contains("No rails", _s.CantHaul(wt, "telegraph", out load, out secs));
            var stage = DepthContent.Project("trestle").Stages[_s.Progress("trestle").stage];
            _s.State.inventory.Change(stage.ResourceId, 500);
            if (_s.IsProjectUnlocked(DepthContent.Project("trestle")))
            {
                Assert.IsNull(_s.CantHaul(wt, "trestle", out load, out secs));
                Assert.AreEqual(2 * Haulage.Route("trestle").RailMeters / 4.5, secs, 0.01);
            }
        }

        [Test]
        public void StarterWagon_ArrivesWithTheFirstProject()
        {
            Assert.AreEqual(0, _s.State.haulers.Count);
            Level(Haulage.UnlockLevel);
            Assert.AreEqual(1, _s.State.haulers.Count);
            Level(Haulage.UnlockLevel + 1);
            Assert.AreEqual(1, _s.State.haulers.Count, "only once");
        }

        [Test]
        public void Delivery_LoadsGoodsTravelsAndCountsOnArrival()
        {
            Level(3);
            _s.State.inventory.Change("timber", 100);
            int timber = _s.Amount("timber");
            var w = Wagon();
            var r = _s.SendHauler(w.uid, "telegraph");
            Assert.IsTrue(r.Ok, r.Message);
            int cap = _s.HaulCapacity(w);
            Assert.AreEqual(timber - cap, _s.Amount("timber"));
            Assert.AreEqual(0, _s.Progress("telegraph").delivered, "not until it gets there");
            Assert.AreEqual(cap, _s.InFlight("telegraph"));
            Assert.IsFalse(_s.SendHauler(w.uid, "telegraph").Ok, "busy");

            _clock.Advance(Haulage.TripSeconds(Haulage.Hauler(w.defId), Haulage.Route("telegraph")) + 1);
            _s.Tick();
            Assert.IsTrue(w.IsIdle);
            Assert.AreEqual(cap, _s.Progress("telegraph").delivered);
            Assert.AreEqual(cap, _s.State.stats.projectDeliveries);
        }

        [Test]
        public void LastBatch_IsNeverOversent()
        {
            Level(3);
            _s.State.inventory.Change("timber", 500);
            _s.State.coins = 100000;
            _s.State.level = 4;
            Assert.IsTrue(_s.BuyHauler("muletrain").Ok);
            var stage = DepthContent.Project("telegraph").Stages[0]; // 40 timber
            _s.Progress("telegraph").delivered = stage.Amount - 25;
            Assert.IsTrue(_s.SendHauler(Wagon().uid, "telegraph").Ok); // 20
            var mule = _s.State.haulers.First(h => h.defId == "muletrain");
            Assert.IsTrue(_s.SendHauler(mule.uid, "telegraph").Ok);
            Assert.AreEqual(5, mule.amount, "only what is still needed");
            Assert.AreEqual(0, _s.StillToSend("telegraph"));
            int dummy; double secs;
            StringAssert.Contains("last batch", _s.CantHaul(new HaulerState { uid = 1, defId = "muletrain" }, "telegraph", out dummy, out secs));

            _clock.Advance(1000);
            _s.Tick();
            Assert.AreEqual(1, _s.Progress("telegraph").stage, "stage filled exactly");
            Assert.AreEqual(0, _s.Progress("telegraph").delivered);
        }

        [Test]
        public void Boats_OnlyReachSitesOnTheWater()
        {
            _s.State.level = 12;
            _s.Tick();
            _s.State.coins = 100000;
            Assert.IsTrue(_s.BuyHauler("sternwheeler").Ok);
            var boat = _s.State.haulers.First(h => h.defId == "sternwheeler");
            _s.State.inventory.Change("timber", 100);
            _s.State.inventory.Change("planks", 100);
            int load; double secs;
            StringAssert.Contains("isn't on the water", _s.CantHaul(boat, "telegraph", out load, out secs));
            Assert.IsNull(_s.CantHaul(boat, "trestle", out load, out secs));
            Assert.Greater(load, 0);
        }

        [Test]
        public void Rush_DeliversAtOnce_AndStageCompletionPays()
        {
            Level(3);
            var stage = DepthContent.Project("telegraph").Stages[0];
            _s.State.inventory.Change(stage.ResourceId, 100);
            _s.Progress("telegraph").delivered = stage.Amount - 10;
            var w = Wagon();
            Assert.IsTrue(_s.SendHauler(w.uid, "telegraph").Ok);
            int coins = _s.Coins;
            _s.State.nuggets = 100;
            Assert.IsTrue(_s.RushHauler(w.uid).Ok);
            Assert.IsTrue(w.IsIdle);
            Assert.AreEqual(1, _s.Progress("telegraph").stage);
            Assert.AreEqual(coins + stage.Coins, _s.Coins);
        }

        [Test]
        public void BuyUpgradeSell()
        {
            Level(3);
            Assert.IsFalse(_s.BuyHauler("muletrain").Ok, "locked until level 4");
            _s.State.level = 4;
            _s.State.coins = 2500;
            Assert.IsTrue(_s.BuyHauler("muletrain").Ok);
            Assert.AreEqual(0, _s.Coins);
            var mule = _s.State.haulers.First(h => h.defId == "muletrain");
            int cap = _s.HaulCapacity(mule);
            _s.State.coins = 100000;
            foreach (var r in _s.HaulerUpgradeResources(mule)) _s.State.inventory.Change(r.id, r.amount);
            Assert.IsTrue(_s.UpgradeHauler(mule.uid).Ok);
            Assert.Greater(_s.HaulCapacity(mule), cap);
            int before = _s.Coins;
            Assert.IsTrue(_s.SellHauler(mule.uid).Ok);
            Assert.AreEqual(before + Haulage.SellPrice(Haulage.Hauler("muletrain"), 2), _s.Coins);
            Assert.IsFalse(_s.SellHauler(Wagon().uid).Ok, "keep one");
        }

        [Test]
        public void OldSave_PastLevelThree_GetsItsWagonOnLoad()
        {
            var st = GameSession.NewGame(_clock.Now, 9);
            st.level = 8;
            st.haulers = null;
            var s = new GameSession(st, _clock);
            Assert.AreEqual(1, s.State.haulers.Count);
        }

        [Test]
        public void NewTerritory_BringsLoadsHome()
        {
            Level(3);
            _s.State.inventory.Change("timber", 100);
            var w = Wagon();
            Assert.IsTrue(_s.SendHauler(w.uid, "telegraph").Ok);
            foreach (var p in DepthContent.Projects) _s.Progress(p.Id).done = true;
            Assert.IsTrue(_s.CharterNewTerritory().Ok);
            Assert.IsTrue(w.IsIdle);
        }

        [Test]
        public void NorthStorylines_WaitForFortResupply()
        {
            _s.State.level = 15;
            var light = DepthContent.Project("lighthouse");
            Assert.IsFalse(_s.NorthOpen);
            Assert.IsFalse(_s.IsProjectUnlocked(light));
            StringAssert.Contains("Fort Calder Resupply", _s.ProjectLockText(light));
            _s.Progress(DepthContent.NorthKey).done = true;
            Assert.IsTrue(_s.NorthOpen);
            Assert.IsTrue(_s.IsProjectUnlocked(light));
        }

        [Test]
        public void Windfall_GivesTheOutpostsGoods()
        {
            _s.State.level = 6;
            var ev = new MapEventState { uid = 999, kind = MapEventKind.Windfall, destinationId = "pinecrest", startsAt = _clock.Now, expiresAt = _clock.Now + 600 };
            _s.State.events.Add(ev);
            int before = _s.Amount("timber");
            Assert.IsTrue(_s.ClaimEvent(999).Ok);
            Assert.AreEqual(before + Hooks.WindfallAmount(6), _s.Amount("timber"));
            Assert.AreEqual(0, _s.State.events.Count);
        }
    }
}
