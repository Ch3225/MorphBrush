using UnityEngine;

namespace VRBrush.Interface.Brush.AdaptiBrush
{
    /// <summary>
    /// AdaptiBrush核心算法实现（恢复接近提交 604388bb8da145a736b54027f206ba4d7076b0d0 的逻辑）
    /// 说明：当前版本回退去除了后期加入的额外连续性与权重修正，保持论文基础公式 (2)(3)(4)(5)(6) 的等价实现，
    /// 仅保留：
    /// 1. 轨迹切线用三点贝塞尔近似（原始实现方式）
    /// 2. V_tilde 使用 t × (V' × t) 并按与上一帧 ruling 的点积选取符号
    /// 3. 能量函数 world + control，control 中将控制器轴投影到正交平面后若与 vTilde 方向相反会翻转，保持与旧实现等价
    /// 4. 搜索 -90° ~ 90° 以 0.04° 步长暴力最优
    /// 额外安全措施：只在必要时做最小幅度 fallback，不再引入改变优化目标的附加权重。
    /// </summary>
    public static class AdaptiBrushAlgorithm
    {
        private const float EPSILON = 5f * Mathf.Deg2Rad; // 与旧实现保持一致
        private const int ANGLE_SAMPLES = 2250;           // -90~90, step 0.04°
        private const float ANGLE_STEP = 0.04f * Mathf.Deg2Rad;

        /// <summary>
        /// 计算新的ruling方向
        /// </summary>
        /// <param name="vTilde">输出的V_tilde方向</param>
        /// <param name="currentPos">当前控制器位置</param>
        /// <param name="previousPos">前一帧控制器位置</param>
        /// <param name="previousPos2">前两帧控制器位置</param>
        /// <param name="controllerFrame">当前控制器坐标系{R, U, F}</param>
        /// <param name="previousRuling">前一帧的ruling方向</param>
        /// <param name="previousTangent">前一帧的切线方向</param>
        /// <returns>新的ruling方向</returns>
        public static Vector3 ComputeRulingDirection(
            out Vector3 vTilde,
            Vector3 currentPos,
            Vector3 previousPos,
            Vector3 previousPos2,
            Matrix4x4 controllerFrame,
            Vector3 previousRuling,
            Vector3 previousTangent)
        {
            Vector3 tangent = ComputeTrajectoryTangent(currentPos, previousPos, previousPos2);

            // 与旧实现保持：若当前切线与上一 ruling 非常接近，使用上一切线作为上一 ruling 参考
            if (previousRuling != Vector3.zero && Vector3.Angle(tangent, previousRuling) < EPSILON * Mathf.Rad2Deg)
                previousRuling = previousTangent;

            Vector3 rightAxis = controllerFrame.GetColumn(0);
            Vector3 upAxis = controllerFrame.GetColumn(1);
            Vector3 forwardAxis = controllerFrame.GetColumn(2);

            vTilde = ComputeVTilde(tangent, previousRuling);
            var ruling = OptimizeRulingDirection(tangent, vTilde, rightAxis, upAxis, forwardAxis);

            // 最小安全：强制正交
            if (tangent != Vector3.zero)
            {
                ruling = Vector3.ProjectOnPlane(ruling, tangent).normalized;
            }
            return ruling;
        }

        /// <summary>
        /// 初始化第一个ruling方向（根据论文3.2.1节）
        /// </summary>
        public static Vector3 InitializeRuling(Vector3 tangent, Vector3 upAxis, Vector3 forwardAxis)
        {
            // 按旧逻辑：>45° 用 t×U 否则 t×F
            float angleWithUp = Vector3.Angle(tangent, upAxis);
            Vector3 ruling = angleWithUp > 45f ? Vector3.Cross(tangent, upAxis).normalized : Vector3.Cross(tangent, forwardAxis).normalized;
            return ruling == Vector3.zero ? Vector3.Cross(tangent, Vector3.up).normalized : ruling;
        }

