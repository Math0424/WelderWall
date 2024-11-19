using ProtoBuf;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using WelderWall.Data.Scripts.Math0424.WelderWall.Util;

namespace WelderWall.Data.Scripts.Math0424.WelderWall
{
    enum WelderState
    {
        Grind,
        Weld,
    }

    [ProtoContract]
    internal class WelderWall
    {
        [ProtoMember(1)] public int ID;
        [ProtoMember(2)] public Vector3I? TR;
        [ProtoMember(3)] public Vector3I? TL;
        [ProtoMember(4)] public Vector3I? BR;
        [ProtoMember(5)] public Vector3I? BL;

        [ProtoMember(7)] public WelderState State;
        [ProtoMember(8)] public float PowerInput;
        [ProtoMember(9)] public bool Enabled;

        [ProtoMember(10)] public long Owner;
        [ProtoMember(11)] public bool IsWorking;

        public IMyInventory Inventory;
        public List<IMySlimBlock> Blocks;
        private IMyCubeBlock[] Corners;

        public void UpdateTerminalControls()
        {
            foreach(var block in Corners)
                if (block != null)
                    WelderManager.TerminalControls.SetValues(block.EntityId, Enabled, State == WelderState.Weld, PowerInput);
        }

        public void UpdatePowerState()
        {
            foreach(var block in Corners)
                if (block != null)
                {
                    block.ResourceSink.SetRequiredInputByType(MyResourceDistributorComponent.ElectricityId, PowerInput / 4);
                }
        }

        public WelderWall()
        {
            State = WelderState.Grind;
            Blocks = new List<IMySlimBlock>();
            Corners = new IMyCubeBlock[4];
        }

        public void ColdInit(IMyCubeGrid grid)
        {
            if (BL.HasValue)
            {
                var cube = grid.GetCubeBlock(BL.Value)?.FatBlock;
                if (cube != null)
                    Inventory = cube.GetInventory();
                Set(0, cube);
            }
            if (BR.HasValue)
                Set(1, grid.GetCubeBlock(BR.Value)?.FatBlock);
            if (TR.HasValue)
                Set(2, grid.GetCubeBlock(TR.Value)?.FatBlock);
            if (TL.HasValue)
                Set(3, grid.GetCubeBlock(TL.Value)?.FatBlock);

            UpdateTerminalControls();
            UpdatePowerState();
        }

        public void Set(int i, IMyCubeBlock corner)
        {
            if (corner == null)
            {
                IsWorking = false;
                return;
            }

            Corners[i % 4] = corner;
            switch(i % 4) 
            {
                case 0:
                    BL = corner.Position;
                    break;
                case 1:
                    BR = corner.Position;
                    break;
                case 2:
                    TR = corner.Position;
                    break;
                case 3:
                    TL = corner.Position;
                    break;
            }
            CalculateID();

            corner.IsWorkingChanged += CheckDisabled;
            corner.OnClose += RemoveCorner;
            if (!corner.IsWorking)
                IsWorking = false;
        }

        private void CheckDisabled(IMyCubeBlock corner)
        {
            if (!corner.IsWorking)
                IsWorking = false;
        }

        public void UpdateIsWorking()
        {
            IsWorking = true;
            foreach(var block in Corners)
            {
                if (block == null || !block.IsWorking)
                {
                    IsWorking = false;
                    return;
                }
            }
        }

        private void RemoveCorner(IMyEntity ent)
        {
            var block = ent as IMyCubeBlock;
            if (block == null)
                return;
            IsWorking = false;
            block.IsWorkingChanged -= CheckDisabled;
            block.OnClose -= RemoveCorner;

            var pos = block.Position;
            if (BL.HasValue && BL.Value == pos)
            {
                BL = null;
                Corners[0] = null;
            }
            if (BR.HasValue && BR.Value == pos)
            {
                BR = null;
                Corners[1] = null;
            }
            if (TR.HasValue && TR.Value == pos)
            {
                TR = null;
                Corners[2] = null;
            }
            if (TL.HasValue && TL.Value == pos)
            {
                TL = null;
                Corners[3] = null;
            }
            CalculateID();
        }





        public bool ContainsCorner(Vector3I pos)
        {
            if (BL.HasValue && BL.Value == pos)
                return true;
            if (BR.HasValue && BR.Value == pos)
                return true;
            if (TL.HasValue && TL.Value == pos)
                return true;
            if (TR.HasValue && TR.Value == pos)
                return true;
            return false;
        }

        private bool IsBetweenPoints(Vector3I a, Vector3I b, Vector3I c)
        {
            bool isAxisAligned = (a.X == b.X && a.Y == b.Y) ||
                                 (a.X == b.X && a.Z == b.Z) ||
                                 (a.Y == b.Y && a.Z == b.Z);
            if (!isAxisAligned)
                return false;

            bool withinX = c.X >= Math.Min(a.X, b.X) && c.X <= Math.Max(a.X, b.X);
            bool withinY = c.Y >= Math.Min(a.Y, b.Y) && c.Y <= Math.Max(a.Y, b.Y);
            bool withinZ = c.Z >= Math.Min(a.Z, b.Z) && c.Z <= Math.Max(a.Z, b.Z);

            return ((a.X == b.X && a.X == c.X && withinY && withinZ) ||
                    (a.Y == b.Y && a.Y == c.Y && withinX && withinZ) ||
                    (a.Z == b.Z && a.Z == c.Z && withinX && withinY));
        }

        public bool HasOneValidCorner()
        {
            return BL.HasValue || TL.HasValue || BR.HasValue || TR.HasValue;
        }

        public bool HasAllCorners()
        {
            return BL.HasValue && TL.HasValue && BR.HasValue && TR.HasValue;
        }

        public bool CanFunction()
        {
            return IsWorking && HasAllCorners();
        }

        public bool IsFunctional()
        {
            return CanFunction() && Enabled;
        }

        public bool Contains(Vector3I pos)
        {
            //  TL---TR
            //  |     |
            //  BL---BR
            if (ContainsCorner(pos))
                return true;

            if (BL.HasValue && BR.HasValue)
                return IsBetweenPoints(BL.Value, BR.Value, pos);

            if (TR.HasValue && BR.HasValue)
                return IsBetweenPoints(TR.Value, BR.Value, pos);

            if (TR.HasValue && TL.HasValue)
                return IsBetweenPoints(TR.Value, TL.Value, pos);

            if (BL.HasValue && TL.HasValue)
                return IsBetweenPoints(BL.Value, TL.Value, pos);

            return false;
        }

        public void CalculateID()
        {
            ID = 17;
            ID = ID * 31 + TR.GetHashCode();
            ID = ID * 31 + TL.GetHashCode();
            ID = ID * 31 + BR.GetHashCode();
            ID = ID * 31 + BL.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is WelderWall)
                return ID == ((WelderWall)obj).ID;
            return false;
        }

        public override int GetHashCode()
        {
            return ID;
        }
    }
}
