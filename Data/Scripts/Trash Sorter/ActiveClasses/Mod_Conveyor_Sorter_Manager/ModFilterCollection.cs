using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Sandbox.ModAPI;
using Trash_Sorter.Data.Scripts.Trash_Sorter.BaseClass;
using VRage.Game;

namespace Trash_Sorter.Data.Scripts.Trash_Sorter.ActiveClasses.Mod_Conveyor_Sorter_Manager
{
    internal class ModFilterCollection : ModBase
    {
        private readonly HashSet<Sandbox.ModAPI.Ingame.MyInventoryItemFilter> myInventory_filter;
        private readonly IMyConveyorSorter _myConveyorSorter;
        private readonly Dictionary<MyDefinitionId, ModFilterItem> _items;

        public ModFilterCollection(IMyConveyorSorter myConveyorSorter, Dictionary<MyDefinitionId, ModFilterItem> items)
        {
            _myConveyorSorter = myConveyorSorter;
            _items = items?? new Dictionary<MyDefinitionId, ModFilterItem>();


            myInventory_filter = new HashSet<Sandbox.ModAPI.Ingame.MyInventoryItemFilter>();
            foreach (var tulip in _items.Values)
            {
                tulip.OnItemOverLimit += Add_Filter_Item;
                tulip.OnItemBelowLimit += Remove_Filter_Item;
            }

            Parse_To_Filters();
        }


        public Dictionary<MyDefinitionId, ModFilterItem> Items
        {
            get { return _items; }
            set
            {
                if (value == null) return;

                // Remove items that are not in the new set
                var itemsToRemove = _items.Keys.Except(value.Keys).ToList();
                foreach (var key in itemsToRemove)
                {
                    Remove_Tulip_Filter(_items[key]);
                    _items.Remove(key);
                }

                // Add or update items from the new set
                foreach (var kvp in value)
                {
                    if (_items.ContainsKey(kvp.Key))
                    {
                        _items[kvp.Key] = kvp.Value;
                    }
                }

                Update_Sorter_Filter();
                Parse_To_Filters();
            }
        }


        public void Add_Filter_Item(MyDefinitionId definitionId)
        {
            if (myInventory_filter.Add(new Sandbox.ModAPI.Ingame.MyInventoryItemFilter(definitionId)))
            {
                Update_Sorter_Filter();
            }
        }

        public void Remove_Filter_Item(MyDefinitionId definitionId)
        {
            if (myInventory_filter.Remove(new Sandbox.ModAPI.Ingame.MyInventoryItemFilter(definitionId)))
            {
                Update_Sorter_Filter();
            }
        }

        public void Remove_Tulip_Filter(ModFilterItem item)
        {
            if (!_items.ContainsKey(item.ItemId)) return;
            _items.Remove(item.ItemId);
            item.OnItemBelowLimit -= Remove_Filter_Item;
            item.OnItemOverLimit -= Add_Filter_Item;
        }

        public void Add_ModFilterItem(ModFilterItem item)
        {
           
            ModFilterItem existingItem;
            if (_items.TryGetValue(item.ItemId, out existingItem))
            {
                existingItem.Update_ModFilterItem(item.ItemRequestedLimit, item.ItemMaxLimit);
                item.Dispose();
            }
            else
            {
                _items[item.ItemId] = item;  // Directly add to the dictionary
                item.OnItemBelowLimit += Remove_Filter_Item;
                item.OnItemOverLimit += Add_Filter_Item;
            }
        }

        public bool ContainsId(MyDefinitionId id, out ModFilterItem modFilterItem)
        {
            return _items.TryGetValue(id, out modFilterItem);
        }

        private void Update_Sorter_Filter()
        {
            if (myInventory_filter.Count == 0) return;

            var filterList = myInventory_filter.ToList();  // Convert to list only if necessary
            _myConveyorSorter.SetFilter(Sandbox.ModAPI.Ingame.MyConveyorSorterMode.Whitelist, filterList);
            _myConveyorSorter.DrainAll = true;
        }

        private void Parse_To_Filters()
        {
            foreach (var modFilterItem in _items.Values)
            {
                if (modFilterItem.ItemMaxLimit == 0 && modFilterItem.ItemRequestedLimit == 0)
                    continue;  // Use continue instead of return to process all items

                if (modFilterItem.ItemRequestedLimit < 0 && !myInventory_filter.Contains(modFilterItem.ItemId))
                {
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
            foreach (var item in _items.Values)
            {
                item.OnItemBelowLimit -= Remove_Filter_Item;
                item.OnItemOverLimit -= Add_Filter_Item;
            }
        }
    }
}