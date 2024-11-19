using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Text;
using Voidfront;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using WelderWall.Data.Scripts.Math0424.WelderWall.Util;

namespace WelderWall.Data.Scripts.Math0424.WelderWall
{
    public enum ConfigOptions
    {
        MaxPower,
        MaxWeldSpeed,
        MaxWallSize,
        MaxWeldsPerTick,
    }

    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    internal class WelderManager : MySessionComponentBase
    {
        public static Guid StorageGUID = Guid.Parse("28ff30bb-768a-4b21-b917-86a88050a844");
        public static MyStringHash WelderCorner = MyStringHash.Get("WelderWallCorner");
        public static MyStringHash WelderPole = MyStringHash.Get("WelderWallPole");
        public static MyStringHash WelderDamage = MyStringHash.Get("Welder");
        public static EasyConfiguration Config;
        public static EasyTerminalControls<IMyCargoContainer> TerminalControls;

        private Dictionary<long, WelderGrid> WelderGrids;

        private Dictionary<Enum, object> defaultConfig = new Dictionary<Enum, object>()
        {
            { ConfigOptions.MaxPower, 100000f },
            { ConfigOptions.MaxWeldSpeed, 6 },
            { ConfigOptions.MaxWallSize, 50 },
            { ConfigOptions.MaxWeldsPerTick, 10 },
        };

        public WelderManager()
        {
            WelderGrids = new Dictionary<long, WelderGrid>();
        }

        public override void BeforeStart()
        {
            EasyNetworker.Init(22345);
            Config = new EasyConfiguration(false, "WelderWallConfig.cfg", defaultConfig);

            TerminalControls = new EasyTerminalControls<IMyCargoContainer>("WelderWallMod", WelderCorner)
                .WithSeperator()
                .WithOnOff("Powered", "Enabled", "On", "Off", Terminal_UpdateEnabled)
                .WithOnOff("Action", "Weld or Grind", "Weld", "Grind", Terminal_UpdateWeldGrind)
                .WithSlider("Power Input", "{value}kw", "Max Power Draw", 0, Config.GetFloat(ConfigOptions.MaxPower), Terminal_UpdatePower)
                .WithSeperator();

            MyAPIGateway.Entities.OnEntityAdd += EntityAdd;
            foreach (var ent in MyEntities.GetEntities())
                EntityAdd(ent);
        }

        private void Terminal_UpdateEnabled(IMyCargoContainer container, bool value)
        {
            if (WelderGrids.ContainsKey(container.CubeGrid.EntityId))
            {
                WelderWall wall = WelderGrids[container.CubeGrid.EntityId].GetWall(container);
                if (wall != null)
                {
                    wall.Enabled = value;
                    wall.UpdateTerminalControls();
                }
            }
        }

        private void Terminal_UpdateWeldGrind(IMyCargoContainer container, bool value)
        {
            if (WelderGrids.ContainsKey(container.CubeGrid.EntityId))
            {
                WelderWall wall = WelderGrids[container.CubeGrid.EntityId].GetWall(container);
                if (wall != null)
                {
                    wall.State = value ? WelderState.Weld : WelderState.Grind;
                    wall.UpdateTerminalControls();
                }
            }
        }

        private void Terminal_UpdatePower(IMyCargoContainer container, float value)
        {
            if (WelderGrids.ContainsKey(container.CubeGrid.EntityId))
            {
                WelderWall wall = WelderGrids[container.CubeGrid.EntityId].GetWall(container);
                if (wall != null)
                {
                    wall.PowerInput = value;
                    wall.UpdateTerminalControls();
                }
            }
        }

        private void EntityAdd(IMyEntity ent)
        {
            if (!(ent is IMyCubeGrid))
                return;
            IMyCubeGrid grid = (IMyCubeGrid)ent;
            if (((MyCubeGrid)grid).IsPreview || !grid.Physics.Enabled)
                return;

            var welderGrid = new WelderGrid(grid);
            welderGrid.Closed += e => WelderGrids.Remove(grid.EntityId);
            WelderGrids.Add(grid.EntityId, welderGrid);
        }

        public override void UpdateBeforeSimulation()
        {
            foreach(var wall in WelderGrids.Values)
            {
                wall.AssembleBlockList();
                wall.DispatchBlocks();
            }
        }

        public override void Draw()
        {
            foreach (var wall in WelderGrids.Values)
                wall.Draw();
        }

        public override void SaveData()
        {
            foreach(var x in WelderGrids.Values)
                x.Save();
        }

    }
}
