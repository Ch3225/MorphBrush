using UnityEngine;
using System.Collections.Generic;

namespace VRBrush.Interface.Brush.HelioBrush
{
    /// <summary>
    /// HelioBrush核心算法实现
    /// 基于飞机航行原理的笔刷方向控制算法
    /// 
    /// 算法核心：
    /// 1. Yaw和Pitch方向：基于轨迹曲率计算，与控制器旋转无关
    /// 2. Roll方向：基于控制器旋转变化的累积值，使用rad/m单位
    /// </summary>
    public static class HelioBrushAlgorithm
    {
        /// <summary>
        /// HelioBrush绘制状态
        /// </summary>
        public class HelioBrushState
        {
            // Roll累积值（rad/m）
            public float accumulatedRollRate = 0f;
            // 缓冲目标（即时计算得到的临时值）
            public float targetRollRate = 0f;
            // 缓冲阈值 h（rad/m），用于 state 层的默认保护
            public float rollBufferThreshold = 0.15f;

            // 上一帧的控制器旋转
            public Quaternion previousControllerRotation = Quaternion.identity;

            // 上一帧的笔刷方向（用于计算曲率）
            public Vector3 previousBrushDirection = Vector3.forward;

            // 上一帧的切线方向
            public Vector3 previousTangent = Vector3.forward;

            // 上一帧的位置
            public Vector3 previousPosition = Vector3.zero;

            // 平滑用：上一增量（用于简单一阶低通）
            public float lastRateDelta = 0f;
            // 小段累计的旋转角度与长度（用于聚合后再更新，减少微抖放大）
            public float pendingTwistAngle = 0f;
            public float pendingLength = 0f;

            // 是否已初始化
            public bool isInitialized = false;
            // 初始方向平滑帧计数
            public int initialFrameCount = 0;
        }

        /// <summary>
        /// 计算HelioBrush的ruling方向
        /// </summary>
        /// <param name="currentPosition">当前控制器位置</param>
        /// <param name="currentRotation">当前控制器旋转</param>
        /// <param name="state">HelioBrush状态</param>
        /// <param name="isDrawingStarted">是否刚开始绘制</param>
        /// <returns>新的ruling方向</returns>
        public static Vector3 ComputeRulingDirection(
            Vector3 currentPosition,
            Quaternion currentRotation,
            ref HelioBrushState state,
            bool isDrawingStarted,
            float rollSensitivity,
            float minSegmentLengthClamp,
            float twistAngleThresholdRad,
            float maxTwistPerMeter,
            float smallLengthScale,
            float rateSmoothing,
            float rollBufferThreshold,
            float bufferCatchupFactor,
            int warmupSmoothFrames)
        {
            // 1. 计算轨迹切线方向
            Vector3 tangent = ComputeTrajectoryTangent(currentPosition, state.previousPosition);

            // 2. 初始化或更新状态
            if (!state.isInitialized || isDrawingStarted)
            {
                InitializeState(currentPosition, currentRotation, tangent, ref state);
                state.initialFrameCount = 0;
            }

            // 3. 计算基础笔刷方向（基于轨迹曲率的Yaw和Pitch）
            Vector3 baseBrushDirection = ComputeBaseBrushDirection(tangent, state.previousTangent, state.previousBrushDirection);

            // 4. 计算Roll角度（基于控制器旋转变化）
            float rawRollRate = ComputeRollAngle(
                currentPosition,
                currentRotation,
                state.previousControllerRotation,
                tangent,
                rollSensitivity,
                minSegmentLengthClamp,
                twistAngleThresholdRad,
                maxTwistPerMeter,
                smallLengthScale,
                rateSmoothing,
                ref state);

            // 5. 应用Roll到基础方向
            // 4.1 缓冲：target = rawRollRate；accumulatedRollRate 为 a
            state.targetRollRate = rawRollRate; // rawRollRate 已经是累积后的 rad/m （沿用旧含义）
            state.rollBufferThreshold = rollBufferThreshold;
            state.accumulatedRollRate = ApplyRollBuffer(state.accumulatedRollRate, state.targetRollRate, rollBufferThreshold, bufferCatchupFactor);

            // 可选对数压缩（需求 4：先注释，留作观察）
            // float compressed = Mathf.Sign(state.accumulatedRollRate) * Mathf.Log(1f + Mathf.Abs(state.accumulatedRollRate));
            // state.accumulatedRollRate = compressed;

            // 5. 应用Roll到基础方向（此处把 accumulatedRollRate 视作 rollAngle）
            Vector3 finalBrushDirection = ApplyRoll(baseBrushDirection, state.accumulatedRollRate, tangent);
            // 初始若干帧平滑（减缓突变）
            if (warmupSmoothFrames > 0 && state.initialFrameCount < warmupSmoothFrames && state.previousBrushDirection != Vector3.forward)
            {
                float t = (state.initialFrameCount + 1f) / (float)warmupSmoothFrames; // 0->1
                finalBrushDirection = Vector3.Slerp(state.previousBrushDirection, finalBrushDirection, t * t);
            }
            state.initialFrameCount++;
            // 6. 更新状态
            UpdateState(currentPosition, currentRotation, tangent, finalBrushDirection, ref state);

            return finalBrushDirection.normalized;
        }

