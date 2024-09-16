using System;
using System.Collections.Generic;
using Trash_Sorter.StaticComponents;
using VRage;
using VRage.Utils;

namespace Trash_Sorter.StorageClasses
{
    public class ObservableDictionary<TKey> : Dictionary<TKey, FixedPointReference>
    {
        // A predefined set of allowed keys (filter list)
        private readonly HashSet<TKey> _filterList;

        // Event triggered when a value for a key is changed
        public event Action<TKey> OnValueChanged;

        // Constructor that takes a predefined filter list
        public ObservableDictionary(HashSet<TKey> filterListReference)
        {
            _filterList = filterListReference; // Store the filter list as a HashSet for quick lookups
        }

        // Override the indexer to control when new entries are allowed to be added
        public new FixedPointReference this[TKey key]
        {
            get
            {
                // Since this dictionary only supports predefined keys, ensure the key exists
                if (ContainsKey(key)) return base[key];

                Logger.LogError("ObservableDictionary",
                    $"ObservableDictionary: The given key '{key}' was not present in the dictionary.");
                return null; // Or throw an exception if needed, depending on how you want to handle this
            }
            set
            {
                // Only allow new entries if the key is in the filter list
                if (!ContainsKey(key))
                {
                    if (_filterList.Contains(key))
                    {
                        // Add the key with the provided value if it's allowed by the filter list
                        Add(key, value);
                        OnValueChanged?.Invoke(key);
                    }
                    else
                    {
                        // If the key is not in the filter list, ignore it or log a message
                        Logger.LogError("ObservableDictionary",
                            $"ObservableDictionary: The key '{key}' is not allowed to be added.");
                    }

                    return; // Exit to avoid double-setting below
                }

                // Only update if the value is different, to avoid unnecessary events
                if (EqualityComparer<FixedPointReference>.Default.Equals(base[key], value)) return;

                base[key] = value;
                OnValueChanged?.Invoke(key);
            }
        }

        // Method to update the value of an existing key
        public void UpdateValue(TKey key, MyFixedPoint amount)
        {
            if (!ContainsKey(key))
            {
                // Only add the key if it's allowed by the filter list
                if (_filterList.Contains(key))
                {
                    Add(key, new FixedPointReference(amount));
                    OnValueChanged?.Invoke(key);
                }
                else
                {
                    MyLog.Default.WriteLine($"ObservableDictionary: The key '{key}' is not allowed to be updated.");
                }

                return;
            }

            base[key].ItemAmount += amount;
            OnValueChanged?.Invoke(key);
        }
    }
}