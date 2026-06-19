using UnityEngine;

namespace VRBrush.Core.Visual.Element
{
    /// <summary>
    /// HelioBrush 专用的边可视化（圆柱），风格类比 BrushEditor 的 EditorEdgeVisual。
    /// </summary>
    public static class HelioEdgeVisual
    {
        public static readonly float BaseLineRadius = 0.001f;

        // 颜色与材质（默认稍偏青色，预览更透明）
        public static readonly Color DefaultColor = new Color(0.1f, 0.9f, 0.9f, 1f);
        public static readonly Color PreviewColor = new Color(0.1f, 0.9f, 0.9f, 0.7f);

        public static readonly float DefaultScale = 6f;
        public static readonly float PreviewScale = 6f;

        public static GameObject Create(Transform parent, string name, float lineRadius, HelioEdgeStyle style = HelioEdgeStyle.Default)
        {
            var cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(cyl.GetComponent<Collider>());
            cyl.transform.SetParent(parent);
            cyl.name = name;

            var comp = cyl.AddComponent<HelioEdgeVisualComponent>();
            comp.Initialize(lineRadius, style);
            return cyl;
        }

        public static Material CreateEdgeMaterial(Color color, bool transparent)
        {
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (transparent)
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

        // 将 ribbonWidth 映射为用于圆柱体/边可视化的半径（保持在 Element 层配置）
        // 可根据需要微调默认系数，Preview/Editor 可共用相同的策略
        public const float RibbonToRadiusFactor = 0.15f;
        public static float ComputeRadiusFromRibbon(float ribbonWidth)
        {
            return Mathf.Max(0.0001f, ribbonWidth * RibbonToRadiusFactor);
        }

        // 固定预览半径（世界单位），不随笔刷大小变化
        public const float DefaultPreviewWorldRadius = 0.003f;
    }

    public enum HelioEdgeStyle
    {
        Default,
        Preview
    }

    public class HelioEdgeVisualComponent : MonoBehaviour
    {
        private float baseLineRadius = 0.001f;
        private Material defaultMaterial;
        private Material previewMaterial;
        private MeshRenderer meshRenderer;
        private HelioEdgeStyle currentStyle = HelioEdgeStyle.Default;

        public void Initialize(float lineRadius, HelioEdgeStyle style = HelioEdgeStyle.Default)
        {
            baseLineRadius = lineRadius;
            meshRenderer = GetComponent<MeshRenderer>();
            defaultMaterial = HelioEdgeVisual.CreateEdgeMaterial(HelioEdgeVisual.DefaultColor, false);
            previewMaterial = HelioEdgeVisual.CreateEdgeMaterial(HelioEdgeVisual.PreviewColor, true);
            SetStyle(style);
        }

        public void SetStyle(HelioEdgeStyle style)
        {
            currentStyle = style;
            if (meshRenderer == null) return;
            meshRenderer.material = (style == HelioEdgeStyle.Preview) ? previewMaterial : defaultMaterial;
        }

        /// <summary>
        /// 在本地坐标系下，用两点更新圆柱体（与 EditorEdgeVisualComponent 相同接口）。
        /// </summary>
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
            float scale = (currentStyle == HelioEdgeStyle.Preview) ? HelioEdgeVisual.PreviewScale : HelioEdgeVisual.DefaultScale;
            transform.localScale = new Vector3(baseLineRadius * scale, len * 0.5f, baseLineRadius * scale);
        }
    }
}
