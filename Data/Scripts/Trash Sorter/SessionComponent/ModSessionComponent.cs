using System;
using System.Collections.Generic;
using System.Linq;
using Trash_Sorter.Data.Scripts.Trash_Sorter.StorageClasses;
using VRage.Utils;
using VRage.Game;
using VRage.Game.Components;
using Sandbox.Definitions;
using System.Diagnostics;
using System.Text;
using VRage.Game.ModAPI;
using VRage.ModAPI;

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

        public static string GuideData { get; private set; }

        public static Dictionary<string, MyDefinitionId> NameToDefinitionMap { get; private set; }
        public static HashSet<MyDefinitionId> ProcessedItemsDefinitions { get; private set; }
        public static ObservableDictionary<MyDefinitionId> ItemStorageReference { get; private set; }

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
            ProcessedItemsDefinitions = new HashSet<MyDefinitionId>();
            ItemStorageReference = new ObservableDictionary<MyDefinitionId>();
            GetDefinitions();
            CreateGuideEntries();
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
        private static void AddToDictionaries(MyDefinitionBase definition)
        {
            var name = definition.DisplayNameText;

            NameToDefinitionMap[name] = definition.Id;
            ItemStorageReference[definition.Id] = new FixedPointReference(0);
            ProcessedItemsDefinitions.Add(definition.Id);
        }

        private static void CreateGuideEntries()
        {
            var wat1 = Stopwatch.StartNew();
            var stringBuilder = new StringBuilder(NameToDefinitionMap.Count * 50);
            const string separator = " | ";
            string lastType = null;
            stringBuilder.AppendLine("<Trash filter OFF>");

            foreach (var name in NameToDefinitionMap)
            {
                var currentType = name.Value.TypeId.ToString();

                // Check if type changed and only append a single newline for separation
                if (lastType != currentType)
                {
                    stringBuilder.AppendLine(); // Only one blank line when the type changes
                    stringBuilder.AppendLine($"// {currentType}"); // Append type with one line before
                    stringBuilder.AppendLine();

                    lastType = currentType;
                }

                // Append the entry data with separator
                stringBuilder.AppendLine($"{name.Key}{separator}0{separator}0");
            }

            GuideData = stringBuilder.ToString();
            wat1.Stop();
            Logger.Log("ModSessionComponent",
                $"Creating all entries took {wat1.Elapsed.Milliseconds}ms, amount of entries sorters {NameToDefinitionMap.Count}");
        }



    }
}