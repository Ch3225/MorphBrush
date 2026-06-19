using UnityEngine;

namespace VRBrush.Core.Model.ADBrush
{
    /// <summary>
    /// ADBrush轨迹上的控制点
    /// 存储位置和旋转姿态，用于定义3D曲线上的截面方向
    /// </summary>
    [System.Serializable]
    public struct ADBrushShapePoint
    {
        public Vector3 position;   // 点的位置Pi
        public Quaternion rotation; // 点的旋转姿态（定义截面的朝向）

        public ADBrushShapePoint(Vector3 pos, Quaternion rot)
        {
            position = pos;
            rotation = rot;
        }

        /// <summary>
        /// 获取切线方向（二维表示，忽略自旋）
        /// 返回截面法线方向的球面坐标（经度theta, 纬度phi）
        /// </summary>
        public Vector2 GetTangentDirection()
        {
            // 从四元数提取前向向量作为法线方向
            Vector3 normal = rotation * Vector3.forward;
            
            // 转换为球面坐标
            float theta = Mathf.Atan2(normal.x, normal.z); // 经度（绕Y轴的角度）
            float phi = Mathf.Asin(normal.y); // 纬度（与XZ平面的角度）
            
            return new Vector2(theta, phi);
        }

        /// <summary>
        /// 绕切线方向旋转deltaAngle角度，返回新的点
        /// </summary>
        /// <param name="deltaAngle">旋转角度（度数）</param>
        public ADBrushShapePoint Rotate(float deltaAngle)
        {
            // 获取当前的前向向量（切线/法线方向）
            Vector3 forward = rotation * Vector3.forward;
            
            // 绕前向向量旋转
            Quaternion deltaRotation = Quaternion.AngleAxis(deltaAngle, forward);
            Quaternion newRotation = deltaRotation * rotation;
            
            return new ADBrushShapePoint(position, newRotation);
        }

        /// <summary>
        /// 基于最新的点和目标法线方向，计算旋转距离最短（四元数差最小）的新点
        /// </summary>
        /// <param name="latestOne">最新的参考点</param>
        /// <param name="targetNormal">目标法线方向（二维球面坐标：theta, phi）</param>
        public static ADBrushShapePoint GetNewOneByLatestOne(ADBrushShapePoint latestOne, Vector2 targetNormal)
        {
            // 将球面坐标转回3D向量
            float theta = targetNormal.x;
            float phi = targetNormal.y;
            Vector3 normalDir = new Vector3(
                Mathf.Sin(theta) * Mathf.Cos(phi),
                Mathf.Sin(phi),
                Mathf.Cos(theta) * Mathf.Cos(phi)
            ).normalized;

            // 使用无扭转旋转方法
            Quaternion newRotation = RotateWithoutTwist(latestOne.rotation, normalDir);

            return new ADBrushShapePoint(latestOne.position, newRotation);
        }
        
        /// <summary>
        /// 计算从上一个旋转到新的前向方向的"无扭转"旋转。
        /// 将上一个旋转的forward方向转到新的forward方向，up方向随之旋转但不产生额外扭转。
        /// 就像指针镶了个底盘，把指针摆过去时底盘也跟着摆但不绕指针轴旋转。
        /// </summary>
        /// <param name="previousRotation">上一个点的旋转</param>
        /// <param name="newForward">新的前向方向（切线/法线）</param>
        /// <returns>新的旋转，forward指向newForward，up是从previousRotation的up无扭转转过来的</returns>
        public static Quaternion RotateWithoutTwist(Quaternion previousRotation, Vector3 newForward)
        {
            Vector3 prevForward = previousRotation * Vector3.forward;
            newForward = newForward.normalized;
            
            // 计算从上一个forward到新forward的旋转
            Quaternion deltaRot = Quaternion.FromToRotation(prevForward, newForward);
            
            // 将这个旋转应用到整个上一个旋转上
            // 这样up方向也会跟着旋转，但不会产生额外的扭转
            Quaternion newRotation = deltaRot * previousRotation;
            
            return newRotation;
        }
    }
}