        /// <summary>
        /// 初始化HelioBrush状态
        /// </summary>
        private static void InitializeState(
            Vector3 currentPosition,
            Quaternion currentRotation,
            Vector3 tangent,
            ref HelioBrushState state)
        {
            state.accumulatedRollRate = 0f;
            state.targetRollRate = 0f;
            state.previousControllerRotation = currentRotation;
            state.previousBrushDirection = currentRotation * Vector3.up; // 初始方向与控制器一致
            state.previousTangent = tangent;
            state.previousPosition = currentPosition;
            state.lastRateDelta = 0f;
            state.pendingTwistAngle = 0f;
            state.pendingLength = 0f;
            state.isInitialized = true;
        }

        /// <summary>
        /// 计算轨迹切线方向
        /// </summary>
        private static Vector3 ComputeTrajectoryTangent(Vector3 currentPos, Vector3 previousPos)
        {
            if (previousPos == Vector3.zero)
            {
                return Vector3.forward; // 默认前向
            }

            Vector3 direction = currentPos - previousPos;
            return direction.magnitude > 0.001f ? direction.normalized : Vector3.forward;
        }

        /// <summary>
        /// 计算基础笔刷方向（基于轨迹曲率的Yaw和Pitch）
        /// 这部分与控制器旋转无关，仅基于位置变化
        /// </summary>
        public static Vector3 ComputeBaseBrushDirection(
            Vector3 currentTangent,
            Vector3 previousTangent,
            Vector3 previousBrushDirection)
        {
            if (previousTangent == Vector3.zero || previousBrushDirection == Vector3.zero)
            {
                // 如果没有历史数据，使用默认方向
                return Vector3.up;
            }

            // 计算切线变化（曲率）
            Vector3 tangentChange = currentTangent - previousTangent;

            // 如果切线变化很小，保持前一帧的方向
            if (tangentChange.magnitude < 0.001f)
            {
                return previousBrushDirection;
            }

            // 计算旋转轴和角度（基于切线变化）
            Vector3 rotationAxis = Vector3.Cross(previousTangent, currentTangent).normalized;
            float rotationAngle = Vector3.Angle(previousTangent, currentTangent) * Mathf.Deg2Rad;

            if (rotationAxis.magnitude < 0.001f || rotationAngle < 0.001f)
            {
                return previousBrushDirection;
            }

            // 将前一帧的笔刷方向围绕旋转轴旋转
            Quaternion rotation = Quaternion.AngleAxis(rotationAngle * Mathf.Rad2Deg, rotationAxis);
            Vector3 newDirection = rotation * previousBrushDirection;

            // 确保新方向与当前切线垂直
            newDirection = Vector3.ProjectOnPlane(newDirection, currentTangent).normalized;

            if (newDirection.magnitude < 0.001f)
            {
                // 如果投影后为零，构建一个垂直方向
                newDirection = Vector3.Cross(currentTangent, Vector3.up).normalized;
                if (newDirection.magnitude < 0.001f)
                {
                    newDirection = Vector3.Cross(currentTangent, Vector3.right).normalized;
                }
            }

            return newDirection;
        }

