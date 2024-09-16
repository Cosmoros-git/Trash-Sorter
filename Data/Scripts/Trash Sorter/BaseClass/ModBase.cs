using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using Trash_Sorter.StaticComponents;
using Trash_Sorter.StaticComponents.StaticFunctions;
using Trash_Sorter.StorageClasses;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Trash_Sorter.BaseClass
{
    public abstract class ModBase : IDisposable
    {
        // Logging feature to not write class names constantly.
        protected string ClassName => GetType().Name;
        protected static readonly Guid ModGuid = ModSessionComponent.Guid;


        // Tags for info or change of status.
        protected const string Trash = "[TRASH]";
        protected const string GuideCall = "[GUIDE]";
        
        // Subtype id of blocks I use as sorter.
        protected static readonly string[] TrashSubtype =
        {
            "LargeTrashSorter",
            "SmallTrashSorter"
        };

        
        protected void OnUpdateRequired(MyEntityUpdateEnum obj) => UpdateRequired?.Invoke(obj);
        protected event Action<MyEntityUpdateEnum> UpdateRequired;

        protected void OnGridAdded(IMyCubeGrid grid) => GridAdded?.Invoke(grid);
        protected event Action<IMyCubeGrid> GridAdded;
        protected void OnGridRemoved(IMyCubeGrid grid) => GridRemoved?.Invoke(grid);
        protected event Action<IMyCubeGrid> GridRemoved;


        protected HashSet<IMyCubeGrid> HashCollectionGrids = new HashSet<IMyCubeGrid>();
        protected HashSet<IMyCubeGrid> HashGridToChange = new HashSet<IMyCubeGrid>();

        public HashSet<IMyCubeGrid> HashSetArg1 = new HashSet<IMyCubeGrid>();
        public HashSet<IMyCubeGrid> HashSetArg2 = new HashSet<IMyCubeGrid>();

        public ObservableGridStorage GridStorage;

        protected IMyEntity OtherManager;
        protected IMyEntity ThisManager;

        protected static bool IsTrashInventory(IMyTerminalBlock block)
        {
            return block.CustomName.IndexOf(Trash, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        protected virtual void PartialDispose(){} // Partial dispose for when manager gets switched off.
        public virtual void Dispose(){} // Disposable call through entire manager
    }
}