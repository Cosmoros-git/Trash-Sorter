using System.Collections.Generic;
using Trash_Sorter.BaseClass;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Trash_Sorter.GridInitializerRewritten
{
    public abstract class GridManagerBase:ModBase
    {
        protected HashSet<IMyCubeGrid> HashCollectionGrids = new HashSet<IMyCubeGrid>();
        protected HashSet<IMyCubeGrid> HashGridToRemove = new HashSet<IMyCubeGrid>();

        protected bool IsThisManager = false;
        protected IMyEntity OtherManager;
        protected IMyEntity ThisManager;

    }
}
