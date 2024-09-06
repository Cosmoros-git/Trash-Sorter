using System.Collections.Generic;
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
        private readonly Logger MyLogger;
        private readonly FixedPointReference ItemAmountRef;

        public SorterLimitManager(MyDefinitionId definitionId, Logger myLogger, FixedPointReference itemAmountRef)
        {
            DefinitionId = definitionId;
            MyLogger = myLogger;
            ItemAmountRef = itemAmountRef;
            SorterItemLimits = new Dictionary<IMyConveyorSorter, ItemLimit>();
        }

        // Deals with adding conveyor sorters with values. Empty values never used.
        public void RegisterSorter(IMyConveyorSorter sorter, ItemLimit itemLimit)
        {
            SorterItemLimits[sorter] = itemLimit;
            sorter.OnClosing += Sorter_OnClosing;
            OnValueChange();
        }

        public void UnRegisterSorter(IMyConveyorSorter sorter)
        {
            SorterItemLimits.Remove(sorter);
            sorter.RemoveItem(DefinitionId);
        }

        public void ChangeLimitsOnSorter(IMyConveyorSorter sorter, MyFixedPoint itemRequestedAmount,
            MyFixedPoint itemTriggerAmount)
        {
            ItemLimit limits;
            if (!SorterItemLimits.TryGetValue(sorter, out limits)) return;
            limits.OverLimitTrigger = false;
            limits.ItemTriggerAmount = itemTriggerAmount;
            limits.ItemRequestedAmount = itemRequestedAmount;
            OnValueChange();
        }

        private void Sorter_OnClosing(VRage.ModAPI.IMyEntity obj)
        {
            obj.OnClosing -= Sorter_OnClosing;
            SorterItemLimits.Remove((IMyConveyorSorter)obj);
        }


        private void HandleFilterStorageChange(IMyConveyorSorter sorter, bool exceeded)
        {
            if (exceeded)
            {
                MyLogger.Log("SorterLimitManager", $"{DefinitionId.SubtypeName} added");
                sorter.AddItem(DefinitionId);
            }
            else
            {
                MyLogger.Log("SorterLimitManager", $"{DefinitionId.SubtypeName} removed");
                sorter.RemoveItem(DefinitionId);
            }
        }

        // Logic behind filters setting on items amount changes.
        public void OnValueChange()
        {
            foreach (var kvp in SorterItemLimits)
            {
                var limit = kvp.Value;
                var sorter = kvp.Key;

                // Log initial state
                MyLogger.Log(ClassName,
                    $"[START] Processing sorter: {sorter.CustomName}. ItemAmount: {ItemAmountRef.ItemAmount}, ItemTriggerLimit: {limit.ItemTriggerAmount}, ItemRequestAmount: {limit.ItemRequestedAmount}, OverLimitTrigger: {limit.OverLimitTrigger}");

                // Case 1: Value exceeds the trigger amount, we need to add the item to the filter.
                if (ItemAmountRef.ItemAmount > limit.ItemTriggerAmount)
                {
                    MyLogger.Log(ClassName, $"[INFO] ItemAmount ({ItemAmountRef.ItemAmount}) is greater than ItemTriggerLimit ({limit.ItemTriggerAmount}).");

                    if (limit.OverLimitTrigger)
                    {
                        // Already over limit, skip further updates.
                        MyLogger.Log(ClassName, $"[SKIP] OverLimitTrigger is already set. No further actions.");
                        continue;
                    }

                    // Setting OverLimitTrigger to true and adding the item to the filter.
                    limit.OverLimitTrigger = true;
                    MyLogger.Log(ClassName, $"[ADD] Setting OverLimitTrigger to true. Adding item {DefinitionId.SubtypeName} to sorter.");
                    HandleFilterStorageChange(sorter, true);
                    continue;
                }

                // Case 2: Value is within the requested amount range, no filter change needed.
                if (ItemAmountRef.ItemAmount > limit.ItemRequestedAmount)
                {
                    // Value is within acceptable limits, no need to add or remove items.
                    MyLogger.Log(ClassName, $"[INFO] ItemAmount ({ItemAmountRef.ItemAmount}) is within acceptable limits (> ItemRequestedAmount {limit.ItemRequestedAmount}), no filter change needed.");
                    continue;
                }

                // Case 3: Value dropped below the requested amount, we need to remove the item from the filter.
                if (!limit.OverLimitTrigger)
                {
                    // The item has already been removed, no further action required.
                    MyLogger.Log(ClassName, $"[SKIP] OverLimitTrigger is not set. No action required.");
                    continue;
                }

                // Reset the flag and remove the item from the filter.
                MyLogger.Log(ClassName, $"[REMOVE] ItemAmount ({ItemAmountRef.ItemAmount}) dropped below ItemRequestedAmount ({limit.ItemRequestedAmount}). Removing item {DefinitionId.SubtypeName} from sorter.");
                limit.OverLimitTrigger = false;
                HandleFilterStorageChange(sorter, false);
            }
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