using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Sandbox.Game.Entities.Cube;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Ingame;
using Trash_Sorter.Data.Scripts.Trash_Sorter.BaseClass;
using Trash_Sorter.Data.Scripts.Trash_Sorter.Main_Storage_Class;
using VRage;
using VRage.Game;
using IMyConveyorSorter = Sandbox.ModAPI.IMyConveyorSorter;
using IMyTerminalBlock = Sandbox.ModAPI.IMyTerminalBlock;

namespace Trash_Sorter.Data.Scripts.Trash_Sorter.ActiveClasses.Mod_Conveyor_Sorter_Manager
{
    internal class ModConveyor_Sorter_Manager : ModBase
    {
        // ReSharper disable InconsistentNaming
        public HashSet<IMyConveyorSorter> Trash_Sorters;

        public Dictionary<IMyConveyorSorter, ModFilterCollection> Sorter_Filter;

        public Dictionary<IMyConveyorSorter, string> Sorters_Custom_Data_Dictionary;


        private readonly Dictionary<string, MyDefinitionId> Definitions_Reference;
        private readonly ObservableDictionary<MyDefinitionId, MyFixedPoint> Observable_Dictionary_Reference;
        private readonly Inventory_Grid_Manager Inventory_Grid_Manager;

        private TimeSpan debugTime = TimeSpan.Zero;
        private TimeSpan debugTime2 = TimeSpan.Zero;
        private TimeSpan debugTime3 = TimeSpan.Zero;

        private string Guide_Data;

        public ModConveyor_Sorter_Manager(HashSet<IMyConveyorSorter> sorters,
            Main_Storage_Class.Main_Storage_Class mainAccess, Inventory_Grid_Manager inventoryGridManager)
        {
            var watch = Stopwatch.StartNew();
            Trash_Sorters = sorters;
            Sorters_Custom_Data_Dictionary = new Dictionary<IMyConveyorSorter, string>();
            Sorter_Filter = new Dictionary<IMyConveyorSorter, ModFilterCollection>();
            Observable_Dictionary_Reference = mainAccess.ItemsDictionary;
            Definitions_Reference = mainAccess.NameToDefinition;
            Inventory_Grid_Manager = inventoryGridManager;
            inventoryGridManager.OnTrashSorterAdded += Add_Sorter;

            SorterInit();
            Create_All_Possible_Entries();


            watch.Stop();
            Logger.Instance.Log(ClassName,
                $"Initialization took {watch.Elapsed.Milliseconds}ms, amount of trash sorters {Trash_Sorters.Count}");
        }


        private void SorterInit()
        {
            foreach (var sorter in Trash_Sorters)
            {
                Add_Sorter(sorter);
            }
        }

        private void Create_All_Possible_Entries()
        {
            // Called once at init to create a string collection for player use.
            var stringBuilder = new StringBuilder();
            const string separator = " | ";
            var lastType = "";

            const string importantShit = "<Trash sorter filter>";
            stringBuilder.AppendLine(importantShit);
            foreach (var name in Definitions_Reference)
            {
                if (lastType != name.Value.TypeId.ToString())
                {
                    stringBuilder.AppendLine();
                    stringBuilder.AppendLine($"// {name.Value.TypeId}");
                    stringBuilder.AppendLine();
                    lastType = name.Value.TypeId.ToString();
                }

                stringBuilder.AppendLine(name.Key + separator + 0 + separator + 0);
            }

            Guide_Data = stringBuilder.ToString();
        }

        private void Add_Sorter(IMyConveyorSorter sorter)
        {
            Logger.Instance.Log(ClassName, $"Sorter added {sorter.CustomName}");
            sorter.OnClose += Sorter_OnClose;
            var terminal = (IMyTerminalBlock)sorter;
            terminal.CustomNameChanged += Terminal_CustomNameChanged;
            Sorters_Custom_Data_Dictionary[sorter] = "Original";
            TryApplySorterFilter(sorter);

            //sorter.SetFilter();
        }


