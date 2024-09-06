using System;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Trash_Sorter.Data.Scripts.Trash_Sorter.BaseClass
{



    public abstract class ModBase: IDisposable
    {
        // Logging feature to not write class names constantly.
        public string ClassName => GetType().Name;

        // Tags for info or change of status.
        public const string Trash = "[TRASH]";
        public const string GuideCall = "[GUIDE]";

        // Subtype id of blocks I use as sorter.
        protected static readonly string[] TrashSubtype = {
            "LargeTrashSorter",
            "SmallTrashSorter"
        };

        protected static readonly HashSet<string> CountedTypes = new HashSet<string>() // Object builders I care about. Also thing of a past.
        {
            "MyObjectBuilder_Ingot",
            "MyObjectBuilder_Ore",
            "MyObjectBuilder_Component",
            "MyObjectBuilder_AmmoMagazine"
        };

        protected static readonly HashSet<string> NamingExceptions = new HashSet<string>() // At end I did not add ingot to everything lmao.
        {
            "Stone",
            "Ice",
            "Crude Oil",
            "Coal",
            "Scrap Metal",
        };

        protected static readonly HashSet<string> UniqueModExceptions = new HashSet<string>()
        {
            "Heat",
        };


        public event Action<MyEntityUpdateEnum> NeedsUpdate;

        public void OnNeedsUpdate(MyEntityUpdateEnum obj)
        {
            NeedsUpdate?.Invoke(obj);
        }

        public event Action DisposeInvoke;

        public void OnDisposeInvoke()
        {
            DisposeInvoke?.Invoke();
        }

        public event Action<IMyCubeGrid> GridDispose;

        public void OnGridDispose(IMyCubeGrid obj)
        {
            GridDispose?.Invoke(obj);
        }

        public virtual void Dispose()
        {
            
        }
    }
}
