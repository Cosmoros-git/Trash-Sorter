using System;
using System.Collections.Generic;

namespace Trash_Sorter.Data.Scripts.Trash_Sorter.BaseClass
{
    public abstract class ModBase: IDisposable
    {
        public string ClassName
        {
            get { return GetType().Name; }
        }

        public const string Trash = "[TRASH]";
        public const string GuideCall = "[GUIDE]";

        protected static readonly string[] TrashSubtype = {
            "LargeTrashSorter",
            "SmallTrashSorter"
        };

        protected static readonly HashSet<string> CountedTypes = new HashSet<string>()
        {
            "MyObjectBuilder_Ingot",
            "MyObjectBuilder_Ore",
            "MyObjectBuilder_Component",
            "MyObjectBuilder_AmmoMagazine"
        };

        protected static readonly HashSet<string> NamingExceptions = new HashSet<string>()
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

        public virtual void Dispose()
        {
            
        }
    }
}
