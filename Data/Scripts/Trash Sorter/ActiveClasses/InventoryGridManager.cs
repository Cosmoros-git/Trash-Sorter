using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Trash_Sorter.Data.Scripts.Trash_Sorter.BaseClass;
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
        private readonly Main_Storage_Class.MainItemStorage _mainsItemStorage;

        /// <summary>
        /// Stopwatch for debugging purposes, measuring execution time of certain operations.
        /// </summary>
        private readonly Stopwatch ExecutionTimer = new Stopwatch();

        /// <summary>
        /// Tracks the elapsed time for smaller function executions.
        /// </summary>
        private float AccumulatedTime;

        private readonly IMyCubeBlock _systemBlock;
        private readonly Logger myLogs;

        /// <summary>
        /// Initializes a new instance of the InventoryGridManager class.
        /// </summary>
        /// <param name="mainItemStorage">Reference to the main item storage class.</param>
        /// <param name="managedGrids">Set of cube grids to manage.</param>
        /// <param name="primaryGrid">The owner grid for the grid manager.</param>
        /// <param name="systemBlock">The system manager block.</param>
        public InventoryGridManager(Main_Storage_Class.MainItemStorage mainItemStorage,
            HashSet<IMyCubeGrid> managedGrids, IMyCubeGrid primaryGrid, IMyCubeBlock systemBlock, Logger MyLogs)
        {
            MyLogs.Log(ClassName, "Started Inventory Grid manager");
            ManagedGrids = managedGrids;
            _primaryGrid = primaryGrid;
            _systemBlock = systemBlock;
            _mainsItemStorage = mainItemStorage;
            myLogs = MyLogs;
            ProcessedIds = mainItemStorage.ProcessedItems;

            // Initialize inventories and global item counts
            Get_All_Inventories();
            Get_Global_Item_Count();
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
            ExecutionTimer.Restart();
            myLogs.Log(ClassName, $"Grids to count {ManagedGrids.Count}");

            foreach (var myGrid in ManagedGrids)
            {
                // Get all blocks of type IMyTerminalBlock from the grid
                var blocks = myGrid.GetFatBlocks<IMyTerminalBlock>();
                Grid_Add((MyCubeGrid)myGrid);

                foreach (var block in blocks)
                {
                    if (TrackedBlocks.Contains(block)) return;

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

                    // Subscribe to block events
                    block.OnClosing += Block_OnClosing;
                    block.CustomNameChanged += Terminal_CustomNameChanged;

                    // Check if block is a trash inventory
                    if (IsTrashInventory(block))
                    {
                        TrashBlocks.Add(block);
                        continue;
                    }

                    // Add inventories from the block
                    for (var i = 0; i < block.InventoryCount; i++)
                    {
                        Add_Inventory((MyInventory)block.GetInventory(i));
                    }
                }
            }

            ExecutionTimer.Stop();
            myLogs.Log(ClassName,
                $"Finished counting inventories, total inventories {Inventories.Count}, time taken {ExecutionTimer.ElapsedMilliseconds}ms, block count {TrackedBlocks.Count}, trash inventories {TrashBlocks.Count}");
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
        private void Get_Global_Item_Count()
        {
            // This method runs once at the start to gather initial item counts.
            ExecutionTimer.Restart();
            myLogs.Log(ClassName, $"Fetching global item count");

            foreach (var inventory in Inventories)
            {
                var itemList = ((MyInventory)inventory).GetItems();
                if (itemList.Count == 0) continue;

                foreach (var item in itemList)
                {
                    var definition = item.GetDefinitionId();
                    if (!ProcessedIds.Contains(definition)) continue;

                    // Increment item count in the main storage dictionary
                    var reference = _mainsItemStorage.ItemsDictionary[definition];
                    reference.ItemAmount += item.Amount;
                }
            }

            ExecutionTimer.Stop();
            myLogs.Log(ClassName,
                $"Finished counting items, time taken {ExecutionTimer.ElapsedMilliseconds}ms");
        }


        // Pure debug TODO REMOVE ON PUBLISH
        public void OnAfterSimulation100()
        {
            if (AccumulatedTime > 100)
            {
                myLogs.LogWarning(ClassName,
                    $"Inventory changes total time taken {AccumulatedTime}ms, medium time per tick {AccumulatedTime / 100} ");
            }

            AccumulatedTime = 0;
        }

        // Todo manager logic and just now its broken somewhat. All grid events except close, that one works.
        private void OnGridMerge(MyCubeGrid arg1, MyCubeGrid arg2)
        {
            ExecutionTimer.Restart();
            // If grid owner is considered the one to be merged set is as main.
            if (arg2 == _primaryGrid)
            {
                _primaryGrid = arg1;
            }

            Add_Inventories_GridMerge(arg1);
            ExecutionTimer.Stop();
            myLogs.LogWarning(ClassName,
                $"GridMerge Alarm, Grid 1 name is {arg1.DisplayName}, grid 1 block count is {arg1.BlocksCount}, time taken {ExecutionTimer.ElapsedMilliseconds}ms");
        }

        private void OnGridSplit(MyCubeGrid arg1, MyCubeGrid arg2)
        {
            ExecutionTimer.Restart();
            if (_systemBlock.CubeGrid == arg1)
            {
                FatGrid_OnClosing(arg2);
            }
            else
            {
                FatGrid_OnGridSplit(arg2);
            }


            ExecutionTimer.Stop();
            myLogs.LogWarning(ClassName,
                $"GridSplit Alarm, Grid 1 name is {arg1.DisplayName}, grid 1 block count is {arg1.BlocksCount}, time taken {ExecutionTimer.ElapsedMilliseconds}ms");
        }


        // Grid merge event is just a joke, literally have to rescan the grid again.
        private void Add_Inventories_GridMerge(IMyCubeGrid changedMainGrid)
        {
            // Get all the fat blocks that are of type IMyTerminalBlock
            ExecutionTimer.Restart();
            changedMainGrid.GetGridGroup(GridLinkTypeEnum.Mechanical).GetGrids(ManagedGrids);
            Get_All_Inventories();
            ExecutionTimer.Start();
            myLogs.Log(ClassName,
                $"Grid merge detected, time taken to calculate {ExecutionTimer.ElapsedMilliseconds}");
        }

        // Probably work fine?
        private void Grid_Add(MyCubeGrid myGrid)
        {
            myGrid.OnClosing += FatGrid_OnClosing;
            myGrid.OnFatBlockAdded += FatGrid_OnFatBlockAdded;
            myGrid.OnGridSplit += OnGridSplit;
            myGrid.OnGridMerge += OnGridMerge;
            ManagedGrids.Add(myGrid);
        }

        private void Grid_Remove(MyCubeGrid myGrid)
        {
            myGrid.OnClosing -= FatGrid_OnClosing;
            myGrid.OnFatBlockAdded -= FatGrid_OnFatBlockAdded;
            myGrid.OnGridSplit -= OnGridSplit;
            myGrid.OnGridMerge -= OnGridMerge;
            ManagedGrids.Remove(myGrid);
        }

        private void FatGrid_OnGridSplit(MyCubeGrid myCubeGrid)
        {
            var connectedGridsToRemove = new HashSet<IMyCubeGrid>();

            myCubeGrid.GetGridGroup(GridLinkTypeEnum.Mechanical)?.GetGrids(connectedGridsToRemove);
            foreach (var Grid in connectedGridsToRemove)
            {
                var myGrid = (MyCubeGrid)Grid;
                myGrid.OnClosing -= FatGrid_OnClosing;
                myGrid.OnFatBlockAdded -= FatGrid_OnFatBlockAdded;
                myGrid.OnGridSplit -= OnGridSplit;
                myGrid.OnGridMerge -= OnGridMerge;
                ManagedGrids.Remove(myGrid);
            }
        }


        private void FatGrid_OnClosing(MyEntity obj)
        {
            //Logger.Instance.Log(ClassName, $"Grid closed {obj.DisplayNameText}");
            var myGrid = (IMyCubeGrid)obj;
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
            ExecutionTimer.Restart();
            var block = obj as IMyTerminalBlock;
            if (block == null) return;

            // Blocks with no inventory do not matter.
            if (block.InventoryCount <= 0) return;
            TrackedBlocks.Add(block);
            // Subscription to crucial for work events.
            block.OnClosing += Block_OnClosing;


            // Trash sorters have inventory, but will not be processed here.
            if (Enumerable.Contains(TrashSubtype, block.BlockDefinition.SubtypeId))
            {
                ModSorters.Add((IMyConveyorSorter)obj);
                OnModSorterAdded?.Invoke((IMyConveyorSorter)obj);
                return;
            }

            block.CustomNameChanged += Terminal_CustomNameChanged;

            // Skip adding inventories if the block's Name contains the "Trash" keyword
            if (IsTrashInventory(block)) return;
            // Adding all inventories
            myLogs.Log(ClassName, $"Inventory added: {block.DisplayNameText}");
            for (var i = 0; i < block.InventoryCount; i++)
            {
                Add_Inventory((MyInventory)block.GetInventory(i));
            }

            ExecutionTimer.Stop();
            myLogs.Log(ClassName,
                $"Grid inventory added, time taken to calculate {ExecutionTimer.ElapsedMilliseconds}");
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
                myLogs.Log(ClassName, $"Block considered as trash inventory.");
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
                myLogs.Log(ClassName, $"Block removed from trash inventories.");
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
                Grid_Remove((MyCubeGrid)grid);
            }

            foreach (var block in TrackedBlocks)
            {
                Block_OnClosing(block);
            }
        }
    } // This should be done and deal with most of grid/block operations. 
}