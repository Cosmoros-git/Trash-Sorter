using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using Trash_Sorter.Data.Scripts.Trash_Sorter.BaseClass;
using VRage.Game;

namespace Trash_Sorter.Data.Scripts.Trash_Sorter.ActiveClasses.Mod_Conveyor_Sorter_Manager
{
    internal class ModFilterCollection : ModBase
    {
        private readonly HashSet<MyInventoryItemFilter> myInventory_filter;
        private readonly IMyConveyorSorter _myConveyorSorter;
        private HashSet<ModFilterItem> _items;

        public ModFilterCollection(IMyConveyorSorter myConveyorSorter, HashSet<ModFilterItem> items)
        {
            _myConveyorSorter = myConveyorSorter;
            _items = items ?? new HashSet<ModFilterItem>();
            myInventory_filter = new HashSet<MyInventoryItemFilter>();
            foreach (var tulip in _items)
            {
                tulip.OnItemOverLimit += Add_Filter_Item;
                tulip.OnItemBelowLimit += Remove_Filter_Item;
            }

            Parse_To_Filters();
        }


        public HashSet<ModFilterItem> Items
        {
            get { return _items; }
            set
            {
                // Remove Tulips that are not in the new set
                var itemsToRemove = _items.Except(value).ToList();
                foreach (var item in itemsToRemove)
                {
                    Remove_Tulip_Filter(item);
                }

                // Replace the _items set with the new one
                _items = value;

                // Update the sorter filter only once after all removals
                Update_Sorter_Filter();

                // Parse the new items to update filters if necessary
                Parse_To_Filters();
            }
        }


        public void Add_Filter_Item(MyDefinitionId definitionId)
        {
            if (myInventory_filter.Add(new MyInventoryItemFilter(definitionId)))
            {
                Update_Sorter_Filter();
            }
        }

        public void Remove_Filter_Item(MyDefinitionId definitionId)
        {
            if (myInventory_filter.Remove(new MyInventoryItemFilter(definitionId)))
            {
                Update_Sorter_Filter();
            }
        }

        public void Remove_Tulip_Filter(ModFilterItem item)
        {
            if (!_items.Contains(item)) return;
            _items.Remove(item);
            item.OnItemBelowLimit -= Remove_Filter_Item;
            item.OnItemOverLimit -= Add_Filter_Item;
        }

        public void Add_Tulip_Filter(ModFilterItem item)
        {
            if (!_items.Add(item)) return;
            item.OnItemBelowLimit += Remove_Filter_Item;
            item.OnItemOverLimit += Add_Filter_Item;
        }

        private void Update_Sorter_Filter()
        {
            _myConveyorSorter.SetFilter(MyConveyorSorterMode.Whitelist, myInventory_filter.ToList());
            _myConveyorSorter.DrainAll = true;
        }

        private void Parse_To_Filters()
        {
            foreach (var modFilterItem in _items)
            {
                if (modFilterItem.ItemMaxLimit == 0 && modFilterItem.ItemRequestedLimit == 0) return;
                if (modFilterItem.ItemRequestedLimit < 0)
                {
                    if (myInventory_filter.Contains(modFilterItem.ItemId)) return;
                    Add_Filter_Item(modFilterItem.ItemId);
                }

                if (modFilterItem.ItemAmount > modFilterItem.ItemMaxLimit)
                {
                    Add_Filter_Item(modFilterItem.ItemId);
                }
            }
        }

        public override void Dispose()
        {
            foreach (var item in _items)
            {
                item.OnItemBelowLimit -= Remove_Filter_Item;
                item.OnItemOverLimit -= Add_Filter_Item;
            }
        }
    }

}