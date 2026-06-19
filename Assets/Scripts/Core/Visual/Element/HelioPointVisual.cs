using UnityEngine;

namespace VRBrush.Core.Visual.Element
{
    /// <summary>
    /// HelioBrush 专用的点可视化（球体），风格类比 BrushEditor 的 EditorPointVisual。
    /// </summary>
    public static class HelioPointVisual
    {
    // 缩小默认点半径以适配预览（原值过大）
    public static readonly float BasePointRadius = 0.0008f;
        public static readonly Color DefaultColor = new Color(0.1f, 0.9f, 0.9f, 1f);
        public static readonly Color PreviewColor = new Color(1f, 0.8f, 0.2f, 0.9f);
    // 适度降低缩放倍数以让点在预览中更小、更不显眼
    public static readonly float DefaultScale = 3f;
    public static readonly float PreviewScale = 2f;

        public static GameObject Create(Transform parent, string name, float radius, HelioPointStyle style = HelioPointStyle.Default)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent);
            go.name = name;
            var comp = go.AddComponent<HelioPointVisualComponent>();
            comp.Initialize(radius, style);
            return go;
        }

        public static Material CreatePointMaterial(Color color)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = color;
            mat.SetFloat("_Smoothness", 0.5f);
            mat.SetFloat("_Metallic", 0.1f);
            return mat;
        }
    }

    public enum HelioPointStyle
    {
        Default,
        Preview
    }

    public class HelioPointVisualComponent : MonoBehaviour
    {
        private float baseRadius = 0.003f;
        private MeshRenderer meshRenderer;
        private HelioPointStyle currentStyle = HelioPointStyle.Default;
        private Material defaultMaterial;

        public void Initialize(float radius, HelioPointStyle style)
        {
            baseRadius = radius;
            meshRenderer = GetComponent<MeshRenderer>();
            defaultMaterial = HelioPointVisual.CreatePointMaterial(HelioPointVisual.DefaultColor);
            SetStyle(style);
        }

        public void SetStyle(HelioPointStyle style)
        {
            currentStyle = style;
            if (meshRenderer == null) return;
            if (style == HelioPointStyle.Preview)
            {
                meshRenderer.material = HelioPointVisual.CreatePointMaterial(HelioPointVisual.PreviewColor);
                transform.localScale = Vector3.one * baseRadius * HelioPointVisual.PreviewScale;
            }
            else
            {
                meshRenderer.material = defaultMaterial;
                transform.localScale = Vector3.one * baseRadius * HelioPointVisual.DefaultScale;
            }
        }

        public void UpdateLocalPosition(Vector3 localPos)
        {
            transform.localPosition = localPos;
        }
    }
}
