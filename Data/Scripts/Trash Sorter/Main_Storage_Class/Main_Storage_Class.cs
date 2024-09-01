using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Definitions;
using Trash_Sorter.Data.Scripts.Trash_Sorter.BaseClass;
using VRage;
using VRage.Game;

namespace Trash_Sorter.Data.Scripts.Trash_Sorter.Main_Storage_Class
{
    public class ObservableDictionary<TKey, TValue> : Dictionary<TKey, TValue>
    {
        public event Action<TKey, TValue> OnValueChanged;

        public new TValue this[TKey key]
        {
            get { return base[key]; }
            set
            {
                if (EqualityComparer<TValue>.Default.Equals(base[key], value)) return;

                base[key] = value;
                OnValueChanged?.Invoke(key, value);
            }
        }
    }

    public class Main_Storage_Class : ModBase
    {
        public Dictionary<string, MyDefinitionId> DefinitionIdToName;
        public HashSet<MyDefinitionId> ProcessedItems = new HashSet<MyDefinitionId>();
        public HashSet<string> ProcessedItemsNames = new HashSet<string>();
        public ObservableDictionary<MyDefinitionId, MyFixedPoint> ItemsDictionary;

        public Main_Storage_Class()
        {
            Logger.Instance.Log(ClassName, "Item storage created");
            DefinitionIdToName = new Dictionary<string, MyDefinitionId>();
            ItemsDictionary = new ObservableDictionary<MyDefinitionId, MyFixedPoint>();
            GetDefinitions();
        }


        public void GetDefinitions()
        {
            Logger.Instance.Log(ClassName, "Getting item definitions");
            try
            {
                var allDefinitions = MyDefinitionManager.Static.GetAllDefinitions();

                if(allDefinitions.Count==0)
                {
                    Logger.Instance.Log(ClassName, "allDefinitions item count is 0");
                    return;
                }

                var componentsDefinitions =
                    allDefinitions.Where(d => d.Id.TypeId == typeof(MyObjectBuilder_Component)).ToList();

                var oresDefinitions = allDefinitions.Where(d => d.Id.TypeId == typeof(MyObjectBuilder_Ore)).ToList();

                var ingotDefinitions = allDefinitions.Where(d => d.Id.TypeId == typeof(MyObjectBuilder_Ingot)).ToList();

                var ammoDefinition = allDefinitions.Where(d => d.Id.TypeId == typeof(MyObjectBuilder_AmmoMagazine))
                    .ToList();


                if (componentsDefinitions.Count == 0)
                {
                    Logger.Instance.Log(ClassName, "componentsDefinitions item count is 0");
                    return;
                }
                if (oresDefinitions.Count == 0)
                {
                    Logger.Instance.Log(ClassName, "componentsDefinitions item count is 0");
                    return;
                }
                if (ingotDefinitions.Count == 0)
                {
                    Logger.Instance.Log(ClassName, "componentsDefinitions item count is 0");
                    return;
                }
                if (ammoDefinition.Count == 0)
                {
                    Logger.Instance.Log(ClassName, "componentsDefinitions item count is 0");
                    return;
                }



                foreach (var definition in ammoDefinition)
                {
                    var name = definition.DisplayNameText;
                    DefinitionIdToName[name] = definition.Id;
                    ItemsDictionary[definition.Id] = 0;
                    ProcessedItems.Add(definition.Id);
                    ProcessedItemsNames.Add(name);
                }

                foreach (var definition in componentsDefinitions)
                {
                    var name = definition.DisplayNameText;
                    DefinitionIdToName[name] = definition.Id;
                    ItemsDictionary[definition.Id] = 0;
                    ProcessedItems.Add(definition.Id);
                    ProcessedItemsNames.Add(name);
                }

                foreach (var definition in oresDefinitions)
                {
                    var name = definition.DisplayNameText;
                    if (UniqueModExceptions.Contains(name)) continue;
                    if (!NamingExceptions.Contains(name))
                    {
                        if (!name.Contains("Ore")) name += " Ore";
                    }

                    DefinitionIdToName[name] = definition.Id;
                    ItemsDictionary[definition.Id] = 0;
                    ProcessedItems.Add(definition.Id);
                    ProcessedItemsNames.Add(name);
                }

                foreach (var definition in ingotDefinitions)
                {
                    var name = definition.DisplayNameText;
                    DefinitionIdToName[name] = definition.Id;
                    ItemsDictionary[definition.Id] = 0;
                    ProcessedItems.Add(definition.Id);
                    ProcessedItemsNames.Add(name);
                }

                Logger.Instance.Log(ClassName, "Finished getting item definitions");
            }
            catch (Exception ex)
            {
                Logger.Instance.Log(ClassName, $"all definitions failed {ex}");
            }
        }
    }
}