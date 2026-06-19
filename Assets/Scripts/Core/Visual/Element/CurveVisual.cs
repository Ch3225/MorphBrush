using UnityEngine;
using VRBrush.Core.Model.ADBrush;

namespace VRBrush.Core.Visual.Element
{
    /// <summary>
    /// 曲线可视化组件
    /// 用于显示 StrokeCurve 的预览
    /// </summary>
    public class CurveVisual : MonoBehaviour
    {
        [SerializeField] private Material lineMaterial;
        [SerializeField] private float lineWidth = 0.002f;
        [SerializeField] private Color lineColor = new Color(0.2f, 0.8f, 1f, 0.8f);
    [SerializeField] private bool showPoints = false;
    [SerializeField] private float pointRadius = 0.005f;
    [SerializeField] private bool showTangents = false; // 默认关闭：不要任何黄色/橙色折线
    [SerializeField] private bool showUps = false;      // 默认关闭：不要任何黄色/橙色折线
    [SerializeField] private int tickEvery = 3; // 每隔多少个样本绘制一次刻度
    [SerializeField] private float tangentTickLen = 0.01f;
    [SerializeField] private float upTickLen = 0.01f;

        private LineRenderer lineRenderer;
    private StrokeCurve currentCurve;
        private readonly System.Collections.Generic.List<GameObject> pointGizmos = new System.Collections.Generic.List<GameObject>();
    private LineRenderer tangentRenderer;
    private LineRenderer upRenderer;
    private GameObject ghostHeadGizmo;

        private void Awake()
        {
            // 创建 LineRenderer
            lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            lineRenderer.useWorldSpace = true;
            lineRenderer.positionCount = 0;

            // 创建材质
            if (lineMaterial == null)
            {
                lineMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                lineMaterial.color = lineColor;
            }

            lineRenderer.material = lineMaterial;

            // 切线刻度渲染器
            tangentRenderer = new GameObject("Curve_TangentTicks").AddComponent<LineRenderer>();
            tangentRenderer.transform.SetParent(transform);
            tangentRenderer.useWorldSpace = true;
            tangentRenderer.startWidth = lineWidth * 0.5f;
            tangentRenderer.endWidth = lineWidth * 0.5f;
            tangentRenderer.material = new Material(lineMaterial);
            tangentRenderer.material.color = new Color(lineColor.r, lineColor.g, lineColor.b, 0.7f);
            tangentRenderer.positionCount = 0;

            // Up 刻度渲染器
            upRenderer = new GameObject("Curve_UpTicks").AddComponent<LineRenderer>();
            upRenderer.transform.SetParent(transform);
            upRenderer.useWorldSpace = true;
            upRenderer.startWidth = lineWidth * 0.5f;
            upRenderer.endWidth = lineWidth * 0.5f;
            upRenderer.material = new Material(lineMaterial);
            upRenderer.material.color = new Color(1f, 0.6f, 0.2f, 0.7f); // 与主线区分
            upRenderer.positionCount = 0;
        }

        /// <summary>
        /// 更新曲线显示
        /// </summary>
        public void UpdateCurve(StrokeCurve curve)
        {
            if (curve == null || curve.PointCount == 0)
            {
                lineRenderer.positionCount = 0;
                UpdateAnchorPoints(null);
                return;
            }

            currentCurve = curve;

            // 设置 LineRenderer 的点
            int count = curve.PointCount;
            // 单点情况下绘制一个极短的线段，保证可见
            if (count == 1)
            {
                CurvePoint p = curve.GetPoint(0);
                lineRenderer.positionCount = 2;
                lineRenderer.SetPosition(0, p.position);
                lineRenderer.SetPosition(1, p.position + Vector3.up * Mathf.Max(1e-4f, pointRadius * 0.2f));
            }
            else
            {
                lineRenderer.positionCount = count;
                for (int i = 0; i < count; i++)
                {
                    CurvePoint point = curve.GetPoint(i);
                    lineRenderer.SetPosition(i, point.position);
                }
            }

            // 刻度：切线与 Up
            UpdateTicks();
        }

        /// <summary>
        /// 清除曲线
        /// </summary>
        public void Clear()
        {
            if (lineRenderer != null)
            {
                lineRenderer.positionCount = 0;
            }
            currentCurve = null;
            UpdateAnchorPoints(null);
            if (tangentRenderer != null) tangentRenderer.positionCount = 0;
            if (upRenderer != null) upRenderer.positionCount = 0;
            UpdateGhostHead(null);
        }

        /// <summary>
        /// 设置可见性
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (lineRenderer != null)
            {
                lineRenderer.enabled = visible;
            }
        }

        /// <summary>
        /// 设置线条颜色
        /// </summary>
        public void SetColor(Color color)
        {
            lineColor = color;
            if (lineMaterial != null)
            {
                lineMaterial.color = color;
            }
            // 同步点材质颜色
            foreach (var go in pointGizmos)
            {
                if (go != null)
                {
                    var mr = go.GetComponent<MeshRenderer>();
                    if (mr != null && mr.material != null) mr.material.color = color;
                }
            }
            if (tangentRenderer != null && tangentRenderer.material != null)
            {
                tangentRenderer.material.color = new Color(color.r, color.g, color.b, 0.7f);
            }
        }