        private void TryApplySorterFilter(IMyConveyorSorter sorter)
        {
            // If CustomData is not empty, parse it
            if (string.IsNullOrWhiteSpace(sorter.CustomData))
            {
                return;
            }

            string customData;
            if (!Sorters_Custom_Data_Dictionary.TryGetValue(sorter, out customData) || string.IsNullOrEmpty(customData))
            {
                Logger.Instance.Log(ClassName, $"Custom data empty or no key value exists");
                return; // Early exit if the sorter is not found or customData is null/empty
            }

            // If the current customData is the same as the stored data, do nothing
            if (sorter.CustomData == Sorters_Custom_Data_Dictionary[sorter])
            {
                return;
            }


            Super_Complicated_Parser(sorter.CustomData, sorter);
        }


        private void Super_Complicated_Parser(string customData, IMyConveyorSorter sorter)
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            Logger.Instance.Log(ClassName, $"Parsing sorter data");

            // Check if customData is null or empty
            if (string.IsNullOrWhiteSpace(customData))
            {
                Logger.Instance.Log(ClassName, "Custom data is empty or null.");
                return;
            }

            var aggregatedData = new StringBuilder();
            var lines = customData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // Retrieve or create ModFilterCollection for the sorter
            ModFilterCollection modFilterCollection;
            if (!Sorter_Filter.TryGetValue(sorter, out modFilterCollection))
            {
                modFilterCollection = new ModFilterCollection(sorter, null);
                Sorter_Filter[sorter] = modFilterCollection;
            }

