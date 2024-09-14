using System.Collections.Generic;
using Trash_Sorter.BaseClass;
using Trash_Sorter.GridManagerRewritten;
using Trash_Sorter.StorageClasses;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Trash_Sorter.GridManagers
{
    public class GridEventManager : GridManagement
    {
        private readonly GridOwnerManager _gridOwnerManager;
        private readonly GridConnectionManager ConnectionManager;
        protected ItemGridManager InventoryManager;
        //Todo, see if I even need those classes. In the size situation issue it should never really happen.

        private readonly HashSet<IMyCubeGrid> ManagedGrids;

        private readonly GridStorage GridStorage;

        public GridEventManager(GridStorage gridStorage, GridOwnerManager gridOwnerManager,
            GridConnectionManager connectionManager)
        {
            _gridOwnerManager = gridOwnerManager;
            ConnectionManager = connectionManager;
            GridStorage = gridStorage;
            ManagedGrids = gridStorage.ManagedGrids;
            GridStorage.ThisManager.OnBlockClosed += ManagerBlock_OnBlockClosed;
            InventoryLink += SubscribeInventoryGridManagerEvents;

            SubscribeOwnerManagerEvents();


            //EnterStandBy += GridEventManager_EnterStandBy;
            //ExitStandBy += GridEventManager_ExitStandBy;
        }
        /*
        private void GridEventManager_ExitStandBy(byte obj)
        {
            if (obj == SizeIssue)
            {
                SubscribeGridToBlockManagement(ManagedGrids);
            }
        }

        private void GridEventManager_EnterStandBy(byte obj)
        {
            if (obj == SizeIssue)
            {
                UnSubscribeGridToBlockManagement(ManagedGrids);
            }
        }*/

        private void SubscribeOwnerManagerEvents()
        {
            EnterStandBy += _gridOwnerManager.GOM_EnterStandBy;
            ExitStandBy += _gridOwnerManager.GOM_ExitStandBy;
            GridSplitInvoked += _gridOwnerManager.GOM_ManagerWasSeparated;
            ManagerDeleted += _gridOwnerManager.GOM_ManagerDeleted;
        }

        private void UnSubscribeOwnerManagerEvents()
        {
            EnterStandBy -= _gridOwnerManager.GOM_EnterStandBy;
            ExitStandBy -= _gridOwnerManager.GOM_ExitStandBy;
            GridSplitInvoked -= _gridOwnerManager.GOM_ManagerWasSeparated;
            ManagerDeleted -= _gridOwnerManager.GOM_ManagerDeleted;
        }

        private void SubscribeInventoryGridManagerEvents(ItemGridManager manager)
        {
            InventoryManager = manager;
            GridRemoved -= InventoryManager.RemoveGridFromSystem;
            GridAdded -= InventoryManager.AddedGridToSystem;
            GridUpdated -= InventoryManager.UpdateGridInSystem;
            BlockAdded -= InventoryManager.AddBlock;
        }
        private void UnsubscribeInventoryGridManagerEvents()
        {
        }
        private void SubscribeConnectionManagerEvents()
        {
            GridSplitInvoked += 
        }

        private void UnsubscribeConnectionManagerEvents()
        {
        }

        private void ManagerBlock_OnBlockClosed(IMyEntity obj)
        {
            if (obj == null) return;
            // Moved entire dispose into earlier method to be sure it does its job.
            GridStorage.ThisManager.OnBlockClosed -= ManagerBlock_OnBlockClosed;
            OnUpdateRequired(MyEntityUpdateEnum.NONE);
            Dispose();
        }

        public override void Dispose()
        {
            base.Dispose();
            UnSubscribeOwnerManagerEvents();
            UnsubscribeInventoryGridManagerEvents();
            UnsubscribeConnectionManagerEvents();
        }
    }
}