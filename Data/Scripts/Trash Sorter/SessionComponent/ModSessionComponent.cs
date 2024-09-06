using System;
using System.Collections.Generic;
using System.Linq;
using Trash_Sorter.Data.Scripts.Trash_Sorter.StorageClasses;
using VRage.Utils;
using VRage.Game;
using VRage.Game.Components;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using System.Diagnostics;
using System.Text;

namespace Trash_Sorter.Data.Scripts.Trash_Sorter.SessionComponent
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class ModSessionComponent : MySessionComponentBase
    {
        protected static readonly HashSet<string> UniqueModExceptions = new HashSet<string>()
        {
            "Heat",
        };

        public static event Action AllowInitialization;
        public static bool IsInitializationAllowed { get; private set; }


        public override void UpdateAfterSimulation()
        {
            base.UpdateAfterSimulation();
            AllowInitialization?.Invoke();
            IsInitializationAllowed = true;
            UpdateOrder = MyUpdateOrder.NoUpdate;
        }

        public static ModSessionComponent Instance { get; private set; }


        public static Dictionary<string, MyDefinitionId> NameToDefinitionMap;

        /// <summary>
        /// Guide data string used for generating user instructions.
        /// </summary>
        public static string Guide_Data { get; private set; }

        //  public Dictionary<MyDefinitionId, string> DefinitionToName;
        public static HashSet<MyDefinitionId> ProcessedItems;

        //  public HashSet<string> ProcessedItemsNames;

        public static ObservableDictionary<MyDefinitionId> ItemsDictionaryReference { get; private set; }

        public override void BeforeStart()
        {
            MyLog.Default.WriteLine("ModSessionComponent: BeforeStart called.");
        }

        public override void LoadData()
        {
            base.LoadData();
            MyLog.Default.WriteLine("ModSessionComponent: LoadData called.");
            Instance = this;
            Init();
        }

        public void Init()
        {
            NameToDefinitionMap = new Dictionary<string, MyDefinitionId>();
            ProcessedItems = new HashSet<MyDefinitionId>();
            ItemsDictionaryReference = new ObservableDictionary<MyDefinitionId>();
            GetDefinitions();
            Create_All_Possible_Entries();
        }

        public void GetDefinitions()
        {
            Logger.Log("ModSessionComponent", "Getting item definitions");
            try
            {
                var allDefinitions = MyDefinitionManager.Static.GetAllDefinitions();

                if (allDefinitions.Count == 0)
                {
                    Logger.Log("ModSessionComponent", "allDefinitions item count is 0");
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
                    Logger.Log("ModSessionComponent", "componentsDefinitions item count is 0");
                    return;
                }

                if (oresDefinitions.Count == 0)
                {
                    Logger.Log("ModSessionComponent", "oresDefinitions item count is 0");
                    return;
                }

                if (ingotDefinitions.Count == 0)
                {
                    Logger.Log("ModSessionComponent", "ingotDefinitions item count is 0");
                    return;
                }

                if (ammoDefinition.Count == 0)
                {
                    Logger.Log("ModSessionComponent", "ammoDefinition item count is 0");
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

                Logger.Log("ModSessionComponent", "Finished getting item definitions");
            }
            catch (Exception ex)
            {
                Logger.Log("ModSessionComponent", $"all definitions failed {ex}");
            }
        }

        private void Create_All_Possible_Entries()
        {
            var wat1 = Stopwatch.StartNew();
            var stringBuilder = new StringBuilder(NameToDefinitionMap.Count * 20);
            const string separator = " | ";
            string lastType = null;
            stringBuilder.AppendLine("<Trash filter OFF>");

            foreach (var name in NameToDefinitionMap)
            {
                var currentType = name.Value.TypeId.ToString();
                if (lastType != currentType)
                {
                    if (lastType != null) stringBuilder.AppendLine();
                    stringBuilder.AppendLine($"// {currentType}");
                    stringBuilder.AppendLine();
                    lastType = currentType;
                }

                stringBuilder.AppendLine($"{name.Key}{separator}0{separator}0");
            }

            Guide_Data = stringBuilder.ToString();
            wat1.Stop();
            Logger.Log("ModSessionComponent",
                $"Creating all entries took {wat1.Elapsed.Milliseconds}ms, amount of entries sorters {NameToDefinitionMap.Count}");
        }

        private static void AddToDictionaries(MyDefinitionBase definition)
        {
            var name = definition.DisplayNameText;

            NameToDefinitionMap[name] = definition.Id;
            ItemsDictionaryReference[definition.Id] = new FixedPointReference(0);
            ProcessedItems.Add(definition.Id);
        }
    }
}