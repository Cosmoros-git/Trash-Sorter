using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game;
using Trash_Sorter.StorageClasses;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;

namespace Trash_Sorter.StaticComponents.StaticFunctions
{
    public static class InventoryFunctions
    {
        public static IEnumerable<MyPhysicalInventoryItem> ScanInventory(IMyInventory inventoryToScan,
            HashSet<MyDefinitionId> filterList = null)
        {
            if (inventoryToScan == null) yield break; // Exit early if inventory is null

            var items = ((MyInventory)inventoryToScan).GetItems();

            // If there's no filter (filterList is null), return all items
            foreach (var item in items)
            {
                if (filterList == null || filterList.Contains(item.Content.GetId()))
                {
                    yield return item; // Return all items or only filtered items
                }
            }
        }
        
        public static void GetInventoryItemDefinitions(IMyInventory inventoryToScan,
            HashSet<MyDefinitionId> itemDefinitions)
        {
            if (inventoryToScan == null || itemDefinitions == null) return;

            foreach (var item in ScanInventory(inventoryToScan))
            {
                var defId = item.Content.GetId();
                itemDefinitions.Add(defId);
            }
        }

        public static void GetDictionaryDefIdFixedPoint(IMyInventory inventoryToScan,
            HashSet<MyDefinitionId> filteredIds, IDictionary<MyDefinitionId, MyFixedPoint> definitionWithValues)
        {
            if (inventoryToScan == null || filteredIds == null || definitionWithValues == null ||
                filteredIds.Count == 0)
                return;

            foreach (var item in ScanInventory(inventoryToScan, filteredIds))
            {
                var defItem = item.Content.GetId();

                MyFixedPoint existingValue;
                if (definitionWithValues.TryGetValue(defItem, out existingValue))
                {
                    definitionWithValues[defItem] = existingValue + item.Amount;
                }
                else
                {
                    definitionWithValues[defItem] = item.Amount;
                }
            }
        }


        public static void GetDictionaryDefIdFixedPoint(IMyCubeBlock block, HashSet<MyDefinitionId> filteredIds,
            Dictionary<MyDefinitionId, MyFixedPoint> definitionWithValues)
        {
            ProcessAllInventories(block, (inventory) => GetDictionaryDefIdFixedPoint(inventory, filteredIds, definitionWithValues));
        }


        public static void ProcessInventory(IMyCubeBlock block, Action<IMyInventory> process)
        {
            if (block == null || block.InventoryCount == 0) return;

            for (var i = 0; i < block.InventoryCount; i++)
            {
                var inventory = block.GetInventory(i);
                if (inventory != null)
                {
                    process(inventory);
                }
            }
        }
        private static void ProcessAllInventories(IMyCubeBlock block, Action<IMyInventory> action)
        {
            if (block == null || block.InventoryCount == 0) return;

            for (var i = 0; i < block.InventoryCount; i++)
            {
                var inventory = block.GetInventory(i);
                if (inventory != null) // Ensure that the inventory is not null
                {
                    action(inventory); // Perform the action for each inventory
                }
            }
        }






        public static void ScanInventoryUsingMyModDictionary<T>(T block, ObservableDictionary<MyDefinitionId> observableModDictionary, int multiplier = 1) where T : IMyCubeBlock
        {
            ProcessAllInventories(block, (inventory) => ScanInventoryUsingMyModDictionary(inventory, observableModDictionary, multiplier));
        }
        public static void ScanInventoryUsingMyModDictionary(IMyInventory inventoryToScan, ObservableDictionary<MyDefinitionId> observableModDictionary, int multiplier = 1)
        {
            if (inventoryToScan == null || observableModDictionary == null) return;

            var filter = observableModDictionary.Keys.ToHashSet();

            foreach (var item in ScanInventory(inventoryToScan, filter))
            {
                FixedPointReference value;
                if (observableModDictionary.TryGetValue(item.Content.GetId(), out value))
                {
                    value.ItemAmount += item.Amount * multiplier;
                }
            }
        }
    }
}