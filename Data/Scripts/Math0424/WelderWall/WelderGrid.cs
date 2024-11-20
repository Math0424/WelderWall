using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Gui;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using VRage;
using VRage.Game;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using WelderWall.Data.Scripts.Math0424.WelderWall.Util;

namespace WelderWall.Data.Scripts.Math0424.WelderWall
{
    /// <summary>
    /// Mid level overview, manages how much it can weld based upon how much the WelderManage allocated it
    /// </summary>
    internal class WelderGrid
    {
        public int ActionsThisTick;

        public Action<WelderGrid> Closed;
        IMyCubeGrid _grid;
        List<WelderWall> _walls;

        public WelderGrid(IMyCubeGrid grid) 
        {
            _walls = new List<WelderWall>();
            _grid = grid;
            _grid.OnClose += Close;
            Load();
        }

        public void CheckWallFunctional(IMyCubeBlock block)
        {
            var dir = Base6Directions.GetIntVector(block.Orientation.Up);
            for (int i = 1; i < WelderManager.Config.GetInt(ConfigOptions.MaxWallSize); i++)
            {
                var bBlock = _grid.GetCubeBlock(block.Position + (dir * i));
                if (bBlock == null || bBlock.FatBlock == null || !bBlock.FatBlock.IsWorking)
                    return;

                if (bBlock.BlockDefinition.Id.SubtypeId == WelderManager.WelderCorner)
                {
                    AddCorner(bBlock.FatBlock);
                    return;
                }
                else if (bBlock.BlockDefinition.Id.SubtypeId == WelderManager.WelderPole)
                    continue;
                else
                    return;
            }
        }

        public void AddCorner(IMyCubeBlock block)
        {
            WelderWall dummyWall = new WelderWall();
            if (!FindWall(0, dummyWall, block.SlimBlock, block.Orientation.Forward))
                return;

            foreach (var wall in _walls)
                if (wall.Equals(dummyWall))
                {
                    wall.Corners = dummyWall.Corners;
                    wall.UpdateTerminalControls();
                    return;
                }

            dummyWall.Owner = _grid.BigOwners[0];
            dummyWall.PowerInput = 100;
            dummyWall.UpdateTerminalControls();

            _walls.Add(dummyWall);
            Save();
        }

        public void RemoveWall(WelderWall wall)
        {
            _walls.Remove(wall);
            Save();
            foreach (var block in wall.Corners)
                if (block != null)
                    WelderManager.TerminalControls.SetEnabled(block, false);
        }

        public void RemoveWall(IMyCubeBlock block)
        {
            foreach (var wall in _walls)
                if (wall.Contains(block.Position))
                {
                    RemoveWall(wall);
                    return;
                }
        }

        public WelderWall GetWallByCorner(IMyCubeBlock block)
        {
            foreach(var x in _walls)
            {
                if (x.ContainsCorner(block.Position))
                    return x;
            }
            return null;
        }

        public WelderWall GetWallAny(IMyCubeBlock block)
        {
            foreach (var x in _walls)
            {
                if (x.Contains(block.Position))
                    return x;
            }
            return null;
        }

        private bool FindWall(int index, WelderWall wall, IMySlimBlock corner, Base6Directions.Direction trace)
        {
            if (corner.BlockDefinition.Id.SubtypeId != WelderManager.WelderCorner || index >= 5)
                return false;
            wall.SetCorner(index, corner.FatBlock);

            var cOrient = corner.Orientation;
            var dir = Base6Directions.GetIntVector(trace);
            for (int i = 1; i < WelderManager.Config.GetInt(ConfigOptions.MaxWallSize); i++)
            {
                var bPos = corner.Position + (dir * i);
                if (wall.ContainsCorner(bPos))
                    return true;

                var bBlock = _grid.GetCubeBlock(bPos);
                if (bBlock == null || bBlock.FatBlock == null || !bBlock.FatBlock.IsWorking)
                    return false;

                if (bBlock.BlockDefinition.Id.SubtypeId == WelderManager.WelderCorner)
                {
                    var bOrient = bBlock.Orientation;
                    if (cOrient.Forward == Base6Directions.GetOppositeDirection(bOrient.Left) && cOrient.Left == bOrient.Forward)
                    {
                        return FindWall(index + 1, wall, bBlock, bOrient.Forward);
                    }
                    else if (cOrient.Forward == Base6Directions.GetOppositeDirection(bOrient.Forward) && cOrient.Left == bOrient.Left)
                    {
                        return FindWall(index + 1, wall, bBlock, bOrient.Left);
                    }
                    else
                        return false;
                }
                else if (bBlock.BlockDefinition.Id.SubtypeId == WelderManager.WelderPole)
                    continue;
                else
                    return false;
            }
            return false;
        }

