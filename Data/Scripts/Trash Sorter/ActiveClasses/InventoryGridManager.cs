using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.ModAPI;
using Trash_Sorter.Data.Scripts.Trash_Sorter.BaseClass;
using Trash_Sorter.Data.Scripts.Trash_Sorter.SessionComponent;
using Trash_Sorter.Data.Scripts.Trash_Sorter.StorageClasses;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Game.ModAPI.Ingame;
using IMyCubeBlock = VRage.Game.ModAPI.IMyCubeBlock;
using IMyCubeGrid = VRage.Game.ModAPI.IMyCubeGrid;
using IMyInventory = VRage.Game.ModAPI.IMyInventory;

namespace Trash_Sorter.Data.Scripts.Trash_Sorter.ActiveClasses
{
    /// <summary>
    /// The InventoryGridManager class manages grids and inventories, tracks changes in cube grids,
    /// and handles conveyor sorter events. It is responsible for linking and unlinking events, 
    /// as well as managing the logic for merging and splitting grids.
    /// </summary>
    internal class InventoryGridManager : ModBase
    {
        /// <summary>
        /// Set of cube grids managed by the inventory grid manager. Used for removing events and handling grid logic.
        /// </summary>
        public readonly HashSet<IMyCubeGrid> ManagedGridsRef;
        private readonly HashSet<IMyCubeGrid> ManagedGridsLocal;
        /// <summary>
        /// Event triggered when a modded conveyor sorter is added. 
        /// Subscribed by the ModConveyorManager.
        /// </summary>
        public event Action<IMyConveyorSorter> OnModSorterAdded;

        /// <summary>
        /// Set of observed inventories to manage event un links and logic.
        /// </summary>
        public HashSet<IMyInventory> Inventories = new HashSet<IMyInventory>();

        /// <summary>
        /// Set of terminal blocks marked as "trash" for tracking their state and managing events.
        /// </summary>
        public HashSet<IMyTerminalBlock> TrashBlocks = new HashSet<IMyTerminalBlock>();

        /// <summary>
        /// Set of trash sorters, primarily used during initialization.
        /// </summary>
        public HashSet<IMyConveyorSorter> ModSorters = new HashSet<IMyConveyorSorter>();

        /// <summary>
        /// Set of cube blocks with linked events, used for managing grid events, dispose logic, and merge/split operations.
        /// </summary>
        public HashSet<IMyCubeBlock> TrackedBlocks = new HashSet<IMyCubeBlock>();

        /// <summary>
        /// Set of processed item IDs to filter out unnecessary items during scanning.
        /// </summary>
        private readonly HashSet<MyDefinitionId> ProcessedIds;

        /// <summary>
        /// Reference to the main item storage, used to update item values and manage stored data.
        /// </summary>
        /// 
        private readonly MainItemStorage _mainsItemStorage;

        /// <summary>
        /// Initializes a new instance of the InventoryGridManager class.
        /// </summary>
        /// <param name="mainItemStorage">Reference to the main item storage class.</param>
        /// <param name="managedGrids">Set of cube grids to manage.</param>
        public InventoryGridManager(MainItemStorage mainItemStorage,
            HashSet<IMyCubeGrid> managedGrids)
        {
            Logger.Log(ClassName, "Started Inventory Grid manager");
            ManagedGridsLocal = new HashSet<IMyCubeGrid>();
            ManagedGridsRef = managedGrids;
            
            _mainsItemStorage = mainItemStorage;
            ProcessedIds = mainItemStorage.ProcessedItems;

            // Initialize inventories and global item counts
            Get_All_Inventories();
            GridDispose += RemoveGridFromSystem;
            GridAdd += AddGridToSystem;
        }

        private void Get_All_Inventories()
        {
            var wat1 = Stopwatch.StartNew();
            var wat2 = new Stopwatch();
            Logger.Log(ClassName, $"Grids to count {ManagedGridsRef.Count}");

            foreach (var myGrid in ManagedGridsRef)
            {
                wat2.Restart();
                AddGridToSystem(myGrid);
                wat2.Stop();
                Logger.Log(ClassName,$"{myGrid.CustomName} took {wat2.Elapsed.TotalMilliseconds}ms");
            }

            wat1.Stop();
            Logger.Log(ClassName,
                $"Finished counting inventories, total inventories {Inventories.Count}, total time taken: {wat1.Elapsed.TotalMilliseconds}ms, block count {TrackedBlocks.Count}, trash inventories {TrashBlocks.Count}");
        }

