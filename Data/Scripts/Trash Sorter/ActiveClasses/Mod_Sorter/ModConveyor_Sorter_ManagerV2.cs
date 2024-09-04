using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Sandbox.ModAPI;
using Trash_Sorter.Data.Scripts.Trash_Sorter.BaseClass;
using Trash_Sorter.Data.Scripts.Trash_Sorter.Main_Storage_Class;
using VRage;
using VRage.Game;
using VRageMath;

namespace Trash_Sorter.Data.Scripts.Trash_Sorter.ActiveClasses.Mod_Sorter
{
    internal class ModConveyorSorterManagerV2 : ModBase
    {
        // ReSharper disable InconsistentNaming
        public HashSet<IMyConveyorSorter> Trash_Sorters; // List of sorters
        public SorterDataStorage SorterDataStorageRef; // Dictionary of k-Sorter, v = SorterCustomData

        public Dictionary<MyDefinitionId, SorterLimitManager>
            DictionarySorterLimitsManagers; // Dictionary of id to sorterLimitManager

        private readonly Dictionary<string, MyDefinitionId>
            Definitions_Reference; // String output - Definition transformer

        private readonly InventoryGridManager InventoryGridManager; // Event link
        private readonly Stopwatch watch = new Stopwatch();


        private string Guide_Data;

        public ModConveyorSorterManagerV2(HashSet<IMyConveyorSorter> sorters,
            MainStorageClass mainAccess, InventoryGridManager inventoryGridManager,
            Dictionary<MyDefinitionId, SorterLimitManager> dictionarySorterLimitsManagers,
            Dictionary<string, MyDefinitionId> nameToDefinition)
        {
            watch.Start();
            Trash_Sorters = sorters;
            DictionarySorterLimitsManagers = dictionarySorterLimitsManagers;


            SorterDataStorageRef = new SorterDataStorage(nameToDefinition);
            Definitions_Reference = mainAccess.NameToDefinition;
            InventoryGridManager = inventoryGridManager;


            inventoryGridManager.OnTrashSorterAdded += Add_Sorter;
            Create_All_Possible_Entries();
            SorterInit();


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
            watch.Restart();
            // Called once at init to create a string collection for player use.
            var stringBuilder = new StringBuilder(Definitions_Reference.Count * 50); // Estimate initial capacity
            const string separator = " | ";
            string lastType = null;

            stringBuilder.AppendLine("\"<Trash filter>\"");

            foreach (var name in Definitions_Reference)
            {
                var currentType = name.Value.TypeId.ToString();

                if (lastType != currentType)
                {
                    if (lastType != null)
                    {
                        stringBuilder.AppendLine();
                    }

                    stringBuilder.AppendLine($"// {currentType}");
                    stringBuilder.AppendLine();
                    lastType = currentType;
                }

                stringBuilder.AppendLine($"{name.Key}{separator}0{separator}0");
            }

            Guide_Data = stringBuilder.ToString();
            watch.Stop();
            Logger.Instance.Log(ClassName,
                $"Creating all entries took {watch.Elapsed.Milliseconds}ms, amount of entries sorters {Definitions_Reference.Count}");
        }

        private void Add_Sorter(IMyConveyorSorter sorter)
        {
            watch.Restart();
            sorter.OnClose += Sorter_OnClose;

            sorter.CustomNameChanged += Terminal_CustomNameChanged;
            SorterDataStorageRef.AddOrUpdateSorterRawData(sorter);
            Update_Values(sorter);
            watch.Stop();
            Logger.Instance.Log(ClassName, $"Adding sorter has taken {watch.ElapsedMilliseconds}ms");
        }


        private void Try_Updating_Values(IMyConveyorSorter sorter)
        {
            if (string.IsNullOrWhiteSpace(sorter.CustomData) && SorterDataStorageRef.IsEmpty(sorter))
            {
                return;
            }

            if (!SorterDataStorageRef.HasCustomDataChanged(sorter))
            {
                return;
            }

            Update_Values(sorter);
            SorterDataStorageRef.AddOrUpdateSorterRawData(sorter);
        }


        private void Update_Values(IMyConveyorSorter sorter)
        {
            Logger.Instance.Log(ClassName, $"Starting values update");
            watch.Restart();
            List<MyDefinitionId> removedEntries;
            List<string> addedEntries;
            Dictionary<string, string> changedEntries;
            var data = SorterDataStorageRef.TrackChanges(sorter,
                out removedEntries, out addedEntries, out changedEntries);

            // Process and replace removed entries
            foreach (var line in removedEntries)
            {
                ProcessDeletedLine(line, sorter);
            }

            foreach (var line in addedEntries)
            {
                var newLine = ProcessNewLine(line, sorter);

                // Replace the line in 'data'
                var index = data.IndexOf(line);
                if (index != -1)
                {
                    data[index] = newLine;
                }
            }

            foreach (var kvp in changedEntries)
            {
                var newLine = ProcessChangedLine(kvp.Key, kvp.Value, sorter);
                var index = data.IndexOf(kvp.Key + " | " + kvp.Value);
                if (index != -1)
                {
                    data[index] = newLine;
                }
            }

            watch.Stop();
            Logger.Instance.Log(ClassName, $"Updating values taken {watch.ElapsedMilliseconds}");
        }


