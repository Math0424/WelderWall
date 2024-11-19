using System.Collections.Generic;
using VRage.Game;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;

namespace WelderWall.Data.Scripts.Math0424.WelderWall.Util
{
    /// <summary>
    /// Easily draw some simple things.
    /// </summary>
    internal static class EasyDraw
    {
        private static MyStringId SQUARE = MyStringId.GetOrCompute("Square");
        private static List<Vector3D> buffer = new List<Vector3D>();

        public static void DrawOBB(MyOrientedBoundingBoxD obb, Color color, MySimpleObjectRasterizer raster = MySimpleObjectRasterizer.Wireframe, float thickness = 0.01f)
        {
            var box = new BoundingBoxD(-obb.HalfExtent, obb.HalfExtent);
            var wm = MatrixD.CreateFromQuaternion(obb.Orientation);
            wm.Translation = obb.Center;
            MySimpleObjectDraw.DrawTransparentBox(ref wm, ref box, ref color, raster, 1, thickness, SQUARE, SQUARE);
        }

        public static void DrawLine(Vector3D pos, Vector3D dir, Color color)
        {
            Vector4 vColor = color;
            MySimpleObjectDraw.DrawLine(pos, pos + dir * 10, SQUARE, ref vColor, 0.01f);
        }

        public static void DrawSphere(Vector3D center, float size, Color color, MySimpleObjectRasterizer raster = MySimpleObjectRasterizer.Wireframe, float thickness = 0.01f)
        {
            MySimpleObjectDraw.DrawTransparentSphere(buffer, size, ref color, raster, SQUARE, SQUARE, thickness);
        }

        public static void DrawPlane(Vector3D center, Vector3 up, Vector3 left, Color color)
        {
            float height = up.Length();
            float width = left.Length();
            Vector3 normUp = Vector3.Normalize(up);
            Vector3 normLeft = Vector3.Normalize(left);
            MyTransparentGeometry.AddBillboardOriented(SQUARE, color, center, normLeft, normUp, width, height);
        }

        public static void DrawCubeOnGrid(IMyCubeGrid grid, Vector3I pos, Color color, MySimpleObjectRasterizer raster = MySimpleObjectRasterizer.Wireframe, float thickness = 0.01f)
        {
            BoundingBoxD bb = new BoundingBoxD(new Vector3(-grid.GridSize) / 2, new Vector3(grid.GridSize) / 2);
            bb.Translate(pos * grid.GridSize);

            MatrixD m = grid.WorldMatrix;
            MySimpleObjectDraw.DrawTransparentBox(ref m, ref bb, ref color, raster, 1, thickness, SQUARE, SQUARE);
        }

        public static void DrawCube(this IMyCubeGrid grid, Vector3I pos, Color color, MySimpleObjectRasterizer raster = MySimpleObjectRasterizer.Wireframe, float thickness = 0.01f)
        {
            float gridSize = grid.GridSize;
            BoundingBoxD aabb = new BoundingBoxD(pos * gridSize - gridSize / 2f, pos * gridSize + gridSize / 2f);
            MatrixD wm = grid.WorldMatrix;
            MySimpleObjectDraw.DrawTransparentBox(ref wm, ref aabb, ref color, raster, 1, thickness, SQUARE, SQUARE);
        }

        public static void DrawAABB(this IMySlimBlock block, Color color, MySimpleObjectRasterizer raster = MySimpleObjectRasterizer.Wireframe, float thickness = 0.01f)
        {
            color.A = 255;
            BoundingBoxD aabb = block.GetAABBLocal().Inflate(.05);
            MatrixD wm = block.CubeGrid.WorldMatrix;
            MySimpleObjectDraw.DrawTransparentBox(ref wm, ref aabb, ref color, raster, 1, thickness, SQUARE, SQUARE);
        }

        private static BoundingBoxD GetAABBLocal(this IMySlimBlock block)
        {
            float gridSize = block.CubeGrid.GridSize;
            return new BoundingBoxD(block.Min * gridSize - gridSize / 2f, block.Max * gridSize + gridSize / 2f);
        }

        public static void DrawQuaternion(Vector3D pos, Quaternion quat, Color color)
        {
            Vector4 vColor = color;
            Matrix m = Matrix.CreateFromQuaternion(quat);
            MySimpleObjectDraw.DrawLine(pos, pos + m.Forward, SQUARE, ref vColor, 0.01f);
            MySimpleObjectDraw.DrawLine(pos, pos + m.Up, SQUARE, ref vColor, 0.01f);
            MySimpleObjectDraw.DrawLine(pos, pos + m.Left, SQUARE, ref vColor, 0.01f);
        }

        public static void DrawMatrix(Vector3D pos, Matrix m, Color color)
        {
            Vector4 vColor = color;
            MySimpleObjectDraw.DrawLine(pos, pos + m.Forward, SQUARE, ref vColor, 0.01f);
            MySimpleObjectDraw.DrawLine(pos, pos + m.Up, SQUARE, ref vColor, 0.01f);
            MySimpleObjectDraw.DrawLine(pos, pos + m.Left, SQUARE, ref vColor, 0.01f);
        }

        public static void DrawMatrix(Vector3D pos, Matrix m)
        {
            Vector4 R = new Vector4(255, 0, 0, 1);
            Vector4 G = new Vector4(0, 255, 0, 1);
            Vector4 B = new Vector4(0, 0, 255, 1);
            MySimpleObjectDraw.DrawLine(pos, pos + m.Forward, SQUARE, ref R, 0.01f);
            MySimpleObjectDraw.DrawLine(pos, pos + m.Up, SQUARE, ref G, 0.01f);
            MySimpleObjectDraw.DrawLine(pos, pos + m.Left, SQUARE, ref B, 0.01f);
        }

        public static void DrawPoints(Vector3D pos, Vector3D pos2, Color color, float thickness = 0.01f)
        {
            Vector4 vColor = color;
            MySimpleObjectDraw.DrawLine(pos, pos2, SQUARE, ref vColor, thickness);
        }

    }
}
