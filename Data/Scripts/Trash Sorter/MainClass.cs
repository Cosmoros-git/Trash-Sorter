using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces.Terminal;
using Trash_Sorter.Data.Scripts.Trash_Sorter.ActiveClasses;
using Trash_Sorter.Data.Scripts.Trash_Sorter.ActiveClasses.Mod_Sorter;
using Trash_Sorter.Data.Scripts.Trash_Sorter.Main_Storage_Class;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;

namespace Trash_Sorter.Data.Scripts.Trash_Sorter
{
    /* What mod has to do. Sort "trash" with trash sorter out of inventories
     * Classes needed for that
     * Main -> Initialize the mod and create other class instances
     * Logger -> Deals with logging shit in a readable manner.
     *
     *
     * Grid scanner -> Scans the grid for blocks and deals with state changes.
     *
     * Storage -> Stores amount of items and deals with removing storage items from the item list or adding them.
     * Storage also gets all the definitions of the items its going to store.
     * Only items out of interested definitions are to be tracked.
     *
     *
     * Sorter Manager -> Manager sorter filters.
     * Also makes sure those filters are not being reset by people breaking the system.
     *
     * Custom Data Change Manager -> Because of event not working as intended this class deals with scanning every object custom data and dealing with it in different ways.
     * Data parser -> Takes the data from the custom data and passes it into more useful type of data.
     *
     * TODO fix issue with grid splitting/merging
     *
     *
     */


    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_MyProgrammableBlock), false, "LargeTrashController",
        "SmallTrashController")]
    public class MainClass : MyGameLogicComponent
    {
        private const string ClassName = "Main-Class";
        public GridSystemOwner GridSystemOwner;
        public Stopwatch Watch = new Stopwatch();
        private Logger myLogger;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            MyLog.Default.WriteLine("Trash Sorter starting up");
            GridSystemOwner = new GridSystemOwner(Entity);

            GridSystemOwnerCallback.NeedsUpdate += GridSystemOwnerCallback_NeedsUpdate;
            GridSystemOwnerCallback.DisposeInvoke += GridSystemOwnerCallback_DisposeInvoke;
        }

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();
            GridSystemOwner.UpdateOnceBeforeFrame(); // Initializing management.
        }

        private void GridSystemOwnerCallback_DisposeInvoke()
        {
            GridSystemOwnerCallback.NeedsUpdate -= GridSystemOwnerCallback_NeedsUpdate;
            GridSystemOwnerCallback.DisposeInvoke -= GridSystemOwnerCallback_DisposeInvoke;
        }

        private void GridSystemOwnerCallback_NeedsUpdate(MyEntityUpdateEnum obj)
        {
            NeedsUpdate = obj;
        }

        private int Initialization_Step;


        // Storing files so GC won't sudo rm rf them.
        private MainItemStorage _mainItemStorage;
        private InventoryGridManager inventoryGridManager;
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
                    Watch.Restart();
                    myLogger = GridSystemOwner.Logger;
                    myLogger.Log(ClassName, "Initializing step 1. Creating item storage.");
                    _mainItemStorage = new Main_Storage_Class.MainItemStorage(myLogger);
                    Initialization_Step++;
                    Watch.Stop();
                    totalInitTime += Watch.Elapsed;
                    myLogger.Log(ClassName, $"Step 1. Time taken {Watch.ElapsedMilliseconds}ms");
                    break;

                case 1:
                    Watch.Restart();
                    myLogger.Log(ClassName, "Initializing step 2. Starting grid inventory management.");
                    inventoryGridManager = new InventoryGridManager(_mainItemStorage, GridSystemOwner.ConnectedToSystemGrid, GridSystemOwner.SystemGrid, GridSystemOwner.SystemBlock,myLogger);
                    Initialization_Step++;
                    Watch.Stop();
                    totalInitTime += Watch.Elapsed;
                    myLogger.Log(ClassName, $"Step 2. Time taken {Watch.ElapsedMilliseconds}ms");
                    break;
                case 2:
                    Watch.Restart();
                    myLogger.Log(ClassName, "Initializing step 3. Starting inventory callback management.");
                    sorterChangeHandler = new SorterChangeHandler(_mainItemStorage);
                    Initialization_Step++;
                    Watch.Stop();
                    totalInitTime += Watch.Elapsed;
                    myLogger.Log(ClassName, $"Step 3. Time taken {Watch.ElapsedMilliseconds}ms");
                    break;
                case 3:
                    Watch.Restart();
                    myLogger.Log(ClassName, "Initializing step 4. Starting trash sorter management.");
                    _modSorterMainManager =
                        new ModSorterManager(inventoryGridManager.ModSorters, _mainItemStorage,
                            inventoryGridManager, sorterChangeHandler.SorterLimitManagers,
                            _mainItemStorage.NameToDefinitionMap, _mainItemStorage.ItemsDictionary, myLogger);
                    Watch.Stop();
                    totalInitTime += Watch.Elapsed;
                    myLogger.Log(ClassName, $"Step 4. Time taken {Watch.ElapsedMilliseconds}ms");
                    myLogger.Log(ClassName, $"Total init time. Time taken {totalInitTime.Milliseconds}ms");
                    Initialization_Step++;
                    break;
            }
        }

        // Logging/Comparison logic.
        public override void UpdateAfterSimulation100()
        {
            base.UpdateAfterSimulation100();
            if (Initialization_Step < 4) return;

            inventoryGridManager.OnAfterSimulation100();
            _modSorterMainManager.OnAfterSimulation100();
            sorterChangeHandler.OnAfterSimulation100();
        }
    }
}