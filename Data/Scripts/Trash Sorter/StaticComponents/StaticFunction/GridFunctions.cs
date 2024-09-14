using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Trash_Sorter.StaticComponents.StaticFunction
{
    public static class GridFunctions
    {
        /// <summary>
        /// Represents the result of attempting to claim ownership of a grid.
        /// This enum provides information on whether the grid is owned by the current system, not claimed, or owned by another system.
        /// </summary>
        public enum GridClaimResult : byte
        {
            Owned = 0, // 1: Already owned by given Id
            NotClaimed = 1, // 2: Not claimed 
            OwnedByOther = 2, // 3: Owned by another id
        }


        /// <summary>
        /// Represents the result of processing grids that are connected to a reference grid.
        /// This class categorizes connected grids into four groups:
        /// <para>- Owned grids: Grids that are already owned by the system managing them.</para>
        /// <para>- Unowned grids: Grids that are not yet claimed by any system.</para>
        /// <para>- Grids owned by a different system: Grids managed by a different system than the one performing the processing.</para>
        /// <para>- Connected grids: All grids that are connected to the reference grid.</para>
        /// </summary>
        public class GridProcessingResult
        {
            /// <summary>
            /// Gets or sets the set of grids that are owned by the system performing the grid processing.
            /// </summary>
            public HashSet<IMyCubeGrid> OwnedGrids { get; set; } = new HashSet<IMyCubeGrid>();

            /// <summary>
            /// Gets or sets the set of grids that are not yet owned by any system.
            /// </summary>
            public HashSet<IMyCubeGrid> UnownedGrids { get; set; } = new HashSet<IMyCubeGrid>();

            /// <summary>
            /// Gets or sets the set of grids that are owned by a different system or manager.
            /// These grids are managed by a system with a different ID from the one performing the grid processing.
            /// </summary>
            public HashSet<IMyCubeGrid> ForeignOwnedGrids { get; set; } = new HashSet<IMyCubeGrid>();

            /// <summary>
            /// Gets or sets the set of grids that are connected to the reference grid, 
            /// including owned, unowned, and grids owned by different systems.
            /// </summary>
            public HashSet<IMyCubeGrid> ConnectedGrids { get; set; } = new HashSet<IMyCubeGrid>();

            /// <summary>
            /// Hash set of manager ids, should never go above 1.
            /// If its size = 0 there is no other manager.
            /// </summary>
            public HashSet<string> OtherManagerId { get; set; } = new HashSet<string>();

            public HashSet<IMyEntity> OtherManagerEntity { get; set; } = new HashSet<IMyEntity>();
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
        /// - <c>Claimed</c>: The grid is successfully claimed by the system.
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
        /// Processes all grids connected to a reference grid via a specified grid link type,
        /// categorizing them into sets based on their ownership status.
        /// </summary>
        /// <param name="modGuid">The unique identifier (GUID) used by the mod or system to claim ownership of the grids.</param>
        /// <param name="managerId">The ID of the system or entity attempting to manage the grids.</param>
        /// <param name="referenceGrid">The reference grid to which other grids are connected via a specified link type.</param>
        /// <param name="type">The type of grid link to be considered for finding connected grids (e.g., Mechanical, Electrical, Logical).</param>
        /// <returns>
        /// A <see cref="GridProcessingResult"/> object containing categorized grids:
        /// <list type="bullet">
        /// <item><c>OwnedGrids</c>: Grids that are already owned by the system using the provided <paramref name="managerId"/>.</item>
        /// <item><c>UnownedGrids</c>: Grids that are not yet claimed by any system.</item>
        /// <item><c>ForeignOwnedGrids</c>: Grids that are owned by a system with a different ID than the one provided in <paramref name="managerId"/>.</item>
        /// <item><c>ConnectedGrids</c>: All grids that are connected to the <paramref name="referenceGrid"/>.</item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// This overload is useful for scenarios where a reference grid is provided, and grids connected to it by a specific link type (e.g., Mechanical or Electrical) need to be categorized.
        /// </remarks>
        public static GridProcessingResult ProcessConnectedGrids(
            Guid modGuid,
            string managerId,
            IMyCubeGrid referenceGrid,
            GridLinkTypeEnum type)
        {
            var result = new GridProcessingResult
            {
                // Get all connected grids
                ConnectedGrids = GetConnectedGrids(referenceGrid, type)
            };

            // Early exit if there are no connected grids
            if (result.ConnectedGrids.Count == 0)
            {
                Logger.Log("ProcessConnectedGrids", "No connected grids found.");
                return result;
            }

            // Process each connected grid
            foreach (var grid in result.ConnectedGrids)
            {
                string containedManagerId;
                var gridClaimResult = CheckGridClaimStatus(modGuid, grid, managerId, out containedManagerId);

                Logger.Log("ProcessConnectedGrids",
                    $"Processing grid '{grid.DisplayName}' with manager ID '{managerId}', stored ID '{containedManagerId}'");

                // Categorize the grid based on its claim result
                switch (gridClaimResult)
                {
                    case GridClaimResult.Owned:
                        result.OwnedGrids.Add(grid);
                        break;
                    case GridClaimResult.NotClaimed:
                        result.UnownedGrids.Add(grid);
                        break;
                    case GridClaimResult.OwnedByOther:
                        result.ForeignOwnedGrids.Add(grid);
                        result.OtherManagerId.Add(containedManagerId);
                        break;
                }
            }

            // Log the final result summary
            Logger.Log("ProcessConnectedGrids",
                $"Finished processing grids. Owned: {result.OwnedGrids.Count}, Unowned: {result.UnownedGrids.Count}, " +
                $"Owned by different ID: {result.ForeignOwnedGrids.Count}");

            return result;
        }


        /// <summary>
        /// Processes all grids in the provided collection to categorize them based on their ownership status.
        /// </summary>
        /// <param name="modGuid">The unique identifier (GUID) used by the mod or system to claim ownership of the grids.</param>
        /// <param name="managerId">The ID of the system or entity attempting to manage the grids.</param>
        /// <param name="connectedGrids">A collection of grids to be processed, implementing <see cref="HashSet{IMyCubeGrid}"/>.</param>
        /// <returns>
        /// A <see cref="GridProcessingResult"/> object containing categorized grids:
        /// <list type="bullet">
        /// <item><c>OwnedGrids</c>: Grids that are already owned by the system using the provided <paramref name="managerId"/>.</item>
        /// <item><c>UnownedGrids</c>: Grids that are not yet claimed by any system.</item>
        /// <item><c>ForeignOwnedGrids</c>: Grids that are owned by a different system with a different ID than the one provided in <paramref name="managerId"/>.</item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// This overload is used when a predefined collection of grids is available for processing, rather than starting from a reference grid.
        /// </remarks>
        public static GridProcessingResult ProcessConnectedGrids(
            Guid modGuid,
            string managerId,
            HashSet<IMyCubeGrid> connectedGrids)
        {
            var result = new GridProcessingResult
            {
                // Get all connected grids
                ConnectedGrids = connectedGrids
            };

            // Early exit if there are no connected grids
            if (result.ConnectedGrids.Count == 0)
            {
                Logger.Log("ProcessConnectedGrids", "No connected grids found.");
                return result;
            }

            // Process each connected grid
            foreach (var grid in result.ConnectedGrids)
            {
                string containedManagerId;
                var gridClaimResult = CheckGridClaimStatus(modGuid, grid, managerId, out containedManagerId);

                Logger.Log("ProcessConnectedGrids", $"Processing grid '{grid.DisplayName}' with manager ID '{managerId}', stored ID '{containedManagerId}'");

                // Categorize the grid based on its claim result
                switch (gridClaimResult)
                {
                    case GridClaimResult.Owned:
                        result.OwnedGrids.Add(grid);
                        break;
                    case GridClaimResult.NotClaimed:
                        result.UnownedGrids.Add(grid);
                        break;
                    case GridClaimResult.OwnedByOther:
                        result.ForeignOwnedGrids.Add(grid);
                        result.OtherManagerId.Add(containedManagerId);
                        break;
                }
            }

            // Log the final result summary
            Logger.Log("ProcessConnectedGrids",
                $"Finished processing grids. Owned: {result.OwnedGrids.Count}, Unowned: {result.UnownedGrids.Count}, " +
                $"Owned by different ID: {result.ForeignOwnedGrids.Count}");

            return result;
        }


        /// <summary>
        /// Retrieves all grids connected to the provided grid based on the specified grid link type.
        /// The method returns a set of connected grids, which can be linked mechanically, electrically, or logically depending on the provided connection type.
        /// </summary>
        /// <param name="grid">The reference grid from which to find connected grids. If the grid is null, an empty set is returned.</param>
        /// <param name="type">The type of connection to use for finding connected grids (e.g., Mechanical, Electrical, Logical).</param>
        /// <returns>
        /// A <see cref="HashSet{IMyCubeGrid}"/> containing all grids connected to the provided grid based on the given link type.
        /// If the grid or its grid group is null, an empty set is returned.
        /// </returns>
        /// <remarks>
        /// <para>- This method is useful for retrieving all grids connected to a reference grid through various types of connections, such as mechanical or electrical links.</para>
        /// <para>- The result can be used to perform operations on all connected grids, such as ownership validation or block manipulation.</para>
        /// </remarks>
        public static HashSet<IMyCubeGrid> GetConnectedGrids(IMyCubeGrid grid, GridLinkTypeEnum type)
        {
            // Ensure the grid is not null
            if (grid == null)
            {
                Logger.LogError("GetConnectedGrids", "Provided grid is null.");
                return new HashSet<IMyCubeGrid>(); // Return an empty set if grid is null
            }

            // Retrieve the grid group using the mechanical connection type
            var gridGroup = grid.GetGridGroup(type);
            if (gridGroup == null)
            {
                Logger.LogError("GetConnectedGrids", $"Grid group is null for grid: {grid.DisplayName}");
                return new HashSet<IMyCubeGrid>(); // Return an empty set if no group is found
            }

            // Collect all connected grids
            var connectedGrids = new HashSet<IMyCubeGrid>();
            gridGroup.GetGrids(connectedGrids);
            // Log the result and return
            Logger.Log("GetConnectedGrids",
                $"Found {connectedGrids.Count} connected grids for grid: {grid.DisplayName}");
            return connectedGrids;
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
        /// This method casts the provided <see cref="IMyCubeGrid"/> to <see cref="MyCubeGrid"/> to access the <c>BlocksCount</c> property. If the cast is not successful, it returns 0.
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
        /// <param name="grids">A collection of grids implementing <see cref="IEnumerable{IMyCubeGrid}"/>.
        /// The collection can be of any type, such as <see cref="List{IMyCubeGrid}"/>, <see cref="HashSet{IMyCubeGrid}"/>, or an array.
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