        /// <summary>
        /// 设置线条宽度
        /// </summary>
        public void SetWidth(float width)
        {
            lineWidth = width;
            if (lineRenderer != null)
            {
                lineRenderer.startWidth = width;
                lineRenderer.endWidth = width;
            }
        }

        public void SetShowPoints(bool show, float radius = 0.005f)
        {
            showPoints = show;
            pointRadius = Mathf.Max(1e-4f, radius);
            // 立即刷新现有点尺寸
            for (int i = 0; i < pointGizmos.Count; i++)
            {
                var go = pointGizmos[i];
                if (go != null) go.transform.localScale = Vector3.one * (pointRadius * 2f);
            }
        }

        // 同步锚点（确认点）的小球显示
        public void UpdateAnchorPoints(System.Collections.Generic.IReadOnlyList<Vector3> anchors)
        {
            if (!showPoints)
            {
                // 隐藏并回收
                foreach (var go in pointGizmos) if (go) go.SetActive(false);
                return;
            }

            int target = anchors != null ? anchors.Count : 0;
            // 扩容
            while (pointGizmos.Count < target)
            {
                var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                DestroyImmediate(sphere.GetComponent<Collider>());
                sphere.name = "CurvePointGizmo";
                sphere.transform.SetParent(transform);
                sphere.transform.localScale = Vector3.one * (pointRadius * 2f);
                var mr = sphere.GetComponent<MeshRenderer>();
                if (mr != null)
                {
                    if (lineMaterial != null)
                    {
                        var mat = new Material(lineMaterial);
                        mat.color = lineColor;
                        mr.material = mat;
                    }
                    else
                    {
                        mr.material.color = lineColor;
                    }
                }
                pointGizmos.Add(sphere);
            }
            // 设置位置与显示
            for (int i = 0; i < pointGizmos.Count; i++)
            {
                var go = pointGizmos[i];
                if (i < target)
                {
                    go.SetActive(true);
                    go.transform.position = anchors[i];
                    go.transform.localScale = Vector3.one * (pointRadius * 2f);
                }
                else
                {
                    go.SetActive(false);
                }
            }
        }

        private void UpdateTicks()
        {
            if (currentCurve == null || currentCurve.PointCount < 2 || (!showTangents && !showUps))
            {
                if (tangentRenderer != null) tangentRenderer.positionCount = 0;
                if (upRenderer != null) upRenderer.positionCount = 0;
                return;
            }

            int count = currentCurve.PointCount;
            int step = Mathf.Max(1, tickEvery);

            // 切线刻度
            if (showTangents && tangentRenderer != null)
            {
                var positions = new System.Collections.Generic.List<Vector3>();
                for (int i = 0; i < count; i += step)
                {
                    var p = currentCurve.GetPoint(i);
                    Vector3 pos = p.position;
                    Vector3 forward = (p.rotation * Vector3.forward).normalized;
                    positions.Add(pos);
                    positions.Add(pos + forward * tangentTickLen);
                }
                tangentRenderer.positionCount = positions.Count;
                for (int i = 0; i < positions.Count; i++) tangentRenderer.SetPosition(i, positions[i]);
            }
            else if (tangentRenderer != null)
            {
                tangentRenderer.positionCount = 0;
            }

            // Up 刻度
            if (showUps && upRenderer != null)
            {
                var positions = new System.Collections.Generic.List<Vector3>();
                for (int i = 0; i < count; i += step)
                {
                    var p = currentCurve.GetPoint(i);
                    Vector3 pos = p.position;
                    Vector3 up = (p.rotation * Vector3.up).normalized;
                    positions.Add(pos);
                    positions.Add(pos + up * upTickLen);
                }
                upRenderer.positionCount = positions.Count;
                for (int i = 0; i < positions.Count; i++) upRenderer.SetPosition(i, positions[i]);
            }
            else if (upRenderer != null)
            {
                upRenderer.positionCount = 0;
            }
        }

        public void UpdateGhostHead(Vector3? headPosition)
        {
            if (headPosition.HasValue)
            {
                if (ghostHeadGizmo == null)
                {
                    ghostHeadGizmo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    DestroyImmediate(ghostHeadGizmo.GetComponent<Collider>());
                    ghostHeadGizmo.name = "Curve_GhostHead";
                    ghostHeadGizmo.transform.SetParent(transform);
                    ghostHeadGizmo.transform.localScale = Vector3.one * (pointRadius * 2f);
                    var mr = ghostHeadGizmo.GetComponent<MeshRenderer>();
                    if (mr != null)
                    {
                        var mat = new Material(lineMaterial);
                        mat.color = new Color(1f, 0.3f, 0.3f, 0.6f); // 半透明红色
                        mr.material = mat;
                    }
                }
                ghostHeadGizmo.SetActive(true);
                ghostHeadGizmo.transform.position = headPosition.Value;
            }
            else
            {
                if (ghostHeadGizmo != null) ghostHeadGizmo.SetActive(false);
            }
        }
    }
}