            stopwatch.Stop();
            Logger.Instance.LogWarning(ClassName, $"Step 1 of parse took {stopwatch.ElapsedMilliseconds}ms");
            stopwatch.Restart();
            var TriggerReached = false;

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmedLine))
                {
                    aggregatedData.AppendLine();
                    continue;
                }

                if (!TriggerReached)
                {
                    if (trimmedLine.Contains("<Trash sorter filter>")) TriggerReached = true;
                    aggregatedData.AppendLine(trimmedLine);
                    continue;
                }

                // Handle comments
                if (trimmedLine.StartsWith("//"))
                {
                    aggregatedData.AppendLine(trimmedLine);
                    continue;
                }

                // Process the line if we're in the values section
                ProcessLineAndAddToCollection(trimmedLine, sorter, modFilterCollection, aggregatedData);
            }

            stopwatch.Stop();
            Logger.Instance.LogWarning(ClassName,
                $"Total time taken to read lines data: {stopwatch.ElapsedMilliseconds}ms or around {stopwatch.ElapsedMilliseconds / lines.Length}ms per line");
            Logger.Instance.LogWarning(ClassName,
                $"Total checking for existing item {debugTime.Milliseconds}ms, creating new item {debugTime2.Milliseconds}ms");
            debugTime = TimeSpan.Zero;
            debugTime2 = TimeSpan.Zero;
            debugTime3 = TimeSpan.Zero;
            var result = aggregatedData.ToString();

            // Ensure the result is meaningful before setting it
            if (!string.IsNullOrWhiteSpace(result))
            {
                Sorters_Custom_Data_Dictionary[sorter] = result;
                sorter.CustomData = result;
            }
            else
            {
                Logger.Instance.Log(ClassName, "No valid data to update sorter custom data.");
            }
        }

        private void ProcessLineAndAddToCollection(string line, IMyConveyorSorter sorter,
            ModFilterCollection modFilterCollection, StringBuilder aggregatedData)
        {
            ModFilterItem item;
            var processedLine = ProcessLine(line, out item, sorter);

            aggregatedData.AppendLine(processedLine);

            if (item == null) return;

            modFilterCollection.Add_ModFilterItem(item);
        }


        private string ProcessLine(string trimmedLine, out ModFilterItem filterItem, IMyConveyorSorter sorter)
        {
            // If the line is empty or consists only of whitespace, skip it

            filterItem = null;
            var parts = trimmedLine.Split(new[] { '|' }, StringSplitOptions.None);
            var firstEntry = parts[0].Trim();


            MyDefinitionId definitionId;
            if (!Definitions_Reference.TryGetValue(firstEntry, out definitionId))
            {
                return
                    $"// {firstEntry} is not a valid identifier. If you need all possible entries, add to sorter tag [GUIDE]";
            }

            double requestedAmount = 0;
            double maxLimitTrigger = 0;

            switch (parts.Length)
            {
                case 1:
                    break;
                case 2:
                    double.TryParse(parts[1].Trim(), out requestedAmount);
                    break;
                case 3:
                    double.TryParse(parts[1].Trim(), out requestedAmount);
                    if (!double.TryParse(parts[2].Trim(), out maxLimitTrigger) ||
                        maxLimitTrigger <= requestedAmount)
                    {
                        maxLimitTrigger = requestedAmount + requestedAmount * 0.5;
                    }

                    break;
            }


            var stopwatch3 = Stopwatch.StartNew();
            // Create filter if request is above or below 0;
            filterItem = requestedAmount == 0
                ? null
                : Create_Filter(definitionId, requestedAmount, maxLimitTrigger, sorter);

            // Return the line back for custom data.
            stopwatch3.Stop();
            debugTime3 += stopwatch3.Elapsed;
            return $"{firstEntry} | {requestedAmount} | {maxLimitTrigger}";
        }

        private void Terminal_CustomNameChanged(IMyTerminalBlock obj)
        {
            var name = obj.CustomName;
            if (name.IndexOf(GuideCall, StringComparison.OrdinalIgnoreCase) < 0) return;

            Logger.Instance.Log(ClassName, $"Sorter guide detected, {name}");
            obj.CustomName = Regex.Replace(name, Regex.Escape(GuideCall), string.Empty, RegexOptions.IgnoreCase);
            obj.CustomData = Guide_Data;
            Sorters_Custom_Data_Dictionary[(IMyConveyorSorter)obj] = obj.CustomData;
        }


        private ModFilterItem Create_Filter(MyDefinitionId definitionId, double requestedAmount, double maxLimitTrigger, IMyConveyorSorter sorter)
        {
            // Check if the item already exists
            ModFilterItem filterItem;
            if (Sorter_Filter[sorter].ContainsId(definitionId, out filterItem))
            {
                // Update the existing item's properties
                filterItem.Update_ModFilterItem((MyFixedPoint)requestedAmount, (MyFixedPoint)maxLimitTrigger);
                return filterItem;
            }

            // Create a new item only if it doesn't exist
            var newItem = new ModFilterItem(
                definitionId,
                (MyFixedPoint)requestedAmount,
                (MyFixedPoint)maxLimitTrigger,
                Observable_Dictionary_Reference
            );

            Sorter_Filter[sorter].Add_ModFilterItem(newItem);

            return newItem;
        }



        private void Sorter_OnClose(VRage.ModAPI.IMyEntity obj)
        {
            obj.OnClose -= Sorter_OnClose;
            var terminal = (IMyTerminalBlock)obj;
            terminal.CustomNameChanged += Terminal_CustomNameChanged;
            var sorter = (IMyConveyorSorter)obj;
            Trash_Sorters.Remove(sorter);
        }

        public void OnAfterSimulation100()
        {
            if (ModSorterTime.FunctionTimes > 10)
            {
                Logger.Instance.LogWarning(ClassName,
                    $"Conveyor sorters processing took {ModSorterTime.FunctionTimes}ms");
            }

            foreach (var sorter in Sorters_Custom_Data_Dictionary.Keys.ToList())
            {
                TryApplySorterFilter(sorter);
            }

            ModSorterTime.FunctionTimes = 0;
        }

        public void OnAfterSimulation()
        {
            //If I cant make the parser not nuke game for half a sec I will not need this
        }

        public override void Dispose()
        {
            base.Dispose();
            Inventory_Grid_Manager.OnTrashSorterAdded -= Add_Sorter;
            foreach (var sorter in Sorter_Filter.Keys)
            {
                Sorter_OnClose(sorter);
            }
        }
    }
}