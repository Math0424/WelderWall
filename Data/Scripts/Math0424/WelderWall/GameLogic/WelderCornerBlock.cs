using Sandbox.Common.ObjectBuilders;
using Sandbox.Engine.Utils;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Noise.Combiners;
using VRage.ObjectBuilders;
using VRage.Utils;
using VRageMath;
using WelderWall.Data.Scripts.Math0424.WelderWall.Util;

namespace WelderWall.Data.Scripts.Math0424.WelderWall.GameLogic
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Refinery), true, WelderManager.WelderCornerName)]
    internal class WelderCornerBlock : MyGameLogicComponent
    {
        IMyRefinery _block;
        float _powerConsumption;
        MyResourceSinkComponent _power;
        bool _drawCorners = false;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            _block = (IMyRefinery)Entity;
            NeedsUpdate = MyEntityUpdateEnum.BEFORE_NEXT_FRAME;
        }

        public override void UpdateOnceBeforeFrame()
        {
            WelderManager.UpdateTerminalControls();
            NeedsUpdate = MyEntityUpdateEnum.NONE;

            if (_block.CubeGrid.Physics == null || ((MyCubeGrid)_block.CubeGrid).IsPreview)
                return;

            NeedsUpdate = MyEntityUpdateEnum.EACH_100TH_FRAME | MyEntityUpdateEnum.EACH_FRAME;

            _power = _block.Components.Get<MyResourceSinkComponent>();
            _power.SetRequiredInputFuncByType(MyResourceDistributorComponent.ElectricityId, GetPowerRequirement);

            for (int i = 0; i < _block.InventoryCount; i++)
            {
                ((MyInventory)_block.GetInventory(i)).Constraint = new MyInventoryConstraint("Empty constraint", null, false);
                ((MyInventory)_block.GetInventory(i)).SetFlags(MyInventoryFlags.CanSend);
            }

            _block.IsWorkingChanged += CheckFunctional;
            _block.OnClose += Removed;

            WelderManager.TerminalControls.SetAllEnabled(_block, false);
            WelderManager.AddCorner(_block);
        }

        public void CheckFunctional(IMyCubeBlock block)
        {
            if (!_block.IsFunctional)
                WelderManager.SetWallDisabled(_block);
            else
                WelderManager.AddCorner(_block);
        }

        private void Removed(IMyEntity ent)
        {
            WelderManager.SetWallDisabled(_block);
        }

        public override void UpdateBeforeSimulation()
        {
            if (_drawCorners && !MyAPIGateway.Utilities.IsDedicated)
            {
                var matrix = _block.WorldMatrix;
                float length = WelderManager.Config.GetInt(ConfigOptions.MaxWallSize) * _block.CubeGrid.GridSize;
                EasyDraw.DrawLine(matrix.Translation, matrix.Backward, length, Color.Blue, .1f);
                EasyDraw.DrawLine(matrix.Translation, matrix.Right, length, Color.Blue, .1f);
            }
        }

        public override void UpdateBeforeSimulation100()
        {
            var wall = WelderManager.GetWelderWall(_block);
            _drawCorners = wall == null;
            if (wall == null)
                return;

            float newPowerConsumption = wall.RequiredPower() / 1000000;
            if (_powerConsumption !=  newPowerConsumption)
            {
                _powerConsumption = newPowerConsumption;
                _power.Update();
                ((MyResourceDistributorComponent)_block.CubeGrid.ResourceDistributor).MarkForUpdate();
            }
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