        public void Draw()
        {
            foreach(var wall in _walls)
            {
                if (!wall.IsFunctioning())
                    continue;

                Vector3D worldCenter = Vector3D.Transform(((Vector3D)(wall.TR.Value + wall.BL.Value)) / 2 * _grid.GridSize, _grid.WorldMatrix);
                Vector3 halfExtents = new Vector3(.5, (wall.BL.Value - wall.TL.Value).Length() - 1, (wall.BL.Value - wall.BR.Value).Length() - 1) * 1.25f;

                Quaternion orientation = Quaternion.CreateFromForwardUp(Vector3D.Normalize((wall.BL.Value - wall.BR.Value)), Vector3D.Normalize((wall.BL.Value - wall.TL.Value)));
                orientation = Quaternion.CreateFromRotationMatrix(_grid.WorldMatrix) * orientation;

                MyOrientedBoundingBoxD obb = new MyOrientedBoundingBoxD(worldCenter, halfExtents, orientation);

                EasyDraw.DrawOBB(obb, (wall.State == WelderState.Grind ? Color.Red : Color.Green) * 0.25f, MySimpleObjectRasterizer.SolidAndWireframe);
            }
        }

        public void Save()
        {
            if (!MyAPIGateway.Session.IsServer)
                return;

            if (_grid.Storage == null)
                _grid.Storage = new MyModStorageComponent();

            byte[] data = MyAPIGateway.Utilities.SerializeToBinary(_walls.ToArray());
            data = MyCompression.Compress(data);
            _grid.Storage[WelderManager.StorageGUID] = Convert.ToBase64String(data);
        }

        public void Load()
        {
            if (!MyAPIGateway.Session.IsServer || _grid.Storage == null || !_grid.Storage.ContainsKey(WelderManager.StorageGUID))
                return;

            try
            {
                byte[] data = Convert.FromBase64String(_grid.Storage[WelderManager.StorageGUID]);
                data = MyCompression.Decompress(data);
                _walls = new List<WelderWall>(MyAPIGateway.Utilities.SerializeFromBinary<WelderWall[]>(data));
            }
            catch
            {
                _grid.Storage.Remove(WelderManager.StorageGUID);
            }
        }

        public void AssembleBlockList()
        {
            //  TL---TR
            //  |     |
            //  BL---BR
            ActionsThisTick = 0;
            foreach (var wall in _walls)
            {
                // arbitrary hard cap on per grid actions
                if (ActionsThisTick > 100)
                    return;

                wall.Blocks.Clear();
                wall.UpdateTerminalControls();
                if (!wall.IsFunctioning())
                    continue;

                Vector3D worldCenter = Vector3D.Transform(((Vector3D)(wall.TR.Value + wall.BL.Value)) / 2 * _grid.GridSize, _grid.WorldMatrix);
                Vector3 halfExtents = new Vector3(.5, (wall.BL.Value - wall.TL.Value).Length() - 1, (wall.BL.Value - wall.BR.Value).Length() - 1) * 1.25f;

                Quaternion orientation = Quaternion.CreateFromForwardUp(Vector3D.Normalize((wall.BL.Value - wall.BR.Value)), Vector3D.Normalize((wall.BL.Value - wall.TL.Value)));
                orientation = Quaternion.CreateFromRotationMatrix(_grid.WorldMatrix) * orientation;
                
                MyOrientedBoundingBoxD obb = new MyOrientedBoundingBoxD(worldCenter, halfExtents, orientation);

                List<MyEntity> entities = new List<MyEntity>();
                MyGamePruningStructure.GetAllEntitiesInOBB(ref obb, entities);

                foreach(var ent in entities)
                {
                    if (ent == _grid)
                        continue;
                    else if (ent is IMyCharacter)
                    {
                        var character = ((IMyCharacter)ent);
                        MyOrientedBoundingBoxD characterBounds = new MyOrientedBoundingBoxD(character.WorldMatrix);
                        if (obb.Intersects(ref characterBounds))
                            character.DoDamage(1, WelderManager.WelderDamage, true);
                    }
                    else if(ent is IMyCubeGrid)
                        CalculateGridBlocks(wall, obb, (IMyCubeGrid)ent);
                }

                if (wall.State == WelderState.Weld)
                {
                    wall.Blocks.RemoveAll((block) =>
                    {
                        if (block.CubeGrid.Physics == null)
                            return (((IMyProjector)((MyCubeGrid)block.CubeGrid).Projector).CanBuild(block, false) != BuildCheckResult.OK);
                        return block.IsFullIntegrity;
                    });
                }
                else
                {
                    wall.Blocks.RemoveAll((block) => block.CubeGrid.Physics == null || !block.CubeGrid.Physics.Enabled);
                }

                ActionsThisTick += wall.Blocks.Count;
            }
        }

