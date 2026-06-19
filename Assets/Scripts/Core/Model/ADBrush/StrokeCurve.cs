using System.Collections.Generic;
using UnityEngine;

namespace VRBrush.Core.Model.ADBrush
{
    /// <summary>
    /// 轨迹曲线点（用于预览显示）
    /// </summary>
    [System.Serializable]
    public struct CurvePoint
    {
        public Vector3 position;       // 点的位置
        public Quaternion rotation;    // 点的旋转（截面方向）

        public CurvePoint(Vector3 pos, Quaternion rot)
        {
            position = pos;
            rotation = rot;
        }
    }

    /// <summary>
    /// 笔画曲线数据
    /// 用于StrokeCurvePreview的显示
    /// </summary>
    [System.Serializable]
    public class StrokeCurve
    {
        [SerializeField] private List<CurvePoint> curvePoints = new List<CurvePoint>();

        public IReadOnlyList<CurvePoint> CurvePoints => curvePoints.AsReadOnly();
        public int PointCount => curvePoints.Count;

        public StrokeCurve()
        {
            curvePoints = new List<CurvePoint>();
        }

        /// <summary>
        /// 添加曲线点
        /// </summary>
        public void AddPoint(CurvePoint point)
        {
            curvePoints.Add(point);
        }

        /// <summary>
        /// 添加曲线点（使用位置和旋转）
        /// </summary>
        public void AddPoint(Vector3 position, Quaternion rotation)
        {
            curvePoints.Add(new CurvePoint(position, rotation));
        }

        /// <summary>
        /// 清空曲线
        /// </summary>
        public void Clear()
        {
            curvePoints.Clear();
        }

        /// <summary>
        /// 获取指定索引的点
        /// </summary>
        public CurvePoint GetPoint(int index)
        {
            if (index >= 0 && index < curvePoints.Count)
            {
                return curvePoints[index];
            }
            return default;
        }
    }
}
