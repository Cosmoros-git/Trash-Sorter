using System;
using System.Collections.Generic;
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

            ItemQuantities = mainItemStorage.ItemsDictionary;
            ItemQuantities.OnValueChanged += OnItemQuantityChanged;
        }

        /// <summary>
        /// Called whenever the item quantity changes in the main storage. Updates the corresponding SorterLimitManager.
        /// </summary>
        /// <param name="definitionId">The definition ID of the item that has changed.</param>
        /// <param name="newQuantity">The new quantity of the item.</param>
        private void OnItemQuantityChanged(MyDefinitionId definitionId, MyFixedPoint newQuantity)
        {
            SorterLimitManager sorterLimitManager;
            if (SorterLimitManagers.TryGetValue(definitionId, out sorterLimitManager))
            {
                sorterLimitManager.OnValueChange(newQuantity);
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