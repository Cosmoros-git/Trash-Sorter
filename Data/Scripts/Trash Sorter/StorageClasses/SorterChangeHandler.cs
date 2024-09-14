using System.Collections.Generic;
using Trash_Sorter.BaseClass;
using Trash_Sorter.SorterClasses;
using VRage.Game;

namespace Trash_Sorter.StorageClasses
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
        public HashSet<MyDefinitionId> PendingItemChanges;


        /// <summary>
        /// Initializes a new instance of the SorterChangeHandler class, setting up the filter dictionary and subscribing to item quantity changes.
        /// </summary>
        /// <param name="mainItemStorage">The main item storage that holds the item definitions and quantities.</param>
        public SorterChangeHandler(ItemStorage mainItemStorage)
        {
            PendingItemChanges = new HashSet<MyDefinitionId>();
            ItemQuantities = mainItemStorage.ItemsDictionary;
            SorterLimitManagers = new Dictionary<MyDefinitionId, SorterLimitManager>(mainItemStorage.NameToDefinitionMap.Count);
            foreach (var definitionId in mainItemStorage.ProcessedItems)
            {
                FixedPointReference value;
                ItemQuantities.TryGetValue(definitionId, out value);
                SorterLimitManagers[definitionId] = new SorterLimitManager(definitionId, value);
            }
            ItemQuantities.OnValueChanged += OnItemAmountChanged;
          
        }

     

        /// <summary>
        /// Called whenever the item quantity changes in the main storage. Adds values into batch updates.
        /// </summary>
        /// <param name="definitionId">The definition ID of the item that has changed.</param>
        /// <param name="newQuantity">The new quantity of the item.</param>
        private void OnItemAmountChanged(MyDefinitionId definitionId)
        {
            // Accumulate the new quantity change into the dictionary
            PendingItemChanges.Add(definitionId);
        }

        public void OnAfterSimulation100()
        {
            // Make a shallow copy of the dictionary to iterate over
            var pendingChangesCopy = new HashSet<MyDefinitionId>(PendingItemChanges);
            // Clear the original pending changes dictionary before processing
            PendingItemChanges.Clear();

            // Process all pending item changes from the copied dictionary
            foreach (var myDefId in pendingChangesCopy)
            {
                SorterLimitManager sorterLimitManager;
                if (!SorterLimitManagers.TryGetValue(myDefId, out sorterLimitManager)) continue;
                sorterLimitManager.OnValueChange();
            }
        }



        /// <summary>
        /// Unsubscribes from events and disposes of the object to prevent memory leaks.
        /// </summary>
        public override void Dispose()
        {
            base.Dispose();
            ItemQuantities.OnValueChanged -= OnItemAmountChanged;
        }
    }
}