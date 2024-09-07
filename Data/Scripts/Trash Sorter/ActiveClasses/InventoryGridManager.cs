using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Sandbox.Game;
using Sandbox.Game.Entities;
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
        public HashSet<IMyCubeGrid> ManagedGrids;

        /// <summary>
        /// The owner grid used for merging/splitting logic.
        /// </summary>
        private IMyCubeGrid _primaryGrid;

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
        private readonly MainItemStorage _mainsItemStorage;

        /// <summary>
        /// Stopwatch for debugging purposes, measuring execution time of certain operations.
        /// </summary>
        private readonly Stopwatch ExecutionTimer = new Stopwatch();

        /// <summary>
        /// Tracks the elapsed time for smaller function executions.
        /// </summary>
        private float AccumulatedTime;

        private readonly IMyCubeBlock _systemBlock;

        /// <summary>
        /// Initializes a new instance of the InventoryGridManager class.
        /// </summary>
        /// <param name="mainItemStorage">Reference to the main item storage class.</param>
        /// <param name="managedGrids">Set of cube grids to manage.</param>
        /// <param name="primaryGrid">The owner grid for the grid manager.</param>
        /// <param name="systemBlock">The system manager block.</param>
        public InventoryGridManager(MainItemStorage mainItemStorage,
            HashSet<IMyCubeGrid> managedGrids, IMyCubeGrid primaryGrid, IMyCubeBlock systemBlock)
        {
            Logger.Log(ClassName, "Started Inventory Grid manager");
            ManagedGrids = managedGrids;
            _primaryGrid = primaryGrid;
            _systemBlock = systemBlock;
            _mainsItemStorage = mainItemStorage;
            ProcessedIds = mainItemStorage.ProcessedItems;

            // Initialize inventories and global item counts
            Get_All_Inventories();
            GridDispose += GridSystemOwnerCallback_GridDispose;
        }

        private void GridSystemOwnerCallback_GridDispose(IMyCubeGrid obj)
        {
            Grid_Remove((MyCubeGrid)obj);
        }

        /// <summary>
        /// Retrieves all inventories from the managed grids and registers relevant events.
        /// It also processes blocks based on their conveyor system capabilities and trash status.
        /// </summary>
        private void Get_All_Inventories()
        {
            var wat1 = Stopwatch.StartNew(); // Overall time for the method
            var watGrid = new Stopwatch(); // Time for processing each grid
            var watBlocks = new Stopwatch(); // Time for processing each block
            var watTrashCheck = new Stopwatch(); // Time for checking trash inventories
            var watInventoryAdd = new Stopwatch(); // Time for adding inventories

            Logger.Log(ClassName, $"Grids to count {ManagedGrids.Count}");

            foreach (var myGrid in ManagedGrids)
            {
                watGrid.Restart();
                var blocks = myGrid.GetFatBlocks<IMyTerminalBlock>().ToHashSet();
                Grid_Add((MyCubeGrid)myGrid);
                watGrid.Stop();

                Logger.Log(ClassName,
                    $"Processed grid {myGrid.DisplayName}, block count: {blocks.Count}, time taken: {watGrid.Elapsed.TotalMilliseconds}ms");

                foreach (var block in blocks)
                {
                    watBlocks.Restart();

                    if (TrackedBlocks.Contains(block)) return;

                    // Skip blocks with no inventories
                    if (block.InventoryCount <= 0)
                    {
                        watBlocks.Stop();
                        continue;
                    }

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
                        watBlocks.Stop();
                        continue;
                    }

                    // Subscribe to block events
                    block.OnClosing += Block_OnClosing;
                    block.CustomNameChanged += Terminal_CustomNameChanged;

                    watBlocks.Stop();

                    // Time to check if the block is a trash inventory
                    watTrashCheck.Restart();
                    if (IsTrashInventory(block))
                    {
                        TrashBlocks.Add(block);
                        watTrashCheck.Stop();
                        continue;
                    }

                    watTrashCheck.Stop();

                    // Time to add inventories from the block
                    watInventoryAdd.Restart();
                    for (var i = 0; i < block.InventoryCount; i++)
                    {
                        Add_Inventory((MyInventory)block.GetInventory(i));
                    }

                    watInventoryAdd.Stop();
                }

                Logger.Log(ClassName,
                    $"Processed all blocks in grid {myGrid.DisplayName}, time taken: {watBlocks.Elapsed.TotalMilliseconds}ms, trash check time: {watTrashCheck.Elapsed.TotalMilliseconds}ms, inventory add time: {watInventoryAdd.Elapsed.TotalMilliseconds}ms");
            }

            wat1.Stop();
            Logger.Log(ClassName,
                $"Finished counting inventories, total inventories {Inventories.Count}, total time taken: {wat1.Elapsed.TotalMilliseconds}ms, block count {TrackedBlocks.Count}, trash inventories {TrashBlocks.Count}");
        }


        /// <summary>
        /// Checks whether a block can use the conveyor system by verifying if it is one of the relevant block types.
        /// </summary>
        /// <param name="block">The terminal block being checked.</param>
        /// <returns>True if the block supports the conveyor system; otherwise, false.</returns>
        private static bool CanUseConveyorSystem(IMyTerminalBlock block)
        {
            return (block is IMyCargoContainer ||
                    block is IMyConveyorSorter ||
                    block is IMyProductionBlock ||
                    block is IMyShipConnector ||
                    block is IMyCollector ||
                    block is IMyShipDrill ||
                    block is IMyShipGrinder ||
                    block is IMyShipWelder ||
                    block is IMyReactor ||
                    block is IMyGasTank ||
                    block is IMyGasGenerator ||
                    block is IMyPowerProducer);
        }


        /// <summary>
        /// Scans all managed inventories at initialization and sums up the total item amounts.
        /// It uses the ProcessedIds to filter which items to track, and updates the item counts in the main item storage.
        /// </summary>

        // Pure debug TODO REMOVE ON PUBLISH
        public void OnAfterSimulation100()
        {
            if (AccumulatedTime > 100)
            {
                Logger.LogWarning(ClassName,
                    $"Inventory changes total time taken {AccumulatedTime}ms, medium time per tick {AccumulatedTime / 100} ");
            }

            AccumulatedTime = 0;
        }

        // Todo manager logic and just now its broken somewhat. All grid events except close, that one works.

        // Log for when a grid is merged
        private void OnGridMerge(MyCubeGrid arg1, MyCubeGrid arg2)
        {
            Logger.Log(ClassName,
                $"OnGridMerge: Grid {arg1.DisplayName} merged with Grid {arg2.DisplayName} (now 0 blocks)");

            // Ensure the _primaryGrid is updated to the surviving grid (arg1)
            if (_primaryGrid == arg2)
            {
                Logger.Log(ClassName,
                    $"OnGridMerge: _primaryGrid was on Grid {arg2.DisplayName}, updating to merged Grid {arg1.DisplayName}");
                _primaryGrid = arg1;
            }
            else
            {
                Logger.Log(ClassName,
                    $"OnGridMerge: _primaryGrid is already on Grid {arg1.DisplayName}, no update needed.");
            }

            Add_Inventories_GridMerge(arg1);
        }


        // Log for when a grid is split
        private void OnGridSplit(MyCubeGrid arg1, MyCubeGrid arg2)
        {
            Logger.Log(ClassName, $"OnGridSplit: Grid {arg1.DisplayName} split with Grid {arg2.DisplayName}");

            // Check if the manager's system block is still on the grid
            if (_systemBlock.CubeGrid == arg1)
            {
                Logger.Log(ClassName,
                    $"OnGridSplit: Manager block is still on Grid {arg1.DisplayName}. Handling split.");
                FatGrid_OnGridSplit(arg2); // Unregister the split grid
            }
            else if (_systemBlock.CubeGrid == arg2)
            {
                Logger.Log(ClassName,
                    $"OnGridSplit: Manager block is on the new split Grid {arg2.DisplayName}. Handling split.");
                GridSplit_IsSplitGridCase(arg2);
                FatGrid_OnGridSplit(arg1); // Unregister the old grid
            }
            else
            {
                Logger.Log(ClassName, $"OnGridSplit: Manager block is on neither of the split grids, possible error.");
            }
        }

        // Grid merge event is just a joke, literally have to rescan the grid again.
        // Adding inventories after a grid is merged
        private void Add_Inventories_GridMerge(IMyCubeGrid changedMainGrid)
        {
            Logger.Log(ClassName,
                $"Add_Inventories_GridMerge: Processing grid {changedMainGrid.DisplayName} after merge.");

            changedMainGrid.GetGridGroup(GridLinkTypeEnum.Mechanical).GetGrids(ManagedGrids);
            Get_All_Inventories();
        }

        // Log for when a grid is added
        private void Grid_Add(MyCubeGrid myGrid)
        {
            Logger.Log(ClassName, $"Grid_Add: Adding grid {myGrid.DisplayName}");

            myGrid.OnClosing += FatGrid_OnClosing;
            myGrid.OnFatBlockAdded += FatGrid_OnFatBlockAdded;
            myGrid.OnGridSplit += OnGridSplit;
            myGrid.OnGridMerge += OnGridMerge;
            ManagedGrids.Add(myGrid);
        }

        // Log for when a grid is removed
        private void Grid_Remove(MyCubeGrid myGrid)
        {
            Logger.Log(ClassName, $"Grid_Remove: Removing grid {myGrid.DisplayName}");

            myGrid.OnClosing -= FatGrid_OnClosing;
            myGrid.OnFatBlockAdded -= FatGrid_OnFatBlockAdded;
            myGrid.OnGridSplit -= OnGridSplit;
            myGrid.OnGridMerge -= OnGridMerge;
            ManagedGrids.Remove(myGrid);
        }

        private void FatGrid_OnGridSplit(MyCubeGrid myCubeGrid)
        {
            Logger.Log(ClassName, $"FatGrid_OnGridSplit: Grid {myCubeGrid.DisplayName} is splitting.");

            var connectedGridsToRemove = new HashSet<IMyCubeGrid>();

            myCubeGrid.GetGridGroup(GridLinkTypeEnum.Mechanical)?.GetGrids(connectedGridsToRemove);
            foreach (var grid in connectedGridsToRemove)
            {
                var myGrid = (MyCubeGrid)grid;
                myGrid.OnClosing -= FatGrid_OnClosing;
                myGrid.OnFatBlockAdded -= FatGrid_OnFatBlockAdded;
                myGrid.OnGridSplit -= OnGridSplit;
                myGrid.OnGridMerge -= OnGridMerge;
                ManagedGrids.Remove(myGrid);

                Logger.Log(ClassName, $"FatGrid_OnGridSplit: Removed grid {myGrid.DisplayName}");
            }
        }

        private void GridSplit_IsSplitGridCase(MyCubeGrid myCubeGrid)
        {
            Grid_Add(myCubeGrid);
        }

        private void FatGrid_OnClosing(MyEntity obj)
        {
            var myGrid = (IMyCubeGrid)obj;
            Logger.Log(ClassName, $"FatGrid_OnClosing: Grid {myGrid.DisplayName} is closing.");

            Grid_Remove((MyCubeGrid)obj);
            var blocks = myGrid.GetFatBlocks<IMyTerminalBlock>();

            foreach (var block in blocks)
            {
                block.CustomNameChanged -= Terminal_CustomNameChanged;
                if (!TrackedBlocks.Contains(block)) continue;
                Block_OnClosing(block);
            }
        }


        // Adds inventories or trash sorters when block added to the grid.
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


        // Deals with block having trash tag or not.
        private void Terminal_CustomNameChanged(IMyTerminalBlock myTerminalBlock)
        {
            if (IsTrashInventory(myTerminalBlock))
            {
                if (TrashBlocks.Contains(myTerminalBlock)) return;

                var block = (IMyCubeBlock)myTerminalBlock;
                TrashBlocks.Add(myTerminalBlock);
                Logger.Log(ClassName, $"Block considered as trash inventory.");
                for (var i = 0; i < block.InventoryCount; i++)
                {
                    Remove_Inventory((MyInventory)block.GetInventory(i));
                }
            }
            else
            {
                if (!TrashBlocks.Contains(myTerminalBlock)) return;

                var block = (IMyCubeBlock)myTerminalBlock;
                TrashBlocks.Remove(myTerminalBlock);
                Logger.Log(ClassName, $"Block removed from trash inventories.");
                for (var i = 0; i < block.InventoryCount; i++)
                {
                    Add_Inventory((MyInventory)block.GetInventory(i));
                }
            }
        }

        // Scuffed way to check for trash tag.
        private static bool IsTrashInventory(IMyTerminalBlock block)
        {
            return block.CustomName.IndexOf(Trash, StringComparison.OrdinalIgnoreCase) >= 0;
        }


        // Inventory event and value changes logic. Probably need to move value updates onto cycles of 10. A lot of these end with end change being 0.
        private void InventoryOnInventoryContentChanged(MyInventoryBase arg1, MyPhysicalInventoryItem arg2,
            MyFixedPoint arg3)
        {
            var definition = arg2.GetDefinitionId();
            //Logger.Instance.Log(ClassName, $"Changed {definition}, change {arg3}");
            if (ProcessedIds.Contains(definition))
            {
                _mainsItemStorage.ItemsDictionary.UpdateValue(definition, arg3);
            }

            ExecutionTimer.Stop();
        }


        // Inventory complete injection and coupling
        private void Add_Inventory(MyInventory inventory)
        {
            SingleInventoryScan(inventory);
            Inventories.Add(inventory);
            inventory.InventoryContentChanged += InventoryOnInventoryContentChanged;
            var itemList = inventory.GetItems();
            if (itemList.Count == 0) return;
        }

        private void Remove_Inventory(MyInventory inventory)
        {
            inventory.InventoryContentChanged -= InventoryOnInventoryContentChanged;
            Inventories.Remove(inventory);
            var items = inventory.GetItems();
            foreach (var item in items)
            {
                var id = item.GetDefinitionId();
                if (!ProcessedIds.Contains(id)) continue;

                _mainsItemStorage.ItemsDictionary.UpdateValue(id, -item.Amount);
            }
        }


        // Not sure if Grid On Closing and this can fight each other. Pls no crash.
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
            foreach (var grid in ManagedGrids)
            {
                FatGrid_OnClosing((MyCubeGrid)grid);
            }

            foreach (var block in TrackedBlocks)
            {
                Block_OnClosing(block);
            }
        }
    } // This should be done and deal with most of grid/block operations. 
}