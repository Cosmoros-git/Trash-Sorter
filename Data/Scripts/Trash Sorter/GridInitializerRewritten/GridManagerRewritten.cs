using System;
using System.Collections.Generic;
using Trash_Sorter.StaticComponents;
using Trash_Sorter.StaticComponents.StaticFunction;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Trash_Sorter.GridInitializerRewritten
{
    public class GridManagerRewritten : GridManagerBase
    {
        public GridOwnerChecks OwnerChecks = new GridOwnerChecks();
        private readonly CoflictSize _conflictSize = new CoflictSize();
        private readonly ConflictManager _conflictManager = new ConflictManager();

        private readonly int[] SubscribedEvents = { 0, 0, 0, 0 };

        private enum InitializationStep
        {
            BlockVerification = 0,
            GridInfoCollection = 1,
            MinSizeConfirmation = 2,
            OwnerConfirmation = 3,
        }

        private InitializationStep _initializationStep;


        private event Action InvokeInit;


        private HashSet<IMyCubeGrid> connectedGrids;

        private bool _isSizeSubscribed;
        private bool _isCounting;
        private readonly int[] Count = { 0, 0 };

        private void OnInvokeInit()
        {
            InvokeInit?.Invoke();
        }

        private void StartCount()
        {
            Count[1] = ModSessionComponent.UpdateCooldownLimit;
            _isCounting = true;
        }


        public GridManagerRewritten(IMyEntity controllerEntity)
        {
            ThisManager = controllerEntity;
            _initializationStep = 0;
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
            switch (_initializationStep)
            {
                case InitializationStep.BlockVerification:
                    BlockCheck();
                    break;
                case InitializationStep.GridInfoCollection:
                    GetGrids();
                    break;
                case InitializationStep.MinSizeConfirmation:
                    GridSizeCheck();
                    break;
                case InitializationStep.OwnerConfirmation:
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
                _initializationStep = InitializationStep.GridInfoCollection;
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
            connectedGrids = GridFunctions.GetConnectedGrids(grid, GridLinkTypeEnum.Mechanical);
            _initializationStep = InitializationStep.MinSizeConfirmation;
            OnInvokeInit();
        }



        // Check if grid size is valid.
        private void GridSizeCheck()
        {
            // Check if there are multiple grids or if the block count exceeds the limit
            if (connectedGrids.Count > 1 || GridFunctions.GridBlockCount(connectedGrids) >=
                ModSessionComponent.BlockLimitsToStartManaging)
            {
                _initializationStep = InitializationStep.OwnerConfirmation;
                SubscribedEvents[0] = 0;
                if (_isSizeSubscribed) _conflictSize.SizeAchieved -= OnInvokeInit;
                OnInvokeInit();
            }
            else
            {
                _isSizeSubscribed = true;
                _conflictSize.SizeAchieved += OnInvokeInit;
            }
        }



        private void OwnerCheck()
        {
            OwnerChecks.IsThisOwner += OwnerChecks_IsThisOwner;
            SubscribedEvents[1] = 1;
            OwnerChecks.CheckOwner(ThisManager, connectedGrids);
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
                _conflictManager.OwnerConflict();
                if (SubscribedEvents[2] == 1) return;

                _conflictManager.ManagerSeparated += Init;
                SubscribedEvents[1] = 1;
            }
        }

        public override void Dispose()
        {
            base.Dispose();
            if (SubscribedEvents[0] == 1) InvokeInit -= Init;
            if (SubscribedEvents[1] == 1) OwnerChecks.IsThisOwner -= OwnerChecks_IsThisOwner;
            if (SubscribedEvents[2] == 1) _conflictManager.ManagerSeparated -= Init;
            if (SubscribedEvents[3] == 1) return;
        }
    }
}