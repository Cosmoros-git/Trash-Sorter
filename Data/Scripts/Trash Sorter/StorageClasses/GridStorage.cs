using System.Collections.Generic;
using Trash_Sorter.BaseClass;
using Trash_Sorter.GridManagers;
using Trash_Sorter.StaticComponents;
using VRage.Game.ModAPI;

namespace Trash_Sorter.StorageClasses
{
    public class GridStorage : GridManagement
    {
        public readonly SystemManagerStorage ThisManager = new SystemManagerStorage();
        public readonly SystemManagerStorage OtherManager = new SystemManagerStorage();

        public readonly HashSet<IMyCubeGrid> ManagedGrids = new HashSet<IMyCubeGrid>();

        public static readonly int MinAmountOfBlocks = ModSessionComponent.BlockLimitsToStartManaging;
        public static readonly int MinTimeBetweenActivations = ModSessionComponent.UpdateCooldownLimit;

        public readonly GridEventManager EventManager;
        public readonly GridConnectionManager GridConnectionManager;
        public readonly GridOwnerManager GridOwnerManager;

        public bool IsOnline;
        public bool IsOnStandBy;

        public bool HasSceneErrorBeenShown;
        public bool BasicLevelBlockVerified;
        public bool HasPhysicsErrorBeenShown;
        public bool GridTooSmallError;
        public bool ConflictDetected;

        public GridStorage()
        {
         
            GridOwnerManager = new GridOwnerManager(this);
            GridConnectionManager = new GridConnectionManager(this);
            EventManager = new GridEventManager(this,GridOwnerManager, GridConnectionManager);

        }

       
    }
}