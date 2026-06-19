using System.Collections.Generic;
using UnityEngine;
using VRBrush.Util;

namespace VRBrush.Core.Model
{
    /// <summary>
    /// 基于BrushShape的笔画类
    /// 支持自定义形状的笔刷渲染
    /// </summary>
    public class Stroke
    {
        // 从 RibbonStroke 继承过来的成员
        public List<StrokePoint> points { get; private set; }
        public bool isComplete { get; private set; }
        public Material material { get; set; }
        public Color color { get; set; }

        // Mesh相关
        protected Mesh mesh;
        protected List<Vector3> vertices;
        protected List<int> triangles;
        protected List<Vector2> uvs;
        protected List<Color> colors;

        // Stroke 自己的成员
    private BrushShape brushShape;
    // 可选：为每个笔画点提供独立的2D截面顶点（用于按样本/按点变形）
    private List<Vector2[]> perPointShape2D = null;
        private bool useShapeRendering = true;
        // 是否在笔画末端封顶（与历史实现保持一致）
        private bool capEnds = false;
        
        public Stroke(BrushShape shape = null, bool capEnds = false)
        {
            // 初始化从 RibbonStroke 继承的成员
            points = new List<StrokePoint>();
            vertices = new List<Vector3>();
            triangles = new List<int>();
            uvs = new List<Vector2>();
            colors = new List<Color>();
            isComplete = false;
            color = Color.white;

            // 初始化 Stroke 自己的成员
            brushShape = shape;
            // 如果有形状且有足够的节点，启用形状渲染
            useShapeRendering = shape != null && shape.NodeCount >= 3;
            this.capEnds = capEnds;
        }
        
        /// <summary>
        /// 获取或设置当前使用的笔刷形状
        /// </summary>
        public BrushShape BrushShape
        {
            get => brushShape;
            set => brushShape = value;
        }

        /// <summary>
        /// 启用并清空“按点自定义截面”模式。
        /// </summary>
        public void EnablePerPointShapes()
        {
            if (perPointShape2D == null) perPointShape2D = new List<Vector2[]>();
            else perPointShape2D.Clear();
        }

        /// <summary>
        /// 添加一个带有自定义2D截面的点。
        /// 顶点数组按形状直角坐标 (x,y) 给出，长度需与其他点一致。
        /// </summary>
        public void AddPointWithShape(StrokePoint point, Vector2[] shapeVertices2D)
        {
            points.Add(point);
            if (perPointShape2D == null) perPointShape2D = new List<Vector2[]>();
            perPointShape2D.Add(shapeVertices2D);
        }
        
        /// <summary>
        /// 检查是否有有效的笔刷形状
        /// </summary>
        public bool HasValidShape => brushShape != null && brushShape.NodeCount > 0;
        
        /// <summary>
        /// 获取形状的顶点数量
        /// </summary>
        public int ShapeVertexCount => HasValidShape ? brushShape.NodeCount : 0;
        
        /// <summary>
        /// 获取形状的边数量
        /// </summary>
        public int ShapeEdgeCount => HasValidShape ? brushShape.EdgeCount : 0;
        
        /// <summary>
        /// 获取形状信息的描述字符串
        /// </summary>
        public string GetShapeInfo()
        {
            if (!HasValidShape)
                return "No shape";
            
            return $"Shape: {brushShape.Name} ({ShapeVertexCount} vertices, {ShapeEdgeCount} edges)";
        }
        
        /// <summary>
        /// 获取形状在指定位置的轮廓点
        /// </summary>
        /// <param name="centerPosition">中心位置</param>
        /// <param name="orientation">方向</param>
        /// <param name="scale">缩放</param>
        /// <returns>轮廓点列表</returns>
        public List<Vector3> GetShapeContour(Vector3 centerPosition, Quaternion orientation, float scale = 1f)
        {
            var contour = new List<Vector3>();
            
            if (!HasValidShape)
            {
                // 如果没有有效形状，返回简单的圆形轮廓
                int segments = 8;
                for (int i = 0; i < segments; i++)
                {
                    float angle = (float)i / segments * 2f * Mathf.PI;
                    Vector3 point = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * scale * 0.01f;
                    contour.Add(centerPosition + orientation * point);
                }
                return contour;
            }
            
            // 使用BrushShape的顶点列表 (vertices / nodes) 来生成轮廓
            // BrushShape现在存储直角坐标 (x, y)
            foreach (var node in brushShape.vertices)
            {
                // node 已经是直角坐标,直接使用
                Vector3 localPoint = new Vector3(node.x, 0f, node.y) * scale;
                Vector3 worldPoint = centerPosition + orientation * localPoint;
                contour.Add(worldPoint);
            }
            
            return contour;
        }
        
