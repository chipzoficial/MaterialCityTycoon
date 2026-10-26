using System;
using System.Collections.Generic;
using System.Linq;
using MaterialCityTycoon.Building;
using MaterialCityTycoon.Commerce;
using UnityEditor;
using UnityEngine;

namespace MaterialCityTycoon.EditorTools
{
    public readonly struct StoreDebugBalances
    {
        public readonly long Reserve, Storage, Display;
        public long Total => Reserve + Storage + Display;
        public StoreDebugBalances(long reserve, long storage, long display)
        { Reserve = reserve; Storage = storage; Display = display; }
    }

    public static class StoreDebugData
    {
        public static StoreDebugBalances GetBalances(FurniturePlacementController store, ProductDefinition product)
        {
            if (store == null || product == null || !product.IsValid(out _)) return default;
            long display = 0;
            List<string> ids = new(); store.GetPlacedFurnitureIds(ids);
            foreach (string id in ids)
            {
                if (!store.TryGetPlacedFurnitureDefinition(id, out FurnitureDefinition definition)) continue;
                foreach (DisplaySlotDefinition slot in definition.DisplaySlots)
                    if (slot != null && store.TryGetDisplayedInventory(id, slot.SlotId, out string productId, out int quantity)
                        && productId == product.ProductId) display += quantity;
            }
            return new StoreDebugBalances(store.StoreInventory.GetQuantity(product), store.ShelfInventory.GetTotalStored(product), display);
        }
    }

    public sealed class StoreDebugWindow : EditorWindow
    {
        private int storeIndex, productIndex, storageIndex, levelIndex, furnitureIndex, slotIndex, units = 1;
        private Vector2 scroll;
        private string status;
        private MessageType statusType = MessageType.Info;
        private bool storageExpanded, displayExpanded, advancedExpanded;
        private readonly Dictionary<string, bool> expandedStorage = new(), expandedDisplay = new();

        [MenuItem("Tools/Material City Tycoon/Store Debug")]
        public static void Open() => GetWindow<StoreDebugWindow>("Store Debug");
        [MenuItem("Tools/Material City Tycoon/Store Debug", true)]
        private static bool CanOpen() => EditorApplication.isPlaying;
        private void OnInspectorUpdate() => Repaint();

        private void OnGUI()
        {
            if (!EditorApplication.isPlaying)
            { EditorGUILayout.HelpBox("Disponível somente em Play Mode.", MessageType.Info); return; }
            var stores = UnityEngine.Object.FindObjectsByType<FurniturePlacementController>(FindObjectsInactive.Include)
                .OrderBy(store => store.gameObject.scene.path, StringComparer.Ordinal)
                .ThenBy(store => store.name, StringComparer.Ordinal).ThenBy(store => store.GetEntityId().ToString()).ToArray();
            if (stores.Length == 0) { EditorGUILayout.HelpBox("Nenhuma loja encontrada.", MessageType.Warning); return; }
            storeIndex = EditorGUILayout.Popup("Loja", Clamp(storeIndex, stores.Length),
                stores.Select(store => $"{store.name} — {store.gameObject.scene.name} ({store.GetEntityId().ToString()})").ToArray());
            FurniturePlacementController controller = stores[storeIndex];
            List<ProductDefinition> products = new(); controller.GetConfiguredProductDefinitions(products);
            List<string> furnitureIds = new(); controller.GetPlacedFurnitureIds(furnitureIds);
            scroll = EditorGUILayout.BeginScrollView(scroll);
            DrawBalances(controller, products);
            DrawStorageDetails(controller);
            DrawDisplayDetails(controller, furnitureIds);
            DrawOperations(controller, products, furnitureIds);
            EditorGUILayout.Space();
            bool verbose = EditorGUILayout.ToggleLeft("Logs detalhados de layout e visualização", FurnitureInspectionPanel.VerboseDiagnostics);
            FurnitureInspectionPanel.VerboseDiagnostics = verbose;
            ShelfProductVisualPresenter.VerboseDiagnostics = verbose;
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, statusType);
            EditorGUILayout.EndScrollView();
        }

        private static void DrawBalances(FurniturePlacementController controller, List<ProductDefinition> products)
        {
            EditorGUILayout.LabelField("Saldos por produto", EditorStyles.boldLabel);
            if (products.Count == 0) EditorGUILayout.HelpBox("Nenhum produto válido configurado na loja.", MessageType.Info);
            foreach (ProductDefinition product in products)
            {
                StoreDebugBalances balances = StoreDebugData.GetBalances(controller, product);
                EditorGUILayout.LabelField(product.DisplayName, $"Reserva {balances.Reserve}  |  Depósito {balances.Storage}  |  Exposição {balances.Display}  |  Total {balances.Total}");
            }
        }

