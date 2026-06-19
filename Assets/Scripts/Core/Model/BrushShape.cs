using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using VRBrush.Util;
// using VRBrush.Core; // Removed to avoid circular dependency

namespace VRBrush.Core.Model
{
    /// <summary>
    /// 代表一个笔刷形状，支持基本的点和边操作，以及序列化/反序列化功能
    /// 整合了原有的Serializer功能
    /// </summary>
    [System.Serializable]
    public class BrushShape
    {
        [SerializeField] private List<Vector2> nodes = new List<Vector2>();
        [SerializeField] private List<Vector2Int> edges = new List<Vector2Int>();
        [SerializeField] private string graphName;

        // 向后兼容的公共属性
        public string name => graphName;
        public List<Vector2> vertices => nodes;
        public List<Vector2> graphNodes => nodes;
        public List<Vector2Int> graphEdges => edges;

        // 事件通知图结构变化
        public System.Action<Vector2> OnNodeAdded;
        public System.Action<Vector2Int> OnEdgeAdded;
        public System.Action OnGraphChanged;

        public BrushShape(string name = "")
        {
            graphName = string.IsNullOrEmpty(name) ? $"Shape_{System.DateTime.Now:yyyyMMdd_HHmmss}" : name;
        }

        #region Basic Graph Operations

        /// <summary>
        /// 获取所有节点的只读列表
        /// </summary>
        public IReadOnlyList<Vector2> Nodes => nodes;

        /// <summary>
        /// 获取所有边的只读列表
        /// </summary>
        public IReadOnlyList<Vector2Int> Edges => edges.AsReadOnly();

        /// <summary>
        /// 获取或设置图的名称
        /// </summary>
        public string Name
        {
            get => graphName;
            set => graphName = value;
        }

        /// <summary>
        /// 添加节点到图中
        /// </summary>
        /// <param name="position">节点的直角坐标位置 (x, y)</param>
        /// <returns>节点索引</returns>
        public int AddNode(Vector2 position)
        {
            nodes.Add(position);
            int index = nodes.Count - 1;
            OnNodeAdded?.Invoke(position);
            OnGraphChanged?.Invoke();
            return index;
        }

