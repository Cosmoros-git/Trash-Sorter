using System.Collections.Concurrent;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using static Trash_Sorter.StaticComponents.StaticFunctions.GridFunctions;

namespace Trash_Sorter.StaticComponents.StaticFunctions
{
    public class ModObjectPools
    {
        public static class HashSetPool<T>
        {
            private static readonly ConcurrentStack<HashSet<T>> Pool = new ConcurrentStack<HashSet<T>>();
            private const int MaxPoolSize = 100; // Set a limit to prevent uncontrolled growth

            public static HashSet<T> Get()
            {
                // Try to get an object from the pool, otherwise create a new one
                HashSet<T> hashSet;
                return Pool.TryPop(out hashSet) ? hashSet : new HashSet<T>();
            }

            public static void Return(HashSet<T> hashSet)
            {
                if (hashSet == null) return; // Avoid adding null objects

                hashSet.Clear(); // Reset state of the HashSet

                // Only return to the pool if we haven't exceeded the pool size limit
                if (Pool.Count < MaxPoolSize)
                {
                    Pool.Push(hashSet);
                }
            }
        }
        /// <summary>
        /// Represents the result of processing grids that are connected to a reference grid.
        /// This class categorizes connected grids into four groups:
        /// <para>- Owned grids: Grids that are already owned by the system managing them.</para>
        /// <para>- Unowned grids: Grids that are not yet claimed by any system.</para>
        /// <para>- Grids owned by a different system: Grids managed by a different system than the one performing the processing.</para>
        /// <para>- Connected grids: All grids that are connected to the reference grid.</para>
        /// </summary>
        public class GridProcessingResult
        {
            /// <summary>
            /// Gets or sets the set of grids that are owned by the system performing the grid processing.
            /// </summary>
            public HashSet<IMyCubeGrid> OwnedGrids { get; set; } = new HashSet<IMyCubeGrid>();

            /// <summary>
            /// Gets or sets the set of grids that are not yet owned by any system.
            /// </summary>
            public HashSet<IMyCubeGrid> UnownedGrids { get; set; } = new HashSet<IMyCubeGrid>();

            /// <summary>
            /// Gets or sets the set of grids that are owned by a different system or manager.
            /// These grids are managed by a system with a different ID from the one performing the grid processing.
            /// </summary>
            public HashSet<IMyCubeGrid> ForeignOwnedGrids { get; set; } = new HashSet<IMyCubeGrid>();

            /// <summary>
            /// Hash set of manager ids, should never go above 1.
            /// If its size = 0 there is no other manager.
            /// </summary>
            public HashSet<string> OtherManagerId { get; set; } = new HashSet<string>();
        }
        public static class GridProcessingResultPool
        {
            private static readonly Stack<GridProcessingResult> Pool = new Stack<GridProcessingResult>();
            private const int MaxPoolSize = 100; // Limit pool size to prevent excessive growth

            public static GridProcessingResult Get()
            {
                return Pool.Count > 0 ? Pool.Pop() : new GridProcessingResult();
            }

            public static void Return(GridProcessingResult result)
            {
                // Clear internal HashSets to avoid carrying over state between uses
                result.OwnedGrids.Clear();
                result.UnownedGrids.Clear();
                result.ForeignOwnedGrids.Clear();
                result.ConnectedGrids.Clear();
                result.OtherManagerId.Clear();
                result.OtherManagerEntity.Clear();

                // Return to the pool if the pool hasn't reached its maximum size
                if (Pool.Count < MaxPoolSize)
                {
                    Pool.Push(result);
                }
            }
        }
    }
}
