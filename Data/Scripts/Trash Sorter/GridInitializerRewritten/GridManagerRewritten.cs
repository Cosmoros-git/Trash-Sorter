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
        private readonly int[] SubscribedEvents = { 0, 0, 0, 0 };


        private bool _isSizeSubscribed;
        private bool _isCounting;
        private readonly int[] Count = { 0, ModSessionComponent.UpdateCooldownLimit };
        private void StartCount() => _isCounting = true;

        public GridManagerRewritten(ObservableGridStorage gridStorage)
        {
            GridStorage = gridStorage;
            InitializationStep = 0;
            InvokeInit += Init;
            SubscribedEvents[0] = 1;
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
            var grid = ((IMyCubeBlock)ThisManager).CubeGrid;
            GridFunctions.TryGetConnectedGrids(grid, GridLinkTypeEnum.Mechanical, GridStorage.ManagedGrids);
            OnInvokeInit();
        }


        // Check if grid size is valid.
        private void GridSizeCheck()
        {
            // Check if there are multiple grids or if the block count exceeds the limit
            if (GridStorage.ManagedGrids.Count > 1 || GridFunctions.GridBlockCount(HashCollectionGrids) >=
                ModSessionComponent.BlockLimitsToStartManaging)
            {
                InitializationStep = InitializationStepGrid.OwnerConfirmation;
                SubscribedEvents[0] = 0;
                if (_isSizeSubscribed) ConflictSize.SizeAchieved -= OnInvokeInit;
                OnInvokeInit();
            }
            else
            {
                if (_isSizeSubscribed) return;
                ConflictSize.GridSizeIssue();
                _isSizeSubscribed = true;
                ConflictSize.SizeAchieved += OnInvokeInit;
            }
        }


        private void OwnerCheck()
        {
            if (SubscribedEvents[1] != 1)
            {
                OwnerChecks.IsThisOwner += OwnerChecks_IsThisOwner;
                SubscribedEvents[1] = 1;
            }

            OwnerChecks.CheckOwner(ThisManager, GridStorage.ManagedGrids);
        }

        private void OwnerChecks_IsThisOwner(bool obj)
        {
            if (obj)
            {
                if (SubscribedEvents[1] != 0)
                {
                    OwnerChecks.IsThisOwner -= OwnerChecks_IsThisOwner;
                    SubscribedEvents[1] = 0;
                }

                OnUpdateRequired(MyEntityUpdateEnum.EACH_10TH_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME);
            }
            else
            {
                OnUpdateRequired(MyEntityUpdateEnum.NONE);
                ConflictManager.OwnerConflict();
                InitializationStep = InitializationStepGrid.GridInfoCollection;
                PartialDispose();
                if (SubscribedEvents[2] == 1) return;
                ConflictManager.ManagerRemoved += Init;
                SubscribedEvents[1] = 1;
            }
        }


        public override void Dispose()
        {
            base.Dispose();
            if (SubscribedEvents[0] == 1) InvokeInit -= Init;
            if (SubscribedEvents[1] == 1) OwnerChecks.IsThisOwner -= OwnerChecks_IsThisOwner;
            if (SubscribedEvents[2] == 1) ConflictManager.ManagerRemoved -= Init;
            if (SubscribedEvents[3] == 1) return;
        }
    }
}