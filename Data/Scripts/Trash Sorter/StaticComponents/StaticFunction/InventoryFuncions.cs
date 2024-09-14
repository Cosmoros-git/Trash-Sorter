using Sandbox.Game;
using SharpDX.Toolkit.Collections;
using System.Collections.Generic;
using Trash_Sorter.StorageClasses;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI.Ingame;

namespace Trash_Sorter.StaticComponents.StaticFunction
{
    public static class InventoryFunctions
    {
        public static void ScanInventory(IMyInventory inventoryToScan, List<MyInventoryItem> items, HashSet<MyDefinitionId> filterList = null)
        {
            inventoryToScan?.GetItems(items, filterList);
        }

      
        public static void ScanInventory(IMyInventory inventoryToScan, List<MyInventoryItem> items, ref HashSet<MyDefinitionId> itemDefinitions)
        {
            if (inventoryToScan == null || itemDefinitions == null) return;
            ScanInventory(inventoryToScan, items);
            foreach (var item in items)
            {

                itemDefinitions.Add(item.Type);
            }
        }

      
        public static void ScanInventory(IMyInventory inventoryToScan, List<MyInventoryItem> items, HashSet<MyDefinitionId> filteredIds,
            ref Dictionary<MyDefinitionId, MyFixedPoint> definitionWithValues)
        {
            if (inventoryToScan == null || filteredIds == null || definitionWithValues == null || filteredIds.Count == 0) return;
            ScanInventory(inventoryToScan, items);
            foreach (var item in items)
            {
                var defItem = item.;
                if (!filteredIds.Contains(defItem)) continue;

                MyFixedPoint value;
                if (definitionWithValues.TryGetValue(defItem, out value))
                {
                    definitionWithValues[defItem] += item.Amount;
                }
                else
                {
                    definitionWithValues[defItem] = item.Amount;
                }
            }
        }

        
        public static void ScanInventory(IMyInventory inventoryToScan,
            ref ObservableDictionary<MyDefinitionId> observableModDictionary)
        {
            if (inventoryToScan == null || observableModDictionary == null) return;

            foreach (var item in ScanInventory(inventoryToScan))
            {
                var defItem = item.GetDefinitionId();
                FixedPointReference value;
                if (observableModDictionary.TryGetValue(defItem, out value))
                {
                    value.ItemAmount += item.Amount;
                }
            }
        }
    }
}