        /// <summary>
        /// 设置指定节点的位置
        /// </summary>
        /// <param name="index">节点索引</param>
        /// <param name="position">新的直角坐标位置 (x, y)</param>
        /// <returns>是否成功设置</returns>
        public bool SetNode(int index, Vector2 position)
        {
            if (index < 0 || index >= nodes.Count)
                return false;
            
            nodes[index] = position;
            OnGraphChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 添加边到图中
        /// </summary>
        /// <param name="nodeA">节点A的索引</param>
        /// <param name="nodeB">节点B的索引</param>
        /// <returns>是否成功添加</returns>
        public bool AddEdge(int nodeA, int nodeB)
        {
            // 验证节点索引有效性
            if (nodeA < 0 || nodeA >= nodes.Count || nodeB < 0 || nodeB >= nodes.Count)
            {
                Debug.LogWarning($"BrushShape: AddEdge failed - invalid node indices: {nodeA}, {nodeB} (total nodes: {nodes.Count})");
                return false;
            }

            // 避免自环
            if (nodeA == nodeB)
            {
                Debug.LogWarning($"BrushShape: AddEdge failed - self loop attempted: {nodeA}");
                return false;
            }

            // 检查边是否已存在
            Vector2Int edge = new Vector2Int(nodeA, nodeB);
            Vector2Int reverseEdge = new Vector2Int(nodeB, nodeA);

            if (edges.Contains(edge) || edges.Contains(reverseEdge))
            {
                Debug.LogWarning($"BrushShape: AddEdge failed - edge already exists: {nodeA}-{nodeB}");
                return false;
            }

            edges.Add(edge);
            Debug.Log($"BrushShape: Successfully added edge {nodeA}-{nodeB}, total edges: {edges.Count}");
            OnEdgeAdded?.Invoke(edge);
            OnGraphChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 添加边（使用Vector2Int）
        /// </summary>
        /// <param name="edge">边</param>
        /// <returns>是否成功添加</returns>
        public bool AddEdge(Vector2Int edge)
        {
            return AddEdge(edge.x, edge.y);
        }

        /// <summary>
        /// 移除节点及其相关的所有边
        /// </summary>
        /// <param name="nodeIndex">要移除的节点索引</param>
        /// <returns>是否成功移除</returns>
        public bool RemoveNode(int nodeIndex)
        {
            if (nodeIndex < 0 || nodeIndex >= nodes.Count)
                return false;

            // 移除所有相关的边
            for (int i = edges.Count - 1; i >= 0; i--)
            {
                if (edges[i].x == nodeIndex || edges[i].y == nodeIndex)
                {
                    edges.RemoveAt(i);
                }
            }

            // 更新边的索引（所有大于nodeIndex的索引减1）
            for (int i = 0; i < edges.Count; i++)
            {
                Vector2Int edge = edges[i];
                if (edge.x > nodeIndex) edge.x--;
                if (edge.y > nodeIndex) edge.y--;
                edges[i] = edge;
            }

            // 移除节点
            nodes.RemoveAt(nodeIndex);
            OnGraphChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 移除边
        /// </summary>
        /// <param name="nodeA">节点A索引</param>
        /// <param name="nodeB">节点B索引</param>
        /// <returns>是否成功移除</returns>
        public bool RemoveEdge(int nodeA, int nodeB)
        {
            Vector2Int edge = new Vector2Int(nodeA, nodeB);
            Vector2Int reverseEdge = new Vector2Int(nodeB, nodeA);

            bool removed = edges.Remove(edge) || edges.Remove(reverseEdge);
            if (removed)
            {
                OnGraphChanged?.Invoke();
            }
            return removed;
        }

        /// <summary>
        /// 清空图
        /// </summary>
        public void Clear()
        {
            nodes.Clear();
            edges.Clear();
            OnGraphChanged?.Invoke();
        }

        /// <summary>
        /// 获取节点数量
        /// </summary>
        public int NodeCount => nodes.Count;

        /// <summary>
        /// 获取边数量
        /// </summary>
        public int EdgeCount => edges.Count;

        /// <summary>
        /// 检查是否存在边
        /// </summary>
        public bool HasEdge(int nodeA, int nodeB)
        {
            Vector2Int edge = new Vector2Int(nodeA, nodeB);
            Vector2Int reverseEdge = new Vector2Int(nodeB, nodeA);
            return edges.Contains(edge) || edges.Contains(reverseEdge);
        }

        /// <summary>
        /// 获取与指定节点相连的所有邻居节点
        /// </summary>
        public List<int> GetNeighbors(int nodeIndex)
        {
            var neighbors = new List<int>();
            foreach (var edge in edges)
            {
                if (edge.x == nodeIndex)
                    neighbors.Add(edge.y);
                else if (edge.y == nodeIndex)
                    neighbors.Add(edge.x);
            }
            return neighbors;
        }

        /// <summary>
        /// 检测图中是否存在环路（封闭区域）
        /// 使用DFS检测环路
        /// </summary>
        /// <returns>如果存在环路返回true</returns>
        public bool HasCycle()
        {
            if (nodes.Count < 3 || edges.Count < 3)
                return false;
            
            var visited = new HashSet<int>();
            
            // 对每个连通分量进行DFS
            for (int start = 0; start < nodes.Count; start++)
            {
                if (visited.Contains(start))
                    continue;
                
                if (DFSDetectCycle(start, -1, visited))
                    return true;
            }
            
            return false;
        }
        
        private bool DFSDetectCycle(int current, int parent, HashSet<int> visited)
        {
            visited.Add(current);
            
            foreach (int neighbor in GetNeighbors(current))
            {
                if (!visited.Contains(neighbor))
                {
                    if (DFSDetectCycle(neighbor, current, visited))
                        return true;
                }
                else if (neighbor != parent)
                {
                    // 发现了一个不是父节点的已访问节点，说明存在环
                    return true;
                }
            }
            
            return false;
        }

        /// <summary>
        /// 查找图中的所有封闭环路并返回组成环路的节点索引列表
        /// 用于生成封口mesh的三角剖分
        /// </summary>
        /// <returns>所有环路，每个环路是一个有序的节点索引列表</returns>
        public List<List<int>> FindAllCycles()
        {
            var cycles = new List<List<int>>();
            
            if (nodes.Count < 3 || edges.Count < 3)
                return cycles;
            
            // 对于简单的平面图形，找到最外层的封闭轮廓
            // 使用最小环查找算法
            var usedEdges = new HashSet<string>();
            
            foreach (var edge in edges)
            {
                string edgeKey = GetEdgeKey(edge.x, edge.y);
                if (usedEdges.Contains(edgeKey))
                    continue;
                
                var cycle = FindMinimalCycleFromEdge(edge.x, edge.y);
                if (cycle != null && cycle.Count >= 3)
                {
                    // 标记环中的边为已使用
                    for (int i = 0; i < cycle.Count; i++)
                    {
                        int a = cycle[i];
                        int b = cycle[(i + 1) % cycle.Count];
                        usedEdges.Add(GetEdgeKey(a, b));
                    }
                    cycles.Add(cycle);
                }
            }
            
            return cycles;
        }
        
        private string GetEdgeKey(int a, int b)
        {
            return a < b ? $"{a}-{b}" : $"{b}-{a}";
        }
        
        /// <summary>
        /// 从指定边开始寻找最小环路
        /// </summary>
        private List<int> FindMinimalCycleFromEdge(int startNode, int nextNode)
        {
            // BFS找最短环
            var queue = new Queue<List<int>>();
            queue.Enqueue(new List<int> { startNode, nextNode });
            var visited = new HashSet<string>();
            visited.Add($"{startNode}-{nextNode}");
            
            while (queue.Count > 0)
            {
                var path = queue.Dequeue();
                int current = path[path.Count - 1];
                
                foreach (int neighbor in GetNeighbors(current))
                {
                    if (neighbor == startNode && path.Count >= 3)
                    {
                        // 找到环路
                        return path;
                    }
                    
                    // 避免回溯到上一个节点
                    if (path.Count >= 2 && neighbor == path[path.Count - 2])
                        continue;
                    
                    // 避免重访环路中的其他节点（除了起点）
                    if (path.Contains(neighbor))
                        continue;
                    
                    string visitKey = $"{current}-{neighbor}";
                    if (!visited.Contains(visitKey))
                    {
                        visited.Add(visitKey);
                        var newPath = new List<int>(path) { neighbor };
                        queue.Enqueue(newPath);
                    }
                }
            }
            
            return null;
        }

        /// <summary>
        /// 获取用于封口三角剖分的有序顶点列表
        /// 如果图有环路，返回组成封闭区域的顶点（按逆时针或顺时针顺序）
        /// </summary>
        /// <returns>有序的顶点索引列表，如果没有封闭区域则返回空列表</returns>
        public List<int> GetCapVerticesOrder()
        {
            var cycles = FindAllCycles();
            if (cycles.Count == 0)
                return new List<int>();
            
            // 返回最大的环（通常是外轮廓）
            List<int> maxCycle = cycles[0];
            foreach (var cycle in cycles)
            {
                if (cycle.Count > maxCycle.Count)
                    maxCycle = cycle;
            }
            
            return maxCycle;
        }

        /// <summary>
        /// 查找离指定位置最近的节点
        /// </summary>
        /// <param name="targetPosition">目标位置(直角坐标)</param>
        /// <param name="maxDistance">可选的最大距离阈值</param>
        /// <returns>最近节点的索引，如果没有找到则返回-1</returns>
        public int FindNearestNode(Vector2 targetPosition, float maxDistance = float.MaxValue)
        {
            int nearestIndex = -1;
            float nearestDistanceSquared = maxDistance * maxDistance;

            for (int i = 0; i < nodes.Count; i++)
            {
                float distanceSquared = (nodes[i] - targetPosition).sqrMagnitude;
                if (distanceSquared < nearestDistanceSquared)
                {
                    nearestDistanceSquared = distanceSquared;
                    nearestIndex = i;
                }
            }

            return nearestIndex;
        }

        #endregion

        // Additional shape/runtime types were moved to namespace-level types below

        #region Serialization Operations

        /// <summary>
        /// 保存图到JSON文件
        /// </summary>
        /// <param name="filePath">文件路径（如为null则使用默认路径）</param>
        /// <returns>是否保存成功</returns>
        public bool SaveToFile(string filePath = null)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    // 如果 graphName 已经包含类似日期时间戳（_yyyyMMdd_HHmmss）则不要再次追加，直接使用 graphName.json
                    // 否则在 graphName 后追加时间戳以保证唯一性
                    string fileName;
                    if (Regex.IsMatch(graphName ?? string.Empty, @"_\d{8}_\d{6}$"))
                    {
                        fileName = $"{graphName}.json";
                    }
                    else
                    {
                        fileName = $"{graphName}_{System.DateTime.Now:yyyyMMdd_HHmmss}.json";
                    }
                    // 使用同命名空间下的默认目录，避免对 VRBrush.Core 层的硬依赖
                    filePath = Path.Combine(BrushShapeLoader.DefaultDirectory, fileName);
                }

                // 确保目录存在
                string directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 构建保存数据 - 现在使用直角坐标格式
                var saveData = new BrushShapeJson
                {
                    name = graphName,
                    type = "cartesian", // 标记为直角坐标格式
                    points = new List<BrushShapePointRaw>(),
                    edges = new List<BrushShapeEdgeRaw>()
                };

                // 转换节点数据 - 仅保存直角坐标 (x, y)
                foreach (var node in nodes)
                {
                    saveData.points.Add(new BrushShapePointRaw { x = node.x, y = node.y });
                }

                // 转换边数据
                foreach (var edge in edges)
                {
                    saveData.edges.Add(new BrushShapeEdgeRaw { a = edge.x, b = edge.y });
                }

                string json = JsonUtility.ToJson(saveData, true);
                File.WriteAllText(filePath, json);

                Debug.Log($"BrushShape: Saved shape '{graphName}' to {filePath}");
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"BrushShape: Save failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 从JSON文件加载形状
        /// </summary>
        /// <param name="filePath">文件路径</param>
        /// <returns>加载的形状，如失败则返回null</returns>
        public static BrushShape LoadFromFile(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Debug.LogWarning($"BrushShape: File does not exist: {filePath}");
                    return null;
                }

                string json = File.ReadAllText(filePath);
                // 统一使用本命名空间下的 DTO 类型
                var jsonData = JsonUtility.FromJson<BrushShapeJson>(json);

                if (jsonData == null)
                {
                    Debug.LogWarning($"BrushShape: Invalid JSON format in {filePath}");
                    return null;
                }

                string shapeName = string.IsNullOrEmpty(jsonData.name) ?
                    Path.GetFileNameWithoutExtension(filePath) : jsonData.name;

                var shape = new BrushShape(shapeName);

                // 加载节点 - 统一使用直角坐标格式
                if (jsonData.points != null)
                {
                    foreach (var point in jsonData.points)
                    {
                        // 直接使用直角坐标
                        Vector2 nodePos = new Vector2(point.x, point.y);
                        shape.AddNode(nodePos);
                    }
                }

                // 加载边
                if (jsonData.edges != null)
                {
                    foreach (var edge in jsonData.edges)
                    {
                        shape.AddEdge(edge.a, edge.b);
                    }
                }

                Debug.Log($"BrushShape: Loaded shape '{shapeName}' from {filePath}");
                return shape;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"BrushShape: Load failed: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 从BrushShape创建BrushShape
        /// </summary>
        /// <param name="brushShape">笔刷形状</param>
        /// <returns>创建的形状</returns>
        public static BrushShape FromBrushShape(BrushShape brushShape)
        {
            var shape = new BrushShape(brushShape.name);

            // 复制节点
            foreach (var node in brushShape.graphNodes)
            {
                shape.AddNode(node);
            }

            // 复制边
            foreach (var edge in brushShape.graphEdges)
            {
                shape.AddEdge(edge);
            }

            return shape;
        }

        /// <summary>
        /// 转换为BrushShape格式
        /// </summary>
        /// <returns>BrushShape对象</returns>
        public BrushShape ToBrushShape()
        {
            var brushShape = new BrushShape(graphName);

            // 复制节点
            brushShape.graphNodes.Clear();
            brushShape.graphNodes.AddRange(nodes);

            // 复制边
            brushShape.graphEdges.Clear();
            brushShape.graphEdges.AddRange(edges);

            return brushShape;
        }

        #endregion
    }
}

// Namespace-level support types moved to BrushShapeDtos.cs to avoid duplication
namespace VRBrush.Core.Model
{
    /// <summary>
    /// 负责从指定目录加载笔刷截面JSON定义
    /// </summary>
    public static class BrushShapeLoader
    {
        public const string DefaultDirectory = "Assets/Brushes"; // 可在 Inspector 中覆盖

