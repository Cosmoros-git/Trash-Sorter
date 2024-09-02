using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.ModAPI;
using Trash_Sorter.Data.Scripts.Trash_Sorter.ActiveClasses.Mod_Conveyor_Sorter_Manager;
using Trash_Sorter.Data.Scripts.Trash_Sorter.BaseClass;
using VRage;
using VRage.Game;

namespace Trash_Sorter.Data.Scripts.Trash_Sorter.Main_Storage_Class
{
    public static class Mod_Filter_StorageCallback
    {
        public static event Action<IMyConveyorSorter, MyDefinitionId, bool> On_Change_CallBack;

        public static void OnOnChangeCallBack(IMyConveyorSorter arg1, MyDefinitionId arg2, bool arg3)
        {
            On_Change_CallBack?.Invoke(arg1, arg2, arg3);
        }
    }

    internal class Main_Mod_Filter_Storage : ModBase
    {
        public Dictionary<MyDefinitionId, ModFilterItemV2> ModFilterDictionary;
        private readonly ObservableDictionary<MyDefinitionId, MyFixedPoint> ObservableDictionaryReference;

        public Main_Mod_Filter_Storage(Main_Storage_Class mainStorageClass)
        {
            ModFilterDictionary =
                new Dictionary<MyDefinitionId, ModFilterItemV2>(mainStorageClass.NameToDefinition.Count);

            foreach (var definitionId in mainStorageClass.ProcessedItems)
            {
                ModFilterDictionary[definitionId] = new ModFilterItemV2(definitionId);
            }

            ObservableDictionaryReference = mainStorageClass.ItemsDictionary;
            ObservableDictionaryReference.OnValueChanged += ObservableDictionary_OnValueChanged;
        }

        private void ObservableDictionary_OnValueChanged(MyDefinitionId arg1, VRage.MyFixedPoint arg2)
        {
            ModFilterItemV2 value;
            if (ModFilterDictionary.TryGetValue(arg1, out value))
            {
                value.On_Value_Updated(arg2);
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            ObservableDictionaryReference.OnValueChanged-= ObservableDictionary_OnValueChanged;
        }
    }
}