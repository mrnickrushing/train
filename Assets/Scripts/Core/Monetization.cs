using System;
using System.Collections.Generic;
using System.Linq;

namespace Ironhorse.Core
{
    public enum ProductKind { Consumable, NonConsumable }

    /// <summary>What a purchase, offer, pass tier or ad gives the player.</summary>
    public sealed class Grant
    {
        public int Coins;
        public int Nuggets;
        public int Fuel;
        public string LocomotiveId;
        public ResourceAmount[] Resources = new ResourceAmount[0];
        public bool PassPremium;
        public bool FuelTank;
        public string BlueprintLoco;
        public int Blueprints;

        public override string ToString()
        {
            var parts = new List<string>();
            if (LocomotiveId != null) parts.Add(GameDatabase.Locomotive(LocomotiveId).Name);
            if (Nuggets > 0) parts.Add(Nuggets + " nuggets");
            if (Coins > 0) parts.Add(Coins.ToString("#,0") + " coins");
            if (Fuel > 0) parts.Add(Fuel + " fuel");
            foreach (var r in Resources) parts.Add(r.amount + " " + GameDatabase.Resource(r.id).Name);
            if (PassPremium) parts.Add("Premium Season Pass");
            if (BlueprintLoco != null && Blueprints > 0) parts.Add(Blueprints + " " + GameDatabase.Locomotive(BlueprintLoco).Name + " blueprint" + (Blueprints > 1 ? "s" : ""));
            if (FuelTank) parts.Add("+" + Balance.FuelTankBonus + " max fuel forever");
            return string.Join(", ", parts);
        }
    }

    /// <summary>A real-money product sold through the App Store (via RevenueCat).</summary>
    public sealed class ProductDef
    {
        public string Id;            // must match App Store Connect / RevenueCat product id
        public ProductKind Kind;
        public string Title;
        public string FallbackPrice; // shown until the store returns localized prices
        public Grant Grant;
    }

    public enum OfferSchedule { Starter, Weekly, Always }

    /// <summary>A featured bundle shown in the shop, bought with real money or nuggets.</summary>
    public sealed class OfferDef
    {
        public string Id;
        public string Title;
        public string Subtitle;
        public string Badge;          // e.g. "+150% VALUE"
        public OfferSchedule Schedule;
        public string ProductId;      // real-money offers
        public int NuggetPrice;       // nugget offers (ProductId == null)
        public int UnlockLevel;
        public Grant Grant;           // nugget offers only; IAP offers grant via their product
    }

    public enum AdReward { Fuel, Nuggets, RushTrain }

    public sealed class PassTier
    {
        public int Tier;
        public Grant Free;
        public Grant Premium;
    }

    /// <summary>Store catalog, offers and Rail Season Pass content.</summary>
    public static class Catalog
    {
        public const string StarterPack = "ironhorse.starter_pack";
        public const string SeasonPass = "ironhorse.season_pass";
        public const string FuelTank = "ironhorse.fuel_tank";

        public static readonly List<ProductDef> Products = new List<ProductDef>
        {
            P("ironhorse.nuggets_100",  ProductKind.Consumable, "Pocketful of Nuggets", "$0.99",  new Grant { Nuggets = 100 }),
            P("ironhorse.nuggets_550",  ProductKind.Consumable, "Pouch of Nuggets",     "$4.99",  new Grant { Nuggets = 550 }),
            P("ironhorse.nuggets_1200", ProductKind.Consumable, "Sack of Nuggets",      "$9.99",  new Grant { Nuggets = 1200 }),
            P("ironhorse.nuggets_2600", ProductKind.Consumable, "Strongbox of Nuggets", "$19.99", new Grant { Nuggets = 2600 }),
            P("ironhorse.nuggets_7000", ProductKind.Consumable, "Bank Vault of Nuggets","$49.99", new Grant { Nuggets = 7000 }),
            P(StarterPack, ProductKind.NonConsumable, "Homesteader's Starter Pack", "$2.99",
                new Grant { Nuggets = 300, Coins = 5000, Fuel = 120, LocomotiveId = "lark", Resources = new[] { new ResourceAmount("timber", 60), new ResourceAmount("planks", 30) } }),
            P(FuelTank, ProductKind.NonConsumable, "Big Tender Upgrade", "$4.99", new Grant { FuelTank = true, Fuel = 200 }),
            P(SeasonPass, ProductKind.Consumable, "Rail Season Pass", "$7.99", new Grant { PassPremium = true }),
            P("ironhorse.bundle_ironbison", ProductKind.Consumable, "Iron Bison Bundle", "$9.99",
                new Grant { LocomotiveId = "ironbison", Coins = 15000, Fuel = 150, Resources = new[] { new ResourceAmount("steel", 40) } }),
            P("ironhorse.bundle_silverarrow", ProductKind.Consumable, "Silver Arrow Bundle", "$19.99",
                new Grant { LocomotiveId = "silverarrow", Nuggets = 500, Coins = 25000, Fuel = 250 }),
            P("ironhorse.bundle_thunderhead", ProductKind.Consumable, "Thunderhead Twin Bundle", "$29.99",
                new Grant { LocomotiveId = "thunderhead", Nuggets = 800, Coins = 40000, Fuel = 400 }),
            P("ironhorse.bundle_prospector", ProductKind.Consumable, "Prospector's Haul", "$4.99",
                new Grant { Nuggets = 250, Fuel = 200, Resources = new[] { new ResourceAmount("steel", 30), new ResourceAmount("tools", 10) } }),
        };

