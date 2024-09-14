using System.Collections.Generic;
using Trash_Sorter.BaseClass;
using Trash_Sorter.StaticComponents;
using Trash_Sorter.StorageClasses;
using VRage.Game.ModAPI;

namespace Trash_Sorter.GridManagers
{
    public class GridConnectionManager : GridManagement
    {
        private readonly GridEventManager _gridEventManager;


        private readonly SystemManagerStorage ThisManager;
        private readonly SystemManagerStorage OtherManager;
        public GridConnectionManager(GridStorage gridStorage, GridEventManager gridEventManager)
        {
            _gridEventManager = gridEventManager;
            ThisManager = gridStorage.ThisManager;
            OtherManager = gridStorage.OtherManager;
            GridSplitInvoked += OnGridSplit;
            GridMergeInvoked += OnGridMerge;
        }

        private void OnGridSplit(IMyCubeGrid arg1, IMyCubeGrid arg2)
        {
            HashSet<IMyCubeGrid> gridSet;
            ThisManager.ForceReferenceUpdates();
            var cubeGridRef = GridManagerExistsOn(arg2) ? arg2 : arg1;

            GetConnectedGrids(cubeGridRef, out gridSet);
            _gridEventManager.UnsubscribeGrid(gridSet);

        }
        private void OnGridMerge(IMyCubeGrid arg1, IMyCubeGrid _)
        {
            HashSet<IMyCubeGrid> managedGrids, notManagedGrids, connectedGrids;
            ThisManager.ForceReferenceUpdates();

            // Process connected grids from the first grid (arg1)
            ProcessConnectedGrids(arg1, out managedGrids, out notManagedGrids, out connectedGrids, OtherManager, ThisManager);

            // Case 1: No other manager found, claim all grids
            if (notManagedGrids.Count == 0)
            {
                Logger.Log(ClassName, "Grid_OnGridMerge: No other manager found, claiming all grids.");
                _gridEventManager.SubscribeGrid(connectedGrids); // Subscribe to all connected grids
                return;
            }

            // Case 2: This system should remain the manager
            if (IsThisManager(notManagedGrids.Count, managedGrids.Count,ThisManager,OtherManager))
            {
                Logger.Log(ClassName, $"Grid_OnGridMerge: This grid remains the manager, claiming unmanaged grids.");
                _gridEventManager.SubscribeGrid(notManagedGrids);
                return;
            }

            // Case 3: The other system becomes the manager
            Logger.LogWarning(ClassName, $"Grid_OnGridMerge: New merged grid managed by {OtherManager.EntityI?.DisplayName} is larger, transferring control.");

            if (OtherManager.EntityI != null)
            {
                // Subscribe to the other manager's OnMarkForClose event
                OnEnterStandBy(Conflict);
                Logger.Log(ClassName, $"Grid_OnGridMerge: Subscribed to OnMarkForClose for {OtherManager.IdString}");
            }
            else
            {
                // Error handling in case OtherSystemManager is unexpectedly null
                Logger.LogError(ClassName, "Critical error: OtherSystemManager is null.");
                OnDisposeRequired(); // This is just a nuke in situation that something went really bad.
            }
        }


        private bool GridManagerExistsOn(IMyCubeGrid grid)
        {
            HashSet<IMyCubeGrid> connectedGrids;
            GetConnectedGrids(grid, out connectedGrids);
            var systemGrid = ThisManager.CubeGridI;

            return connectedGrids.Contains(systemGrid);
        }
    }
}