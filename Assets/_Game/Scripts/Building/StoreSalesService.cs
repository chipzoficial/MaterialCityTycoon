using System;
using System.Collections.Generic;
using MaterialCityTycoon.Commerce;

namespace MaterialCityTycoon.Building
{
    public sealed class SaleTransaction
    {
        public string FurnitureId { get; }
        public string SlotId { get; }
        public string ProductId { get; }
        public int Quantity { get; }
        public long UnitPriceCents { get; }
        public long TotalCents { get; }

        internal SaleTransaction(string furnitureId, string slotId, string productId, int quantity, long unitPrice, long total)
        {
            FurnitureId = furnitureId; SlotId = slotId; ProductId = productId;
            Quantity = quantity; UnitPriceCents = unitPrice; TotalCents = total;
        }
    }

    /// <summary>Session-only sales and cash for one store. Sales consume displayed stock only.</summary>
    public sealed class StoreSalesService
    {
        private readonly FurnitureDisplaySlotInventory display;
        private readonly Func<string, string, ProductDefinition> resolveProduct;
        private readonly List<SaleTransaction> transactions = new();
        private readonly System.Collections.ObjectModel.ReadOnlyCollection<SaleTransaction> history;

        public long BalanceCents { get; private set; }
        public IReadOnlyList<SaleTransaction> History => history;

        internal StoreSalesService(FurnitureDisplaySlotInventory display, Func<string, string, ProductDefinition> resolveProduct)
        {
            this.display = display;
            this.resolveProduct = resolveProduct;
            history = transactions.AsReadOnly();
        }

        public bool TrySell(string furnitureId, string slotId, int units, out string reason)
        {
            if (units <= 0) { reason = "Informe uma quantidade positiva."; return false; }
            ProductDefinition product = resolveProduct(furnitureId, slotId);
            if (product == null || !product.IsValid(out _))
            { reason = "Móvel, slot ou produto atribuído inválido."; return false; }
            if (!display.TryGetQuantity(furnitureId, slotId, out string productId, out int available)
                || productId != product.ProductId || available < units)
            { reason = "Estoque exposto insuficiente ou inconsistente."; return false; }
            long price = product.SalePriceCents;
            if (price <= 0) { reason = "Configure um preço de venda positivo em centavos."; return false; }
            if (price > long.MaxValue / units || BalanceCents > long.MaxValue - price * units)
            { reason = "O valor da venda excede o limite do caixa."; return false; }

            long total = price * units;
            SaleTransaction transaction = new(furnitureId, slotId, productId, units, price, total);
            // Reserve list capacity before changing either inventory or cash.
            if (transactions.Count == transactions.Capacity)
                transactions.Capacity = checked(transactions.Count + 1);
            if (!display.TryRemoveUnits(furnitureId, slotId, productId, units))
            { reason = "Não foi possível retirar o estoque exposto."; return false; }
            BalanceCents += total;
            transactions.Add(transaction);
            reason = string.Empty;
            return true;
        }
    }
}
