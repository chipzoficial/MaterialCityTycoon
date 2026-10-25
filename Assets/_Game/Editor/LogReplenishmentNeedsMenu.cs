using System.Collections.Generic;
using MaterialCityTycoon.Building;
using UnityEditor;
using UnityEngine;

namespace MaterialCityTycoon.EditorTools
{
    public static class LogReplenishmentNeedsMenu
    {
        [MenuItem("Tools/Material City Tycoon/Log Replenishment Needs")]
        private static void LogNeeds()
        {
            if (!EditorApplication.isPlaying) return;
            FurniturePlacementController[] stores = Object.FindObjectsByType<FurniturePlacementController>(FindObjectsInactive.Include);
            if (stores.Length == 0) { Debug.LogWarning("Nenhuma loja encontrada."); return; }
            foreach (FurniturePlacementController store in stores)
            {
                IReadOnlyList<ReplenishmentNeed> needs = new ReplenishmentNeedsService(store).GetNeeds();
                if (needs.Count == 0) { Debug.Log($"Loja '{store.name}': nenhuma necessidade de reposição.", store); continue; }
                foreach (ReplenishmentNeed need in needs)
                {
                    store.TryGetPlacedFurnitureDefinition(need.FurnitureId, out FurnitureDefinition furniture);
                    store.TryGetAssignedProductForDisplaySlot(need.FurnitureId, need.SlotId, out var product);
                    string slotName = need.SlotId;
                    foreach (DisplaySlotDefinition slot in furniture.DisplaySlots)
                        if (slot.SlotId == need.SlotId) { slotName = slot.DisplayName; break; }
                    string availability = need.StoredAvailable == 0 ? "sem estoque no depósito" : "estoque no depósito disponível";
                    Debug.Log($"Loja '{store.name}' — {furniture.DisplayName} ({need.FurnitureId}), {slotName}: {product.DisplayName}; exposição {need.Displayed}/{need.Capacity}, necessidade {need.Required}, depósito {need.StoredAvailable}, atendível {need.PotentiallyFulfilled} ({availability}).", store);
                }
            }
        }

        [MenuItem("Tools/Material City Tycoon/Log Replenishment Needs", true)]
        private static bool CanLogNeeds() => EditorApplication.isPlaying;
    }
}
