using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace VRBrush.Core.Model.ADBrush
{
    /// <summary>
    /// ADBrush形状构建器
    /// 管理轨迹关键点、变形权重、幽灵点，并生成最终的3D Mesh和曲线
    /// </summary>
    public class ADBrushShapeBuilder
    {
        // 已确认的关键点（可见的 0..n-1）
        private readonly List<ADBrushShapePoint> points = new List<ADBrushShapePoint>();
        // 每个点各自的 morph 权重（仅 morph，不包含 size）
        private readonly List<List<float>> perPointWeights = new List<List<float>>();
        // 每个点各自的 size（世界空间宽度）
        private readonly List<float> perPointSizes = new List<float>();

        // 头尾哨兵（不可见，仅用于插值稳定）
        private ADBrushShapePoint? head = null;
        private ADBrushShapePoint? tail = null;

        // 当前形状（v 与 u1..um）
        private MorphableShape morphableShape;

    // 当前全局权重快照（仅 morph，不包含 size）
        private readonly List<float> currentWeights = new List<float>();

        public int PointCount => points.Count;
        public bool HasShadowPoint => head.HasValue; // 兼容旧控制器的查询
        public IReadOnlyList<ADBrushShapePoint> Points => points.AsReadOnly();

        public MorphableShape MorphableShape
        {
            get => morphableShape;
            set
            {
                morphableShape = value;
                EnsureWeightSize();
            }
        }

        public IReadOnlyList<float> MorphWeights => currentWeights.AsReadOnly();

        public ADBrushShapeBuilder(MorphableShape shape = null)
        {
            morphableShape = shape;
            EnsureWeightSize();
        }

        private void EnsureWeightSize()
        {
            int required = (morphableShape?.MorphCount ?? 0);
            while (currentWeights.Count < required) currentWeights.Add(0f);
            while (currentWeights.Count > required && required >= 0) currentWeights.RemoveAt(currentWeights.Count - 1);
        }

        // 新接口：设置/获取当前权重（用于下次确认点时的快照）
        public void SetMorphWeight(int index, float value)
        {
            EnsureWeightSize();
            if (index < 0) return;
            while (index >= currentWeights.Count) currentWeights.Add(0f);
            currentWeights[index] = Mathf.Clamp01(value);
        }

        public float GetMorphWeight(int index)
        {
            if (index >= 0 && index < currentWeights.Count) return currentWeights[index];
            return 0f;
        }

        // 兼容旧接口：把“影子点”理解为 head
        public void UpdateShadowPoint(ADBrushShapePoint newShadowPoint)
        {
            head = newShadowPoint;
        }

        public void AddShadowPointToList()
        {
            if (head.HasValue)
            {
                // 使用默认 size（这个方法已废弃，保留兼容性）
                AddConfirmedPoint(head.Value, new List<float>(currentWeights), 0.02f);
                head = null;
                Debug.Log($"ADBrushShapeBuilder: Added point (total: {points.Count})");
            }
        }

        public void ClearShadowPoint()
        {
            head = null;
        }

        // 新接口：显式设置/清空 head/tail（供控制器将来使用）
        public void SetHead(ADBrushShapePoint h) => head = h;
        public void ClearHead() => head = null;
        public void SetTail(ADBrushShapePoint t) => tail = t;
        public void ClearTail() => tail = null;

        public void AddConfirmedPoint(ADBrushShapePoint point, List<float> weightsSnapshot, float size)
        {
            if (weightsSnapshot == null)
            {
                weightsSnapshot = new List<float>(currentWeights);
            }
            NormalizeWeights(weightsSnapshot);
            points.Add(point);
            perPointWeights.Add(weightsSnapshot);
            perPointSizes.Add(Mathf.Max(0.001f, size)); // 确保 size 不为零
        }
        
        /// <summary>
        /// 替换指定索引处的点（保留原有的权重和大小）
        /// </summary>
        public void ReplacePointAt(int index, ADBrushShapePoint newPoint)
        {
            if (index >= 0 && index < points.Count)
            {
                points[index] = newPoint;
            }
        }

        public void Clear()
        {
            points.Clear();
            perPointWeights.Clear();
            perPointSizes.Clear();
            head = null;
            tail = null;
        }

        private void NormalizeWeights(List<float> w)
        {
            EnsureWeightSize();
            int required = currentWeights.Count;
            while (w.Count < required) w.Add(0f);
            for (int i = 0; i < required; i++) w[i] = Mathf.Clamp01(w[i]);
        }

        // 采样结构（内部使用）
        private struct Sample
        {
            public Vector3 pos;
            public Quaternion rot;  // 完整旋转，与预览保持一致
            public List<float> weights;
            public float size;  // 每个采样点的 size（插值自确认点）
        }

        /// <summary>
        /// 构建曲线采样（基于 Catmull-Rom，使用 head/tail 作为端点外延）
        /// 仅在“真实点之间”的区间采样；head->p0 与 pn-1->tail 不输出样条，但会作为外延点参与端点插值。
        /// </summary>
        private List<Sample> BuildSamples(int subdivisionLevel)
        {
            var samples = new List<Sample>();
            if (points.Count == 0)
            {
                // 没有确认点，如果有 head，就输出一个点用于预览
                if (head.HasValue)
                {
                    samples.Add(new Sample
                    {
                        pos = head.Value.position,
                        rot = head.Value.rotation,
                        weights = new List<float>(currentWeights),
                        size = 0.02f  // 预览默认 size
                    });
                }
                return samples;
            }

            // 逐段生成：point[k] -> point[k+1]
            for (int k = 0; k < points.Count - 1; k++)
            {
                // 四个控制点位置（不使用 head/tail 影响已确认段的形状）
                Vector3 p0 = (k > 0) ? points[k - 1].position : points[k].position; // 边界重复自身
                Vector3 p1 = points[k].position;
                Vector3 p2 = points[k + 1].position;
                Vector3 p3 = (k < points.Count - 2) ? points[k + 2].position : points[k + 1].position; // 边界重复自身

                // 使用 Quaternion.Slerp 直接插值旋转（与 SectionEdgePreview 保持一致）
                Quaternion rot1 = points[k].rotation;
                Quaternion rot2 = points[k + 1].rotation;

                // 权重：在段内线性插值（仅 morph 权重）
                var w1 = GetWeightsForPoint(k);
                var w2 = GetWeightsForPoint(k + 1);

                // Size：在段内线性插值
                float size1 = GetSizeForPoint(k);
                float size2 = GetSizeForPoint(k + 1);

                for (int j = 0; j <= subdivisionLevel; j++)
                {
                    float t = (float)j / subdivisionLevel;
                    Vector3 pos = CatmullRomInterpolate(p0, p1, p2, p3, t);
                    
                    // 直接 Slerp 旋转，与预览保持一致
                    Quaternion rot = Quaternion.Slerp(rot1, rot2, t);

                    var w = LerpWeights(w1, w2, t);
                    float size = Mathf.Lerp(size1, size2, t);

                    samples.Add(new Sample
                    {
                        pos = pos,
                        rot = rot,
                        weights = w,
                        size = size
                    });
                }
            }

            return samples;
        }

        private List<float> GetWeightsForPoint(int index)
        {
            if (index >= 0 && index < perPointWeights.Count)
            {
                return new List<float>(perPointWeights[index]);
            }
            // 兼容：没有存每点权重时，使用当前全局权重
            return new List<float>(currentWeights);
        }

        private float GetSizeForPoint(int index)
        {
            if (index >= 0 && index < perPointSizes.Count)
            {
                return perPointSizes[index];
            }
            // 兼容：没有存每点 size 时，使用默认值
            return 0.02f;
        }
        
        /// <summary>
        /// 公共接口：获取指定索引点的权重快照
        /// </summary>
        public List<float> GetWeightsForPointIndex(int index)
        {
            return GetWeightsForPoint(index);
        }
        
        /// <summary>
        /// 公共接口：获取指定索引点的宽度
        /// </summary>
        public float GetSizeForPointIndex(int index)
        {
            return GetSizeForPoint(index);
        }

        private List<float> LerpWeights(List<float> a, List<float> b, float t)
        {
            int n = Mathf.Max(a.Count, b.Count);
            var r = new List<float>(n);
            for (int i = 0; i < n; i++)
            {
                float ai = (i < a.Count) ? a[i] : 0f;
                float bi = (i < b.Count) ? b[i] : 0f;
                r.Add(Mathf.Lerp(ai, bi, t));
            }
            return r;
        }

        public StrokeCurve BuildStrokeCurve(int subdivisionLevel, bool includeShadow = false)
        {
            var curve = new StrokeCurve();

            // 只有一个点时，输出一个点即可
            if (points.Count == 1)
            {
                var p = points[0];
                curve.AddPoint(p.position, p.rotation);

                // 如果需要包含 shadow（head），并且存在 head，则补一小段预览
                if (includeShadow && head.HasValue)
                {
                    // 从唯一确认点到 head 的临时段
                    Vector3 p0 = p.position; // 无上一个点，重复自身
                    Vector3 p1 = p.position;
                    Vector3 p2 = head.Value.position;
                    Vector3 p3 = head.Value.position; // 无下一个，再次重复

                    Vector3 up1 = p.rotation * Vector3.up;
                    Vector3 up2 = head.Value.rotation * Vector3.up;

                    for (int j = 1; j <= subdivisionLevel; j++)
                    {
                        float t = (float)j / subdivisionLevel;
                        Vector3 pos = CatmullRomInterpolate(p0, p1, p2, p3, t);
                        Vector3 tan = CatmullRomTangent(p0, p1, p2, p3, t).normalized;
                        Vector3 up = Vector3.Slerp(up1, up2, t);
                        up = Vector3.ProjectOnPlane(up, tan);
                        if (up.sqrMagnitude < 1e-6f)
                        {
                            up = Vector3.Cross(tan, Vector3.right);
                            if (up.sqrMagnitude < 1e-6f) up = Vector3.Cross(tan, Vector3.up);
                        }
                        up.Normalize();

                        curve.AddPoint(pos, Quaternion.LookRotation(tan, up));
                    }
                }
                return curve;
            }

            var samples = BuildSamples(subdivisionLevel);
            foreach (var s in samples)
            {
                curve.AddPoint(s.pos, s.rot);
            }

            // 如果要求包含 shadow（head），并且至少有1个确认点，则补上最后一段预览
            if (includeShadow && head.HasValue && points.Count >= 1)
            {
                int last = points.Count - 1;
                var lp = points[last];

                Vector3 p0 = (last > 0) ? points[last - 1].position : lp.position;
                Vector3 p1 = lp.position;
                Vector3 p2 = head.Value.position;
                Vector3 p3 = head.Value.position; // 无进一步点，重复 head

                Vector3 up1 = lp.rotation * Vector3.up;
                Vector3 up2 = head.Value.rotation * Vector3.up;

                for (int j = 1; j <= subdivisionLevel; j++)
                {
                    float t = (float)j / subdivisionLevel;
                    Vector3 pos = CatmullRomInterpolate(p0, p1, p2, p3, t);
                    Vector3 tan = CatmullRomTangent(p0, p1, p2, p3, t).normalized;
                    Vector3 up = Vector3.Slerp(up1, up2, t);
                    up = Vector3.ProjectOnPlane(up, tan);
                    if (up.sqrMagnitude < 1e-6f)
                    {
                        up = Vector3.Cross(tan, Vector3.right);
                        if (up.sqrMagnitude < 1e-6f) up = Vector3.Cross(tan, Vector3.up);
                    }
                    up.Normalize();

                    curve.AddPoint(pos, Quaternion.LookRotation(tan, up));
                }
            }

            return curve;
        }

        public Stroke Build3DMesh(int subdivisionLevel, float globalWidth)
        {
            if (morphableShape == null || morphableShape.BaseShape == null)
            {
                Debug.LogWarning("ADBrushShapeBuilder: Cannot build mesh without a valid morphable shape");
                return null;
            }
            if (points.Count < 2)
            {
                Debug.LogWarning("ADBrushShapeBuilder: Need at least 2 confirmed points to build mesh");
                return null;
            }

            var samples = BuildSamples(subdivisionLevel);
            if (samples.Count < 2)
            {
                Debug.LogWarning("ADBrushShapeBuilder: Not enough samples to build mesh");
                return null;
            }

            // 以第一条样本的权重生成初始截面（Stroke 会在内部复制该截面）
            // 初始化 Stroke，并启用“按点自定义截面”模式
            var initialShape = morphableShape.GetBrushShape(new List<float>(samples[0].weights));
            
            // 根据形状是否有封闭环路来决定是否添加封口
            bool shouldCapEnds = initialShape != null && initialShape.HasCycle();
            var stroke = new Stroke(initialShape, capEnds: shouldCapEnds);
            stroke.EnablePerPointShapes();

            for (int i = 0; i < samples.Count; i++)
            {
                var s = samples[i];

                // 使用每个采样点自己的 size（插值自确认点的 per-point size）
                float width = Mathf.Max(1e-5f, s.size);

                // 使用新的构造函数，直接传入旋转（与预览保持完全一致）
                var sp = new StrokePoint(s.pos, s.rot, width, Time.time);

                // 为该样本计算“变形后”的2D截面，并以 per-point 方式提供给 Stroke
                var morphedShape = morphableShape.GetBrushShape(new List<float>(s.weights));
                Vector2[] shape2D = (morphedShape != null && morphedShape.vertices != null)
                    ? morphedShape.vertices.ToArray()
                    : (initialShape != null ? initialShape.vertices.ToArray() : new Vector2[] { new Vector2(0,0) });
                stroke.AddPointWithShape(sp, shape2D);
            }

            stroke.Complete();
            return stroke;
        }

        // Catmull-Rom 插值/切线
        private Vector3 CatmullRomInterpolate(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private Vector3 CatmullRomTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            return 0.5f * ((-p0 + p2) + 2f * (2f * p0 - 5f * p1 + 4f * p2 - p3) * t + 3f * (-p0 + 3f * p1 - 3f * p2 + p3) * t2);
        }

        public ADBrushShapePoint? GetLastConfirmedPoint()
        {
            if (points.Count > 0) return points[points.Count - 1];
            return null;
        }

        // 兼容旧接口
        public ADBrushShapePoint? GetShadowPoint() => head;
    }
}
