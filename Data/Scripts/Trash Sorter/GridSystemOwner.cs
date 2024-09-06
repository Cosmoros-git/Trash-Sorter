using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Trash_Sorter.Data.Scripts.Trash_Sorter.BaseClass;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;

namespace Trash_Sorter.Data.Scripts.Trash_Sorter
{
    public class GridSystemOwner : ModBase
    {
        private readonly Guid Guid = new Guid("f6ea728c-8890-4012-8c81-165593a65b86");
        public readonly HashSet<IMyCubeGrid> ConnectedToSystemGrid = new HashSet<IMyCubeGrid>();
        private readonly HashSet<IMyCubeGrid> NewConnectedToSystemGrids = new HashSet<IMyCubeGrid>();


        private bool IsOnline;
        private bool IsOnStandBy;
        private bool IsOtherManagerGone;
        private string OtherManagerId;
        private readonly string SystemBlockId;

        // ReSharper disable once NotAccessedField.Local
        public Logger Logger;


        public IMyEntity SystemEntity;
        public IMyCubeBlock SystemBlock;
        public IMyCubeGrid SystemGrid;

        public GridSystemOwner(IMyEntity systemEntity)
        {
            SystemEntity = systemEntity;
            SystemBlock = (IMyCubeBlock)SystemEntity;
            SystemGrid = SystemBlock.CubeGrid;
            SystemBlockId = SystemBlock.EntityId.ToString();
        }

        public void UpdateOnceBeforeFrame()
        {
            if (IsOnline)
            {
                // If the block is already online, continue with regular updates
                OnNeedsUpdate(MyEntityUpdateEnum.EACH_10TH_FRAME |
                              MyEntityUpdateEnum.EACH_100TH_FRAME);
                return;
            }

            if (VerifyBlock())
            {
                // If the block was successfully verified, start regular updates
                MyLog.Default.WriteLine("Trash Sorter startup finished");
                OnNeedsUpdate(MyEntityUpdateEnum.EACH_10TH_FRAME |
                              MyEntityUpdateEnum.EACH_100TH_FRAME);
                return;
            }

            if (!IsOnStandBy)
            {
                // If the block is not verified, and we're not on standby, retry next frame
                OnNeedsUpdate(MyEntityUpdateEnum.BEFORE_NEXT_FRAME);
                return;
            }

            // If we fail to verify after retrying, enter standby mode
            MyLog.Default.WriteLine("Trash Sorter could not start. Entering standby.");
            Dispose();
            OnNeedsUpdate(MyEntityUpdateEnum.NONE);
        }

        private bool VerifyBlock()
        {
            if (IsOnline) return true; // Early exit if already online
            if (IsOtherManagerGone) return true; // Removed extra checks on load from standby

            MyLog.Default.WriteLine("Trash Sorter starting up");

            var block = SystemBlock;
            if (block == null)
            {
                MyLog.Default.WriteLine("Entity is not a valid IMyCubeBlock.");
                return false;
            }

            // Check if the block's grid is valid
            if (block.CubeGrid == null)
            {
                MyLog.Default.WriteLine("CubeGrid is null.");
                return false;
            }

            // Check if the block has physics enabled
            if (block.CubeGrid.Physics == null)
            {
                MyLog.Default.WriteLine("Physics is null.");
                return false;
            }

            // Check if GridManagement passes
            if (!GridManagement(block))
            {
                MyLog.Default.WriteLine("GridManagement failed.");
                IsOnStandBy = true;
                return false;
            }

            // If all checks pass, proceed with startup
            Logger = new Logger(block.EntityId.ToString());
            block.OnMarkForClose += Block_OnMarkForClose;
            SubscribeForGridChanges();
            IsOnline = true; // Mark as online after successful verification
            OnNeedsUpdate(MyEntityUpdateEnum.BEFORE_NEXT_FRAME);
            Logger.Log(ClassName, "Wtf is going onn here?");
            return true;
        }

