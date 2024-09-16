using System.Collections.Generic;
using ParallelTasks;
using Sandbox.ModAPI;
using Trash_Sorter.BaseClass;
using Trash_Sorter.StaticComponents.StaticFunctions;
using VRage.Game.ModAPI;

namespace Trash_Sorter.GridInitializerRewritten
{
    internal class GridEventsManager : GridManagerBase
    {
        private IMyGridGroupData _gridGroupDataPooled;

        public void SubscribeGridData()
        {
            _gridGroupDataPooled =
                GridFunctions.GetGridGroup(((IMyCubeBlock)ThisManager).CubeGrid, GridLinkTypeEnum.Mechanical);
            _gridGroupDataPooled.OnGridRemoved += OnGridRemoved;
            _gridGroupDataPooled.OnGridAdded += OnGridAdded;
            _gridGroupDataPooled.OnReleased += OnReleased;
        }

        public void OnReleased(IMyGridGroupData obj)
        {
            _gridGroupDataPooled.OnGridRemoved -= OnGridRemoved;
            _gridGroupDataPooled.OnGridAdded -= OnGridAdded;
            _gridGroupDataPooled.OnReleased -= OnReleased;
            _gridGroupDataPooled = null;

            GetTheGroupBack();
            InitializationStep = InitializationStepGrid.GridInfoCollection;
            OnInvokeInit();
        }

        private void GetTheGroupBack()
        {
            var tempSet = ModObjectPools.HashSetPool<IMyCubeGrid>.Get();

            try
            {
                _gridGroupDataPooled = GridFunctions.GetGridGroup(((IMyCubeBlock)ThisManager).CubeGrid, GridLinkTypeEnum.Mechanical);
                GridFunctions.TryGetConnectedGrids(((IMyCubeBlock)ThisManager).CubeGrid, GridLinkTypeEnum.Mechanical, tempSet);

                HashGridToChange = new HashSet<IMyCubeGrid>(tempSet);
                HashGridToChange.SymmetricExceptWith(HashCollectionGrids);

                SubscribeGridData();
            }
            finally
            {
                // Ensure tempSet is always returned to the pool
                ModObjectPools.HashSetPool<IMyCubeGrid>.Return(tempSet);
            }
        }

        private void OnGridAdded(IMyGridGroupData arg1, IMyCubeGrid arg2, IMyGridGroupData arg3)
        {
            if (HashCollectionGrids.Add(arg2))
            {
                OnGridAdded(arg2);
            }
        }

        private void OnGridRemoved(IMyGridGroupData arg1, IMyCubeGrid arg2, IMyGridGroupData arg3)
        {
            if (HashCollectionGrids.Remove(arg2))
            {
                OnGridRemoved(arg2);
            }
        }
    }
}