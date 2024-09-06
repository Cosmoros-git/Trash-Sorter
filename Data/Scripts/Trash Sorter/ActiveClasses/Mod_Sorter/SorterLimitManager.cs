using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using Trash_Sorter.Data.Scripts.Trash_Sorter.BaseClass;
using Trash_Sorter.Data.Scripts.Trash_Sorter.SessionComponent;
using Trash_Sorter.Data.Scripts.Trash_Sorter.StorageClasses;
using VRage;
using VRage.Game;

namespace Trash_Sorter.Data.Scripts.Trash_Sorter.ActiveClasses.Mod_Sorter
{
    internal class SorterLimitManager : ModBase
    {
        // Should be quite good way to hold item limits. Atm they are going like [DefId]=>[Sorter]=>Value with defIds being pre-generated and sorter with value being dictionary.
        public Dictionary<IMyConveyorSorter, MyFixedPoint[]> SorterItemLimits;
        public readonly MyDefinitionId DefinitionId;
        private readonly FixedPointReference ItemAmountRef;

        public SorterLimitManager(MyDefinitionId definitionId, FixedPointReference itemAmountRef)
        {
            DefinitionId = definitionId;
            ItemAmountRef = itemAmountRef;
            SorterItemLimits = new Dictionary<IMyConveyorSorter, MyFixedPoint[]>();
        }

        // Deals with adding conveyor sorters with values. Empty values never used.
        public void RegisterSorter(IMyConveyorSorter sorter, double itemRequestAmount, double itemTriggerAmount)
        {
            SorterItemLimits[sorter] = new[] { (MyFixedPoint)itemRequestAmount, (MyFixedPoint)itemTriggerAmount, 0 };
            sorter.OnClosing += Sorter_OnClosing;
            OnValueChange();
        }

        public void UnRegisterSorter(IMyConveyorSorter sorter)
        {
            SorterItemLimits.Remove(sorter);
            sorter.RemoveItem(DefinitionId);
        }

        public void ChangeLimitsOnSorter(IMyConveyorSorter sorter, double itemRequestedAmount,
            double itemTriggerAmount)
        {
            MyFixedPoint[] limits;
            if (!SorterItemLimits.TryGetValue(sorter, out limits)) return;
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
                Logger.Log("SorterLimitManager", $"{DefinitionId.SubtypeName} added");
                sorter.AddItem(DefinitionId);
            }
            else
            {
                Logger.Log("SorterLimitManager", $"{DefinitionId.SubtypeName} removed");
                sorter.RemoveItem(DefinitionId);
            }
        }

        // Logic behind filters setting on items amount changes.
        public void OnValueChange()
        {
            //Limit 0 = itemRequestAmount
            //Limit 1 = itemTriggerAmount
            //Limit 2 = Bool sort of 0/1 true.

            foreach (var kvp in SorterItemLimits)
            {
                var limit = kvp.Value;
                var sorter = kvp.Key;

                // Log initial state
                // Case 1: Value exceeds the trigger amount, we need to add the item to the filter.
                if (ItemAmountRef.ItemAmount > limit[0])
                {

                    if (limit[2]==1)
                    {
                        // Already over limit, skip further updates.
                        continue;
                    }

                    // Setting OverLimitTrigger to true and adding the item to the filter.
                    limit[2]= 1;
                    HandleFilterStorageChange(sorter, true);
                    continue;
                }

                // Case 2: Value is within the requested amount range, no filter change needed.
                if (ItemAmountRef.ItemAmount > limit[0])
                {
                    // Value is within acceptable limits, no need to add or remove items.
                    continue;
                }

                // Case 3: Value dropped below the requested amount, we need to remove the item from the filter.
                if (limit[2]!=1)
                {
                    // The item has already been removed, no further action required.
                    continue;
                }

                // Reset the flag and remove the item from the filter.
                limit[2] = 0;
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