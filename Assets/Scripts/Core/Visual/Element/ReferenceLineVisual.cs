using UnityEngine;

namespace VRBrush.Core.Visual.Element
{
    /// <summary>
    /// 参考线（两端为球，中间为圆柱），用于拖拽时从按下点到当前点的可视化。
    /// </summary>
    public class ReferenceLineVisual : MonoBehaviour
    {
        [SerializeField] private Material lineMaterial;
        [SerializeField] private Color lineColor = new Color(0.9f, 0.6f, 0.2f, 0.9f);
        [SerializeField] private float radius = 0.004f;

        private GameObject startSphere;
        private GameObject endSphere;
        private GameObject cylinder;
        private bool initialized = false;

        public void Initialize(Material baseMat = null, Color? color = null, float? lineRadius = null)
        {
            if (initialized) return;
            initialized = true;

            if (lineRadius.HasValue) radius = Mathf.Max(1e-4f, lineRadius.Value);
            if (color.HasValue) lineColor = color.Value;

            if (baseMat == null)
            {
                lineMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                lineMaterial.SetFloat("_Surface", 1f); // 透明
                lineMaterial.SetFloat("_AlphaClip", 0f);
                lineMaterial.SetInt("_CullMode", 0);
                lineMaterial.color = lineColor;
                // 设置渲染队列和深度偏移以减少闪烁
                lineMaterial.renderQueue = 3001; // 透明队列+1
                lineMaterial.SetFloat("_ZWrite", 0f);
            }
            else
            {
                lineMaterial = new Material(baseMat);
                lineMaterial.color = lineColor;
            }

            // 创建端点球
            startSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DestroyImmediate(startSphere.GetComponent<Collider>());
            startSphere.name = "ReferenceLine_Start";
            startSphere.transform.SetParent(transform);
            startSphere.transform.localScale = Vector3.one * (radius * 2f);
            var mrA = startSphere.GetComponent<MeshRenderer>();
            if (mrA != null) mrA.material = lineMaterial;

            endSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            DestroyImmediate(endSphere.GetComponent<Collider>());
            endSphere.name = "ReferenceLine_End";
            endSphere.transform.SetParent(transform);
            endSphere.transform.localScale = Vector3.one * (radius * 2f);
            var mrB = endSphere.GetComponent<MeshRenderer>();
            if (mrB != null) mrB.material = lineMaterial;

            // 创建圆柱
            cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            DestroyImmediate(cylinder.GetComponent<Collider>());
            cylinder.name = "ReferenceLine_Cylinder";
            cylinder.transform.SetParent(transform);
            var mrC = cylinder.GetComponent<MeshRenderer>();
            if (mrC != null) mrC.material = lineMaterial;

            SetVisible(false);
        }

        public void SetVisible(bool visible)
        {
            if (!initialized) Initialize();
            if (startSphere) startSphere.SetActive(visible);
            if (endSphere) endSphere.SetActive(visible);
            if (cylinder) cylinder.SetActive(visible);
        }

        public void SetColor(Color color)
        {
            lineColor = color;
            if (lineMaterial != null) lineMaterial.color = color;
        }

        public void SetRadius(float r)
        {
            radius = Mathf.Max(1e-4f, r);
            if (startSphere) startSphere.transform.localScale = Vector3.one * (radius * 2f);
            if (endSphere) endSphere.transform.localScale = Vector3.one * (radius * 2f);
            // 圆柱半径随 UpdateEndpoints 时一起更新 XZ 缩放
        }

        public void UpdateEndpoints(Vector3 a, Vector3 b)
        {
            if (!initialized) Initialize();
            if (startSphere) startSphere.transform.position = a;
            if (endSphere) endSphere.transform.position = b;

            if (cylinder)
            {
                Vector3 dir = b - a;
                float len = dir.magnitude;
                if (len < 1e-6f)
                {
                    cylinder.SetActive(false);
                    return;
                }
                cylinder.SetActive(true);
                Vector3 mid = a + dir * 0.5f;
                cylinder.transform.position = mid;
                // Unity 原生 Cylinder 沿局部 Y 轴
                cylinder.transform.rotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
                // 缩放：默认高度=2，因此 Y 缩放=长度/2；XZ 为直径
                cylinder.transform.localScale = new Vector3(radius * 2f, len * 0.5f, radius * 2f);
            }
        }

        public void Clear()
        {
            SetVisible(false);
        }
    }
}
