using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Library.Collections;

namespace Trash_Sorter.Data.Scripts.Trash_Sorter.ActiveClasses.Mod_Sorter
{
    internal class SorterCustomData
    {
        public Dictionary<string, string> ProcessedCustomData;
        public string RawCustomData;
        public int CheckSum;

        public SorterCustomData()
        {
            ProcessedCustomData = new Dictionary<string, string>();
            RawCustomData = "";
            CheckSum = ComputeSimpleChecksum(RawCustomData);
        }

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

    internal class SorterDataStorage
    {
        private readonly Dictionary<IMyConveyorSorter, SorterCustomData> sorterDataDictionary =
            new Dictionary<IMyConveyorSorter, SorterCustomData>();

        private readonly Dictionary<string, MyDefinitionId> ReferenceIdDictionary;

        public SorterDataStorage(Dictionary<string, MyDefinitionId> nameToDefinition)
        {
            ReferenceIdDictionary = nameToDefinition;
        }


        public void AddOrUpdateSorterData(IMyConveyorSorter sorter)
        {
            var customData = sorter.CustomData;
            if (string.IsNullOrEmpty(customData)) return;
            var value = new SorterCustomData();

            var customDataLines = customData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            // Find the index of the line that contains "<Trash sorter filter>"
            var startIndex = Array.FindIndex(customDataLines, line => line.Contains("<Trash filter>"));

            if (startIndex >= 0 && startIndex < customDataLines.Length - 1)
            {
                // Skip the tag itself and take the lines after it
                var relevantLines = customDataLines.Skip(startIndex + 1).ToList();
                value.RawCustomData = customData;
            }
            else
            {
                value.RawCustomData = customData;
                value.ProcessedCustomData = new Dictionary<string, string>();
            }

            sorterDataDictionary[sorter] = value;
        }

        public List<string> TrackChanges(IMyConveyorSorter sorter, out List<MyDefinitionId> removedEntries,
            out List<string> addedEntries, out Dictionary<string, string> changedEntries)
        {
            removedEntries = new List<MyDefinitionId>();
            addedEntries = new List<string>();
            changedEntries = new Dictionary<string, string>();

            var newCustomData = sorter.CustomData;

            // Split custom data into lines
            var newCustomDataList = !string.IsNullOrEmpty(newCustomData)
                ? new List<string>(newCustomData.Split(new[] { '\r', '\n' }, StringSplitOptions.None))
                : new List<string>();

            var newNonEmptyEntries = newCustomDataList
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();

            SorterCustomData customDataAccess;
            if (!sorterDataDictionary.TryGetValue(sorter, out customDataAccess))
            {
                // If no previous data exists, treat all lines as new
                return newCustomDataList;
            }

            var startIndex = newNonEmptyEntries.FindIndex(line => line.Contains("<Trash filter>"));
            if (startIndex == -1)
            {
                startIndex = 0; // Start comparing from the first line
            }
            else
            {
                startIndex += 1; // Skip the tag line itself
            }

            // Old data set
            var oldDataDictionary = customDataAccess.ProcessedCustomData;

            // Skip lines up to the tag for new data set
            var newDataSet = newCustomDataList.Skip(startIndex).ToList();

            // Dictionaries to hold split and trimmed data
            var newDataDictionary = new Dictionary<string, string>();

            // Fill the newDataDictionary
            foreach (var line in newDataSet)
            {
                if (line.StartsWith("//")) continue;
                var value = "";
                var parts = line.Split('|').Select(part => part.Trim()).ToArray(); // Split and trim each part

                if (parts.Length == 1)
                {
                    MyDefinitionId defId;
                    if (!ReferenceIdDictionary.TryGetValue(parts[0], out defId)) continue;
                    value = " 0|0";
                }
                else
                {
                    value = string.Join(" | ", parts.Skip(1)); // The rest is the value
                }

                var key = parts[0]; // First part is the key (e.g., MyDefinitionID)

                newDataDictionary[key] = value;
            }

            // Identify removed entries (in old but not in new)
            foreach (var entry in oldDataDictionary.Keys)
            {
                if (!newDataDictionary.ContainsKey(entry))
                {
                    MyDefinitionId defId;
                    if (!ReferenceIdDictionary.TryGetValue(entry, out defId)) continue;
                    removedEntries.Add(defId);
                }
            }

            // Identify added entries (in new but not in old)
            foreach (var entry in newDataDictionary.Keys)
            {
                if (!oldDataDictionary.ContainsKey(entry))
                {
                    addedEntries.Add(entry+" | "+newDataDictionary[entry]);
                }
            }

            // Identify changed entries (exists in both but values are different)
            foreach (var entry in oldDataDictionary.Keys)
            {
                if (newDataDictionary.ContainsKey(entry) && oldDataDictionary[entry] != newDataDictionary[entry])
                {
                    changedEntries.Add(entry, newDataDictionary[entry]);
                }
            }

            // Update the stored data and checksum to the new version
            customDataAccess.ProcessedCustomData = newDataDictionary; 

            return newCustomDataList;
        }


        public bool HasCustomDataChanged(IMyConveyorSorter sorter)
        {
            SorterCustomData value;
            if (!sorterDataDictionary.TryGetValue(sorter, out value)) return false;
            return SorterCustomData.ComputeSimpleChecksum(sorter.CustomData) == value.CheckSum;
        }

        public bool IsEmpty(IMyConveyorSorter sorter)
        {
            SorterCustomData rawData;
            return !sorterDataDictionary.TryGetValue(sorter, out rawData) ||
                   string.IsNullOrWhiteSpace(rawData?.RawCustomData);
        }
    }
}