        private void SubscribeForGridChanges()
        {
            foreach (var grid in ConnectedToSystemGrid)
            {
                grid.OnGridMerge += Grid_OnGridMerge;
                grid.OnGridSplit += Grid_OnGridSplit;
            }
        }

        private void Grid_OnGridSplit(IMyCubeGrid arg1, IMyCubeGrid arg2)
        {
            IMyEntity otherManager;
            HashSet<IMyCubeGrid> managedGrids, notManagedGrids;

            // SystemGrid is set to the part of the grid that remains part of this system
            SystemGrid = SystemBlock.CubeGrid;

            // Process the connected grids after the split
            ProcessConnectedGrids(out managedGrids, out notManagedGrids, out otherManager);

            // If no other manager is found, no conflict
            if (notManagedGrids.Count <= 0) return;

            // Check if the new split grid is larger than the old
            if (IsNewGridLargerThanOld((MyCubeGrid)arg1, (MyCubeGrid)SystemGrid))
            {
                Logger.Log(ClassName, "Grid split is... forgot");
                if (otherManager != null)
                {
                    otherManager.OnMarkForClose += OwnerBlock_OnMarkForClose;
                }

                // Dispose of current manager and relinquish control over the grids
                foreach (var grid in managedGrids)
                {
                    OnGridDispose(grid);
                    grid.Storage.RemoveValue(Guid);
                }
            }
            else
            {
                // If the current manager remains larger, continue managing the grid and claim unclaimed grids
                foreach (var grid in notManagedGrids)
                {
                    grid.Storage.Add(Guid, SystemBlockId);
                }
            }

            // Verify the current block's status after the split
            VerifyBlock();
        }

        private void Grid_OnGridMerge(IMyCubeGrid arg1, IMyCubeGrid arg2)
        {
            IMyEntity otherManager;
            HashSet<IMyCubeGrid> managedGrids, notManagedGrids;

            // Process the connected grids
            ProcessConnectedGrids(out managedGrids, out notManagedGrids, out otherManager);

            // If no other manager is found, claim all grids
            if (notManagedGrids.Count <= 0) return;

            // Check which grid is larger
            if (IsNewGridLargerThanOld((MyCubeGrid)arg1, (MyCubeGrid)SystemGrid))
            {
                // If the new grid is larger, transfer control to its manager
                if (otherManager != null)
                {
                    otherManager.OnMarkForClose += OwnerBlock_OnMarkForClose; // Transfer event handling to new manager
                }

                // Dispose of current manager and relinquish control over the grids
                foreach (var grid in managedGrids)
                {
                    OnGridDispose(grid);
                    grid.Storage.RemoveValue(Guid);
                }
            }
            else
            {
                // No other manager or this grid is larger; claim the previously not managed grids
                foreach (var grid in notManagedGrids)
                {
                    grid.Storage.Add(Guid, SystemBlockId);
                }
            }

            // Finally, re-verify the block's status after merge
            VerifyBlock();
        }


        // This is called when grids status is changed.
        private bool BasicGridChecks(IMyCubeGrid grid, out long storedId, out MyModStorageComponentBase storage)
        {
            storage = grid.Storage;
            storedId = 0;
            if (storage == null)
            {
                MyLog.Default.WriteLine($"{grid.DisplayName} storage null grid marked as managed by this block");
                storage = new MyModStorageComponent();
                grid.Storage = storage;
                storage.Add(Guid, SystemBlockId);
                return true;
            }

            string storedBlockId;
            string value;
            if (storage.TryGetValue(Guid, out value))
            {
                storedBlockId = value;
            }
            else
            {
                MyLog.Default.WriteLine(
                    $"{grid.DisplayName} string parse failed, grid marked as managed by this block");
                storage.Add(Guid, SystemBlockId);
                return true;
            }

            if (storedBlockId == SystemBlockId)
            {
                MyLog.Default.WriteLine($"{grid.DisplayName} blockId is equal, grid is managed by this block");
                return true;
            }

            if (string.IsNullOrEmpty(storedBlockId))
            {
                MyLog.Default.WriteLine(
                    $"{grid.DisplayName}stored string is empty, grid marked as managed by this block");
                storage.SetValue(Guid, SystemBlockId);
                return true;
            }

            if (long.TryParse(storedBlockId, out storedId)) return false;

            MyLog.Default.WriteLine($"{grid.DisplayName} parse to long failed, grid marked as managed by this block");
            storage.SetValue(Guid, SystemBlockId);
            return true;
        }

