using System.Collections.Generic;
using Trash_Sorter.Data.Scripts.Trash_Sorter.BaseClass;
using Trash_Sorter.Data.Scripts.Trash_Sorter.SessionComponent;
using VRage;
using VRage.Game;

namespace Trash_Sorter.Data.Scripts.Trash_Sorter.StorageClasses
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

    public class MainItemStorage : ModBase
    {
        // Commented out dictionaries were just never used.
        public Dictionary<string, MyDefinitionId> NameToDefinitionMap;

        //  public Dictionary<MyDefinitionId, string> DefinitionToName;
        public HashSet<MyDefinitionId> ProcessedItems;

        //  public HashSet<string> ProcessedItemsNames;
        public ObservableDictionary<MyDefinitionId> ItemsDictionary;

        // This is main storage of my values, id references from strings and hash set of ids I do care about.
        public MainItemStorage()
        {
            NameToDefinitionMap = new Dictionary<string, MyDefinitionId>(ModSessionComponent.NameToDefinitionMap);
            ItemsDictionary = new ObservableDictionary<MyDefinitionId>(new Dictionary<MyDefinitionId, FixedPointReference>(ModSessionComponent.ItemStorageReference));
            Logger.Log(ClassName,$"Amount of items in dictionary {ItemsDictionary.Count}");
            ProcessedItems = new HashSet<MyDefinitionId>(ModSessionComponent.ProcessedItemsDefinitions);
            Logger.Log(ClassName, "Item storage created");
        }
    }
}