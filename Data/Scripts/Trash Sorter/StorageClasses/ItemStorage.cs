using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Trash_Sorter.BaseClass;
using Trash_Sorter.StaticComponents;
using VRage;
using VRage.Game;

namespace Trash_Sorter.StorageClasses
{
    public class FixedPointReference
    {
        // This was made because fuck structs.
        public MyFixedPoint ItemAmount;

        public FixedPointReference(MyFixedPoint itemAmount)
        {
            ItemAmount = itemAmount;
        }
    }

    public class ItemStorage : ModBase
    {
        //  public HashSet<string> ProcessedItemsNames;
        public readonly ObservableDictionary<MyDefinitionId> ItemsDictionary;

        // This is main storage of my values, id references from strings and hash set of ids I do care about.
        public ItemStorage()
        {
            ItemsDictionary = new ObservableDictionary<MyDefinitionId>(ModSessionComponent.ProcessedItemsDefinitions);
            Logger.Log(ClassName,$"Item storage created, amount of items in dictionary {ItemsDictionary.Count}, amount of allowed definitions {ModSessionComponent.ProcessedItemsDefinitions.Count}");
        }
    }
}