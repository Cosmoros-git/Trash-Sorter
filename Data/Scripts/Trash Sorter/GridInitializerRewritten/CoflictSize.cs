using System;
using System.Collections.Generic;
using Trash_Sorter.StaticComponents;
using Trash_Sorter.StaticComponents.StaticFunction;
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
    internal class CoflictSize : GridManagerBase
    {
        private HashSet<IMyCubeGrid> _connectedGrids; // In theory this is only 1 grid.
        private IMyEntity _manager;
        private int count;
        private readonly int MinAmount = ModSessionComponent.BlockLimitsToStartManaging;

        public event Action SizeAchieved;

        protected virtual void OnSizeAchieved()
        {
            SizeAchieved?.Invoke();
            UnsubscribeEvents();
        }

        // Initializer
        public void GridSizeIssue(ref HashSet<IMyCubeGrid> grids, IMyEntity manager)
        {
            _connectedGrids = grids;
            _manager = manager;

            foreach (var grid in _connectedGrids)
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
            if (((IMyCubeBlock)_manager).CubeGrid != arg1)
            {
                count -= GridFunctions.GridBlockCount(arg1);
                _connectedGrids.Add(arg2);
                _connectedGrids.Remove(arg1);
            }
            else
            {
                count -= GridFunctions.GridBlockCount(arg2);
            }
        }

        private void Grid_OnGridMerge(IMyCubeGrid arg1, IMyCubeGrid arg2)
        {
            count = GridFunctions.GridBlockCount(arg1);
            if (count > MinAmount) OnSizeAchieved();
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
            foreach (var grid in _connectedGrids)
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