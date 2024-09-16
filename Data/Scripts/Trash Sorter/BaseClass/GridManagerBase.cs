using System;
using System.Collections.Generic;
using Trash_Sorter.GridInitializerRewritten;
using Trash_Sorter.StorageClasses;
using VRage.Game.ModAPI;
using static Trash_Sorter.MainClass;

namespace Trash_Sorter.BaseClass
{
    public abstract class GridManagerBase : ModBase
    {
        public GridOwnerChecks OwnerChecks = new GridOwnerChecks();
        public readonly ConflictSize ConflictSize = new ConflictSize();
        public readonly ConflictManager ConflictManager = new ConflictManager();
        public ObservableGridStorage GridStorage;

        public enum InitializationStepGrid
        {
            BlockVerification = 0,
            GridInfoCollection = 1,
            MinSizeConfirmation = 2,
            OwnerConfirmation = 3,
        }

        public InitializationStepGrid InitializationStep;

        public void OnInvokeInit() => InvokeInit?.Invoke();
        public event Action InvokeInit;
    }
}