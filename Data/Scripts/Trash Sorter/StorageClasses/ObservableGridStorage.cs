using System;
using System.Collections.Generic;
using System.Linq;
using ParallelTasks;
using Trash_Sorter.StaticComponents.StaticFunctions;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Trash_Sorter.StorageClasses
{
    public class ObservableGridStorage
    {
        public HashSet<IMyCubeGrid> ManagedGrids = new HashSet<IMyCubeGrid>();

        public event Action<IMyCubeGrid> GridRemoved;
        public event Action<IMyCubeGrid> GridAdded;
        public event Action<bool> GridSplit;
        public event Action<IMyCubeGrid> GridUpdated;

        public readonly IMyEntity ManagingEntity;
        public readonly IMyCubeBlock ManagingBlock;

        public ObservableGridStorage(IMyEntity entity)
        {
            ManagingEntity = entity;
            ManagingBlock = (IMyCubeBlock)entity;
        }


        private void GetDifferences()
        {
            // Get a pooled HashSet from the pool
            var tempSet = ModObjectPools.HashSetPool<IMyCubeGrid>.Get();

            try
            {
                // Get connected grids into tempSet
                GridFunctions.TryGetConnectedGrids(ManagingBlock.CubeGrid, GridLinkTypeEnum.Mechanical, tempSet);

                // Special case: if exactly one grid is added
                if (tempSet.Count == ManagedGrids.Count + 1)
                {
                    tempSet.ExceptWith(ManagedGrids); // Find the new grid
                    SubscribeGrids(tempSet);
                    return;
                }

                // Find grids that are in one set but not the other (symmetric difference)
                tempSet.SymmetricExceptWith(ManagedGrids);

                // Subscribe or unsubscribe grids based on their presence in ManagedGrids
                foreach (var grid in tempSet)
                {
                    if (ManagedGrids.Contains(grid))
                    {
                        // Grid is in ManagedGrids but not in tempSet, so unsubscribe it
                        UnsubscribeGrid(grid);
                    }
                    else
                    {
                        // Grid is in tempSet but not in ManagedGrids, so subscribe it
                        SubscribeGrids(grid);
                    }
                }
            }
            finally
            {
                // Ensure tempSet is always returned to the pool
                ModObjectPools.HashSetPool<IMyCubeGrid>.Return(tempSet);
            }
        }

        public void SubscribeGrids(IMyCubeGrid grid)
        {
            if (grid == null) return;
            if (!ManagedGrids.Add(grid)) return;

            GridAdded?.Invoke(grid);
            grid.OnGridMerge += OnGridMerge;
            grid.OnGridSplit += OnGridSplit;
            grid.OnClosing += Grid_OnClosing;
        }

        public void SubscribeGrids(HashSet<IMyCubeGrid> gridsToAdd)
        {
            foreach (var grid in gridsToAdd)
            {
                SubscribeGrids(grid);
            }
        }

        public void UnsubscribeGrid(IMyCubeGrid grid)
        {
            if (grid == null) return;
            if (!ManagedGrids.Remove(grid)) return;

            GridRemoved?.Invoke(grid);
            grid.OnGridMerge -= OnGridMerge;
            grid.OnGridSplit -= OnGridSplit;
            grid.OnClosing -= Grid_OnClosing;
        }

        public void UnsubscribeGrid(HashSet<IMyCubeGrid> gridsToRemove)
        {
            foreach (var grid in gridsToRemove)
            {
                UnsubscribeGrid(grid);
            }
        }


        private void Grid_OnClosing(IMyEntity obj)
        {
            UnsubscribeGrid(obj as IMyCubeGrid);
        }

        private void OnGridSplit(IMyCubeGrid arg1, IMyCubeGrid arg2)
        {
            var tempSet = ModObjectPools.HashSetPool<IMyCubeGrid>.Get();

            try
            {
                GridFunctions.TryGetConnectedGrids(arg1, GridLinkTypeEnum.Mechanical, tempSet);

                // Check if the manager's block is still on the original grid (arg1)
                var managerRemainsOnOriginalGrid = tempSet.Contains(ManagingBlock.CubeGrid);

                // Invoke the GridSplit event, informing if the manager is still on the original grid
                GridSplit?.Invoke(managerRemainsOnOriginalGrid);

                // If the manager is still on the original grid, we want to handle the second grid (arg2)
                if (managerRemainsOnOriginalGrid)
                {
                    // Get connected grids for the second grid (arg2)
                    GridFunctions.TryGetConnectedGrids(arg2, GridLinkTypeEnum.Mechanical, tempSet);
                }

                // Unsubscribe all grids in TemporalGrids (this will include either arg1 or arg2)
                UnsubscribeGrid(tempSet);
            }
            finally
            {
                // Ensure tempSet is always returned to the pool
                ModObjectPools.HashSetPool<IMyCubeGrid>.Return(tempSet);
            }
        }

        private void OnGridMerge(IMyCubeGrid arg1, IMyCubeGrid arg2)
        {
            GridUpdated?.Invoke(arg1);

            // Only unsubscribe arg2
            UnsubscribeGrid(arg2);

            // Update managed grids and subscribe new ones based on the merge result
            GetDifferences();
        }
    }
}