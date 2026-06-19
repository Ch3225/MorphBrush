using UnityEngine;

namespace VRBrush.Core.Visual.Element
{
    /// <summary>
    /// Creates and manages coordinate system visual elements including axes, circles, and editing planes.
    /// Unified coordinate system visual management for BrushEditor.
    /// </summary>
    public static class AxisVisual
    {
        // 配置常量
        public static readonly float CoordinateSystemSize = 0.14f; // 坐标系大小
        public static readonly float EditingPlaneHalfThickness = 0.002f; // 编辑平面半厚度
        public static readonly float GeometryLift = 0.0022f; // 几何抬升高度 (editingPlaneHalfThickness + 0.0002f)
        public static readonly int DynamicCircleSegments = 32; // 动态圆圈分段数
        
        // 坐标系材质颜色
        public static readonly Color CoordinateSystemColor = new Color(1f, 1f, 1f, 0.7f); // 白色半透明

        /// <summary>
        /// 创建简单直线（原有功能）
        /// </summary>
        public static GameObject Create(Transform parent, string name, Vector3 start, Vector3 end, Material lineMaterial, float width)
        {
            GameObject line = new GameObject(name);
            LineRenderer lr = line.AddComponent<LineRenderer>();
            if (lineMaterial != null) lr.material = lineMaterial;
            lr.startWidth = lr.endWidth = width;
            lr.positionCount = 2;
            lr.useWorldSpace = false;
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
            line.transform.SetParent(parent);
            return line;
        }

        /// <summary>
        /// 更新直线位置（原有功能）
        /// </summary>
        public static void UpdateLine(GameObject line, Vector3 start, Vector3 end)
        {
            if (line == null) return;
            var lr = line.GetComponent<LineRenderer>();
            if (lr == null) return;
            lr.SetPosition(0, start);
            lr.SetPosition(1, end);
        }

        /// <summary>
        /// 创建透明坐标系材质
        /// </summary>
        public static Material CreateCoordinateSystemMaterial()
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetFloat("_Surface", 1); // Transparent
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.SetInt("_CullMode", 0); // Disable culling
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.renderQueue = 3000;
            material.color = CoordinateSystemColor;
            return material;
        }
        
        /// <summary>
        /// 创建编辑平面 - 使用DiskVisual
        /// </summary>
        public static GameObject CreateEditingPlane(Transform parent, string name = "EditingPlane")
        {
            float planeRadius = CoordinateSystemSize * 2f;
            var material = CreateCoordinateSystemMaterial();
            return DiskVisual.Create(parent, name, planeRadius, EditingPlaneHalfThickness, material);
        }
        
        // 圆形线条功能由CircleVisual类提供
        
        /// <summary>
        /// 创建坐标系直线（使用预设材质）
        /// </summary>
        public static GameObject CreateCoordinateSystemLine(Transform parent, Vector3 start, Vector3 end, string name = "CoordinateSystemLine")
        {
            var lineObj = new GameObject(name);
            lineObj.transform.SetParent(parent);
            
            var lr = lineObj.AddComponent<LineRenderer>();
            lr.startWidth = lr.endWidth = EditorEdgeVisual.BaseLineRadius * 2f;
            lr.positionCount = 2;
            lr.useWorldSpace = false;
            lr.material = CreateCoordinateSystemMaterial();
            lr.SetPositions(new Vector3[] { start, end });
            
            return lineObj;
        }
        
        /// <summary>
        /// 创建极坐标系角度等值线（射线）
        /// </summary>
        public static GameObject CreatePolarAngularLine(Transform parent, string name, float maxRadius)
        {
            var lineObj = new GameObject(name);
            lineObj.transform.SetParent(parent);
            
            var lr = lineObj.AddComponent<LineRenderer>();
            lr.startWidth = lr.endWidth = EditorEdgeVisual.BaseLineRadius * 2f;
            lr.positionCount = 2;
            lr.useWorldSpace = false;
            lr.material = CreateCoordinateSystemMaterial();
            
            // 初始为零长度射线
            Vector3 origin = new Vector3(0f, GeometryLift, 0f);
            lr.SetPositions(new Vector3[] { origin, origin });
            
            return lineObj;
        }
        
        /// <summary>
        /// 更新极坐标角度线方向和长度
        /// </summary>
        public static void UpdatePolarAngularLine(GameObject angularLine, Vector3 localTargetPoint)
        {
            if (angularLine == null) return;
            var lr = angularLine.GetComponent<LineRenderer>();
            if (lr == null) return;
            
            Vector3 origin = new Vector3(0f, GeometryLift, 0f);
            Vector3 target = localTargetPoint;
            target.y = GeometryLift; // 确保在同一高度
            
            lr.SetPosition(0, origin);
            lr.SetPosition(1, target);
        }
    }
}
