using UnityEngine;

namespace VRBrush.Core.Visual.Element
{
    /// <summary>
    /// Editor-specific point visual (sphere) used by BrushEditor.
    /// </summary>
    public static class EditorPointVisual
    {
        // 配置常量
        public static readonly float BasePointRadius = 0.003f; // 基础点半径
        
        // 预设材质和颜色
        public static readonly Color DefaultColor = new Color(0.2f, 0.4f, 1f, 1f); // 蓝色
        public static readonly Color HighlightColor = new Color(1f, 0.5f, 0f, 1f); // 橙色高亮
        public static readonly Color PreviewColor = new Color(1f, 0.8f, 0.2f, 0.9f); // 黄色预览点
        
        // 预设尺寸
        public static readonly float DefaultScale = 6f;
        public static readonly float HighlightScale = 6f;
        public static readonly float PreviewScale = 6f;

        public static GameObject Create(Transform parent, string name, float radius, PointStyle style = PointStyle.Default)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent);
            go.name = name;

            var comp = go.AddComponent<EditorPointVisualComponent>();
            comp.Initialize(radius, style);

            return go;
        }

        public static Material CreatePointMaterial(Color color)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.color = color;
            material.SetFloat("_Smoothness", 0.5f);
            material.SetFloat("_Metallic", 0.1f);
            return material;
        }
    }

    public enum PointStyle
    {
        Default,
        Highlight,
        Preview
    }

    public class EditorPointVisualComponent : MonoBehaviour
    {
        private float baseRadius = 0.01f;
        private Material defaultMaterial;
        private Material highlightMaterial;
        private MeshRenderer meshRenderer;
        private PointStyle currentStyle = PointStyle.Default;

        public void Initialize(float radius, PointStyle style = PointStyle.Default)
        {
            baseRadius = radius;
            meshRenderer = GetComponent<MeshRenderer>();
            
            // 创建材质
            defaultMaterial = EditorPointVisual.CreatePointMaterial(EditorPointVisual.DefaultColor);
            highlightMaterial = EditorPointVisual.CreatePointMaterial(EditorPointVisual.HighlightColor);
            
            SetStyle(style);
        }

        public void SetStyle(PointStyle style)
        {
            currentStyle = style;
            
            switch (style)
            {
                case PointStyle.Default:
                    transform.localScale = Vector3.one * baseRadius * EditorPointVisual.DefaultScale;
                    if (meshRenderer != null) meshRenderer.material = defaultMaterial;
                    break;
                    
                case PointStyle.Highlight:
                    transform.localScale = Vector3.one * baseRadius * EditorPointVisual.HighlightScale;
                    if (meshRenderer != null) meshRenderer.material = highlightMaterial;
                    break;
                    
                case PointStyle.Preview:
                    transform.localScale = Vector3.one * baseRadius * EditorPointVisual.PreviewScale;
                    if (meshRenderer != null) 
                    {
                        var previewMat = EditorPointVisual.CreatePointMaterial(EditorPointVisual.PreviewColor);
                        meshRenderer.material = previewMat;
                    }
                    break;
            }
        }

        public void UpdateLocalPosition(Vector3 localPos)
        {
            transform.localPosition = localPos;
        }

        public PointStyle GetCurrentStyle()
        {
            return currentStyle;
        }
    }
}
