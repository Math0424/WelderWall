using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text;
using VRage.Game.ModAPI;
using VRageMath;

namespace WelderWall.Data.Scripts.Math0424.WelderWall
{
    enum WelderState
    {
        None,
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
        [ProtoMember(6)] public WelderState State;
        [ProtoMember(7)] public double PowerInput;
        [ProtoMember(8)] public bool DisplayBox;
        [ProtoMember(9)] public bool IsConnected;

        public long Owner;
        public IMyInventory Inventory;
        public List<IMySlimBlock> Welds;

        public WelderWall()
        {
            State = WelderState.Weld;
            Welds = new List<IMySlimBlock>();
        }

        public override bool Equals(object obj)
        {
            if (obj is WelderWall)
                return ID == ((WelderWall)obj).ID;
            return false;
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

        public void Set(int i, Vector3I pos)
        {
            switch(i % 4) 
            {
                case 0:
                    BL = pos;
                    break;
                case 1:
                    BR = pos;
                    break;
                case 2:
                    TR = pos;
                    break;
                case 3:
                    TL = pos;
                    break;
            }
            CalculateID();
        }

        public void Remove(Vector3I pos)
        {
            if (TR.HasValue && TR.Value == pos)
                TR = null;
            if (TL.HasValue && TL.Value == pos)
                TL = null;
            if (BR.HasValue && BR.Value == pos)
                BR = null;
            if (BL.HasValue && BL.Value == pos)
                BL = null;
            CalculateID();
        }

        public bool AllCornersValid()
        {
            return BL.HasValue && TL.HasValue && BR.HasValue && TR.HasValue;
        }

        public bool IsValid()
        {
            return IsConnected && AllCornersValid();
        }
        
        public void CalculateID()
        {
            ID = 17;
            ID = ID * 31 + TR.GetHashCode();
            ID = ID * 31 + TL.GetHashCode();
            ID = ID * 31 + BR.GetHashCode();
            ID = ID * 31 + BL.GetHashCode();
        }

        public override int GetHashCode()
        {
            return ID;
        }
    }
}