        /// <summary>
        /// 加载目录下的所有形状文件(.json)
        /// 失败文件跳过并输出日志
        /// 支持新的多笔画格式，同时向后兼容旧格式
        /// 返回 BrushShape 对象，如果文件包含图数据则需要额外调用LoadBrushShape
        /// </summary>
        public static List<BrushShape> LoadAllShapes(string directory = null)
        {
            var results = new List<BrushShape>();
            directory = string.IsNullOrEmpty(directory) ? DefaultDirectory : directory;

            if (!Directory.Exists(directory))
            {
                Debug.LogWarning($"BrushShapeLoader: Directory does not exist: {directory}");
                return results;
            }

            var files = Directory.GetFiles(directory, "*.json", SearchOption.TopDirectoryOnly);
            foreach (var file in files)
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var wrapper = JsonUtility.FromJson<BrushShapeJson>(json);
                    if (wrapper == null)
                    {
                        Debug.LogWarning($"BrushShapeLoader: {file} invalid JSON format");
                        continue;
                    }

                    var shape = new BrushShape(string.IsNullOrEmpty(wrapper.name) ? Path.GetFileNameWithoutExtension(file) : wrapper.name);

                    // 注意：图结构数据不再保存到BrushShape 中
                    // 如果需要图数据，请使用 LoadBrushShape 方法

                    // 判断使用新格式还是旧格式（若存在 points 则作为单个笔画处理）
                    if (wrapper.points != null && wrapper.points.Count >= 2)
                    {
                        // 统一使用直角坐标格式
                        var strokeVertices = new List<Vector2>();
                        foreach (var p in wrapper.points)
                        {
                            Vector2 v = ParsePoint(p);
                            strokeVertices.Add(v);
                        }
                        // 合并到主 vertices
                        shape.vertices.AddRange(strokeVertices);
                    }
                    else
                    {
                        Debug.LogWarning($"BrushShapeLoader: {file} has no valid point data");
                        continue;
                    }

                    // 归一化尺寸
                    Normalize(shape);
                    results.Add(shape);
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning($"BrushShapeLoader: Failed to parse {file}: {ex.Message}");
                }
            }
            return results;
        }