        private void ProcessConnectedGrids(out HashSet<IMyCubeGrid> managedGrids,
            out HashSet<IMyCubeGrid> notManagedGrids, out IMyEntity otherManager)
        {
            otherManager = null;
            managedGrids = new HashSet<IMyCubeGrid>();
            notManagedGrids = new HashSet<IMyCubeGrid>();

            SystemBlock.CubeGrid.GetGridGroup(GridLinkTypeEnum.Mechanical)?.GetGrids(NewConnectedToSystemGrids);

            foreach (var grid in NewConnectedToSystemGrids)
            {
                long storedId;
                MyModStorageComponentBase storage;
                if (BasicGridChecks(grid, out storedId, out storage))
                {
                    managedGrids.Add(grid);
                    continue;
                }

                if (MyAPIGateway.Entities.TryGetEntityById(storedId, out otherManager))
                {
                    notManagedGrids.Add(grid);
                }
                else
                {
                    MyLog.Default.WriteLine(
                        $"{grid.DisplayName} other block was not found, grid marked as managed by this block");
                    storage.SetValue(Guid, SystemBlockId);
                    managedGrids.Add(grid);
                }
            }
        }


        private static bool IsNewGridLargerThanOld(MyCubeGrid newGrid, MyCubeGrid oldSavedGrid)
        {
            // Logic is if new grid - old grid gives more blocks than old grid means it means merged grid is bigger.
            return (newGrid.BlocksCount - oldSavedGrid.BlocksCount) > oldSavedGrid.BlocksCount;
        }


        private bool GridManagement(IMyCubeBlock iMyBlock)
        {
            var isThisManager = false;
            var hasSubscribed = false;

            iMyBlock.CubeGrid.GetGridGroup(GridLinkTypeEnum.Mechanical)?.GetGrids(ConnectedToSystemGrid);

            foreach (var grid in ConnectedToSystemGrid)
            {
                var storage = grid.Storage;

                if (storage == null)
                {
                    MyLog.Default.WriteLine($"{grid.DisplayName} storage null grid marked as managed by this block");
                    storage = new MyModStorageComponent();
                    grid.Storage = storage;
                    storage.Add(Guid, SystemBlockId);
                    isThisManager = true;
                    continue;
                }

                string storedBlockId;
                string value;
                if (storage.TryGetValue(Guid, out value))
                {
                    storedBlockId = value;
                }
                else
                {
                    MyLog.Default.WriteLine(
                        $"{grid.DisplayName} string parse failed, grid marked as managed by this block");
                    storage.Add(Guid, SystemBlockId);
                    isThisManager = true;
                    continue;
                }

                if (storedBlockId == SystemBlockId)
                {
                    MyLog.Default.WriteLine($"{grid.DisplayName} blockId is equal, grid is managed by this block");
                    isThisManager = true;
                    continue;
                }

                if (string.IsNullOrEmpty(storedBlockId))
                {
                    MyLog.Default.WriteLine(
                        $"{grid.DisplayName}stored string is empty, grid marked as managed by this block");
                    storage.SetValue(Guid, SystemBlockId);
                    isThisManager = true;
                    continue;
                }

                long storedId;
                if (!long.TryParse(storedBlockId, out storedId))
                {
                    MyLog.Default.WriteLine(
                        $"{grid.DisplayName} parse to long failed, grid marked as managed by this block");
                    storage.SetValue(Guid, SystemBlockId);
                    isThisManager = true;
                    continue;
                }

                IMyEntity entity;
                if (MyAPIGateway.Entities.TryGetEntityById(storedId, out entity))
                {
                    if (hasSubscribed) continue;
                    MyLog.Default.WriteLine($"{grid.DisplayName} grid is not managed by this block");
                    entity.OnClose += OwnerBlock_OnMarkForClose;
                    OtherManagerId = entity.EntityId.ToString();
                    hasSubscribed = true;
                }
                else
                {
                    MyLog.Default.WriteLine(
                        $"{grid.DisplayName} Other block was not found, grid marked as managed by this block");
                    storage.SetValue(Guid, SystemBlockId);
                    isThisManager = true;
                }
            }

            return isThisManager;
        }