        /// <summary>
        /// 计算Roll角度（基于控制器旋转变化）
        /// 这是算法的核心：计算rad/m单位的Roll变化率并累积
        /// </summary>
        /// <summary>
        /// 计算Roll角度（基于控制器旋转变化）
        /// 这是算法的核心：计算rad/m单位的Roll变化率并累积
        /// </summary>
        private static float ComputeRollAngle(
            Vector3 currentPosition,
            Quaternion currentRotation,
            Quaternion previousRotation,
            Vector3 tangent,
            float rollSensitivity,
            float minSegmentLengthClamp,
            float twistAngleThresholdRad,
            float maxTwistPerMeter,
            float smallLengthScale,
            float rateSmoothing,
            ref HelioBrushState state)
        {
            if (previousRotation == Quaternion.identity)
            {
                return state.accumulatedRollRate;
            }

            // 计算从上一帧到当前帧的旋转变化
            Quaternion rotationDelta = currentRotation * Quaternion.Inverse(previousRotation);

            // 将四元数转换为轴角表示
            rotationDelta.ToAngleAxis(out float angleInDegrees, out Vector3 rotationAxis);

            if (angleInDegrees < 0.001f)
                return state.accumulatedRollRate;

            float angleInRadians = angleInDegrees * Mathf.Deg2Rad;

            // 计算旋转轴与切线的夹角
            float angleWithTangent = Vector3.Angle(rotationAxis, tangent) * Mathf.Deg2Rad;

            // 计算cos值
            float cosValue = Mathf.Cos(angleWithTangent);

            // 计算这一帧相对切线的“纯 twist”角度（沿切线的分量）
            float twistAngle = angleInRadians * cosValue; // 已是沿切线的角度

            // 死区：过滤极小微抖
            if (Mathf.Abs(twistAngle) < twistAngleThresholdRad)
                return state.accumulatedRollRate;

            // 真实位移长度
            float segmentLength = Vector3.Distance(currentPosition, state.previousPosition);
            if (segmentLength < 1e-6f) segmentLength = minSegmentLengthClamp; // 防止除零

            // 小段衰减：当移动距离很小时，直接按比例降低贡献，避免除以很小长度放大
            float smallLenWeight = Mathf.Clamp01(segmentLength / smallLengthScale);

            // 基础rate（rad/m）
            float baseRateDelta = (twistAngle / Mathf.Max(segmentLength, minSegmentLengthClamp)) * rollSensitivity * smallLenWeight;

            // 聚合模式：将多个很短段落累计到一定长度后再更新，避免噪声
            state.pendingTwistAngle += twistAngle;
            state.pendingLength += segmentLength;

            float aggregatedRateDelta = 0f;
            const float AGGREGATE_MIN_LENGTH = 0.01f; // 1cm 聚合阈值
            if (state.pendingLength >= AGGREGATE_MIN_LENGTH)
            {
                float avgTwistPerMeter = (state.pendingTwistAngle / state.pendingLength) * rollSensitivity;
                aggregatedRateDelta = avgTwistPerMeter * smallLenWeight;
                state.pendingTwistAngle = 0f;
                state.pendingLength = 0f;
            }

            // 选择：如果本段很短，使用聚合；否则使用base
            float chosenDelta = (segmentLength < AGGREGATE_MIN_LENGTH * 0.5f) ? aggregatedRateDelta : baseRateDelta;

            // 简单一阶低通平滑
            chosenDelta = Mathf.Lerp(chosenDelta, state.lastRateDelta, rateSmoothing);
            state.lastRateDelta = chosenDelta;

            // Clamp 防止极端爆发（单位：rad/m）
            chosenDelta = Mathf.Clamp(chosenDelta, -maxTwistPerMeter, maxTwistPerMeter);

            state.accumulatedRollRate += chosenDelta;

            return state.accumulatedRollRate;
        }

        /// <summary>
        /// 应用Roll角度到基础笔刷方向
        /// </summary>
        private static Vector3 ApplyRoll(Vector3 baseDirection, float rollAngle, Vector3 tangent)
        {
            if (Mathf.Abs(rollAngle) < 0.001f)
            {
                return baseDirection;
            }

            // 围绕切线旋转基础方向
            Quaternion rollRotation = Quaternion.AngleAxis(rollAngle * Mathf.Rad2Deg, tangent);
            Vector3 rolledDirection = rollRotation * baseDirection;

            return rolledDirection.normalized;
        }

        /// <summary>
        /// 更新HelioBrush状态
        /// </summary>
        private static void UpdateState(
            Vector3 currentPosition,
            Quaternion currentRotation,
            Vector3 tangent,
            Vector3 brushDirection,
            ref HelioBrushState state)
        {
            state.previousPosition = currentPosition;
            state.previousControllerRotation = currentRotation;
            state.previousTangent = tangent;
            state.previousBrushDirection = brushDirection;
        }

        /// <summary>
        /// 重置HelioBrush状态（用于开始新的笔画）
        /// </summary>
        /// <summary>
        /// 重置HelioBrush状态（用于开始新的笔画）
        /// </summary>
        public static void ResetState(ref HelioBrushState state)
        {
            state.accumulatedRollRate = 0f;
            state.targetRollRate = 0f;
            state.previousControllerRotation = Quaternion.identity;
            state.previousBrushDirection = Vector3.forward;
            state.previousTangent = Vector3.forward;
            state.previousPosition = Vector3.zero;
            state.lastRateDelta = 0f;
            state.pendingTwistAngle = 0f;
            state.pendingLength = 0f;
            state.isInitialized = false;
        }

        /// <summary>
        /// 缓冲机制：a 向 atemp 追赶，保持 |a - atemp| <= h 区域内的弹性拖拽
        /// 类比：弹性绳长度 h，若超出则按 catchupFactor 拉近。
        /// </summary>
        private static float ApplyRollBuffer(float current, float target, float threshold, float catchupFactor)
        {
            float diff = target - current;
            float abs = Mathf.Abs(diff);
            if (abs <= threshold) return current; // 在缓冲带内不动
            float step = (abs - threshold) * Mathf.Clamp01(catchupFactor);
            return current + Mathf.Sign(diff) * step;
        }
    }
}
