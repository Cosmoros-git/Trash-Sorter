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
        private readonly ObservableDictionary<MyDefinitionId, MyFixedPoint> itemQuantityDictionary;

        public SorterChangeHandler(MainStorageClass mainStorage)
        {
            FilterDictionary = new Dictionary<MyDefinitionId, SorterLimitManager>(mainStorage.NameToDefinition.Count);

            foreach (var definitionId in mainStorage.ProcessedItems)
            {
                FilterDictionary[definitionId] = new SorterLimitManager(definitionId);
            }

            itemQuantityDictionary = mainStorage.ItemsDictionary;
            itemQuantityDictionary.OnValueChanged += OnItemQuantityChanged;
        }

        private void OnItemQuantityChanged(MyDefinitionId definitionId, MyFixedPoint newQuantity)
        {
            SorterLimitManager filterItem;
            if (FilterDictionary.TryGetValue(definitionId, out filterItem))
            {
                filterItem.OnValueChange(newQuantity);
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            itemQuantityDictionary.OnValueChanged -= OnItemQuantityChanged;
        }
    }
}