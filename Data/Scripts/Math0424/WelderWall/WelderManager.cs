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

        private List<WelderGrid> WelderGrids;

        private Dictionary<Enum, object> defaultConfig = new Dictionary<Enum, object>()
        {
            { ConfigOptions.MaxPower, 100000 },
            { ConfigOptions.MaxWeldSpeed, 6 },
            { ConfigOptions.MaxWallSize, 50 },
            { ConfigOptions.MaxWeldsPerTick, 10 },
        };

        public WelderManager()
        {
            WelderGrids = new List<WelderGrid>();
        }

        public override void BeforeStart()
        {
            EasyNetworker.Init(22345);
            Config = new EasyConfiguration(false, "WelderWallConfig.cfg", defaultConfig);
            MyAPIGateway.Entities.OnEntityAdd += EntityAdd;
            foreach (var ent in MyEntities.GetEntities())
                EntityAdd(ent);
        }

        private void EntityAdd(IMyEntity ent)
        {
            if (!(ent is IMyCubeGrid))
                return;
            IMyCubeGrid grid = (IMyCubeGrid)ent;
            if (((MyCubeGrid)grid).IsPreview || !grid.Physics.Enabled)
                return;

            var welderGrid = new WelderGrid(grid);
            welderGrid.Closed += e => WelderGrids.Remove(e);
            WelderGrids.Add(welderGrid);
        }

        public override void UpdateBeforeSimulation()
        {
            foreach(var wall in WelderGrids)
            {
                wall.AssembleBlockList();
                wall.DispatchBlocks();
            }
        }

        public override void Draw()
        {
            foreach (var wall in WelderGrids)
                wall.Draw();
        }

        protected override void UnloadData()
        {

        }

    }
}
