using System;
using System.Collections.Generic;
using Trash_Sorter.BaseClass;
using Trash_Sorter.StaticComponents;
using Trash_Sorter.StaticComponents.StaticFunctions;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Trash_Sorter.GridInitializerRewritten
{
    /// <summary>
    /// Manages grid size-related logic for grids associated with a manager entity.
    /// This class tracks the total number of blocks across connected grids and fires an event when the block count exceeds a specified minimum threshold.
    /// It also handles grid merges and splits, adjusting the block count accordingly.
    /// </summary>
    /// <remarks>
    /// - This class subscribes to grid-related events such as block addition, block removal, grid merge, and grid split to maintain an up-to-date count of blocks.
    /// - The system only activates and fires the <see cref="SizeAchieved"/> event once the total block count across connected grids exceeds the predefined limit.
    /// - Once the <see cref="SizeAchieved"/> event is triggered, the class unsubscribes from all grid events to prevent further tracking.
    /// </remarks>
    public class ConflictSize : GridManagerBase
    {
        private int count;
        private readonly int MinAmount = ModSessionComponent.BlockLimitsToStartManaging;

        public event Action SizeAchieved;
        private IMyCubeBlock ManagerBlock;

        protected virtual void OnSizeAchieved()
        {
            SizeAchieved?.Invoke();
            UnsubscribeEvents();
        }

        // Initializer
        public void GridSizeIssue()
        {
            ManagerBlock = (IMyCubeBlock)ThisManager;
            foreach (var grid in HashCollectionGrids)
            {
                count += GridFunctions.GridBlockCount(grid);
                grid.OnBlockAdded += Grid_OnBlockAdded;
                grid.OnBlockRemoved += Grid_OnBlockRemoved;
                grid.OnGridMerge += Grid_OnGridMerge;
                grid.OnGridSplit += Grid_OnGridSplit;
            }
        }

        // If grid split or merged count changes
        private void Grid_OnGridSplit(IMyCubeGrid arg1, IMyCubeGrid arg2)
        {
            if (ManagerBlock.CubeGrid != arg1)
            {
                count -= GridFunctions.GridBlockCount(arg1);
                HashCollectionGrids.Add(arg2);
                HashCollectionGrids.Remove(arg1);
            }
            else
            {
                count -= GridFunctions.GridBlockCount(arg2);
                HashCollectionGrids.Remove(arg2);
            }
        }

        private void Grid_OnGridMerge(IMyCubeGrid arg1, IMyCubeGrid arg2)
        {
            var tempHashGrid = ModObjectPools.HashSetPool<IMyCubeGrid>.Get();
            try
            {
                GridFunctions.TryGetConnectedGrids(arg1, GridLinkTypeEnum.Mechanical, tempHashGrid);
                count = 0;
                HashCollectionGrids.Clear();
                HashCollectionGrids.UnionWith(tempHashGrid);
                foreach (var grid in tempHashGrid)
                {
                    count += GridFunctions.GridBlockCount(grid);
                }

                if (count > MinAmount) OnSizeAchieved();
            }
            finally
            {
                ModObjectPools.HashSetPool<IMyCubeGrid>.Return(tempHashGrid);
            }
        }


        // Counts blocks added/removed. System wont start until min is reached.
        private void Grid_OnBlockRemoved(IMySlimBlock obj)
        {
            count--;
        }

        private void Grid_OnBlockAdded(IMySlimBlock obj)
        {
            count++;
            if (count > MinAmount) OnSizeAchieved();
        }

        // Dispose method.
        private void UnsubscribeEvents()
        {
            foreach (var grid in HashCollectionGrids)
            {
                count -= GridFunctions.GridBlockCount(grid);
                grid.OnBlockAdded -= Grid_OnBlockAdded;
                grid.OnBlockRemoved -= Grid_OnBlockRemoved;
                grid.OnGridMerge -= Grid_OnGridMerge;
                grid.OnGridSplit -= Grid_OnGridSplit;
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            UnsubscribeEvents();
        }
    }
}