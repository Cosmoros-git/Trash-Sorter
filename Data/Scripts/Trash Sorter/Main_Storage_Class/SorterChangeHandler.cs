using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using Trash_Sorter.Data.Scripts.Trash_Sorter.ActiveClasses.Mod_Sorter;
using Trash_Sorter.Data.Scripts.Trash_Sorter.BaseClass;
using VRage;
using VRage.Game;

namespace Trash_Sorter.Data.Scripts.Trash_Sorter.Main_Storage_Class
{
    internal class SorterChangeHandler : ModBase
    {
        public Dictionary<MyDefinitionId, SorterLimitManager> FilterDictionary;
        private readonly ObservableDictionary<MyDefinitionId> itemQuantityDictionary;

        // This is what makes the freaking mod not be a reason your sim speed dies. It allows for fast and pretty efficient data access with around O(1) speeds. 
        public SorterChangeHandler(MainItemStorage mainItemStorage)
        {
            FilterDictionary = new Dictionary<MyDefinitionId, SorterLimitManager>(mainItemStorage.NameToDefinition.Count);

            foreach (var definitionId in mainItemStorage.ProcessedItems)
            {
                FilterDictionary[definitionId] = new SorterLimitManager(definitionId);
            }

            itemQuantityDictionary = mainItemStorage.ItemsDictionary;
            itemQuantityDictionary.OnValueChanged += OnItemQuantityChanged;
        }

        // Called when main storage value gets updated. Maybe should be made to happen on AfterSim 1 or 10. Amount of 0 value changes are quite... a bit.
        private void OnItemQuantityChanged(MyDefinitionId definitionId, MyFixedPoint newQuantity)
        {
            SorterLimitManager filterItem;
            if (FilterDictionary.TryGetValue(definitionId, out filterItem))
            {
                filterItem.OnValueChange(newQuantity);
            }
        }

        // No memory leak
        public override void Dispose()
        {
            base.Dispose();
            itemQuantityDictionary.OnValueChanged -= OnItemQuantityChanged;
        }
    }
}