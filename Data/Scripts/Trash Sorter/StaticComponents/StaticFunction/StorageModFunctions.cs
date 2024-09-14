using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox.Game.EntityComponents;
using VRage.Game.ModAPI;

namespace Trash_Sorter.StaticComponents.StaticFunction
{
    public static class StorageModFunctions
    {
        public enum GridStorageResult : byte
        {
            Claimed = 0, // 1: Already owned by given Id
            Failed = 1, // 3: Owned by another id
        }

        /// <summary>
        /// Sets the storage value for the given mod GUID on the specified grid. If the grid storage does not exist, 
        /// it will initialize the storage and add the specified value.
        /// </summary>
        /// <param name="modGuid">The GUID for the mod or system setting the storage value.</param>
        /// <param name="grid">The grid on which to set or update the storage value.</param>
        /// <param name="managerId">The value to store in the grid's storage. This represents the manager ID.</param>
        /// <returns>
        /// A <see cref="GridStorageResult"/> indicating the result of the storage operation:
        /// <list type="bullet">
        /// <item><see cref="GridStorageResult.Claimed"/>: If the storage value was successfully set or added.</item>
        /// <item><see cref="GridStorageResult.Failed"/>: If the storage already contains a conflicting value for the given <paramref name="modGuid"/>.</item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// This method ensures that grid storage is initialized if it does not exist, and will attempt to store 
        /// the <paramref name="managerId"/> in the grid's storage. If the storage already contains a conflicting value, 
        /// the method will fail.
        /// </remarks>
        public static GridStorageResult SetGridStorageValue(Guid modGuid, IMyCubeGrid grid, string managerId)
        {
            // Ensure grid storage is initialized
            if (grid.Storage == null)
            {
                Logger.LogWarning("SetGridStorageValue", $"{grid.CustomName} storage is null, initializing storage.");
                grid.Storage = new MyModStorageComponent();
                grid.Storage.Add(modGuid, managerId);
                Logger.Log("SetGridStorageValue", $"Initialized and set storage value for {grid.CustomName} to '{managerId}'");
                return GridStorageResult.Claimed;
            }

            // Try to get the current value from storage
            string storedValue;
            if (grid.Storage.TryGetValue(modGuid, out storedValue))
            {
                // If storage has a value and it doesn't match managerId, return failure
                if (!string.IsNullOrEmpty(storedValue) && storedValue != managerId)
                {
                    return GridStorageResult.Failed;
                }
            }
            else
            {
                // If the modGuid is not in the storage, add the managerId
                grid.Storage.Add(modGuid, managerId);
                Logger.Log("SetGridStorageValue", $"Added storage value for {grid.CustomName} to '{managerId}'");
                return GridStorageResult.Claimed;
            }

            // Set the storage value to managerId if no issue
            grid.Storage.SetValue(modGuid, managerId);
            Logger.Log("SetGridStorageValue", $"Updated storage value for {grid.CustomName} to '{managerId}'");
            return GridStorageResult.Claimed;
        }

        /// <summary>
        /// Attempts to set the storage value of a grid for a specific manager identified by <paramref name="managerId"/>.
        /// If the grid storage is uninitialized, the method will initialize it and assign the new manager. 
        /// If the current stored manager differs from the expected one (identified by <paramref name="otherManagerId"/>), 
        /// the operation will fail.
        /// </summary>
        /// <param name="modGuid">The unique identifier (GUID) of the mod or system managing the grid storage.</param>
        /// <param name="grid">The grid object (<see cref="IMyCubeGrid"/>) whose storage is being modified.</param>
        /// <param name="managerId">The ID of the manager that is claiming or modifying the storage value of the grid.</param>
        /// <param name="otherManagerId">
        /// The ID of the current manager that is expected to be stored in the grid's storage. 
        /// If the stored value differs from this ID, the storage update will fail.
        /// </param>
        /// <returns>
        /// A <see cref="GridStorageResult"/> indicating the result of the storage update operation:
        /// <list type="bullet">
        /// <item><see cref="GridStorageResult.Claimed"/>: If the grid's storage was successfully updated or initialized with the new manager ID.</item>
        /// <item><see cref="GridStorageResult.Failed"/>: If the stored manager ID did not match <paramref name="otherManagerId"/> or the storage value retrieval failed.</item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// This method ensures that a grid's storage is claimed and managed by a single manager at a time. 
        /// If the grid storage is uninitialized, it is initialized with the provided <paramref name="managerId"/>. 
        /// If the storage already contains a manager ID and it does not match <paramref name="otherManagerId"/>, the update will fail.
        /// <para>
        /// <strong>Important:</strong> This method assumes that grid storage is persistent and linked to a unique <paramref name="modGuid"/>.
        /// Be sure to handle cases where storage could be missing or corrupted carefully to avoid unintended failures.
        /// </para>
        /// </remarks>
        public static GridStorageResult SetGridStorageValue(Guid modGuid, IMyCubeGrid grid, string managerId,
            string otherManagerId)
        {
            // Ensure grid storage is initialized
            if (grid.Storage == null)
            {
                Logger.LogWarning("SetGridStorageValue",
                    $"{grid.CustomName} storage is null, initializing storage.");
                grid.Storage = new MyModStorageComponent();

                // Assign the new manager as there's no previous storage
                grid.Storage.SetValue(modGuid, managerId);
                return GridStorageResult.Claimed;
            }

            // Try to get the current manager ID from storage
            string storedValue;
            if (!grid.Storage.TryGetValue(modGuid, out storedValue))
            {
                Logger.Log("SetGridStorageValue",
                    $"No existing value found for {grid.CustomName}, setting value for the first time.");

                // No stored value, so this is the first time setting it. Proceed to set the value.
                grid.Storage.SetValue(modGuid, managerId);
                return GridStorageResult.Claimed;
            }

            // Skip updating if the manager ID is already the same
            if (managerId == storedValue)
                return GridStorageResult.Claimed; // Grid already managed by this manager, skip it


            // Check if the current manager ID matches the one passed in
            if (storedValue != otherManagerId)
            {
                Logger.LogWarning("SetGridStorageValue", $"Cannot override grid storage for {grid.CustomName}, mismatching manager IDs. " +
                                                         $"Stored manager: {storedValue}, Expected: {otherManagerId}");
                return GridStorageResult.Failed; // The manager ID does not match, fail to claim
            }

            // Update the storage with the new manager ID
            Logger.Log("SetGridStorageValue",
                $"Overriding storage for {grid.CustomName}. Previous manager: {otherManagerId}, New manager: {managerId}");
            grid.Storage.SetValue(modGuid, managerId);

            return GridStorageResult.Claimed; // Success in updating the manager ID
        }