        private void RemoveGridFromSystem(IMyCubeGrid grid)
        {
            if (!ManagedGridsLocal.Contains(grid)) return;
            Logger.Log(ClassName, $"Grid {grid.DisplayName} is being removed.");

            Grid_Unsubscribe((MyCubeGrid)grid);
            var blocks = grid.GetFatBlocks<IMyTerminalBlock>();

            foreach (var block in blocks)
            {
                if (!TrackedBlocks.Contains(block)) continue;
                block.CustomNameChanged -= Terminal_CustomNameChanged;
                Block_OnClosing(block);
            }
        }

        private void AddGridToSystem(IMyCubeGrid grid)
        {
           
            if (ManagedGridsLocal.Contains(grid))
            {
                Logger.Log(ClassName, $"Grid already subscribed {grid.CustomName}");
                return;
            } // Skip if already managed
            Logger.Log(ClassName, $"Subscribing to the grid {grid.CustomName}");
            var blocks = grid.GetFatBlocks<IMyTerminalBlock>().ToHashSet();
            FatBlockSorter(blocks);
            Grid_Subscribe((MyCubeGrid)grid);
        }

        private void FatBlockSorter(HashSet<IMyTerminalBlock> blocks)
        {
            Logger.Log(ClassName, $"Amount of blocks to count {blocks.Count}");

            foreach (var block in blocks)
            {
                if (TrackedBlocks.Contains(block)) continue;

                // Skip blocks with no inventories
                if (block.InventoryCount <= 0) continue;

                // Add block if it can use the conveyor system
                if (CanUseConveyorSystem(block))
                {
                    TrackedBlocks.Add(block);
                }

                // If the block is a trash sorter, add to TrashSorter and invoke event
                if (Enumerable.Contains(TrashSubtype, block.BlockDefinition.SubtypeId))
                {
                    var sorter = (IMyConveyorSorter)block;
                    ModSorters.Add(sorter);
                    OnModSorterAdded?.Invoke(sorter);
                    continue;
                }

                // If the block is part of trash inventory
                if (IsTrashInventory(block))
                {
                    TrashBlocks.Add(block);
                    continue;
                }

                // Add inventories for the block
                for (var i = 0; i < block.InventoryCount; i++)
                {
                    Add_Inventory((MyInventory)block.GetInventory(i));
                }
            }
        }



        // Log for when a grid is added
        private void Grid_Subscribe(MyCubeGrid myGrid)
        {
            Logger.Log(ClassName, $"Grid_Add: Adding grid {myGrid.DisplayName}");

            if (ManagedGridsLocal.Contains(myGrid))
            {
                Logger.Log(ClassName, $"Grid {myGrid.DisplayName} is already subscribed.");
                return;
            }

            myGrid.OnFatBlockAdded += FatGrid_OnFatBlockAdded;
            ManagedGridsLocal.Add(myGrid);
        }

        private void Grid_Unsubscribe(MyCubeGrid myGrid)
        {
            Logger.Log(ClassName, $"Grid_Remove: Removing grid {myGrid.DisplayName}");

            myGrid.OnFatBlockAdded -= FatGrid_OnFatBlockAdded;
            ManagedGridsLocal.Remove(myGrid);
        }

        private void FatGrid_OnFatBlockAdded(MyCubeBlock obj)
        {
            var wat1 = Stopwatch.StartNew();
            var block = obj as IMyTerminalBlock;
            if (block == null) return;

            Logger.Log(ClassName,
                $"FatGrid_OnFatBlockAdded: Block {block.DisplayNameText} added to grid {block.CubeGrid.DisplayName}");

            // Blocks with no inventory do not matter.
            if (block.InventoryCount <= 0) return;

            TrackedBlocks.Add(block);
            block.OnClosing += Block_OnClosing;

            if (Enumerable.Contains(TrashSubtype, block.BlockDefinition.SubtypeId))
            {
                ModSorters.Add((IMyConveyorSorter)obj);
                OnModSorterAdded?.Invoke((IMyConveyorSorter)obj);
                return;
            }

            block.CustomNameChanged += Terminal_CustomNameChanged;

            if (IsTrashInventory(block)) return;

            for (var i = 0; i < block.InventoryCount; i++)
            {
                Add_Inventory((MyInventory)block.GetInventory(i));
            }

            wat1.Stop();
            Logger.Log(ClassName,
                $"FatGrid_OnFatBlockAdded: Time taken to add block {block.DisplayNameText} to grid {block.CubeGrid.DisplayName}: {wat1.Elapsed.TotalMilliseconds}ms");
        }


