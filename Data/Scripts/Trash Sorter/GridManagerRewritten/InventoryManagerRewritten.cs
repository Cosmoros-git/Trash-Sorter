using System.Collections.Generic;
using Trash_Sorter.BaseClass;
using Trash_Sorter.StorageClasses;
using VRage.Game.ModAPI;

namespace Trash_Sorter.GridManagerRewritten
{
    internal class InventoryManagerRewritten : InventoryManagerBase
    {
        public readonly InventoryManager InventoryManager;

        public InventoryManagerRewritten(ItemStorage itemStorage, HashSet<IMyCubeGrid> gridManagerHashCollectionGrids)
        {
            InventoryManager = new InventoryManager();
            ItemStorage = itemStorage.ItemsDictionary;
        }
    }
}