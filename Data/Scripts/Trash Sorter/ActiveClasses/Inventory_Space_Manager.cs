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
    internal class Inventory_Grid_Manager : ModBase
    {
        public HashSet<IMyCubeGrid> Grids;
        private IMyCubeGrid OwnerGrid;


        public HashSet<IMyInventory> Inventories = new HashSet<IMyInventory>();

        public HashSet<IMyTerminalBlock> TrashBlocks = new HashSet<IMyTerminalBlock>();

        public HashSet<IMyConveyorSorter> TrashSorter = new HashSet<IMyConveyorSorter>();

        public HashSet<IMyCubeBlock> Blocks = new HashSet<IMyCubeBlock>();
        private readonly HashSet<MyDefinitionId> ProcessedIds;

        private readonly ItemStorage.Main_Storage_Class _mainsStorageClass;
        private readonly Stopwatch StopWatch = new Stopwatch();
        private float elapsedTime;

        public Inventory_Grid_Manager(ItemStorage.Main_Storage_Class mainStorageClass,
            HashSet<IMyCubeGrid> myCubeGrids, IMyCubeGrid gridOwner)
        {
            Logger.Instance.Log(ClassName, "Started Inventory Grid manager");
            Grids = myCubeGrids;
            OwnerGrid = gridOwner;
            _mainsStorageClass = mainStorageClass;
            ProcessedIds = mainStorageClass.ProcessedItems;
            Get_All_Inventories();
            Get_Global_Item_Count();
        }

        private void Get_All_Inventories()
        {
            var watch = Stopwatch.StartNew();
            Logger.Instance.Log(ClassName, $"Grids to count {Grids.Count}");
            foreach (var myGrid in Grids)
            {
                // Get all the fat blocks that are of type IMyTerminalBlock
                var blocks = myGrid.GetFatBlocks<IMyTerminalBlock>();
                Grid_Add((MyCubeGrid)myGrid);
                foreach (var block in blocks)
                {
                    if (Blocks.Contains(block)) return;
                    // Check if the block has any inventories
                    if (block.InventoryCount <= 0) continue;
                    // Add the block to the Blocks list. Just for dispose or checks
                    Blocks.Add(block);

                    // Skip processing if the block's CustomData contains the "Trash" keyword
                    if (block.CustomName.Contains(Trash)) return;
                    if (Enumerable.Contains(TrashSubtype, block.BlockDefinition.SubtypeId))
                    {
                        TrashSorter.Add(block as IMyConveyorSorter);
                        continue;
                    }

                    // Subscribe to the events
                    block.OnClosing += Block_OnClosing;
                    block.CustomNameChanged += Terminal_CustomNameChanged;


                    // Iterate through all inventories of the block
                    for (var i = 0; i < block.InventoryCount; i++)
                    {
                        Inventories.Add((MyInventory)block.GetInventory(i));
                    }
                }
            }

            watch.Stop();
            Logger.Instance.Log(ClassName,
                $"Finished counting inventories, total inventories {Inventories.Count}, time taken {watch.Elapsed}ms");
        }

        public void OnAfterSimulation100()
        {
            Logger.Instance.LogWarning(ClassName,
                $"Inventory changes total time taken {elapsedTime}ms in 100 ticks, medium time per tick {elapsedTime / 100} ");
        }

        private void OnGridMerge(MyCubeGrid arg1, MyCubeGrid arg2)
        {
            var watch = Stopwatch.StartNew();
            if (arg2 == OwnerGrid)
            {
                OwnerGrid = arg1;
            }

            Add_Inventories_GridMerge(arg1);
            watch.Stop();
            Logger.Instance.LogWarning(ClassName,
                $"GridMerge Alarm, Grid 1 name is {arg1.DisplayName}, grid 1 block count is {arg1.BlocksCount}, time taken {watch.Elapsed}ms");
        }

        private void OnGridSplit(MyCubeGrid arg1, MyCubeGrid arg2)
        {
            var watch = Stopwatch.StartNew();
            if (arg1 == OwnerGrid)
            {
                Remove_Inventories_GridSplit(arg2);
            }
            else
            {
                OwnerGrid = arg2;
                Remove_Inventories_GridSplit(arg1);
            }

            watch.Stop();
            Logger.Instance.LogWarning(ClassName,
                $"GridSplit Alarm, Grid 1 name is {arg1.DisplayName}, grid 1 block count is {arg1.BlocksCount}, time taken {watch.Elapsed}ms");
        }

        private void Get_Global_Item_Count()
        {
            // Is run at start only
            var watch = Stopwatch.StartNew();
            Logger.Instance.Log(ClassName, $"Getting global item count");
            foreach (var inventory in Inventories)
            {
                var itemList = ((MyInventory)inventory).GetItems();
                if (itemList.Count == 0) continue;

                foreach (var item in itemList)
                {
                    var definition = item.GetDefinitionId();
                    if (!ProcessedIds.Contains(definition)) continue;
                    _mainsStorageClass.ItemsDictionary[definition] += item.Amount;
                }
            }

            watch.Stop();
            Logger.Instance.Log(ClassName, $"Finished counting items, time taken {watch.Elapsed}ms");
        }


        private void Add_Inventories_GridMerge(IMyCubeGrid changedMainGrid)
        {
            // Get all the fat blocks that are of type IMyTerminalBlock
            changedMainGrid.GetGridGroup(GridLinkTypeEnum.Mechanical).GetGrids(Grids);
            Get_All_Inventories();
        }

        private void Remove_Inventories_GridSplit(IMyCubeGrid otherGrid)
        {
            // Get all the fat blocks that are of type IMyTerminalBlock
            Grid_Remove((MyCubeGrid)otherGrid);
            var blocks = otherGrid.GetFatBlocks<IMyTerminalBlock>();
            foreach (var block in blocks)
            {
                // Check if the block has any inventories
                if (block.InventoryCount <= 0) continue;
                // Add the block to the Blocks list
                Blocks.Remove(block);

                // Skip processing if the block's CustomName contains the "Trash" keyword
                if (block.CustomName.Contains(Trash)) return;

                // Subscribe to the OnClosing event
                block.OnClosing -= Block_OnClosing;
                ((IMyTerminalBlock)block).CustomNameChanged -= Terminal_CustomNameChanged;

                // Iterate through all inventories of the block
                for (var i = 0; i < block.InventoryCount; i++)
                {
                    Inventories.Remove((MyInventory)block.GetInventory(i));
                    Remove_Inventory((MyInventory)block.GetInventory(i));
                }
            }
        }

        private void Grid_Add(MyCubeGrid myGrid)
        {
            Logger.Instance.Log(ClassName, $"Grid added {myGrid.DisplayNameText}");
            myGrid.OnClosing += FatGrid_OnClosing;
            myGrid.OnFatBlockAdded += FatGrid_OnFatBlockAdded;
            myGrid.OnGridSplit += OnGridSplit;
            myGrid.OnGridMerge += OnGridMerge;
            Grids.Add(myGrid);
        }

        private void Grid_Remove(MyCubeGrid myGrid)
        {
            Logger.Instance.Log(ClassName, $"Grid removed {myGrid.DisplayNameText}");
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
                ((IMyTerminalBlock)block).CustomNameChanged -= Terminal_CustomNameChanged;
                if (!Blocks.Contains(block)) continue;
                Block_OnClosing(block);
            }
        }


        private void FatGrid_OnFatBlockAdded(MyCubeBlock obj)
        {
            var block = obj as IMyTerminalBlock;
            if (block == null) return;

            // Blocks with no inventory do not matter.
            if (block.InventoryCount <= 0) return;
            Blocks.Add(block);
            // Subscription to crucial for work events.
            block.OnClosing += Block_OnClosing;


            // Trash sorters have inventory, but will not be processed here.
            if (Enumerable.Contains(TrashSubtype, block.BlockDefinition.SubtypeId)) return;
            block.CustomNameChanged += Terminal_CustomNameChanged;

            // Skip adding inventories if the block's Name contains the "Trash" keyword
            if (block.CustomName.Contains(Trash)) return;

            // Adding all inventories
            Logger.Instance.Log(ClassName, $"Inventory added: {block.DisplayNameText}");
            for (var i = 0; i < block.InventoryCount; i++)
            {
                Add_Inventory((MyInventory)block.GetInventory(i));
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
                _mainsStorageClass.ItemsDictionary[definition] += item.Amount;
            }
        }


        // Deals with block having trash tag or not.
        private void Terminal_CustomNameChanged(IMyTerminalBlock myTerminalBlock)
        {
            if (myTerminalBlock.CustomName.Contains(Trash))
            {
                if (TrashBlocks.Contains(myTerminalBlock)) return;

                var block = (IMyCubeBlock)myTerminalBlock;
                TrashBlocks.Add(myTerminalBlock);

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

                for (var i = 0; i < block.InventoryCount; i++)
                {
                    Add_Inventory((MyInventory)block.GetInventory(i));
                }
            }
        }

        private void InventoryOnInventoryContentChanged(MyInventoryBase arg1, MyPhysicalInventoryItem arg2,
            MyFixedPoint arg3)
        {
            StopWatch.Restart();
            var definition = arg2.GetDefinitionId();
            if (ProcessedIds.Contains(definition))
            {
                _mainsStorageClass.ItemsDictionary[definition] += arg3;
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
                _mainsStorageClass.ItemsDictionary[id] -= item.Amount;
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

            if (myBlock.CustomName.Contains(Trash)) return;

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
    }
}