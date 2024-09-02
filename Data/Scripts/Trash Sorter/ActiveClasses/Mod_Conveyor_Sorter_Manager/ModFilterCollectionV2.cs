using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Sandbox.ModAPI.Ingame;
using Trash_Sorter.Data.Scripts.Trash_Sorter.BaseClass;
using Trash_Sorter.Data.Scripts.Trash_Sorter.Main_Storage_Class;
using VRage;
using VRage.Game;
using IMyConveyorSorter = Sandbox.ModAPI.IMyConveyorSorter;

namespace Trash_Sorter.Data.Scripts.Trash_Sorter.ActiveClasses.Mod_Conveyor_Sorter_Manager
{
    internal class ModFilterCollectionV2 : ModBase
    {
        private readonly HashSet<Sandbox.ModAPI.Ingame.MyInventoryItemFilter> myInventory_filter;
        private readonly Main_Mod_Filter_Storage ModFilterStorageReference;
        private readonly ObservableDictionary<MyDefinitionId, MyFixedPoint> ModStorageReference;

        public ModFilterCollectionV2(Main_Mod_Filter_Storage modFilterStorage,
            Main_Storage_Class.Main_Storage_Class modStorageClass)
        {
            ModFilterStorageReference = modFilterStorage;
            ModStorageReference = modStorageClass.ItemsDictionary;
            Mod_Filter_StorageCallback.On_Change_CallBack += Mod_Filter_StorageCallback_On_Change_CallBack;
        }

        private void Mod_Filter_StorageCallback_On_Change_CallBack(IMyConveyorSorter arg1, MyDefinitionId arg2,
            bool arg3)
        {
            if (arg3)
            {
                arg1.AddItem(arg2);
            }
            else
            {
                arg1.RemoveItem(arg2);
            }
        }

        public void Add_Item_ToStorage(MyDefinitionId id, IMyConveyorSorter sorter, ModStructClass values)
        {
            ModFilterItemV2 value;
            if (ModFilterStorageReference.ModFilterDictionary.TryGetValue(id, out value))
            {
                value.Add_Update_Limits(sorter, values);
            }
        }

        public void Remove_Item_FromStorage(MyDefinitionId id, IMyConveyorSorter sorter, ModStructClass values)
        {
            ModFilterItemV2 value;
            if (ModFilterStorageReference.ModFilterDictionary.TryGetValue(id, out value))
            {
                value.Remove_Limit(sorter);
            }
        }

        public override void Dispose()
        {
            Mod_Filter_StorageCallback.On_Change_CallBack -= Mod_Filter_StorageCallback_On_Change_CallBack;
        }
    }
}