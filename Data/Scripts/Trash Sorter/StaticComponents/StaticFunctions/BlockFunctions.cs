using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Trash_Sorter.StaticComponents.StaticFunctions
{
    public static class BlockFunctions
    {
        /// <summary>
        /// Represents the result of verifying the initialization and state of an entity in the game.
        /// This enum provides details on whether the entity and its associated components (grid and physics) are properly initialized.
        /// </summary>
        public enum EntityVerificationResult : byte
        {
            /// <summary>
            /// The verification was successful. The entity, its grid, and physics are all properly initialized.
            /// </summary>
            Success = 0,

            /// <summary>
            /// The entity is null, meaning it has not been initialized or does not exist.
            /// </summary>
            EntityNull = 1,

            /// <summary>
            /// The entity exists, but its associated grid is null, meaning the grid has not been initialized or is missing.
            /// </summary>
            GridNull = 2,

            /// <summary>
            /// The grid exists, but its physics are null, meaning that the grid cannot interact with the game world physically.
            /// </summary>
            PhysicsNull = 3
        }


        /// <summary>
        /// Performs a basic verification check on a block entity to ensure it is properly initialized.
        /// This method verifies that the provided entity exists, has an associated grid, and that the grid has physics enabled.
        /// </summary>
        /// <param name="checkedEntity">The entity to verify. This should be a block entity derived from <see cref="IMyEntity"/>.</param>
        /// <returns>
        /// A <see cref="EntityVerificationResult"/> enum indicating the result of the verification:
        /// - <c>Success</c>: The entity, grid, and physics are all initialized correctly.
        /// - <c>EntityNull</c>: The entity is null.
        /// - <c>GridNull</c>: The entity exists but the associated grid is null.
        /// - <c>PhysicsNull</c>: The grid exists but physics are not enabled for it.
        /// </returns>
        /// <example>
        /// Example usage:
        /// <code>
        /// var verificationResult = BasicBlockVerification(myBlockEntity);
        /// if (verificationResult == EntityVerificationResult.Success)
        /// {
        ///     Logger.Log("Block entity verification passed.");
        /// }
        /// else if (verificationResult == EntityVerificationResult.EntityNull)
        /// {
        ///     Logger.LogError("The provided entity is null.");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method is useful for ensuring that a block entity has been properly initialized and that its grid is functioning with physics enabled. 
        /// It can help identify issues related to null entities, uninitialized grids, or missing physics.
        /// </remarks>
        public static EntityVerificationResult BasicBlockVerification(IMyEntity checkedEntity)
        {
            Logger.Log("BasicBlockVerification", "BasicBlockVerification: Starting check...");

            // Check if the entity is null
            if (checkedEntity == null)
            {
                Logger.LogError("BasicBlockVerification", "Entity is null");
                return EntityVerificationResult.EntityNull; // Error code 1: Entity is null
            }

            // Get the cube grid from the entity
            IMyCubeGrid grid = ((MyCubeBlock)checkedEntity)?.CubeGrid;
            if (grid == null)
            {
                Logger.LogError("BasicBlockVerification", $"Grid is null. Block Id: {checkedEntity?.EntityId}");
                return EntityVerificationResult.GridNull; // Error code 2: Grid is null
            }

            // Check if grid has physics enabled
            if (grid.Physics == null)
            {
                Logger.Log("BasicBlockVerification", $"Physics are null. Block Id: {checkedEntity?.EntityId}");
                return EntityVerificationResult.PhysicsNull; // Error code 3: Physics are null
            }

            // If all checks passed, return success
            Logger.Log("BasicBlockVerification", $"Verification successful. Block Id: {checkedEntity?.EntityId}");
            return EntityVerificationResult.Success; // Success code 0
        }


        /// <summary>
        /// Determines whether the specified terminal block can use the conveyor system.
        /// The conveyor system is typically usable by cargo containers, connectors, production blocks, and other related blocks that interact with inventory systems.
        /// </summary>
        /// <param name="block">The terminal block to check. Must implement <see cref="IMyTerminalBlock"/>.</param>
        /// <returns>
        /// Returns <c>true</c> if the block can use the conveyor system; otherwise, returns <c>false</c>.
        /// A block is considered able to use the conveyor system if it implements any of the following interfaces:
        /// <para>- <see cref="IMyCargoContainer"/></para>
        /// <para>- <see cref="IMyShipConnector"/></para>
        /// <para>- <see cref="IMyProductionBlock"/></para>
        /// <para>- <see cref="IMyConveyorSorter"/></para>
        /// <para>- <see cref="IMyCollector"/></para>
        /// <para>- <see cref="IMyShipDrill"/></para>
        /// <para>- <see cref="IMyShipGrinder"/></para>
        /// <para>- <see cref="IMyShipWelder"/></para>
        /// <para>- <see cref="IMyReactor"/></para>
        /// <para>- <see cref="IMyGasTank"/></para>
        /// <para>- <see cref="IMyGasGenerator"/></para>
        /// <para>- <see cref="IMyPowerProducer"/></para>
        /// </returns>
        /// <example>
        /// Example usage:
        /// <code>
        /// IMyTerminalBlock block = GridTerminalSystem.GetBlockWithName("CargoContainer");
        /// if (CanUseConveyorSystem(block))
        /// {
        ///     Logger.Log("Block can use conveyor system.");
        /// }
        /// else
        /// {
        ///     Logger.Log("Block cannot use conveyor system.");
        /// }
        /// </code>
        /// </example>
        /// <remarks>
        /// This method is useful when you need to check if a block can participate in inventory transfers through the conveyor system.
        /// Blocks such as cargo containers, connectors, and production blocks are the primary candidates for using conveyors.
        /// </remarks>
        public static bool CanUseConveyorSystem(IMyTerminalBlock block)
        {
            return (block is IMyCargoContainer ||
                    block is IMyShipConnector ||
                    block is IMyProductionBlock ||
                    block is IMyConveyorSorter ||
                    block is IMyCollector ||
                    block is IMyShipDrill ||
                    block is IMyShipGrinder ||
                    block is IMyShipWelder ||
                    block is IMyReactor ||
                    block is IMyGasTank ||
                    block is IMyGasGenerator ||
                    block is IMyPowerProducer);
        }

        
    }
}