using UnityEngine;

namespace VRBrush.Core.Model
{
    /// <summary>
    /// 表示一个笔画上的控制点，包含位置和ruling信息
    /// </summary>
    [System.Serializable]
    public struct StrokePoint
    {
        public Vector3 position;        // 控制器位置
        public Vector3 rulingDirection; // ruling方向向量（单位向量）
        public Vector3 tangent;         // 轨迹切线方向
        public Vector3 normal;          // 法向量
        public Quaternion rotation;     // 完整旋转（用于精确坐标系计算）
        public float width;             // 笔刷宽度
        public float timestamp;         // 时间戳

        public StrokePoint(Vector3 pos, Vector3 ruling, Vector3 tang, float w, float time)
        {
            position = pos;
            rulingDirection = ruling.normalized;
            tangent = tang.normalized;
            normal = Vector3.Cross(tangent, rulingDirection).normalized;
            rotation = Quaternion.LookRotation(tangent, rulingDirection);
            width = w;
            timestamp = time;
        }
        
        /// <summary>
        /// 使用完整旋转创建 StrokePoint（推荐方式，避免精度损失）
        /// </summary>
        public StrokePoint(Vector3 pos, Quaternion rot, float w, float time)
        {
            position = pos;
            rotation = rot;
            tangent = rot * Vector3.forward;
            rulingDirection = rot * Vector3.up;
            normal = rot * Vector3.right;
            width = w;
            timestamp = time;
        }

        /// <summary>
        /// 获取ruling的起始点
        /// </summary>
        public Vector3 RulingStart => position - rulingDirection * (width * 0.5f);

        /// <summary>
        /// 获取ruling的结束点
        /// </summary>
        public Vector3 RulingEnd => position + rulingDirection * (width * 0.5f);
    }
}
