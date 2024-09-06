using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Sandbox.ModAPI.Ingame;
using Trash_Sorter.Data.Scripts.Trash_Sorter.BaseClass;
using Trash_Sorter.Data.Scripts.Trash_Sorter.Main_Storage_Class;
using VRage;
using VRage.Game;
using IMyConveyorSorter = Sandbox.ModAPI.IMyConveyorSorter;
using IMyTerminalBlock = Sandbox.ModAPI.IMyTerminalBlock;

namespace Trash_Sorter.Data.Scripts.Trash_Sorter.ActiveClasses.Mod_Sorter
{
    /// <summary>
    /// The ModConveyorManager class manages conveyor sorters and their custom data.
    /// It tracks sorters, processes custom data, and updates sorter filters based on changes.
    /// </summary>
    internal class ModSorterManager : ModBase
    {
        /// <summary>
        /// Collection of conveyor sorters.
        /// </summary>
        public HashSet<IMyConveyorSorter> ModSorterCollection;

        /// <summary>
        /// Reference to the SorterDataStorage which holds sorter custom data.
        /// </summary>
        public SorterDataStorage SorterDataStorageRef;

        /// <summary>
        /// Dictionary mapping item definitions to sorter limit managers.
        /// </summary>
        public Dictionary<MyDefinitionId, SorterLimitManager> SorterLimitManagers;

        /// <summary>
        /// Dictionary for transforming names into MyDefinitionId objects.
        /// </summary>
        private readonly Dictionary<string, MyDefinitionId> ItemNameToDefinitionMap;

        /// <summary>
        /// Manages the inventory grid and provides event linking.
        /// </summary>
        private readonly InventoryGridManager InventoryGridManager;

        /// <summary>
        /// Stopwatch to track operation time.
        /// </summary>
        private readonly Stopwatch watch = new Stopwatch();

        /// <summary>
        /// Guide data string used for generating user instructions.
        /// </summary>
        private string Guide_Data;

        private readonly Logger myLogger;

        /// <summary>
        /// Initializes a new instance of the ModConveyorManager class and registers events.
        /// </summary>
        /// <param name="sorters">HashSet of sorters.</param>
        /// <param name="mainItemAccess">Main item storage reference.</param>
        /// <param name="inventoryGridManager">Inventory grid manager instance.</param>
        /// <param name="sorterLimitManagers">Dictionary of sorter limit managers.</param>
        /// <param name="nameToDefinition">Dictionary mapping names to item definitions.</param>
        public ModSorterManager(HashSet<IMyConveyorSorter> sorters,
            MainItemStorage mainItemAccess, InventoryGridManager inventoryGridManager,
            Dictionary<MyDefinitionId, SorterLimitManager> sorterLimitManagers,
            Dictionary<string, MyDefinitionId> nameToDefinition, Logger MyLogger)
        {
            watch.Start();
            myLogger = MyLogger;
            ModSorterCollection = sorters;
            SorterLimitManagers = sorterLimitManagers;
            SorterDataStorageRef = new SorterDataStorage(nameToDefinition, myLogger);
            ItemNameToDefinitionMap = mainItemAccess.NameToDefinitionMap;
            InventoryGridManager = inventoryGridManager;

            // Registering event for sorter addition
            inventoryGridManager.OnModSorterAdded += Add_Sorter;

            // Create entries and initialize sorters
            Create_All_Possible_Entries();
            SorterInit();
            watch.Stop();
            myLogger.Log(ClassName,
                $"Initialization took {watch.Elapsed.Milliseconds}ms, amount of trash sorters {ModSorterCollection.Count}");
        }

        // Guide data is made here :)
        /// <summary>
        /// Creates all possible item entries for user reference.
        /// </summary>
        private void Create_All_Possible_Entries()
        {
            watch.Restart();
            var stringBuilder = new StringBuilder(ItemNameToDefinitionMap.Count * 50);
            const string separator = " | ";
            string lastType = null;
            stringBuilder.AppendLine("<Trash filter OFF>");

            foreach (var name in ItemNameToDefinitionMap)
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
            watch.Stop();
            myLogger.Log(ClassName,
                $"Creating all entries took {watch.Elapsed.Milliseconds}ms, amount of entries sorters {ItemNameToDefinitionMap.Count}");
        }

        /// <summary>
        /// Initializes each sorter by setting default values and registering events.
        /// </summary>
        private void SorterInit()
        {
            foreach (var sorter in ModSorterCollection)
            {
                Add_Sorter(sorter);
            }
        }

