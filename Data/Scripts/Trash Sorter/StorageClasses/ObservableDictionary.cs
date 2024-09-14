using System;
using System.Collections.Generic;
using VRage;
using VRage.Utils;

namespace Trash_Sorter.StorageClasses
{
    public class ObservableDictionary<TKey> : Dictionary<TKey, FixedPointReference>, IEqualityComparer<string>
    {
        public bool Equals(string x, string y)
        {
            if (x == null || y == null)
                return false;

            return string.Equals(
                x.Replace(" ", "").Trim(),
                y.Replace(" ", "").Trim(),
                StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode(string obj)
        {
            return obj.Replace(" ", "").Trim().ToLowerInvariant().GetHashCode();
        }

        public ObservableDictionary() { } // The fact I need this to just create empty one is kinda kek.

        public ObservableDictionary(Dictionary<TKey, FixedPointReference> toDictionary)
        {
            foreach (var kvp in toDictionary)
            {
                Add(kvp.Key, kvp.Value); // Add each key-value pair to the new ObservableDictionary
            }
        }

        public event Action<TKey> OnValueChanged;

        public new FixedPointReference this[TKey key]
        {
            get
            {
                if (!ContainsKey(key))
                {
                    MyLog.Default.WriteLine(
                        $"Observable Dictionary The given key '{key}' was not present in the dictionary.");
                }

                return base[key];
            }
            set
            {
                // If key is not here already it means it was never needed. Might have broken stuff because of this... 
                if (!ContainsKey(key))
                {
                    Add(key, value);
                    OnValueChanged?.Invoke(key);
                    return; // Exit after adding to avoid double-setting below
                }

                if (EqualityComparer<FixedPointReference>.Default.Equals(base[key], value)) return;

                base[key] = value;
                OnValueChanged?.Invoke(key);
            }
        }

        public void UpdateValue(TKey key, MyFixedPoint updateToValue)
        {
            FixedPointReference currentValueRef;
            if (!TryGetValue(key, out currentValueRef))
            {
                Add(key, new FixedPointReference(updateToValue));
                return;
            }

            currentValueRef.ItemAmount += updateToValue;
            OnValueChanged?.Invoke(key);
        }
    }

}
