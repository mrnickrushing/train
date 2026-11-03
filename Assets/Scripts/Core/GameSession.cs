using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironhorse.Core
{
    public interface IClock { double Now { get; } }

    /// <summary>Clock you drive by hand — used by tests and by the offline simulator.</summary>
    public sealed class ManualClock : IClock
    {
        public double Now { get; set; }
        public ManualClock(double start = 1_000_000) { Now = start; }
        public void Advance(double seconds) { Now += seconds; }
    }

    public readonly struct ActionResult
    {
        public readonly bool Ok;
        public readonly string Message;
        ActionResult(bool ok, string msg) { Ok = ok; Message = msg; }
        public static ActionResult Success(string msg = null) { return new ActionResult(true, msg); }
        public static ActionResult Fail(string msg) { return new ActionResult(false, msg); }
        public static implicit operator bool(ActionResult r) { return r.Ok; }
        public override string ToString() { return (Ok ? "OK" : "FAIL") + (Message != null ? ": " + Message : ""); }
    }

    /// <summary>
    /// The whole simulation. Pure C# with no engine dependencies: the Unity layer only reads state,
    /// calls actions and listens to events. All timers are absolute timestamps, so progress made
    /// while the app is closed is applied automatically on the next Tick.
    /// </summary>
    public sealed partial class GameSession
    {
        public GameState State { get; private set; }
        readonly IClock _clock;

        /// <summary>Raised after any state mutation.</summary>
        public event Action Changed;
        public event Action<int> LeveledUp;
        public event Action<string> Notify;
        public event Action<TrainState> TrainArrived;
        public event Action<TrainState> TrainDeparted;
        public event Action<BuildingState> BuildingFinished;
        public event Action<BuildingState> ProductionFinished;
        public event Action<ContractState> ContractCompleted;
        /// <summary>A locomotive joined the fleet (bought, assembled from blueprints, or from a purchase).</summary>
        public event Action<TrainState> LocomotiveAdded;
        void RaiseLocomotiveAdded(TrainState t) { LocomotiveAdded?.Invoke(t); }

        readonly HashSet<int> _arrivalSeen = new HashSet<int>();
        readonly HashSet<int> _constructionSeen = new HashSet<int>();
        readonly HashSet<int> _productionSeen = new HashSet<int>();
        readonly List<CurioDef> _awayCurios = new List<CurioDef>();

        public double Now { get { return _clock.Now; } }

        public GameSession(GameState state, IClock clock)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            Repair();
            // Anything already finished when the save was loaded is not "news".
            foreach (var t in State.trains)
                if (!t.IsIdle && Now >= t.arrivesAt)
                {
                    _arrivalSeen.Add(t.uid);
                    var found = CollectCurio(t, false); // finds made while the app was closed
                    if (found != null) _awayCurios.Add(found);
                }
            foreach (var b in State.buildings)
            {
                if (Now >= b.readyAt) _constructionSeen.Add(b.uid);
                if (IsProductionDone(b)) _productionSeen.Add(b.uid);
            }
            FillContracts(force: State.contracts.Count == 0 && State.nextContractAt <= 0);
            BuildAwayReport();
        }

        public static GameState NewGame(double now, int seed)
        {
            var s = new GameState
            {
                coins = Balance.StartCoins,
                nuggets = Balance.StartNuggets,
                createdAt = now,
                lastSavedAt = now,
                rngSeed = seed,
                unlockedPlots = Balance.StartPlots,
            };
            var starter = new TrainState { uid = s.nextUid++, defId = "dusty", platform = 0 };
            s.trains.Add(starter);
            s.wagons.Add(new WagonState { uid = s.nextUid++, defId = "flatcar", trainUid = starter.uid, slot = 0 });
            s.wagons.Add(new WagonState { uid = s.nextUid++, defId = "hopper", trainUid = starter.uid, slot = 1 });
            s.buildings.Add(new BuildingState { uid = s.nextUid++, defId = "cabin", plot = 0, builtAt = now, readyAt = now, lastCollectAt = now });
            s.buildings.Add(new BuildingState { uid = s.nextUid++, defId = "cabin", plot = 1, builtAt = now, readyAt = now, lastCollectAt = now });
            s.inventory.Change("timber", 10);
            s.inventory.Change("rivets", 12);
            return s;
        }

        void Repair()
        {
            if (State.inventory == null) State.inventory = new List<ResourceAmount>();
            if (State.trains == null) State.trains = new List<TrainState>();
            if (State.wagons == null) State.wagons = new List<WagonState>();
            if (State.buildings == null) State.buildings = new List<BuildingState>();
            if (State.contracts == null) State.contracts = new List<ContractState>();
            if (State.goals == null) State.goals = new List<GoalProgress>();
            if (State.stats == null) State.stats = new Stats();
            if (State.stats.collected == null) State.stats.collected = new List<ResourceAmount>();
            if (State.stats.produced == null) State.stats.produced = new List<ResourceAmount>();
            if (State.settings == null) State.settings = new Settings();
            if (State.unlockedPlots < Balance.StartPlots) State.unlockedPlots = Balance.StartPlots;
            if (State.level < 1) State.level = 1;
            if (State.warehouseLevel < 1) State.warehouseLevel = 1;
            foreach (var t in State.trains)
            {
                if (t.destinationId == null) t.destinationId = "";
                if (t.cargoId == null) t.cargoId = "";
            }
            State.trains.RemoveAll(t => GameDatabase.Locomotive(t.defId) == null);
            State.wagons.RemoveAll(w => GameDatabase.Wagon(w.defId) == null);
            State.buildings.RemoveAll(b => GameDatabase.Building(b.defId) == null);
            RepairMonetization();
            RepairDepth();
            RepairHooks();
            RepairFrontier();
        }

        // ---------------------------------------------------------------- Tick

        /// <summary>Call regularly (e.g. 4x per second). Emits completion events and refills contracts.</summary>
        public void Tick()
        {
            double now = Now;
            bool changed = false;
            foreach (var t in State.trains)
            {
                if (!t.IsIdle && now >= t.arrivesAt && _arrivalSeen.Add(t.uid))
                {
                    var d = GameDatabase.Destination(t.destinationId);
                    var res = GameDatabase.Resource(t.cargoId);
                    CollectCurio(t, true);
                    if (t.robbed > 0 && res != null) Notify?.Invoke("Bandits held up " + Name(t) + " and made off with " + t.robbed + " " + res.Name + "!");
                    else Notify?.Invoke(Name(t) + " is back from " + (d != null ? d.Name : "the line") + (t.crate ? " with a mystery crate!" : "!"));
                    TrainArrived?.Invoke(t);
                    changed = true;
                }
            }
            foreach (var b in State.buildings)
            {
                if (now >= b.readyAt && _constructionSeen.Add(b.uid))
                {
                    if (b.builtAt < b.readyAt) Notify?.Invoke(GameDatabase.Building(b.defId).Name + " is complete!");
                    BuildingFinished?.Invoke(b);
                    changed = true;
                }
                if (IsProductionDone(b) && _productionSeen.Add(b.uid))
                {
                    ProductionFinished?.Invoke(b);
                    changed = true;
                }
            }
            if (FillContracts(force: false)) changed = true;
            if (TickHooks()) changed = true;
            if (TickFrontier()) changed = true;
            int season = State.passSeason;
            RollSeason();
            if (season != State.passSeason) changed = true;
            if (changed) RaiseChanged();
        }

        void RaiseChanged() { Changed?.Invoke(); }

        // ---------------------------------------------------------------- Economy helpers

        public int Coins { get { return State.coins; } }
        public int Nuggets { get { return State.nuggets; } }
        public int Level { get { return State.level; } }
        public bool IsGoldenAge { get { return State.level >= Balance.GoldenAgeLevel; } }

        public int Amount(string resourceId) { return State.inventory.Get(resourceId); }
        public int WarehouseCapacity { get { return Balance.WarehouseCapacity(State.warehouseLevel); } }
        /// <summary>Workshop parts don't take warehouse space.</summary>
        public int WarehouseUsed
        {
            get
            {
                int t = 0;
                foreach (var r in State.inventory) { var d = GameDatabase.Resource(r.id); if (d == null || !d.IsPart) t += r.amount; }
                return t;
            }
        }
        public int WarehouseFree { get { return Math.Max(0, WarehouseCapacity - WarehouseUsed); } }

        public bool CanAfford(int coins, IEnumerable<ResourceAmount> resources, int nuggets = 0)
        {
            if (State.coins < coins || State.nuggets < nuggets) return false;
            if (resources != null)
                foreach (var r in resources) if (Amount(r.id) < r.amount) return false;
            return true;
        }

        string MissingText(int coins, IEnumerable<ResourceAmount> resources, int nuggets = 0)
        {
            var parts = new List<string>();
            if (State.coins < coins) parts.Add((coins - State.coins) + " coins");
            if (State.nuggets < nuggets) parts.Add((nuggets - State.nuggets) + " nuggets");
            if (resources != null)
                foreach (var r in resources)
                {
                    int have = Amount(r.id);
                    if (have < r.amount) parts.Add((r.amount - have) + " " + GameDatabase.Resource(r.id).Name);
                }
            return "Need " + string.Join(", ", parts);
        }

        void Pay(int coins, IEnumerable<ResourceAmount> resources, int nuggets = 0)
        {
            State.coins -= coins;
            State.nuggets -= nuggets;
            if (resources != null) foreach (var r in resources) State.inventory.Change(r.id, -r.amount);
        }

        void EarnCoins(int amount)
        {
            State.coins += amount;
            State.stats.coinsEarned += amount;
        }

        public void AddXp(int amount)
        {
            amount = (int)Math.Round(amount * (1 + CivicBonus(CivicEffect.Xp)));
            State.xp += amount;
            while (State.xp >= Balance.XpForNextLevel(State.level))
            {
                State.xp -= Balance.XpForNextLevel(State.level);
                State.level++;
                // Level-up perk: the tender is refilled.
                if (State.fuel < MaxFuel) { State.fuel = MaxFuel; State.fuelAt = Now; }
                State.nuggets += Balance.LevelUpNuggets(State.level);
                EarnCoins(Balance.LevelUpCoins(State.level));
                LeveledUp?.Invoke(State.level);
            }
        }

        public float LevelProgress { get { return (float)State.xp / Balance.XpForNextLevel(State.level); } }

        int NewUid() { return State.nextUid++; }

        // Deterministic RNG so the same seed always yields the same contracts.
        Random NextRng() { return new Random(unchecked(State.rngSeed * 486187739 + (State.rngCounter++) * 7919)); }

        // ---------------------------------------------------------------- Civic effects

        public float CivicBonus(CivicEffect fx)
        {
            float total = 0;
            foreach (var b in State.buildings)
            {
                if (!IsReady(b)) continue;
                var d = GameDatabase.Building(b.defId);
                if (d.Effect == fx) total += d.EffectValue * (fx == CivicEffect.ContractSlot || fx == CivicEffect.CoinCap ? 1 : Balance.BuildingLevelMultiplier(b.level));
            }
            return total + ProjectPerk(fx) + AlbumPerk(fx) + ShelfPerk(fx) + CharterPerk(fx);
        }

        public int Population
        {
            get
            {
                int p = 0;
                foreach (var b in State.buildings)
                    if (IsReady(b)) p += (int)Math.Round(GameDatabase.Building(b.defId).Population * Balance.BuildingLevelMultiplier(b.level));
                return p;
            }
        }

        // ---------------------------------------------------------------- Goals

        public GoalDef CurrentGoal
        {
            get
            {
                foreach (var g in GameDatabase.Goals)
                    if (!State.goals.Any(p => p.goalId == g.Id && p.claimed)) return g;
                return null;
            }
        }

        public int GoalProgressValue(GoalDef g)
        {
            switch (g.Type)
            {
                case GoalType.DispatchTrains: return State.stats.dispatches;
                case GoalType.CollectResource: return State.stats.collected.Get(g.Target);
                case GoalType.ProduceResource: return State.stats.produced.Get(g.Target);
                case GoalType.ConstructBuilding: return State.buildings.Count(b => b.defId == g.Target);
                case GoalType.ReachLevel: return State.level;
                case GoalType.CompleteContracts: return State.stats.contractsCompleted;
                case GoalType.OwnLocomotives: return State.trains.Count;
                case GoalType.ReachPopulation: return Population;
            }
            return 0;
        }

        public bool IsGoalComplete(GoalDef g) { return GoalProgressValue(g) >= g.Count; }

        public ActionResult ClaimGoal()
        {
            var g = CurrentGoal;
            if (g == null) return ActionResult.Fail("No goal");
            if (!IsGoalComplete(g)) return ActionResult.Fail("Goal not complete yet");
            State.goals.Add(new GoalProgress { goalId = g.Id, claimed = true });
            EarnCoins(g.RewardCoins);
            State.nuggets += g.RewardNuggets;
            AddXp(g.RewardXp);
            RaiseChanged();
            return ActionResult.Success("Goal complete: +" + g.RewardCoins + " coins");
        }

        // ---------------------------------------------------------------- Rush

        public ActionResult SpendNuggets(int amount)
        {
            if (State.nuggets < amount) return ActionResult.Fail("Need " + (amount - State.nuggets) + " more nuggets");
            State.nuggets -= amount;
            return ActionResult.Success();
        }
    }
}
