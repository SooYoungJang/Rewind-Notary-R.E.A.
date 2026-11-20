using System;

namespace RewindNotary
{
    internal enum SupplyItem { MintTea, TravelBandage }

    internal readonly struct SupplyProduct
    {
        internal readonly SupplyItem Item;
        internal readonly int Price, Recovery;
        private readonly string nameKey;
        internal string Name => Loc.Get(nameKey);

        internal SupplyProduct(SupplyItem item, int price, int recovery, string nameKey)
        {
            Item = item;
            Price = price;
            Recovery = recovery;
            this.nameKey = nameKey;
        }

        internal static readonly SupplyProduct[] Stock =
        {
            new SupplyProduct(SupplyItem.MintTea, 3, 2, "shop.mint_tea"),
            new SupplyProduct(SupplyItem.TravelBandage, 5, 4, "shop.travel_bandage")
        };
    }

    [Serializable]
    public sealed class SupplyPouch
    {
        internal const int EncounterReward = 6;
        public int silver = 8;
        public int mintTea, travelBandage;

        internal int Count(SupplyItem item) => item == SupplyItem.MintTea ? mintTea : travelBandage;

        internal SupplyPouch Purchased(SupplyProduct product)
        {
            if (silver < product.Price) return null;
            var updated = (SupplyPouch)MemberwiseClone();
            updated.silver -= product.Price;
            updated.ChangeCount(product.Item, 1);
            return updated;
        }

        internal SupplyPouch Consumed(SupplyItem item)
        {
            if (Count(item) == 0) return null;
            var updated = (SupplyPouch)MemberwiseClone();
            updated.ChangeCount(item, -1);
            return updated;
        }

        private void ChangeCount(SupplyItem item, int amount)
        {
            if (item == SupplyItem.MintTea) mintTea = checked(mintTea + amount);
            else travelBandage = checked(travelBandage + amount);
        }

        internal void Validate()
        {
            if (silver < 0 || mintTea < 0 || travelBandage < 0)
                throw new InvalidOperationException("Invalid supply pouch");
        }
    }
}
