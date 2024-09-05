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
            {   // If key is not here already it means it was never needed. Might have broken stuff because of this... 
                if (!ContainsKey(key)) return;

                if (EqualityComparer<MyFixedPoint>.Default.Equals(base[key], value)) return;

                base[key] = value;
                OnValueChanged?.Invoke(key, value);
            }
        }

        public void UpdateValue(TKey key, MyFixedPoint updateToValue)
        {
            if (!ContainsKey(key)) return;

            //Logger.Instance.Log("Observable dictionary", $"{key} changed value {updateToValue}");
            var currentValue = base[key];
            var result = currentValue + updateToValue;
            this[key] = result; // This will trigger the setter and raise the event
        }
    }


    public class MainItemStorage : ModBase
    {

        // Commented out dictionaries were just never used.
        public Dictionary<string, MyDefinitionId> NameToDefinitionMap;

        //  public Dictionary<MyDefinitionId, string> DefinitionToName;
        public HashSet<MyDefinitionId> ProcessedItems;

        //  public HashSet<string> ProcessedItemsNames;
        public ObservableDictionary<MyDefinitionId> ItemsDictionary;



        // This is main storage of my values, id references from strings and hash set of ids I do care about.
        public MainItemStorage()
        {
            Logger.Instance.Log(ClassName, "Item storage created");
            //DefinitionToName = new Dictionary<MyDefinitionId, string>();
            NameToDefinitionMap = new Dictionary<string, MyDefinitionId>();
            ItemsDictionary = new ObservableDictionary<MyDefinitionId>();
            ProcessedItems = new HashSet<MyDefinitionId>();
            // ProcessedItemsNames = new HashSet<string>();
            GetDefinitions();
        }

        // Here I get all definitions that I will observe. Anything not here is ignored. [Guide] tag in trash sorter will give you data.
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

            NameToDefinitionMap[name.Trim()] = definition.Id;
            ItemsDictionary[definition.Id] = 0;
            // DefinitionToName[definition.Id] = name;
            ProcessedItems.Add(definition.Id);
            // ProcessedItemsNames.Add(name);
        }
    }
}