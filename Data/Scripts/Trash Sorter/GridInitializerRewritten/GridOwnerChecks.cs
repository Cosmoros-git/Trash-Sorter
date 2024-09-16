using System;
using System.Collections.Generic;
using System.Linq;
using Trash_Sorter.BaseClass;
using Trash_Sorter.StaticComponents;
using Trash_Sorter.StaticComponents.StaticFunctions;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using static Trash_Sorter.StaticComponents.StaticFunctions.LogicFunctions;

namespace Trash_Sorter.GridInitializerRewritten
{
    public class GridOwnerChecks : GridManagerBase
    {
        public event Action<bool> IsThisOwner;

        public event Action ManagerRemoved;
        private void OnIsThisOwner(bool value)
        {
            IsThisOwner?.Invoke(value);
        }
        private void OnManagerSeparated()
        {
            ManagerRemoved?.Invoke();
        }

        public void CheckOwner(IMyEntity entity)
        {
            // Process connected grids to categorize ownership
            var result = GridFunctions.ProcessConnectedGrids(ModGuid, entity.EntityId.ToString(), GridStorage.ManagedGrids);

            // Retrieve the other manager's ID, or handle the case where no manager is found
            var otherManagerId = result.OtherManagerId.FirstOrDefault();

            // Check ownership status
            var ownerResult = IsThisManager(result.ForeignOwnedGrids.Count, result.OwnedGrids.Count,
                entity, otherManagerId);

            // Handle the ownership check result
            var id = entity.EntityId.ToString();
            GridClaiming(ownerResult, result, id, otherManagerId);
        }


        private void GridClaiming(OwnerResult ownerResult, GridFunctions.GridProcessingResult result, string id, string otherManagerId)
        {
            switch (ownerResult)
            {
                case OwnerResult.Owner:
                    var overridingGrids = result.UnownedGrids.Concat(result.ForeignOwnedGrids).ToArray();
                    StorageModFunctions.SetGridStorageValue(ModGuid, overridingGrids, id, otherManagerId);
                    HashCollectionGrids = result.ConnectedGrids;
                    OnIsThisOwner(true);
                    break;

                case OwnerResult.NotOwner:
                    Logger.LogWarning("OwnerCheckResult:CheckOwner", $"This manager {id}, is not the manager. Current manager is {otherManagerId}");
                    var idLong = long.Parse(otherManagerId);
                    GetEntityById(idLong, out OtherManager);

                    // Just add all grids to observe because otherwise my manager on grid split wont be enabled...
                    HashGridToChange = result.OwnedGrids;
                    GridStorage.ManagedGrids = result.ConnectedGrids;

                    OnIsThisOwner(false);
                    break;

                case OwnerResult.NullError:
                    Logger.LogError("OwnerCheckResult:CheckOwner", $"Unexpected null error for manager entity {id}. This is a critical issue.");
                    break;
            }
        }

        public enum ManagerSeparationSituation
        {
            NullError = 0,
            GridSeparationArg1Both = 1,
            GridSeparationArg2Both = 2,
            GridSeparationArg1HasManager = 3,
            GridSeparationArg2HasManager = 4,
        }

        public ManagerSeparationSituation ManagerWasSeparated(IMyCubeGrid leftGrid)
        {
            GridFunctions.TryGetConnectedGrids(leftGrid, GridLinkTypeEnum.Mechanical,
                HashSetArg2); // Contains separated grids

            // Manager block checks. In theory never to happen.
            var thisManagerBlock = ThisManager as IMyCubeBlock;
            var otherManagerBlock = OtherManager as IMyCubeBlock;

            if (thisManagerBlock == null || otherManagerBlock == null)
            {
                Logger.LogError(ClassName, "One or both of the system manager blocks are invalid.");
                return ManagerSeparationSituation.NullError;
            }

            var thisManagerGrid = thisManagerBlock.CubeGrid;
            var otherManagerGrid = otherManagerBlock.CubeGrid;

            // Check if both system manager grids are valid
            if (thisManagerGrid == null || otherManagerGrid == null)
            {
                Logger.LogError(ClassName, "One or both of the system manager grids are null.");
                return ManagerSeparationSituation.NullError;
            }

            // Check if managers are on leftGrid (arg2)
            var thisManagerOnLeftGrid = HashSetArg1.Contains(thisManagerGrid);
            var otherManagerOnLeftGrid = HashSetArg1.Contains(otherManagerGrid);

            if (thisManagerOnLeftGrid)
            {
                return otherManagerOnLeftGrid
                    ?
                    // Case 1: Both thisManager and otherManager are on arg2 (leftGrid)
                    ManagerSeparationSituation.GridSeparationArg2Both
                    :
                    // Case 2: thisManager is on arg2 (leftGrid) and otherManager is on arg1 (rightGrid)
                    ManagerSeparationSituation.GridSeparationArg2HasManager;
            }

            return otherManagerOnLeftGrid
                ?
                // Case 3: thisManager is on arg1 (rightGrid), but otherManager is on arg2 (leftGrid)
                ManagerSeparationSituation.GridSeparationArg1Both
                :
                // Case 4: thisManager is on arg1 (rightGrid) and otherManager is also on arg1 (rightGrid)
                ManagerSeparationSituation.GridSeparationArg1HasManager;
        }

    }
}