using UnityEngine;

namespace VRBrush.Core.Visual.Element
{
    /// <summary>
    /// Editor-specific edge visual (cylinder) used by BrushEditor.
    /// </summary>
    public static class EditorEdgeVisual
    {
        // 配置常量
        public static readonly float BaseLineRadius = 0.001f; // 基础线半径
        
        // 预设材质和颜色
        public static readonly Color DefaultColor = new Color(0.3f, 0.6f, 1f, 1f); // 浅蓝色
        public static readonly Color PreviewColor = new Color(0f, 1f, 1f, 0.9f); // 青色预览
        
        // 预设尺寸
        public static readonly float DefaultScale = 6f;
        public static readonly float PreviewScale = 6f;

        public static GameObject Create(Transform parent, string name, float lineRadius, EdgeStyle style = EdgeStyle.Default)
        {
            var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(cyl.GetComponent<Collider>());
            cyl.transform.SetParent(parent);
            cyl.name = name;

            var comp = cyl.AddComponent<EditorEdgeVisualComponent>();
            comp.Initialize(lineRadius, style);

            return cyl;
        }

        public static Material CreateEdgeMaterial(Color color, bool isTransparent = false)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            
            if (isTransparent)
            {
                material.SetFloat("_Surface", 1);
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.SetInt("_CullMode", 0);
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.renderQueue = 3000;
            }
            else
            {
                material.SetFloat("_Surface", 0f);
                material.SetInt("_CullMode", 0);
                material.SetFloat("_Smoothness", 0.3f);
                material.SetFloat("_Metallic", 0.05f);
            }
            
            material.color = color;
            return material;
        }

        // 将 ribbonWidth 映射为用于圆柱体/边可视化的半径
        public const float RibbonToRadiusFactor = 0.15f;
        public static float ComputeRadiusFromRibbon(float ribbonWidth)
        {
            return Mathf.Max(0.0001f, ribbonWidth * RibbonToRadiusFactor);
        }

        // 固定预览半径（世界单位），不随笔刷大小变化
        public const float DefaultPreviewWorldRadius = 0.003f;
    }

    public enum EdgeStyle
    {
        Default,
        Preview
    }

    public class EditorEdgeVisualComponent : MonoBehaviour
    {
        private float baseLineRadius = 0.005f;
        private Material defaultMaterial;
        private Material previewMaterial;
        private MeshRenderer meshRenderer;
        private EdgeStyle currentStyle = EdgeStyle.Default;

        public void Initialize(float lineRadius, EdgeStyle style = EdgeStyle.Default)
        {
            baseLineRadius = lineRadius;
            meshRenderer = GetComponent<MeshRenderer>();
            
            // 创建材质
            defaultMaterial = EditorEdgeVisual.CreateEdgeMaterial(EditorEdgeVisual.DefaultColor, false);
            previewMaterial = EditorEdgeVisual.CreateEdgeMaterial(EditorEdgeVisual.PreviewColor, true);
            
            SetStyle(style);
        }

        public void SetStyle(EdgeStyle style)
        {
            currentStyle = style;
            
            switch (style)
            {
                case EdgeStyle.Default:
                    if (meshRenderer != null) meshRenderer.material = defaultMaterial;
                    break;
                    
                case EdgeStyle.Preview:
                    if (meshRenderer != null) meshRenderer.material = previewMaterial;
                    break;
            }
        }

        public void UpdateBetweenLocalPoints(Vector3 a, Vector3 b)
        {
            Vector3 mid = (a + b) * 0.5f;
            Vector3 dir = b - a;
            float len = dir.magnitude;
            if (len < 1e-5f) { gameObject.SetActive(false); return; }
            dir /= len;
            gameObject.SetActive(true);
            transform.localPosition = mid;
            transform.localRotation = Quaternion.FromToRotation(Vector3.up, dir);
            
            float scaleMultiplier = (currentStyle == EdgeStyle.Preview) ? EditorEdgeVisual.PreviewScale : EditorEdgeVisual.DefaultScale;
            transform.localScale = new Vector3(baseLineRadius * scaleMultiplier, len * 0.5f, baseLineRadius * scaleMultiplier);
        }

        public EdgeStyle GetCurrentStyle()
        {
            return currentStyle;
        }
    }
}
