using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using Trash_Sorter.BaseClass;
using Trash_Sorter.StaticComponents;
using Trash_Sorter.StaticComponents.StaticFunctions;
using Trash_Sorter.StorageClasses;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Trash_Sorter.GridInitializerRewritten
{
    public class GridManagerRewritten : GridManagerBase
    {
        [Flags]
        public enum SubscribedEventFlags
        {
            None = 0,               
            InitInvoked = 1 << 0,     
            OwnerSubscribed = 1 << 1, 
            ManagerRemoved = 1 << 2, 
            SomeOtherEvent = 1 << 3   // Equivalent to SubscribedEvents[3]
        }


        private SubscribedEventFlags _subscribedEvents = SubscribedEventFlags.None;

        private bool _isSizeSubscribed;
        private bool _isCounting;
        private readonly int[] Count = { 0, ModSessionComponent.UpdateCooldownLimit };
        private void StartCount() => _isCounting = true;

        public GridManagerRewritten(ObservableGridStorage gridStorage)
        {
            GridStorage = gridStorage;
            InitializationStep = 0;
            InvokeInit += Init;
            _subscribedEvents |= SubscribedEventFlags.InitInvoked; // Set the InitInvoked flag
        }

        public void Counter()
        {
            if (!_isCounting) return;

            Count[0]++;

            if (Count[0] != Count[1]) return;

            Count[0] = 0;
            _isCounting = false;
            ThisManager.NeedsUpdate &= ~MyEntityUpdateEnum.EACH_FRAME_AFTER;
            OnInvokeInit();
        }

        public void Init()
        {
            switch (InitializationStep)
            {
                case InitializationStepGrid.BlockVerification:
                    BlockCheck();
                    break;
                case InitializationStepGrid.GridInfoCollection:
                    GetGrids();
                    break;
                case InitializationStepGrid.MinSizeConfirmation:
                    GridSizeCheck();
                    break;
                case InitializationStepGrid.OwnerConfirmation:
                    OwnerCheck();
                    break;
            }
        }

        // Block validity checks
        private void BlockCheck()
        {
            var verificationResult = BlockFunctions.BasicBlockVerification(ThisManager);

            if (verificationResult == BlockFunctions.EntityVerificationResult.Success)
            {
                InitializationStep = InitializationStepGrid.GridInfoCollection;
                OnInvokeInit();
            }
            else
            {
                OnUpdateRequired(MyEntityUpdateEnum.EACH_FRAME_AFTER);
                StartCount();
                Logger.Log("BlockCheck", $"Block verification failed with result: {verificationResult}");
            }
        }

        // Get grids to manage
        private void GetGrids()
        {
            GridFunctions.TryGetConnectedGrids(((IMyCubeBlock)ThisManager).CubeGrid, GridLinkTypeEnum.Mechanical, GridStorage.ManagedGrids);
            InitializationStep = InitializationStepGrid.MinSizeConfirmation;
            Logger.Log(ClassName,$"Grids collected, amount of grids {GridStorage.ManagedGrids.Count}");
            OnInvokeInit();
        }

        // Check if grid size is valid.
        private void GridSizeCheck()
        {
            if (GridStorage.ManagedGrids.Count > 1 || GridFunctions.GridBlockCount(HashCollectionGrids) >=
                ModSessionComponent.BlockLimitsToStartManaging)
            {
                InitializationStep = InitializationStepGrid.OwnerConfirmation;
                Logger.Log(ClassName, $"Grid size check passed. {((IMyCubeBlock)ThisManager).CubeGrid.CustomName}");
                _subscribedEvents &= ~SubscribedEventFlags.InitInvoked; // Clear the InitInvoked flag
                if (_isSizeSubscribed) ConflictSize.SizeAchieved -= OnInvokeInit;
                OnInvokeInit();
            }
            else
            {
                if (_isSizeSubscribed) return;
                Logger.Log(ClassName, $"Grid size check failed. {((IMyCubeBlock)ThisManager).CubeGrid.CustomName}");
                ConflictSize.GridSizeIssue();
                _isSizeSubscribed = true;
                ConflictSize.SizeAchieved += OnInvokeInit;
            }
        }

        private void OwnerCheck()
        {
            if ((_subscribedEvents & SubscribedEventFlags.OwnerSubscribed) == 0)
            {
                IsThisOwner += OwnerChecks_IsThisOwner;
                _subscribedEvents |= SubscribedEventFlags.OwnerSubscribed; // Set the OwnerSubscribed flag
            }
            Logger.Log(ClassName, $"Manager with Id: {ThisManager.EntityId} on grid: {((IMyCubeBlock)ThisManager).CubeGrid.CustomName} processing ownership claim");
            OwnerChecks.CheckOwner(ThisManager);
        }

        private void OwnerChecks_IsThisOwner(bool obj)
        {
            if (obj)
            {
                if ((_subscribedEvents & SubscribedEventFlags.OwnerSubscribed) != 0)
                {
                    IsThisOwner -= OwnerChecks_IsThisOwner;
                    _subscribedEvents &= ~SubscribedEventFlags.OwnerSubscribed; // Clear the OwnerSubscribed flag
                }

                OnUpdateRequired(MyEntityUpdateEnum.EACH_10TH_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME);
            }
            else
            {
                OnUpdateRequired(MyEntityUpdateEnum.NONE);
                ConflictManager.OwnerConflict();
                InitializationStep = InitializationStepGrid.GridInfoCollection;
                PartialDispose();
                if ((_subscribedEvents & SubscribedEventFlags.ManagerRemoved) != 0) return;
                ConflictManager.ManagerRemoved += Init;
                _subscribedEvents |= SubscribedEventFlags.OwnerSubscribed; // Set the OwnerSubscribed flag
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            if ((_subscribedEvents & SubscribedEventFlags.InitInvoked) != 0) InvokeInit -= Init;
            if ((_subscribedEvents & SubscribedEventFlags.OwnerSubscribed) != 0) IsThisOwner -= OwnerChecks_IsThisOwner;
            if ((_subscribedEvents & SubscribedEventFlags.ManagerRemoved) != 0) ConflictManager.ManagerRemoved -= Init;
        }
    }

}