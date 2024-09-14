using System;
using Sandbox.ModAPI;
using Trash_Sorter.GridManagerRewritten;
using Trash_Sorter.GridManagers;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Trash_Sorter.BaseClass
{
    public abstract class EventsBase
    {
 
        public event Action DisposeRequired;

        protected void OnDisposeRequired()
        {
            DisposeRequired?.Invoke();
        }

        protected void OnDisposeRequiredHandler(IMyEntity _)
        {
            OnDisposeRequired();
        }



        protected event Action<IMyCubeGrid, IMyCubeGrid> GridMergeInvoked;
        protected event Action<IMyCubeGrid, IMyCubeGrid> GridSplitInvoked;


        protected void OnGridMergeInvoked(IMyCubeGrid arg1, IMyCubeGrid arg2)
        {
            GridMergeInvoked?.Invoke(arg1, arg2);
        }

        protected void OnGridSplitInvoked(IMyCubeGrid arg1, IMyCubeGrid arg2)
        {
            GridSplitInvoked?.Invoke(arg1, arg2);
        }



        protected event Action<IMyCubeGrid> GridAdded;
        protected event Action<IMyCubeGrid> GridRemoved;
        protected event Action<IMyCubeGrid> GridUpdated;
        protected event Action<IMyCubeGrid> GridOverriden;



        protected void OnGridAdded(IMyCubeGrid obj)
        {
            GridAdded?.Invoke(obj);
        }
        protected void OnGridUpdated(IMyCubeGrid obj)
        {
            GridUpdated?.Invoke(obj);
        }
        protected void OnGridRemoved(IMyCubeGrid obj)
        {
            GridRemoved?.Invoke(obj);
        }
        protected void OnGridOverriden(IMyCubeGrid obj)
        {
            GridOverriden?.Invoke(obj);
        }




        protected event Action<IMyCubeBlock> BlockAdded;
        protected event Action<IMyCubeBlock> BlockRemoved;
        protected event Action<IMyConveyorSorter> ModSorterAdded;




        protected void OnBlockAdded(IMyCubeBlock obj)
        {
            BlockAdded?.Invoke(obj);
        }
        protected void OnBlockRemoved(IMyCubeBlock obj)
        {
            BlockRemoved?.Invoke(obj);
        }
        protected void OnModSorterAdded(IMyConveyorSorter obj)
        {
            ModSorterAdded?.Invoke(obj);
        }




        protected event Action<byte> EnterStandBy;
        protected event Action<byte> ExitStandBy;
        protected event Action<IMyEntity> ManagerDeleted;
        protected event Action<ItemGridManager> InventoryLink; 



        protected void OnManagerDeleted(IMyEntity obj)
        {
            ManagerDeleted?.Invoke(obj);
        }
        protected void OnEnterStandBy(byte obj)
        {
            EnterStandBy?.Invoke(obj);
        }
        protected void OnExitStandBy(byte obj)
        {
            ExitStandBy?.Invoke(obj);
        }
        protected virtual void OnInventoryLink(ItemGridManager obj)
        {
            InventoryLink?.Invoke(obj);
        }



    }
}