        /// <summary>
        /// 从JSON文件加载BrushShape对象
        /// </summary>
        /// <param name="filePath">JSON文件路径</param>
        /// <returns>加载的BrushShape对象，失败则返回null</returns>
        public static BrushShape LoadBrushShape(string filePath)
        {
            try
            {
                // 直接复用 BrushShape 自带的加载逻辑，避免重复实现与格式分歧
                var shape = VRBrush.Core.Model.BrushShape.LoadFromFile(filePath);
                return shape;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"BrushShapeLoader: Failed to load BrushShape from {filePath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 解析单个点 - 统一使用直角坐标
        /// </summary>
        private static Vector2 ParsePoint(BrushShapePointRaw p)
        {
            return new Vector2(p.x, p.y);
        }

        private static void Normalize(BrushShape shape)
        {
            float maxMag = 0f;

            // 计算所有顶点中的最大距离
            foreach (var v in shape.vertices)
            {
                float m = v.magnitude;
                if (m > maxMag) maxMag = m;
            }

            if (maxMag < 1e-5f) return;
            float scale = 0.5f / maxMag; // 最大半径变为0.5

            // 缩放主要 vertices 列表
            for (int i = 0; i < shape.vertices.Count; i++)
            {
                shape.vertices[i] = shape.vertices[i] * scale;
            }
        }

        /// <summary>
        /// 保存BrushShape到JSON文件，格式对齐triangle.json
        /// 只输出name、type、points、edges 四个字段
        /// </summary>
        public static bool SaveBrushShape(BrushShape shape, string filePath = null)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    // 使用时间戳生成文件名
                    string fileName = $"brush_{System.DateTime.Now:yyyyMMdd_HHmmss}.json";
                    filePath = Path.Combine(DefaultDirectory, fileName);
                }

                // 确保目录存在
                string directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 保存BrushShape数据
                bool success = shape.SaveToFile(filePath);
                if (success)
                {
                    Debug.Log($"BrushShapeLoader: Saved BrushShape to {filePath}");
                }
                return success;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"BrushShapeLoader: Save failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 保存笔刷形状到JSON文件（只保存顶点数据，不包含图结构）
        /// 如需保存图结构请使用 SaveBrushShape
        /// </summary>
        public static bool SaveShape(BrushShape shape, string filePath = null)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    // 使用时间戳生成文件名
                    string fileName = $"brush_{System.DateTime.Now:yyyyMMdd_HHmmss}.json";
                    filePath = Path.Combine(DefaultDirectory, fileName);
                }

