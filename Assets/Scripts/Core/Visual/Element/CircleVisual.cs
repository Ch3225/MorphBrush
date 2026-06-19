using UnityEngine;

namespace VRBrush.Core.Visual.Element
{
    /// <summary>
    /// Manages polar coordinate system radial iso-lines (circles) for BrushEditor.
    /// Creates and updates circle LineRenderer centered at origin in local space.
    /// </summary>
    public static class CircleVisual
    {
        // 默认配置
        public static readonly int DefaultSegments = 64;
        public static readonly float DefaultWidth = 0.002f;
        
        /// <summary>
        /// 创建圆形等值线（使用外部材质）
        /// </summary>
        public static GameObject Create(Transform parent, string name, float radius, int segments, Material material, float width)
        {
            GameObject circle = new GameObject(name);
            LineRenderer lr = circle.AddComponent<LineRenderer>();
            if (material != null) lr.material = material;
            lr.startWidth = lr.endWidth = width;
            lr.positionCount = segments + 1;
            lr.useWorldSpace = false;
            lr.loop = true;

            for (int i = 0; i <= segments; i++)
            {
                float angle = (float)i / segments * 2f * Mathf.PI;
                Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                lr.SetPosition(i, pos);
            }

            circle.transform.SetParent(parent);
            return circle;
        }

        /// <summary>
        /// 创建极坐标系径向等值线（使用预设材质和配置）
        /// </summary>
        public static GameObject CreatePolarRadialLine(Transform parent, string name, float radius, float yOffset = 0f)
        {
            GameObject circle = new GameObject(name);
            LineRenderer lr = circle.AddComponent<LineRenderer>();
            
            // 使用坐标系材质
            lr.material = AxisVisual.CreateCoordinateSystemMaterial();
            lr.startWidth = lr.endWidth = EditorEdgeVisual.BaseLineRadius * 2f;
            lr.positionCount = DefaultSegments + 1;
            lr.useWorldSpace = false;
            lr.loop = true;

            UpdatePolarRadialLine(circle, radius, yOffset);
            circle.transform.SetParent(parent);
            return circle;
        }

        /// <summary>
        /// 更新极坐标径向线半径
        /// </summary>
        public static void UpdatePolarRadialLine(GameObject circle, float radius, float yOffset = 0f)
        {
            if (circle == null) return;
            var lr = circle.GetComponent<LineRenderer>();
            if (lr == null) return;
            
            if (lr.positionCount != DefaultSegments + 1) 
                lr.positionCount = DefaultSegments + 1;
                
            for (int i = 0; i <= DefaultSegments; i++)
            {
                float ang = (float)i / DefaultSegments * Mathf.PI * 2f;
                float x = Mathf.Cos(ang) * radius;
                float z = Mathf.Sin(ang) * radius;
                lr.SetPosition(i, new Vector3(x, yOffset, z));
            }
        }

        /// <summary>
        /// 更新圆形半径（原有功能保持兼容）
        /// </summary>
        public static void UpdateRadius(GameObject circle, float radius, int segments, float yOffset = 0f)
        {
            if (circle == null) return;
            var lr = circle.GetComponent<LineRenderer>();
            if (lr == null) return;
            if (lr.positionCount != segments + 1) lr.positionCount = segments + 1;
            for (int i = 0; i <= segments; i++)
            {
                float ang = (float)i / segments * Mathf.PI * 2f;
                float x = Mathf.Cos(ang) * radius;
                float z = Mathf.Sin(ang) * radius;
                lr.SetPosition(i, new Vector3(x, yOffset, z));
            }
        }
    }
}
