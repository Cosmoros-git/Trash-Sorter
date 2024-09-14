using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game;
using Sandbox.ModAPI;
using Trash_Sorter.BaseClass;
using Trash_Sorter.StaticComponents;
using Trash_Sorter.StaticComponents.StaticFunction;
using Trash_Sorter.StorageClasses;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI.Ingame;
using IMyCubeBlock = VRage.Game.ModAPI.IMyCubeBlock;
using IMyCubeGrid = VRage.Game.ModAPI.IMyCubeGrid;
using IMyInventory = VRage.Game.ModAPI.IMyInventory;

namespace Trash_Sorter.GridManagerRewritten
{
    public class ItemGridManager : ModBase
    {
        public HashSet<IMyInventory> Inventories = new HashSet<IMyInventory>();
        public HashSet<IMyCubeGrid> ManagedGrids = new HashSet<IMyCubeGrid>();
        public HashSet<IMyCubeBlock> ManagedBlocks = new HashSet<IMyCubeBlock>();


        public HashSet<IMyTerminalBlock> TrashBlocks = new HashSet<IMyTerminalBlock>();
        public HashSet<IMyConveyorSorter> ModSorters = new HashSet<IMyConveyorSorter>();


        private readonly HashSet<MyDefinitionId> ProcessedIds;
        protected readonly GridStorage GridStorage;
        private readonly ItemStorage ItemStorage;

        public ItemGridManager(ItemStorage itemStorage, GridStorage storage)
        {
            Logger.Log(ClassName, "Started Inventory Grid manager");

            ItemStorage = itemStorage;
            ProcessedIds = itemStorage.ProcessedItems;
            GridStorage = storage;
        }


        internal void UpdateGridInSystem(IMyCubeGrid grid)
        {
            if (!GridStorage.ManagedGrids.Contains(grid))
            {
                Logger.LogError(ClassName, $"Grid is not in the list of managed.{grid.CustomName}"); return;
            } // Skip if already managed

            Logger.Log(ClassName, $"Subscribing to the grid {grid.CustomName}");
            var blocks = grid.GetFatBlocks<IMyTerminalBlock>().ToHashSet();
            foreach (var block in blocks)
            {
                AddBlock(block);
            }
            
        }

        internal void RemoveGridFromSystem(IMyCubeGrid grid)
        {
            if (!GridStorage.ManagedGrids.Contains(grid)) return;
            Logger.LogWarning(ClassName, $"Grid {grid.DisplayName} is being removed.");
            var blocks = grid.GetFatBlocks<IMyCubeBlock>().ToHashSet();
            blocks.IntersectWith(ManagedBlocks);
            RemoveBlock(blocks);
        }

        internal void AddedGridToSystem(IMyCubeGrid grid)
        {
            if (ManagedGrids.Contains(grid))
            {
                Logger.LogWarning(ClassName, $"Grid already subscribed {grid.CustomName}");
                return;
            } // Skip if already managed

            Logger.Log(ClassName, $"Subscribing to the grid {grid.CustomName}");
            var blocks = grid.GetFatBlocks<IMyTerminalBlock>().ToHashSet();
            AddBlock(blocks);
        }



        private void AddBlock(HashSet<IMyCubeBlock> blocks)
        {
            foreach (var block in blocks)
            {
                AddBlock(block);
            }
           
        }
        private void AddBlock(IMyCubeBlock obj)
        {
            var terminal = obj as IMyTerminalBlock;
            if (terminal == null) return;
            AddBlock(terminal);
        } // Event linking.
        private void AddBlock(IMyTerminalBlock block)
        {
            if (ManagedBlocks.Contains(block)) return;

            // Skip blocks with no inventories
            if (block.InventoryCount <= 0) return;

            // Add block if it can use the conveyor system
            if (BlockFunctions.CanUseConveyorSystem(block))
            {
                ManagedBlocks.Add(block);
            }

            // If the block is a trash sorter, add to TrashSorter and invoke event
            if (Enumerable.Contains(TrashSubtype, block.BlockDefinition.SubtypeId))
            {
                var sorter = (IMyConveyorSorter)block;
                ModSorters.Add(sorter);
                OnModSorterAdded(sorter);
                return;
            }

            // If the block is part of trash inventory
            if (IsTrashInventory(block))
            {
                TrashBlocks.Add(block);
                return;
            }

            // Add inventories for the block
            for (var i = 0; i < block.InventoryCount; i++)
            {
                Add_Inventory((MyInventory)block.GetInventory(i));
            }
        } // Singular block add event.




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
        } // Deals with block inventory being counted or not.
        private void AddTrashBlock(IMyTerminalBlock block)
        {
            TrashBlocks.Add(block);
            Logger.Log(ClassName, $"Block considered as trash inventory.");
            for (var i = 0; i < block.InventoryCount; i++)
            {
                Remove_Inventory((MyInventory)block.GetInventory(i));
            }
        } // Clears inventories of blocks that are ignored.
        private void RemoveTrashBlock(IMyTerminalBlock block)
        {
            TrashBlocks.Remove(block);
            Logger.Log(ClassName, $"Block removed from trash inventories.");
            for (var i = 0; i < block.InventoryCount; i++)
            {
                Add_Inventory((MyInventory)block.GetInventory(i));
            }
        } // Adds inventories if block is no longer ignored




        private void InventoryContentChanged(MyInventoryBase arg1, MyPhysicalInventoryItem arg2, MyFixedPoint arg3)
        {
            var definition = arg2.GetDefinitionId();
            if (ProcessedIds.Contains(definition))
            {
                ItemStorage.ItemsDictionary.UpdateValue(definition, arg3);
            }
        } 
        private void Add_Inventory(MyInventory inventory)
        {
            if(Inventories.Contains(inventory)) return;
            ScanInventory(inventory);
            Inventories.Add(inventory);
            inventory.InventoryContentChanged += InventoryContentChanged;
        } // Adds inventory to management
        private void Remove_Inventory(MyInventory inventory)
        {
            if (!Inventories.Contains(inventory)) return;
            inventory.InventoryContentChanged -= InventoryContentChanged;
            Inventories.Remove(inventory);
            var items = inventory.GetItems();
            foreach (var item in items)
            {
                var id = item.GetDefinitionId();
                if (!ProcessedIds.Contains(id)) continue;

                ItemStorage.ItemsDictionary.UpdateValue(id, -item.Amount);
            }
        } // Removes inventory from management.




        private void RemoveBlock(HashSet<IMyCubeBlock> blocks)
        {
            foreach (var block in blocks)
            {
                RemoveBlock(block);
            }
        } // Group removal overload
        private void RemoveBlock(VRage.ModAPI.IMyEntity block)
        {
            if (block == null) return;
            block.OnClosing -= RemoveBlock;

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
        } // individual block removal

        public override void Dispose()
        {
            RemoveBlock(ManagedBlocks);
        }
    } // This should be done and deal with most of grid/block operations. 
}