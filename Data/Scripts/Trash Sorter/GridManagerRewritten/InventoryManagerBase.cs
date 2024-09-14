using System.Collections.Generic;
using System.Runtime.InteropServices;
using Sandbox.Game;
using Trash_Sorter.BaseClass;
using Trash_Sorter.StaticComponents.StaticFunction;
using VRage.Game.ModAPI;

namespace Trash_Sorter.GridManagerRewritten
{
    public class InventoryManagerBase : ModBase
    {
        public GridFunctions.GridProcessingResult GridInitializationResult;
        protected HashSet<IMyCubeGrid> ManagedGrids;
        protected HashSet<IMyCubeBlock> ManageBlocks;
    }
}