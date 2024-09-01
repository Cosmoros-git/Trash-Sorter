﻿using System;
using System.Collections;
using System.Diagnostics;
using Trash_Sorter.Data.Scripts.Trash_Sorter.BaseClass;
using Trash_Sorter.Data.Scripts.Trash_Sorter.Main_Storage_Class;
using VRage;
using VRage.Game;

namespace Trash_Sorter.Data.Scripts.Trash_Sorter.ActiveClasses.Mod_Conveyor_Sorter_Manager
{
    internal class ModFilterItem : ModBase
    {
        public MyDefinitionId ItemId { get; }

        private bool wasOverLimitInvoked;

        private MyFixedPoint _itemAmount;
        private readonly ObservableDictionary<MyDefinitionId, MyFixedPoint> ItemsDictionary;
        private readonly Stopwatch watch = new Stopwatch();


        public MyFixedPoint ItemAmount
        {
            get { return _itemAmount; }
            set
            {
                _itemAmount = value;
                ValidateLimits();
            }
        }

        private MyFixedPoint _itemRequestedLimit;

        public MyFixedPoint ItemRequestedLimit
        {
            get { return _itemRequestedLimit; }
            set
            {
                _itemRequestedLimit = value;
                ValidateLimits();
            }
        }

        private MyFixedPoint _itemMaxLimit;

        public MyFixedPoint ItemMaxLimit
        {
            get { return _itemMaxLimit; }
            set
            {
                _itemMaxLimit = value;
                ValidateLimits();
            }
        }

        public event Action<MyDefinitionId> OnItemOverLimit;
        public event Action<MyDefinitionId> OnItemBelowLimit;

        public ModFilterItem(MyDefinitionId itemId, MyFixedPoint requestedLimit,
            MyFixedPoint maxLimit, ObservableDictionary<MyDefinitionId, MyFixedPoint> itemsDictionary)
        {
            if (itemsDictionary == null)
            {
                throw new ArgumentNullException(nameof(itemsDictionary), "Items dictionary cannot be null.");
            }

           
            ItemId = itemId;
            _itemAmount = itemsDictionary[itemId];
            _itemRequestedLimit = requestedLimit;
            _itemMaxLimit = maxLimit;
            wasOverLimitInvoked = false;


            Logger.Instance.Log(ClassName,
                $"Creating filter item with name {itemId.SubtypeName}, values {_itemAmount},{requestedLimit},{maxLimit}");
            ItemsDictionary = itemsDictionary;
            ItemsDictionary.OnValueChanged += On_Value_Updated;

            ValidateLimits(); // Validate limits at the start
        }


        public void Update_ModFilterItem(MyFixedPoint itemRequestLimit, MyFixedPoint maxLimit)
        {
            if (itemRequestLimit != ItemRequestedLimit)
            {
                ItemRequestedLimit = itemRequestLimit;
            }

            if (maxLimit != ItemMaxLimit)
            {
                ItemMaxLimit = maxLimit;
            }
        }


        private void On_Value_Updated(MyDefinitionId myDefinitionId, MyFixedPoint myFixedPoint)
        {
            if (myDefinitionId == ItemId)
            {
                ItemAmount = myFixedPoint; // This triggers ValidateLimits
            }
        }

        private void ValidateLimits()
        {
            watch.Restart();
            if (_itemAmount > _itemMaxLimit)
            {
                if (wasOverLimitInvoked) return;

                OnItemOverLimit?.Invoke(ItemId);
                wasOverLimitInvoked = true;
            }
            else if (_itemAmount < _itemRequestedLimit)
            {
                if (!wasOverLimitInvoked) return;

                OnItemBelowLimit?.Invoke(ItemId);
                wasOverLimitInvoked = false;
            }
            else
            {
                wasOverLimitInvoked = false;
            }

            watch.Stop();
            ModSorterTime.FunctionTimes = +watch.ElapsedMilliseconds;
        }


        public override void Dispose()
        {
            ItemsDictionary.OnValueChanged -= On_Value_Updated;
        }

        public override bool Equals(object obj)
        {
            var other = obj as ModFilterItem;
            if (other == null) return false;
            return !ItemId.Equals(other.ItemId);
        }

        public override int GetHashCode()
        {
            return ItemId.GetHashCode();
        }
    }
}