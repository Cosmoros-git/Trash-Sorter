using System;
using Trash_Sorter.BaseClass;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Trash_Sorter.GridInitializerRewritten
{
    public class ConflictManager : GridManagerBase
    {
        public event Action ManagerRemoved;


        private bool Subscribed;
        private IMyCubeBlock _otherManagerBlock;
        private bool GridRemovedSubscribed;

        public void OwnerConflict()
        {
            if (OtherManager == null) return;
            OtherManager.OnClosing += OtherManager_OnClosing;
            _otherManagerBlock = (IMyCubeBlock)OtherManager;
            if (((IMyCubeBlock)ThisManager).CubeGrid != _otherManagerBlock.CubeGrid && !GridRemovedSubscribed)
            {
                GridRemoved += ConflictManager_GridRemoved;
                GridRemovedSubscribed = true;
            }

            Subscribed = true;
        }

        private void ConflictManager_GridRemoved(IMyCubeGrid obj)
        {
            if (obj == null) return;
            if (obj == ((IMyCubeBlock)OtherManager).CubeGrid)
            {
                OtherManager_OnClosing(OtherManager);
            }
        }

        private void OtherManager_OnClosing(IMyEntity obj)
        {
            OtherManager.OnClosing -= OtherManager_OnClosing;
            if (GridRemovedSubscribed) GridRemoved -= ConflictManager_GridRemoved;
            ManagerRemoved?.Invoke();
            OtherManager = null;
        }


        public override void Dispose()
        {
            base.Dispose();
            if (Subscribed) OtherManager.OnClosing -= OtherManager_OnClosing;
            if (GridRemovedSubscribed) GridRemoved -= ConflictManager_GridRemoved;
        }
    }
}