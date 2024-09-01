using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
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
        private readonly Stopwatch StopWatch;

        private string Guide_Data;

        public ModConveyor_Sorter_Manager(HashSet<IMyConveyorSorter> sorters, Main_Storage_Class.Main_Storage_Class mainAccess)
        {
            var watch = Stopwatch.StartNew();
            Trash_Sorters = sorters;
            Sorters_Custom_Data_Dictionary = new Dictionary<IMyConveyorSorter, string>();
            Sorter_Filter = new Dictionary<IMyConveyorSorter, ModFilterCollection>();
            Observable_Dictionary_Reference = mainAccess.ItemsDictionary;
            Definitions_Reference = mainAccess.DefinitionIdToName;
            StopWatch=new Stopwatch();


            SorterInit();
            Create_All_Possible_Entries();


            watch.Stop();
            Logger.Instance.Log(ClassName, $"Initialization took {watch.Elapsed.Milliseconds}ms");
        }


        private void SorterInit()
        {
            foreach (var sorter in Trash_Sorters)
            {
                Add_Sorter(sorter);
            }
        }

        private void Add_Sorter(IMyConveyorSorter sorter)
        {
            sorter.OnClose += Sorter_OnClose;
            var terminal = (IMyTerminalBlock)sorter;
            terminal.CustomNameChanged += Terminal_CustomNameChanged;
            TryApplySorterFilter(sorter);

            //sorter.SetFilter();
        }


        private void TryApplySorterFilter(IMyConveyorSorter sorter)
        {
            // If CustomData is not empty, parse it
            if (!string.IsNullOrWhiteSpace(sorter.CustomData))
            {
                Super_Complicated_Parser(sorter.CustomData, sorter);
            }
        }


        private void Super_Complicated_Parser(string customData, IMyConveyorSorter sorter)
        {
            var aggregatedData = new StringBuilder();
            var lines = customData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var valuesReached = false;
            foreach (var line in lines)
            {
                // Trim any leading or trailing whitespace
                var trimmedLine = line.Trim();
                // Skip lines that start with "//"
                if (trimmedLine.StartsWith("//"))
                {
                    aggregatedData.AppendLine(trimmedLine);
                    continue;
                }

                if (!valuesReached)
                {
                    // Check if the line matches "<Trash sorter filter>"
                    if (trimmedLine == "<Trash sorter filter>")
                    {
                        aggregatedData.AppendLine(trimmedLine);
                        valuesReached = true;
                    }

                    continue;
                }


                ModFilterItem item;
                aggregatedData.AppendLine(ProcessLine(line, sorter, out item));
            }

            var result = aggregatedData.ToString();

            Sorters_Custom_Data_Dictionary[sorter] = result;
            sorter.CustomData = result;
        }

        private void Terminal_CustomNameChanged(IMyTerminalBlock obj)
        {
            var name = obj.CustomName;
            if (!name.Contains(GuideCall)) return;
            obj.CustomName = name.Replace(GuideCall, string.Empty);
            obj.CustomData = Guide_Data;
        }

        private void Create_All_Possible_Entries()
        {
            // Called once at init to create a string collection for player use.
            var stringBuilder = new StringBuilder();
            const string separator = " | ";
            foreach (var name in Definitions_Reference.Keys)
            {
                stringBuilder.AppendLine(name + separator + 0 + separator + 0);
            }

            Guide_Data = stringBuilder.ToString();
        }

        private string ProcessLine(string trimmedLine, IMyConveyorSorter sorter, out ModFilterItem filterItem)
        {
            // Split the line by '|' and keep each part intact
            var parts = trimmedLine.Split(new[] { '|' }, StringSplitOptions.None);
            var firstEntry = parts[0].Trim();
            ModFilterCollection modFilterCollection;

            // Checks if filter item already exists.
            if (!Sorter_Filter.TryGetValue(sorter, out modFilterCollection))
            {
                modFilterCollection = new ModFilterCollection(sorter, null);
            }

            // Log the first entry for debugging
            Logger.Instance.Log(ClassName, $"First ID value: {firstEntry}");

            // Initialize the new line string
            var newLine = "";

            // Check if the first entry is a valid identifier
            MyDefinitionId definitionId;
            if (!Definitions_Reference.TryGetValue(firstEntry, out definitionId))
            {
                // Return a comment if the identifier is not valid
                filterItem = null;
                return $"// {firstEntry} is not a valid identifier. If you need all possible entries, add to sorter tag [GUIDE]";
            }

            // Add the valid identifier to the new line
            newLine += firstEntry + " | ";

            // Initialize default values
            float requestedAmount = 0;
            float maxLimitTrigger;

            // Parse the second part if available, set default otherwise
            if (parts.Length > 0)
            {
                if (!float.TryParse(parts[1].Trim(), out requestedAmount))
                {
                    requestedAmount = 0; // Default value if parsing fails
                }
            }

            newLine += requestedAmount + " | ";

            // Parse the third part if available, set default otherwise
            if (parts.Length > 1)
            {
                if (!float.TryParse(parts[2].Trim(), out maxLimitTrigger))
                {
                    maxLimitTrigger = requestedAmount + 100; // Default value if parsing fails
                }
            }
            else
            {
                maxLimitTrigger = requestedAmount + 100; // Default value when not provided
            }

            if (requestedAmount < 0)
            {
                requestedAmount = -1;
            }

            filterItem = Create_Filter(definitionId, requestedAmount, maxLimitTrigger);

            newLine += maxLimitTrigger;

            // Return the constructed new line
            return newLine;
        }

        private ModFilterItem Create_Filter(MyDefinitionId definitionId, float requestedAmount, float maxLimitTrigger)
        {
            return new ModFilterItem(definitionId, Observable_Dictionary_Reference[definitionId],
                (MyFixedPoint)requestedAmount,
                (MyFixedPoint)maxLimitTrigger, Observable_Dictionary_Reference);
        }


        private void Sorter_OnClose(VRage.ModAPI.IMyEntity obj)
        {
            obj.OnClose -= Sorter_OnClose;
            var sorter = (IMyConveyorSorter)obj;
            Trash_Sorters.Remove(sorter);
        }


        public void OnAfterSimulation100()
        {
            if (ModSorterTime.FunctionTimes > 10)
            {
                Logger.Instance.LogWarning(ClassName,$"Conveyor sorters processing took {ModSorterTime.FunctionTimes}ms");
            }

            ModSorterTime.FunctionTimes = 0;
        }
    }
}