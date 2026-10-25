using System;
using System.Collections.Generic;
using MaterialCityTycoon.Building;
using MaterialCityTycoon.Commerce;
using MaterialCityTycoon.EditorTests;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MaterialCityTycoon.EditorTools
{
    public static class ValidateDisplaySlotCompatibilityMenu
    {
        [MenuItem("Tools/Material City Tycoon/Validate Product Slot Compatibility")]
        public static void ValidateProductSlotCompatibility()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogError("Exit Play Mode before validating product and slot compatibility.");
                return;
            }

            int passedScenarios = 0;
            int failedScenarios = 0;
            RunScenario(
                "Initial product and slot definitions",
                tests => tests.InitialDefinitionsMatchTheirExpectedSlotModes(),
                ref passedScenarios,
                ref failedScenarios);
            RunScenario(
                "Product with multiple display modes",
                tests => tests.ProductWithMultipleModesIsCompatibleWhenAnyModeIntersects(),
                ref passedScenarios,
                ref failedScenarios);
            RunScenario(
                "Null and invalid definitions",
                tests => tests.NullOrInvalidDefinitionsAreIncompatible(),
                ref passedScenarios,
                ref failedScenarios);

            if (failedScenarios == 0)
            {
                Debug.Log($"Product-slot compatibility validation passed: {passedScenarios} scenarios.");
                return;
            }

            Debug.LogError(
                $"Product-slot compatibility validation failed: {failedScenarios} of {passedScenarios + failedScenarios} scenarios.");
        }

        private static void RunScenario(
            string scenarioName,
            Action<DisplaySlotCompatibilityTests> validation,
            ref int passedScenarios,
            ref int failedScenarios)
        {
            DisplaySlotCompatibilityTests tests = new();
            try
            {
                validation(tests);
                passedScenarios++;
                Debug.Log($"Product-slot compatibility validation passed: {scenarioName}.");
            }
            catch (AssertionException exception)
            {
                failedScenarios++;
                Debug.LogError($"Product-slot compatibility validation failed: {scenarioName}. {exception.Message}");
            }
            catch (Exception exception)
            {
                failedScenarios++;
                Debug.LogException(exception);
            }
            finally
            {
                tests.TearDown();
            }
        }
    }

    /// <summary>
    /// Provides a Play Mode-only development surface for exercising the runtime inventory APIs.
    /// </summary>
    public sealed class InventoryDebugWindow : EditorWindow
    {
        private readonly List<ProductDefinition> productDefinitions = new();
        private readonly List<string> furnitureIds = new();
        private readonly List<DisplaySlotDefinition> displaySlots = new();

        private FurniturePlacementController placementController;
        private int selectedProductIndex;
        private int selectedFurnitureIndex;
        private int selectedSlotIndex;
        private int reserveUnits = 1;
        private int transferUnits = 1;

        [MenuItem("Tools/Material City Tycoon/Inventory Debug")]
        private static void Open()
        {
            GetWindow<InventoryDebugWindow>("Inventory Debug");
        }

        [MenuItem("Tools/Material City Tycoon/Inventory Debug", true)]
        private static bool ValidateOpen()
        {
            return EditorApplication.isPlaying;
        }

        private void OnGUI()
        {
            if (!EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play Mode to inspect runtime inventory.", MessageType.Info);
                return;
            }

            if (!TryGetPlacementController())
            {
                EditorGUILayout.HelpBox(
                    "Exactly one active FurniturePlacementController is required in the loaded scene.",
                    MessageType.Warning);
                return;
            }

            RefreshSelections();
            DrawReserveControls();
            EditorGUILayout.Space();
            DrawDisplayControls();
        }

        private bool TryGetPlacementController()
        {
            if (placementController != null)
            {
                return true;
            }

            FurniturePlacementController[] controllers = UnityEngine.Object.FindObjectsByType<FurniturePlacementController>(
                FindObjectsInactive.Include);
            if (controllers.Length != 1)
            {
                return false;
            }

            placementController = controllers[0];
            return true;
        }

        private void RefreshSelections()
        {
            placementController.GetConfiguredProductDefinitions(productDefinitions);
            selectedProductIndex = ClampIndex(selectedProductIndex, productDefinitions.Count);

            placementController.GetPlacedFurnitureIds(furnitureIds);
            selectedFurnitureIndex = ClampIndex(selectedFurnitureIndex, furnitureIds.Count);

            displaySlots.Clear();
            if (furnitureIds.Count > 0
                && placementController.TryGetPlacedFurnitureDefinition(
                    furnitureIds[selectedFurnitureIndex],
                    out FurnitureDefinition definition))
            {
                foreach (DisplaySlotDefinition slot in definition.DisplaySlots)
                {
                    if (slot != null && slot.IsValid(out _))
                    {
                        displaySlots.Add(slot);
                    }
                }
            }

            selectedSlotIndex = ClampIndex(selectedSlotIndex, displaySlots.Count);
        }

        private void DrawReserveControls()
        {
            EditorGUILayout.LabelField("Store reserve", EditorStyles.boldLabel);
            if (productDefinitions.Count == 0)
            {
                EditorGUILayout.HelpBox("No valid configured product definitions were found.", MessageType.Warning);
                return;
            }

            selectedProductIndex = EditorGUILayout.Popup(
                "Product",
                selectedProductIndex,
                BuildProductLabels());
            reserveUnits = EditorGUILayout.IntField("Units to add", reserveUnits);

            ProductDefinition productDefinition = productDefinitions[selectedProductIndex];
            EditorGUILayout.LabelField(
                "Reserve balance",
                placementController.StoreInventory.GetQuantity(productDefinition).ToString());

            using (new EditorGUI.DisabledScope(reserveUnits <= 0))
            {
                if (GUILayout.Button("Add to reserve"))
                {
                    LogOperation(
                        placementController.StoreInventory.TryAddUnits(productDefinition, reserveUnits),
                        $"Added {reserveUnits} unit(s) of '{productDefinition.DisplayName}' to the reserve.");
                }
            }
        }

        private void DrawDisplayControls()
        {
            EditorGUILayout.LabelField("Display slot transfer", EditorStyles.boldLabel);
            if (furnitureIds.Count == 0)
            {
                EditorGUILayout.HelpBox("Place furniture in Play Mode before transferring inventory.", MessageType.Info);
                return;
            }

            selectedFurnitureIndex = EditorGUILayout.Popup(
                "Furniture",
                selectedFurnitureIndex,
                BuildFurnitureLabels());
            if (displaySlots.Count == 0)
            {
                EditorGUILayout.HelpBox("The selected furniture has no valid display slots.", MessageType.Info);
                return;
            }

            selectedSlotIndex = EditorGUILayout.Popup("Slot", selectedSlotIndex, BuildSlotLabels());
            transferUnits = EditorGUILayout.IntField("Units to transfer", transferUnits);

            string furnitureId = furnitureIds[selectedFurnitureIndex];
            DisplaySlotDefinition slotDefinition = displaySlots[selectedSlotIndex];
            bool hasAssignedProduct = placementController.TryGetAssignedProductForDisplaySlot(
                furnitureId,
                slotDefinition.SlotId,
                out ProductDefinition assignedProduct);

            EditorGUILayout.LabelField(
                "Assigned product",
                hasAssignedProduct ? assignedProduct.DisplayName : "None");
            EditorGUILayout.LabelField(
                "Displayed balance",
                placementController.GetDisplayedQuantity(furnitureId, slotDefinition.SlotId).ToString());
            if (hasAssignedProduct)
            {
                EditorGUILayout.LabelField(
                    "Reserve balance",
                    placementController.StoreInventory.GetQuantity(assignedProduct).ToString());
            }

            using (new EditorGUI.DisabledScope(!hasAssignedProduct || transferUnits <= 0))
            {
                if (GUILayout.Button("Transfer reserve to display"))
                {
                    LogOperation(
                        placementController.TryTransferReserveToDisplay(
                            furnitureId,
                            slotDefinition.SlotId,
                            transferUnits),
                        $"Transferred {transferUnits} unit(s) to '{slotDefinition.DisplayName}'.");
                }

                if (GUILayout.Button("Return display to reserve"))
                {
                    LogOperation(
                        placementController.TryTransferDisplayToReserve(
                            furnitureId,
                            slotDefinition.SlotId,
                            transferUnits),
                        $"Returned {transferUnits} unit(s) from '{slotDefinition.DisplayName}' to the reserve.");
                }
            }
        }

        private string[] BuildProductLabels()
        {
            string[] labels = new string[productDefinitions.Count];
            for (int index = 0; index < productDefinitions.Count; index++)
            {
                labels[index] = productDefinitions[index].DisplayName;
            }

            return labels;
        }

        private string[] BuildFurnitureLabels()
        {
            string[] labels = new string[furnitureIds.Count];
            for (int index = 0; index < furnitureIds.Count; index++)
            {
                string furnitureId = furnitureIds[index];
                labels[index] = placementController.TryGetPlacedFurnitureDefinition(
                    furnitureId,
                    out FurnitureDefinition definition)
                    ? $"{definition.DisplayName} ({furnitureId})"
                    : furnitureId;
            }

            return labels;
        }

        private string[] BuildSlotLabels()
        {
            string[] labels = new string[displaySlots.Count];
            for (int index = 0; index < displaySlots.Count; index++)
            {
                DisplaySlotDefinition slot = displaySlots[index];
                labels[index] = $"{slot.DisplayName} ({slot.SlotId})";
            }

            return labels;
        }

        private void LogOperation(bool succeeded, string successMessage)
        {
            if (succeeded)
            {
                Debug.Log(successMessage);
            }
            else
            {
                Debug.LogWarning("Inventory operation was rejected; balances were not changed.");
            }

            Repaint();
        }

        private static int ClampIndex(int index, int count)
        {
            return count == 0 ? 0 : Mathf.Clamp(index, 0, count - 1);
        }
    }
}