        private void OwnerBlock_OnMarkForClose(IMyEntity obj)
        {
            IsOnStandBy = false;
            obj.OnClose -= OwnerBlock_OnMarkForClose;
            if (!OverrideManagerBlock())
            {
                IsOnStandBy = true;
                IsOtherManagerGone = false;
                MyLog.Default.WriteLine("Trash Sorter could not start. Entering standby.");
            }

            IsOtherManagerGone = true;
        }

        private bool OverrideManagerBlock()
        {
            var blockId = SystemGrid.EntityId.ToString();
            var isThisManager = false;
            var hasSubscribed = false;

            // Retrieve connected grids
            SystemGrid.GetGridGroup(GridLinkTypeEnum.Mechanical)?.GetGrids(ConnectedToSystemGrid);

            MyLog.Default.WriteLine($"{ConnectedToSystemGrid.Count} Amount of grids overriding.");

            foreach (var grid in ConnectedToSystemGrid)
            {
                // Synchronize access to the grid's storage
                var storage = grid.Storage;

                string storedBlockId;
                storage.TryGetValue(Guid, out storedBlockId);

                if (string.IsNullOrEmpty(storedBlockId))
                {
                    MyLog.Default.WriteLine(
                        $"{grid.DisplayName}: Override manager on destruction, old {storedBlockId} grid marked as managed by block {SystemBlock.EntityId}");

                    // Assign the block as the manager with a timestamp or priority
                    storage.SetValue(Guid, blockId);

                    isThisManager = true;
                    OnNeedsUpdate(MyEntityUpdateEnum.BEFORE_NEXT_FRAME);
                    continue;
                }

                if (storedBlockId != blockId && storedBlockId == OtherManagerId)
                {
                    MyLog.Default.WriteLine(
                        $"{grid.DisplayName}: Override manager on destruction, old {storedBlockId} grid marked as managed by block {SystemBlock.EntityId}");

                    // Assign the block as the manager with a timestamp or priority
                    storage.SetValue(Guid, blockId);

                    isThisManager = true;
                    OnNeedsUpdate(MyEntityUpdateEnum.BEFORE_NEXT_FRAME);
                    continue;
                }

                if (hasSubscribed) continue;

                IMyEntity entity;
                if (MyAPIGateway.Entities.TryGetEntityById(long.Parse(storedBlockId), out entity))
                {
                    if (entity.EntityId == SystemBlock.EntityId) continue;

                    MyLog.Default.WriteLine(
                        $"{grid.DisplayName}: Override manager on destruction failed, grid is not managed by this block");
                    entity.OnClose += OwnerBlock_OnMarkForClose;
                    OtherManagerId = entity.EntityId.ToString();
                    isThisManager = false;
                    hasSubscribed = true;
                }
                else
                {
                    MyLog.Default.WriteLine(
                        $"{grid.DisplayName}: Override manager on destruction, grid is marked as managed by block");

                    storage.SetValue(Guid, blockId);
                    isThisManager = true;
                }
            }

            return isThisManager;
        }

        private void Block_OnMarkForClose(IMyEntity obj)
        {
            // Moved entire dispose into earlier method to be sure it does its job.
            obj.OnClosing -= Block_OnMarkForClose;
            OnNeedsUpdate(MyEntityUpdateEnum.NONE);
            OnDisposeInvoke();
            Dispose();
        }
    }
}