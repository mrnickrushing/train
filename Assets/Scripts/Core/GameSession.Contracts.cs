using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironhorse.Core
{
    public sealed partial class GameSession
    {
        static readonly string[] Clients =
        {
            "Widow McAllister", "Sheriff Barlow", "The Halvorsen Brothers", "Doc Pettibone", "Mayor Quill",
            "Two Rivers Trading Co.", "Cavalry Quartermaster", "Miss Adelaide's Hotel", "Prospector Gus",
            "The Union Telegraph Co.", "Reverend Ashby", "Captain Vance", "Golden Spur Saloon", "Territorial Survey",
            "Hollister & Sons", "The Iron Range Syndicate",
        };

        public int ContractSlots { get { return Balance.BaseContractSlots + (int)Math.Round(CivicBonus(CivicEffect.ContractSlot)); } }

        public double NextContractIn { get { return Math.Max(0, State.nextContractAt - Now); } }

        /// <summary>Resources the player can currently obtain: raw ones from unlocked outposts, goods from owned factories.</summary>
        public List<string> ObtainableResources()
        {
            var list = new List<string>();
            foreach (var d in GameDatabase.Destinations)
                if (IsDestinationUnlocked(d) && !list.Contains(d.ResourceId)) list.Add(d.ResourceId);
            foreach (var b in State.buildings)
            {
                var d = GameDatabase.Building(b.defId);
                if (d.Recipe != null && !list.Contains(d.Recipe.Output.id)) list.Add(d.Recipe.Output.id);
            }
            return list;
        }

        bool FillContracts(bool force)
        {
            if (State.contracts.Count >= ContractSlots) return false;
            if (force)
            {
                // Fresh board: fill every slot at once.
                while (State.contracts.Count < ContractSlots) State.contracts.Add(GenerateContract());
                State.nextContractAt = Now + Balance.ContractRefreshSeconds;
                return true;
            }
            if (Now < State.nextContractAt) return false;
            // Afterwards orders trickle in one at a time.
            State.contracts.Add(GenerateContract());
            State.nextContractAt = Now + Balance.ContractRefreshSeconds;
            return true;
        }

        ContractState GenerateContract()
        {
            var rng = NextRng();
            var pool = ObtainableResources();
            int reqCount = pool.Count < 2 ? 1 : (rng.NextDouble() < Math.Min(0.65, 0.2 + State.level * 0.05) ? 2 : 1);
            var c = new ContractState { uid = NewUid(), client = Clients[rng.Next(Clients.Length)] };
            int value = 0;
            for (int i = 0; i < reqCount; i++)
            {
                string id;
                int guard = 0;
                do { id = pool[rng.Next(pool.Count)]; } while (c.requirements.Any(r => r.id == id) && ++guard < 20);
                if (c.requirements.Any(r => r.id == id)) break;
                var res = GameDatabase.Resource(id);
                double scale = res.Manufactured ? 2 + State.level * 0.9 : 8 + State.level * 3.2;
                int amt = Math.Max(2, (int)Math.Round(scale * (0.75 + rng.NextDouble() * 0.6)));
                // Never ask for more than a fraction of the warehouse.
                amt = Math.Min(amt, WarehouseCapacity / 3);
                c.requirements.Add(new ResourceAmount(id, amt));
                value += res.Value * amt;
            }
            c.rewardCoins = (int)Math.Round(value * 1.8 + 40);
            c.rewardXp = Math.Max(5, (int)Math.Round(value * 0.45));
            c.rewardNuggets = rng.NextDouble() < 0.15 ? 1 + rng.Next(3) : 0;
            return c;
        }

        public bool CanDeliver(ContractState c)
        {
            foreach (var r in c.requirements) if (Amount(r.id) < r.amount) return false;
            return true;
        }

        public ActionResult DeliverContract(int uid)
        {
            var c = State.contracts.FirstOrDefault(x => x.uid == uid);
            if (c == null) return ActionResult.Fail("Unknown contract");
            if (!CanDeliver(c)) return ActionResult.Fail(MissingText(0, c.requirements));
            Pay(0, c.requirements);
            EarnCoins(c.rewardCoins);
            State.nuggets += c.rewardNuggets;
            AddXp(c.rewardXp);
            State.contracts.Remove(c);
            State.stats.contractsCompleted++;
            // A fresh order arrives shortly after a delivery.
            State.nextContractAt = Math.Min(State.nextContractAt, Now + 10);
            ContractCompleted?.Invoke(c);
            RaiseChanged();
            return ActionResult.Success(c.client + " pays " + c.rewardCoins + " coins");
        }

        public ActionResult DiscardContract(int uid)
        {
            var c = State.contracts.FirstOrDefault(x => x.uid == uid);
            if (c == null) return ActionResult.Fail("Unknown contract");
            State.contracts.Remove(c);
            State.nextContractAt = Now + Balance.ContractRefreshSeconds;
            RaiseChanged();
            return ActionResult.Success("Contract torn up. A new one arrives in " + (int)Balance.ContractRefreshSeconds / 60 + " min");
        }
    }
}
