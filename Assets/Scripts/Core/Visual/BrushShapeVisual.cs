using System.Collections.Generic;
using UnityEngine;
using VRBrush.Core.Model;
using VRBrush.Core.Visual.Element;

namespace VRBrush.Core.Visual
{
    /// <summary>
    /// 纯视觉化组件：显示 BrushShape 的点和边
    /// 不绑定 BrushShapeBuilder，只显示静态或可编辑的形状
    /// </summary>
    public class BrushShapeVisual : MonoBehaviour
    {
        private BrushShape shape;
        private Transform coordinateTransform;
        private bool isEditable = false;
        private float pointRadius = 0.01f;
        private float lineRadius = 0.005f;
        
        // 可视化元素
        private List<GameObject> pointVisuals = new List<GameObject>();
        private List<GameObject> edgeVisuals = new List<GameObject>();
        
        // 材质
        private Material pointMaterial;
        private Material edgeMaterial;
        
        /// <summary>
        /// 初始化可视化
        /// </summary>
        /// <param name="brushShape">要显示的 BrushShape</param>
        /// <param name="coordinateSystem">坐标系 Transform</param>
        /// <param name="editable">是否可编辑（影响材质和交互）</param>
        /// <param name="ptRadius">点半径</param>
        /// <param name="lnRadius">线半径</param>
        public void Initialize(
            BrushShape brushShape,
            Transform coordinateSystem,
            bool editable = false,
            float ptRadius = 0.01f,
            float lnRadius = 0.005f)
        {
            shape = brushShape;
            coordinateTransform = coordinateSystem;
            isEditable = editable;
            pointRadius = ptRadius;
            lineRadius = lnRadius;
            
            CreateMaterials();
            RebuildVisuals();
        }
        
        /// <summary>
        /// 创建材质
        /// </summary>
        private void CreateMaterials()
        {
            if (isEditable)
            {
                // 可编辑：使用 BrushEditor 风格的蓝色（完全不透明）
                pointMaterial = CreateMaterial(new Color(0.2f, 0.4f, 1f, 1f), false); // 蓝色（与EditorPointVisual一致）
                edgeMaterial = CreateMaterial(new Color(0.3f, 0.6f, 1f, 1f), false);  // 浅蓝色（与EditorEdgeVisual一致）
            }
            else
            {
                // 只读（影子）：浅色蓝色系变体，半透明但不透明度超过0.5
                // 参考 BrushEditor 的蓝色系，调整为更浅的色调
                pointMaterial = CreateMaterial(new Color(0.5f, 0.7f, 1f, 0.6f), true); // 浅蓝色，alpha=0.6
                edgeMaterial = CreateMaterial(new Color(0.6f, 0.75f, 1f, 0.6f), true); // 更浅的蓝色，alpha=0.6
            }
        }
        
        private Material CreateMaterial(Color color, bool transparent)
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            
            if (transparent)
            {
                // 设置透明模式（参考 EditorEdgeVisual 的设置）
                mat.SetFloat("_Surface", 1f); // Transparent
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.SetInt("_CullMode", 0); // 双面渲染，避免角度问题
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.renderQueue = 3000;
                
                // 设置材质属性，避免白色高光
                mat.SetFloat("_Smoothness", 0.2f); // 降低光滑度
                mat.SetFloat("_Metallic", 0.0f);   // 非金属
            }
            else
            {
                // 不透明模式
                mat.SetFloat("_Surface", 0f); // Opaque
                mat.SetInt("_CullMode", 0); // 双面渲染
                mat.SetFloat("_Smoothness", 0.3f);
                mat.SetFloat("_Metallic", 0.05f);
            }
            
            mat.color = color;
            return mat;
        }
        
        /// <summary>
        /// 重建所有可视化元素
        /// </summary>
        public void RebuildVisuals()
        {
            ClearVisuals();
            
            if (shape == null || coordinateTransform == null)
            {
                return;
            }
            
            // 创建点
            for (int i = 0; i < shape.NodeCount; i++)
            {
                CreatePointVisual(i);
            }
            
            // 创建边
            for (int i = 0; i < shape.EdgeCount; i++)
            {
                CreateEdgeVisual(i);
            }
            
            var createdMsg = string.Format(
                "BrushShapeVisual: Created {0} points and {1} edges for shape '{2}'",
                pointVisuals.Count,
                edgeVisuals.Count,
                shape.Name);
            Debug.Log(createdMsg);
        }
        
        /// <summary>
        /// 创建点的可视化
        /// </summary>
        private void CreatePointVisual(int nodeIndex)
        {
            if (nodeIndex < 0 || nodeIndex >= shape.NodeCount)
                return;
                
            Vector2 localPos2D = shape.Nodes[nodeIndex];
            // 将几何体中心放在圆盘表面（使用 GeometryLift，与 BrushEditor 一致）
            const float GeometryLift = 0.0022f;
            Vector3 localPos3D = new Vector3(localPos2D.x, GeometryLift, localPos2D.y);
            
            GameObject point = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            point.name = $"Point_{nodeIndex}_{(isEditable ? "Edit" : "Shadow")}";
            point.transform.SetParent(coordinateTransform);
            point.transform.localPosition = localPos3D;
            point.transform.localRotation = Quaternion.identity;
            point.transform.localScale = Vector3.one * pointRadius;
            
            point.GetComponent<Renderer>().material = pointMaterial;
            
            // 移除碰撞器（避免干扰射线检测）
            Destroy(point.GetComponent<Collider>());
            
            pointVisuals.Add(point);
        }
        
