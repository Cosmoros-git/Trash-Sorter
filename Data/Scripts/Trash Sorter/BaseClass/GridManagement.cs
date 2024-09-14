using Trash_Sorter.StorageClasses;

namespace Trash_Sorter.BaseClass
{
    public abstract class GridManagement : ModBase
    {
        private const string ClassNameStatic = "GridManagement";
        
        protected static bool IsThisManager(int notManagedCount, int managedCount, SystemManagerStorage thisManager,
            SystemManagerStorage otherManager)
        {
            return notManagedCount != managedCount
                ? notManagedCount <= managedCount
                : thisManager.IdLong > otherManager.IdLong;
        }
    }
}