                // 确保目录存在
                string directory = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                // 构建简化的 JSON 结构，保存为直角坐标格式
                var saveData = new BrushShapeJson
                {
                    name = shape.name,
                    type = "cartesian",
                    points = new List<BrushShapePointRaw>()
                };

                // 使用 vertices 数据（笔画顶点）
                if (shape.vertices != null && shape.vertices.Count > 0)
                {
                    foreach (var vertex in shape.vertices)
                    {
                        saveData.points.Add(new BrushShapePointRaw { x = vertex.x, y = vertex.y });
                    }
                }

                string json = JsonUtility.ToJson(saveData, true);
                File.WriteAllText(filePath, json);

                Debug.Log($"BrushShapeLoader: Saved brush shape to {filePath}");
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"BrushShapeLoader: Save failed: {ex.Message}");
                return false;
            }
        }

        // 运行时截面缩放：为兼容调用方，这里提供统一的运行时缩放参数与工具方法
        // 注意：不影响磁盘上的 JSON，仅用于运行时几何缩放。
        public static float RuntimeShapeScale = 0.125f; // 默认 1/8

        /// <summary>
        /// 返回按 RuntimeShapeScale 缩放后的形状副本（不修改原始 shape）。
        /// </summary>
        public static BrushShape ApplyRuntimeScale(BrushShape shape)
        {
            if (shape == null) return null;
            var copy = new BrushShape(shape.Name);
            // 拷贝节点并缩放
            foreach (var v in shape.graphNodes)
            {
                copy.AddNode(v * RuntimeShapeScale);
            }
            // 拷贝边
            foreach (var e in shape.graphEdges)
            {
                copy.AddEdge(e);
            }
            return copy;
        }

        /// <summary>
        /// 原地按给定比例缩放传入形状（修改实参）。
        /// </summary>
        public static void ApplyRuntimeScale(BrushShape shape, float scale)
        {
            if (shape == null) return;
            if (Mathf.Approximately(scale, 1f)) return;
            // 直接更新内部节点
            for (int i = 0; i < shape.vertices.Count; i++)
            {
                shape.vertices[i] = shape.vertices[i] * scale;
            }
        }
    }
}

// Namespace-level support types (moved from BrushShapeDtos.cs)
namespace VRBrush.Core.Model
{
    /// <summary>
    /// 点坐标 DTO（仅直角坐标 x,y） - 用于 JSON 序列化/反序列化
    /// </summary>
    [Serializable]
    public class BrushShapePointRaw
    {
        public float x, y;
    }

    /// <summary>
    /// 边 DTO
    /// </summary>
    [Serializable]
    public class BrushShapeEdgeRaw
    {
        public int a, b;
    }

    /// <summary>
    /// 以直角坐标保存的 BrushShape JSON 包装
    /// </summary>
    [Serializable]
    public class BrushShapeJson
    {
        public string name;
        public string type;
        public List<BrushShapePointRaw> points;
        public List<BrushShapeEdgeRaw> edges;
        public List<MorphData> morphs; // 变形数据数组
    }

    /// <summary>
    /// Morph 数据 DTO - 用于保存变形信息
    /// </summary>
    [Serializable]
    public class MorphData
    {
        public string name; // 变形名称
        public List<BrushShapePointRaw> points; // 变形后的顶点坐标
    }
}