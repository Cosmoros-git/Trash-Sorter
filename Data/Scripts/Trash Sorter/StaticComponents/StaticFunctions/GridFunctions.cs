using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Policy;
using Sandbox.Game.Entities;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Trash_Sorter.StaticComponents.StaticFunctions
{
    public static class GridFunctions
    {
        /// <summary>
        /// Represents the result of attempting to claim ownership of a grid.
        /// This enum provides information on whether the grid is owned by the current system, not claimed, or owned by another system.
        /// </summary>
        public enum GridClaimResult : byte
        {
            /// <summary>
            /// The grid is already owned by the given ID.
            /// </summary>
            Owned = 0,

            /// <summary>
            /// The grid is not claimed by any system.
            /// </summary>
            NotClaimed = 1,

            /// <summary>
            /// The grid is owned by another system with a different ID.
            /// </summary>
            OwnedByOther = 2,
        }


        /// <summary>
        /// Verifies the claim status of the specified grid without modifying its storage.
        /// This method checks if a grid has a stored ID associated with a specific mod.
        /// If no ID is found, or if the ID belongs to another entity, the method returns the appropriate status.
        /// </summary>
        /// <param name="grid">The grid to check for a claim.</param>
        /// <param name="modGuid">The GUID used to identify the mod or system claiming the grid.</param>
        /// <param name="managerId">The ID of the entity attempting to claim the grid.</param>
        /// <param name="containedManagerId">An output parameter that contains the ID currently stored in the grid, if any.</param>
        /// <returns>
        /// A <see cref="GridClaimResult"/> indicating the status of the claim:
        /// - <c>Owned</c>: The grid is already owned by the entity with the specified ID.
        /// - <c>NotClaimed</c>: No claim or stored ID exists for the grid.
        /// - <c>OwnedByOther</c>: The grid is owned by another entity with a different ID.
        /// </returns>
        /// <remarks>
        /// This method does not modify the grid's storage. It is intended only for checking the current claim status of a grid.
        /// </remarks>
        public static GridClaimResult CheckGridClaimStatus(Guid modGuid,
            IMyCubeGrid grid,
            string managerId,
            out string containedManagerId)
        {
            var storage = grid.Storage;
            containedManagerId = null;

            if (storage == null)
            {
                Logger.Log("CheckGridClaimStatus", $"{grid.DisplayName} storage is null. No existing claim found.");
                return GridClaimResult.NotClaimed;
            }

            if (storage.TryGetValue(modGuid, out containedManagerId))
            {
                Logger.Log("CheckGridClaimStatus",
                    $"{grid.DisplayName}: Retrieved storedBlockId: {containedManagerId}");
            }
            else
            {
                Logger.Log("CheckGridClaimStatus", $"{grid.DisplayName}: Could not retrieve stored ID.");
                return GridClaimResult.NotClaimed;
            }

            if (string.IsNullOrEmpty(containedManagerId))
            {
                Logger.Log("CheckGridClaimStatus", $"{grid.DisplayName}: Stored ID is empty, grid not claimed.");
                return GridClaimResult.NotClaimed;
            }

            if (containedManagerId == managerId)
            {
                Logger.Log("CheckGridClaimStatus",
                    $"{grid.DisplayName}: Grid already owned by this system with ID: {managerId}.");
                return GridClaimResult.Owned;
            }
            else
            {
                Logger.Log("CheckGridClaimStatus",
                    $"{grid.DisplayName}: Grid is owned by another system with ID: {containedManagerId}.");
                return GridClaimResult.OwnedByOther;
            }
        }

        /// <summary>
        /// Processes all grids in the provided collection to categorize them based on their ownership status.
        /// </summary>
        /// <param name="modGuid">The unique identifier (GUID) used by the mod or system to claim ownership of the grids.</param>
        /// <param name="managerId">The ID of the system or entity attempting to manage the grids.</param>
        /// <param name="connectedGrids">A collection of grids to be processed, implementing <see cref="HashSet{IMyCubeGrid}"/>.</param>
        /// <param name="tempGridResult">A <see cref="ModObjectPools.GridProcessingResult"/> object to store the categorized grids.</param>
        /// <returns>
        /// A <see cref="ModObjectPools.GridProcessingResult"/> object containing categorized grids:
        /// <list type="bullet">
        /// <item><c>OwnedGrids</c>: Grids that are already owned by the system using the provided <paramref name="managerId"/>.</item>
        /// <item><c>UnownedGrids</c>: Grids that are not yet claimed by any system.</item>
        /// <item><c>ForeignOwnedGrids</c>: Grids that are owned by a different system with a different ID than the one provided in <paramref name="managerId"/>.</item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// This overload is used when a predefined collection of grids is available for processing, rather than starting from a reference grid.
        /// </remarks>
        public static void ProcessConnectedGrids(Guid modGuid,
            string managerId,
            HashSet<IMyCubeGrid> connectedGrids, ModObjectPools.GridProcessingResult tempGridResult)
        {
            // Early exit if there are no connected grids
            if (connectedGrids.Count == 0)
            {
                Logger.Log("ProcessConnectedGrids", "No connected grids found.");
                return;
            }

            // Process each connected grid
            foreach (var grid in connectedGrids)
            {
                string containedManagerId;
                var gridClaimResult = CheckGridClaimStatus(modGuid, grid, managerId, out containedManagerId);

                Logger.Log("ProcessConnectedGrids",
                    $"Processing grid '{grid.DisplayName}' with manager ID '{managerId}', stored ID '{containedManagerId}'");

                // Categorize the grid based on its claim result
                switch (gridClaimResult)
                {
                    case GridClaimResult.Owned:
                        tempGridResult.OwnedGrids.Add(grid);
                        break;
                    case GridClaimResult.NotClaimed:
                        tempGridResult.UnownedGrids.Add(grid);
                        break;
                    case GridClaimResult.OwnedByOther:
                        tempGridResult.ForeignOwnedGrids.Add(grid);
                        tempGridResult.OtherManagerId.Add(containedManagerId);
                        break;
                }
            }

            // Log the final result summary
            Logger.Log("ProcessConnectedGrids",
                $"Finished processing grids. Owned: {tempGridResult.OwnedGrids.Count}, Unowned: {tempGridResult.UnownedGrids.Count}, " +
                $"Owned by different ID: {tempGridResult.ForeignOwnedGrids.Count}");
        }

        /// <summary>
        /// Retrieves the grid group data for the specified grid and connection type.
        /// </summary>
        /// <param name="grid">The grid from which to retrieve the connected grid group. If the grid is null, the method returns null.</param>
        /// <param name="type">The type of connection to consider when retrieving the grid group (e.g., Mechanical, Electrical, Logical).</param>
        /// <returns>
        /// An instance of <see cref="IMyGridGroupData"/> representing the grid group connections. Returns null if the grid is null or if the grid group data cannot be retrieved.
        /// </returns>
        /// <remarks>
        /// The returned <see cref="IMyGridGroupData"/> instance is a pooled object. Do not store references to it beyond immediate use. Subscribe to the <see cref="IMyGridGroupData.OnReleased"/> event to clean up any event handlers or references when the grid group data is released.
        /// </remarks>
        public static IMyGridGroupData GetGridGroup(IMyCubeGrid grid, GridLinkTypeEnum type)
        {
            if (grid == null)
            {
                Logger.LogWarning("GetGridGroup", "Attempted to retrieve grid group data for a null grid.");
                return null;
            }

            var gridGroupData = grid.GetGridGroup(type);
            if (gridGroupData == null)
            {
                Logger.LogError("GetGridGroup",
                    $"Failed to retrieve grid group data for grid ID: {grid.EntityId} with link type: {type}");
            }

            return gridGroupData;
        }

        /// <summary>
        /// Retrieves all grids connected to the provided grid based on the specified grid link type.
        /// The method clears the passed set and then populates it with grids connected mechanically, electrically, or logically based on the connection type.
        /// </summary>
        /// <param name="grid">The reference grid from which to find connected grids. If the grid is null, an empty set is returned.</param>
        /// <param name="type">The type of connection to use for finding connected grids (e.g., Mechanical, Electrical, Logical).</param>
        /// <param name="connectedGrids">A <see cref="HashSet{IMyCubeGrid}"/> that will be populated with connected grids. This set is cleared at the start of the method.</param>
        /// <returns>
        /// A boolean indicating whether connected grids were successfully retrieved. Returns false if the grid or its grid group is null.
        /// </returns>
        public static bool TryGetConnectedGrids(IMyCubeGrid grid, GridLinkTypeEnum type,
            HashSet<IMyCubeGrid> connectedGrids)
        {
            // Clear the passed grid set before populating it
            connectedGrids.Clear();

            // Validate that the grid is not null
            if (grid == null)
            {
                Logger.LogError("TryGetConnectedGrids", "Provided grid is null.");
                return false;
            }

            // Retrieve the grid group using the provided connection type
            var gridGroup = grid.GetGridGroup(type);
            if (gridGroup == null)
            {
                Logger.LogError("TryGetConnectedGrids", $"Grid group is null for grid: {grid.DisplayName}.");
                return false;
            }

            // Collect all connected grids
            gridGroup.GetGrids(connectedGrids);

            // Log the result for debugging purposes
            Logger.Log("TryGetConnectedGrids",
                $"Found {connectedGrids.Count} connected grids for grid: {grid.DisplayName}.");

            return true; // Indicate success
        }

        /// <summary>
        /// Returns the block count of the specified grid.
        /// If the grid is null or cannot be cast to <see cref="MyCubeGrid"/>, it returns 0.
        /// </summary>
        /// <param name="grid">The grid from which to retrieve the block count. Must implement <see cref="IMyCubeGrid"/>.</param>
        /// <returns>
        /// The number of blocks in the grid. If the grid is null or the cast to <see cref="MyCubeGrid"/> fails, returns 0.
        /// </returns>
        /// <remarks>
        /// This method casts the provided <see cref="IMyCubeGrid"/> to <see cref="MyCubeGrid"/> to access the <c>BlocksCount</c> property.
        /// If the cast is not successful, it returns 0.
        /// </remarks>
        public static int GridBlockCount(IMyCubeGrid grid)
        {
            // Check if the grid is null, return 0 if it is
            if (grid == null) return 0;

            // Cast to MyCubeGrid and return the block count
            var myCubeGrid = grid as MyCubeGrid;
            return myCubeGrid?.BlocksCount ?? 0;
        }

        /// <summary>
        /// Returns the total number of blocks across all grids in the provided collection.
        /// This method calculates the total block count by summing the block counts of each grid
        /// using the <see cref="GridBlockCount(IMyCubeGrid)"/> method for individual grids.
        /// </summary>
        /// <param name="grids">
        /// A collection of grids implementing <see cref="IEnumerable{IMyCubeGrid}"/>. The collection can be of any type,
        /// such as <see cref="List{IMyCubeGrid}"/>, <see cref="HashSet{IMyCubeGrid}"/>, or an array.
        /// </param>
        /// <returns>
        /// The total number of blocks across all grids in the collection. If the collection is empty or null, returns 0.
        /// </returns>
        /// <remarks>
        /// This method iterates through the provided collection and sums the block count of each grid by calling the
        /// <see cref="GridBlockCount(IMyCubeGrid)"/> method for each grid. It works with any type of collection that
        /// implements <see cref="IEnumerable{IMyCubeGrid}"/>.
        /// </remarks>
        public static int GridBlockCount(IEnumerable<IMyCubeGrid> grids)
        {
            return grids.Sum(GridBlockCount);
        }
    }
}