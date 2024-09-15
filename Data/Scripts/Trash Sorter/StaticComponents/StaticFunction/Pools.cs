using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Trash_Sorter.Data.Scripts.Trash_Sorter.StaticComponents.StaticFunction
{
    internal class Pools
    {
        public static class ListPool<T>
        {
            private static readonly Stack<List<T>> _pool = new Stack<List<T>>();

            public static List<T> Get()
            {
                return _pool.Count > 0 ? _pool.Pop() : new List<T>();
            }

            public static void Return(List<T> list)
            {
                list.Clear();  // Ensure the list is empty before returning it to the pool
                _pool.Push(list);
            }
        }
    }
}