        /// <summary>
        /// 获取形状的边信息
        /// </summary>
        /// <returns>边的索引对列表</returns>
        public List<Vector2Int> GetShapeEdges()
        {
            if (!HasValidShape)
                return new List<Vector2Int>();
            
            return new List<Vector2Int>(brushShape.Edges);
        }
        
        private Mesh overrideMesh;

        /// <summary>
        /// 添加新的点到笔画中
        /// </summary>
        public virtual void AddPoint(StrokePoint point)
        {
            points.Add(point);
        }

        /// <summary>
        /// 完成笔画
        /// </summary>
        public virtual void Complete()
        {
            isComplete = true;
            GenerateMesh();
        }

        /// <summary>
        /// Mesh生成方法 - 支持自定义形状
        /// </summary>
        protected virtual void GenerateMesh()
        {
            bool usePerPointShapes = perPointShape2D != null && perPointShape2D.Count == points.Count && perPointShape2D.Count > 0;
            
            // 如果有 perPointShapes 数据，优先使用 GenerateShapeMesh（即使形状只有2个节点）
            // 这确保了 line_segment 等2节点形状也能正确使用形状坐标渲染
            if (usePerPointShapes)
            {
                GenerateShapeMesh();
                return;
            }
            
            // 没有 perPointShapes 时，检查是否有有效的固定形状
            if (!useShapeRendering || !HasValidShape)
            {
                // 回退到基础的ribbon渲染
                GenerateBasicRibbonMesh();
                return;
            }
            
            // 使用形状生成更复杂的mesh
            GenerateShapeMesh();
        }

        /// <summary>
        /// 基础ribbon mesh生成（从 RibbonStroke 移植过来）
        /// </summary>
        private void GenerateBasicRibbonMesh()
        {
            if (points.Count < 2) return;

            vertices.Clear();
            triangles.Clear();
            uvs.Clear();
            colors.Clear();

            // 为每个点生成两个顶点（ruling的两端）
            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];

                // 添加ruling的两端顶点
                vertices.Add(point.RulingStart);
                vertices.Add(point.RulingEnd);

                // UV坐标
                float u = (float)i / (points.Count - 1);
                uvs.Add(new Vector2(u, 0f));
                uvs.Add(new Vector2(u, 1f));

                // 颜色
                colors.Add(color);
                colors.Add(color);

                // 生成三角形（除了最后一个点）
                if (i < points.Count - 1)
                {
                    int baseIndex = i * 2;

                    // 第一个三角形：左下 -> 右下 -> 左上
                    triangles.Add(baseIndex);     // 左下
                    triangles.Add(baseIndex + 2); // 右下
                    triangles.Add(baseIndex + 1); // 左上

                    // 第二个三角形：左上 -> 右下 -> 右上
                    triangles.Add(baseIndex + 1); // 左上
                    triangles.Add(baseIndex + 2); // 右下
                    triangles.Add(baseIndex + 3); // 右上
                }
            }

            // 创建mesh
            if (mesh == null)
                mesh = new Mesh();

