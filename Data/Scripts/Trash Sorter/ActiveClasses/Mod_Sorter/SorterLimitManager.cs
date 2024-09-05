using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Sandbox.ModAPI;
using Trash_Sorter.Data.Scripts.Trash_Sorter.BaseClass;
using Trash_Sorter.Data.Scripts.Trash_Sorter.Main_Storage_Class;
using VRage;
using VRage.Game;

namespace Trash_Sorter.Data.Scripts.Trash_Sorter.ActiveClasses.Mod_Sorter
{
    internal class SorterLimitManager : ModBase
    {
        // Should be quite good way to hold item limits. Atm they are going like [DefId]=>[Sorter]=>Value with defIds being pre-generated and sorter with value being dictionary.
        public Dictionary<IMyConveyorSorter, ItemLimit> SorterItemLimits;
        public readonly MyDefinitionId DefinitionId;
        private readonly Stopwatch watch = new Stopwatch();

        public SorterLimitManager(MyDefinitionId definitionId)
        {
            DefinitionId = definitionId;
            SorterItemLimits = new Dictionary<IMyConveyorSorter, ItemLimit>();
        }

        // Deals with adding conveyor sorters with values. Empty values never used.
        public void RegisterSorter(IMyConveyorSorter sorter, ItemLimit itemLimit, MyFixedPoint currentValue)
        {
            SorterItemLimits[sorter] = itemLimit;
            sorter.OnClosing += Sorter_OnClosing;
            OnValueChangeInit(sorter, currentValue, itemLimit);
        }
        public void UnRegisterSorter(IMyConveyorSorter sorter)
        {
            SorterItemLimits.Remove(sorter);
        }
        public void ChangeLimitsOnSorter(IMyConveyorSorter sorter, MyFixedPoint ItemRequestedAmount,
            MyFixedPoint itemTriggerAmount)
        {
            ItemLimit limits;
            if (!SorterItemLimits.TryGetValue(sorter, out limits)) return;
            limits.OverLimitTrigger = false;
            limits.ItemTriggerAmount = itemTriggerAmount;
            limits.ItemRequestedAmount = ItemRequestedAmount;
        }

        private void Sorter_OnClosing(VRage.ModAPI.IMyEntity obj)
        {
            obj.OnClosing -= Sorter_OnClosing;
            SorterItemLimits.Remove((IMyConveyorSorter)obj);
        }



        private static void HandleFilterStorageChange(IMyConveyorSorter sorter, MyDefinitionId definitionId,
            bool exceeded)
        {
            if (exceeded)
            {
                sorter.AddItem(definitionId);
            }
            else
            {
                sorter.RemoveItem(definitionId);
            }
        }


        // On Init forces values to check if they need to be added to filter or removed.
        private void OnValueChangeInit(IMyConveyorSorter sorter, MyFixedPoint currentValue, ItemLimit itemLimit)
        {
            // Logger.Instance.Log(ClassName, $"Item init, current value: {currentValue}, request amount {itemLimit.ItemRequestedAmount}, trigger amount {itemLimit.ItemTriggerAmount}");
            if (currentValue > itemLimit.ItemTriggerAmount)
            {
                if (itemLimit.OverLimitTrigger) return;

                itemLimit.OverLimitTrigger = true;
                HandleFilterStorageChange(sorter, DefinitionId, true);
                return;
            }

            if (currentValue > itemLimit.ItemRequestedAmount) return;

            if (!itemLimit.OverLimitTrigger) return;

            itemLimit.OverLimitTrigger = false;
            HandleFilterStorageChange(sorter, DefinitionId, false);
        }

        // Logic behind filters setting on items amount changes.
        public void OnValueChange(MyFixedPoint value)
        {
            watch.Restart();
            foreach (var kvp in SorterItemLimits)
            {
                var limit = kvp.Value;
                var sorter = kvp.Key;

                // Case 1: Value exceeds the trigger amount, we need to add the item to the filter.
                if (value > limit.ItemTriggerAmount)
                {
                    if (limit.OverLimitTrigger)
                    {
                        // We have already added this item, so skip further updates.
                        continue;
                    }

                    limit.OverLimitTrigger = true;
                    HandleFilterStorageChange(sorter, DefinitionId, true);
                    continue;
                }

                // Case 2: Value is within the requested amount range, no filter change needed.
                if (value > limit.ItemRequestedAmount)
                {
                    // Value is within acceptable limits, skip further updates.
                    continue;
                }

                // Case 3: Value dropped below the requested amount, we need to remove the item from the filter.
                if (limit.OverLimitTrigger)
                {
                    // Reset the flag and remove the item from the filter.
                    limit.OverLimitTrigger = false;
                    HandleFilterStorageChange(sorter, DefinitionId, false);
                }
            }

            watch.Stop();
            DebugTimeClass.TimeOne = watch.Elapsed;
        }


        public override void Dispose()
        {
            foreach (var sorter in
                     SorterItemLimits.Keys.ToList()) // Use ToList() to avoid potential modification issues
            {
                sorter.OnClosing -= Sorter_OnClosing;
            }
        }
    }
}