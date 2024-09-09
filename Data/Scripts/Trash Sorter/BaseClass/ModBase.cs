using System;
using System.Collections.Generic;
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Trash_Sorter.Data.Scripts.Trash_Sorter.BaseClass
{
    public abstract class ModBase : IDisposable
    {
        // Logging feature to not write class names constantly.
        public string ClassName => GetType().Name;

        // Tags for info or change of status.
        public const string Trash = "[TRASH]";
        public const string GuideCall = "[GUIDE]";

        // Subtype id of blocks I use as sorter.
        protected static readonly string[] TrashSubtype =
        {
            "LargeTrashSorter",
            "SmallTrashSorter"
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

        public event Action<IMyCubeGrid> GridAdd;

        public void OnGridAdded(IMyCubeGrid obj)
        {
            GridAdd?.Invoke(obj);
        }

        protected static bool CanUseConveyorSystem(IMyTerminalBlock block)
        {
            return (block is IMyCargoContainer ||
                    block is IMyConveyorSorter ||
                    block is IMyProductionBlock ||
                    block is IMyShipConnector ||
                    block is IMyCollector ||
                    block is IMyShipDrill ||
                    block is IMyShipGrinder ||
                    block is IMyShipWelder ||
                    block is IMyReactor ||
                    block is IMyGasTank ||
                    block is IMyGasGenerator ||
                    block is IMyPowerProducer);
        }
        protected static bool IsTrashInventory(IMyTerminalBlock block)
        {
            return block.CustomName.IndexOf(Trash, StringComparison.OrdinalIgnoreCase) >= 0;
        }
        protected static void GetConnectedGrids(IMyCubeGrid grid, out HashSet<IMyCubeGrid> connectedGrids)
        {
            connectedGrids = new HashSet<IMyCubeGrid>();
            grid.GetGridGroup(GridLinkTypeEnum.Mechanical).GetGrids(connectedGrids);
        }

        public virtual void Dispose()
        {
        }
    }
}