        public static readonly List<OfferDef> Offers = new List<OfferDef>
        {
            new OfferDef { Id = "starter", Title = "Homesteader's Starter Pack", Subtitle = "A second locomotive, nuggets, coins and supplies. One time only!", Badge = "+400% VALUE", Schedule = OfferSchedule.Starter, ProductId = StarterPack },
            new OfferDef { Id = "w_ironbison", Title = "Iron Bison Bundle", Subtitle = "The 2-8-2 heavy hauler plus coins, fuel and steel.", Badge = "+150% VALUE", Schedule = OfferSchedule.Weekly, ProductId = "ironhorse.bundle_ironbison", UnlockLevel = 4 },
            new OfferDef { Id = "w_prospector", Title = "Prospector's Haul", Subtitle = "Nuggets, a full tender of fuel and factory goods.", Badge = "+120% VALUE", Schedule = OfferSchedule.Weekly, ProductId = "ironhorse.bundle_prospector", UnlockLevel = 2 },
            new OfferDef { Id = "w_silverarrow", Title = "Silver Arrow Bundle", Subtitle = "Ride the Golden Age early: the stainless streamliner.", Badge = "+100% VALUE", Schedule = OfferSchedule.Weekly, ProductId = "ironhorse.bundle_silverarrow", UnlockLevel = 8 },
            new OfferDef { Id = "w_thunderhead", Title = "Legendary: Thunderhead Twin", Subtitle = "Two units, one roar. The mightiest hauler in the territory.", Badge = "LEGENDARY", Schedule = OfferSchedule.Weekly, ProductId = "ironhorse.bundle_thunderhead", UnlockLevel = 10 },
            new OfferDef { Id = "n_coal", Title = "Coal Car Delivery", Subtitle = "150 fuel for your fleet.", Schedule = OfferSchedule.Always, NuggetPrice = 30, Grant = new Grant { Fuel = 150 } },
            new OfferDef { Id = "n_lumber", Title = "Lumber Shipment", Subtitle = "80 timber and 40 planks.", Schedule = OfferSchedule.Always, NuggetPrice = 25, Grant = new Grant { Resources = new[] { new ResourceAmount("timber", 80), new ResourceAmount("planks", 40) } } },
            new OfferDef { Id = "n_steel", Title = "Steel Consignment", Subtitle = "40 steel straight from the mills back east.", Schedule = OfferSchedule.Always, NuggetPrice = 45, UnlockLevel = 4, Grant = new Grant { Resources = new[] { new ResourceAmount("steel", 40) } } },
            new OfferDef { Id = "n_parts", Title = "Workshop Crate", Subtitle = "30 rivets, 20 bearings and 8 drive rods for locomotive overhauls.", Schedule = OfferSchedule.Always, NuggetPrice = 50, UnlockLevel = 2, Grant = new Grant { Resources = new[] { new ResourceAmount("rivets", 30), new ResourceAmount("bearings", 20), new ResourceAmount("rods", 8) } } },
            new OfferDef { Id = "n_coins", Title = "Bank Draft", Subtitle = "10,000 coins.", Schedule = OfferSchedule.Always, NuggetPrice = 60, Grant = new Grant { Coins = 10000 } },
        };

        static List<PassTier> _tiers;

        /// <summary>30 tiers. Free track: steady coins/fuel/goods. Premium: nuggets, big bundles, an exclusive locomotive.</summary>
        public static List<PassTier> PassTiers
        {
            get
            {
                if (_tiers != null) return _tiers;
                _tiers = new List<PassTier>();
                for (int i = 1; i <= Balance.PassTiers; i++)
                {
                    var free = new Grant();
                    var prem = new Grant();
                    switch (i % 4)
                    {
                        case 1: free.Coins = 400 + i * 120; break;
                        case 2: free.Fuel = 25; break;
                        case 3: free.Resources = new[] { new ResourceAmount(i < 12 ? "planks" : "steel", 10 + i) }; break;
                        default: free.Nuggets = 5; break;
                    }
                    if (i % 5 == 0) { prem.Nuggets = 40 + i * 2; prem.Fuel = 60; }
                    else if (i % 2 == 0) { prem.Coins = 1500 + i * 300; prem.Fuel = 40; }
                    else { prem.Nuggets = 15; prem.Resources = new[] { new ResourceAmount(i < 15 ? "steel" : "tools", 8 + i / 2) }; }
                    if (i == 10) prem.LocomotiveId = "redmesa";
                    if (i == Balance.PassTiers) { prem.LocomotiveId = "sundown"; prem.Nuggets = 200; }
                    _tiers.Add(new PassTier { Tier = i, Free = free, Premium = prem });
                }
                return _tiers;
            }
        }

        public static ProductDef Product(string id) { return Products.FirstOrDefault(p => p.Id == id); }
        public static OfferDef Offer(string id) { return Offers.FirstOrDefault(o => o.Id == id); }

        static ProductDef P(string id, ProductKind kind, string title, string price, Grant g)
        { return new ProductDef { Id = id, Kind = kind, Title = title, FallbackPrice = price, Grant = g }; }
    }
}
