using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Sandbox.ModAPI;
using Trash_Sorter.Data.Scripts.Trash_Sorter.BaseClass;
using Trash_Sorter.Data.Scripts.Trash_Sorter.SessionComponent;
using VRage.Game;

namespace Trash_Sorter.Data.Scripts.Trash_Sorter.ActiveClasses.Mod_Sorter
{
    /// <summary>
    /// The SorterCustomData class is used to manage and process custom data for a sorter.
    /// It stores both the raw custom data string and its processed form as a dictionary of key-value pairs.
    /// The class also maintains a checksum of the raw custom data for comparison purposes.
    /// </summary>
    internal class SorterCustomData
    {
        /// <summary>
        /// Stores processed custom data as a dictionary of key-value pairs.
        /// The key is a string representing an identifier, and the value is the associated string data.
        /// </summary>
        public Dictionary<string, string> ProcessedCustomData;

        /// <summary>
        /// Private backing field for RawCustomData, which stores the raw string of custom data.
        /// </summary>
        private string _rawCustomData = "";

        /// <summary>
        /// Gets or sets the raw custom data string. When set, it automatically recalculates the checksum
        /// using the ComputeSimpleChecksum method.
        /// </summary>
        public string RawCustomData
        {
            get { return _rawCustomData; }
            set
            {
                _rawCustomData = value;
                CheckSum = ComputeSimpleChecksum(value); // Recalculate checksum when data changes
            }
        }

        /// <summary>
        /// Stores the checksum of the raw custom data. Used to detect changes or compare data integrity.
        /// </summary>
        public int CheckSum;

        /// <summary>
        /// Initializes a new instance of the SorterCustomData class, with an empty raw custom data string
        /// and an initialized checksum.
        /// </summary>
        public SorterCustomData()
        {
            ProcessedCustomData = new Dictionary<string, string>();
            RawCustomData = "";
            CheckSum = ComputeSimpleChecksum(RawCustomData);
        }

        /// <summary>
        /// Computes a simple checksum for a given string. The checksum is calculated by iterating through
        /// each character in the string and using a hash-like algorithm that multiplies by 31 and adds the
        /// character's value.
        /// </summary>
        /// <param name="data">The string for which the checksum is calculated.</param>
        /// <returns>An integer representing the checksum of the input string.</returns>
        public static int ComputeSimpleChecksum(string data)
        {
            unchecked // Allow overflow, it wraps around
            {
                var checksum = 0;
                foreach (var c in data)
                {
                    checksum = (checksum * 31) + c;
                }

                return checksum;
            }
        }
    }

    /// <summary>
    /// The SorterDataStorage class handles storing and managing custom data for conveyor sorters.
    /// It maintains a dictionary of sorters and their associated custom data, tracks changes,
    /// and verifies if the custom data has been modified since it was last stored.
    /// </summary>
    internal class SorterDataStorage : ModBase
    {
        /// <summary>
        /// Dictionary storing the sorter and its associated custom data (processed and raw).
        /// </summary>
        private readonly Dictionary<IMyConveyorSorter, SorterCustomData> sorterDataDictionary =
            new Dictionary<IMyConveyorSorter, SorterCustomData>();

        /// <summary>
        /// Dictionary to reference MyDefinitionId objects by string keys (used for custom data parsing).
        /// </summary>
        private readonly Dictionary<string, MyDefinitionId> ReferenceIdDictionary;

        /// <summary>
        /// Stopwatch for tracking execution time of operations.
        /// </summary>

        public readonly List<MyDefinitionId> RemovedEntries;
        public readonly List<string> AddedEntries;
        public readonly Dictionary<string, string> ChangedEntries;

        /// <summary>
        /// Initializes a new instance of the SorterDataStorage class and sets up the reference dictionary.
        /// </summary>
        /// <param name="nameToDefinition">A dictionary mapping names to MyDefinitionId objects.</param>
        public SorterDataStorage(Dictionary<string, MyDefinitionId> nameToDefinition)
        {
            ReferenceIdDictionary = nameToDefinition;
            ChangedEntries = new Dictionary<string, string>();
            AddedEntries = new List<string>();
            RemovedEntries = new List<MyDefinitionId>();
        }

        /// <summary>
        /// Adds a new sorter to the storage or updates its raw custom data if already present.
        /// The operation is timed and logged.
        /// </summary>
        /// <param name="sorter">The conveyor sorter object to add or update.</param>
        public void AddOrUpdateSorterRawData(IMyConveyorSorter sorter)
        {
            var wat1 = Stopwatch.StartNew();

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
                // Update the RawCustomData of the existing entry
                value.RawCustomData = customData;
            }

