using System.Collections.Generic;
using Sandbox.ModAPI;
using Trash_Sorter.StaticComponents;
using Trash_Sorter.StorageClasses;
using VRage.Game;
using VRage.Game.ModAPI;

namespace Trash_Sorter.BaseClass
{
    public abstract class InventoryManagerBase : ModBase
    {
        protected HashSet<IMyCubeGrid> ManagedGrids; // Initialized outside. 
        protected HashSet<IMyCubeBlock> ManagedBlocks = new HashSet<IMyCubeBlock>();
        protected HashSet<IMyInventory> ManagedInventories = new HashSet<IMyInventory>();


        protected HashSet<IMyCubeBlock> TrashBlocks = new HashSet<IMyCubeBlock>();
        protected HashSet<IMyConveyorSorter> ModSorters = new HashSet<IMyConveyorSorter>();

        protected readonly HashSet<MyDefinitionId> ProcessedIds = ModSessionComponent.ProcessedItemsDefinitions;
        protected ObservableDictionary<MyDefinitionId> ItemStorage;
    }
}