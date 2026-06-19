using System.Collections.Generic;
using UnityEngine;

namespace VRBrush.Core.Visual.Element
{
    /// <summary>
    /// 截面边缘预览：显示从上一个锚点截面到当前位置截面的连接线（"棱"）。
    /// 用于帮助用户预览将要生成的广义圆柱体侧面。
    /// </summary>
    public class SectionEdgePreview : MonoBehaviour
    {
        [SerializeField] private Color edgeColor = new Color(0.3f, 0.8f, 0.3f, 0.7f);
        [SerializeField] private float edgeRadius = 0.002f;
        [SerializeField] private int curveSubdivisions = 10;
        
        private List<LineRenderer> edgeLines = new List<LineRenderer>();
        private Material lineMaterial;
        private bool initialized = false;
        
        // 上一个锚点的数据
        private Vector3 lastAnchorPosition;
        private Quaternion lastAnchorRotation;
        private Vector2[] lastSectionShape;
        private float lastSectionWidth;
        private bool hasLastAnchor = false;
        
        public void Initialize(Material baseMat = null, Color? color = null, float? radius = null)
        {
            if (initialized) return;
            initialized = true;
            
            if (color.HasValue) edgeColor = color.Value;
            if (radius.HasValue) edgeRadius = Mathf.Max(0.0005f, radius.Value);
            
            if (baseMat == null)
            {
                lineMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                lineMaterial.color = edgeColor;
            }
            else
            {
                lineMaterial = new Material(baseMat);
                lineMaterial.color = edgeColor;
            }
        }
        
        /// <summary>
        /// 设置上一个锚点的截面数据（在确认点时调用）
        /// </summary>
        public void SetLastAnchor(Vector3 position, Quaternion rotation, Vector2[] sectionShape, float width)
        {
            lastAnchorPosition = position;
            lastAnchorRotation = rotation;
            lastSectionShape = sectionShape != null ? (Vector2[])sectionShape.Clone() : null;
            lastSectionWidth = width;
            hasLastAnchor = true;
        }
        
        /// <summary>
        /// 清除上一个锚点数据（开始新笔画或结束笔画时调用）
        /// </summary>
        public void ClearLastAnchor()
        {
            hasLastAnchor = false;
            lastSectionShape = null;
            SetVisible(false);
        }
        
        /// <summary>
        /// 更新当前位置的截面预览（连接到上一个锚点）
        /// </summary>
        /// <param name="currentPosition">当前控制器位置</param>
        /// <param name="currentRotation">当前截面旋转（LookRotation(forward, up)）</param>
        /// <param name="currentShape">当前截面形状的2D顶点</param>
        /// <param name="currentWidth">当前截面宽度</param>
        /// <param name="prevAnchorPosition">前一个锚点的位置（用于Catmull-Rom插值的p0）</param>
        public void UpdatePreview(Vector3 currentPosition, Quaternion currentRotation, 
                                   Vector2[] currentShape, float currentWidth,
                                   Vector3? prevAnchorPosition = null)
        {
            if (!initialized) Initialize();
            if (!hasLastAnchor || lastSectionShape == null || currentShape == null) 
            {
                SetVisible(false);
                return;
            }
            
            int vertexCount = Mathf.Min(lastSectionShape.Length, currentShape.Length);
            if (vertexCount == 0)
            {
                SetVisible(false);
                return;
            }
            
            // 确保有足够的LineRenderer
            EnsureEdgeLines(vertexCount);
            
            // 计算Catmull-Rom控制点
            // p0 = 前前个锚点位置（如果没有则延长）
            // p1 = 上一个锚点位置
            // p2 = 当前位置
            // p3 = 延长到当前方向
            Vector3 p1 = lastAnchorPosition;
            Vector3 p2 = currentPosition;
            Vector3 p0 = prevAnchorPosition ?? (p1 - (p2 - p1)); // 向后延长
            Vector3 p3 = p2 + (p2 - p1); // 向前延长
            
            // 同样处理旋转（使用Slerp）
            Quaternion r1 = lastAnchorRotation;
            Quaternion r2 = currentRotation;
            
            // 宽度插值
            float w1 = lastSectionWidth;
            float w2 = currentWidth;
            
            // 为每个顶点更新曲线
            for (int v = 0; v < vertexCount; v++)
            {
                var lr = edgeLines[v];
                lr.positionCount = curveSubdivisions + 1;
                lr.enabled = true;
                
                Vector2 shape1 = lastSectionShape[v];
                Vector2 shape2 = currentShape[v];
                
                for (int i = 0; i <= curveSubdivisions; i++)
                {
                    float t = (float)i / curveSubdivisions;
                    
                    // Catmull-Rom位置插值
                    Vector3 pos = CatmullRomInterpolate(p0, p1, p2, p3, t);
                    
                    // 旋转插值
                    Quaternion rot = Quaternion.Slerp(r1, r2, t);
                    
                    // 宽度插值
                    float w = Mathf.Lerp(w1, w2, t);
                    
                    // 形状顶点插值
                    Vector2 shapeVert = Vector2.Lerp(shape1, shape2, t);
                    
                    // 计算世界坐标
                    Vector3 ruling = rot * Vector3.up;
                    Vector3 normal = rot * Vector3.right;
                    Vector3 worldPos = pos + (ruling * shapeVert.x + normal * shapeVert.y) * w;
                    
                    lr.SetPosition(i, worldPos);
                }
            }
        }
        
        public void SetVisible(bool visible)
        {
            foreach (var lr in edgeLines)
            {
                if (lr != null) lr.enabled = visible;
            }
        }
        
        public void SetColor(Color color)
        {
            edgeColor = color;
            if (lineMaterial != null) lineMaterial.color = color;
            foreach (var lr in edgeLines)
            {
                if (lr != null)
                {
                    lr.startColor = color;
                    lr.endColor = color;
                }
            }
        }
        
        public void SetRadius(float radius)
        {
            edgeRadius = Mathf.Max(0.0005f, radius);
            foreach (var lr in edgeLines)
            {
                if (lr != null)
                {
                    lr.startWidth = edgeRadius * 2f;
                    lr.endWidth = edgeRadius * 2f;
                }
            }
        }
        
        private void EnsureEdgeLines(int count)
        {
            // 移除多余的
            while (edgeLines.Count > count)
            {
                var lr = edgeLines[edgeLines.Count - 1];
                if (lr != null) Destroy(lr.gameObject);
                edgeLines.RemoveAt(edgeLines.Count - 1);
            }
            
            // 添加不足的
            while (edgeLines.Count < count)
            {
                var obj = new GameObject($"EdgeLine_{edgeLines.Count}");
                obj.transform.SetParent(transform);
                var lr = obj.AddComponent<LineRenderer>();
                lr.material = lineMaterial;
                lr.startColor = edgeColor;
                lr.endColor = edgeColor;
                lr.startWidth = edgeRadius * 2f;
                lr.endWidth = edgeRadius * 2f;
                lr.useWorldSpace = true;
                lr.enabled = false;
                edgeLines.Add(lr);
            }
        }
        
        private Vector3 CatmullRomInterpolate(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }
        
        private void OnDestroy()
        {
            foreach (var lr in edgeLines)
            {
                if (lr != null) Destroy(lr.gameObject);
            }
            edgeLines.Clear();
            if (lineMaterial != null) Destroy(lineMaterial);
        }
    }
}
