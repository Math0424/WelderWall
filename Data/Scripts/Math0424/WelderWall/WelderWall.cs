using ProtoBuf;
using System;
using System.Collections.Generic;
using VRage.Game.ModAPI;
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
        [ProtoMember(8)] public bool Enabled;

        [ProtoMember(9)] public long Owner;
        /// <summary>
        /// Are the corners working?
        /// </summary>
        [ProtoMember(10)] public bool CornersWorking;
        /// <summary>
        /// Are all the poles working?
        /// </summary>
        [ProtoMember(12)] public bool Connected;

        public List<IMySlimBlock> Blocks;
        public IMyCubeBlock[] Corners;

        public void UpdateTerminalControls()
        {
            foreach(var block in Corners)
                if (block != null)
                {
                    WelderManager.TerminalControls.SetValues(block, Enabled, State == WelderState.Weld, PowerInput);
                    WelderManager.TerminalControls.SetEnabled(block, CanFunction());
                }
        }

        public void UpdateIsWorking()
        {
            CornersWorking = true;
            foreach (var block in Corners)
            {
                if (block == null || !block.IsWorking)
                {
                    CornersWorking = false;
                    return;
                }
            }
        }

        public WelderWall()
        {
            Connected = true;
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
            {
                CornersWorking = false;
                return;
            }

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
            CornersWorking = false;
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
            if (IsFunctioning())
                return PowerInput / 4;
            return 0;
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
        /// Are we able to function
        /// </summary>
        /// <returns></returns>
        public bool CanFunction()
        {
            return Connected && CornersWorking && HasAllCorners();
        }

        /// <summary>
        /// Should we function
        /// </summary>
        /// <returns></returns>
        public bool IsFunctioning()
        {
            return CanFunction() && Enabled;
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
