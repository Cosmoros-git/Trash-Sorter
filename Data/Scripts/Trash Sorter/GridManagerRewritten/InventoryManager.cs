using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game;
using Sandbox.ModAPI;
using Trash_Sorter.BaseClass;
using Trash_Sorter.StaticComponents;
using Trash_Sorter.StaticComponents.StaticFunctions;
using Trash_Sorter.StorageClasses;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Trash_Sorter.GridManagerRewritten
{
    public class InventoryManager : InventoryManagerBase
    {
        public InventoryManager()
        {
            Logger.Log(ClassName, "Started Inventory Grid manager");
        }

        internal void UpdateGridInSystem(IMyCubeGrid grid)
        {
            if (!ManagedGrids.Contains(grid))
            {
                Logger.LogError(ClassName, $"Grid is not in the list of managed.{grid.CustomName}");
                return;
            } // Skip if already managed

            Logger.Log(ClassName, $"Subscribing to the grid {grid.CustomName}");
            var blocks = grid.GetFatBlocks<IMyTerminalBlock>().ToHashSet();
            blocks.ExceptWith(ManagedBlocks.OfType<IMyTerminalBlock>());
            AddBlock(blocks);
        }

        internal void RemoveGridFromSystem(IMyCubeGrid grid)
        {
            if (!ManagedGrids.Contains(grid)) return;
            Logger.LogWarning(ClassName, $"Grid {grid.DisplayName} is being removed.");
            var blocks = grid.GetFatBlocks<IMyTerminalBlock>().ToHashSet();
            blocks.IntersectWith(ManagedBlocks.OfType<IMyTerminalBlock>());
            RemoveBlock(blocks);
        }
        internal void AddGridToSystem(IMyCubeGrid grid)
        {
            if (!ManagedGrids.Add(grid))
            {
                Logger.LogWarning(ClassName, $"Grid already subscribed {grid.CustomName}");
                return;
            } // Skip if already managed

            Logger.Log(ClassName, $"Subscribing to the grid {grid.CustomName}");
            var blocks = grid.GetFatBlocks<IMyTerminalBlock>().ToHashSet();
            AddBlock(blocks);
        }


        private void AddBlock(HashSet<IMyTerminalBlock> blocks)
        {
            foreach (var block in blocks)
            {
                AddBlock(block);
            }
        }
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
            AddInventory(block);
        }






        // Events
        private void Terminal_CustomNameChanged(IMyTerminalBlock myTerminalBlock)
        {
            if (IsTrashInventory(myTerminalBlock))
            {
                if (!TrashBlocks.Add(myTerminalBlock)) return;
                Logger.Log(ClassName, $"Block considered as trash inventory.");
                ManageInventory(myTerminalBlock, -1);
            }
            else
            {
                if (!TrashBlocks.Remove(myTerminalBlock)) return;
                Logger.Log(ClassName, $"Block removed from trash inventories.");
                ManageInventory(myTerminalBlock, 1);
            }
        } // Deals with block inventory being counted or not.
        private void InventoryContentChanged(MyInventoryBase arg1, MyPhysicalInventoryItem arg2, MyFixedPoint arg3)
        {
            var definition = arg2.Content.GetId();
            if (ProcessedIds.Contains(definition))
            {
                StorageClasses.ItemStorage.ItemsDictionary.UpdateValue(definition, arg3);
            }
        }



        private void AddInventory(IMyInventory inventory)
        {
            if (inventory == null || !ManagedInventories.Add(inventory)) return;

            var myInventory = inventory as MyInventory;
            if (myInventory != null)
            {
                myInventory.InventoryContentChanged += InventoryContentChanged;
            }

            ManageInventory(inventory, 1);
        }
        private void AddInventory(IMyCubeBlock block)
        {
            InventoryFunctions.ProcessInventory(block, AddInventory);
        }






        private void RemoveInventory(IMyCubeBlock block)
        {
            InventoryFunctions.ProcessInventory(block, RemoveInventory);
        }
        private void RemoveInventory(IMyInventory inventory)
        {
            if (inventory == null || !ManagedInventories.Remove(inventory)) return;

            var myInventory = inventory as MyInventory;
            if (myInventory != null)
            {
                myInventory.InventoryContentChanged -= InventoryContentChanged;
            }

            ManageInventory(inventory, -1);
        }




        private void RemoveBlock<T>(HashSet<T> blocks) where T : IMyEntity
        {
            foreach (var block in blocks)
            {
                RemoveBlock(block);
            }
        } // Group removal overload
        private void RemoveBlock<T>(T block) where T : IMyEntity
        {
            if (block == null) return;
            block.OnClosing -= RemoveBlock;

            var terminal = (IMyTerminalBlock)block;
            if (TrashSubtype.Contains(terminal.BlockDefinition.SubtypeId))
            {
                ModSorters.Remove(terminal as IMyConveyorSorter);
                return;
            }

            if (IsTrashInventory(terminal)) return;

            terminal.CustomNameChanged -= Terminal_CustomNameChanged;
            RemoveInventory((IMyCubeBlock)block);
        } // individual block removal




        private void ManageInventory(IMyInventory inventory, int multiplier)
        {
            InventoryFunctions.ScanInventoryUsingMyModDictionary(inventory, ItemStorage, multiplier);
        }
        private void ManageInventory(IMyCubeBlock inventory, int multiplier)
        {
            InventoryFunctions.ScanInventoryUsingMyModDictionary(inventory, ItemStorage, multiplier);
        }





        public override void Dispose()
        {
            RemoveBlock(ManagedBlocks);
        }
    } // This should be done and deal with most of grid/block operations. 
}