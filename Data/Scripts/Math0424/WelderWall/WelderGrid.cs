using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Text;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRageMath;
using WelderWall.Data.Scripts.Math0424.WelderWall.Util;

namespace WelderWall.Data.Scripts.Math0424.WelderWall
{
    internal class WelderGrid
    {
        public Action<WelderGrid> Closed;
        IMyCubeGrid _grid;
        List<WelderWall> _walls;

        public WelderGrid(IMyCubeGrid grid) 
        {
            _walls = new List<WelderWall>();
            _grid = grid;
            _grid.OnBlockRemoved += BlockRemoved;
            _grid.OnBlockAdded += BlockAdded;
            _grid.OnClose += Close;
            foreach(var x in ((MyCubeGrid)_grid).GetFatBlocks())
                BlockAdded(x.SlimBlock);
            Load();
        }

        public void Close(IMyEntity ent)
        {
            _grid = null;
            _walls = null;
            Closed.Invoke(this);
        }

        private void BlockRemoved(IMySlimBlock block)
        {
            if (block.BlockDefinition.Id.SubtypeId != WelderManager.WelderCorner)
                return;
            foreach (var wall in _walls)
                if (wall.Contains(block.Position))
                {
                    wall.IsConnected = false;
                    wall.Remove(block.Position);
                    if (wall.AllCornersValid())
                        _walls.Remove(wall);
                    return;
                }
        }

        private void BlockAdded(IMySlimBlock block)
        {
            if (block.BlockDefinition.Id.SubtypeId != WelderManager.WelderCorner)
                return;

            WelderWall dummyWall = new WelderWall();
            TraceForward(0, dummyWall, block);

            foreach (var wall in _walls)
                if (wall.Equals(dummyWall) || wall.ContainsCorner(dummyWall.BL.Value))
                    return;

            dummyWall.Inventory = _grid.GetCubeBlock(dummyWall.BL.Value)?.FatBlock?.GetInventory();
            if (dummyWall.Inventory == null)
                return;
            dummyWall.Owner = block.OwnerId;

            if (dummyWall.AllCornersValid())
                dummyWall.IsConnected = true;

            _walls.Add(dummyWall);
        }


        private void TraceForward(int index, WelderWall wall, IMySlimBlock corner)
        {
            if (corner.BlockDefinition.Id.SubtypeId != WelderManager.WelderCorner || index >= 4)
                return;
            wall.Set(index, corner.Position);

            var dir = Base6Directions.GetIntVector(corner.Orientation.Forward);
            for (int i = 1; i < WelderManager.Config.GetInt(ConfigOptions.MaxWallSize); i++)
            {
                var bPos = corner.Position + (dir * i);
                if (wall.ContainsCorner(bPos))
                    return;

                var bBlock = _grid.GetCubeBlock(bPos);
                if (bBlock == null)
                    return;

                if (bBlock.BlockDefinition.Id.SubtypeId == WelderManager.WelderCorner)
                {
                    if (corner.Orientation.Left != bBlock.Orientation.Forward)
                        return;
                    TraceForward(index + 1, wall, bBlock);
                }
                else if (bBlock.BlockDefinition.Id.SubtypeId == WelderManager.WelderPole)
                    continue;
                else
                    return;
            }
        }

        public void Draw()
        {
            if (_grid.Closed)
                return;

            foreach (var wall in _walls)
            {
                if (!wall.BL.HasValue)
                    continue;

                Vector3D worldPos = _grid.GridIntegerToWorld(wall.BL.Value);
                var blocc = _grid.GetCubeBlock(wall.BL.Value);
                if (blocc == null)
                    continue;

                Vector3D dir = blocc.FatBlock.WorldMatrix.Forward;
                dir *= WelderManager.Config.GetInt(ConfigOptions.MaxWallSize);
                DrawUtil.DrawLine(worldPos, dir, 0, 0, 255);

                dir = blocc.FatBlock.WorldMatrix.Left;
                dir *= WelderManager.Config.GetInt(ConfigOptions.MaxWallSize);
                DrawUtil.DrawLine(worldPos, dir, 255, 0, 0);

                //if (wall.IsValid())
                //{
                    //Vector3D one = _grid.GetCubeBlock(wall.BL.Value).FatBlock.WorldMatrix.Translation;
                    //Vector3D two = _grid.GetCubeBlock(wall.TR.Value).FatBlock.WorldMatrix.Translation;
                    //DrawUtil.DrawPoints(one, two, 0, 255, 0);
                //}
            }
        }

