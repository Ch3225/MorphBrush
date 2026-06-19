using UnityEngine;

namespace VRBrush.Util
{
    /// <summary>
    /// 2D 数学工具类，提供统一的坐标转换和角度处理
    /// </summary>
    public static class Math2D
    {
        /// <summary>
        /// 将极坐标转换为直角坐标 (r, theta) -> (x, y)
        /// theta 单位：弧度
        /// </summary>
        public static Vector2 PolarToCartesian(float r, float thetaRadians)
        {
            float x = r * Mathf.Cos(thetaRadians);
            float y = r * Mathf.Sin(thetaRadians);
            return new Vector2(x, y);
        }

        /// <summary>
        /// 将极坐标转换为直角坐标 (r, theta) -> (x, y)
        /// theta 单位：PI 系数 (0.5 = 90度)
        /// </summary>
        public static Vector2 PolarToCartesianPI(float r, float thetaPI)
        {
            float thetaRadians = thetaPI * Mathf.PI;
            return PolarToCartesian(r, thetaRadians);
        }

        /// <summary>
        /// 将直角坐标转换为极坐标 (x, y) -> (r, theta)
        /// theta 单位：弧度
        /// </summary>
        public static Vector2 CartesianToPolar(Vector2 cartesian)
        {
            float r = cartesian.magnitude;
            float thetaRadians = Mathf.Atan2(cartesian.y, cartesian.x);
            return new Vector2(r, thetaRadians);
        }

        /// <summary>
        /// 将直角坐标转换为极坐标 (x, y) -> (r, theta)
        /// theta 单位：PI 系数
        /// </summary>
        public static Vector2 CartesianToPolarPI(Vector2 cartesian)
        {
            Vector2 polar = CartesianToPolar(cartesian);
            polar.y /= Mathf.PI;
            return polar;
        }

        /// <summary>
        /// 将极坐标转换为 3D 直角坐标 (r, theta) -> (x, 0, z)
        /// theta 单位：弧度
        /// </summary>
        public static Vector3 PolarToCartesian3D(float r, float thetaRadians)
        {
            float x = r * Mathf.Cos(thetaRadians);
            float z = r * Mathf.Sin(thetaRadians);
            return new Vector3(x, 0f, z);
        }

        /// <summary>
        /// 将极坐标转换为 3D 直角坐标 (r, theta) -> (x, y, z)
        /// theta 单位：弧度
        /// </summary>
        public static Vector3 PolarToCartesian3D(float r, float thetaRadians, float y)
        {
            float x = r * Mathf.Cos(thetaRadians);
            float z = r * Mathf.Sin(thetaRadians);
            return new Vector3(x, y, z);
        }
    }
}
