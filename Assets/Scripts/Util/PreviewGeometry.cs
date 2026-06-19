using UnityEngine;

namespace VRBrush.Util
{
    /// <summary>
    /// 预览几何工具：从现有 UniversalBrushPreview 与 BrushEditorDisplay 中抽取的 2D→3D 计算。
    /// 仅包含现有逻辑的无副作用封装，不引入新行为。
    /// </summary>
    public static class PreviewGeometry
    {
        /// <summary>
        /// 将局部 2D 坐标映射到世界空间。
        /// 约定：orientation.up 作为“ruling”，orientation.right 作为“normal”。
        /// world = origin + (up * local.x + right * local.y) * scale
        /// </summary>
        public static Vector3 Map2DToWorld(Vector3 origin, Quaternion orientation, Vector2 local, float scale)
        {
            Vector3 worldUp = orientation * Vector3.up;
            Vector3 worldRight = orientation * Vector3.right;
            return origin + (worldUp * local.x + worldRight * local.y) * scale;
        }

        /// <summary>
        /// 计算将圆柱体放置在两点之间所需的平移、旋转与缩放。
        /// 匹配原逻辑：
        /// - 位置：中点
        /// - 旋转：FromToRotation(Vector3.up, dir)
        /// - 缩放：(radius*2, len*0.5, radius*2)
        /// </summary>
        public static bool ComputeCylinderBetween(Vector3 a, Vector3 b, float radius, out Vector3 position, out Quaternion rotation, out Vector3 scale)
        {
            Vector3 dir = b - a;
            float len = dir.magnitude;
            if (len < 1e-5f)
            {
                position = a;
                rotation = Quaternion.identity;
                scale = Vector3.zero;
                return false;
            }
            dir /= len;
            position = (a + b) * 0.5f;
            rotation = Quaternion.FromToRotation(Vector3.up, dir);
            scale = new Vector3(radius * 2f, len * 0.5f, radius * 2f);
            return true;
        }

        /// <summary>
        /// 将空间点投影到指定平面（由平面上一点与法线确定）的垂直投影点。
        /// 匹配 BrushEditorDisplay 的 worldHit 计算：point - normal * signedDistance
        /// </summary>
        public static Vector3 ProjectPointOntoPlane(Vector3 point, Vector3 planePoint, Vector3 planeNormal)
        {
            Vector3 n = planeNormal.normalized;
            float signedDistance = Vector3.Dot(point - planePoint, n);
            return point - n * signedDistance;
        }
    }
}