        /// <summary>
        /// Attempts to set the storage value for a collection of grids, ensuring that each grid is claimed by the provided manager ID.
        /// Grids are processed in a consistent order based on their <c>EntityId</c>.
        /// </summary>
        /// <param name="modGuid">The unique identifier (GUID) of the mod or system managing the grid storage.</param>
        /// <param name="grids">A <see cref="IEnumerable{IMyCubeGrid}"/> representing the collection of grids to be processed.</param>
        /// <param name="managerId">The ID of the manager that is claiming or modifying the storage value of the grids.</param>
        /// <param name="otherManagerId">
        /// The ID of the current manager that is expected to be stored in the grids' storage. 
        /// If the stored value differs from this ID, the storage update will fail.
        /// </param>
        /// <returns>
        /// A <see cref="GridStorageResult"/> indicating the result of the storage update operation:
        /// <list type="bullet">
        /// <item><see cref="GridStorageResult.ClaimedStorageNull"/>: If a grid's storage was uninitialized and has been successfully initialized with the new manager ID.</item>
        /// <item><see cref="GridStorageResult.Claimed"/>: If the grids' storage values were successfully updated with the new manager ID.</item>
        /// <item><see cref="GridStorageResult.Failed"/>: If the storage value retrieval failed or the stored manager ID did not match <paramref name="otherManagerId"/>.</item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// This method ensures that each grid in the <paramref name="grids"/> collection is claimed and managed by a single manager at a time. 
        /// If the grid storage is uninitialized, it is initialized with the provided <paramref name="managerId"/>. 
        /// If a grid's storage already contains a manager ID and it does not match <paramref name="otherManagerId"/>, the update will fail for that grid.
        /// <para>
        /// <strong>Important:</strong> The grids are processed in a consistent order based on their <c>EntityId</c> to ensure deterministic behavior. 
        /// If you are working with unordered collections like <see cref="HashSet{T}"/>, sorting by <c>EntityId</c> ensures the grids are processed in the same order during each invocation.
        /// </para>
        /// </remarks>
        public static GridStorageResult SetGridStorageValue(Guid modGuid, IEnumerable<IMyCubeGrid> grids, string managerId,
            string otherManagerId)
        {
            // Process the grids in a consistent order by EntityId and exit early if any grid fails.
            var failedGrid = grids.OrderBy(x => x.EntityId).FirstOrDefault(grid =>
                SetGridStorageValue(modGuid, grid, managerId, otherManagerId) == GridStorageResult.Failed);

            return failedGrid != null ? GridStorageResult.Failed : GridStorageResult.Claimed;
        }


        //Funny enough both function on the bottom are useless. Due to how OnClose works it will just make it a fight for who is owner every game restart

        /// <summary>
        /// Removes the storage entry for the given mod GUID on the specified grid by completely removing the entry.
        /// </summary>
        /// <param name="modGuid">The GUID for the mod or system managing the storage.</param>
        /// <param name="grid">The grid from which to remove the storage entry.</param>
        public static void RemoveGridStorageValue(Guid modGuid, IMyCubeGrid grid)
        {
            if (grid.Storage == null)
            {
                Logger.LogWarning("RemoveGridStorageValue", $"{grid.CustomName} storage is null, nothing to remove.");
                return;
            }

            // Attempt to remove the mod GUID entry from the storage
            if (!grid.Storage.ContainsKey(modGuid)) return;

            Logger.Log("RemoveGridStorageValue", $"Removing storage value for {grid.CustomName}");
            grid.Storage.Remove(modGuid);
        }


        /// <summary>
        /// Clears the storage value for the given mod GUID on the specified grid by setting it to an empty string.
        /// </summary>
        /// <param name="modGuid">The GUID for the mod or system managing the storage.</param>
        /// <param name="grid">The grid on which to clear the storage value.</param>
        public static void ClearGridStorageValue(Guid modGuid, IMyCubeGrid grid)
        {
            if (grid.Storage == null)
            {
                Logger.LogWarning("ClearGridStorageValue", $"{grid.CustomName} storage is null, initializing storage.");
                grid.Storage = new MyModStorageComponent();
            }

            // Set the value to an empty string
            Logger.Log("ClearGridStorageValue",
                $"Clearing storage value for {grid.CustomName} (setting to empty string)");
            grid.Storage.SetValue(modGuid, "");
        }
    }
}
