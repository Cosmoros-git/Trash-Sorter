using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using Trash_Sorter.StaticComponents;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Trash_Sorter.BaseClass
{
    public abstract class ModBase : EventsBase, IDisposable
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

        public event Action<MyEntityUpdateEnum> UpdateRequired;
        protected void OnUpdateRequired(MyEntityUpdateEnum obj)
        {
            UpdateRequired?.Invoke(obj);
        }

        protected static bool IsTrashInventory(IMyTerminalBlock block)
        {
            return block.CustomName.IndexOf(Trash, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public virtual void PartialDispose(){} // Partial dispose for when manager gets switched off.
        public virtual void Dispose(){} // Disposable call through entire manager
    }
}