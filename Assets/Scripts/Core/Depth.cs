using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironhorse.Core
{
    // ================================================================ Territory projects

    public sealed class ProjectStage
    {
        public string ResourceId;
        public int Amount;
        public int Coins;
        public int Xp;
    }

    /// <summary>A big multi-stage build in the territory. Stages are filled by partial deliveries.</summary>
    public sealed class ProjectDef
    {
        public string Id;
        public string Name;
        public string Description;
        public int UnlockLevel;
        public ProjectStage[] Stages;
        public Grant Reward;
        /// <summary>Permanent bonus once complete, applied like a civic building.</summary>
        public CivicEffect Perk;
        public float PerkValue;
        public string PerkText;
    }

    // ================================================================ Foreman's orders

    public enum TaskKind { Dispatch, Contracts, ProduceAny, CollectAny, Passengers, ProjectDeliveries, OwnWagons, TotalPower, LocoLevels, Population, EarnCoins }

    public sealed class TaskDef
    {
        public TaskKind Kind;
        public int Count;
        public string Text;
        /// <summary>Delta tasks count progress made after the set started; absolute ones read current state.</summary>
        public bool IsAbsolute { get { return Kind == TaskKind.OwnWagons || Kind == TaskKind.TotalPower || Kind == TaskKind.LocoLevels || Kind == TaskKind.Population; } }
    }

    public sealed class TaskSetDef
    {
        public int Index;
        public string Title;
        public string Quote;
        public TaskDef[] Tasks;
        public Grant Reward;
    }

    public static class DepthContent
    {
        public static readonly List<ProjectDef> Projects = new List<ProjectDef>
        {
            Project("telegraph", "Telegraph Line to Fort Calder", "String wire across the badlands so orders arrive faster.", 3,
                new[] { S("timber", 40), S("planks", 20), S("timber", 70), S("planks", 35) },
                new Grant { Nuggets = 30, Resources = new[] { new ResourceAmount("rivets", 20) } }, CivicEffect.Xp, 0.05f, "+5% XP forever"),
            Project("trestle", "Crooked River Trestle", "A timber trestle to shave miles off the western run.", 5,
                new[] { S("planks", 40), S("steel", 10), S("planks", 60), S("steel", 20), S("timber", 120) },
                new Grant { Nuggets = 45, Resources = new[] { new ResourceAmount("bearings", 15) } }, CivicEffect.TrainSpeed, 0.05f, "+5% train speed forever"),
            Project("mineshaft", "Black Hollow Deep Shaft", "Sink a new shaft and timber it properly this time.", 7,
                new[] { S("timber", 100), S("steel", 25), S("tools", 8), S("coal", 150), S("steel", 40), S("tools", 15) },
                new Grant { Nuggets = 60, BlueprintLoco = "sundown", Blueprints = 4 }, CivicEffect.Yield, 0.05f, "+5% cargo from every run"),
            Project("resupply", "Fort Calder Resupply", "Keep the cavalry fed through the long winter.", 9,
                new[] { S("provisions", 30), S("flour", 40), S("tools", 10), S("provisions", 60), S("flour", 80), S("tools", 20) },
                new Grant { Nuggets = 80, BlueprintLoco = "silverarrow", Blueprints = 5, Resources = new[] { new ResourceAmount("rods", 15) } }, CivicEffect.Income, 0.10f, "+10% town income"),
            Project("dam", "Silver Lake Dam", "Tame the river and light the valley.", 12,
                new[] { S("steel", 60), S("tools", 20), S("copper_wire", 30), S("steel", 100), S("tools", 40), S("copper_wire", 60), S("timber", 250), S("steel", 150) },
                new Grant { Nuggets = 120, BlueprintLoco = "thunderhead", Blueprints = 6, Resources = new[] { new ResourceAmount("gauges", 20) } }, CivicEffect.Yield, 0.10f, "+10% cargo from every run"),
            Project("goldenspike", "The Golden Spike", "Join east and west. The territory becomes a state.", 15,
                new[] { S("gold_bars", 20), S("steel", 150), S("tools", 50), S("copper_wire", 60), S("fuel", 40), S("gold_bars", 40), S("steel", 250), S("tools", 80), S("gold_bars", 60), S("fuel", 100) },
                new Grant { Nuggets = 300, BlueprintLoco = "thunderhead", Blueprints = 10 }, CivicEffect.Income, 0.15f, "+15% town income"),
        };

        static List<TaskSetDef> _handmade;

        /// <summary>The foreman's sets: a handcrafted run, then an endless, scaling rotation.</summary>
        public static TaskSetDef TaskSet(int index)
        {
            if (_handmade == null) _handmade = Handmade();
            if (index < _handmade.Count) return _handmade[index];
            int cycle = index - _handmade.Count;
            int k = cycle + 3;
            var rotation = new[]
            {
                new[] { T(TaskKind.Dispatch, 10 + 2 * k), T(TaskKind.Contracts, 3 + k / 2), T(TaskKind.ProduceAny, 20 + 6 * k) },
                new[] { T(TaskKind.CollectAny, 300 + 80 * k), T(TaskKind.Passengers, 100 + 40 * k), T(TaskKind.ProjectDeliveries, 60 + 25 * k) },
                new[] { T(TaskKind.TotalPower, 12 + 2 * k), T(TaskKind.OwnWagons, 10 + 2 * k), T(TaskKind.EarnCoins, 5000 + 2500 * k) },
                new[] { T(TaskKind.LocoLevels, 6 + 2 * k), T(TaskKind.Dispatch, 12 + 2 * k), T(TaskKind.Population, 100 + 40 * k) },
            };
            var tasks = rotation[cycle % rotation.Length];
            return new TaskSetDef
            {
                Index = index,
                Title = new[] { "Keep 'em Rolling", "Bring the Goods Home", "A Bigger Railroad", "Polish the Brass" }[cycle % 4] + " " + ToRoman(cycle / 4 + 1),
                Quote = "The territory never sleeps, and neither does this railroad.",
                Tasks = tasks,
                Reward = new Grant { Nuggets = 15 + k * 2, Coins = 1500 + 400 * k, Resources = new[] { PartFor(cycle, 8 + k) }, BlueprintLoco = BlueprintFor(cycle), Blueprints = BlueprintFor(cycle) != null ? 2 : 0 },
            };
        }

        static List<TaskSetDef> Handmade()
        {
            return new List<TaskSetDef>
            {
                Set(0, "First Shift", "Show me you can keep a timetable, greenhorn.",
                    new[] { T(TaskKind.Dispatch, 3), T(TaskKind.CollectAny, 40), T(TaskKind.ProduceAny, 6) },
                    new Grant { Coins = 600, Nuggets = 5, Resources = new[] { new ResourceAmount("rivets", 10) } }),
                Set(1, "Open for Business", "Folks out here need goods. Get 'em what they ask for.",
                    new[] { T(TaskKind.Contracts, 2), T(TaskKind.Dispatch, 6), T(TaskKind.Population, 30) },
                    new Grant { Coins = 1000, Nuggets = 8, Resources = new[] { new ResourceAmount("rivets", 15) } }),
                Set(2, "Freight Fleet", "Expand your freight cars so your trains can haul more.",
                    new[] { T(TaskKind.OwnWagons, 5), T(TaskKind.TotalPower, 5), T(TaskKind.Dispatch, 10) },
                    new Grant { Coins = 1500, Nuggets = 10, Resources = new[] { new ResourceAmount("bearings", 10) } }),
                Set(3, "Build the Territory", "Big projects make big railroads. Start hauling.",
                    new[] { T(TaskKind.ProjectDeliveries, 60), T(TaskKind.ProduceAny, 30), T(TaskKind.Contracts, 4) },
                    new Grant { Coins = 2500, Nuggets = 15, Resources = new[] { new ResourceAmount("bearings", 15), new ResourceAmount("rivets", 15) } }),
                Set(4, "All Aboard", "Passengers pay in cash and gossip. Carry plenty of both.",
                    new[] { T(TaskKind.Passengers, 80), T(TaskKind.LocoLevels, 6), T(TaskKind.EarnCoins, 6000) },
                    new Grant { Coins = 4000, Nuggets = 20, Resources = new[] { new ResourceAmount("rods", 10) }, BlueprintLoco = "sundown", Blueprints = 2 }),
                Set(5, "Iron Will", "A real railroad runs hot every hour of the day.",
                    new[] { T(TaskKind.Dispatch, 20), T(TaskKind.CollectAny, 600), T(TaskKind.TotalPower, 14) },
                    new Grant { Coins = 6000, Nuggets = 25, Resources = new[] { new ResourceAmount("gauges", 8) }, BlueprintLoco = "silverarrow", Blueprints = 2 }),
            };
        }

        /// <summary>Every other repeatable set grants blueprints, rotating through all assemblable locomotives.</summary>
        static string BlueprintFor(int cycle)
        {
            if (cycle % 2 == 0) return null;
            var locos = new[] { "sundown", "silverarrow", "thunderhead" };
            return locos[(cycle / 2) % locos.Length];
        }

        /// <summary>A repeatable stand-in when a task can no longer be completed (e.g. all projects are finished).</summary>
        public static TaskDef Fallback(TaskDef t)
        {
            if (t.Kind == TaskKind.ProjectDeliveries) return new TaskDef { Kind = TaskKind.CollectAny, Count = t.Count * 4 };
            return t;
        }

        static ResourceAmount PartFor(int cycle, int amount)
        {
            var ids = new[] { "rivets", "bearings", "rods", "gauges" };
            return new ResourceAmount(ids[cycle % ids.Length], amount);
        }

        static string ToRoman(int n)
        {
            var r = new[] { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
            return n <= r.Length ? r[n - 1] : n.ToString();
        }

        public static string Describe(TaskDef t)
        {
            if (t.Text != null) return t.Text;
            switch (t.Kind)
            {
                case TaskKind.Dispatch: return "Dispatch " + t.Count + " trains";
                case TaskKind.Contracts: return "Complete " + t.Count + " orders";
                case TaskKind.ProduceAny: return "Produce " + t.Count + " goods in factories";
                case TaskKind.CollectAny: return "Haul " + t.Count + " cargo home";
                case TaskKind.Passengers: return "Carry " + t.Count + " passengers";
                case TaskKind.ProjectDeliveries: return "Deliver " + t.Count + " goods to territory projects";
                case TaskKind.OwnWagons: return "Own " + t.Count + " wagons";
                case TaskKind.TotalPower: return "Reach " + t.Count + " combined pulling power";
                case TaskKind.LocoLevels: return "Reach " + t.Count + " combined locomotive levels";
                case TaskKind.Population: return "Grow the town to " + t.Count + " people";
                case TaskKind.EarnCoins: return "Earn " + t.Count.ToString("#,0") + " coins";
            }
            return t.Kind.ToString();
        }

        static ProjectStage S(string res, int amount)
        {
            var r = GameDatabase.Resource(res);
            int value = r.Value * amount;
            return new ProjectStage { ResourceId = res, Amount = amount, Coins = (int)(value * 1.4), Xp = Math.Max(10, (int)(value * 0.4)) };
        }

        static ProjectDef Project(string id, string name, string desc, int lvl, ProjectStage[] stages, Grant reward, CivicEffect perk, float value, string perkText)
        { return new ProjectDef { Id = id, Name = name, Description = desc, UnlockLevel = lvl, Stages = stages, Reward = reward, Perk = perk, PerkValue = value, PerkText = perkText }; }

        static TaskDef T(TaskKind k, int count) { return new TaskDef { Kind = k, Count = count }; }

        static TaskSetDef Set(int i, string title, string quote, TaskDef[] tasks, Grant reward)
        { return new TaskSetDef { Index = i, Title = title, Quote = quote, Tasks = tasks, Reward = reward }; }

        public static ProjectDef Project(string id) { return Projects.FirstOrDefault(p => p.Id == id); }
    }
}
