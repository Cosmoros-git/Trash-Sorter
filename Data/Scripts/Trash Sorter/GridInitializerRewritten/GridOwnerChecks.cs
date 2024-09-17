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
        public void CheckOwner()
        {
            var tempGridResult = ModObjectPools.GridProcessingResultPool.Get();
            try
            {
                // Process connected grids to categorize ownership
                GridFunctions.ProcessConnectedGrids(ModGuid, ThisManager.EntityId.ToString(), GridStorage.ManagedGrids,
                    tempGridResult);

                // Retrieve the other manager's ID, or handle the case where no manager is found
                var otherManagerId = tempGridResult.OtherManagerId.FirstOrDefault();

                // Check ownership status
                var ownerResult = IsThisManager(tempGridResult.ForeignOwnedGrids.Count, tempGridResult.OwnedGrids.Count,
                    ThisManager, otherManagerId);

                // Handle the ownership check result
                var id = ThisManager.EntityId.ToString();
                GridClaiming(ownerResult, tempGridResult, id, otherManagerId);
            }
            finally
            {
                ModObjectPools.GridProcessingResultPool.Return(tempGridResult);
            }
        }
        
        private void GridClaiming(OwnerResult ownerResult, ModObjectPools.GridProcessingResult result, string id,
            string otherManagerId)
        {
            switch (ownerResult)
            {
                case OwnerResult.Owner:
                    var overridingGrids = result.UnownedGrids.Concat(result.ForeignOwnedGrids).ToArray();
                    StorageModFunctions.SetGridStorageValue(ModGuid, overridingGrids, id, otherManagerId);
                    GridStorage.SubscribeAllGrids();
                    GridStorage.SubscribeGrids(overridingGrids);
                    OnIsThisOwner(true);
                    break;

                case OwnerResult.NotOwner:
                    Logger.LogWarning("OwnerCheckResult:CheckOwner",
                        $"This manager {id}, is not the manager. Current manager is {otherManagerId}");
                    GetEntityById(otherManagerId, out OtherManager);

                    // Just add all grids to observe because otherwise my manager on grid split wont be enabled...
                    GridStorage.PartialDispose();

                    OnIsThisOwner(false);
                    break;

                case OwnerResult.NullError:
                    Logger.LogError("OwnerCheckResult:CheckOwner",
                        $"Unexpected null error for manager entity {id}. This is a critical issue.");
                    Dispose();
                    break;
            }
        }
    }
}