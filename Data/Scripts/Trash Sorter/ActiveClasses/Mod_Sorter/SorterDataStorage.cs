﻿using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.ModAPI;
using Trash_Sorter.Data.Scripts.Trash_Sorter.BaseClass;
using VRage.Game;
using VRage.Library.Collections;

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
        private readonly Stopwatch watch = new Stopwatch();

        private readonly Logger myLogger;

        /// <summary>
        /// Initializes a new instance of the SorterDataStorage class and sets up the reference dictionary.
        /// </summary>
        /// <param name="nameToDefinition">A dictionary mapping names to MyDefinitionId objects.</param>
        public SorterDataStorage(Dictionary<string, MyDefinitionId> nameToDefinition, Logger MyLogger)
        {
            ReferenceIdDictionary = nameToDefinition;
            myLogger = MyLogger;
        }

        /// <summary>
        /// Adds a new sorter to the storage or updates its raw custom data if already present.
        /// The operation is timed and logged.
        /// </summary>
        /// <param name="sorter">The conveyor sorter object to add or update.</param>
        public void AddOrUpdateSorterRawData(IMyConveyorSorter sorter)
        {
            watch.Restart();
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

            watch.Stop();
            myLogger.Log(ClassName, $"Adding or updating storage custom data taken {watch.ElapsedMilliseconds}ms");
        }

        /// <summary>
        /// Tracks changes between the current and previous sorter data. Identifies added, removed, 
        /// and modified entries, and updates the stored data accordingly.
        /// </summary>
        /// <param name="sorter">The conveyor sorter being tracked.</param>
        /// <param name="removedEntries">List of removed entries.</param>
        /// <param name="addedEntries">List of added entries.</param>
        /// <param name="changedEntries">Dictionary of changed entries.</param>
        /// <param name="dataFound">Returns true if data is found.</param>
        /// <returns>A list of strings representing the new custom data lines.</returns>
        public List<string> TrackChanges(IMyConveyorSorter sorter, out List<MyDefinitionId> removedEntries,
            out List<string> addedEntries, out Dictionary<string, string> changedEntries, out bool dataFound)
        {
            watch.Restart();
            removedEntries = new List<MyDefinitionId>();
            addedEntries = new List<string>();
            changedEntries = new Dictionary<string, string>();
            dataFound = false;

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
                dataFound = true;
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
                if (line.StartsWith("//")) continue;  // Skip commented lines
                var parts = line.Split('|').Select(part => part.Trim()).ToArray();
                string keyValue = "";
                // If there is no separator '|', treat the entire line as the key
                keyValue = parts.Length <= 1 ? line : parts[0];
                var value = parts.Length > 1 ? string.Join(" | ", parts.Skip(1)) : "0|0";

                newDataDictionary[keyValue] = value;
            }


            dataFound = true;
            foreach (var entry in oldDataDictionary.Keys)
            {
                if (newDataDictionary.ContainsKey(entry)) continue;

                MyDefinitionId defId;
                if (ReferenceIdDictionary.TryGetValue(entry, out defId))
                {
                    removedEntries.Add(defId);
                }
            }

            foreach (var entry in newDataDictionary.Keys)
            {
                if (!oldDataDictionary.ContainsKey(entry))
                {
                    addedEntries.Add(entry + " | " + newDataDictionary[entry]);
                }
            }

            foreach (var entry in oldDataDictionary.Keys)
            {
                if (newDataDictionary.ContainsKey(entry) && oldDataDictionary[entry] != newDataDictionary[entry])
                {
                    changedEntries.Add(entry, newDataDictionary[entry]);
                }
            }

            customDataAccess.ProcessedCustomData = newDataDictionary;
            watch.Stop();
            myLogger.Log(ClassName, $"Tracking changes in custom data taken {watch.ElapsedMilliseconds}");
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
                myLogger.Log(ClassName, $"Storage value not found");
                return true;
            }

            var checksum = SorterCustomData.ComputeSimpleChecksum(sorter.CustomData);
            if (checksum == value.CheckSum) return false;

            myLogger.Log(ClassName, $"Checksum of string {checksum} is not equal stored {value.CheckSum}");
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