using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace WelderWall.Data.Scripts.Math0424.WelderWall.GameLogic
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ConveyorConnector), true, WelderManager.WelderPoleName)]
    internal class WelderFrameBlock : MyGameLogicComponent
    {
        IMyCubeBlock _block;
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            _block = (IMyCubeBlock)Entity;
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            if (_block.CubeGrid.Physics == null || ((MyCubeGrid)_block.CubeGrid).IsPreview)
                return;

            _block.IsWorkingChanged += CheckFunctional;
            _block.OnClose += Removed;

            UpdateEmissives();
            if (_block.IsWorking)
                WelderManager.CheckWallConnected(_block);
        }

        public void CheckFunctional(IMyCubeBlock block)
        {
            UpdateEmissives();
            if (!_block.IsWorking)
                WelderManager.SetWallDisabled(_block);
            else
                WelderManager.CheckWallConnected(_block);
        }

        public override void UpdateAfterSimulation100()
        {
            UpdateEmissives();
        }

        private void UpdateEmissives()
        {
            if (!_block.IsFunctional)
            {
                _block.SetEmissiveParts("Emissive0", Color.Red, 1f);
                return;
            }

            if (!_block.IsWorking || !WelderManager.IsWallWorking(_block))
            {
                _block.SetEmissiveParts("Emissive0", Color.Yellow, 1f);
                return;
            }
            
            _block.SetEmissiveParts("Emissive0", Color.Green, 1f);
        }

        private void Removed(IMyEntity ent)
        {
            WelderManager.SetWallDisabled(_block);
        }
    }
}
