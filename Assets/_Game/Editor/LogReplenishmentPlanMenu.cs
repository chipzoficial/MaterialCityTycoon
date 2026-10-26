using System.Collections.Generic;
using MaterialCityTycoon.Building;
using MaterialCityTycoon.Commerce;
using UnityEditor;
using UnityEngine;

namespace MaterialCityTycoon.EditorTools
{
    public static class LogReplenishmentPlanMenu
    {
        [MenuItem("Tools/Material City Tycoon/Log Replenishment Plan")]
        private static void LogPlan()
        {
            if (!EditorApplication.isPlaying) return;
            FurniturePlacementController[] stores = Object.FindObjectsByType<FurniturePlacementController>(FindObjectsInactive.Include);
            if (stores.Length == 0) { Debug.LogWarning("Nenhuma loja encontrada."); return; }
            foreach (FurniturePlacementController store in stores)
            {
                ReplenishmentPlan plan = new ReplenishmentPlanService(store).CreatePlan();
                if (plan.Lines.Count == 0 && plan.UnmetNeeds.Count == 0)
                { Debug.Log($"Loja '{store.name}': nenhuma necessidade de reposição.", store); continue; }
                Debug.Log($"Plano da loja '{store.name}' (consulta sem reserva de estoque):", store);
                SortedDictionary<string, long> totals = new(System.StringComparer.Ordinal);
                Dictionary<string, string> productNames = new(System.StringComparer.Ordinal);
                foreach (ReplenishmentPlanLine line in plan.Lines)
                {
                    store.TryGetPlacedFurnitureDefinition(line.StorageId, out FurnitureDefinition origin);
                    store.TryGetPlacedFurnitureDefinition(line.FurnitureId, out FurnitureDefinition destination);
                    store.TryGetAssignedProductForDisplaySlot(line.FurnitureId, line.SlotId, out ProductDefinition product);
                    string levelName = line.LevelId;
                    foreach (StorageLevelDefinition level in origin.StorageCapacity.Levels)
                        if (level.LevelId == line.LevelId) { levelName = level.DisplayName; break; }
                    string slotName = SlotName(destination, line.SlotId);
                    Debug.Log($"{origin.DisplayName} ({line.StorageId}), {levelName} ({line.LevelId}) → {destination.DisplayName} ({line.FurnitureId}), {slotName} ({line.SlotId}): {product.DisplayName} ({line.ProductId}), {line.Quantity} unidade(s).", store);
                    totals.TryGetValue(line.ProductId, out long previous);
                    totals[line.ProductId] = previous + line.Quantity;
                    productNames[line.ProductId] = product.DisplayName;
                }
                foreach (UnmetReplenishmentNeed unmet in plan.UnmetNeeds)
                {
                    store.TryGetPlacedFurnitureDefinition(unmet.FurnitureId, out FurnitureDefinition furniture);
                    store.TryGetAssignedProductForDisplaySlot(unmet.FurnitureId, unmet.SlotId, out ProductDefinition product);
                    if (!totals.ContainsKey(unmet.ProductId)) totals.Add(unmet.ProductId, 0);
                    productNames[unmet.ProductId] = product.DisplayName;
                    Debug.Log($"Não atendido: {furniture.DisplayName} ({unmet.FurnitureId}), {SlotName(furniture, unmet.SlotId)} ({unmet.SlotId}), {product.DisplayName} ({unmet.ProductId}): {unmet.Quantity} unidade(s).", store);
                }
                foreach (KeyValuePair<string, long> total in totals)
                    Debug.Log($"Total planejado para {productNames[total.Key]} ({total.Key}): {total.Value} unidade(s).", store);
            }
        }

        private static string SlotName(FurnitureDefinition furniture, string id)
        {
            foreach (DisplaySlotDefinition slot in furniture.DisplaySlots)
                if (slot.SlotId == id) return slot.DisplayName;
            return id;
        }

        [MenuItem("Tools/Material City Tycoon/Log Replenishment Plan", true)]
        private static bool CanLogPlan() => EditorApplication.isPlaying;
    }
}
