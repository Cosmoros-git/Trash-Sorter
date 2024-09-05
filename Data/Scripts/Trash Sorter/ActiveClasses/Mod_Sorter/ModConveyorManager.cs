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
    internal class ModConveyorManager : ModBase
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
        private readonly Dictionary<string, MyDefinitionId> ItemNameToDefintionMap;

        /// <summary>
        /// Observable dictionary for tracking current values of MyDefinitionId items.
        /// </summary>
        private readonly ObservableDictionary<MyDefinitionId> ItemValuesReference;

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

        /// <summary>
        /// Initializes a new instance of the ModConveyorManager class and registers events.
        /// </summary>
        /// <param name="sorters">HashSet of sorters.</param>
        /// <param name="mainItemAccess">Main item storage reference.</param>
        /// <param name="inventoryGridManager">Inventory grid manager instance.</param>
        /// <param name="sorterLimitManagers">Dictionary of sorter limit managers.</param>
        /// <param name="nameToDefinition">Dictionary mapping names to item definitions.</param>
        /// <param name="itemsDictionary">Observable dictionary of item values.</param>
        public ModConveyorManager(HashSet<IMyConveyorSorter> sorters,
            MainItemStorage mainItemAccess, InventoryGridManager inventoryGridManager,
            Dictionary<MyDefinitionId, SorterLimitManager> sorterLimitManagers,
            Dictionary<string, MyDefinitionId> nameToDefinition,
            ObservableDictionary<MyDefinitionId> itemsDictionary)
        {
            watch.Start();
            ModSorterCollection = sorters;
            SorterLimitManagers = sorterLimitManagers;
            ItemValuesReference = itemsDictionary;
            SorterDataStorageRef = new SorterDataStorage(nameToDefinition);
            ItemNameToDefintionMap = mainItemAccess.NameToDefinitionMap;
            InventoryGridManager = inventoryGridManager;

            // Registering event for sorter addition
            inventoryGridManager.OnModSorterAdded += Add_Sorter;

            // Create entries and initialize sorters
            Create_All_Possible_Entries();
            SorterInit();
            watch.Stop();
            Logger.Instance.Log(ClassName, $"Initialization took {watch.Elapsed.Milliseconds}ms, amount of trash sorters {ModSorterCollection.Count}");
        }

        // Guide data is made here :)
        /// <summary>
        /// Creates all possible item entries for user reference.
        /// </summary>
        private void Create_All_Possible_Entries()
        {
            watch.Restart();
            var stringBuilder = new StringBuilder(ItemNameToDefintionMap.Count * 50);
            const string separator = " | ";
            string lastType = null;
            stringBuilder.AppendLine("<Trash filter OFF>");

            foreach (var name in ItemNameToDefintionMap)
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
            Logger.Instance.Log(ClassName, $"Creating all entries took {watch.Elapsed.Milliseconds}ms, amount of entries sorters {ItemNameToDefintionMap.Count}");
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
            watch.Restart();
            sorter.SetFilter(MyConveyorSorterMode.Whitelist, new List<MyInventoryItemFilter>());
            sorter.DrainAll = true;

            // Register events to avoid memory leaks
            sorter.OnClose += Sorter_OnClose;
            sorter.CustomNameChanged += Terminal_CustomNameChanged;

            SorterDataStorageRef.AddOrUpdateSorterRawData(sorter);
            Update_Values(sorter);
            watch.Stop();
            Logger.Instance.Log(ClassName, $"Adding sorter has taken {watch.ElapsedMilliseconds}ms");
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
            Logger.Instance.Log(ClassName, $"Starting values update");
            watch.Restart();

            List<MyDefinitionId> removedEntries;
            List<string> addedEntries;
            Dictionary<string, string> changedEntries;
            bool hasFilterTagBeenFound;

            var data = SorterDataStorageRef.TrackChanges(sorter, out removedEntries, out addedEntries, out changedEntries, out hasFilterTagBeenFound);

            if (hasFilterTagBeenFound)
            {
                foreach (var line in removedEntries)
                {
                    ProcessDeletedLine(line, sorter);
                }

                foreach (var line in addedEntries)
                {
                    var newLine = ProcessNewLine(line, sorter);
                    var index = data.IndexOf(line);
                    if (index != -1) data[index] = newLine;
                }

                var defIdList = new List<string>();
                if (changedEntries.Count > 0)
                {
                    foreach (var stringData in data)
                    {
                        var defId = stringData.Split(new[] { '|' }, StringSplitOptions.None)[0].Trim();
                        defIdList.Add(defId);
                    }
                }

                foreach (var sorterChangedData in changedEntries)
                {
                    var newLine = ProcessChangedLine(sorterChangedData.Key, sorterChangedData.Value, sorter);
                    var index = defIdList.FindIndex(defId => string.Equals(defId, sorterChangedData.Key.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (index != -1) data[index] = newLine;
                }
            }

            var stringBuilder = new StringBuilder();
            foreach (var t in data)
            {
                var line = t.Trim();
                if (!string.IsNullOrWhiteSpace(line)) stringBuilder.AppendLine(line);
            }

            var newCustomData = stringBuilder.ToString();
            sorter.CustomData = newCustomData;
            SorterDataStorageRef.AddOrUpdateSorterRawData(sorter);
            watch.Stop();
        }



        // Functions to process new/removed/edited lines. TODO MAYBE LOOK INTO OPTIMIZING THIS
        private string ProcessNewLine(string trimmedLine, IMyConveyorSorter sorter)
        {
            var parts = trimmedLine.Split(new[] { '|' }, StringSplitOptions.None);
            var firstEntry = parts[0].Trim();


            MyDefinitionId definitionId;
            if (!ItemNameToDefintionMap.TryGetValue(firstEntry, out definitionId))
            {
                return
                    $"// {firstEntry} is not a valid identifier. If you need all possible entries, add to sorter tag [GUIDE]";
            }

            double itemRequestAmount = 0;
            double itemTriggerAmount = 0;

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
                    if (!double.TryParse(parts[2].Trim(), out itemTriggerAmount) ||
                        itemTriggerAmount <= itemRequestAmount)
                    {
                        itemTriggerAmount = itemRequestAmount + itemRequestAmount * 0.5;
                    }

                    if (itemRequestAmount < 0) itemRequestAmount = -1;

                    break;
            }

            // Create filter if request is above or below 0;
            var itemLimit = new ItemLimit()
            {
                ItemRequestedAmount = (MyFixedPoint)itemRequestAmount,
                ItemTriggerAmount = (MyFixedPoint)itemTriggerAmount,
                OverLimitTrigger = false
            };
            SorterLimitManager limitManager;
            if (!SorterLimitManagers.TryGetValue(definitionId, out limitManager))
                return "This is bad, you better don't see this line.";

            if (itemRequestAmount == 0) return $"{firstEntry} | {itemRequestAmount} | {itemTriggerAmount}";

            MyFixedPoint itemAmountNow;
            if (ItemValuesReference.TryGetValue(definitionId, out itemAmountNow))
            {
                limitManager.RegisterSorter(sorter, itemLimit, itemAmountNow);
            }

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
                Logger.Instance.LogError(ClassName, "This is bad, you better don't see this line.");
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
            if (!ItemNameToDefintionMap.TryGetValue(defId, out definitionId))
            {
                return $"// {defId} is not a valid identifier. If you need all possible entries, add to sorter tag [GUIDE]";
            }

            double itemRequestAmount;
            double itemTriggerAmount;

            var parts = combinedValue.Split(new[] { '|' }, StringSplitOptions.None);

            double.TryParse(parts[0].Trim(), out itemRequestAmount);

            if (!double.TryParse(parts[1].Trim(), out itemTriggerAmount) || itemTriggerAmount <= itemRequestAmount)
            {
                itemTriggerAmount = itemRequestAmount + itemRequestAmount * 0.75;
            }

            if (itemRequestAmount < 0) itemRequestAmount = -1;

            SorterLimitManager limitManager;
            if (!SorterLimitManagers.TryGetValue(definitionId, out limitManager))
            {
                return "This is bad, you better don't see this line.";
            }

            if (itemRequestAmount == 0)
            {
                limitManager.UnRegisterSorter(sorter);
            }
            else
            {
                limitManager.ChangeLimitsOnSorter(sorter, (MyFixedPoint)itemRequestAmount, (MyFixedPoint)itemTriggerAmount);
            }

            Logger.Instance.Log(ClassName, $"Changed line is: {defId} | {itemRequestAmount} | {itemTriggerAmount}");
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

            Logger.Instance.Log(ClassName, $"Sorter guide detected, {name}");
            obj.CustomName = Regex.Replace(obj.CustomName, Regex.Escape(GuideCall), string.Empty, RegexOptions.IgnoreCase);
            obj.CustomData = Guide_Data;
            SorterDataStorageRef.AddOrUpdateSorterRawData((IMyConveyorSorter)obj);
        }

        /// <summary>
        /// Method called after every 100 simulation ticks. It checks and updates values for all sorters in the collection.
        /// </summary>
        public void OnAfterSimulation100()
        {
            watch.Restart();
            foreach (var sorter in ModSorterCollection)
            {
                Try_Updating_Values(sorter);
            }

            watch.Stop();
            DebugTimeClass.TimeOne = TimeSpan.Zero;
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