        /// <summary>
        /// Adds a new sorter, sets default values, and registers events.
        /// </summary>
        /// <param name="sorter">The sorter being added.</param>
        private void Add_Sorter(IMyConveyorSorter sorter)
        {
            var wat1 = Stopwatch.StartNew();
            sorter.SetFilter(MyConveyorSorterMode.Whitelist, new List<MyInventoryItemFilter>());
            sorter.DrainAll = true;
            wat1.Stop();
            myLogger.Log(ClassName, $"Adding filters to sorter has taken {wat1.Elapsed.TotalMilliseconds}ms");
            wat1.Restart();
            sorter.OnClose += Sorter_OnClose;
            sorter.CustomNameChanged += Terminal_CustomNameChanged;
            wat1.Stop();
            myLogger.Log(ClassName, $"Adding filters to sorter has taken {wat1.Elapsed.TotalMilliseconds}ms");
            wat1.Restart();
            ModSorterCollection.Add(sorter);

            SorterDataStorageRef.AddOrUpdateSorterRawData(sorter);
            myLogger.Log(ClassName,
                $"Adding to collection and updating datastorageref to sorter has taken {wat1.Elapsed.TotalMilliseconds}ms");
            wat1.Stop();
            wat1.Restart();
            Update_Values(sorter);
            wat1.Stop();
            myLogger.Log(ClassName, $"Updating values has taken {wat1.Elapsed.TotalMilliseconds}ms");
        }

        /// <summary>
        /// Tries to update the sorter's values if custom data has changed.
        /// </summary>
        /// <param name="sorter">The sorter to update.</param>
        private void Try_Updating_Values(IMyConveyorSorter sorter)
        {
            if (string.IsNullOrWhiteSpace(sorter.CustomData) && SorterDataStorageRef.IsEmpty(sorter))
                return;

            if (!SorterDataStorageRef.HasCustomDataChanged(sorter))
                return;

            Update_Values(sorter);
        }

