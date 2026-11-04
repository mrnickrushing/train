using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironhorse.Core
{
    public sealed partial class GameSession
    {
        public event Action<ProjectDef> ProjectCompleted;
        public event Action<TaskSetDef> TaskSetCompleted;

        void RepairDepth()
        {
            if (State.blueprints == null) State.blueprints = new List<ResourceAmount>();
            if (State.projects == null) State.projects = new List<ProjectProgress>();
            if (State.taskBaselines == null) State.taskBaselines = new List<int>();
            if (State.taskFallback == null) State.taskFallback = new List<bool>();
            foreach (var c in State.contracts) if (c.rewardParts == null) c.rewardParts = new List<ResourceAmount>();
            MigrateProduction();
            foreach (var p in DepthContent.Projects)
                if (!State.projects.Any(x => x.id == p.Id)) State.projects.Add(new ProjectProgress { id = p.Id });
            State.projects.RemoveAll(x => DepthContent.Project(x.id) == null);
            if (State.taskBaselines.Count != 3 || State.taskFallback.Count != 3) ResetTaskBaselines();
        }

        // ================================================================ Territory projects

        public ProjectProgress Progress(string projectId) { return State.projects.First(p => p.id == projectId); }

        public bool IsProjectUnlocked(ProjectDef p) { return State.level >= p.UnlockLevel; }

        public ProjectStage CurrentStage(ProjectDef def)
        {
            var pr = Progress(def.Id);
            return pr.done ? null : def.Stages[pr.stage];
        }

        float ProjectPerk(CivicEffect fx)
        {
            float total = 0;
            if (State.projects == null) return 0;
            foreach (var pr in State.projects)
            {
                if (!pr.done) continue;
                var d = DepthContent.Project(pr.id);
                if (d != null && d.Perk == fx) total += d.PerkValue;
            }
            return total;
        }

        public int ProjectsReadyToDeliver
        {
            get
            {
                int n = 0;
                foreach (var p in DepthContent.Projects)
                {
                    if (!IsProjectUnlocked(p)) continue;
                    var st = CurrentStage(p);
                    if (st != null && Amount(st.ResourceId) > 0) n++;
                }
                return n;
            }
        }

        /// <summary>Delivers as much of the current stage's resource as the warehouse holds (partial deliveries allowed).</summary>
        public ActionResult DeliverToProject(string projectId)
        {
            var def = DepthContent.Project(projectId);
            if (def == null) return ActionResult.Fail("Unknown project");
            if (!IsProjectUnlocked(def)) return ActionResult.Fail("Unlocks at level " + def.UnlockLevel);
            var pr = Progress(projectId);
            if (pr.done) return ActionResult.Fail("Already complete");
            var stage = def.Stages[pr.stage];
            int give = Math.Min(Amount(stage.ResourceId), stage.Amount - pr.delivered);
            var res = GameDatabase.Resource(stage.ResourceId);
            if (give <= 0) return ActionResult.Fail("No " + res.Name + " in the warehouse");
            State.inventory.Change(stage.ResourceId, -give);
            pr.delivered += give;
            State.stats.projectDeliveries += give;
            string msg = "Delivered " + give + " " + res.Name;
            if (pr.delivered >= stage.Amount)
            {
                EarnCoins(stage.Coins);
                AddXp(stage.Xp);
                AddPassXp(Balance.PassXpContract);
                pr.stage++;
                pr.delivered = 0;
                msg = "Stage " + pr.stage + "/" + def.Stages.Length + " of " + def.Name + " done! +" + stage.Coins + " coins";
                if (pr.stage >= def.Stages.Length)
                {
                    pr.done = true;
                    string reward = Apply(def.Reward);
                    msg = def.Name + " complete! " + def.PerkText + ". " + reward;
                    ProjectCompleted?.Invoke(def);
                }
            }
            RaiseChanged();
            return ActionResult.Success(msg);
        }

        // ================================================================ Foreman's orders

        /// <summary>The current set, with any task that was impossible at set start swapped for a repeatable one.</summary>
        public TaskSetDef CurrentTaskSet
        {
            get
            {
                var raw = DepthContent.TaskSet(State.taskSet);
                if (State.taskFallback == null || !State.taskFallback.Any(f => f)) return raw;
                var tasks = raw.Tasks.Select((t, i) => i < State.taskFallback.Count && State.taskFallback[i] ? DepthContent.Fallback(t) : t).ToArray();
                return new TaskSetDef { Index = raw.Index, Title = raw.Title, Quote = raw.Quote, Tasks = tasks, Reward = raw.Reward };
            }
        }

        /// <summary>Goods still needed by every unfinished project (locked ones unlock with level, so they count).</summary>
        public int RemainingProjectGoods
        {
            get
            {
                int n = 0;
                foreach (var def in DepthContent.Projects)
                {
                    var pr = Progress(def.Id);
                    if (pr.done) continue;
                    for (int s = pr.stage; s < def.Stages.Length; s++) n += def.Stages[s].Amount;
                    n -= pr.delivered;
                }
                return n;
            }
        }

        int TaskMetric(TaskKind k)
        {
            switch (k)
            {
                case TaskKind.Dispatch: return State.stats.dispatches;
                case TaskKind.Contracts: return State.stats.contractsCompleted;
                case TaskKind.ProduceAny: return State.stats.produced.Total();
                case TaskKind.CollectAny: return State.stats.collected.Total();
                case TaskKind.Passengers: return State.stats.passengers;
                case TaskKind.ProjectDeliveries: return State.stats.projectDeliveries;
                case TaskKind.EarnCoins: return State.stats.coinsEarned;
                case TaskKind.OwnWagons: return State.wagons.Count;
                case TaskKind.TotalPower: return State.trains.Sum(t => GameDatabase.Locomotive(t.defId).Power);
                case TaskKind.LocoLevels: return State.trains.Sum(t => t.level);
                case TaskKind.Population: return Population;
            }
            return 0;
        }

        void ResetTaskBaselines()
        {
            // Decide fallbacks once, at set start, so a set never changes its tasks halfway through.
            State.taskFallback.Clear();
            foreach (var t in DepthContent.TaskSet(State.taskSet).Tasks)
                State.taskFallback.Add(t.Kind == TaskKind.ProjectDeliveries && RemainingProjectGoods < t.Count);
            State.taskBaselines.Clear();
            foreach (var t in CurrentTaskSet.Tasks) State.taskBaselines.Add(t.IsAbsolute ? 0 : TaskMetric(t.Kind));
        }

        public int TaskProgress(int index)
        {
            var t = CurrentTaskSet.Tasks[index];
            int v = TaskMetric(t.Kind) - (t.IsAbsolute ? 0 : State.taskBaselines[index]);
            return Math.Max(0, Math.Min(t.Count, v));
        }

        public bool IsTaskDone(int index) { return TaskProgress(index) >= CurrentTaskSet.Tasks[index].Count; }

        public int TasksDone { get { int n = 0; for (int i = 0; i < CurrentTaskSet.Tasks.Length; i++) if (IsTaskDone(i)) n++; return n; } }

        public bool CanClaimTaskSet { get { return TasksDone == CurrentTaskSet.Tasks.Length; } }

        public ActionResult ClaimTaskSet()
        {
            if (!CanClaimTaskSet) return ActionResult.Fail("Finish all three orders first");
            var set = CurrentTaskSet;
            string reward = Apply(set.Reward);
            AddXp(60 + 30 * set.Index);
            State.taskSet++;
            ResetTaskBaselines();
            TaskSetCompleted?.Invoke(set);
            RaiseChanged();
            return ActionResult.Success("Foreman's orders complete! " + reward);
        }

        // ================================================================ Blueprints

        public int BlueprintsOf(string locoId) { return State.blueprints.Get(locoId); }

        public bool CanAssemble(LocomotiveDef d) { return d.Blueprints > 0 && BlueprintsOf(d.Id) >= d.Blueprints; }

        /// <summary>Builds a locomotive in the shops from collected blueprints (no coins needed).</summary>
        public ActionResult AssembleLocomotive(string locoId)
        {
            var d = GameDatabase.Locomotive(locoId);
            if (d == null || d.Blueprints <= 0) return ActionResult.Fail("No blueprints exist for that locomotive");
            if (BlueprintsOf(d.Id) < d.Blueprints) return ActionResult.Fail("Need " + (d.Blueprints - BlueprintsOf(d.Id)) + " more blueprints");
            if (State.trains.Count >= FleetCapacity) return ActionResult.Fail(FullText);
            State.blueprints.Change(d.Id, -d.Blueprints);
            var built = new TrainState { uid = NewUid(), defId = d.Id, platform = FreePlatform() };
            State.trains.Add(built);
            RaiseLocomotiveAdded(built);
            AddXp(200);
            RaiseChanged();
            return ActionResult.Success(d.Name + " rolls out of the shops!");
        }
    }
}
