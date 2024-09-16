using System;
using System.Diagnostics;
using Sandbox.Common.ObjectBuilders;
using Trash_Sorter.GridManagerRewritten;
using Trash_Sorter.GridManagers;
using Trash_Sorter.SorterClasses;
using Trash_Sorter.StaticComponents;
using Trash_Sorter.StorageClasses;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using static Trash_Sorter.MainClass;

namespace Trash_Sorter
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_MyProgrammableBlock), false, "LargeTrashController",
        "SmallTrashController")]
    public class MainClass : MyGameLogicComponent
    {
        private const string ClassName = "Main-Class";
        private bool isSubbed;
        private TimeSpan totalInitTime;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            if (ModSessionComponent.IsInitializationAllowed == false)
            {
                ModSessionComponent.AllowInitialization += ModSessionComponent_AllowInitialization;
                isSubbed = true;
            }
            else
            {
                ModSessionComponent_AllowInitialization();
            }
        }

        private void ModSessionComponent_AllowInitialization()
        {
            if (isSubbed) ModSessionComponent.AllowInitialization -= ModSessionComponent_AllowInitialization;
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            Logger.Log("MainClass", "Trash Sorter starting up");
        }

        private void Entity_OnClosing(IMyEntity obj)
        {
            NeedsUpdate = MyEntityUpdateEnum.NONE;
            Entity.OnClosing -= Entity_OnClosing;
            _gridManager.Dispose();
            Logger.LogWarning(ClassName,
                $"Dispose called on entity id {Entity.EntityId}, on grid {((IMyCubeBlock)Entity).CubeGrid.CustomName}");
        }

        public override void UpdateAfterSimulation()
        {
            base.UpdateAfterSimulation();
            _gridManager.Counter();
        }

        private int Initialization_Step;

        // Storing files so GC won't sudo rm rf them.
        private ObservableGridStorage _gridStorage;
        private GridInitializerRewritten.GridManagerRewritten _gridManager;
        private ItemStorage _itemStorage;
        private InventoryManagerRewritten _inventoryManagerRewritten;
        private ModSorterManager _modSorterMainManager;
        private SorterChangeHandler sorterChangeHandler;


        // Initializing sequence. So far it takes around 5ms-20ms even on ultra large grids.
        public enum InitializationStep
        {
            CreateGridManager = 1,
            CreateGridObservableStorage = 0,
            CreateStorage = 2,
            StartInventoryManager = 3,
            StartSorterManager = 4,
        }

        private InitializationStep _initializationStep;


        public override void UpdateAfterSimulation10()
        {
            base.UpdateAfterSimulation10();
            switch (_initializationStep)
            {
                case InitializationStep.CreateGridObservableStorage:
                    CreateObservableStorage();
                    break;

                case InitializationStep.CreateGridManager:
                    CreateGridManager();
                    break;

                case InitializationStep.CreateStorage:
                    CreateStorage();
                    break;
                case InitializationStep.StartInventoryManager:
                    StartInventoryManager();
                    break;
                case InitializationStep.StartSorterManager:
                    var wat3 = Stopwatch.StartNew();
                    Logger.Log(ClassName, "Initializing step 4. Starting inventory callback management.");
                    sorterChangeHandler = new SorterChangeHandler(_itemStorage);
                    Initialization_Step++;
                    wat3.Stop();
                    totalInitTime += wat3.Elapsed;
                    Logger.Log(ClassName, $"Step 4. Time taken {wat3.Elapsed.TotalMilliseconds}ms");
                    break;

                case 4:
                    var wat4 = Stopwatch.StartNew();
                    Logger.Log(ClassName, "Initializing step 5. Starting trash sorter management.");
                    _modSorterMainManager =
                        new ModSorterManager(_inventoryManagerRewritten.ModSorters, _itemStorage,
                            _inventoryManagerRewritten, sorterChangeHandler.SorterLimitManagers,
                            _itemStorage.NameToDefinitionMap);
                    wat4.Stop();
                    totalInitTime += wat4.Elapsed;
                    Logger.Log(ClassName, $"Step 5. Time taken {wat4.Elapsed.TotalMilliseconds}ms");
                    Logger.Log(ClassName, $"Total init time. Time taken {totalInitTime.TotalMilliseconds}ms");
                    Initialization_Step++;
                    break;
            }
        }

        private void CreateGridManager()
        {
            if (_gridManager != null) return;

            var wat5 = Stopwatch.StartNew();
            Logger.Log(ClassName, "Initializing step 1. Starting grid management.");
            _gridManager = new GridInitializerRewritten.GridManagerRewritten(_gridStorage);
            Entity.OnClosing += Entity_OnClosing;
            Initialization_Step = 1;
            Logger.Log(ClassName, $"Step 1. Time taken {wat5.Elapsed.TotalMilliseconds}ms");
            wat5.Stop();
            totalInitTime += wat5.Elapsed;
        }

        private void CreateObservableStorage()
        {
            _gridStorage = new ObservableGridStorage(Entity);
        }


        private void CreateStorage()
        {
            if (_itemStorage != null) return;

            Logger.Log(ClassName, "Initializing step 2. Creating item storage.");
            var wat1 = Stopwatch.StartNew();
            _itemStorage = new ItemStorage();
            wat1.Stop();
            totalInitTime += wat1.Elapsed;
            Logger.Log(ClassName, $"Step 2. Time taken {wat1.Elapsed.TotalMilliseconds}ms");
            Initialization_Step++;
        }

        private void StartInventoryManager()
        {
            if (_inventoryManagerRewritten != null) return;

            var wat2 = Stopwatch.StartNew();
            Logger.Log(ClassName, "Initializing step 3. Starting grid inventory management.");
            _inventoryManagerRewritten = new InventoryManagerRewritten(_itemStorage, _gridManager.HashCollectionGrids);
            Initialization_Step++;
            wat2.Stop();
            _initializationStep = InitializationStep.StartSorterManager;
            totalInitTime += wat2.Elapsed;
            Logger.Log(ClassName, $"Step 3. Time taken {wat2.Elapsed.TotalMilliseconds}ms");
        }


        // Logging/Comparison logic.
        public override void UpdateAfterSimulation100()
        {
            base.UpdateAfterSimulation100();
            if (Initialization_Step < 5) return;

            _modSorterMainManager.OnAfterSimulation100();
            sorterChangeHandler.OnAfterSimulation100();
        }
    }
}