        /// <summary>
        /// Updates the sorter's filter values based on changes in custom data.
        /// </summary>
        /// <param name="sorter">The sorter whose values are updated.</param>
        private void Update_Values(IMyConveyorSorter sorter)
        {
            bool hasFilterTagBeenFound;
            var wat1 = Stopwatch.StartNew();
            var data = SorterDataStorageRef.TrackChanges(sorter, out hasFilterTagBeenFound);
            myLogger.Log(ClassName,
                $"New entries {SorterDataStorageRef.AddedEntries.Count}, removed entries {SorterDataStorageRef.RemovedEntries.Count}, changed entries {SorterDataStorageRef.ChangedEntries.Count}, has filter been found {hasFilterTagBeenFound}");
            wat1.Stop();
            myLogger.Log(ClassName, $"Step 1 update {wat1.Elapsed.TotalMilliseconds}ms");
            wat1.Restart();
            if (hasFilterTagBeenFound)
            {
                var wat2 = Stopwatch.StartNew();
                var defIdList = new List<string>();
                foreach (var stringData in data)
                {
                    var defId = stringData.Split(new[] { '|' }, StringSplitOptions.None)[0].Trim().ToLower();
                    defIdList.Add(defId);
                }

                wat2.Stop();
                myLogger.Log(ClassName, $"Splitting data took took {wat2.Elapsed.TotalMilliseconds}ms");
                wat2.Restart();
                foreach (var line in SorterDataStorageRef.RemovedEntries)
                {
                    ProcessDeletedLine(line, sorter);
                }

                wat2.Stop();
                myLogger.Log(ClassName, $"Processing removed entires took {wat2.Elapsed.TotalMilliseconds}ms");
                wat2.Restart();
                foreach (var line in SorterDataStorageRef.AddedEntries)
                {
                    string idString;
                    var newLine = ProcessNewLine(line, sorter, out idString);
                    var index = defIdList.IndexOf(idString);
                    if (index != -1) data[index] = newLine;
                }

                wat2.Stop();
                myLogger.Log(ClassName, $"Processing added entires took {wat2.Elapsed.TotalMilliseconds}ms");
                wat2.Restart();
                foreach (var sorterChangedData in SorterDataStorageRef.ChangedEntries)
                {
                    var newLine = ProcessChangedLine(sorterChangedData.Key, sorterChangedData.Value, sorter);
                    var index = defIdList.FindIndex(defId =>
                        string.Equals(defId, sorterChangedData.Key.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (index != -1) data[index] = newLine;
                }

                wat2.Stop();
                myLogger.Log(ClassName, $"Processing changed entires took {wat2.Elapsed.TotalMilliseconds}ms");
            }

            wat1.Stop();
            myLogger.Log(ClassName, $"Step 2 update {wat1.Elapsed.TotalMilliseconds}ms");
            wat1.Restart();
            var stringBuilder = new StringBuilder();
            foreach (var t in data)
            {
                var line = t.Trim();
                stringBuilder.Append(line + "\n");
            }

            var newCustomData = stringBuilder.ToString();
            sorter.CustomData = newCustomData;
            SorterDataStorageRef.AddOrUpdateSorterRawData(sorter);
            wat1.Stop();
            myLogger.Log(ClassName, $"Step 3 update {wat1.Elapsed.TotalMilliseconds}ms");
        }


        // Functions to process new/removed/edited lines. TODO MAYBE LOOK INTO OPTIMIZING THIS
        private string ProcessNewLine(string trimmedLine, IMyConveyorSorter sorter, out string idString)
        {
            var parts = trimmedLine.Split(new[] { '|' }, StringSplitOptions.None);
            var firstEntry = parts[0].Trim();
            idString = firstEntry;
            MyDefinitionId definitionId;
            if (!ItemNameToDefinitionMap.TryGetValue(firstEntry, out definitionId))
            {
                myLogger.Log(ClassName, $"String invalid {firstEntry}");
                return
                    $"// {firstEntry} is not a valid identifier. If you need all possible entries, add to sorter tag [GUIDE]";
            }

            double itemRequestAmount = 0;
            double itemTriggerAmount = 0;

            switch (parts.Length)
            {
                case 2:
                    double.TryParse(parts[1].Trim(), out itemRequestAmount);
                    if (itemRequestAmount < 0) itemRequestAmount = 0;
                    break;
                case 3:
                    double.TryParse(parts[1].Trim(), out itemRequestAmount);
                    if (!double.TryParse(parts[2].Trim(), out itemTriggerAmount) ||
                        itemTriggerAmount <= itemRequestAmount)
                    {
                        itemTriggerAmount = itemRequestAmount + itemRequestAmount * 0.5;
                    }

                    if (itemRequestAmount < 0) itemRequestAmount = 0;

                    break;
            }

            // Create filter if request is above or below 0
            ItemLimit itemLimit = null;
            if (itemRequestAmount != 0)
            {
                itemLimit = new ItemLimit()
                {
                    ItemRequestedAmount = (MyFixedPoint)itemRequestAmount,
                    ItemTriggerAmount = (MyFixedPoint)itemTriggerAmount,
                    OverLimitTrigger = false
                };
            }

            SorterLimitManager limitManager;
            if (!SorterLimitManagers.TryGetValue(definitionId, out limitManager))
                return "This is bad, you better don't see this line.";

            if (itemRequestAmount == 0) return $"{firstEntry} | {itemRequestAmount} | {itemTriggerAmount}";
            limitManager.RegisterSorter(sorter, itemLimit);

            // Return the line back for custom data.
            return $"{firstEntry} | {itemRequestAmount} | {itemTriggerAmount}";
        }

        /// <summary>
        /// Processes the deletion of a line corresponding to a sorter limit. 
        /// It un registers the sorter from the SorterLimitManager associated with the given definition.
        /// </summary>
        /// <param name="defId">The MyDefinitionId of the item being removed.</param>
        /// <param name="sorter">The sorter from which the item is being removed.</param>
        private void ProcessDeletedLine(MyDefinitionId defId, IMyConveyorSorter sorter)
        {
            SorterLimitManager limitManager;
            if (!SorterLimitManagers.TryGetValue(defId, out limitManager))
            {
                myLogger.LogError(ClassName, "This is bad, you better don't see this line.");
                return;
            }

            limitManager.UnRegisterSorter(sorter);
        }

        /// <summary>
        /// Processes the modification of a line corresponding to a sorter limit. 
        /// It updates or un registers the sorter from the SorterLimitManager based on the parsed data.
        /// </summary>
        /// <param name="defId">The string identifier of the item being modified.</param>
        /// <param name="combinedValue">The value string representing request and trigger amounts.</param>
        /// <param name="sorter">The sorter being updated.</param>
        /// <returns>A formatted string representing the updated line.</returns>
        private string ProcessChangedLine(string defId, string combinedValue, IMyConveyorSorter sorter)
        {
            MyDefinitionId definitionId;
            if (!ItemNameToDefinitionMap.TryGetValue(defId, out definitionId))
            {
                return
                    $"// {defId} is not a valid identifier. If you need all possible entries, add to sorter tag [GUIDE]";
            }

            double itemRequestAmount = 0;
            double itemTriggerAmount = 0;

            var parts = combinedValue.Split(new[] { '|' }, StringSplitOptions.None);


            if (parts.Length > 0)
            {
                if (!double.TryParse(parts[0].Trim(), out itemRequestAmount) || itemRequestAmount < 0)
                {
                    itemRequestAmount = 0;
                }
            }

            if (parts.Length > 1)
            {
                if (!double.TryParse(parts[1].Trim(), out itemTriggerAmount) || itemTriggerAmount <= itemRequestAmount)
                {
                    // If parsing fails or trigger amount is less than or equal to request amount,
                    // default to a calculated value based on itemRequestAmount
                    itemTriggerAmount = itemRequestAmount + Math.Abs(itemRequestAmount) * 0.75;
                }
            }


            SorterLimitManager limitManager;
            if (!SorterLimitManagers.TryGetValue(definitionId, out limitManager))
            {
                return "This is bad, you better don't see this line.";
            }


            if (itemRequestAmount == 0)
            {
                myLogger.Log(ClassName, "Removing limit on sorter");
                limitManager.UnRegisterSorter(sorter);
            }
            else
            {
                myLogger.Log(ClassName, "Changing limit on sorter");
                limitManager.ChangeLimitsOnSorter(sorter, (MyFixedPoint)itemRequestAmount,
                    (MyFixedPoint)itemTriggerAmount);
            }

            myLogger.Log(ClassName, $"Changed line is: {defId} | {itemRequestAmount} | {itemTriggerAmount}");
            return $"{defId} | {itemRequestAmount} | {itemTriggerAmount}";
        }

        // TODO find why it double triggers. Guide is always found twice. Not really an issue. But still
        /// <summary>
        /// Event handler for when the terminal block's custom name is changed. 
        /// Detects if the guide call is present in the name and replaces it with the guide data.
        /// </summary>
        /// <param name="obj">The terminal block whose name was changed.</param>
        private void Terminal_CustomNameChanged(IMyTerminalBlock obj)
        {
            var name = obj.CustomName;
            if (name.IndexOf(GuideCall, StringComparison.OrdinalIgnoreCase) < 0) return;

            myLogger.Log(ClassName, $"Sorter guide detected, {name}");
            obj.CustomName = Regex.Replace(obj.CustomName, Regex.Escape(GuideCall), string.Empty,
                RegexOptions.IgnoreCase);
            obj.CustomData = Guide_Data;
            SorterDataStorageRef.AddOrUpdateSorterRawData((IMyConveyorSorter)obj);
        }

        /// <summary>
        /// Method called after every 100 simulation ticks. It checks and updates values for all sorters in the collection.
        /// </summary>
        public void OnAfterSimulation100()
        {
            foreach (var sorter in ModSorterCollection)
            {
                Try_Updating_Values(sorter);
            }
        }

        /// <summary>
        /// Event handler for when a sorter is closed. Unregisters events and removes the sorter from the collection.
        /// </summary>
        /// <param name="obj">The entity (sorter) that is being closed.</param>
        private void Sorter_OnClose(VRage.ModAPI.IMyEntity obj)
        {
            var terminal = (IMyTerminalBlock)obj;
            obj.OnClose -= Sorter_OnClose;
            terminal.CustomNameChanged -= Terminal_CustomNameChanged;
            var sorter = (IMyConveyorSorter)obj;
            ModSorterCollection.Remove(sorter);
        }

        /// <summary>
        /// Disposes the ModConveyorManager instance and unregisters all events to prevent memory leaks.
        /// </summary>
        public override void Dispose()
        {
            base.Dispose();
            InventoryGridManager.OnModSorterAdded -= Add_Sorter;
            foreach (var sorter in ModSorterCollection.ToList())
            {
                sorter.OnClose -= Sorter_OnClose;
                var terminal = (IMyTerminalBlock)sorter;
                terminal.CustomNameChanged -= Terminal_CustomNameChanged;
            }
        }
    }
}