        private string ProcessNewLine(string trimmedLine, IMyConveyorSorter sorter)
        {
            var parts = trimmedLine.Split(new[] { '|' }, StringSplitOptions.None);
            var firstEntry = parts[0].Trim();


            MyDefinitionId definitionId;
            if (!Definitions_Reference.TryGetValue(firstEntry, out definitionId))
            {
                return
                    $"// {firstEntry} is not a valid identifier. If you need all possible entries, add to sorter tag [GUIDE]";
            }

            double itemRequestAmount = 0;
            double itemRequestLimit = 0;

            switch (parts.Length)
            {
                case 1:
                    break;
                case 2:
                    double.TryParse(parts[1].Trim(), out itemRequestAmount);
                    if (itemRequestAmount < 0) itemRequestAmount = -1;
                    break;
                case 3:
                    double.TryParse(parts[1].Trim(), out itemRequestAmount);
                    if (!double.TryParse(parts[2].Trim(), out itemRequestLimit) ||
                        itemRequestLimit <= itemRequestAmount)
                    {
                        itemRequestLimit = itemRequestAmount + itemRequestAmount * 0.5;
                    }

                    if (itemRequestAmount < 0) itemRequestAmount = -1;

                    break;
            }

            // Create filter if request is above or below 0;
            var itemLimit = new ItemLimit()
            {
                ItemRequestedAmount = (MyFixedPoint)itemRequestAmount,
                ItemTriggerAmount = (MyFixedPoint)itemRequestLimit,
                OverLimitTrigger = false
            };
            SorterLimitManager limitManager;
            if (!DictionarySorterLimitsManagers.TryGetValue(definitionId, out limitManager))
                return "This is bad, you better don't see this line.";

            if (itemRequestAmount != 0)
            {
                limitManager.RegisterSorter(sorter, itemLimit);
            }

            // Return the line back for custom data.
            return $"{firstEntry} | {itemRequestAmount} | {itemRequestLimit}";
        }

        private void ProcessDeletedLine(MyDefinitionId defId, IMyConveyorSorter sorter)
        {
            SorterLimitManager limitManager;
            if (!DictionarySorterLimitsManagers.TryGetValue(defId, out limitManager))
            {
                Logger.Instance.LogError(ClassName, "This is bad, you better don't see this line.");
                return;
            }

            limitManager.UnRegisterSorter(sorter);
        }

        private string ProcessChangedLine(string defId, string combinedValue, IMyConveyorSorter sorter)
        {
            MyDefinitionId definitionId;
            if (!Definitions_Reference.TryGetValue(defId, out definitionId))
            {
                return
                    $"// {defId} is not a valid identifier. If you need all possible entries, add to sorter tag [GUIDE]";
            }

            double itemRequestAmount;
            double itemTriggerAmount;

            var parts = combinedValue.Split(new[] { '|' }, StringSplitOptions.None);
            double.TryParse(parts[1].Trim(), out itemRequestAmount);
            if (!double.TryParse(parts[2].Trim(), out itemTriggerAmount) ||
                itemTriggerAmount <= itemRequestAmount)
            {
                itemTriggerAmount = itemRequestAmount + itemRequestAmount * 0.5;
            }

            if (itemRequestAmount < 0) itemRequestAmount = -1;
            SorterLimitManager limitManager;
            if (!DictionarySorterLimitsManagers.TryGetValue(definitionId, out limitManager))
                return "This is bad, you better don't see this line.";

            if (itemRequestAmount == 0)
            {
                limitManager.UnRegisterSorter(sorter);
            }
            else
            {
                limitManager.ChangeLimitsOnSorter(sorter, (MyFixedPoint)itemRequestAmount,
                    (MyFixedPoint)itemTriggerAmount);
            }


            return $"{defId} | {itemRequestAmount} | {itemTriggerAmount}";
        }

        private void Terminal_CustomNameChanged(IMyTerminalBlock obj)
        {
            var name = obj.CustomName;
            if (name.IndexOf(GuideCall, StringComparison.OrdinalIgnoreCase) < 0) return;

            Logger.Instance.Log(ClassName, $"Sorter guide detected, {name}");
            obj.CustomName = Regex.Replace(obj.CustomName, Regex.Escape(GuideCall), string.Empty,
                RegexOptions.IgnoreCase);
            obj.CustomData = Guide_Data;
            SorterDataStorageRef.AddOrUpdateSorterRawData((IMyConveyorSorter)obj);
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
            watch.Restart();
            foreach (var sorter in Trash_Sorters)
            {
                Try_Updating_Values(sorter);
            }

            watch.Stop();

            // Logger.Instance.LogWarning(ClassName,$"Custom data parsing has taken {watch.ElapsedMilliseconds}, SorterLimitManager has taken total {DebugTimeClass.TimeOne.Milliseconds}ms");
            DebugTimeClass.TimeOne = TimeSpan.Zero;
        }

        public override void Dispose()
        {
            base.Dispose();
            InventoryGridManager.OnTrashSorterAdded -= Add_Sorter;
            foreach (var sorter in Trash_Sorters.ToList())
            {
                Sorter_OnClose(sorter);
            }
        }
    }
}