        private void Terminal_CustomNameChanged(IMyTerminalBlock myTerminalBlock)
        {
            if (IsTrashInventory(myTerminalBlock))
            {
                if (TrashBlocks.Contains(myTerminalBlock)) return;
                AddTrashBlock(myTerminalBlock);
            }
            else
            {
                if (!TrashBlocks.Contains(myTerminalBlock)) return;
                RemoveTrashBlock(myTerminalBlock);
            }
        }

        private void AddTrashBlock(IMyTerminalBlock block)
        {
            TrashBlocks.Add(block);
            Logger.Log(ClassName, $"Block considered as trash inventory.");
            for (var i = 0; i < block.InventoryCount; i++)
            {
                Remove_Inventory((MyInventory)block.GetInventory(i));
            }
        }

        private void RemoveTrashBlock(IMyTerminalBlock block)
        {
            TrashBlocks.Remove(block);
            Logger.Log(ClassName, $"Block removed from trash inventories.");
            for (var i = 0; i < block.InventoryCount; i++)
            {
                Add_Inventory((MyInventory)block.GetInventory(i));
            }
        }


        private void InventoryContentChanged(MyInventoryBase arg1, MyPhysicalInventoryItem arg2,
            MyFixedPoint arg3)
        {
            var definition = arg2.GetDefinitionId();
            if (ProcessedIds.Contains(definition))
            {
                _mainsItemStorage.ItemsDictionary.UpdateValue(definition, arg3);
            }
        }

        private void SingleInventoryScan(IMyInventory inventory)
        {
            var itemList = ((MyInventory)inventory).GetItems();
            if (itemList.Count <= 0) return;
            foreach (var item in itemList)
            {
                var definition = item.GetDefinitionId();
                if (!ProcessedIds.Contains(definition)) continue;
                var reference = _mainsItemStorage.ItemsDictionary[definition];
                reference.ItemAmount += item.Amount;
            }
        }

        private void Add_Inventory(MyInventory inventory)
        {
            SingleInventoryScan(inventory);
            Inventories.Add(inventory);
            inventory.InventoryContentChanged += InventoryContentChanged;
        }

        private void Remove_Inventory(MyInventory inventory)
        {
            inventory.InventoryContentChanged -= InventoryContentChanged;
            Inventories.Remove(inventory);
            var items = inventory.GetItems();
            foreach (var item in items)
            {
                var id = item.GetDefinitionId();
                if (!ProcessedIds.Contains(id)) continue;

                _mainsItemStorage.ItemsDictionary.UpdateValue(id, -item.Amount);
            }
        }


        private void Block_OnClosing(VRage.ModAPI.IMyEntity block)
        {
            if (block == null) return;
            block.OnClosing -= Block_OnClosing;

            var myBlock = (IMyTerminalBlock)block;
            if (TrashSubtype.Contains(myBlock.BlockDefinition.SubtypeId))
            {
                ModSorters.Remove(myBlock as IMyConveyorSorter);
                return;
            }

            if (IsTrashInventory(myBlock)) return;

            ((IMyTerminalBlock)block).CustomNameChanged -= Terminal_CustomNameChanged;
            for (var i = 0; i < block.InventoryCount; i++)
            {
                Remove_Inventory((MyInventory)block.GetInventory(i));
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            GridAdd -= AddGridToSystem;
            GridDispose -= RemoveGridFromSystem;

            foreach (var grid in ManagedGridsLocal.ToList())
            {
                RemoveGridFromSystem(grid);
            }

            foreach (var block in TrackedBlocks.ToList())
            {
                Block_OnClosing(block);
            }
        }
    } // This should be done and deal with most of grid/block operations. 
}