using UnityEngine;

namespace VRBrush.Core.Visual.Element
{
    /// <summary>
    /// 使用LineRenderer显示2D BrushShape的轮廓
    /// 用于MorphEditor的幽灵预览和变形预览
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class ShapeVisual : MonoBehaviour
    {
        private LineRenderer lineRenderer;
        private Material sharedMaterial;

        // ==================== Unity Lifecycle ====================

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            ConfigureLineRenderer();
        }

        // ==================== Public API ====================

        /// <summary>
        /// 更新显示的形状
        /// </summary>
        /// <param name="shape">要显示的BrushShape</param>
        /// <param name="coordinateSystem">坐标系Transform（左手控制器）</param>
        /// <param name="shapeSize">形状显示大小</param>
        public void UpdateShape(Model.BrushShape shape, Transform coordinateSystem, float shapeSize)
        {
            if (shape == null || coordinateSystem == null)
            {
                lineRenderer.positionCount = 0;
                return;
            }

            // 构建闭合的轮廓线
            var edges = shape.Edges;
            if (edges == null || edges.Count == 0)
            {
                // 如果没有边定义，按顺序连接所有点
                UpdateShapeFromNodes(shape, coordinateSystem, shapeSize);
                return;
            }

            // 使用边定义构建轮廓
            UpdateShapeFromEdges(shape, coordinateSystem, shapeSize);
        }

        /// <summary>
        /// 设置材质和颜色
        /// </summary>
        public void SetMaterial(Material material)
        {
            if (material != null)
            {
                sharedMaterial = material;
                lineRenderer.material = material;
            }
        }

        /// <summary>
        /// 设置线条颜色（使用渐变）
        /// </summary>
        public void SetColor(Color color)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(color, 0.0f), new GradientColorKey(color, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(color.a, 0.0f), new GradientAlphaKey(color.a, 1.0f) }
            );
            lineRenderer.colorGradient = gradient;
        }

        /// <summary>
        /// 设置线宽
        /// </summary>
        public void SetWidth(float width)
        {
            lineRenderer.startWidth = width;
            lineRenderer.endWidth = width;
        }

        /// <summary>
        /// 显示或隐藏
        /// </summary>
        public void SetVisible(bool visible)
        {
            lineRenderer.enabled = visible;
        }

        // ==================== Private Methods ====================

        private void ConfigureLineRenderer()
        {
            lineRenderer.useWorldSpace = true;
            lineRenderer.loop = true; // 闭合形状
            lineRenderer.startWidth = 0.002f;
            lineRenderer.endWidth = 0.002f;
            lineRenderer.alignment = LineAlignment.View; // 面向相机
            lineRenderer.numCornerVertices = 8; // 圆滑拐角
            lineRenderer.numCapVertices = 4;
            
            // 默认颜色：半透明白色
            SetColor(new Color(1f, 1f, 1f, 0.5f));
        }

        /// <summary>
        /// 按节点顺序构建形状（无边定义时）
        /// </summary>
        private void UpdateShapeFromNodes(Model.BrushShape shape, Transform coordinateSystem, float shapeSize)
        {
            var nodes = shape.Nodes;
            lineRenderer.positionCount = nodes.Count;

            for (int i = 0; i < nodes.Count; i++)
            {
                Vector3 localPos = new Vector3(nodes[i].x, nodes[i].y, 0f) * shapeSize;
                Vector3 worldPos = coordinateSystem.TransformPoint(localPos);
                lineRenderer.SetPosition(i, worldPos);
            }
        }

        /// <summary>
        /// 使用边定义构建形状轮廓
        /// </summary>
        private void UpdateShapeFromEdges(Model.BrushShape shape, Transform coordinateSystem, float shapeSize)
        {
            var nodes = shape.Nodes;
            var edges = shape.Edges;

            // 简单实现：直接按边的顺序绘制
            // TODO: 更复杂的实现可以追踪连续的边形成完整轮廓
            lineRenderer.positionCount = edges.Count * 2;

            for (int i = 0; i < edges.Count; i++)
            {
                var edge = edges[i];

                // BrushShape.Edges 使用的是 Vector2Int，因此这里用 x/y 访问两个端点索引
                int indexA = edge.x;
                int indexB = edge.y;

                if (indexA < 0 || indexA >= nodes.Count || indexB < 0 || indexB >= nodes.Count)
                {
                    continue; // 越界保护
                }

                Vector3 localPosA = new Vector3(nodes[indexA].x, nodes[indexA].y, 0f) * shapeSize;
                Vector3 localPosB = new Vector3(nodes[indexB].x, nodes[indexB].y, 0f) * shapeSize;

                Vector3 worldPosA = coordinateSystem.TransformPoint(localPosA);
                Vector3 worldPosB = coordinateSystem.TransformPoint(localPosB);

                lineRenderer.SetPosition(i * 2, worldPosA);
                lineRenderer.SetPosition(i * 2 + 1, worldPosB);
            }
        }
    }
}
