using ProtoBuf;
using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace WelderWall.Data.Scripts.Math0424.WelderWall
{
    enum WelderState
    {
        Grind,
        Weld,
    }

    /// <summary>
    /// Mostly a data storage but has instanced variables. really not good design. fix it later.
    /// </summary>
    [ProtoContract]
    internal class WelderWall
    {
        [ProtoMember(1)] public int ID;
        [ProtoMember(2)] public Vector3I? BL; // 0
        [ProtoMember(3)] public Vector3I? BR; // 1
        [ProtoMember(4)] public Vector3I? TL; // 2
        [ProtoMember(5)] public Vector3I? TR; // 3

        [ProtoMember(6)] public WelderState State;
        [ProtoMember(7)] public float PowerInput;

        [ProtoMember(9)] public long Owner;

        public List<IMySlimBlock> Blocks;
        public IMyCubeBlock[] Corners;

        public void UpdateTerminalControls()
        {
            foreach(var block in Corners)
                if (block != null)
                {
                    WelderManager.TerminalControls.SetValues(block, State == WelderState.Weld, PowerInput);
                    WelderManager.TerminalControls.SetAllEnabled(block, true);
                }
        }

        public WelderWall()
        {
            Blocks = new List<IMySlimBlock>();
            Corners = new IMyCubeBlock[4];
        }

        /// <summary>
        /// Set a corner of our wall
        /// </summary>
        /// <param name="i"></param>
        /// <param name="corner"></param>
        public void SetCorner(int i, IMyCubeBlock corner)
        {
            if (corner == null)
                return;

            Corners[i % 4] = corner;
            switch (i % 4) 
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
        }

        public void RemoveCorner(IMyCubeBlock corner)
        {
            var pos = corner.Position;
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

        public int Area()
        {
            if (!HasAllCorners())
                return 0;
            return BL.Value.RectangularDistance(BR.Value) * TR.Value.RectangularDistance(BR.Value);
        }

        public float RequiredPower()
        {
            return PowerInput / 4;
        }

        /// <summary>
        /// Does this position have a corner
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
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

        /// <summary>
        /// Do we have at least one valid corner
        /// </summary>
        /// <returns></returns>
        public bool HasAValidCorner()
        {
            return BL.HasValue || TL.HasValue || BR.HasValue || TR.HasValue;
        }

        /// <summary>
        /// Do we have all 4 corners
        /// </summary>
        /// <returns></returns>
        public bool HasAllCorners()
        {
            return BL.HasValue && TL.HasValue && BR.HasValue && TR.HasValue;
        }

        /// <summary>
        /// Should we function
        /// </summary>
        /// <returns></returns>
        public bool IsFunctional()
        {
            if (PowerInput == 0 || !HasAllCorners())
                return false;
            foreach (var block in Corners)
                if (!block.IsWorking || block.ResourceSink.SuppliedRatioByType(MyResourceDistributorComponent.ElectricityId) != 1)
                    return false;
            return true;
        }

        /// <summary>
        /// Is this a block that is within our frame, corners included
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        public bool Contains(Vector3I pos)
        {
            //  TL---TR
            //  |     |
            //  BL---BR
            if (ContainsCorner(pos))
                return true;

            if (BL.HasValue && BR.HasValue && IsBetweenPoints(BL.Value, BR.Value, pos))
                return true;

            if (TR.HasValue && BR.HasValue && IsBetweenPoints(TR.Value, BR.Value, pos))
                return true;

            if (TR.HasValue && TL.HasValue && IsBetweenPoints(TR.Value, TL.Value, pos))
                return true;

            if (BL.HasValue && TL.HasValue && IsBetweenPoints(BL.Value, TL.Value, pos))
                return true;

            return false;
        }

        private bool IsBetweenPoints(Vector3I a, Vector3I b, Vector3I check)
        {
            bool isAxisAligned = (a.X == b.X && a.Y == b.Y) ||
                                 (a.X == b.X && a.Z == b.Z) ||
                                 (a.Y == b.Y && a.Z == b.Z);
            if (!isAxisAligned)
                return false;

            bool withinX = check.X >= Math.Min(a.X, b.X) && check.X <= Math.Max(a.X, b.X);
            bool withinY = check.Y >= Math.Min(a.Y, b.Y) && check.Y <= Math.Max(a.Y, b.Y);
            bool withinZ = check.Z >= Math.Min(a.Z, b.Z) && check.Z <= Math.Max(a.Z, b.Z);

            return ((a.X == b.X && a.X == check.X && withinY && withinZ) ||
                    (a.Y == b.Y && a.Y == check.Y && withinX && withinZ) ||
                    (a.Z == b.Z && a.Z == check.Z && withinX && withinY));
        }

        public void CalculateID()
        {
            // poor hash, but will result in the same value regardless of positions of corners
            ID = TR.GetHashCode() * TL.GetHashCode() * BR.GetHashCode() * BL.GetHashCode();
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
