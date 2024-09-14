using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Trash_Sorter.BaseClass;
using Trash_Sorter.StaticComponents;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Trash_Sorter.StorageClasses
{
    public class SystemManagerStorage : GridManagement
    {
        public IMyEntity EntityI;
        public IMyCubeBlock CubeBlockI;
        public IMyCubeGrid CubeGridI;
        public MyCubeGrid CubeGridObj;
        public long IdLong;
        public string IdString;

        public byte CheckId = 0;
        // 0 none
        // 1 gridCheck
        // 2 physics


        public event Action<IMyEntity> OnBlockClosed;

        private readonly HashSet<IMyEntity> SubscribedBlock = new HashSet<IMyEntity>();

        public void SetValue(IMyEntity myEntity)
        {
            try
            {
                if (EntityI != null)
                {
                    UnSubscribeBlock(CubeBlockI);
                }
                else
                {
                    Logger.Log(ClassName, $"Old entity was null. No un-subscription needed.");
                }

                EntityI = myEntity;
                CubeBlockI = (IMyCubeBlock)EntityI;
                CubeGridI = CubeBlockI?.CubeGrid;
                CubeGridObj = (MyCubeGrid)CubeGridI;
                IdLong = CubeBlockI?.EntityId ?? 0;
                IdString = IdLong.ToString();
                SubscribeBlock(CubeBlockI);
            }
            catch (Exception ex)
            {
                Logger.LogError(ClassName, $"This is stupid. Values are {EntityI?.EntityId},{CubeBlockI?.EntityId},{ex}");
            }
        }

        private void OnBlockClosedEvent(IMyEntity myEntity)
        {
            OnBlockClosed?.Invoke(myEntity);
        }

        private void BlockOnClosing(IMyEntity myEntity)
        {
           UnSubscribeBlock(myEntity);
        }

        /// Todo check if this is even needed.
        public void ForceReferenceUpdates() // Not sure if I have to do this even.
        {
            CubeBlockI = (IMyCubeBlock)EntityI;
            CubeGridI = CubeBlockI?.CubeGrid;
            CubeGridObj = (MyCubeGrid)CubeGridI;
            IdLong = CubeBlockI?.EntityId ?? 0;
            IdString = IdLong.ToString();
           // Logger.Log(ClassName,$"{CubeBlockI?.EntityId},{CubeGridI?.EntityId},{CubeGridI?.Physics} this is stupid.");
        }

        public void ForceUpdateHooks()
        {
            SubscribeBlock(CubeBlockI);
        }

        private void SubscribeBlock(IMyEntity cubeBlockI)
        {
            if(!SubscribedBlock.Add(cubeBlockI)) return;

            cubeBlockI.OnClosing += BlockOnClosing;
            cubeBlockI.OnClosing += OnBlockClosedEvent;
        }
        private void UnSubscribeBlock(IMyEntity cubeBlockI)
        {
            if (!SubscribedBlock.Add(cubeBlockI)) return;

            cubeBlockI.OnClosing -= BlockOnClosing;
            cubeBlockI.OnClosing -= OnBlockClosedEvent;
        }

        public override void Dispose()
        {
            CubeBlockI.OnClosing -= OnBlockClosedEvent;
        }
    }
}