            mesh.Clear();
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.uv = uvs.ToArray();
            mesh.colors = colors.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }

        /// <summary>
        /// 获取生成的mesh
        /// </summary>
        public virtual Mesh GetMesh()
        {
            return overrideMesh ?? mesh;
        }

        /// <summary>
        /// 实时更新mesh（用于绘制过程中）
        /// </summary>
        public virtual void UpdateMesh()
        {
            if (!isComplete)
            {
                GenerateMesh();
            }
        }
        
        /// <summary>
        /// 基于BrushShape生成复杂的形状mesh - 完全匹配原始PolygonStroke实现
        /// </summary>
        private void GenerateShapeMesh()
        {
            if (points.Count < 2) return;
            bool usePerPointShapes = perPointShape2D != null && perPointShape2D.Count == points.Count && perPointShape2D.Count > 0;
            if (!usePerPointShapes)
            {
                if (brushShape == null || brushShape.vertices.Count < 3) return;
            }

            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var uvs = new List<Vector2>();
            var colors = new List<Color>();

            int sectionVertexCount = usePerPointShapes ? perPointShape2D[0].Length : brushShape.vertices.Count;

            // 为每个笔画点生成形状轮廓
            for (int i = 0; i < points.Count; i++)
            {
                var p = points[i];
                // 直接使用 StrokePoint 的 rotation 提取坐标系，与预览保持完全一致
                // ruling = rotation * Vector3.up
                // normal = rotation * Vector3.right
                Vector3 ruling = p.rotation * Vector3.up;
                Vector3 normal = p.rotation * Vector3.right;
                float scale = p.width; // 统一缩放

                // 为每个shape顶点生成世界坐标
                var verts2D = usePerPointShapes ? perPointShape2D[i] : brushShape.vertices.ToArray();
                foreach (var v2 in verts2D)
                {
                    // BrushShape.vertices 存储直角坐标 (x, y)
                    // x 对应 ruling (up) 方向, y 对应 normal (right) 方向
                    Vector3 offset = (ruling * v2.x + normal * v2.y) * scale;
                    vertices.Add(p.position + offset);
                    uvs.Add(new Vector2((float)i / (points.Count - 1), 0));
                    colors.Add(color);
                }
            }

            // 生成三角形 - 基于边的结构而不是假设闭合轮廓
            for (int i = 0; i < points.Count - 1; i++)
            {
                int baseA = i * sectionVertexCount;
                int baseB = (i + 1) * sectionVertexCount;
                
                // 如果 BrushShape 有边信息，使用边来生成三角形
                if (brushShape != null && brushShape.EdgeCount > 0)
                {
                    // 基于图论的边结构拉伸
                    foreach (var edge in brushShape.Edges)
                    {
                        int j0 = edge.x; // 边的起点索引
                        int j1 = edge.y; // 边的终点索引
                        
                        if (j0 < 0 || j0 >= sectionVertexCount || j1 < 0 || j1 >= sectionVertexCount)
                            continue;
                        
                        int a0 = baseA + j0;
                        int a1 = baseA + j1;
                        int b0 = baseB + j0;
                        int b1 = baseB + j1;
                        
                        // 沿边拉伸形成四边形（2个三角形）
                        triangles.Add(a0); triangles.Add(b0); triangles.Add(a1);
                        triangles.Add(a1); triangles.Add(b0); triangles.Add(b1);
                    }
                }
                else
                {
                    // 没有边信息时，假设是闭合轮廓（旧行为）
                    for (int j = 0; j < sectionVertexCount; j++)
                    {
                        int a0 = baseA + j;
                        int a1 = baseA + ((j + 1) % sectionVertexCount);
                        int b0 = baseB + j;
                        int b1 = baseB + ((j + 1) % sectionVertexCount);
                        
                        triangles.Add(a0); triangles.Add(b0); triangles.Add(a1);
                        triangles.Add(a1); triangles.Add(b0); triangles.Add(b1);
                    }
                }
            }

            // 端帽处理 - 如果启用capEnds且存在封闭区域
            if (capEnds && brushShape != null && brushShape.HasCycle())
            {
                // 获取封闭区域的有序顶点索引
                var capOrder = brushShape.GetCapVerticesOrder();
                
                if (capOrder.Count >= 3)
                {
                    // 使用耳切法对封闭区域进行三角剖分
                    var capTriangles = TriangulateCapPolygon(capOrder, brushShape);
                    
                    // 起始端帽（需要反向三角形以面向外侧）
                    int startBase = 0;
                    foreach (var tri in capTriangles)
                    {
                        triangles.Add(startBase + tri.z);
                        triangles.Add(startBase + tri.y);
                        triangles.Add(startBase + tri.x);
                    }
                    
                    // 结束端帽
                    int endBase = (points.Count - 1) * sectionVertexCount;
                    foreach (var tri in capTriangles)
                    {
                        triangles.Add(endBase + tri.x);
                        triangles.Add(endBase + tri.y);
                        triangles.Add(endBase + tri.z);
                    }
                }
                else
                {
                    // 回退到简单的扇形三角剖分
                    // 起始端帽
                    int startBase = 0;
                    for (int j = 1; j < sectionVertexCount - 1; j++)
                    {
                        triangles.Add(startBase);
                        triangles.Add(startBase + j);
                        triangles.Add(startBase + j + 1);
                    }
                    
                    // 结束端帽
                    int endBase = (points.Count - 1) * sectionVertexCount;
                    for (int j = 1; j < sectionVertexCount - 1; j++)
                    {
                        triangles.Add(endBase);
                        triangles.Add(endBase + j + 1);
                        triangles.Add(endBase + j);
                    }
                }
            }
            else if (capEnds)
            {
                // 简单的扇形三角剖分（用于没有环路的形状）
                // 起始端帽
                int startBase = 0;
                for (int j = 1; j < sectionVertexCount - 1; j++)
                {
                    triangles.Add(startBase); 
                    triangles.Add(startBase + j); 
                    triangles.Add(startBase + j + 1);
                }
                
                // 结束端帽
                int endBase = (points.Count - 1) * sectionVertexCount;
                for (int j = 1; j < sectionVertexCount - 1; j++)
                {
                    triangles.Add(endBase); 
                    triangles.Add(endBase + j + 1); 
                    triangles.Add(endBase + j);
                }
            }

            // 创建或更新mesh
            if (overrideMesh == null)
            {
                overrideMesh = new Mesh();
            }

            overrideMesh.Clear();
            overrideMesh.SetVertices(vertices);
            overrideMesh.SetTriangles(triangles, 0);
            overrideMesh.SetUVs(0, uvs);
            overrideMesh.SetColors(colors);
            overrideMesh.RecalculateNormals();
            overrideMesh.RecalculateBounds();
        }

        /// <summary>
        /// 使用耳切法对封闭多边形进行三角剖分
        /// </summary>
        /// <param name="vertexOrder">有序的顶点索引列表（组成封闭多边形）</param>
        /// <param name="shape">BrushShape用于获取顶点位置</param>
        /// <returns>三角形列表（每个Vector3Int包含三个顶点索引）</returns>
        private List<Vector3Int> TriangulateCapPolygon(List<int> vertexOrder, BrushShape shape)
        {
            var result = new List<Vector3Int>();
            
            if (vertexOrder.Count < 3)
                return result;
            
            // 获取多边形顶点的2D坐标
            var polygon = new List<Vector2>();
            var indices = new List<int>(vertexOrder);
            
            foreach (int idx in vertexOrder)
            {
                if (idx >= 0 && idx < shape.NodeCount)
                    polygon.Add(shape.Nodes[idx]);
                else
                    return result; // 无效索引
            }
            
            // 确保多边形是逆时针方向（正面朝向）
            if (GetPolygonArea(polygon) < 0)
            {
                polygon.Reverse();
                indices.Reverse();
            }
            
            // 耳切法
            while (polygon.Count > 3)
            {
                bool earFound = false;
                
                for (int i = 0; i < polygon.Count; i++)
                {
                    int prev = (i + polygon.Count - 1) % polygon.Count;
                    int next = (i + 1) % polygon.Count;
                    
                    // 检查是否是凸顶点（耳朵候选）
                    if (IsConvexVertex(polygon, prev, i, next))
                    {
                        // 检查是否有其他顶点在这个三角形内
                        bool isEar = true;
                        for (int j = 0; j < polygon.Count; j++)
                        {
                            if (j == prev || j == i || j == next)
                                continue;
                            
                            if (PointInTriangle(polygon[j], polygon[prev], polygon[i], polygon[next]))
                            {
                                isEar = false;
                                break;
                            }
                        }
                        
                        if (isEar)
                        {
                            // 找到了耳朵，添加三角形
                            result.Add(new Vector3Int(indices[prev], indices[i], indices[next]));
                            
                            // 移除耳朵顶点
                            polygon.RemoveAt(i);
                            indices.RemoveAt(i);
                            earFound = true;
                            break;
                        }
                    }
                }
                
                if (!earFound)
                {
                    // 无法找到耳朵，可能是退化多边形，使用扇形剖分作为回退
                    Debug.LogWarning("Stroke: Ear clipping failed, using fan triangulation fallback");
                    result.Clear();
                    for (int i = 1; i < vertexOrder.Count - 1; i++)
                    {
                        result.Add(new Vector3Int(vertexOrder[0], vertexOrder[i], vertexOrder[i + 1]));
                    }
                    return result;
                }
            }
            
            // 添加最后一个三角形
            if (polygon.Count == 3)
            {
                result.Add(new Vector3Int(indices[0], indices[1], indices[2]));
            }
            
            return result;
        }
        
        /// <summary>
        /// 计算多边形的有符号面积（正值表示逆时针）
        /// </summary>
        private float GetPolygonArea(List<Vector2> polygon)
        {
            float area = 0;
            for (int i = 0; i < polygon.Count; i++)
            {
                int j = (i + 1) % polygon.Count;
                area += polygon[i].x * polygon[j].y;
                area -= polygon[j].x * polygon[i].y;
            }
            return area / 2f;
        }
        
        /// <summary>
        /// 检查顶点是否是凸顶点
        /// </summary>
        private bool IsConvexVertex(List<Vector2> polygon, int prev, int curr, int next)
        {
            Vector2 a = polygon[prev];
            Vector2 b = polygon[curr];
            Vector2 c = polygon[next];
            
            // 使用叉积判断凹凸性
            return Cross2D(b - a, c - b) >= 0;
        }
        
        /// <summary>
        /// 2D叉积
        /// </summary>
        private float Cross2D(Vector2 a, Vector2 b)
        {
            return a.x * b.y - a.y * b.x;
        }
        
        /// <summary>
        /// 检查点是否在三角形内
        /// </summary>
        private bool PointInTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross2D(p - a, b - a);
            float d2 = Cross2D(p - b, c - b);
            float d3 = Cross2D(p - c, a - c);
            
            bool hasNeg = (d1 < 0) || (d2 < 0) || (d3 < 0);
            bool hasPos = (d1 > 0) || (d2 > 0) || (d3 > 0);
            
            return !(hasNeg && hasPos);
        }
    }
}