        /// <summary>
        /// 计算轨迹切线方向（基于论文的C²连续cubic Bezier构造）
        /// </summary>
        private static Vector3 ComputeTrajectoryTangent(Vector3 p, Vector3 p1, Vector3 p2)
        {
            // 论文中的C²连续三次贝塞尔曲线构造
            // h1 = p' + 1/6 * (p'' - p)
            // h0 = h1 + 1/3 * (p' - p)
            Vector3 h1 = p1 + (p2 - p) / 6f;
            Vector3 h0 = h1 + (p1 - p) / 3f;

            // 切线方向为 p - h0
            Vector3 tangent = (p - h0).normalized;
            return tangent;
        }

        /// <summary>
        /// 计算V_tilde（论文公式2-3）
        /// 严格按照原始论文的精确定义实现
        /// 关键修正：确保连续性判断的正确性
        /// </summary>
        public static Vector3 ComputeVTilde(Vector3 tangent, Vector3 previousRuling)
        {
            if (previousRuling == Vector3.zero || tangent == Vector3.zero)
                return previousRuling != Vector3.zero ? previousRuling : Vector3.forward;

            Vector3 vBar = Vector3.Cross(tangent, Vector3.Cross(previousRuling, tangent));
            if (vBar == Vector3.zero) return previousRuling;
            vBar = vBar.normalized;
            return Vector3.Dot(vBar, previousRuling) > -Vector3.Dot(vBar, previousRuling) ? vBar : -vBar;
        }

        /// <summary>
        /// 优化求解ruling方向（论文公式4-6）
        /// 使用完整的180度搜索范围以确保找到全局最优解
        /// </summary>
        private static Vector3 OptimizeRulingDirection(
            Vector3 tangent,
            Vector3 vTilde,
            Vector3 rightAxis,
            Vector3 upAxis,
            Vector3 forwardAxis)
        {
            float minEnergy = float.MaxValue;
            Vector3 best = vTilde;
            for (int i = 0; i < ANGLE_SAMPLES; i++)
            {
                float theta = -90f * Mathf.Deg2Rad + i * ANGLE_STEP;
                Vector3 candidate = RotateVectorAroundAxis(vTilde, tangent, theta);
                candidate = (candidate - Vector3.Dot(candidate, tangent) * tangent).normalized;
                if (candidate == Vector3.zero) continue;
                // 论文约束：V·Ṽ > 0
                if (Vector3.Dot(candidate, vTilde) <= 0f) continue;
                float e = ComputeEnergy(candidate, vTilde, rightAxis, upAxis, forwardAxis, tangent);
                if (e < minEnergy)
                {
                    minEnergy = e;
                    best = candidate;
                }
            }
            return best;
        }

        /// <summary>
        /// 计算总能量函数（论文公式6）
        /// 严格按照论文实现，关键修正权重计算逻辑
        /// </summary>
        public static float ComputeEnergy(
            Vector3 ruling,
            Vector3 vTilde,
            Vector3 rightAxis,
            Vector3 upAxis,
            Vector3 forwardAxis,
            Vector3 tangent)
        {
            float worldE = 1f - Mathf.Pow(Vector3.Dot(ruling, vTilde), 2f);
            float controlE = 0f;
            Vector3[] axes = { rightAxis, upAxis, forwardAxis };
            foreach (var axis in axes)
            {
                if (Vector3.Angle(axis, tangent) < EPSILON * Mathf.Rad2Deg) continue;
                Vector3 projected = (axis - Vector3.Dot(axis, tangent) * tangent).normalized;
                if (projected == Vector3.zero) continue;
                // 旧实现：若与 vTilde 夹角大于 90° 翻转
                if (Vector3.Dot(projected, vTilde) < 0) projected = -projected;
                // 论文公式(5)中的 D·Ṽ 使用原始轴（非投影轴）
                float axisDot = Vector3.Dot(axis, vTilde);
                controlE += axisDot * (1f - Mathf.Pow(Vector3.Dot(ruling, projected), 2f));
            }
            return worldE + controlE; // 不再附加额外权重
        }

        /// <summary>
        /// 围绕轴旋转向量
        /// </summary>
        private static Vector3 RotateVectorAroundAxis(Vector3 vector, Vector3 axis, float angle)
        {
            return Quaternion.AngleAxis(angle * Mathf.Rad2Deg, axis) * vector;
        }
    }
}