        public void Save()
        {
            if (_grid.Storage.ContainsKey(WelderManager.StorageGUID))
                _grid.Storage.Remove(WelderManager.StorageGUID);

            byte[] data = MyAPIGateway.Utilities.SerializeToBinary(_walls.ToArray());
            data = MyCompression.Compress(data);
            _grid.Storage.Add(new KeyValuePair<Guid, string>(WelderManager.StorageGUID, Encoding.UTF8.GetString(data)));
        }

        public void Load()
        {
            if (_grid.Storage == null || !_grid.Storage.ContainsKey(WelderManager.StorageGUID))
                return;

            byte[] data = Encoding.UTF8.GetBytes(_grid.Storage.GetValue(WelderManager.StorageGUID));
            data = MyCompression.Decompress(data);
            //_walls = new List<WelderWall>(MyAPIGateway.Utilities.SerializeFromBinary<WelderWall[]>(data));
        }

        public void AssembleBlockList()
        {
            //  TL---TR
            //  |     |
            //  BL---BR
            foreach (var wall in _walls)
            {
                if (!wall.IsValid() || wall.State == WelderState.None)
                    continue;
                wall.Welds.Clear();

                Vector3D worldCenter = Vector3D.Transform(((Vector3D)(wall.TR.Value + wall.BL.Value)) / 2 * _grid.GridSize, _grid.WorldMatrix);
                Vector3 halfExtents = new Vector3(.5, (wall.BL.Value - wall.TL.Value).Length() - 1, (wall.BL.Value - wall.BR.Value).Length() - 1) * 1.25f;

                Quaternion orientation = Quaternion.CreateFromForwardUp(Vector3D.Normalize((wall.BL.Value - wall.BR.Value)), Vector3D.Normalize((wall.BL.Value - wall.TL.Value)));
                orientation = Quaternion.CreateFromRotationMatrix(_grid.WorldMatrix) * orientation;
                
                MyOrientedBoundingBoxD obb = new MyOrientedBoundingBoxD(worldCenter, halfExtents, orientation);

                List<MyEntity> entities = new List<MyEntity>();
                MyGamePruningStructure.GetAllEntitiesInOBB(ref obb, entities);

                DrawUtil.DrawOBB(obb, Color.Red);

                foreach(var ent in entities)
                {
                    if (ent == _grid)
                        continue;
                    else if (ent is IMyCharacter)
                        ((IMyCharacter)ent).DoDamage(10, WelderManager.WelderDamage, true);
                    else if(ent is IMyCubeGrid)
                        CalculateGridBlocks(wall, obb, (IMyCubeGrid)ent);
                }
            }


        }

        public void DispatchBlocks()
        {
            foreach (var wall in _walls)
            {
                if (!wall.IsValid())
                    continue;

                switch (wall.State)
                {
                    case WelderState.Weld:
                        foreach (var block in wall.Welds)
                        {
                            if (block.CubeGrid.Physics == null || !block.CubeGrid.Physics.Enabled)
                                continue;

                            if (!block.HasDeformation && block.IsFullIntegrity)
                                continue;

                            block.MoveItemsToConstructionStockpile(wall.Inventory);
                            block.MoveItemsFromConstructionStockpile(wall.Inventory, MyItemFlags.Damaged);
                            
                            if (block.CanContinueBuild(wall.Inventory))
                                block.IncreaseMountLevel(0.5f, wall.Owner, wall.Inventory);

                        }
                        break;
                    case WelderState.Grind:
                        foreach (var block in wall.Welds)
                        {
                            if (block.CubeGrid.Physics == null || !block.CubeGrid.Physics.Enabled)
                                continue;

                            float grindSpeed = 1;

                            block.DecreaseMountLevel(grindSpeed, wall.Inventory, true);
                            block.MoveItemsFromConstructionStockpile(wall.Inventory);

                            if (block.IsFullyDismounted)
                            {
                                if (block.FatBlock != null && block.FatBlock.HasInventory)
                                    EmptyBlockInventories(block.FatBlock, wall);

                                block.SpawnConstructionStockpile();
                                block.CubeGrid.RazeBlock(block.Min);
                            }
                        }
                        break;
                }
            }
        }

