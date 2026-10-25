using System.Collections.Generic;
using MaterialCityTycoon.Building;
using MaterialCityTycoon.Commerce;
using UnityEditor;
using UnityEngine;

namespace MaterialCityTycoon.EditorTools
{
    public sealed class StorageDebugWindow : EditorWindow
    {
        private int locationIndex;
        private int levelIndex;
        private int units = 1;
        private int displayIndex;
        private int displaySlotIndex;
        private string status;

        [MenuItem("Tools/Material City Tycoon/Storage Debug")]
        private static void Open() => GetWindow<StorageDebugWindow>("Storage Debug");

        [MenuItem("Tools/Material City Tycoon/Storage Debug", true)]
        private static bool CanOpen() => EditorApplication.isPlaying;

        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Disponível somente em Play Mode.", MessageType.Info);
                return;
            }
            FurniturePlacementController[] controllers = Object.FindObjectsByType<FurniturePlacementController>(FindObjectsInactive.Include);
            if (controllers.Length != 1)
            {
                EditorGUILayout.HelpBox("É necessário exatamente um FurniturePlacementController.", MessageType.Warning);
                return;
            }
            FurniturePlacementController controller = controllers[0];
            List<StorageLocation> locations = new();
            foreach (StorageLocation location in controller.StorageLocations)
                if (controller.TryGetPlacedFurnitureDefinition(location.Id, out FurnitureDefinition furniture)
                    && furniture.StorageCapacity != null && furniture.StorageCapacity.IsValid()) locations.Add(location);
            locations.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            if (locations.Count == 0)
            {
                EditorGUILayout.HelpBox("Construa uma estante com capacidade configurada.", MessageType.Info);
                return;
            }
            string[] labels = new string[locations.Count];
            for (int i = 0; i < locations.Count; i++)
                labels[i] = $"{locations[i].DisplayName} ({locations[i].Id})";
            locationIndex = EditorGUILayout.Popup("Estante", Mathf.Clamp(locationIndex, 0, locations.Count - 1), labels);
            StorageLocation selected = locations[locationIndex];
            controller.TryGetPlacedFurnitureDefinition(selected.Id, out FurnitureDefinition definition);
            StorageCapacityDefinition capacity = definition.StorageCapacity;
            ProductDefinition product = capacity.Product;
            StoreShelfInventory inventory = controller.ShelfInventory;
            EditorGUILayout.LabelField("Produto", product.DisplayName);
            EditorGUILayout.LabelField("Reserva livre", controller.StoreInventory.GetQuantity(product).ToString());
            EditorGUILayout.LabelField("Total nas estantes", inventory.GetTotalStored(product).ToString());
            string[] levelLabels = new string[capacity.Levels.Count];
            for (int i = 0; i < levelLabels.Length; i++)
            {
                StorageLevelDefinition level = capacity.Levels[i];
                capacity.TryGetCapacity(level.LevelId, product, out int max);
                levelLabels[i] = $"{level.DisplayName}: {inventory.GetQuantity(selected.Id, level.LevelId, product)} / {max}";
            }
            levelIndex = EditorGUILayout.Popup("Nível", Mathf.Clamp(levelIndex, 0, levelLabels.Length - 1), levelLabels);
            units = EditorGUILayout.IntField("Quantidade", units);
            string levelId = capacity.Levels[levelIndex].LevelId;
            if (GUILayout.Button("Transferir para estante"))
                status = inventory.TryTransferToStorage(selected.Id, levelId, product, units) ? "Transferência concluída." : "Transferência recusada; confira saldo e capacidade.";
            if (GUILayout.Button("Devolver à reserva"))
                status = inventory.TryReturnToReserve(selected.Id, levelId, product, units) ? "Devolução concluída." : "Devolução recusada; confira quantidade e saldo.";
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Reposição da exposição", EditorStyles.boldLabel);
            List<string> furnitureIds = new();
            controller.GetPlacedFurnitureIds(furnitureIds);
            List<string> displayIds = new();
            List<string> displayLabels = new();
            foreach (string id in furnitureIds)
            {
                if (controller.TryGetPlacedFurnitureDefinition(id, out FurnitureDefinition candidate)
                    && candidate.FurnitureTypeId == "prototype_shelf")
                {
                    displayIds.Add(id);
                    displayLabels.Add($"{candidate.DisplayName} ({id})");
                }
            }
            if (displayIds.Count == 0)
                EditorGUILayout.HelpBox("Construa uma prateleira comercial para selecionar o destino.", MessageType.Info);
            else
            {
                displayIndex = EditorGUILayout.Popup("Prateleira", Mathf.Clamp(displayIndex, 0, displayIds.Count - 1), displayLabels.ToArray());
                string displayId = displayIds[displayIndex];
                controller.TryGetPlacedFurnitureDefinition(displayId, out FurnitureDefinition displayDefinition);
                if (displayDefinition.DisplaySlots.Count > 0)
                {
                    string[] slots = new string[displayDefinition.DisplaySlots.Count];
                    for (int i = 0; i < slots.Length; i++)
                    {
                        DisplaySlotDefinition slot = displayDefinition.DisplaySlots[i];
                        bool assigned = controller.TryGetAssignedProductForDisplaySlot(displayId, slot.SlotId, out ProductDefinition slotProduct);
                        slots[i] = $"{slot.DisplayName}: {controller.GetDisplayedQuantity(displayId, slot.SlotId)} / {slot.CapacityUnits} — {(assigned ? slotProduct.DisplayName : "sem produto")}";
                    }
                    displaySlotIndex = EditorGUILayout.Popup("Slot", Mathf.Clamp(displaySlotIndex, 0, slots.Length - 1), slots);
                    if (GUILayout.Button("Transferir para exposição"))
                        status = controller.TryRestockFromStorage(selected.Id, levelId, displayId,
                            displayDefinition.DisplaySlots[displaySlotIndex].SlotId, product, units)
                            ? "Reposição concluída." : "Reposição recusada; confira produto, saldo e capacidade.";
                }
                else EditorGUILayout.HelpBox("A prateleira não possui slots configurados.", MessageType.Warning);
            }
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, MessageType.Info);
        }
    }
}