        private void DrawStorageDetails(FurniturePlacementController controller)
        {
            storageExpanded = EditorGUILayout.Foldout(storageExpanded, "Depósitos por estante e nível", true);
            if (!storageExpanded) return;
            foreach (StorageLocation location in controller.StorageLocations.OrderBy(item => item.Id, StringComparer.Ordinal))
            {
                expandedStorage.TryGetValue(location.Id, out bool open);
                open = EditorGUILayout.Foldout(open, $"{location.DisplayName} ({location.Id}) — {location.Type}", true);
                expandedStorage[location.Id] = open;
                if (!open) continue;
                EditorGUI.indentLevel++;
                if (controller.TryGetPlacedFurnitureDefinition(location.Id, out FurnitureDefinition furniture)
                    && furniture.StorageCapacity != null && furniture.StorageCapacity.IsValid())
                {
                    StorageCapacityDefinition capacity = furniture.StorageCapacity;
                    foreach (StorageLevelDefinition level in capacity.Levels)
                    {
                        capacity.TryGetCapacity(level.LevelId, capacity.Product, out int maximum);
                        int quantity = controller.ShelfInventory.GetQuantity(location.Id, level.LevelId, capacity.Product);
                        EditorGUILayout.LabelField($"{level.DisplayName} ({level.LevelId})", $"{capacity.Product.DisplayName}: {quantity} / {maximum}");
                    }
                }
                else EditorGUILayout.LabelField("Definição de capacidade indisponível ou inválida.");
                EditorGUI.indentLevel--;
            }
        }

        private void DrawDisplayDetails(FurniturePlacementController controller, List<string> ids)
        {
            displayExpanded = EditorGUILayout.Foldout(displayExpanded, "Exposição por móvel e slot", true);
            if (!displayExpanded) return;
            foreach (string id in ids)
            {
                if (!controller.TryGetPlacedFurnitureDefinition(id, out FurnitureDefinition furniture) || furniture.DisplaySlots.Count == 0) continue;
                expandedDisplay.TryGetValue(id, out bool open);
                open = EditorGUILayout.Foldout(open, $"{furniture.DisplayName} ({id})", true); expandedDisplay[id] = open;
                if (!open) continue;
                EditorGUI.indentLevel++;
                foreach (DisplaySlotDefinition slot in furniture.DisplaySlots)
                {
                    if (slot == null) continue;
                    bool assigned = controller.TryGetAssignedProductForDisplaySlot(id, slot.SlotId, out ProductDefinition product);
                    bool hasStock = controller.TryGetDisplayedInventory(id, slot.SlotId, out string productId, out int quantity);
                    string name = assigned ? product.DisplayName : "Nenhum produto";
                    if (hasStock && (!assigned || productId != product.ProductId)) name += $"; estoque: {productId}";
                    string capacity = slot.HasConfiguredCapacity ? $" / {slot.CapacityUnits}" : " / não configurada";
                    EditorGUILayout.LabelField($"{slot.DisplayName} ({slot.SlotId})", $"{name}: {quantity}{capacity}");
                }
                EditorGUI.indentLevel--;
            }
        }

        private void DrawOperations(FurniturePlacementController controller, List<ProductDefinition> products, List<string> ids)
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Operações de teste", EditorStyles.boldLabel);
            if (products.Count == 0) return;
            productIndex = EditorGUILayout.Popup("Produto", Clamp(productIndex, products.Count), products.Select(p => p.DisplayName).ToArray());
            ProductDefinition product = products[productIndex];
            units = EditorGUILayout.IntField("Quantidade", units);
            if (GUILayout.Button("Adicionar estoque de teste → Reserva"))
                Result(controller.StoreInventory.TryAddUnits(product, units), "Estoque adicionado à reserva.");