        private void CalculateGridBlocks(WelderWall wall, MyOrientedBoundingBoxD obb, IMyCubeGrid grid)
        {
            Vector3D[] corners = new Vector3D[8];
            obb.GetCorners(corners, 0);

            Vector3I BL = grid.WorldToGridInteger(corners[0]);
            Vector3I BR = grid.WorldToGridInteger(corners[4]);
            Vector3I TR = grid.WorldToGridInteger(corners[7]);
            Vector3I TL = grid.WorldToGridInteger(corners[3]);

            Vector3I minBounds = grid.Min;
            Vector3I maxBounds = grid.Max;

            HashSet<Vector3I> welds = new HashSet<Vector3I>();
            //  TL---TR
            //  |     |
            //  BL---BR
            GetPointsInPlane(TL, TR, BL, welds, minBounds, maxBounds);
            GetPointsInPlane(BL, TR, BR, welds, minBounds, maxBounds);

            int i = 0;
            foreach(var y in welds)
                if (i++ % 1 == 0)
                    grid.DrawCube(y, Color.Red);

            foreach(var wPos in welds)
            {
                IMySlimBlock blocc = ((MyCubeGrid)grid).GetCubeBlock(wPos);
                if (blocc != null)
                    wall.Welds.Add(blocc);
            }
        }

        private void GetPointsInPlane(Vector3I v1, Vector3I v2, Vector3I v3, HashSet<Vector3I> points, Vector3I minBounds, Vector3I maxBounds)
        {
            Vector3 normal = Vector3.Cross(v2 - v1, v3 - v1);
            double d = -Vector3.Dot(normal, v1);

            // XY plane projection
            for (int x = minBounds.X; x <= maxBounds.X; x++)
            {
                for (int y = minBounds.Y; y <= maxBounds.Y; y++)
                {
                    if (normal.Z != 0)
                    {
                        double z = -(normal.X * x + normal.Y * y + d) / normal.Z;
                        int zRound = (int)Math.Round(z);
                        if (zRound >= minBounds.Z && zRound <= maxBounds.Z)
                        {
                            Vector3I projectedPoint = new Vector3I(x, y, zRound);
                            if (IsPointInTriangle(projectedPoint, v1, v2, v3))
                            {
                                points.Add(projectedPoint);
                            }
                        }
                    }
                }
            }

            // XZ plane projection
            for (int x = minBounds.X; x <= maxBounds.X; x++)
            {
                for (int z = minBounds.Z; z <= maxBounds.Z; z++)
                {
                    if (normal.Y != 0)
                    {
                        double y = -(normal.X * x + normal.Z * z + d) / normal.Y;
                        int yRound = (int)Math.Round(y);
                        if (yRound >= minBounds.Y && yRound <= maxBounds.Y)
                        {
                            Vector3I projectedPoint = new Vector3I(x, yRound, z);
                            if (IsPointInTriangle(projectedPoint, v1, v2, v3))
                            {
                                points.Add(projectedPoint);
                            }
                        }
                    }
                }
            }

            // YZ plane projection
            for (int y = minBounds.Y; y <= maxBounds.Y; y++)
            {
                for (int z = minBounds.Z; z <= maxBounds.Z; z++)
                {
                    if (normal.X != 0)
                    {
                        double x = -(normal.Y * y + normal.Z * z + d) / normal.X;
                        int xRound = (int)Math.Round(x);
                        if (xRound >= minBounds.X && xRound <= maxBounds.X)
                        {
                            Vector3I projectedPoint = new Vector3I(xRound, y, z);
                            if (IsPointInTriangle(projectedPoint, v1, v2, v3))
                            {
                                points.Add(projectedPoint);
                            }
                        }
                    }
                }
            }
        }

        private bool IsPointInTriangle(Vector3I p, Vector3I v1, Vector3I v2, Vector3I v3)
        {
            Vector3 v0 = v2 - v1;
            Vector3 v1v3 = v3 - v1;
            Vector3 vp = p - v1;

            double d00 = Vector3.Dot(v0, v0);
            double d01 = Vector3.Dot(v0, v1v3);
            double d11 = Vector3.Dot(v1v3, v1v3);
            double d20 = Vector3.Dot(vp, v0);
            double d21 = Vector3.Dot(vp, v1v3);

            double denom = d00 * d11 - d01 * d01;
            double u = (d11 * d20 - d01 * d21) / denom;
            double v = (d00 * d21 - d01 * d20) / denom;

            return (u >= 0) && (v >= 0) && (u + v <= 1);
        }

        private static List<VRage.Game.ModAPI.Ingame.MyInventoryItem> m_tmpItemList = new List<VRage.Game.ModAPI.Ingame.MyInventoryItem>();
        private void EmptyBlockInventories(IMyCubeBlock block, WelderWall wall)
        {
            for (int i = 0; i < block.InventoryCount; i++)
            {
                IMyInventory inventory = block.GetInventory(i);
                if (!inventory.Empty())
                {
                    m_tmpItemList.Clear();
                    inventory.GetItems(m_tmpItemList);
                    foreach (VRage.Game.ModAPI.Ingame.MyInventoryItem item in m_tmpItemList)
                        wall.Inventory.TransferItemFrom(inventory, item, item.Amount);
                }
            }
        }

    }
}
