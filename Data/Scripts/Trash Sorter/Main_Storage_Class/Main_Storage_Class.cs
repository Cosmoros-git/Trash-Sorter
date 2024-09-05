using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox.Definitions;
using Trash_Sorter.Data.Scripts.Trash_Sorter.BaseClass;
using VRage;
using VRage.Game;

namespace Trash_Sorter.Data.Scripts.Trash_Sorter.Main_Storage_Class
{
    public class ObservableDictionary<TKey> : Dictionary<TKey, MyFixedPoint>
    {
        public event Action<TKey, MyFixedPoint> OnValueChanged;

        public new MyFixedPoint this[TKey key]
        {
            get
            {
                if (!ContainsKey(key))
                {
                    Logger.Instance.LogError("Observable Dictionary",
                        $"The given key '{key}' was not present in the dictionary.");
                }

                return base[key];
            }
            set
            {
                if (!ContainsKey(key))
                {
                    Add(key, value);
                }
                else if (!EqualityComparer<MyFixedPoint>.Default.Equals(base[key], value))
                {
                    base[key] = value;
                    OnValueChanged?.Invoke(key, value);
                }
            }
        }

        public void UpdateValue(TKey key, MyFixedPoint updateToValue)
        {
            if (ContainsKey(key))
            {
                //Logger.Instance.Log("Observable dictionary", $"{key} changed value {updateToValue}");
                var currentValue = base[key];
                var result = currentValue + updateToValue;
                this[key] = result; // This will trigger the setter and raise the event
            }
            else
            {
                this[key] = updateToValue; // Add new key with the provided value
            }
        }
    }


    public class MainStorageClass : ModBase
    {
        public Dictionary<string, MyDefinitionId> NameToDefinition;
        public Dictionary<MyDefinitionId, string> DefinitionToName;
        public HashSet<MyDefinitionId> ProcessedItems;
        public HashSet<string> ProcessedItemsNames;
        public ObservableDictionary<MyDefinitionId> ItemsDictionary;


        public MainStorageClass()
        {
            Logger.Instance.Log(ClassName, "Item storage created");
            DefinitionToName = new Dictionary<MyDefinitionId, string>();
            NameToDefinition = new Dictionary<string, MyDefinitionId>();
            ItemsDictionary = new ObservableDictionary<MyDefinitionId>();
            ProcessedItems = new HashSet<MyDefinitionId>();
            ProcessedItemsNames = new HashSet<string>();
            GetDefinitions();
        }


        public void GetDefinitions()
        {
            Logger.Instance.Log(ClassName, "Getting item definitions");
            try
            {
                var allDefinitions = MyDefinitionManager.Static.GetAllDefinitions();

                if (allDefinitions.Count == 0)
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
                    Logger.Instance.Log(ClassName, "oresDefinitions item count is 0");
                    return;
                }

                if (ingotDefinitions.Count == 0)
                {
                    Logger.Instance.Log(ClassName, "ingotDefinitions item count is 0");
                    return;
                }

                if (ammoDefinition.Count == 0)
                {
                    Logger.Instance.Log(ClassName, "ammoDefinition item count is 0");
                    return;
                }

                foreach (var definition in oresDefinitions)
                {
                    if (UniqueModExceptions.Contains(definition.DisplayNameText)) continue;
                    AddToDictionaries(definition);
                }

                foreach (var definition in ingotDefinitions)
                {
                    AddToDictionaries(definition);
                }

                foreach (var definition in componentsDefinitions)
                {
                    AddToDictionaries(definition);
                }

                foreach (var definition in ammoDefinition)
                {
                    AddToDictionaries(definition);
                }

                Logger.Instance.Log(ClassName, "Finished getting item definitions");
            }
            catch (Exception ex)
            {
                Logger.Instance.Log(ClassName, $"all definitions failed {ex}");
            }
        }

        private void AddToDictionaries(MyDefinitionBase definition)
        {
            var name = definition.DisplayNameText;

            NameToDefinition[name.Trim()] = definition.Id;
            ItemsDictionary[definition.Id] = 0;
            DefinitionToName[definition.Id] = name;
            ProcessedItems.Add(definition.Id);
            ProcessedItemsNames.Add(name);
        }
    }
}