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

        // Adds a new conveyor sorter with value.
        public void RegisterSorter(IMyConveyorSorter sorter, ItemLimit itemLimit, MyFixedPoint currentValue)
        {
            SorterItemLimits[sorter] = itemLimit;
            sorter.OnClosing += Sorter_OnClosing;
            OnValueChangeInit(sorter, currentValue, itemLimit);
        }

        // Removes limits on specific DefinitionId key, Sorter Key, removed value.
        public void UnRegisterSorter(IMyConveyorSorter sorter)
        {
            SorterItemLimits.Remove(sorter);
        }


        // Updates limits on specific DefinitionId key, Sorter Key, removed value.
        public void ChangeLimitsOnSorter(IMyConveyorSorter sorter, MyFixedPoint ItemRequestedAmount,
            MyFixedPoint itemTriggerAmount)
        {
            ItemLimit limits;
            if (!SorterItemLimits.TryGetValue(sorter, out limits)) return;
            limits.OverLimitTrigger = false;
            limits.ItemTriggerAmount = itemTriggerAmount;
            limits.ItemRequestedAmount = ItemRequestedAmount;
        }



        // Removes events related to sorter and limits data too. Preferably make on close stop event calls.
        private void Sorter_OnClosing(VRage.ModAPI.IMyEntity obj)
        {
            obj.OnClosing -= Sorter_OnClosing;
            SorterItemLimits.Remove((IMyConveyorSorter)obj);
        }

        // Adds removes filters from sorters.
        private static void HandleFilterStorageChange(IMyConveyorSorter sorter, MyDefinitionId definitionId,
            bool exceeded)
        {
            if (exceeded)
            {
                sorter.AddItem(definitionId);
                Logger.Instance.Log("Handle filter add", $"Item added to filter {definitionId}");
            }
            else
            {
                sorter.RemoveItem(definitionId);
                Logger.Instance.Log("Handle filter remove", $"Item removed from filter {definitionId}");
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

                if (value > limit.ItemTriggerAmount)
                {
                    if (limit.OverLimitTrigger) continue;
                    //Logger.Instance.Log(ClassName, $"Item changed over limits");
                    limit.OverLimitTrigger = true;
                    HandleFilterStorageChange(sorter, DefinitionId, true);
                    continue;
                }

                if (value > limit.ItemRequestedAmount) continue;

                if (!limit.OverLimitTrigger) continue;

                limit.OverLimitTrigger = false;
                HandleFilterStorageChange(sorter, DefinitionId, false);
            }

            watch.Stop();
            DebugTimeClass.TimeOne = +watch.Elapsed;
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