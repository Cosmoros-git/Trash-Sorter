using System;
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
        public Dictionary<IMyConveyorSorter, ItemLimit> SorterItemLimits;
        public readonly MyDefinitionId DefinitionId;

        public SorterLimitManager(MyDefinitionId definitionId)
        {
            DefinitionId = definitionId;
            SorterItemLimits = new Dictionary<IMyConveyorSorter, ItemLimit>();
        }

        public void RegisterSorter(IMyConveyorSorter sorter, ItemLimit itemLimit)
        {
            SorterItemLimits[sorter] = itemLimit;
            sorter.OnClosing += Sorter_OnClosing;
        }

        public void UnRegisterSorter(IMyConveyorSorter sorter)
        {
            SorterItemLimits.Remove(sorter);
        }

        public void ChangeLimitsOnSorter(IMyConveyorSorter sorter, MyFixedPoint ItemRequestedAmount, MyFixedPoint itemTriggerAmount)
        {
            var limits = SorterItemLimits[sorter];
            limits.OverLimitTrigger = false;
            limits.ItemTriggerAmount = itemTriggerAmount;
            limits.ItemRequestedAmount = itemTriggerAmount;
        }

        private void Sorter_OnClosing(VRage.ModAPI.IMyEntity obj)
        {
            obj.OnClosing -= Sorter_OnClosing;
            SorterItemLimits.Remove((IMyConveyorSorter)obj);
        }



        private static void HandleFilterStorageChange(IMyConveyorSorter sorter, MyDefinitionId definitionId, bool exceeded)
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

        public void OnValueChange(MyFixedPoint value)
        {
            foreach (var kvp in SorterItemLimits)
            {
                var limit = kvp.Value;
                var sorter = kvp.Key;

                if (value > limit.ItemTriggerAmount)
                {
                    if (limit.OverLimitTrigger) continue;

                    limit.OverLimitTrigger = true;
                    HandleFilterStorageChange(sorter, DefinitionId, false);
                    continue;
                }

                if (value > limit.ItemRequestedAmount) continue;

                if (!limit.OverLimitTrigger) continue;

                limit.OverLimitTrigger = false;
                HandleFilterStorageChange(sorter, DefinitionId, false);
            }
        }

        public override void Dispose()
        {
            foreach (var sorter in SorterItemLimits.Keys.ToList()) // Use ToList() to avoid potential modification issues
            {
                sorter.OnClosing -= Sorter_OnClosing;
            }
        }
    }

}