        /// <summary>
        /// We are over our limits, remove X blocks from the queue
        /// </summary>
        /// <param name="count"></param>
        public void RazeActions(int count)
        {
            if (_walls.Count == 0)
                return;

            foreach(var wall in _walls)
            {
                float percentToRemove = Math.Min(0.95f, (float)count / wall.Blocks.Count);
                wall.Blocks.RemoveRange(0, (int)(wall.Blocks.Count * percentToRemove));
            }
        }

        public void DispatchBlocks(int delta)
        {
            float maxDraw = WelderManager.Config.GetFloat(ConfigOptions.MaxPower);
            int maxArea = WelderManager.Config.GetInt(ConfigOptions.MaxWallSize);
            maxArea *= maxArea;

            foreach (var wall in _walls)
            {
                if (!wall.IsFunctioning())
                    continue;

                foreach (var x in wall.Blocks)
                    x.DrawAABB(Color.Green);

                IMyInventory transferTo = null;
                transferTo = wall.Corners[0].GetInventory();
                if (transferTo == null)
                    return;

                float efficiency = (float)Math.Sin(wall.PowerInput / maxDraw * Math.PI / 2);
                float efficentyLoss = (1 - (wall.Area() / (float)maxArea)) + 0.25f;
                efficiency *= Math.Max(1f, efficentyLoss);

                switch (wall.State)
                {
                    case WelderState.Weld:

                        foreach (var block in wall.Blocks)
                        {
                            if (block.CubeGrid.Physics == null)
                            {
                                ((IMyProjector)((MyCubeGrid)block.CubeGrid).Projector).Build(block, wall.Owner, wall.Owner, true);
                                continue;
                            }

                            if (block.CubeGrid.Physics == null)
                                continue;

                            block.MoveItemsToConstructionStockpile(transferTo);
                            block.MoveItemsFromConstructionStockpile(transferTo, MyItemFlags.Damaged);
                            
                            if (block.CanContinueBuild(transferTo))
                            {
                                float weldSpeed = efficiency * WelderManager.Config.GetFloat(ConfigOptions.Speed) * delta;
                                block.IncreaseMountLevel(weldSpeed, wall.Owner, transferTo);
                            }
                        }
                        break;
                    case WelderState.Grind:
                        foreach (var block in wall.Blocks)
                        {
                            float grindSpeed = efficiency * WelderManager.Config.GetFloat(ConfigOptions.Speed) * delta;
                            block.DecreaseMountLevel(grindSpeed, transferTo, true);
                            block.MoveItemsFromConstructionStockpile(transferTo);

                            if (block.IsFullyDismounted)
                            {
                                if (block.FatBlock != null && block.FatBlock.HasInventory)
                                    EmptyBlockInventories(block.FatBlock, wall, transferTo);

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
            if ((((MyCubeGrid)grid).IsPreview || !grid.Physics.Enabled) && ((MyCubeGrid)grid).Projector == null)
                return;

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

            foreach (var wPos in welds)
            {
                IMySlimBlock blocc = ((MyCubeGrid)grid).GetCubeBlock(wPos);
                if (blocc != null)
                    wall.Blocks.Add(blocc);
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
        private void EmptyBlockInventories(IMyCubeBlock block, WelderWall wall, IMyInventory transferTo)
        {
            for (int i = 0; i < block.InventoryCount; i++)
            {
                IMyInventory inventory = block.GetInventory(i);
                if (!inventory.Empty())
                {
                    m_tmpItemList.Clear();
                    inventory.GetItems(m_tmpItemList);
                    foreach (VRage.Game.ModAPI.Ingame.MyInventoryItem item in m_tmpItemList)
                        transferTo.TransferItemFrom(inventory, item, item.Amount);
                }
            }
        }

        public void Close(IMyEntity ent)
        {
            _grid = null;
            _walls = null;
            Closed?.Invoke(this);
        }
    }
}
