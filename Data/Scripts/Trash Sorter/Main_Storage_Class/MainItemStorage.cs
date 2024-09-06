using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Definitions;
using Trash_Sorter.Data.Scripts.Trash_Sorter.BaseClass;
using VRage;
using VRage.Game;
using VRage.Utils;

namespace Trash_Sorter.Data.Scripts.Trash_Sorter.Main_Storage_Class
{
    public class FixedPointReference
    {
        // This was made because fuck structs.
        public MyFixedPoint ItemAmount;

        public FixedPointReference(MyFixedPoint itemAmount)
        {
            ItemAmount = itemAmount;
        }
    }

    public class ObservableDictionary<TKey> : Dictionary<TKey, FixedPointReference>
    {
        public event Action<TKey> OnValueChanged;

        public new FixedPointReference this[TKey key]
        {
            get
            {
                if (!ContainsKey(key))
                {
                    MyLog.Default.WriteLine($"Observable Dictionary The given key '{key}' was not present in the dictionary.");
                }

                return base[key];
            }
            set
            {
                // If key is not here already it means it was never needed. Might have broken stuff because of this... 
                if (!ContainsKey(key))
                {
                    Add(key, value);
                    OnValueChanged?.Invoke(key);
                    return; // Exit after adding to avoid double-setting below
                }

                if (EqualityComparer<FixedPointReference>.Default.Equals(base[key], value)) return;

                base[key] = value;
                OnValueChanged?.Invoke(key);
            }
        }

        public void UpdateValue(TKey key, MyFixedPoint updateToValue)
        {
            FixedPointReference currentValueRef;
            if (!TryGetValue(key,out currentValueRef))
            {
                Add(key,new FixedPointReference(updateToValue));
                return;
            }
            currentValueRef.ItemAmount += updateToValue;
            OnValueChanged?.Invoke(key);
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
        private readonly Logger myLogger;

        // This is main storage of my values, id references from strings and hash set of ids I do care about.
        public MainItemStorage(Logger MyLogger)
        {
            myLogger = MyLogger;
            myLogger.Log(ClassName, "Item storage created");

            NameToDefinitionMap = new Dictionary<string, MyDefinitionId>();
            ItemsDictionary = new ObservableDictionary<MyDefinitionId>();
            ProcessedItems = new HashSet<MyDefinitionId>();
            GetDefinitions();
        }

        // Here I get all definitions that I will observe. Anything not here is ignored. [Guide] tag in trash sorter will give you data.
        public void GetDefinitions()
        {
            myLogger.Log(ClassName, "Getting item definitions");
            try
            {
                var allDefinitions = MyDefinitionManager.Static.GetAllDefinitions();

                if (allDefinitions.Count == 0)
                {
                    myLogger.Log(ClassName, "allDefinitions item count is 0");
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
                    myLogger.Log(ClassName, "componentsDefinitions item count is 0");
                    return;
                }

                if (oresDefinitions.Count == 0)
                {
                    myLogger.Log(ClassName, "oresDefinitions item count is 0");
                    return;
                }

                if (ingotDefinitions.Count == 0)
                {
                    myLogger.Log(ClassName, "ingotDefinitions item count is 0");
                    return;
                }

                if (ammoDefinition.Count == 0)
                {
                    myLogger.Log(ClassName, "ammoDefinition item count is 0");
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

                myLogger.Log(ClassName, "Finished getting item definitions");
            }
            catch (Exception ex)
            {
                myLogger.Log(ClassName, $"all definitions failed {ex}");
            }
        }

        private void AddToDictionaries(MyDefinitionBase definition)
        {
            var name = definition.DisplayNameText;

            NameToDefinitionMap[name.Trim().ToLower()] = definition.Id;
            ItemsDictionary[definition.Id] = new FixedPointReference(0);
            ProcessedItems.Add(definition.Id);
        }
    }
}