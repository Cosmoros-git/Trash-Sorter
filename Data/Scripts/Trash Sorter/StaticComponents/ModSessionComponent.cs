using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Sandbox.Definitions;
using Sandbox.ModAPI;
using Trash_Sorter.StorageClasses;
using VRage.Game;
using VRage.Game.Components;
using VRage.Utils;

namespace Trash_Sorter.StaticComponents
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class ModSessionComponent : MySessionComponentBase
    {

        public static readonly Guid Guid = new Guid("f6ea728c-8890-4012-8c81-165593a65b86");

        public static readonly HashSet<string> UniqueModExceptions = new HashSet<string>()
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



        private static int _updateCooldownLimit = 2000;
        private static int _blockLimitsToStartManaging = 10;

        public static bool IsLoggerEnabled = true;
        public static int UpdateCooldownLimit
        {
            get { return _updateCooldownLimit; }
            private set
            {
                if (value > 0)
                {
                    _updateCooldownLimit = value;
                }
            }
        }
        public static int BlockLimitsToStartManaging
        {
            get { return _blockLimitsToStartManaging; }
            private set
            {
                if (value > 0)
                {
                    _blockLimitsToStartManaging = value;
                }
            }
        }


        private const string SettingLoc = "Trash_Sorter_Settings.txt";

        private const string DefaultSettings =
            "Minimum amount of blocks for grid to be managed = 10 \n Time between trying to initialize (in frames) = 2000 \n Is Logger Enabled = true";

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
            UpdateCooldownLimit = 200;
            BlockLimitsToStartManaging = 10;
            GetDefinitions();
            GetSettings();
            CreateGuideEntries();
        }

        private static void GetSettings()
        {
            try
            {
                // Define the file path and name (this will be saved in the world storage folder)

                // Check if the file exists in world storage
                if (MyAPIGateway.Utilities.FileExistsInWorldStorage(SettingLoc, typeof(ModSessionComponent)))
                {
                    using (var reader =
                           MyAPIGateway.Utilities.ReadFileInWorldStorage(SettingLoc,
                               typeof(ModSessionComponent)))
                    {
                        var content = reader.ReadToEnd();
                        Logger.Log("ModSessionComponent", $"Settings loaded: {content}");

                        // You can now deserialize or process the file content based on the format
                        ParseSettings(content);
                    }
                }
                else
                {
                    Logger.Log("ModSessionComponent", "Settings file not found. Creating default settings.");
                    // You can create and save default settings if the file doesn't exist
                    CreateDefaultSettings();
                }
            }
            catch (Exception ex)
            {
                Logger.Log("ModSessionComponent", $"Error loading settings: {ex}");
            }
        }
        private static void ParseSettings(string content)
        {
            var lines = content.Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var line in lines)
            {
                var parts = line.Split(new[] { '=' }, 2); // Split by the first '='
                if (parts.Length != 2)
                {
                    CreateDefaultSettings();
                    Logger.Log("ModSessionComponent", "Error on parsing settings, resetting");
                    return;
                }

                var key = parts[0].Trim();
                var value = parts[1].Trim();

                // Process the key-value pair
                Logger.Log("ModSessionComponent", $"Setting: {key} = {value}");

                switch (key)
                {
                    case "Minimum amount of blocks for grid to be managed":
                    {
                        if (int.TryParse(value, out _blockLimitsToStartManaging))
                        {
                            Logger.Log("ModSessionComponent",
                                $"Parsed Minimum amount of blocks: {BlockLimitsToStartManaging}");
                            // Store the parsed value as needed
                        }

                        break;
                    }
                    case "Time between trying to initialize (in frames)":
                    {
                        if (!int.TryParse(value, out _updateCooldownLimit)) continue;
                        Logger.Log("ModSessionComponent", $"Parsed Initialization time: {UpdateCooldownLimit}");
                        break;
                    }
                    case "Is Logger Enabled":
                        if(!bool.TryParse(value, out IsLoggerEnabled)) continue;
                        Logger.Log("ModSessionComponent", $"Logger has been set to {IsLoggerEnabled}");
                        if(!IsLoggerEnabled) Logger.LogWarning("TrashSorter_SessionComponent","Logging has been disabled. Bye.");
                        Logger.IsEnabled = IsLoggerEnabled;
                        break;
                }
            }
        }
        private static void CreateDefaultSettings()
        {
            try
            {
                using (var writer =
                       MyAPIGateway.Utilities.WriteFileInWorldStorage(SettingLoc, typeof(ModSessionComponent)))
                {
                    writer.Write(DefaultSettings);
                    Logger.Log("ModSessionComponent", "Default settings created and saved.");
                }
            }
            catch (Exception ex)
            {
                Logger.Log("ModSessionComponent", $"Error creating default settings: {ex}");
            }
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