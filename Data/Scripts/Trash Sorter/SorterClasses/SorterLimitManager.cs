using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using Trash_Sorter.BaseClass;
using Trash_Sorter.StaticComponents;
using Trash_Sorter.StorageClasses;
using VRage;
using VRage.Game;

namespace Trash_Sorter.SorterClasses
{
    internal class SorterLimitManager : ModBase
    {
        // Should be quite good way to hold item limits. Atm they are going like [DefId]=>[Sorter]=>Value with defIds being pre-generated and sorter with value being dictionary.
        public Dictionary<IMyConveyorSorter, MyFixedPoint[]> SorterItemLimits;
        public readonly MyDefinitionId DefinitionId;
        private readonly FixedPointReference ItemAmountRef;
        private readonly HashSet<IMyConveyorSorter> ManagedSorters;


        public SorterLimitManager(MyDefinitionId definitionId, FixedPointReference itemAmountRef)
        {
            DefinitionId = definitionId;
            ItemAmountRef = itemAmountRef;
            ManagedSorters = new HashSet<IMyConveyorSorter>();
            SorterItemLimits = new Dictionary<IMyConveyorSorter, MyFixedPoint[]>();
        }

        // Deals with adding conveyor sorters with values. Empty values never used.
        public void RegisterSorter(IMyConveyorSorter sorter, double itemRequestAmount, double itemTriggerAmount)
        {
            SorterItemLimits[sorter] = new[] { (MyFixedPoint)itemRequestAmount, (MyFixedPoint)itemTriggerAmount, 0 };
            OnValueChange();
            if (!ManagedSorters.Add(sorter)) return;
            sorter.OnClosing += Sorter_OnClosing;
        }
        public void UnRegisterSorter(IMyConveyorSorter sorter)
        {
            SorterItemLimits.Remove(sorter);
            sorter.RemoveItem(DefinitionId);
            if (!ManagedSorters.Contains(sorter)) return;
            sorter.OnClosing -= Sorter_OnClosing;
        }


        public void ChangeLimitsOnSorter(IMyConveyorSorter sorter, double itemRequestedAmount, double itemTriggerAmount)
        {
            MyFixedPoint[] limits;
            if (!SorterItemLimits.TryGetValue(sorter, out limits))
            {
                Logger.Log("SorterLimitManager",
                    $"ChangeLimitsOnSorter: Sorter {sorter.CustomName} not found in SorterItemLimits");
                return;
            }

            limits[0] = (MyFixedPoint)itemRequestedAmount;
            limits[1] = (MyFixedPoint)itemTriggerAmount;
            limits[2] = 0;

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
                Logger.LogWarning("SorterLimitManager",
                    $"{DefinitionId.SubtypeName} added to sorter {sorter.CustomName}");
                sorter.AddItem(DefinitionId);
            }
            else
            {
                Logger.LogWarning("SorterLimitManager",
                    $"{DefinitionId.SubtypeName} removed from sorter {sorter.CustomName}");
                sorter.RemoveItem(DefinitionId);
            }
        }
        public void OnValueChange()
        {
            Logger.Log("SorterLimitManager", "OnValueChange: Checking item amounts and sorter limits.");

            foreach (var kvp in SorterItemLimits)
            {
                var limit = kvp.Value;
                var sorter = kvp.Key;

                Logger.Log("SorterLimitManager",
                    $"OnValueChange: Item {DefinitionId.SubtypeName} - Requested Amount: {limit[0]}, Trigger Amount: {limit[1]}, OverLimitFlag: {limit[2]}");

                // Log current item amount for comparison
                Logger.Log("SorterLimitManager", $"OnValueChange: ItemAmount: {ItemAmountRef.ItemAmount}");

                // Case 1: Value exceeds the trigger amount, we need to add the item to the filter.
                if (ItemAmountRef.ItemAmount > limit[0])
                {
                    if (limit[2] == 1)
                    {
                        Logger.Log("SorterLimitManager",
                            $"OnValueChange:  Item {DefinitionId.SubtypeName} - Already over limit, skipping update.");
                        continue;
                    }

                    // Setting OverLimitTrigger to true and adding the item to the filter.
                    limit[2] = 1;
                    Logger.Log("SorterLimitManager",
                        $"OnValueChange:  Item {DefinitionId.SubtypeName} - Exceeded limit, adding item.");
                    HandleFilterStorageChange(sorter, true);
                    continue;
                }

                // Case 2: Value is within the requested amount range, no filter change needed.
                if (ItemAmountRef.ItemAmount > limit[0])
                {
                    Logger.Log("SorterLimitManager",
                        $"OnValueChange:  Item  {DefinitionId.SubtypeName} - Item amount within acceptable limits, no action taken.");
                    continue;
                }

                // Case 3: Value dropped below the requested amount, we need to remove the item from the filter.
                if (limit[2] != 1)
                {
                    Logger.Log("SorterLimitManager",
                        $"OnValueChange:  Item  {DefinitionId.SubtypeName} - Item already removed from filter, no action needed.");
                    continue;
                }

                // Reset the flag and remove the item from the filter.
                limit[2] = 0;
                Logger.Log("SorterLimitManager",
                    $"OnValueChange:  Item  {DefinitionId.SubtypeName} - Dropped below requested amount, removing item.");
                HandleFilterStorageChange(sorter, false);
            }
        }


        public override void Dispose()
        {
            Logger.Log("SorterLimitManager", "Dispose: Cleaning up sorter references.");

            foreach (var sorter in SorterItemLimits.Keys.ToArray())
            {
                sorter.OnClosing -= Sorter_OnClosing;
            }
        }
    }
}