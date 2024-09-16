using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Trash_Sorter.StaticComponents.StaticFunctions
{
    internal class ModObjectPools
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
    }
}
