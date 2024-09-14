using System;
using System.Collections.Generic;
using Trash_Sorter.StaticComponents;
using Trash_Sorter.StaticComponents.StaticFunction;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Trash_Sorter.GridInitializerRewritten
{
    public class ConflictManager : GridManagerBase
    {
        public event Action ManagerSeparated;

        private readonly HashSet<IMyCubeGrid> Grids = new HashSet<IMyCubeGrid>();
        private bool Subscribed;

        public void OwnerConflict()
        {
            if (OtherManager == null) return;
            OtherManager.OnClosing += OtherManager_OnClosing;
            Subscribed = true;
            SubscribeGrids();
        }
        
        private void OtherManager_OnClosing(IMyEntity obj)
        {
            OtherManager.OnClosing -= OtherManager_OnClosing;
            OtherManager = null;
        }


        private void SubscribeGrids()
        {
            foreach (var grid in HashCollectionGrids)
            {
                if (Grids.Contains(grid)) continue;

                grid.OnGridSplit += Grid_OnGridSplit;
                Grids.Add(grid);
            }
        }

        private void UnsubscribeGrids()
        {
            foreach (var grid in HashCollectionGrids)
            {
                if (!Grids.Contains(grid)) continue;

                grid.OnGridSplit -= Grid_OnGridSplit;
                Grids.Remove(grid);
            }
        }

        private void Grid_OnGridSplit(IMyCubeGrid arg1, IMyCubeGrid arg2)
        {
            if (!ManagerWasSeparated(arg2)) return;
            ManagerSeparated?.Invoke();
            UnsubscribeGrids();
            OnUpdateRequired(MyEntityUpdateEnum.BEFORE_NEXT_FRAME);
        }

        public bool ManagerWasSeparated(IMyCubeGrid leftGrid)
        {
            // Get the connected grids based on mechanical connections (rotors, pistons, etc.)
            var connectedGrids = GridFunctions.GetConnectedGrids(leftGrid, GridLinkTypeEnum.Mechanical);

            // Ensure ThisManager and OtherManager are valid grid blocks
            var thisManagerBlock = ThisManager as IMyCubeBlock;
            var otherManagerBlock = OtherManager as IMyCubeBlock;

            if (thisManagerBlock == null || otherManagerBlock == null)
            {
                Logger.LogError(ClassName, "One or both of the system manager blocks are null.");
                return false;
            }

            // Check if both system manager grids are valid and not null
            if (thisManagerBlock.CubeGrid == null || otherManagerBlock.CubeGrid == null)
            {
                Logger.LogError(ClassName, "One or both of the system manager grids are null.");
                return false;
            }

            // Check if either manager's grid is no longer part of the connected grids set
            // If both are separated, we do nothing
            if (!connectedGrids.Contains(otherManagerBlock.CubeGrid) &&
                !connectedGrids.Contains(thisManagerBlock.CubeGrid))
            {
                return false; // Grids are completely separated
            }

            // If only one grid is disconnected, they have separated
            return !connectedGrids.Contains(otherManagerBlock.CubeGrid) ||
                   !connectedGrids.Contains(thisManagerBlock.CubeGrid);
        }

        public override void Dispose()
        {
            base.Dispose();
            UnsubscribeGrids();
            if (Subscribed) OtherManager.OnClosing -= OtherManager_OnClosing;
        }
    }
}