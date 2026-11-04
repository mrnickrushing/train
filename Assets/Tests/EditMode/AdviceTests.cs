using System.Linq;
using Ironhorse.Core;
using NUnit.Framework;

namespace Ironhorse.Tests
{
    /// <summary>The HUD's next-step button names the most useful thing to do now.</summary>
    public class AdviceTests
    {
        ManualClock _clock;
        GameSession _s;

        [SetUp]
        public void SetUp()
        {
            _clock = new ManualClock();
            _s = new GameSession(GameSession.NewGame(_clock.Now, 3), _clock);
            _s.State.settings.tutorialDone = true;
            _s.State.contracts.Clear();
            _s.State.paydayLastDay = int.MaxValue / 2; // payday out of the way unless a test wants it
            _s.State.dailyTasks.Clear();
        }

        TrainState Dusty { get { return _s.State.trains[0]; } }

        void SendDustyHome()
        {
            Assert.IsTrue(_s.Dispatch(Dusty.uid, "pinecrest").Ok);
            _clock.Advance(100000);
            _s.Tick();
        }

        [Test]
        public void ShortName_DropsTheWheelArrangement()
        {
            Assert.AreEqual("Dusty", GameSession.ShortName(new TrainState { defId = "dusty" }));
            Assert.AreEqual("Prairie Lark", GameSession.ShortName(new TrainState { defId = "lark" }));
            Assert.AreEqual("Silver Arrow", GameSession.ShortName(new TrainState { defId = "silverarrow" }));
            Assert.AreEqual("Old 9 4-4-0", GameSession.ShortName(new TrainState { defId = "lark", nickname = "Old 9 4-4-0" }), "nicknames are the player's");
        }

        [Test]
        public void IdleTrain_IsSentOut()
        {
            var a = _s.NextAction();
            Assert.AreEqual(AdviceKind.Dispatch, a.Kind);
            Assert.AreEqual(Dusty.uid, a.Uid);
            StringAssert.StartsWith("Send ", a.Text);
        }

        [Test]
        public void TrainOnTheLine_NothingToDo()
        {
            Assert.IsTrue(_s.Dispatch(Dusty.uid, "pinecrest").Ok);
            Assert.IsNull(_s.NextAction());
        }

        [Test]
        public void TrainHome_UnloadComesFirst()
        {
            SendDustyHome();
            _s.State.paydayLastDay = -1;
            var a = _s.NextAction();
            Assert.AreEqual(AdviceKind.Unload, a.Kind);
            Assert.AreEqual(Dusty.uid, a.Uid);
            StringAssert.Contains("+" + Dusty.cargoAmount, a.Detail);
            Assert.IsTrue(_s.Unload(a.Uid).Ok);
            Assert.AreEqual(AdviceKind.Payday, _s.NextAction().Kind);
        }

        [Test]
        public void FullWarehouse_SaysSoInsteadOfUnload()
        {
            SendDustyHome();
            _s.State.inventory.Change("timber", _s.WarehouseFree);
            Assert.AreEqual(0, _s.WarehouseFree);
            Assert.AreEqual(AdviceKind.WarehouseFull, _s.NextAction().Kind);
        }

        [Test]
        public void FillableOrder_BeatsPaydayAndDispatch_BestPayFirst()
        {
            _s.State.paydayLastDay = -1;
            _s.State.inventory.Change("timber", 20);
            _s.State.contracts.Add(new ContractState { uid = 801, client = "Mayor Quill", rewardCoins = 100, requirements = { new ResourceAmount("timber", 5) } });
            _s.State.contracts.Add(new ContractState { uid = 802, client = "Doc Hale", rewardCoins = 300, requirements = { new ResourceAmount("timber", 5) } });
            _s.State.contracts.Add(new ContractState { uid = 803, client = "Ma Kettle", rewardCoins = 900, requirements = { new ResourceAmount("timber", 50) } });
            var a = _s.NextAction();
            Assert.AreEqual(AdviceKind.Deliver, a.Kind);
            Assert.AreEqual(802, a.Uid, "the best-paying order the player can actually fill");
        }

        [Test]
        public void Payday_WaitsForTheTutorial()
        {
            _s.State.paydayLastDay = -1;
            _s.State.settings.tutorialDone = false;
            _s.State.level = 1;
            Assert.AreEqual(AdviceKind.Dispatch, _s.NextAction().Kind);
            _s.State.settings.tutorialDone = true;
            Assert.AreEqual(AdviceKind.Payday, _s.NextAction().Kind);
        }

        [Test]
        public void FinishedGoods_AreCollected()
        {
            _s.State.buildings.Add(new BuildingState { uid = 901, defId = "sawmill", readyAt = _clock.Now - 10, queue = { new ProductionJob { startsAt = _clock.Now - 100, endsAt = _clock.Now - 1 } } });
            var a = _s.NextAction();
            Assert.AreEqual(AdviceKind.CollectGoods, a.Kind);
            Assert.AreEqual(901, a.Uid);
            Assert.AreEqual("planks", a.Icon);
        }

        [Test]
        public void OpenPlatform_IsSuggestedWhenAffordable()
        {
            Assert.IsTrue(_s.Dispatch(Dusty.uid, "pinecrest").Ok);
            _s.State.level = _s.NextPlatformLevel;
            _s.State.coins = _s.NextPlatformCoins;
            foreach (var g in _s.NextPlatformResources) _s.State.inventory.Change(g.id, g.amount);
            var a = _s.NextAction();
            Assert.AreEqual(AdviceKind.LayPlatform, a.Kind);
            StringAssert.Contains("platform 4", a.Text);
        }
    }
}
