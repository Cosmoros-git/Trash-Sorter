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
    public class ModHeartbeatCore : MyGameLogicComponent
    {
        // ReSharper disable once InconsistentNaming

        private readonly Guid Guid = new Guid("f6ea728c-8890-4012-8c81-165593a65b86");
        private const string ClassName = "Main-Class";
        private readonly HashSet<IMyCubeGrid> connectedGrids = new HashSet<IMyCubeGrid>();
        private IMyCubeBlock block;
        public static IMyCubeGrid GridOwner;
        public Stopwatch Watch = new Stopwatch();

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            base.Init(objectBuilder);
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
            MyLog.Default.WriteLine("Trash Sorter starting up");
        }

        private bool IsOnline;
        private bool IsOnStandBy;
        private bool IsOtherManagerGone;
        private string OtherManagerId;
        private int Initialization_Step;

        // ReSharper disable once NotAccessedField.Local
        private Logger _logger;

        public override void UpdateOnceBeforeFrame()
        {
            base.UpdateOnceBeforeFrame();

            if (IsOnline)
            {
                // If the block is already online, continue with regular updates
                NeedsUpdate = MyEntityUpdateEnum.EACH_10TH_FRAME;
                return;
            }

            if (VerifyBlock())
            {
                // If the block was successfully verified, start regular updates
                MyLog.Default.WriteLine("Trash Sorter startup finished");
                NeedsUpdate = MyEntityUpdateEnum.EACH_10TH_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME;
            }
            else
            {
                if (!IsOnStandBy)
                {
                    // If the block is not verified, and we're not on standby, retry next frame
                    NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                }
                else
                {
                    // If we fail to verify after retrying, enter standby mode
                    MyLog.Default.WriteLine("Trash Sorter could not start. Entering standby.");
                    NeedsUpdate = MyEntityUpdateEnum.NONE;
                }
            }
        }

        private bool VerifyBlock()
        {
            if (IsOnline) return true; // Early exit if already online
            if (IsOtherManagerGone) return true; // Removed extra checks on load from standby

            MyLog.Default.WriteLine("Trash Sorter starting up");

            block = Entity as IMyCubeBlock;
            if (block == null)
            {
                MyLog.Default.WriteLine("Entity is not a valid IMyCubeBlock.");
                return false;
            }

            // Check if the block's grid is valid
            if (block.CubeGrid == null)
            {
                MyLog.Default.WriteLine("CubeGrid is null.");
                return false;
            }

            // Check if the block has physics enabled
            if (block.CubeGrid.Physics == null)
            {
                MyLog.Default.WriteLine("Physics is null.");
                return false;
            }

            // Check if GridManagement passes
            if (!GridManagement(block))
            {
                MyLog.Default.WriteLine("GridManagement failed.");
                IsOnStandBy = true;
                return false;
            }

            // If all checks pass, proceed with startup
            _logger = new Logger(block.EntityId.ToString());
            block.OnMarkForClose += Block_OnMarkForClose;
            GridOwner = block.CubeGrid;
            IsOnline = true; // Mark as online after successful verification

            return true;
        }


        private void Block_OnMarkForClose(IMyEntity obj)
        {
            // Moved entire dispose into earlier method to be sure it does its job.
            obj.OnClosing -= Block_OnMarkForClose;
            _mainItemStorage.Dispose();
        }

        private bool GridManagement(IMyCubeBlock iMyBlock)
        {
            var blockId = iMyBlock.EntityId.ToString();
            var isThisManager = false;
            var hasSubscribed = false;

            iMyBlock.CubeGrid.GetGridGroup(GridLinkTypeEnum.Mechanical)?.GetGrids(connectedGrids);

            foreach (var grid in connectedGrids)
            {
                var storage = grid.Storage;

                if (storage == null)
                {
                    MyLog.Default.WriteLine($"{grid.DisplayName} storage null grid marked as managed by this block");
                    storage = new MyModStorageComponent();
                    grid.Storage = storage;
                    storage.Add(Guid, blockId);
                    isThisManager = true;
                    continue;
                }

                string storedBlockId;
                if (storage.ContainsKey(Guid))
                {
                    storedBlockId = storage[Guid];
                }
                else
                {
                    MyLog.Default.WriteLine(
                        $"{grid.DisplayName} string parse failed, grid marked as managed by this block");
                    storage.Add(Guid, blockId);
                    isThisManager = true;
                    continue;
                }

                if (storedBlockId == blockId)
                {
                    MyLog.Default.WriteLine($"{grid.DisplayName} blockId is equal, grid is managed by this block");
                    isThisManager = true;
                    continue;
                }

                if (string.IsNullOrEmpty(storedBlockId))
                {
                    MyLog.Default.WriteLine(
                        $"{grid.DisplayName}stored string is empty, grid marked as managed by this block");
                    storage.SetValue(Guid, blockId);
                    isThisManager = true;
                    continue;
                }

                long storedId;
                if (!long.TryParse(storedBlockId, out storedId))
                {
                    MyLog.Default.WriteLine(
                        $"{grid.DisplayName} parse to long failed, grid marked as managed by this block");
                    storage.SetValue(Guid, blockId);
                    isThisManager = true;
                    continue;
                }

                IMyEntity entity;
                if (MyAPIGateway.Entities.TryGetEntityById(storedId, out entity))
                {
                    if (hasSubscribed) continue;
                    MyLog.Default.WriteLine($"{grid.DisplayName} grid is not managed by this block");
                    entity.OnClose += OwnerBlock_OnMarkForClose;
                    OtherManagerId = entity.EntityId.ToString();
                    hasSubscribed = true;
                }
                else
                {
                    MyLog.Default.WriteLine(
                        $"{grid.DisplayName} Other block was not found, grid marked as managed by this block");
                    storage.SetValue(Guid, blockId);
                    isThisManager = true;
                }
            }

            return isThisManager;
        }

        private void OwnerBlock_OnMarkForClose(IMyEntity obj)
        {
            IsOnStandBy = false;
            obj.OnClose -= OwnerBlock_OnMarkForClose;
            if (!OverrideManagerBlock())
            {
                IsOnStandBy = true;
                IsOtherManagerGone = false;
                MyLog.Default.WriteLine("Trash Sorter could not start. Entering standby.");
            }

            IsOtherManagerGone = true;
        }

        private bool OverrideManagerBlock()
        {
            var myCubeBlock = (IMyCubeBlock)Entity;
            var blockId = myCubeBlock.EntityId.ToString();
            var isThisManager = false;
            var hasSubscribed = false;

            // Retrieve connected grids
            myCubeBlock.CubeGrid.GetGridGroup(GridLinkTypeEnum.Mechanical)?.GetGrids(connectedGrids);

            MyLog.Default.WriteLine($"{connectedGrids.Count} Amount of grids overriding.");

            foreach (var grid in connectedGrids)
            {
                // Synchronize access to the grid's storage
                var storage = grid.Storage;

                string storedBlockId;
                storage.TryGetValue(Guid, out storedBlockId);

                if (string.IsNullOrEmpty(storedBlockId))
                {
                    MyLog.Default.WriteLine(
                        $"{grid.DisplayName}: Override manager on destruction, old {storedBlockId} grid marked as managed by block {myCubeBlock.EntityId}");

                    // Assign the block as the manager with a timestamp or priority
                    storage.SetValue(Guid, blockId);

                    isThisManager = true;
                    NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                    continue;
                }

                if (storedBlockId != blockId && storedBlockId == OtherManagerId)
                {
                    MyLog.Default.WriteLine(
                        $"{grid.DisplayName}: Override manager on destruction, old {storedBlockId} grid marked as managed by block {myCubeBlock.EntityId}");

                    // Assign the block as the manager with a timestamp or priority
                    storage.SetValue(Guid, blockId);

                    isThisManager = true;
                    NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
                    continue;
                }

                if (hasSubscribed) continue;

                IMyEntity entity;
                if (MyAPIGateway.Entities.TryGetEntityById(long.Parse(storedBlockId), out entity))
                {
                    if (entity.EntityId == Entity.EntityId) continue;

                    MyLog.Default.WriteLine(
                        $"{grid.DisplayName}: Override manager on destruction failed, grid is not managed by this block");
                    entity.OnClose += OwnerBlock_OnMarkForClose;
                    OtherManagerId = entity.EntityId.ToString();
                    isThisManager = false;
                    hasSubscribed = true;
                }
                else
                {
                    MyLog.Default.WriteLine(
                        $"{grid.DisplayName}: Override manager on destruction, grid is marked as managed by block");

                    storage.SetValue(Guid, blockId);
                    isThisManager = true;
                }
            }

            return isThisManager;
        }


        // Storing files so GC won't sudo rm rf them.
        private MainItemStorage _mainItemStorage;
        private InventoryGridManager inventoryGridManager;
        private ModConveyorManager modConveyorMainManager;
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
                    Logger.Instance.Log(ClassName, "Initializing step 1. Creating item storage.");
                    _mainItemStorage = new Main_Storage_Class.MainItemStorage();
                    Initialization_Step++;
                    Watch.Stop();
                    totalInitTime += Watch.Elapsed;
                    Logger.Instance.Log(ClassName, $"Step 1. Time taken {Watch.ElapsedMilliseconds}ms");
                    break;

                case 1:
                    Watch.Restart();
                    Logger.Instance.Log(ClassName, "Initializing step 2. Starting grid inventory management.");
                    inventoryGridManager = new InventoryGridManager(_mainItemStorage, connectedGrids, GridOwner);
                    Initialization_Step++;
                    Watch.Stop();
                    totalInitTime += Watch.Elapsed;
                    Logger.Instance.Log(ClassName, $"Step 2. Time taken {Watch.ElapsedMilliseconds}ms");
                    break;
                case 2:
                    Watch.Restart();
                    Logger.Instance.Log(ClassName, "Initializing step 3. Starting inventory callback management.");
                    sorterChangeHandler = new SorterChangeHandler(_mainItemStorage);
                    Initialization_Step++;
                    Watch.Stop();
                    totalInitTime += Watch.Elapsed;
                    Logger.Instance.Log(ClassName, $"Step 3. Time taken {Watch.ElapsedMilliseconds}ms");
                    break;
                case 3:
                    Watch.Restart();
                    Logger.Instance.Log(ClassName, "Initializing step 4. Starting trash sorter management.");
                    modConveyorMainManager =
                        new ModConveyorManager(inventoryGridManager.ModSorters, _mainItemStorage,
                            inventoryGridManager, sorterChangeHandler.SorterLimitManagers,
                            _mainItemStorage.NameToDefinitionMap, _mainItemStorage.ItemsDictionary);
                    Watch.Stop();
                    totalInitTime += Watch.Elapsed;
                    Logger.Instance.Log(ClassName, $"Step 4. Time taken {Watch.ElapsedMilliseconds}ms");
                    Logger.Instance.Log(ClassName, $"Total init time. Time taken {totalInitTime.Milliseconds}ms");
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
            modConveyorMainManager.OnAfterSimulation100();
        }
    }
}