            wat1.Stop();
            Logger.Log(ClassName, $"Adding or updating storage custom data taken {wat1.Elapsed.TotalMilliseconds}ms");
        }

        /// <summary>
        /// Tracks changes between the current and previous sorter data. Identifies added, removed, 
        /// and modified entries, and updates the stored data accordingly.
        /// </summary>
        /// <param name="sorter">The conveyor sorter being tracked.</param>
        /// <param name="RemovedEntries">List of removed entries.</param>
        /// <param name="addedEntries">List of added entries.</param>
        /// <param name="changedEntries">Dictionary of changed entries.</param>
        /// <param name="dataFound">Returns true if data is found.</param>
        /// <returns>A list of strings representing the new custom data lines.</returns>
        public List<string> TrackChanges(IMyConveyorSorter sorter, out bool dataFound)
        {
            var wat1 = Stopwatch.StartNew();
            dataFound = false;

            RemovedEntries.Clear();
            AddedEntries.Clear();
            ChangedEntries.Clear();

            var newCustomData = sorter.CustomData;
            var newCustomDataList = !string.IsNullOrEmpty(newCustomData)
                ? new List<string>(newCustomData.Split(new[] { '\r', '\n' }, StringSplitOptions.None))
                : new List<string>();


            var newNonEmptyEntries = newCustomDataList
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            var startIndex = newNonEmptyEntries.FindIndex(line => line.Contains("<Trash filter ON>"));
            if (startIndex == -1)
            {
                dataFound = false;
                return newCustomDataList;
            }

            startIndex += 1;

            SorterCustomData customDataAccess;
            if (!sorterDataDictionary.TryGetValue(sorter, out customDataAccess))
            {
                dataFound = true;
                return newCustomDataList;
            }

            var oldDataDictionary = customDataAccess.ProcessedCustomData;
            var newDataSet = newCustomDataList.Skip(startIndex).ToList();
            var newDataDictionary = new Dictionary<string, string>();

            foreach (var line in newDataSet)
            {
                // Skip empty or whitespace-only lines
                if (string.IsNullOrWhiteSpace(line)) continue;

                // Skip commented lines
                if (line.StartsWith("//")) continue;

                var parts = line.Split('|').Select(part => part.Trim()).ToArray();
                var keyValue = parts.Length == 0 ? line : parts[0]; // Treat the entire line as key if no separator
                var value = parts.Length > 1
                    ? string.Join(" | ", parts.Skip(1))
                    : "0|0"; // Default value if no separator

                // Normalize both key and value
                keyValue = keyValue.Trim();
                value = value.Trim();

                newDataDictionary[keyValue] = value;
            }

            dataFound = true;

            // Track removed entries
            foreach (var entry in oldDataDictionary.Keys)
            {
                if (!newDataDictionary.ContainsKey(entry))
                {
                    MyDefinitionId defId;
                    if (ReferenceIdDictionary.TryGetValue(entry, out defId))
                    {
                        RemovedEntries.Add(defId);
                    }
                }
            }

            // Track added entries
            foreach (var entry in newDataDictionary.Keys)
            {
                if (!oldDataDictionary.ContainsKey(entry))
                {
                    AddedEntries.Add(entry + " | " + newDataDictionary[entry]);
                }
            }

            // Track changed entries
            foreach (var entry in oldDataDictionary.Keys)
            {
                string value;
                if (!newDataDictionary.TryGetValue(entry, out value)) continue;

                var oldValue = oldDataDictionary[entry];
                var newValue = value.Trim();

                // Compare normalized values to detect changes
                if (!string.Equals(oldValue, newValue, StringComparison.OrdinalIgnoreCase))
                {
                    ChangedEntries.Add(entry, newValue);
                }
            }

            // Update the processed custom data with the new entries
            customDataAccess.ProcessedCustomData = newDataDictionary;

            wat1.Stop();
            Logger.Log(ClassName, $"Tracking changes in custom data took {wat1.Elapsed.TotalMilliseconds}ms");

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

            var checksum = SorterCustomData.ComputeSimpleChecksum(sorter.CustomData);
            if (checksum == value.CheckSum) return false;

            Logger.Log(ClassName, $"Checksum of string {checksum} is not equal stored {value.CheckSum}");
            return true;
        }

        /// <summary>
        /// Determines if the sorter has no custom data.
        /// </summary>
        /// <param name="sorter">The conveyor sorter being checked.</param>
        /// <returns>True if the sorter's custom data is empty or null; otherwise, false.</returns>
        public bool IsEmpty(IMyConveyorSorter sorter)
        {
            SorterCustomData rawData;
            return !sorterDataDictionary.TryGetValue(sorter, out rawData) ||
                   string.IsNullOrWhiteSpace(rawData?.RawCustomData);
        }
    }
}