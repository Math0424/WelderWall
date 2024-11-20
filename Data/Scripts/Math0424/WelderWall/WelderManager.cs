using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.Utils;
using WelderWall.Data.Scripts.Math0424.WelderWall.Util;

namespace WelderWall.Data.Scripts.Math0424.WelderWall
{
    public enum ConfigOptions
    {
        Speed,
        MaxPower,
        MaxWallSize,
        MaxActionsPerTick,
    }

    /// <summary>
    /// Highest overview of our class structure, manages how much time each grid can consume
    /// </summary>
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    internal class WelderManager : MySessionComponentBase
    {
        public const string WelderCornerName = "WelderWallCorner";
        public const string WelderPoleName = "WelderWallPole";


        public static Guid StorageGUID = Guid.Parse("28ff30bb-768a-4b21-b917-86a88050a844");
        public static MyStringHash WelderCorner = MyStringHash.Get(WelderCornerName);
        public static MyStringHash WelderPole = MyStringHash.Get(WelderPoleName);
        public static MyStringHash WelderDamage = MyStringHash.Get("Welder");
        public static EasyConfiguration Config;
        public static EasyTerminalControls<IMyCargoContainer> TerminalControls;

        private static Dictionary<long, WelderGrid> WelderGrids;

        private Dictionary<Enum, object> defaultConfig = new Dictionary<Enum, object>()
        {
            { ConfigOptions.Speed, 0.25f },
            { ConfigOptions.MaxPower, 1000000f },
            { ConfigOptions.MaxWallSize, 50 },
            { ConfigOptions.MaxActionsPerTick, 100 },
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
                .WithSlider("Power Input", SliderFormat, "Max Power Draw", 0, Config.GetFloat(ConfigOptions.MaxPower), Terminal_UpdatePower)
                .WithSeperator();
        }

        private void SliderFormat(IMyTerminalBlock block, float value, StringBuilder sb)
        {
            string unit;
            float adjustedValue;

            if (value >= 1000000000) // Gigawatts
            {
                unit = "GW";
                adjustedValue = value / 1000000000f;
            }
            else if (value >= 1000000) // Megawatts
            {
                unit = "MW";
                adjustedValue = value / 1000000f;
            }
            else if (value >= 1000) // Kilowatts
            {
                unit = "kW";
                adjustedValue = value / 1000f;
            }
            else // Watts
            {
                unit = "W";
                adjustedValue = value;
            }

            sb.Append($"{Math.Round(adjustedValue, 2)} {unit}");
        }

        private void Terminal_UpdateEnabled(IMyCargoContainer container, bool value)
        {
            if (WelderGrids.ContainsKey(container.CubeGrid.EntityId))
            {
                WelderWall wall = WelderGrids[container.CubeGrid.EntityId].GetWallByCorner(container);
                if (wall != null)
                {
                    wall.Enabled = value;
                    wall.UpdateTerminalControls();
                    WelderGrids[container.CubeGrid.EntityId].Save();
                }
            }
        }

        private void Terminal_UpdateWeldGrind(IMyCargoContainer container, bool value)
        {
            if (WelderGrids.ContainsKey(container.CubeGrid.EntityId))
            {
                WelderWall wall = WelderGrids[container.CubeGrid.EntityId].GetWallByCorner(container);
                if (wall != null)
                {
                    wall.State = value ? WelderState.Weld : WelderState.Grind;
                    wall.UpdateTerminalControls();
                    WelderGrids[container.CubeGrid.EntityId].Save();
                }
            }
        }

        private void Terminal_UpdatePower(IMyCargoContainer container, float value)
        {
            if (WelderGrids.ContainsKey(container.CubeGrid.EntityId))
            {
                WelderWall wall = WelderGrids[container.CubeGrid.EntityId].GetWallByCorner(container);
                if (wall != null)
                {
                    wall.PowerInput = value;
                    wall.UpdateTerminalControls();
                    WelderGrids[container.CubeGrid.EntityId].Save();
                }
            }
        }


        public static void AddCorner(IMyCubeBlock block)
        {
            IMyCubeGrid grid = block.CubeGrid;
            if (((MyCubeGrid)grid).IsPreview || !grid.Physics.Enabled)
                return;

            if (!WelderGrids.ContainsKey(grid.EntityId))
            {
                var tmp = new WelderGrid(grid);
                WelderGrids.Add(grid.EntityId, tmp);
                tmp.Closed += e => WelderGrids.Remove(grid.EntityId);
            }

            WelderGrids[grid.EntityId].AddCorner(block);
        }

        public static void RemoveCorner(IMyCubeBlock block)
        {
            IMyCubeGrid grid = block.CubeGrid;
            if (((MyCubeGrid)grid).IsPreview || !grid.Physics.Enabled)
                return;

            if (!WelderGrids.ContainsKey(grid.EntityId))
                return;

            WelderGrids[grid.EntityId].RemoveWall(block);
        }

        public static WelderWall GetWelderWall(IMyCubeBlock block)
        {
            if (!WelderGrids.ContainsKey(block.CubeGrid.EntityId))
                return null;

            var wall = WelderGrids[block.CubeGrid.EntityId].GetWallAny(block);
            if (wall != null)
                return wall;

            return null;
        }

        public static void SetWallDisabled(IMyCubeBlock block)
        {
            if (!WelderGrids.ContainsKey(block.CubeGrid.EntityId))
                return;

            var wall = WelderGrids[block.CubeGrid.EntityId].GetWallAny(block);
            if (wall != null)
                WelderGrids[block.CubeGrid.EntityId].RemoveWall(wall);
        }

        public static void CheckWallConnected(IMyCubeBlock block)
        {
            if (!WelderGrids.ContainsKey(block.CubeGrid.EntityId))
                return;

            WelderGrids[block.CubeGrid.EntityId].CheckWallFunctional(block);
        }

        bool GoNow = false;
        public override void UpdateBeforeSimulation()
        {
            if (!MyAPIGateway.Session.IsServer)
                return;

            // 30 TPS
            GoNow = !GoNow;
            if (!GoNow || WelderGrids.Count == 0)
                return;

            int totalActionCount = 0;
            List<WelderGrid> active = new List<WelderGrid>();
            // assemble all blocks
            foreach(var wall in WelderGrids.Values)
            {
                wall.AssembleBlockList();
                totalActionCount += wall.ActionsThisTick;
                if (wall.ActionsThisTick != 0)
                    active.Add(wall);
            }

            if (totalActionCount == 0)
                return;

            int razeActions = Math.Max(0, totalActionCount - Config.GetInt(ConfigOptions.MaxActionsPerTick));
            if (razeActions != 0)
            {
                foreach (var grid in active)
                {
                    float razePercent = grid.ActionsThisTick / (float)totalActionCount;
                    grid.RazeActions((int)(razeActions * razePercent));
                }
            }

            // preform the actions on the blocks
            foreach (var wall in active)
                wall.DispatchBlocks(2);
        }

        public override void Draw()
        {
            foreach (var wall in WelderGrids.Values)
                wall.Draw();
        }

        public override void SaveData()
        {
            foreach(var wall in WelderGrids.Values)
                wall.Save();
        }


    }
}
