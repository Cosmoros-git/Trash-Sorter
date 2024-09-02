using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using Sandbox.Game.World.Generator;
using Sandbox.ModAPI;
using Trash_Sorter.Data.Scripts.Trash_Sorter.BaseClass;
using Trash_Sorter.Data.Scripts.Trash_Sorter.Main_Storage_Class;
using VRage;
using VRage.Game;

namespace Trash_Sorter.Data.Scripts.Trash_Sorter.ActiveClasses.Mod_Conveyor_Sorter_Manager
{
    internal struct ModStructClass
    {
        public MyFixedPoint ItemRequestedLimit;
        public MyFixedPoint ItemTriggerAmount;
    }

    internal class ModLimitsClass : ModBase
    {
        public IMyConveyorSorter SorterReference { get; set; }
        public event Action<ModLimitsClass> DeleteValue;

        private readonly MyDefinitionId _definitionId;
        private MyFixedPoint _itemRequestedLimit;

        public MyFixedPoint ItemRequestedLimit
        {
            get { return _itemRequestedLimit; }
            set
            {
                _itemRequestedLimit = value;
                OverLimitTrigger = false;
            }
        }

        private MyFixedPoint _itemTriggerAmount;

        public MyFixedPoint ItemTriggerAmount
        {
            get { return _itemTriggerAmount; }
            set
            {
                _itemTriggerAmount = value;
                OverLimitTrigger = false;
            }
        }

        private bool OverLimitTrigger;

        public ModLimitsClass(IMyConveyorSorter sorter, MyDefinitionId definitionId, MyFixedPoint itemRequestedLimit,
            MyFixedPoint itemTriggerAmount)
        {
            _definitionId = definitionId;
            _itemTriggerAmount = itemTriggerAmount;
            _itemRequestedLimit = itemRequestedLimit;
            SorterReference = sorter;
            OverLimitTrigger = false;
            sorter.OnClosing += Sorter_OnClosing;
        }

        private void Sorter_OnClosing(VRage.ModAPI.IMyEntity obj)
        {
            Dispose();
        }

        public void ValueChange(MyFixedPoint value)
        {
            switch (EvaluateValueChange(value))
            {
                case 1:
                    Mod_Filter_StorageCallback.OnOnChangeCallBack(SorterReference, _definitionId, false);
                    break;
                case -1:
                    Mod_Filter_StorageCallback.OnOnChangeCallBack(SorterReference, _definitionId, true);
                    break;
            }
        }

        private int EvaluateValueChange(MyFixedPoint value)
        {
            if (value > ItemTriggerAmount)
            {
                if (OverLimitTrigger) return 0; // No change

                OverLimitTrigger = true;
                return 1; // Trigger exceeded
            }

            if (value > ItemRequestedLimit) return 0; // No change

            if (!OverLimitTrigger) return 0; // No change

            OverLimitTrigger = false;
            return -1; // Value fell below ItemRequestedLimit
        }

        public override void Dispose()
        {
            SorterReference.OnClosing -= Sorter_OnClosing;
            OnDeleteValue(this);
        }

        protected virtual void OnDeleteValue(ModLimitsClass obj)
        {
            DeleteValue?.Invoke(obj);
        }
    }


    internal class ModFilterItemV2 : ModBase
    {
        private readonly Dictionary<IMyConveyorSorter, ModLimitsClass> Limits_Observer_Dictionary;
        private readonly MyDefinitionId definitionId;

        public ModFilterItemV2(MyDefinitionId itemId)
        {
            Limits_Observer_Dictionary = new Dictionary<IMyConveyorSorter, ModLimitsClass>();
            definitionId = itemId;
        }


        public void On_Value_Updated(MyFixedPoint myFixedPoint)
        {
            foreach (var limit in Limits_Observer_Dictionary.Values)
            {
                limit.ValueChange(myFixedPoint);
            }
        }

        public void Add_Update_Limits(IMyConveyorSorter sorter, ModStructClass value)
        {
            ModLimitsClass limits;
            if (Limits_Observer_Dictionary.TryGetValue(sorter, out limits))
            {
                limits.ItemRequestedLimit = value.ItemRequestedLimit;
                limits.ItemTriggerAmount = value.ItemTriggerAmount;
                return;
            }
            var itemLimit = new ModLimitsClass(sorter, definitionId, value.ItemRequestedLimit,
                value.ItemTriggerAmount);
            itemLimit.DeleteValue += ItemLimit_DeleteValue;
            Limits_Observer_Dictionary[sorter] = itemLimit;
        }

        public void Remove_Limit(IMyConveyorSorter sorter)
        {
            ModLimitsClass limits;
            if (!Limits_Observer_Dictionary.TryGetValue(sorter, out limits)) return;

            limits.Dispose();
            limits.DeleteValue-= ItemLimit_DeleteValue;
        }

        private void ItemLimit_DeleteValue(ModLimitsClass obj)
        {
            obj.DeleteValue-= ItemLimit_DeleteValue;
            Limits_Observer_Dictionary.Remove(obj.SorterReference);
        }

        public override void Dispose()
        {
        }
    }
}