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

    // Redirect previously saved Editor layouts to the consolidated window.
    public sealed class InventoryDebugWindow : EditorWindow
    {
        private void OnEnable()
        {
            if (EditorApplication.isPlaying) StoreDebugWindow.Open();
            Close();
        }
    }
}
