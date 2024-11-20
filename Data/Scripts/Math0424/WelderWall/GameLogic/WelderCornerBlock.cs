using Sandbox.Game.EntityComponents;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using WelderWall.Data.Scripts.Math0424.WelderWall.Util;

namespace WelderWall.Data.Scripts.Math0424.WelderWall.GameLogic
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_CargoContainer), true, WelderManager.WelderCornerName)]
    internal class WelderCornerBlock : MyGameLogicComponent
    {
        IMyCubeBlock _block;
        float _powerConsumption;
        MyResourceSinkComponent _powerSystem;
        bool _drawCorners = false;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            _block = (IMyCubeBlock)Entity;
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME | MyEntityUpdateEnum.EACH_100TH_FRAME | MyEntityUpdateEnum.EACH_FRAME;

            _powerSystem = new MyResourceSinkComponent();
            _powerSystem.Init(MyStringHash.GetOrCompute("Utility"), 100, GetPowerRequirement, null);
            Entity.Components.Add(_powerSystem);
            _powerSystem.Update();
        }

        public void CheckFunctional(IMyCubeBlock block)
        {
            if (!_block.IsWorking)
                WelderManager.SetWallDisabled(_block);
            else
                WelderManager.AddCorner(_block);
        }

        private void Removed(IMyEntity ent)
        {
            WelderManager.SetWallDisabled(_block);
        }

        public override void UpdateOnceBeforeFrame()
        {
            _block.IsWorkingChanged += CheckFunctional;
            _block.OnClose += Removed;

            WelderManager.AddCorner(_block);
            WelderManager.TerminalControls.SetEnabled(_block, false);
        }

        public override void UpdateBeforeSimulation()
        {
            if (_drawCorners)
            {
                var matrix = _block.WorldMatrix;
                float length = WelderManager.Config.GetInt(ConfigOptions.MaxWallSize) * _block.CubeGrid.GridSize;
                EasyDraw.DrawLine(matrix.Translation, matrix.Forward, length, Color.Blue, .1f);
                EasyDraw.DrawLine(matrix.Translation, matrix.Left, length, Color.Blue, .1f);
            }
        }

        public override void UpdateBeforeSimulation100()
        {
            var wall = WelderManager.GetWelderWall(_block);
            _drawCorners = wall == null;
            if (wall == null)
                return;

            _powerConsumption = wall.RequiredPower();
            _powerSystem.Update();
        }

        public float GetPowerRequirement()
        {
            if (!_block.IsFunctional)
                return 0;
            return _powerConsumption;
        }

        public override void Close()
        {
            WelderManager.RemoveCorner(_block);
        }

    }
}
