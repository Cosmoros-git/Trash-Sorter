using System;
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

    internal class SorterChangedData
    {
        public string DefId;
        public string CombinedValues;
        public string OldLine;

        public SorterChangedData(string defId, string combinedValues, string oldLine)
        {
            DefId = defId;
            CombinedValues = combinedValues;
            OldLine = oldLine;
        }
    }

    internal class SorterCustomData
    {
        public Dictionary<string, string> ProcessedCustomData;
        private string _rawCustomData = "";

        public string RawCustomData
        {
            get { return _rawCustomData; }
            set
            {
                _rawCustomData = value;
                CheckSum = ComputeSimpleChecksum(value);
            }
        }

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

    internal class SorterDataStorage : ModBase
    {
        private readonly Dictionary<IMyConveyorSorter, SorterCustomData> sorterDataDictionary =
            new Dictionary<IMyConveyorSorter, SorterCustomData>();

        private readonly Dictionary<string, MyDefinitionId> ReferenceIdDictionary;
        private readonly Stopwatch watch = new Stopwatch();

        public SorterDataStorage(Dictionary<string, MyDefinitionId> nameToDefinition)
        {
            ReferenceIdDictionary = nameToDefinition;
        }


        public void AddOrUpdateSorterRawData(IMyConveyorSorter sorter)
        {
            watch.Restart();
            var customData = sorter.CustomData;

            SorterCustomData value;

            // Check if the sorter already has a stored entry
            if (!sorterDataDictionary.TryGetValue(sorter, out value))
            {
                // If no existing entry, create a new one and initialize it
                value = new SorterCustomData
                {
                    RawCustomData = customData,
                    ProcessedCustomData = new Dictionary<string, string>() // Always initialize this
                };

                // Add to the dictionary
                sorterDataDictionary.Add(sorter, value);
            }
            else
            {
                // If an entry exists, update the RawCustomData field
                value.RawCustomData = customData;

                // Optionally, you can reprocess custom data if needed
                // value.ProcessedCustomData.Clear();
                // (rebuild ProcessedCustomData if necessary)
            }

            watch.Stop();
            Logger.Instance.Log(ClassName,
                $"Adding or updating storage custom data taken {watch.ElapsedMilliseconds}ms");
        }


        public List<string> TrackChanges(IMyConveyorSorter sorter, out List<MyDefinitionId> removedEntries,
            out List<string> addedEntries, out Dictionary<string, string> changedEntries, out bool dataFound)
        {
            watch.Restart();
            removedEntries = new List<MyDefinitionId>();
            addedEntries = new List<string>();
            changedEntries = new Dictionary<string,string>();
            dataFound = false;

            var newCustomData = sorter.CustomData;

            // Split custom data into lines
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

            startIndex += 1; // Skip the tag line itself

            SorterCustomData customDataAccess;
            if (!sorterDataDictionary.TryGetValue(sorter, out customDataAccess))
            {
                // If no previous data exists, treat all lines as new
                dataFound = true;
                return newCustomDataList;
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
            dataFound = true;
            // Identify removed entries (in old but not in new)
            foreach (var entry in oldDataDictionary.Keys)
            {
                if (newDataDictionary.ContainsKey(entry)) continue;

                MyDefinitionId defId;
                if (!ReferenceIdDictionary.TryGetValue(entry, out defId)) continue;
                removedEntries.Add(defId);
            }

            // Identify added entries (in new but not in old)
            foreach (var entry in newDataDictionary.Keys)
            {
                if (!oldDataDictionary.ContainsKey(entry))
                {
                    addedEntries.Add(entry + " | " + newDataDictionary[entry]);
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
            watch.Stop();
            Logger.Instance.Log(ClassName, $"Tracking changes in custom data taken {watch.ElapsedMilliseconds}");
            return newCustomDataList;
        }


        public bool HasCustomDataChanged(IMyConveyorSorter sorter)
        {
            SorterCustomData value;
            if (!sorterDataDictionary.TryGetValue(sorter, out value))
            {
                Logger.Instance.Log(ClassName, $"Storage value not found");
                return true;
            }

            var checksum = SorterCustomData.ComputeSimpleChecksum(sorter.CustomData);
            if (checksum == value.CheckSum) return false;

            Logger.Instance.Log(ClassName, $"Checksum of string {checksum} is not equal stored {value.CheckSum}");
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