using Sandbox.Common.ObjectBuilders;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace WelderWall.Data.Scripts.Math0424.WelderWall.GameLogic
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_ConveyorConnector), true, WelderManager.WelderPoleName)]
    internal class WelderFrameBlock : MyGameLogicComponent
    {
        IMyCubeBlock _block;
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            _block = (IMyCubeBlock)Entity;
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            _block.IsWorkingChanged += CheckFunctional;
            _block.OnClose += Removed;
        }

        public void CheckFunctional(IMyCubeBlock block)
        {
            if (!_block.IsWorking)
                WelderManager.SetWallDisabled(_block);
            else
                WelderManager.CheckWallConnected(_block);
        }

        private void Removed(IMyEntity ent)
        {
            WelderManager.SetWallDisabled(_block);
        }
    }
}
