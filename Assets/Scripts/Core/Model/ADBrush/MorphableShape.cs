using System.Collections.Generic;
using UnityEngine;

namespace VRBrush.Core.Model.ADBrush
{
    /// <summary>
    /// 可变形的2D截面形状
    /// 支持基础形状和多个变形目标的混合
    /// 公式: BrushShape = Base + Σ(weight_i * (Morph_i - Base))
    /// 注意：这里不处理 Size（缩放）。全局 Size 由 BrushSetting（如 ribbonWidth）统一控制。
    /// </summary>
    [System.Serializable]
    public class MorphableShape
    {
        [SerializeField] private BrushShape baseShape;
        [SerializeField] private List<BrushShape> morphShapes = new List<BrushShape>();
        [SerializeField] private List<string> morphNames = new List<string>();

        public BrushShape BaseShape => baseShape;
        public IReadOnlyList<BrushShape> MorphShapes => morphShapes.AsReadOnly();
        public IReadOnlyList<string> MorphNames => morphNames.AsReadOnly();
        public int MorphCount => morphShapes.Count;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="baseShape">基础形状</param>
        public MorphableShape(BrushShape baseShape)
        {
            this.baseShape = baseShape;
        }

        /// <summary>
        /// 从JSON文件加载可变形状（支持扩展的morph数据）
        /// </summary>
        public static MorphableShape LoadFromFile(string filePath)
        {
            // 首先加载基础形状
            var baseShape = BrushShape.LoadFromFile(filePath);
            if (baseShape == null)
            {
                Debug.LogError($"MorphableShape: Failed to load base shape from {filePath}");
                return null;
            }

            var morphableShape = new MorphableShape(baseShape);

            // 尝试加载morph数据
            try
            {
                if (System.IO.File.Exists(filePath))
                {
                    string json = System.IO.File.ReadAllText(filePath);
                    var jsonData = JsonUtility.FromJson<BrushShapeJson>(json);

                    if (jsonData != null && jsonData.morphs != null && jsonData.morphs.Count > 0)
                    {
                        Debug.Log($"MorphableShape: Found {jsonData.morphs.Count} morphs in {filePath}");

                        // 加载每个morph
                        foreach (var morphData in jsonData.morphs)
                        {
                            if (morphData == null || morphData.points == null)
                            {
                                Debug.LogWarning("MorphableShape: Skipping invalid morph data");
                                continue;
                            }

                            // 创建morph形状
                            var morphShape = new BrushShape(morphData.name ?? "Unnamed");

                            // 添加顶点（直接使用 JSON 中的原始坐标）
                            foreach (var point in morphData.points)
                            {
                                Vector2 v = new Vector2(point.x, point.y);
                                morphShape.AddNode(v);
                            }

                            // 复制基础形状的边结构
                            foreach (var edge in baseShape.Edges)
                            {
                                morphShape.AddEdge(edge);
                            }

                            // 添加到morph列表
                            morphableShape.AddMorph(morphShape, morphData.name);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"MorphableShape: Failed to load morph data: {ex.Message}");
            }

            Debug.Log($"MorphableShape: Loaded base shape '{baseShape.Name}' with {morphableShape.MorphCount} morphs from {filePath}");
            return morphableShape;
        }

        /// <summary>
        /// 添加变形目标
        /// </summary>
        /// <param name="morphShape">变形后的形状</param>
        /// <param name="morphName">变形名称</param>
        public void AddMorph(BrushShape morphShape, string morphName)
        {
            if (morphShape == null)
            {
                Debug.LogWarning("MorphableShape: Cannot add null morph shape");
                return;
            }

            // 验证变形形状的顶点数量与基础形状一致
            if (morphShape.NodeCount != baseShape.NodeCount)
            {
                Debug.LogWarning($"MorphableShape: Morph '{morphName}' has {morphShape.NodeCount} nodes, " +
                               $"but base shape has {baseShape.NodeCount} nodes. Skipping.");
                return;
            }

            morphShapes.Add(morphShape);
            morphNames.Add(string.IsNullOrEmpty(morphName) ? $"Morph_{morphShapes.Count}" : morphName);
            
            Debug.Log($"MorphableShape: Added morph '{morphName}' (total: {morphShapes.Count})");
        }

        /// <summary>
        /// 根据权重列表返回变形后的形状（不包含 Size 缩放）
        /// </summary>
        /// <param name="morphWeights">变形权重列表（长度应等于 MorphCount；范围 0..1）</param>
        /// <returns>混合后的形状（单位尺度，缩放由 BrushSetting 负责）</returns>
        public BrushShape GetBrushShape(List<float> morphWeights)
        {
            if (baseShape == null)
            {
                Debug.LogWarning("MorphableShape: Base shape is null");
                return null;
            }

            // 创建结果形状（从基础形状复制名称）
            var resultShape = new BrushShape($"{baseShape.Name}_Morphed");

            // 如果没有 morph，直接返回基础形状的拷贝（单位尺度）
            if (morphShapes.Count == 0 || morphWeights == null || morphWeights.Count == 0)
            {
                foreach (var node in baseShape.Nodes)
                {
                    resultShape.AddNode(node);
                }
                foreach (var edge in baseShape.Edges)
                {
                    resultShape.AddEdge(edge);
                }
                return resultShape;
            }

            // 计算混合后的节点位置
            for (int i = 0; i < baseShape.NodeCount; i++)
            {
                Vector2 baseNode = baseShape.Nodes[i];
                Vector2 morphedNode = baseNode;

                // 应用每个morph的贡献
                // 公式: p = base + Σ(weight_i * (morph_i - base))
                for (int m = 0; m < morphShapes.Count && m < morphWeights.Count; m++)
                {
                    float weight = morphWeights[m];
                    if (Mathf.Abs(weight) < 0.001f) continue; // 跳过权重接近0的morph

                    BrushShape morphShape = morphShapes[m];
                    if (i < morphShape.NodeCount)
                    {
                        Vector2 morphNode = morphShape.Nodes[i];
                        Vector2 delta = morphNode - baseNode;
                        morphedNode += delta * weight;
                    }
                }

                // 不在此处应用全局 Size；由渲染/网格阶段用 BrushSetting 统一缩放
                resultShape.AddNode(morphedNode);
            }

            // 复制边结构（边的拓扑不变）
            foreach (var edge in baseShape.Edges)
            {
                resultShape.AddEdge(edge);
            }

            return resultShape;
        }

        /// <summary>
        /// 保存到JSON文件（包含morph数据）
        /// </summary>
        public bool SaveToFile(string filePath)
        {
            if (baseShape == null)
            {
                Debug.LogError("MorphableShape: Cannot save, base shape is null");
                return false;
            }

            try
            {
                if (string.IsNullOrEmpty(filePath))
                {
                    // 使用默认路径
                    string fileName = $"{baseShape.Name}_{System.DateTime.Now:yyyyMMdd_HHmmss}.json";
                    filePath = System.IO.Path.Combine("Assets/Brushes", fileName);
                }

                // 确保目录存在
                string directory = System.IO.Path.GetDirectoryName(filePath);
                if (!System.IO.Directory.Exists(directory))
                {
                    System.IO.Directory.CreateDirectory(directory);
                }

                // 构建完整的保存数据（包含基础形状和所有 morph）
                var saveData = new BrushShapeJson
                {
                    name = baseShape.Name,
                    type = "cartesian",
                    points = new System.Collections.Generic.List<BrushShapePointRaw>(),
                    edges = new System.Collections.Generic.List<BrushShapeEdgeRaw>(),
                    morphs = new System.Collections.Generic.List<MorphData>()
                };

                // 保存基础形状的顶点
                foreach (var node in baseShape.Nodes)
                {
                    saveData.points.Add(new BrushShapePointRaw { x = node.x, y = node.y });
                }

                // 保存基础形状的边
                foreach (var edge in baseShape.Edges)
                {
                    saveData.edges.Add(new BrushShapeEdgeRaw { a = edge.x, b = edge.y });
                }

                // 保存所有 morph 数据
                for (int i = 0; i < morphShapes.Count; i++)
                {
                    var morphShape = morphShapes[i];
                    var morphName = i < morphNames.Count ? morphNames[i] : $"Morph_{i}";

                    var morphData = new MorphData
                    {
                        name = morphName,
                        points = new System.Collections.Generic.List<BrushShapePointRaw>()
                    };

                    // 保存 morph 的顶点坐标
                    foreach (var node in morphShape.Nodes)
                    {
                        morphData.points.Add(new BrushShapePointRaw { x = node.x, y = node.y });
                    }

                    saveData.morphs.Add(morphData);
                }

                // 序列化并保存
                string json = UnityEngine.JsonUtility.ToJson(saveData, true);
                System.IO.File.WriteAllText(filePath, json);

                Debug.Log($"MorphableShape: Saved base shape '{baseShape.Name}' with {morphShapes.Count} morphs to {filePath}");
                return true;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"MorphableShape: Save failed: {ex.Message}\n{ex.StackTrace}");
                return false;
            }
        }

        /// <summary>
        /// 获取指定morph的名称
        /// </summary>
        public string GetMorphName(int morphIndex)
        {
            if (morphIndex >= 0 && morphIndex < morphNames.Count)
            {
                return morphNames[morphIndex];
            }
            return null;
        }

        /// <summary>
        /// 通过名称查找morph的索引
        /// </summary>
        public int FindMorphIndex(string morphName)
        {
            return morphNames.IndexOf(morphName);
        }
    }
}