        /// <summary>
        /// 创建边的可视化
        /// </summary>
        private void CreateEdgeVisual(int edgeIndex)
        {
            if (edgeIndex < 0 || edgeIndex >= shape.EdgeCount)
                return;
                
            var edge = shape.Edges[edgeIndex];
            if (edge.x < 0 || edge.x >= shape.NodeCount || edge.y < 0 || edge.y >= shape.NodeCount)
                return;
                
            Vector2 nodeA2D = shape.Nodes[edge.x];
            Vector2 nodeB2D = shape.Nodes[edge.y];
            // 将几何体中心放在圆盘表面（使用 GeometryLift，与 BrushEditor 一致）
            const float GeometryLift = 0.0022f;
            Vector3 nodeA3D = new Vector3(nodeA2D.x, GeometryLift, nodeA2D.y);
            Vector3 nodeB3D = new Vector3(nodeB2D.x, GeometryLift, nodeB2D.y);
            
            GameObject edgeObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            edgeObj.name = $"Edge_{edge.x}_{edge.y}_{(isEditable ? "Edit" : "Shadow")}";
            edgeObj.transform.SetParent(coordinateTransform);
            
            // 设置位置和旋转
            Vector3 midpoint = (nodeA3D + nodeB3D) / 2f;
            Vector3 direction = nodeB3D - nodeA3D;
            float distance = direction.magnitude;
            
            edgeObj.transform.localPosition = midpoint;
            edgeObj.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
            edgeObj.transform.localScale = new Vector3(lineRadius, distance / 2f, lineRadius);
            
            edgeObj.GetComponent<Renderer>().material = edgeMaterial;
            
            // 移除碰撞器
            Destroy(edgeObj.GetComponent<Collider>());
            
            edgeVisuals.Add(edgeObj);
        }
        
        /// <summary>
        /// 更新节点位置（仅可编辑模式）
        /// </summary>
        public void UpdateNodePosition(int nodeIndex, Vector2 newLocalPosition)
        {
            if (!isEditable || shape == null)
            {
                Debug.LogWarning("BrushShapeVisual: Cannot update node position in read-only mode or null shape.");
                return;
            }
            
            if (nodeIndex < 0 || nodeIndex >= shape.NodeCount)
            {
                Debug.LogWarning($"BrushShapeVisual: Invalid node index {nodeIndex}");
                return;
            }
            
            // 更新 BrushShape 中的节点位置
            if (shape.SetNode(nodeIndex, newLocalPosition))
            {
                // 更新点的可视化（使用 GeometryLift）
                if (nodeIndex < pointVisuals.Count && pointVisuals[nodeIndex] != null)
                {
                    const float GeometryLift = 0.0022f;
                    Vector3 localPos3D = new Vector3(
                        newLocalPosition.x,
                        GeometryLift,
                        newLocalPosition.y);
                    pointVisuals[nodeIndex].transform.localPosition = localPos3D;
                }
                
                // 更新相关边的可视化
                UpdateAffectedEdges(nodeIndex);
            }
        }
        
        /// <summary>
        /// 更新受影响的边
        /// </summary>
        private void UpdateAffectedEdges(int nodeIndex)
        {
            for (int i = 0; i < shape.EdgeCount; i++)
            {
                var edge = shape.Edges[i];
                if (edge.x == nodeIndex || edge.y == nodeIndex)
                {
                    UpdateEdgeVisual(i);
                }
            }
        }
        
        /// <summary>
        /// 更新边的可视化
        /// </summary>
        private void UpdateEdgeVisual(int edgeIndex)
        {
            if (edgeIndex < 0 || edgeIndex >= shape.EdgeCount || edgeIndex >= edgeVisuals.Count)
                return;
                
            var edge = shape.Edges[edgeIndex];
            Vector2 nodeA2D = shape.Nodes[edge.x];
            Vector2 nodeB2D = shape.Nodes[edge.y];
            // 使用 GeometryLift
            const float GeometryLift = 0.0022f;
            Vector3 nodeA3D = new Vector3(nodeA2D.x, GeometryLift, nodeA2D.y);
            Vector3 nodeB3D = new Vector3(nodeB2D.x, GeometryLift, nodeB2D.y);
            
            GameObject edgeObj = edgeVisuals[edgeIndex];
            if (edgeObj != null)
            {
                Vector3 midpoint = (nodeA3D + nodeB3D) / 2f;
                Vector3 direction = nodeB3D - nodeA3D;
                float distance = direction.magnitude;
                
                edgeObj.transform.localPosition = midpoint;
                edgeObj.transform.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
                edgeObj.transform.localScale = new Vector3(lineRadius, distance / 2f, lineRadius);
            }
        }
        
        /// <summary>
        /// 清除所有可视化元素
        /// </summary>
        private void ClearVisuals()
        {
            foreach (var point in pointVisuals)
            {
                if (point != null)
                    Destroy(point);
            }
            pointVisuals.Clear();
            
            foreach (var edge in edgeVisuals)
            {
                if (edge != null)
                    Destroy(edge);
            }
            edgeVisuals.Clear();
        }
        
        /// <summary>
        /// 设置可见性
        /// </summary>
        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }
        
        void OnDestroy()
        {
            ClearVisuals();
        }
    }
}
