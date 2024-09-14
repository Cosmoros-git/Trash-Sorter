using System;
using System.Collections.Generic;
using System.Linq;
using Trash_Sorter.BaseClass;
using Trash_Sorter.StaticComponents;
using Trash_Sorter.StorageClasses;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Trash_Sorter.GridManagers
{
    public class GridManager : GridManagement
    {
        public GridStorage GridStorage = new GridStorage();

        private bool ValueSet;
        private readonly IMyEntity systemEntity;

        public GridManager(IMyEntity systemEntity)
        {
            this.systemEntity = systemEntity;
        }


        public void UpdateOnceBeforeFrame()
        {
            if (!ValueSet)
            {
                ValueSet = true;
                GridStorage.ThisManager.SetValue(systemEntity);
            }

            Logger.Log(ClassName, "Starting initialization.");


            // If the system is already online, skip further processing
            if (SystemOnline())
            {
                Logger.LogError(ClassName, " System is already online, skipping.");
                return;
            }

            // Perform basic block verification
            if (!GridStorage.BasicLevelBlockVerified)
            {
                if (BasicBlockVerification())
                {
                    Logger.Log(ClassName, "Block has been verified, proceeding");
                }
                else
                {
                    OnUpdateRequired(MyEntityUpdateEnum.BEFORE_NEXT_FRAME);
                    return;
                }
            }

            // Try to initialize the manager
            if (InitializeManager())
            {
                Logger.Log(ClassName, "Manager initialization successful. Starting regular updates.");
                Logger.Log(ClassName, "Trash Sorter startup finished");
                OnUpdateRequired(MyEntityUpdateEnum.EACH_10TH_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME);
                return;
            }

            // If grid is below minimum size limit of a grid
            if (GridStorage.GridTooSmallError)
            {
                OnEnterStandBy(SizeIssue);
                return;
            }

            // If the block is not initialized but we're not in standby mode, retry next frame
            if (!GridStorage.IsOnStandBy)
            {
                Logger.LogError(ClassName,
                    "UpdateOnceBeforeFrame: Manager initialization failed. Retrying next frame.");
                OnUpdateRequired(MyEntityUpdateEnum.BEFORE_NEXT_FRAME);
                return;
            }

            // Making sure that I won't dispose of the data for no reason.
            if (GridStorage.GridTooSmallError || !GridStorage.IsOnStandBy) return;

            Logger.LogError(ClassName, "Trash Sorter could not start. Entering stand-by.");
            OnUpdateRequired(MyEntityUpdateEnum.NONE);
        }


        private bool SystemOnline()
        {
            if (!GridStorage.IsOnline) return false;

            // If the block is already online, continue with regular updates
            OnUpdateRequired(MyEntityUpdateEnum.EACH_10TH_FRAME |
                             MyEntityUpdateEnum.EACH_100TH_FRAME);

            return true;
        }

        private int CurrentUpdate;

        private bool BasicBlockVerification()
        {
            Logger.Log(ClassName, "BasicBlockVerification: Starting check...");

            // Check if already verified
            if (GridStorage.BasicLevelBlockVerified)
            {
                Logger.Log(ClassName, "BasicBlockVerification: Block is already verified, skipping.");
                return true; // It's already verified, no need to check again
            }

            // Check if the update should be skipped based on the cooldown

            if (CurrentUpdate % GridStorage.MinTimeBetweenActivations != 0) return false;
            CurrentUpdate++;

            // Reset the update counter
            Logger.Log(ClassName, $"BasicBlockVerification: Cooldown reached trying again.");
            CurrentUpdate = 0;

            // Check if the SystemBlock and its CubeGrid are valid
            if (GridStorage.ThisManager?.CubeGridI == null)
            {
                var errorMessage = GridStorage.ThisManager?.CubeGridI == null
                    ? "SystemBlock is not a valid IMyCubeBlock."
                    : "CubeGrid is null.";
                Logger.LogError(ClassName, $"BasicBlockVerification: {errorMessage}");
                OnUpdateRequired(MyEntityUpdateEnum.BEFORE_NEXT_FRAME);
                return false;
            }

            // Check if the CubeGrid has physics enabled
            GridStorage.ThisManager.ForceReferenceUpdates();

            // Check if the CubeGrid has physics enabled
            if (GridStorage.ThisManager.CubeGridI.Physics != null)
            {
                Logger.Log(ClassName, "BasicBlockVerification: Physics is enabled. Basic Verification successful.");
                GridStorage.BasicLevelBlockVerified = true;
                GridStorage.ThisManager.ForceUpdateHooks(); // Update any hooks if necessary
                return true;
            }


            // Handle the case where physics is null and log only once
            if (GridStorage.HasPhysicsErrorBeenShown) return false;


            Logger.LogError(ClassName, "BasicBlockVerification: Physics is null, waiting.");
            OnUpdateRequired(MyEntityUpdateEnum.BEFORE_NEXT_FRAME);
            GridStorage.HasPhysicsErrorBeenShown = true;
            return false;
        } // Done


        private bool InitializeManager()
        {
            Logger.Log(ClassName, "Trash Sorter instance starting up");
            GridStorage.ThisManager.OnBlockClosed += OnDisposeRequiredHandler;

            // Check if GridManagement passes
            if (!GridManagerSetup()) return false;

            // If all checks pass, proceed with startup

            GridStorage.IsOnline = true; // Mark as online after successful verification
            Logger.Log(ClassName, "What are you doing in my swamp!?");
            return true;
        }

        private bool GridManagerSetup()
        {
            HashSet<IMyCubeGrid> managedGrids, notManagedGrids, connectedGrids;

            Logger.Log(ClassName, $"GridManagerSetup: Starting grid manager setup. Manager Id:{GridStorage.ThisManager.IdString}");

            // Check if the main manager's CubeGrid is valid
            if (GridStorage.ThisManager.CubeGridI == null)
            {
                Logger.LogError(ClassName, "GridManagerSetup: CubeGridI is null. Aborting setup.");
                return false;
            }

            Logger.Log(ClassName, $"GridManagerSetup: CubeGridI found. Grid ID: {GridStorage.ThisManager.CubeGridI.EntityId}");

            // Process connected grids and categorize them
            Logger.Log(ClassName, "GridManagerSetup: Processing connected grids...");
            ProcessConnectedGrids(GridStorage.ThisManager.CubeGridI, out managedGrids, out notManagedGrids, out connectedGrids,
                GridStorage.OtherManager, GridStorage.ThisManager);

            Logger.Log(ClassName, $"GridManagerSetup: Managed grids count: {managedGrids.Count}, Not managed grids count: {notManagedGrids.Count}, Connected grids count: {connectedGrids.Count}");

            // Handle not managed grids
            if (notManagedGrids.Count > 0)
            {
                Logger.Log(ClassName, "GridManagerSetup: Handling not managed grids...");

                // If there's no other manager, subscribe to the not managed grids
                if (GridStorage.OtherManager.EntityI == null)
                {
                    Logger.Log(ClassName, "GridManagerSetup: No other manager detected. Subscribing to not managed grids.");
                    foreach (var grid in notManagedGrids)
                    {
                        OnGridAdded(grid);
                    }
                    return true;
                }

                // If there's another manager, try overriding the manager block
                Logger.Log(ClassName, "GridManagerSetup: Another manager detected. Attempting to override manager block...");
                if (OverrideManagerBlock(notManagedGrids, managedGrids))
                {
                    Logger.Log(ClassName, "GridManagerSetup: Manager block successfully overridden.");
                    return true;
                }

                // If the override fails, unsubscribe from managed grids and enter standby
                Logger.LogError(ClassName,
                    "GridManagerSetup: Manager block override failed. Unsubscribing from managed grids and entering standby.");
                foreach (var grid in managedGrids)
                {
                    OnGridRemoved(grid);
                }
                GridStorage.IsOnStandBy = true;
                OnEnterStandBy(Conflict);
                return false;
            }

            // Check if the grid consists of only the manager and is too small
            if (connectedGrids.Count == 1 && GridConsistsOfOnlyManager())
            {
                Logger.LogError(ClassName, "GridManagerSetup: Grid consists of only the manager and is too small. Entering standby mode.");
                GridStorage.GridTooSmallError = true;
                OnEnterStandBy(SizeIssue);
                return false;
            }

            // Subscribe to all connected grids
            Logger.Log(ClassName, "GridManagerSetup: Subscribing to all connected grids...");
            foreach (var myCubeGrid in connectedGrids) OnGridAdded(myCubeGrid);

            Logger.Log(ClassName, "GridManagerSetup: Grid setup completed successfully.");
            return true;
        }


        private bool IsSubscribedToBlocks;
        private int blockCount;

        internal bool GridConsistsOfOnlyManager()
        {
            try
            {
                // Ensure that the CubeGrid is not null
                if (GridStorage.ThisManager.CubeGridI == null)
                {
                    Logger.LogError(ClassName, "GridConsistsOfOnlyManager: CubeGrid is null.");
                    return false;
                }

                // Get the block count for the grid
                blockCount = GridStorage.ThisManager.CubeGridObj.BlocksCount;

                // Check if the grid has reached or exceeded the minimum block count for activation
                if (blockCount >= GridStorage.MinAmountOfBlocks)
                {
                    // If already subscribed to blocks, no need to add another event handler
                    if (IsSubscribedToBlocks)
                    {
                        return true;
                    }

                    // Unsubscribe the event if it is already subscribed
                    GridStorage.ThisManager.CubeGridI.OnBlockAdded -= OnBlockAddedSizeConflictOnly;

                    Logger.Log(ClassName,
                        $"Grid reached the block limit. Activating manager {GridStorage.ThisManager.IdString} on grid {GridStorage.ThisManager.CubeGridI.CustomName}.");
                    IsSubscribedToBlocks = false;
                    return true;
                }

                // If block count is lower than required, subscribe to future block additions if not already subscribed
                if (!IsSubscribedToBlocks)
                {
                    Logger.Log(ClassName,
                        $"Grid is at {blockCount}/{GridStorage.MinAmountOfBlocks} blocks. Waiting for more blocks.");
                    GridStorage.ThisManager.CubeGridI.OnBlockAdded += OnBlockAddedSizeConflictOnly;
                    IsSubscribedToBlocks = true;
                }

                // If the grid is on standby, handle that case
                if (!GridStorage.IsOnStandBy) return false;

                GridStorage.IsOnStandBy = false;
                GridStorage.BasicLevelBlockVerified = false;
                OnUpdateRequired(MyEntityUpdateEnum.BEFORE_NEXT_FRAME);

                return false;
            }
            catch (Exception ex)
            {
                Logger.LogError(ClassName, $"Error in GridConsistsOfOnlyManager: {ex}");
                return false;
            }
        }


        private void OnBlockAddedSizeConflictOnly(IMySlimBlock _)
        {
            try
            {
                blockCount++;
                Logger.LogError(ClassName,
                    $"Grid is at {blockCount}/{GridStorage.MinAmountOfBlocks} needed for activation.");
                if (IsSubscribedToBlocks && blockCount < GridStorage.MinAmountOfBlocks) return;
                GridConsistsOfOnlyManager();
            }
            catch (Exception ex)
            {
                Logger.LogError(ClassName, $"Grid Consists of only manager function error {ex}");
            }
        }

        internal bool OverrideManagerBlock(HashSet<IMyCubeGrid> notManagedGrids, HashSet<IMyCubeGrid> managedGrids)
        {
            Logger.Log(ClassName,
                $"{notManagedGrids.Count}/{managedGrids.Count} grids are being considered for override.");

            if (IsThisManager(notManagedGrids.Count, managedGrids.Count, GridStorage.ThisManager, GridStorage.OtherManager))
            {
                foreach (var grid in notManagedGrids)
                {
                    // Retrieve the current manager ID for this grid
                    string currentManagerStringId;
                    if (!grid.Storage.TryGetValue(ModGuid, out currentManagerStringId)) continue;

                    // If the stored manager ID is different from the one we expect
                    if (currentManagerStringId == GridStorage.OtherManager.IdString) continue;

                    Logger.Log(ClassName, $"{grid.DisplayName} managed by different system ({currentManagerStringId}). Entering standby.");
                    OnEnterStandBy(Conflict);

                    return false; // Stop further processing as we're not the manager
                }

                // If no conflicts, subscribe to grids
                foreach (var grid in notManagedGrids)
                {
                    OnGridOverriden(grid);
                }
                return true;
            }
            return false;
        }

        private void Grid_OnClosing(IMyEntity obj)
        {
            var grid = obj as IMyCubeGrid;
            if (grid == null) return;
            OnGridRemoved(grid);
        }

        public override void Dispose()
        {
            base.Dispose();
            GridStorage.ThisManager.OnBlockClosed -= OnDisposeRequiredHandler;

            foreach (var grid in GridStorage.ManagedGrids.ToArray())
            {
                Grid_OnClosing(grid);
            }
        }
    }
}