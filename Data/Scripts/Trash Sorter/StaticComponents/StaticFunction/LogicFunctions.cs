using Sandbox.ModAPI;
using VRage.ModAPI;

namespace Trash_Sorter.StaticComponents.StaticFunction
{
    public static class LogicFunctions
    {
        public enum OwnerResult
        {
            Owner = 0,
            NotOwner = 1,
            NullError = 2
        }

        /// <summary>
        /// Just custom reference to get entity by id.
        /// </summary>
        /// <param name="entityId"></param>
        /// <param name="entity"></param>
        /// <returns></returns>
        public static bool GetEntityById(long entityId, out IMyEntity entity)
        {
            entity = MyAPIGateway.Entities.GetEntityById(entityId);
            return entity != null;
        }

        /// <summary>
        /// Determines ownership between two entity managers based on their EntityId and managed counts.
        /// </summary>
        /// <param name="notManagedCount">The count of entities not managed by this manager.</param>
        /// <param name="managedCount">The count of entities managed by this manager.</param>
        /// <param name="thisManager">The current entity manager being evaluated for ownership.</param>
        /// <param name="otherManager">Another entity manager to compare with the current manager.</param>
        /// <returns>
        /// A <see cref="OwnerResult"/> indicating ownership status:
        /// <list type="bullet">
        /// <item><see cref="OwnerResult.Owner"/>: If this manager is the owner or the other manager is null.</item>
        /// <item><see cref="OwnerResult.NotOwner"/>: If another manager is determined to be the owner based on counts or EntityId.</item>
        /// <item><see cref="OwnerResult.NullError"/>: If this manager is null.</item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// Ownership is based on comparing entity counts or the EntityId when counts are equal.
        /// </remarks>
        public static OwnerResult IsThisManager(int notManagedCount, int managedCount, IMyEntity thisManager,
            IMyEntity otherManager)
        {
            // If thisManager is null, return NullError
            if (thisManager == null)
            {
                return OwnerResult.NullError;
            }

            // If otherManager is null, return Owner because thisManager is non-null
            if (otherManager == null)
            {
                return OwnerResult.Owner;
            }

            // Check if managedCount and notManagedCount differ
            if (notManagedCount != managedCount)
            {
                return notManagedCount <= managedCount ? OwnerResult.Owner : OwnerResult.NotOwner;
            }

            // Compare EntityId values if counts are the same
            return thisManager.EntityId > otherManager.EntityId ? OwnerResult.Owner : OwnerResult.NotOwner;
        }

        /// <summary>
        /// Determines ownership based on a string representation of the other manager's EntityId.
        /// </summary>
        /// <param name="notManagedCount">The count of entities not managed by this manager.</param>
        /// <param name="managedCount">The count of entities managed by this manager.</param>
        /// <param name="thisManager">The current entity manager being evaluated for ownership.</param>
        /// <param name="otherManager">A string representing the EntityId of the other manager.</param>
        /// <returns>
        /// A <see cref="OwnerResult"/> indicating ownership status:
        /// <list type="bullet">
        /// <item><see cref="OwnerResult.Owner"/>: If this manager is the owner or if the other manager's EntityId cannot be parsed.</item>
        /// <item><see cref="OwnerResult.NotOwner"/>: If another manager is determined to be the owner based on counts or EntityId.</item>
        /// <item><see cref="OwnerResult.NullError"/>: If this manager is null.</item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// The string is parsed into a long value. If parsing fails, the current manager is considered the owner.
        /// </remarks>
        public static OwnerResult IsThisManager(int notManagedCount, int managedCount, IMyEntity thisManager, string otherManager)
        {
            // If thisManager is null, return NullError
            if (thisManager == null) return OwnerResult.NullError;

            // If otherManager is null, return NotOwner
            if (otherManager == null) return OwnerResult.NotOwner;

            // Try parsing the otherManager string to a long (EntityId)
            long entityId;
            return !long.TryParse(otherManager, out entityId) ?
                // If parsing fails, manager is considered owner. In theory it should never fail.
                OwnerResult.Owner :
                // Look up the entity by its ID and check ownership
                IsThisManagerById(notManagedCount, managedCount, thisManager, entityId);
        }

        /// <summary>
        /// Determines ownership based on a long representation of the other manager's EntityId.
        /// </summary>
        /// <param name="notManagedCount">The count of entities not managed by this manager.</param>
        /// <param name="managedCount">The count of entities managed by this manager.</param>
        /// <param name="thisManager">The current entity manager being evaluated for ownership.</param>
        /// <param name="otherManager">A long representing the EntityId of the other manager.</param>
        /// <returns>
        /// A <see cref="OwnerResult"/> indicating ownership status:
        /// <list type="bullet">
        /// <item><see cref="OwnerResult.Owner"/>: If this manager is the owner or if the other manager's EntityId is invalid.</item>
        /// <item><see cref="OwnerResult.NotOwner"/>: If another manager is determined to be the owner based on counts or EntityId.</item>
        /// <item><see cref="OwnerResult.NullError"/>: If this manager is null.</item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// The other manager is looked up using the long EntityId, and ownership is determined accordingly.
        /// </remarks>
        public static OwnerResult IsThisManager(int notManagedCount, int managedCount, IMyEntity thisManager,
            long otherManager)
        {
            // If thisManager is null, return NullError
            return thisManager == null
                ? OwnerResult.NullError
                :
                // Look up the entity by its ID
                IsThisManagerById(notManagedCount, managedCount, thisManager, otherManager);
        }

        // Helper function to handle Entity ID lookups
        private static OwnerResult IsThisManagerById(int notManagedCount, int managedCount, IMyEntity thisManager,
            long entityId)
        {
            // If the entity is not found by ID, consider thisManager as the owner
            IMyEntity otherManager;
            return !GetEntityById(entityId, out otherManager)
                ? OwnerResult.Owner
                :
                // Defer to the primary IsThisManager method using the actual IMyEntity
                IsThisManager(notManagedCount, managedCount, thisManager, otherManager);
        }
    }
}