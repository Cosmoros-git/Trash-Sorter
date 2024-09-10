using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Trash_Sorter.Data.Scripts.Trash_Sorter.BaseClass;
using Trash_Sorter.Data.Scripts.Trash_Sorter.SessionComponent;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace Trash_Sorter.Data.Scripts.Trash_Sorter
{
    public class GridSystemOwnerV2 : ModBase
    {
        private readonly Guid Guid = new Guid("f6ea728c-8890-4012-8c81-165593a65b86");

        public readonly HashSet<IMyCubeGrid> ManagedGrids = new HashSet<IMyCubeGrid>();
        private readonly HashSet<IMyEntity> OtherManagerSubscribes = new HashSet<IMyEntity>();

        // TODO REWRITE THIS ENTIRE MESS, AS I FOR SEEN THIS IS BAD.


        /*
        Logic behind class
        UpdateOnceBeforeFrame -> Trying to initialize from start




         */


        // Dealing with manager subs.
        private IMyEntity _otherSystemManager;

        private IMyEntity OtherSystemManager
        {
            get { return _otherSystemManager; }
            set
            {
                _otherSystemManager = value;
                if (value != null)
                {
                    OtherSystemManagerId = value.EntityId;
                    OtherSystemManagerStringId = value.EntityId.ToString();
                }
                else
                {
                    OtherSystemManagerId = 0; // Reset to a default value when null
                    OtherSystemManagerStringId = string.Empty;
                }
            }
        }

        private long OtherSystemManagerId;
        private string OtherSystemManagerStringId;


        private IMyEntity _systemEntity;

        public IMyEntity SystemEntity
        {
            get { return _systemEntity; }
            set
            {
                _systemEntity = value;
                if (value != null)
                {
                    SystemManagerBlock = (IMyCubeBlock)value;
                    SystemManagerId = value.EntityId;
                    SystemManagerStringId = value.EntityId.ToString();
                }
                else
                {
                    SystemManagerBlock = null; // Reset to a default value when null
                    SystemManagerId = 0;
                    SystemManagerStringId = string.Empty;
                }
            }
        }

        public IMyCubeBlock SystemManagerBlock;
        public IMyCubeGrid SystemGrid;
        private long SystemManagerId;
        private string SystemManagerStringId;


        private bool IsOnline; // Initialization was successful 
        private bool IsOnStandBy; // There exist another manager and that one is managing


        private bool BasicLevelBlockVerified;
        private bool HasPhysicsErrorBeenShown;


        public GridSystemOwnerV2(IMyEntity systemEntity)
        {
            SystemEntity = systemEntity;
        }


        public void UpdateOnceBeforeFrame()
        {
            MyLog.Default.WriteLine("UpdateOnceBeforeFrame: Starting update.");

            // If the system is already online, skip further processing
            if (SystemOnline())
            {
                Logger.LogError(ClassName, "UpdateOnceBeforeFrame: System is already online, skipping.");
                return;
            }

            // Perform basic block verification
            if (!BasicLevelBlockVerified)
            {
                BasicBlockVerification();
                if (!BasicLevelBlockVerified)
                {
                    OnNeedsUpdate(MyEntityUpdateEnum.BEFORE_NEXT_FRAME);
                    return;
                }

                Logger.Log(ClassName, "Block has been verified, proceeding");
            }

            // Try to initialize the manager
            if (InitializeManager())
            {
                Logger.Log(ClassName,
                    "UpdateOnceBeforeFrame: Manager initialization successful. Starting regular updates.");

                // If the block was successfully verified, start regular updates
                Logger.Log(ClassName, "Trash Sorter startup finished");
                OnNeedsUpdate(MyEntityUpdateEnum.EACH_10TH_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME);
                return;
            }

            // If the block is not initialized but we're not in standby mode, retry next frame
            if (!IsOnStandBy)
            {
                Logger.LogError(ClassName,
                    "UpdateOnceBeforeFrame: Manager initialization failed. Retrying next frame.");
                OnNeedsUpdate(MyEntityUpdateEnum.BEFORE_NEXT_FRAME);
                return;
            }

            Logger.LogError(ClassName, "Trash Sorter could not start. Entering stand-by.");
            OnNeedsUpdate(MyEntityUpdateEnum.NONE);
            Dispose();
            Logger.LogError(ClassName, "UpdateOnceBeforeFrame: System disposed and now in standby mode.");
        }


        private bool SystemOnline()
        {
            if (!IsOnline) return false;

            // If the block is already online, continue with regular updates
            OnNeedsUpdate(MyEntityUpdateEnum.EACH_10TH_FRAME |
                          MyEntityUpdateEnum.EACH_100TH_FRAME);

            return true;
        }

        private int CurrentUpdate;
        private const int UpdateCooldown = 2000;

        // Basic level block checks.
        private void BasicBlockVerification()
        {
            Logger.Log(ClassName, "BasicBlockVerification: Starting check...");

            // Check if already verified
            if (BasicLevelBlockVerified)
            {
                Logger.Log(ClassName, "BasicBlockVerification: Block is already verified, skipping.");
                return; // It's already verified, no need to check again
            }

            // Check if the update should be skipped based on the cooldown

            if (CurrentUpdate % UpdateCooldown != 0) return;
            CurrentUpdate++;

            // Reset the update counter
            Logger.Log(ClassName, $"BasicBlockVerification: Cooldown reached trying again.");
            CurrentUpdate = 0;

            // Check if the SystemBlock and its CubeGrid are valid
            if (SystemManagerBlock?.CubeGrid == null)
            {
                var errorMessage = SystemManagerBlock == null
                    ? "SystemBlock is not a valid IMyCubeBlock."
                    : "CubeGrid is null.";
                Logger.LogError(ClassName, $"BasicBlockVerification: {errorMessage}");
                OnNeedsUpdate(MyEntityUpdateEnum.BEFORE_NEXT_FRAME);
                return;
            }

            // Check if the CubeGrid has physics enabled
            if (SystemManagerBlock.CubeGrid.Physics != null)
            {
                Logger.Log(ClassName, "BasicBlockVerification: Physics is enabled. Verification successful.");
                BasicLevelBlockVerified = true;
                return;
            }

            // Handle the case where physics is null and log only once
            if (HasPhysicsErrorBeenShown) return;


            Logger.LogError(ClassName, "BasicBlockVerification: Physics is null, waiting.");
            OnNeedsUpdate(MyEntityUpdateEnum.BEFORE_NEXT_FRAME);
            HasPhysicsErrorBeenShown = true;
        }


        private bool InitializeManager()
        {
            Logger.Log(ClassName, "Trash Sorter instance starting up");
            SystemEntity.OnMarkForClose += ManagerBlock_OnMarkForClose;

            // Check if GridManagement passes
            if (!GridManagerSetup()) return false;

            // If all checks pass, proceed with startup

            IsOnline = true; // Mark as online after successful verification
            OnNeedsUpdate(MyEntityUpdateEnum.BEFORE_NEXT_FRAME);
            Logger.Log(ClassName, "What are you doing in my swamp!?");
            return true;
        }

        private bool IsThisManager(int notManagedCount, int managedCount)
        {
            // If counts are not equal, return based on comparison
            // Otherwise, compare based on SystemManagerId and OtherSystemManagerId
            return notManagedCount != managedCount
                ? notManagedCount <= managedCount
                : SystemManagerId > OtherSystemManagerId;
        }


        private bool GridManagerSetup()
        {
            HashSet<IMyCubeGrid> managedGrids, notManagedGrids, connectedGrids;

            ProcessConnectedGrids(SystemManagerBlock.CubeGrid, out managedGrids, out notManagedGrids,
                out connectedGrids);
            if (notManagedGrids.Count > 0)
            {
                if (IsThisManager(notManagedGrids.Count, managedGrids.Count))
                {
                    SubscribeGrids(connectedGrids);
                    return true;
                }

                if (OtherSystemManager == null)
                {
                    SubscribeGrids(notManagedGrids);
                    return true;
                }

                UnsubscribeGrids(managedGrids);
                EnterStandbyMode();
                return false;
            }

            SubscribeGrids(connectedGrids);
            return true;
            // Process each grid in the connected system
        }

        private bool GridManagerExistsOn(IMyCubeGrid grid)
        {
            HashSet<IMyCubeGrid> connectedGrids;
            GetConnectedGrids(grid, out connectedGrids);
            var systemGrid = SystemManagerBlock.CubeGrid;

            return connectedGrids.Contains(systemGrid);
        }

        private void Grid_OnGridSplit(IMyCubeGrid arg1, IMyCubeGrid arg2)
        {
            HashSet<IMyCubeGrid> gridSet;
            var cubeGridRef = GridManagerExistsOn(arg2) ? arg2 : arg1;

            GetConnectedGrids(cubeGridRef, out gridSet);

            foreach (var grid in gridSet)
            {
                UnsubscribeGrid(grid);
            }
        }

        private void Grid_OnGridMerge(IMyCubeGrid arg1, IMyCubeGrid arg2)
        {
            HashSet<IMyCubeGrid> managedGrids, notManagedGrids, connectedGrids;

            // Process connected grids from the first grid (arg1)
            ProcessConnectedGrids(arg1, out managedGrids, out notManagedGrids, out connectedGrids);

            // Case 1: No other manager found, claim all grids
            if (notManagedGrids.Count == 0)
            {
                Logger.Log(ClassName, "Grid_OnGridMerge: No other manager found, claiming all grids.");
                SubscribeGrids(connectedGrids); // Subscribe to all connected grids
                return;
            }

            // Case 2: This system should remain the manager
            if (IsThisManager(notManagedGrids.Count, managedGrids.Count))
            {
                Logger.Log(ClassName, $"Grid_OnGridMerge: This grid remains the manager, claiming unmanaged grids.");
                SubscribeGrids(notManagedGrids);
                return;
            }

            // Case 3: The other system becomes the manager
            Logger.LogWarning(ClassName,
                $"Grid_OnGridMerge: New merged grid managed by {OtherSystemManager?.DisplayName} is larger, transferring control.");

            if (OtherSystemManager != null)
            {
                // Subscribe to the other manager's OnMarkForClose event
                SubscribeOtherManager(OtherSystemManager);
                Logger.Log(ClassName,
                    $"Grid_OnGridMerge: Subscribed to OnMarkForClose for {OtherSystemManager.DisplayName}");
            }
            else
            {
                // Error handling in case OtherSystemManager is unexpectedly null
                Logger.LogError(ClassName, "Critical error: OtherSystemManager is null.");
                UnsubscribeGrids(managedGrids); // Unsubscribe all managed grids
            }
        }


        private void ProcessConnectedGrids(IMyCubeGrid referenceGrid, out HashSet<IMyCubeGrid> managedGrids,
            out HashSet<IMyCubeGrid> notManagedGrids, out HashSet<IMyCubeGrid> connectedGrids)
        {
            managedGrids = new HashSet<IMyCubeGrid>();
            notManagedGrids = new HashSet<IMyCubeGrid>();

            // Get all connected grids
            GetConnectedGrids(referenceGrid, out connectedGrids);

            foreach (var grid in connectedGrids)
            {
                long gridManagerId;
                TryClaimingGrid(grid, out gridManagerId);

                // Case 1: No other manager yet, and the grid is managed by a different manager
                if (OtherSystemManager == null && gridManagerId != SystemManagerId)
                {
                    if (GetOtherManagerById(gridManagerId))
                    {
                        Logger.Log(ClassName, $"Found another system manager with ID: {gridManagerId}");
                    }

                    notManagedGrids.Add(grid);
                    continue;
                }

                // Case 2: Other manager exists, but grid is managed by a different manager
                if (OtherSystemManager != null && gridManagerId != SystemManagerId)
                {
                    if (OtherSystemManagerId != gridManagerId)
                    {
                        Logger.Log(ClassName,
                            $"Grid {grid.DisplayName} is managed by a different system. Entering standby mode.");
                        EnterStandbyMode();
                        return; // Exit early as we need to enter standby mode
                    }

                    // If the grid is managed by the same system as OtherSystemManager
                    notManagedGrids.Add(grid);
                }
                else
                {
                    // Case 3: Grid is managed by the current system
                    managedGrids.Add(grid);
                }
            }
        }


        private bool OverrideManagerBlock()
        {
            HashSet<IMyCubeGrid> notManagedGrids, managedGrids, connectedGrids;
            ProcessConnectedGrids(SystemGrid, out managedGrids, out notManagedGrids, out connectedGrids);

            Logger.Log(ClassName,
                $"{notManagedGrids.Count}/{managedGrids.Count} grids are being considered for override.");

            if (IsThisManager(notManagedGrids.Count, managedGrids.Count))
            {
                foreach (var grid in notManagedGrids)
                {
                    if (grid.Storage == null)
                    {
                        grid.Storage = new MyModStorageComponent();
                        grid.Storage.SetValue(Guid, SystemManagerStringId);
                        continue;
                    }

                    // Retrieve the current manager ID for this grid
                    string currentManagerStringId;
                    if (!grid.Storage.TryGetValue(Guid, out currentManagerStringId)) continue;

                    // If the stored manager ID is different from the one we expect
                    if (currentManagerStringId == OtherSystemManagerStringId) continue;

                    Logger.Log(ClassName,
                        $"{grid.DisplayName} managed by different system ({currentManagerStringId}). Entering standby.");
                    EnterStandbyMode();
                    UnsubscribeGrids(managedGrids);
                    return false; // Stop further processing as we're not the manager
                }

                // If no conflicts, subscribe to grids
                SubscribeGrids(notManagedGrids);
                return true;
            }

            EnterStandbyMode();
            UnsubscribeGrids(managedGrids);
            return false;
        }

        private void SubscribeOtherManager(IMyEntity otherManager)
        {
            if (otherManager == null) return;
            foreach (var manager in OtherManagerSubscribes.ToList())
            {
                if (manager.EntityId != otherManager.EntityId)
                {
                    manager.OnMarkForClose -= OtherSystemManager_OnMarkForClose;
                }
            }

            OtherManagerSubscribes.Add(otherManager);
            OtherSystemManager.OnMarkForClose += OtherSystemManager_OnMarkForClose;
        }


        private void TryClaimingGrid(IMyCubeGrid grid, out long storedId)
        {
            var storage = grid.Storage;
            storedId = 0;
            if (storage == null)
            {
                Logger.Log(ClassName, $"{grid.DisplayName} storage null grid marked as managed by this block");
                grid.Storage = new MyModStorageComponent();
                grid.Storage.SetValue(Guid, SystemManagerStringId);
                return;
            }

            string storedBlockId;
            string value;
            if (storage.TryGetValue(Guid, out value))
            {
                storedBlockId = value;
            }
            else
            {
                Logger.Log(ClassName, $"{grid.DisplayName} string parse failed, grid marked as managed by this block");
                storage.SetValue(Guid, SystemManagerStringId);
                return;
            }

            if (string.IsNullOrEmpty(storedBlockId))
            {
                Logger.Log(ClassName,
                    $"{grid.DisplayName}stored string is empty, grid marked as managed by this block");
                storage.SetValue(Guid, SystemManagerStringId);
                return;
            }

            if (storedBlockId == SystemManagerStringId)
            {
                Logger.Log(ClassName, $"{grid.DisplayName} blockId is equal, grid is managed by this block");
                return;
            }

            if (long.TryParse(storedBlockId, out storedId)) return;


            Logger.Log(ClassName, $"{grid.DisplayName} parse to long failed, grid marked as managed by this block");
            storage.SetValue(Guid, SystemManagerStringId);
        }

        private bool GetOtherManagerById(long entityId)
        {
            OtherSystemManager = MyAPIGateway.Entities.GetEntityById(entityId);
            return OtherSystemManager != null;
        }

        private void EnterStandbyMode()
        {
            IsOnStandBy = true;
            SubscribeOtherManager(OtherSystemManager);
            OnNeedsUpdate(MyEntityUpdateEnum.NONE);
            if (!IsOnline) return;

            Dispose(); // Clean up resources if online
            IsOnline = false;
        }


        private void UnsubscribeGrids(HashSet<IMyCubeGrid> grids)
        {
            foreach (var grid in grids)
            {
                UnsubscribeGrid(grid);
            }
        }

        private void SubscribeGrids(HashSet<IMyCubeGrid> grids)
        {
            foreach (var grid in grids)
            {
                SubscribeGrid(grid);
            }
        }


        private void UnsubscribeGrid(IMyCubeGrid grid)
        {
            if (grid == null) return;
            if (!ManagedGrids.Contains(grid)) return;

            grid.OnClosing -= Grid_OnClosing;
            grid.OnGridSplit -= Grid_OnGridSplit;
            grid.OnGridMerge -= Grid_OnGridMerge;
            grid.Storage?.RemoveValue(Guid);
            ManagedGrids.Remove(grid);
            OnGridDispose(grid);
        }

        private void SubscribeGrid(IMyCubeGrid grid)
        {
            if (grid == null) return;
            if (ManagedGrids.Contains(grid)) return;

            grid.OnClosing += Grid_OnClosing;
            grid.OnGridSplit += Grid_OnGridSplit;
            grid.OnGridMerge += Grid_OnGridMerge;
            if (grid.Storage == null)
            {
                grid.Storage = new MyModStorageComponent();
            }
            grid.Storage.SetValue(Guid, SystemManagerStringId);
            ManagedGrids.Add(grid);
            OnGridAdded(grid);
        }


        private void ManagerBlock_OnMarkForClose(IMyEntity obj)
        {
            if (obj == null) return;
            // Moved entire dispose into earlier method to be sure it does its job.
            obj.OnClosing -= ManagerBlock_OnMarkForClose;

            foreach (var grid in ManagedGrids.ToList())
            {
                var storage = grid.Storage;
                string value;
                if (!storage.TryGetValue(Guid, out value)) continue;
                if (value != SystemManagerStringId) continue;
                storage.SetValue(Guid, "");
            }

            OnNeedsUpdate(MyEntityUpdateEnum.NONE);
            Dispose();
        }

        private void OtherSystemManager_OnMarkForClose(IMyEntity obj)
        {
            IsOnStandBy = false;
            if (obj == null)
            {
                Logger.LogError(ClassName,
                    "This is really bad. Manager block is null. We are having a phantom block situation.");
                InitializeManager();
            }
            else
            {
                if (OtherManagerSubscribes.Contains(obj))
                {
                    obj.OnClose -= OtherSystemManager_OnMarkForClose;
                    OtherManagerSubscribes.Remove(obj);
                }
            }

            if (OverrideManagerBlock()) return;
            EnterStandbyMode();
            Logger.LogError(ClassName, "Trash Sorter could not start. Entering standby.");
        }


        private void Grid_OnClosing(IMyEntity obj)
        {
            var grid = obj as IMyCubeGrid;
            if (grid == null) return;
            UnsubscribeGrid(grid);
            OnGridDispose(grid);
        }

        public override void Dispose()
        {
            base.Dispose();
            OnDisposeInvoke();
            foreach (var grid in ManagedGrids.ToList())
            {
                Grid_OnClosing(grid);
            }

            foreach (var manager in OtherManagerSubscribes.ToList())
            {
                if (manager != null) manager.OnMarkForClose -= OtherSystemManager_OnMarkForClose;
            }

            SystemEntity = null;
            OtherSystemManager = null;
        }
    }
}