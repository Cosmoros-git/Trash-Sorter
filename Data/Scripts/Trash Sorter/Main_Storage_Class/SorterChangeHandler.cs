using System;
using System.Collections.Generic;
using System.Text;
using Sandbox.ModAPI;
using Trash_Sorter.Data.Scripts.Trash_Sorter.ActiveClasses.Mod_Sorter;
using Trash_Sorter.Data.Scripts.Trash_Sorter.BaseClass;
using VRage;
using VRage.Game;

namespace Trash_Sorter.Data.Scripts.Trash_Sorter.Main_Storage_Class
{
    /// <summary>
    /// The SorterChangeHandler class manages changes in item quantities and updates associated sorter limits. 
    /// It efficiently tracks item quantities in a dictionary and applies changes to sorter filters with minimal performance impact.
    /// </summary>
    internal class SorterChangeHandler : ModBase
    {
        /// <summary>
        /// Dictionary that maps item definitions to their corresponding SorterLimitManager, which manages the sorting limits for each item.
        /// </summary>
        public Dictionary<MyDefinitionId, SorterLimitManager> SorterLimitManagers;

        /// <summary>
        /// Observable dictionary that tracks the quantity of each item in the system.
        /// </summary>
        private readonly ObservableDictionary<MyDefinitionId> ItemQuantities;


        /// <summary>
        /// Tracks items changes for batch processing
        /// </summary>
        public Dictionary<MyDefinitionId, MyFixedPoint> PendingItemChanges;

        /// <summary>
        /// Initializes a new instance of the SorterChangeHandler class, setting up the filter dictionary and subscribing to item quantity changes.
        /// </summary>
        /// <param name="mainItemStorage">The main item storage that holds the item definitions and quantities.</param>
        public SorterChangeHandler(MainItemStorage mainItemStorage)
        {
            SorterLimitManagers =
                new Dictionary<MyDefinitionId, SorterLimitManager>(mainItemStorage.NameToDefinitionMap.Count);

            foreach (var definitionId in mainItemStorage.ProcessedItems)
            {
                SorterLimitManagers[definitionId] = new SorterLimitManager(definitionId);
            }

            PendingItemChanges = new Dictionary<MyDefinitionId, MyFixedPoint>();
            ItemQuantities = mainItemStorage.ItemsDictionary;
            ItemQuantities.OnValueChanged += OnItemQuantityChanged;
        }

        /// <summary>
        /// Called whenever the item quantity changes in the main storage. Adds values into batch updates.
        /// </summary>
        /// <param name="definitionId">The definition ID of the item that has changed.</param>
        /// <param name="newQuantity">The new quantity of the item.</param>
        private void OnItemQuantityChanged(MyDefinitionId definitionId, MyFixedPoint newQuantity)
        {
            // Accumulate the new quantity change into the dictionary
            if (PendingItemChanges.ContainsKey(definitionId))
            {
                var quantity = PendingItemChanges[definitionId] += newQuantity; // Accumulate changes for the same item

                // Remove the entry if the accumulated quantity becomes zero
                if (quantity == 0)
                {
                    PendingItemChanges.Remove(definitionId);
                }
            }
            else
            {
                PendingItemChanges[definitionId] = newQuantity; // Add new entry if it doesn't exist
            }
        }

        public void OnAfterSimulation100()
        {
            // Make a shallow copy of the dictionary to iterate over
            var pendingChangesCopy = new Dictionary<MyDefinitionId, MyFixedPoint>(PendingItemChanges);
            var stringBuild = new StringBuilder();
            // Clear the original pending changes dictionary before processing
            PendingItemChanges.Clear();

            // Process all pending item changes from the copied dictionary
            foreach (var kvp in pendingChangesCopy)
            {
                SorterLimitManager sorterLimitManager;
                if (!SorterLimitManagers.TryGetValue(kvp.Key, out sorterLimitManager)) continue;

                // Apply the accumulated value change
                sorterLimitManager.OnValueChange(kvp.Value);
                stringBuild.Append(kvp.Key + "|" + kvp.Value);
            }
        }



        /// <summary>
        /// Unsubscribes from events and disposes of the object to prevent memory leaks.
        /// </summary>
        public override void Dispose()
        {
            base.Dispose();
            ItemQuantities.OnValueChanged -= OnItemQuantityChanged;
        }
    }
}