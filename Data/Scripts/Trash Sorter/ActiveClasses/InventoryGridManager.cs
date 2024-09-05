using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.Entities.Cube;
using Sandbox.Game.GameSystems.Conveyors;
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
    internal class InventoryGridManager : ModBase
    {
        // Contains the group of grids for removal of events and the owner grid id for merging/splitting logic
        // TODO FINISH THAT LOGIC. ITS BORKED RN.
        public HashSet<IMyCubeGrid> Grids;
        private IMyCubeGrid OwnerGrid;

        public event Action<IMyConveyorSorter> OnTrashSorterAdded; // Event for ModConveyorManager to add my modded sorters

        public HashSet<IMyInventory> Inventories = new HashSet<IMyInventory>(); // List of observed inventories to unlink events and logic.

        public HashSet<IMyTerminalBlock> TrashBlocks = new HashSet<IMyTerminalBlock>(); // Trash blocks, stored to manage their states and events.

        public HashSet<IMyConveyorSorter> TrashSorter = new HashSet<IMyConveyorSorter>(); // List of sorters, ngl... its only useful at start, probably deserves to be changed.

        public HashSet<IMyCubeBlock> Blocks = new HashSet<IMyCubeBlock>(); // List of all blocks I have events linked to. For dispose and merge/split purposes.

        private readonly HashSet<MyDefinitionId> ProcessedIds; // List of Ids for inventory scanner to skip items I don't care.

        private readonly Main_Storage_Class.MainItemStorage _mainsItemStorage; // Item storage, used for item value updates and storage.
        private readonly Stopwatch StopWatch = new Stopwatch(); // Debug stopwatch.
        private float elapsedTime; // To measure time small functions take together.

        public InventoryGridManager(Main_Storage_Class.MainItemStorage mainItemStorage,
            HashSet<IMyCubeGrid> myCubeGrids, IMyCubeGrid gridOwner)
        {
            Logger.Instance.Log(ClassName, "Started Inventory Grid manager");
            Grids = myCubeGrids;
            OwnerGrid = gridOwner;
            _mainsItemStorage = mainItemStorage;
            ProcessedIds = mainItemStorage.ProcessedItems;
            Get_All_Inventories();
            Get_Global_Item_Count();
        }


        // Get all inventories of relevant blocks, such blocks can be seen in CanUseConveyorSystem
        private void Get_All_Inventories()
        {
            StopWatch.Restart();
            Logger.Instance.Log(ClassName, $"Grids to count {Grids.Count}");
            foreach (var myGrid in Grids)
            {
                // Get all the fat blocks that are of type IMyTerminalBlock
                var blocks = myGrid.GetFatBlocks<IMyTerminalBlock>();
                Grid_Add((MyCubeGrid)myGrid);
                foreach (var block in blocks)
                {
                    if (Blocks.Contains(block)) return;

                    // Check if the block has any inventories, if none skip
                    if (block.InventoryCount <= 0) continue;


                    if (CanUseConveyorSystem(block))
                    {
                        Blocks.Add(block);
                    }

                    // Skip processing if the block's CustomData contains the "Trash" keyword

                    if (Enumerable.Contains(TrashSubtype, block.BlockDefinition.SubtypeId))
                    {
                        var sorter = (IMyConveyorSorter)block;
                        TrashSorter.Add(sorter);
                        OnTrashSorterAdded?.Invoke(sorter);
                        continue;
                    }

                    // Subscribe to the events
                    block.OnClosing += Block_OnClosing;
                    block.CustomNameChanged += Terminal_CustomNameChanged;
                    if (IsTrashInventory(block))
                    {
                        TrashBlocks.Add(block);
                        continue;
                    }

                    // Iterate through all inventories of the block
                    for (var i = 0; i < block.InventoryCount; i++)
                    {
                        Add_Inventory((MyInventory)block.GetInventory(i));
                    }
                }
            }

            StopWatch.Stop();
            Logger.Instance.Log(ClassName,
                $"Finished counting inventories, total inventories {Inventories.Count}, time taken {StopWatch.ElapsedMilliseconds}ms, block count {Blocks.Count}, trash inventories {TrashBlocks.Count}");
        }

        // List of Interfaces I check for to get inventories. These are all that implement conveyor logic, sadly myConveyorEndPoint is not whitelisted. So I have this.
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

        // At init scans all items to store them. Uses ProcessedIds as filter.
        private void Get_Global_Item_Count()
        {
            // Is run at start only
            StopWatch.Restart();
            Logger.Instance.Log(ClassName, $"Fetching global item count");
            foreach (var inventory in Inventories)
            {
                var itemList = ((MyInventory)inventory).GetItems();
                if (itemList.Count == 0) continue;

                foreach (var item in itemList)
                {
                    var definition = item.GetDefinitionId();
                    if (!ProcessedIds.Contains(definition)) continue;
                    _mainsItemStorage.ItemsDictionary[definition] += item.Amount;
                }
            }

            StopWatch.Stop();
            Logger.Instance.Log(ClassName, $"Finished counting items, time taken {StopWatch.ElapsedMilliseconds}ms");
        }




        // Pure debug TODO REMOVE ON PUBLISH
        public void OnAfterSimulation100()
        {
            if (elapsedTime > 100)
            {
                Logger.Instance.LogWarning(ClassName,
                    $"Inventory changes total time taken {elapsedTime}ms, medium time per tick {elapsedTime / 100} ");
            }

            elapsedTime = 0;
        }

        // Todo manager logic and just now its broken somewhat. All grid events except close, that one works.
        private void OnGridMerge(MyCubeGrid arg1, MyCubeGrid arg2)
        {
            StopWatch.Restart();

            // If grid owner is considered the one to be merged set is as main.
            if (arg2 == OwnerGrid)
            {
                OwnerGrid = arg1;
            }

            Add_Inventories_GridMerge(arg1);
            StopWatch.Stop();
            Logger.Instance.LogWarning(ClassName,
                $"GridMerge Alarm, Grid 1 name is {arg1.DisplayName}, grid 1 block count is {arg1.BlocksCount}, time taken {StopWatch.ElapsedMilliseconds}ms");
        }
        private void OnGridSplit(MyCubeGrid arg1, MyCubeGrid arg2)
        {
            StopWatch.Restart();
            if (arg1 == OwnerGrid)
            {
                FatGrid_OnClosing(arg2);
            }
            else
            {
                OwnerGrid = arg2;
                FatGrid_OnClosing(arg1);
            }


            StopWatch.Stop();
            Logger.Instance.LogWarning(ClassName,
                $"GridSplit Alarm, Grid 1 name is {arg1.DisplayName}, grid 1 block count is {arg1.BlocksCount}, time taken {StopWatch.ElapsedMilliseconds}ms");
        }


        // Grid merge event is just a joke, literally have to rescan the grid again.
        private void Add_Inventories_GridMerge(IMyCubeGrid changedMainGrid)
        {
            // Get all the fat blocks that are of type IMyTerminalBlock
            StopWatch.Restart();
            changedMainGrid.GetGridGroup(GridLinkTypeEnum.Mechanical).GetGrids(Grids);
            Get_All_Inventories();
            StopWatch.Start();
            Logger.Instance.Log(ClassName,
                $"Grid merge detected, time taken to calculate {StopWatch.ElapsedMilliseconds}");
        }

        // Probably work fine?
        private void Grid_Add(MyCubeGrid myGrid)
        {
            myGrid.OnClosing += FatGrid_OnClosing;
            myGrid.OnFatBlockAdded += FatGrid_OnFatBlockAdded;
            myGrid.OnGridSplit += OnGridSplit;
            myGrid.OnGridMerge += OnGridMerge;
            Grids.Add(myGrid);
        }
        private void Grid_Remove(MyCubeGrid myGrid)
        {
            myGrid.OnClosing -= FatGrid_OnClosing;
            myGrid.OnFatBlockAdded -= FatGrid_OnFatBlockAdded;
            myGrid.OnGridSplit -= OnGridSplit;
            myGrid.OnGridMerge -= OnGridMerge;
            Grids.Remove(myGrid);
        }

        private void FatGrid_OnClosing(MyEntity obj)
        {
            Logger.Instance.Log(ClassName, $"Grid closed {obj.DisplayNameText}");
            var myGrid = (IMyCubeGrid)obj;
            Grid_Remove((MyCubeGrid)obj);
            var blocks = myGrid.GetFatBlocks<IMyTerminalBlock>();

            foreach (var block in blocks)
            {
                block.CustomNameChanged -= Terminal_CustomNameChanged;
                if (!Blocks.Contains(block)) continue;
                Block_OnClosing(block);
            }
        }

        // Adds inventories or trash sorters when block added to the grid.
        private void FatGrid_OnFatBlockAdded(MyCubeBlock obj)
        {
            StopWatch.Restart();
            var block = obj as IMyTerminalBlock;
            if (block == null) return;

            // Blocks with no inventory do not matter.
            if (block.InventoryCount <= 0) return;
            Blocks.Add(block);
            // Subscription to crucial for work events.
            block.OnClosing += Block_OnClosing;


            // Trash sorters have inventory, but will not be processed here.
            if (Enumerable.Contains(TrashSubtype, block.BlockDefinition.SubtypeId))
            {
                TrashSorter.Add((IMyConveyorSorter)obj);
                OnTrashSorterAdded?.Invoke((IMyConveyorSorter)obj);
                return;
            }

            block.CustomNameChanged += Terminal_CustomNameChanged;

            // Skip adding inventories if the block's Name contains the "Trash" keyword
            if (IsTrashInventory(block)) return;
            // Adding all inventories
            Logger.Instance.Log(ClassName, $"Inventory added: {block.DisplayNameText}");
            for (var i = 0; i < block.InventoryCount; i++)
            {
                Add_Inventory((MyInventory)block.GetInventory(i));
            }

            StopWatch.Stop();
            Logger.Instance.Log(ClassName,
                $"Grid inventory added, time taken to calculate {StopWatch.ElapsedMilliseconds}");
        }
        private void SingleInventoryScan(IMyInventory inventory)
        {
            var itemList = ((MyInventory)inventory).GetItems();
            if (itemList.Count <= 0) return;
            foreach (var item in itemList)
            {
                var definition = item.GetDefinitionId();
                if (!ProcessedIds.Contains(definition)) continue;
                _mainsItemStorage.ItemsDictionary[definition] += item.Amount;
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
                Logger.Instance.Log(ClassName, $"Block considered as trash inventory.");
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
                Logger.Instance.Log(ClassName, $"Block removed from trash inventories.");
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
            StopWatch.Restart();
            var definition = arg2.GetDefinitionId();
            //Logger.Instance.Log(ClassName, $"Changed {definition}, change {arg3}");
            if (ProcessedIds.Contains(definition))
            {
                _mainsItemStorage.ItemsDictionary.UpdateValue(definition, arg3);
            }

            StopWatch.Stop();
            elapsedTime += StopWatch.ElapsedMilliseconds;
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
                TrashSorter.Remove(myBlock as IMyConveyorSorter);
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
            foreach (var grid in Grids)
            {
                Grid_Remove((MyCubeGrid)grid);
            }

            foreach (var block in Blocks)
            {
                Block_OnClosing(block);
            }
        }
    } // This should be done and deal with most of grid/block operations. 
}