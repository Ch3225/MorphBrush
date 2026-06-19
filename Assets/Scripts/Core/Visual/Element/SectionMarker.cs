using System.Collections.Generic;
using UnityEngine;

namespace VRBrush.Core.Visual.Element
{
    /// <summary>
    /// 截面标记：在确认点位置显示截面形状的视觉参考。
    /// 这是一个纯视觉元素，帮助用户在绘制过程中看到已确认的截面位置和形状。
    /// </summary>
    public class SectionMarker : MonoBehaviour
    {
        private LineRenderer lineRenderer;
        private Material lineMaterial;
        private bool initialized = false;
        
        /// <summary>
        /// 创建一个截面标记
        /// </summary>
        /// <param name="position">截面中心位置</param>
        /// <param name="rotation">截面旋转</param>
        /// <param name="shapeVerts">2D截面形状顶点</param>
        /// <param name="width">截面宽度</param>
        /// <param name="color">标记颜色</param>
        /// <param name="lineWidth">线宽</param>
        /// <param name="parent">父对象</param>
        /// <returns>创建的SectionMarker组件</returns>
        public static SectionMarker Create(Vector3 position, Quaternion rotation, 
                                           Vector2[] shapeVerts, float width,
                                           Color color, float lineWidth = 0.003f,
                                           Transform parent = null)
        {
            if (shapeVerts == null || shapeVerts.Length < 2) return null;
            
            GameObject markerObj = new GameObject("SectionMarker");
            if (parent != null)
            {
                markerObj.transform.SetParent(parent, worldPositionStays: true);
            }
            
            var marker = markerObj.AddComponent<SectionMarker>();
            marker.Initialize(position, rotation, shapeVerts, width, color, lineWidth);
            
            return marker;
        }
        
        private void Initialize(Vector3 position, Quaternion rotation, 
                               Vector2[] shapeVerts, float width,
                               Color color, float lineWidth)
        {
            if (initialized) return;
            initialized = true;
            
            // 创建材质
            lineMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            lineMaterial.color = color;
            
            // 创建LineRenderer
            lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.material = lineMaterial;
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            lineRenderer.useWorldSpace = true;
            lineRenderer.loop = true;
            
            // 计算世界空间位置
            // 使用与 PreviewGeometry.Map2DToWorld 一致的坐标映射：
            // - rotation * Vector3.up 对应 v.x (ruling方向)
            // - rotation * Vector3.right 对应 v.y (normal方向)
            Vector3 ruling = rotation * Vector3.up;
            Vector3 normal = rotation * Vector3.right;
            
            // 将2D形状转换为3D位置
            List<Vector3> worldPositions = new List<Vector3>();
            for (int i = 0; i < shapeVerts.Length; i++)
            {
                Vector2 v = shapeVerts[i];
                Vector3 worldPos = position + width * (ruling * v.x + normal * v.y);
                worldPositions.Add(worldPos);
            }
            
            // 设置LineRenderer
            lineRenderer.positionCount = worldPositions.Count;
            lineRenderer.SetPositions(worldPositions.ToArray());
        }
        
        /// <summary>
        /// 设置标记可见性
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (lineRenderer != null)
            {
                lineRenderer.enabled = visible;
            }
        }
        
        /// <summary>
        /// 销毁标记
        /// </summary>
        public void Destroy()
        {
            if (lineMaterial != null)
            {
                Object.Destroy(lineMaterial);
            }
            Object.Destroy(gameObject);
        }
        
        private void OnDestroy()
        {
            if (lineMaterial != null)
            {
                Object.Destroy(lineMaterial);
            }
        }
    }
}
