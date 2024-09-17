using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using Trash_Sorter.StaticComponents;
using Trash_Sorter.StaticComponents.StaticFunctions;
using Trash_Sorter.StorageClasses;
using VRage.Game;
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
        protected const string RestartCall = "[Restart]";

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

        protected void OnIsThisOwner(bool value) => IsThisOwner?.Invoke(value);
        protected event Action<bool> IsThisOwner;


        protected HashSet<IMyCubeGrid> HashCollectionGrids = new HashSet<IMyCubeGrid>();
        protected HashSet<IMyCubeGrid> HashGridToChange = new HashSet<IMyCubeGrid>();

        protected ObservableGridStorage GridStorage;

        protected IMyEntity OtherManager;
        protected IMyEntity ThisManager;
        protected bool ManagingStatus;
        public virtual void PartialDispose()
        {
        } // Partial dispose for when manager gets switched off.

        public virtual void Dispose()
        {
        } // Disposable call through entire manager
    }
}