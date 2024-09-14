using System;
using System.Collections.Generic;
using System.Linq;
using Trash_Sorter.StaticComponents;
using Trash_Sorter.StaticComponents.StaticFunction;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using static Trash_Sorter.StaticComponents.StaticFunction.LogicFunctions;

namespace Trash_Sorter.GridInitializerRewritten
{
    public class GridOwnerChecks : GridManagerBase
    {
        public event Action<bool> IsThisOwner;
        private void OnIsThisOwner(bool value)
        {
            IsThisOwner?.Invoke(value);
        }

        public event Action ThisIsNotOwner;
        private void OnThisIsNotOwner()
        {
            ThisIsNotOwner?.Invoke();
        }


        public void CheckOwner(IMyEntity entity, HashSet<IMyCubeGrid> connectedGrids)
        {
            // Process connected grids to categorize ownership
            var result = GridFunctions.ProcessConnectedGrids(ModGuid, entity.EntityId.ToString(), connectedGrids);

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
                    HashGridToRemove = result.OwnedGrids;
                    HashCollectionGrids = result.ConnectedGrids;

                    OnIsThisOwner(false);
                    break;

                case OwnerResult.NullError:
                    Logger.LogError("OwnerCheckResult:CheckOwner", $"Unexpected null error for manager entity {id}. This is a critical issue.");
                    break;
            }
        }

    }
}