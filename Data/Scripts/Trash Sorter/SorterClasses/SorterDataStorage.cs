using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.ModAPI;
using Trash_Sorter.BaseClass;
using Trash_Sorter.StaticComponents;
using VRage.Game;

namespace Trash_Sorter.SorterClasses
{
    /// <summary>
    /// The SorterCustomData class is used to manage and process custom data for a sorter.
    /// It stores both the raw custom data string and its processed form as a dictionary of key-value pairs.
    /// The class also maintains a checksum of the raw custom data for comparison purposes.
    /// </summary>
    internal class SorterCustomData
    {
        public Dictionary<string, string> ProcessedCustomData;
        public string RawCustomData { get; set; } = "";
        public SorterCustomData()
        {
            ProcessedCustomData = new Dictionary<string, string>();
            RawCustomData = "";
        }
    }

    /// <summary>
    /// The SorterDataStorage class handles storing and managing custom data for conveyor sorters.
    /// It maintains a dictionary of sorters and their associated custom data, tracks changes,
    /// and verifies if the custom data has been modified since it was last stored.
    /// </summary>
    internal class SorterDataStorage : ModBase
    {
        private readonly Dictionary<IMyConveyorSorter, SorterCustomData> sorterDataDictionary =
            new Dictionary<IMyConveyorSorter, SorterCustomData>();

        private readonly Dictionary<string, MyDefinitionId> NameToDefinitionMap = ModSessionComponent.NameToDefinitionMap;
        public readonly List<MyDefinitionId> RemovedEntries = new List<MyDefinitionId>();

        public readonly List<string> AddedEntries = new List<string>();
        public readonly Dictionary<string, string> ChangedEntries = new Dictionary<string, string>();
        private Dictionary<string, string> OldEntries = new Dictionary<string, string>();


        public void AddOrUpdateSorterRawData(IMyConveyorSorter sorter)
        {
            var customData = sorter.CustomData;

            SorterCustomData value;

            // Check if the sorter already exists in the dictionary
            if (!sorterDataDictionary.TryGetValue(sorter, out value))
            {
                // If not, create a new SorterCustomData entry
                value = new SorterCustomData
                {
                    RawCustomData = customData,
                    ProcessedCustomData = new Dictionary<string, string>() // Initialize processed data
                };

                sorterDataDictionary.Add(sorter, value);
            }
            else
            {
                value.RawCustomData = customData;
            }

        }

        /// <summary>
        /// This tracks changes in the lines while respecting white lines and spacing somewhat.
        /// </summary>
        /// <param name="sorter"></param>
        /// <param name="dataFound"></param>
        /// <returns></returns>
        public List<string> TrackChanges(IMyConveyorSorter sorter, out bool dataFound)
        {
            dataFound = false;

            // Clear existing tracking lists
            RemovedEntries.Clear();
            AddedEntries.Clear();
            ChangedEntries.Clear();

            // Get the new custom data and split by lines (handling both Windows and Unix new lines)
            var newCustomData = sorter.CustomData;
            var newCustomDataList = string.IsNullOrEmpty(newCustomData)
                ? new List<string>()
                : new List<string>(newCustomData.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None));

            // Find the starting index for processing data (after <Trash filter ON>)
            var startIndex = newCustomDataList.FindIndex(line => line.Contains("<Trash filter ON>"));
            if (startIndex == -1)
            {
                Logger.Log(ClassName, "TrackChanges: '<Trash filter ON>' not found in the custom data.");
                return newCustomDataList;
            }
            startIndex++; // Move past the tag line

            // Get or initialize the sorter data
            SorterCustomData customDataAccess;
            if (!sorterDataDictionary.TryGetValue(sorter, out customDataAccess))
            {
                customDataAccess = new SorterCustomData();
                sorterDataDictionary[sorter] = customDataAccess;
            }

            // Get the old entries and new data after the start index
            var oldEntries = customDataAccess.ProcessedCustomData;
            var newDataSet = newCustomDataList.Skip(startIndex).ToList();
            var newDataDictionary = new Dictionary<string, string>();

            // Process the new data lines
            foreach (var line in newDataSet)
            {
                if (line.StartsWith("//")) continue; // Skip comments

                var parts = line.Split('|').Select(part => part.Trim()).ToArray();
                var keyValue = parts.Length == 0 ? line : parts[0]; // Use entire line as key if no separator
                var value = parts.Length > 1 ? string.Join(" | ", parts.Skip(1)) : "0|0"; // Default value if missing

                newDataDictionary[keyValue.Trim()] = value.Trim();
            }

            // Mark data as found since <Trash filter ON> exists
            dataFound = true;

            // Track removed, added, and changed entries in one pass
            foreach (var entry in oldEntries.Keys)
            {
                string newValue;
                if (!newDataDictionary.TryGetValue(entry, out newValue))
                {
                    // Track removed entries
                    MyDefinitionId defId;
                    if (NameToDefinitionMap.TryGetValue(entry, out defId))
                    {
                        RemovedEntries.Add(defId);
                    }
                }
                else
                {
                    // Track changed entries
                    var oldValue = oldEntries[entry];
                    if (!string.Equals(oldValue, newValue, StringComparison.OrdinalIgnoreCase))
                    {
                        ChangedEntries[entry] = newValue;
                    }
                }
            }

            // Track added entries
            foreach (var entry in newDataDictionary.Keys)
            {
                if (!oldEntries.ContainsKey(entry))
                {
                    AddedEntries.Add($"{entry} | {newDataDictionary[entry]}");
                }
            }

            // Update the custom data
            customDataAccess.ProcessedCustomData = newDataDictionary;

            return newCustomDataList;
        }



        /// <summary>
        /// Checks if the custom data of the specified sorter has changed by comparing its checksum
        /// with the stored checksum.
        /// </summary>
        /// <param name="sorter">The conveyor sorter being checked.</param>
        /// <returns>True if the custom data has changed; otherwise, false.</returns>
        public bool HasCustomDataChanged(IMyConveyorSorter sorter)
        {
            SorterCustomData value;
            if (!sorterDataDictionary.TryGetValue(sorter, out value))
            {
                Logger.Log(ClassName, $"Storage value not found");
                return true;
            }

            if (ReferenceEquals(sorter.CustomData, value.RawCustomData))return false;

            Logger.Log(ClassName, $"Object reference is not equal to stored.");
            return true;
        }

        public bool IsEmpty(IMyConveyorSorter sorter)
        {
            SorterCustomData rawData;
            return !sorterDataDictionary.TryGetValue(sorter, out rawData) ||
                   string.IsNullOrWhiteSpace(rawData?.RawCustomData);
        }
    }
}