            List<StorageLocation> storages = controller.StorageLocations.Where(location =>
                controller.TryGetFurnitureStorageLocation(location.Id, out _)
                && controller.TryGetPlacedFurnitureDefinition(location.Id, out FurnitureDefinition def)
                && def.StorageCapacity != null && def.StorageCapacity.IsValid() && def.StorageCapacity.Product == product)
                .OrderBy(location => location.Id, StringComparer.Ordinal).ToList();
            StorageLocation selectedStorage = null;
            StorageLevelDefinition selectedLevel = null;
            if (storages.Count > 0)
            {
                storageIndex = EditorGUILayout.Popup("Depósito", Clamp(storageIndex, storages.Count), storages.Select(s => $"{s.DisplayName} ({s.Id})").ToArray());
                selectedStorage = storages[storageIndex];
                controller.TryGetPlacedFurnitureDefinition(selectedStorage.Id, out FurnitureDefinition storageDefinition);
                var levels = storageDefinition.StorageCapacity.Levels;
                levelIndex = EditorGUILayout.Popup("Nível", Clamp(levelIndex, levels.Count), levels.Select(l => $"{l.DisplayName} ({l.LevelId})").ToArray());
                selectedLevel = levels[levelIndex];
            }
            else EditorGUILayout.HelpBox("Nenhum depósito válido para este produto.", MessageType.Info);
            using (new EditorGUI.DisabledScope(selectedStorage == null))
            {
                if (GUILayout.Button("Reserva → Depósito"))
                    Result(controller.ShelfInventory.TryTransferToStorage(selectedStorage.Id, selectedLevel.LevelId, product, units), "Transferência para depósito concluída.");
                if (GUILayout.Button("Depósito → Reserva"))
                    Result(controller.ShelfInventory.TryReturnToReserve(selectedStorage.Id, selectedLevel.LevelId, product, units), "Devolução à reserva concluída.");
            }
            List<string> displays = ids.Where(id => controller.TryGetPlacedFurnitureDefinition(id, out FurnitureDefinition f)
                && f.DisplaySlots.Count > 0).ToList();
            string displayId = null;
            DisplaySlotDefinition selectedSlot = null;
            if (displays.Count > 0)
            {
                furnitureIndex = EditorGUILayout.Popup("Móvel de exposição", Clamp(furnitureIndex, displays.Count), displays.Select(id =>
                { controller.TryGetPlacedFurnitureDefinition(id, out FurnitureDefinition f); return $"{f.DisplayName} ({id})"; }).ToArray());
                displayId = displays[furnitureIndex];
                controller.TryGetPlacedFurnitureDefinition(displayId, out FurnitureDefinition definition);
                var slots = definition.DisplaySlots.Where(s => s != null && s.IsValid(out _)).ToList();
                if (slots.Count > 0)
                {
                    slotIndex = EditorGUILayout.Popup("Slot", Clamp(slotIndex, slots.Count), slots.Select(s => $"{s.DisplayName} ({s.SlotId})").ToArray());
                    selectedSlot = slots[slotIndex];
                    bool assigned = controller.TryGetAssignedProductForDisplaySlot(displayId, selectedSlot.SlotId, out ProductDefinition actual);
                    EditorGUILayout.LabelField("Produto atribuído", assigned ? actual.DisplayName : "Nenhum produto");
                }
            }
            else EditorGUILayout.HelpBox("Nenhum móvel de exposição construído.", MessageType.Info);
            bool matches = selectedSlot != null && controller.TryGetAssignedProductForDisplaySlot(displayId, selectedSlot.SlotId, out ProductDefinition assignedProduct) && assignedProduct == product;
            using (new EditorGUI.DisabledScope(selectedStorage == null || !matches))
                if (GUILayout.Button("Depósito → Exposição"))
                    Result(controller.TryRestockFromStorage(selectedStorage.Id, selectedLevel.LevelId, displayId, selectedSlot.SlotId, product, units), "Reposição concluída.");
            using (new EditorGUI.DisabledScope(!matches))
                if (GUILayout.Button("Exposição → Reserva"))
                    Result(controller.TryTransferDisplayToReserve(displayId, selectedSlot.SlotId, units), "Devolução à reserva concluída.");
            advancedExpanded = EditorGUILayout.Foldout(advancedExpanded, "Avançado — bypass do depósito", true);
            if (advancedExpanded)
            {
                EditorGUILayout.HelpBox("Atalho legado: transfere diretamente da reserva para a exposição, sem passar pelo depósito.", MessageType.Info);
                using (new EditorGUI.DisabledScope(!matches))
                    if (GUILayout.Button("Reserva → Exposição (bypass)"))
                        Result(controller.TryTransferReserveToDisplay(displayId, selectedSlot.SlotId, units), "Transferência direta concluída.");
            }
        }

        private void Result(bool succeeded, string message)
        {
            status = succeeded ? message : "Operação recusada. Confira quantidade, saldo, produto atribuído e capacidade; os saldos foram preservados.";
            statusType = succeeded ? MessageType.Info : MessageType.Warning;
            Repaint();
        }
        private static int Clamp(int index, int count) => count == 0 ? 0 : Mathf.Clamp(index, 0, count - 1);
    }
}
