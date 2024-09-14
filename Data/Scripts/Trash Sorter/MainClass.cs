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

namespace Trash_Sorter
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_MyProgrammableBlock), false, "LargeTrashController",
        "SmallTrashController")]
    public class MainClass : MyGameLogicComponent
    {
        private const string ClassName = "Main-Class";
        public GridManager GridSystemOwner;
        private bool isSubbed;
        private bool ManagerCreated;

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
            _gridManager.Dispose();
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            if (!ManagerCreated)
            {
                var wat5 = Stopwatch.StartNew();
                Logger.Log(ClassName, "Initializing step 1. Starting grid management.");
                _gridManager = new GridInitializerRewritten.GridManagerRewritten(Entity);
                Entity.OnClosing += Entity_OnClosing;
                Logger.Log(ClassName, $"Step 1. Time taken {wat5.Elapsed.TotalMilliseconds}ms");
                wat5.Stop();
                totalInitTime += wat5.Elapsed;
                ManagerCreated = true;
            }

            GridSystemOwner.UpdateOnceBeforeFrame(); // Initializing management.
        }

        public override void UpdateAfterSimulation()
        {
            base.UpdateAfterSimulation();
            _gridManager.Counter();
        }


        private void GridSystemOwnerCallbackDisposeRequired()
        {
            Logger.LogWarning(ClassName, $"Dispose called on entity id {Entity.EntityId}, on grid {((IMyCubeBlock)Entity).CubeGrid.CustomName}");
            GridSystemOwner.UpdateRequired -= GridSystemOwnerCallbackUpdateRequired;
            GridSystemOwner.DisposeRequired -= GridSystemOwnerCallbackDisposeRequired;
        }

        private void GridSystemOwnerCallbackUpdateRequired(MyEntityUpdateEnum obj)
        {
            NeedsUpdate = obj;
        }

        private int Initialization_Step;


        // Storing files so GC won't sudo rm rf them.
        private GridInitializerRewritten.GridManagerRewritten _gridManager;
        private ItemStorage _mainItemStorage;
        private ItemGridManager inventoryGridManager;
        private ModSorterManager _modSorterMainManager;
        private SorterChangeHandler sorterChangeHandler;
        private TimeSpan totalInitTime;


        // Initializing sequence. So far it takes around 80ms-200ms even on ultra large grids.
        public override void UpdateAfterSimulation10()
        {
            base.UpdateAfterSimulation10();
            switch (Initialization_Step)
            {
                case 0:
                    
                case 1:
                    var wat1 = Stopwatch.StartNew();
                    Logger.Log(ClassName, "Initializing step 2. Creating item storage.");
                    _mainItemStorage = new ItemStorage();
                    Initialization_Step++;
                    wat1.Stop();
                    totalInitTime += wat1.Elapsed;
                    Logger.Log(ClassName, $"Step 2. Time taken {wat1.Elapsed.TotalMilliseconds}ms");
                    break;

                case 2:
                    var wat2 = Stopwatch.StartNew();
                    Logger.Log(ClassName, "Initializing step 3. Starting grid inventory management.");
                    inventoryGridManager = new ItemGridManager(_mainItemStorage,
                        GridSystemOwner.GridStorage);
                    Initialization_Step++;
                    wat2.Stop();
                    totalInitTime += wat2.Elapsed;
                    Logger.Log(ClassName, $"Step 3. Time taken {wat2.Elapsed.TotalMilliseconds}ms");
                    break;


                case 3:
                    var wat3 = Stopwatch.StartNew();
                    Logger.Log(ClassName, "Initializing step 4. Starting inventory callback management.");
                    sorterChangeHandler = new SorterChangeHandler(_mainItemStorage);
                    Initialization_Step++;
                    wat3.Stop();
                    totalInitTime += wat3.Elapsed;
                    Logger.Log(ClassName, $"Step 4. Time taken {wat3.Elapsed.TotalMilliseconds}ms");
                    break;

                case 4:
                    var wat4 = Stopwatch.StartNew();
                    Logger.Log(ClassName, "Initializing step 5. Starting trash sorter management.");
                    _modSorterMainManager =
                        new ModSorterManager(inventoryGridManager.ModSorters, _mainItemStorage,
                            inventoryGridManager, sorterChangeHandler.SorterLimitManagers,
                            _mainItemStorage.NameToDefinitionMap);
                    wat4.Stop();
                    totalInitTime += wat4.Elapsed;
                    Logger.Log(ClassName, $"Step 5. Time taken {wat4.Elapsed.TotalMilliseconds}ms");
                    Logger.Log(ClassName, $"Total init time. Time taken {totalInitTime.TotalMilliseconds}ms");
                    Initialization_Step++;
                    break;
            }
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