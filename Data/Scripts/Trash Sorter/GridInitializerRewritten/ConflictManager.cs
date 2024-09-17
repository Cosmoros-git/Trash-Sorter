using System;
using Sandbox.ModAPI;
using Trash_Sorter.BaseClass;
using Trash_Sorter.StaticComponents.StaticFunctions;
using Trash_Sorter.StaticComponents;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Trash_Sorter.GridInitializerRewritten
{
    public class ConflictManager : GridManagerBase
    {
        public event Action ManagerRemoved;


        private bool Subscribed;
        private IMyCubeBlock _otherManagerBlock;
        private IMyTerminalBlock _otherTerminalBlock;
        private IMyCubeBlock _thisManagerBlock;

        public void OwnerConflict()
        {
            if (OtherManager == null) return;
            if (Subscribed) return;
            OtherManager.OnClosing += OtherManager_OnClosing;
            _otherManagerBlock = (IMyCubeBlock)OtherManager;
            _thisManagerBlock = (IMyCubeBlock)ThisManager;
            _otherTerminalBlock = (IMyTerminalBlock)_otherManagerBlock;
            _otherTerminalBlock.CustomNameChanged += CustomNameChanged;
            Subscribed = true;
        }

        private void CustomNameChanged(IMyTerminalBlock obj)
        {
            if (obj.CustomName.Contains(RestartCall)) TryRestarting();
        }

        private void TryRestarting()
        {
            if (GridFunctions.GetGridGroup(_otherManagerBlock.CubeGrid, GridLinkTypeEnum.Mechanical) ==
                GridFunctions.GetGridGroup(_thisManagerBlock.CubeGrid, GridLinkTypeEnum.Mechanical)) return;
            OtherManager_OnClosing(OtherManager);
        }

        private void OtherManager_OnClosing(IMyEntity obj)
        {
            if (obj == null) return;
            OtherManager.OnClosing -= OtherManager_OnClosing;
            _otherTerminalBlock.CustomNameChanged -= CustomNameChanged;
            InitializationStep = InitializationStepGrid.GridInfoCollection;
            ManagerRemoved?.Invoke();
            OtherManager = null;
        }


        public override void Dispose()
        {
            base.Dispose();
            if (!Subscribed) return;
            OtherManager.OnClosing -= OtherManager_OnClosing;
            _otherTerminalBlock.CustomNameChanged -= CustomNameChanged;
            
        }
    }
}