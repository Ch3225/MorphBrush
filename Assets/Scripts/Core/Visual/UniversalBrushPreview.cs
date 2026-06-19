using UnityEngine;
using System.Collections.Generic;
using VRBrush.Core;
using VRBrush.Core.Model;
using VRBrush.Util;
using VRBrush.Core.Visual.Element;

namespace VRBrush.Core
{
    /// <summary>
    /// 通用笔刷预览系统
    /// 支持所有笔刷类型（AdaptiBrush、HelioBrush、HelioBrushExtended）的统一预览
    /// 处理线段、多边形、椭圆等各种笔刷形状的预览显示
    /// </summary>
    public class UniversalBrushPreview
    {
        #region 私有字段
        private Transform parentTransform;
    private GameObject mainPreviewObject;        // 主要预览对象（根据笔刷类型变化）
    private List<GameObject> shapePreviewObjects; // 形状预览对象（用于多边形等）
    private List<GameObject> shapePointObjects; // 形状顶点可视对象（Helio样式）
        private bool showWhileDrawing = true;
        private float ribbonWidth = 0.02f;
        private BrushShape currentShape;             // 当前笔刷形状
        private bool isInitialized = false;

        private float shapePreviewRadius = 0.002f;   // 形状预览边的半径
    
    private Color shapePreviewColor = new Color(0.1f, 0.9f, 0.9f, 0.7f); // 青色
    

    // Helio 样式开关：使用 HelioEdgeVisual/HelioPointVisual，仿照 BrushEditor 的风格
    private bool useHelioVisuals = false;
    private bool showShapePoints = false;
    // 最近一次计算的预览小球（world 半径），用于确保圆柱底面半径 = 球半径 * 5/6
    private float lastPreviewSphereWorldRadius = 0f;
    // 点相对于圆柱体半径的倍数（硬编码为 1.2）
    private const float pointToCylinderMultiplier = 1.2f;
        #endregion

        #region 公共属性
        public bool ShowWhileDrawing
        {
            get => showWhileDrawing;
            set => showWhileDrawing = value;
        }

        public float RibbonWidth
        {
            get => ribbonWidth;
            set
            {
                ribbonWidth = value;
                UpdatePreviewSize();
            }
        }

        public BrushShape CurrentShape
        {
            get => currentShape;
            set
            {
                currentShape = value;
                UpdateShapePreview();
            }
        }
        #endregion

        #region 构造函数
        public UniversalBrushPreview(Transform parent, bool showWhileDrawing = true)
        {
            this.parentTransform = parent;
            this.showWhileDrawing = showWhileDrawing;
            this.shapePreviewObjects = new List<GameObject>();
            this.shapePointObjects = new List<GameObject>();
            Initialize();
        }
        #endregion

        #region 公共方法 - 调色与外部控制
        /// <summary>
        /// 设置方向圆盘的颜色（外部控制器调用）
        /// </summary>
        // SetDiskColor removed (orientation disk removed)

        /// <summary>
        /// 设置形状预览边的颜色（用于保持与旧 Helio 版本一致的外观控制）
        /// </summary>
        public void SetPreviewColor(Color color)
        {
            shapePreviewColor = color;
            foreach (var obj in shapePreviewObjects)
            {
                if (obj == null) continue;
                var renderer = obj.GetComponent<Renderer>();
                if (renderer != null && renderer.material != null)
                {
                    renderer.material.color = shapePreviewColor;
                }
            }
        }

        /// <summary>
        /// 设置形状预览圆柱的半径（粗细），用于整体缩放视觉粗细
        /// </summary>
        public void SetShapePreviewRadius(float radius)
        {
            shapePreviewRadius = Mathf.Max(0.0001f, radius);
            // 尝试立刻应用到已有预览对象
            for (int i = 0; i < shapePreviewObjects.Count; i++)
            {
                var obj = shapePreviewObjects[i];
                if (obj == null) continue;
                var ls = obj.transform.localScale;
                obj.transform.localScale = new Vector3(shapePreviewRadius * 2f, ls.y, shapePreviewRadius * 2f);
            }
        }

        // 方向圆盘功能已从代码中移除。

        /// <summary>
        /// 启用/禁用 Helio 风格的预览（使用 HelioEdgeVisual/HelioPointVisual）
        /// </summary>
        public void SetUseHelioVisuals(bool enabled)
        {
            if (useHelioVisuals != enabled)
            {
                useHelioVisuals = enabled;
                UpdateShapePreview();
            }
        }

        /// <summary>
        /// 是否显示形状的顶点小球（仅在使用形状预览时有效）
        /// </summary>
        public void SetShowShapePoints(bool enabled)
        {
            if (showShapePoints != enabled)
            {
                showShapePoints = enabled;
                UpdateShapePreview();
            }
        }

        /// <summary>
        /// 强制设置预览时使用的小球世界半径（用于在没有点创建时指定圆柱体半径关系）。
        /// 外部控制器（例如 AdaptiBrush）可以调用此方法以在 Helio 风格下传入特定尺寸。
        /// </summary>
        public void SetPreviewSphereWorldRadius(float worldRadius)
        {
            lastPreviewSphereWorldRadius = Mathf.Max(0f, worldRadius);
            // 如果已经存在 shape preview 对象，刷新它们以应用新半径
            UpdateShapePreviewSize();
        }

        /// <summary>
        /// 获取默认的预览球体世界半径
        /// 这个值来自 HelioEdgeVisual 的默认配置
        /// </summary>
        public static float GetDefaultPreviewSphereRadius()
        {
            return VRBrush.Core.Visual.Element.HelioEdgeVisual.DefaultPreviewWorldRadius;
        }
        #endregion

        #region 初始化方法
        private void Initialize()
        {
            if (isInitialized) return;

            CreateMainPreviewObject();
            // 不在初始化时创建方向圆盘，按需显示时再创建（Helio 需要彻底禁用时也不会产生对象）

            isInitialized = true;
            SetAllPreviewsActive(false);
        }

        private void CreateMainPreviewObject()
        {
            mainPreviewObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            mainPreviewObject.name = "UniversalBrushPreview_Main";
            mainPreviewObject.transform.SetParent(parentTransform);
            Object.DestroyImmediate(mainPreviewObject.GetComponent<Collider>());

            var renderer = mainPreviewObject.GetComponent<Renderer>();
            renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            renderer.material.color = new Color(0, 1, 1, 0.7f); // 半透明青色

            UpdateMainPreviewSize();
        }

        // CreateOrientationDisk and RemoveOrientationDiskPermanently removed
        #endregion

        #region 尺寸更新方法
        private void UpdatePreviewSize()
        {
            UpdateMainPreviewSize();
            UpdateShapePreviewSize();
        }

        private void UpdateMainPreviewSize()
        {
            if (mainPreviewObject != null)
            {
                // 使用 Element 层提供的映射函数来计算半径，避免在 Preview/Controller 中硬编码视觉参数
                float radius = useHelioVisuals ? VRBrush.Core.Visual.Element.HelioEdgeVisual.ComputeRadiusFromRibbon(ribbonWidth)
                                                : VRBrush.Core.Visual.Element.EditorEdgeVisual.ComputeRadiusFromRibbon(ribbonWidth);
                float height = Mathf.Max(0.001f, ribbonWidth * 0.3f);
                mainPreviewObject.transform.localScale = new Vector3(radius * 2f, height, radius * 2f);
            }
        }

        // Disk size logic removed

        private void UpdateShapePreviewSize()
        {
            // 形状预览尺寸在UpdateShapePreview中处理
        }
        #endregion

        #region 形状预览更新方法
        private void UpdateShapePreview()
        {
            ClearShapePreview();

            if (currentShape == null)
            {
                mainPreviewObject.SetActive(true);
                return;
            }

            // 优先使用图结构的 edges（graphEdges）来渲染任意图论形状（多段线组合）
            bool useGraphEdges = currentShape.graphEdges != null && currentShape.graphEdges.Count > 0;
            int edgeCount = 0;
            if (useGraphEdges)
            {
                edgeCount = currentShape.graphEdges.Count;
            }
            else if (currentShape.vertices != null && currentShape.vertices.Count >= 2)
            {
                edgeCount = currentShape.vertices.Count;
            }

            if (edgeCount == 0)
            {
                mainPreviewObject.SetActive(true);
                return;
            }

            // 为每条边创建预览对象
            for (int i = 0; i < edgeCount; i++)
            {
                CreateEdgePreview(i);
            }

            // 如需显示顶点，创建点对象（Helio风格）
            if (showShapePoints)
            {
                int nodeCount = useGraphEdges ? (currentShape.graphNodes != null ? currentShape.graphNodes.Count : 0)
                                              : (currentShape.vertices != null ? currentShape.vertices.Count : 0);
                for (int i = 0; i < nodeCount; i++)
                {
                    CreatePointPreview(i);
                }
            }

            // 隐藏主预览对象，显示形状预览
            mainPreviewObject.SetActive(false);
        }

        private void CreateEdgePreview(int index)
        {
            GameObject edgeObj;
            if (useHelioVisuals)
            {
                // 为 HelioEdgeVisual 传入的 baseLineRadius 是组件内部的 "基准半径"，组件会乘以 PreviewScale 得到最终世界半径。
                // 我们希望最终的边世界半径 == 最近一次小球世界半径 * 5/6（如可用），因此需要将其除以 PreviewScale 传入。
                float baseLineRadiusToPass = 0f;
                if (lastPreviewSphereWorldRadius > 0f)
                {
                    float desiredEdgeWorldRadius = lastPreviewSphereWorldRadius * (5f / 6f);
                    baseLineRadiusToPass = desiredEdgeWorldRadius / HelioEdgeVisual.PreviewScale;
                }
                else
                {
                    // 回退：使用默认预览世界半径，转换为组件基准半径
                    baseLineRadiusToPass = HelioEdgeVisual.DefaultPreviewWorldRadius / HelioEdgeVisual.PreviewScale;
                }
                edgeObj = HelioEdgeVisual.Create(parentTransform, $"UniversalBrushPreview_Edge_{index}", baseLineRadiusToPass, HelioEdgeStyle.Preview);
            }
            else
            {
                edgeObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                edgeObj.name = $"UniversalBrushPreview_Edge_{index}";
                edgeObj.transform.SetParent(parentTransform);
                Object.DestroyImmediate(edgeObj.GetComponent<Collider>());

                var renderer = edgeObj.GetComponent<Renderer>();
                renderer.material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                renderer.material.color = shapePreviewColor;

                // 初始本地化，长度稍后计算
                edgeObj.transform.localPosition = Vector3.zero;
                edgeObj.transform.localRotation = Quaternion.identity;
                // 优先使用最近一次计算出来的小球世界半径作为基准，使圆柱底面半径 = 球半径 * 5/6
                float previewRadius = 0f;
                if (lastPreviewSphereWorldRadius > 0f)
                {
                    previewRadius = lastPreviewSphereWorldRadius * (5f / 6f);
                }
                else
                {
                    previewRadius = VRBrush.Core.Visual.Element.EditorEdgeVisual.DefaultPreviewWorldRadius;
                }
                edgeObj.transform.localScale = new Vector3(previewRadius * 2f, 0.0005f, previewRadius * 2f);
            }

            shapePreviewObjects.Add(edgeObj);
        }

        private void CreatePointPreview(int index)
        {
            if (!useHelioVisuals) return; // 仅 Helio 风格需要点
            // 点的世界半径不随笔刷宽度变化，基于 Element 层的默认预览半径计算
            float basePreviewRadius = VRBrush.Core.Visual.Element.HelioEdgeVisual.DefaultPreviewWorldRadius;
            float desiredPointWorldRadius = basePreviewRadius * pointToCylinderMultiplier;
            // 记录最近一次的点（小球）世界半径，用于计算圆柱体半径（球 * 5/6）
            lastPreviewSphereWorldRadius = desiredPointWorldRadius;
            float baseRadiusToPass = desiredPointWorldRadius / VRBrush.Core.Visual.Element.HelioPointVisual.PreviewScale;

            var pointObj = VRBrush.Core.Visual.Element.HelioPointVisual.Create(parentTransform, $"UniversalBrushPreview_Point_{index}", baseRadiusToPass, VRBrush.Core.Visual.Element.HelioPointStyle.Preview);
            // 初始位置为原点，稍后更新到实际位置
            pointObj.transform.localPosition = Vector3.zero;
            pointObj.transform.localRotation = Quaternion.identity;
            shapePointObjects.Add(pointObj);
        }

        private void ClearShapePreview()
        {
            foreach (var obj in shapePreviewObjects)
            {
                if (obj != null) Object.DestroyImmediate(obj);
            }
            shapePreviewObjects.Clear();

            foreach (var p in shapePointObjects)
            {
                if (p != null) Object.DestroyImmediate(p);
            }
            shapePointObjects.Clear();
        }
        #endregion

        #region 主要更新方法
        /// <summary>
        /// 更新笔刷预览的主要方法
        /// </summary>
        /// <param name="position">笔刷位置</param>
        /// <param name="controllerRotation">控制器旋转</param>
        /// <param name="brushRotation">笔刷旋转（由算法计算）</param>
        /// <param name="isDrawing">是否正在绘制</param>
        /// <param name="previewRibbonWidth">用于当前帧预览的 ribbon 宽度（支持按压动态调整）</param>
        public void UpdatePreview(Vector3 position, Quaternion controllerRotation, Quaternion brushRotation, bool isDrawing, float previewRibbonWidth)
        {
            if (!isInitialized) return;

            bool shouldShowMainPreview = (showWhileDrawing || !isDrawing);

            // 更新主预览对象（圆柱体或形状预览）
            if (shouldShowMainPreview)
            {
                Quaternion targetRotation = isDrawing ? brushRotation : controllerRotation;
                UpdateMainPreview(position, targetRotation, previewRibbonWidth);
            }
            else
            {
                SetMainPreviewActive(false);
            }

            // 方向圆盘功能已移除
        }

        // 兼容旧接口：缺省使用当前 RibbonWidth
        public void UpdatePreview(Vector3 position, Quaternion controllerRotation, Quaternion brushRotation, bool isDrawing)
        {
            UpdatePreview(position, controllerRotation, brushRotation, isDrawing, RibbonWidth);
        }

        private void UpdateMainPreview(Vector3 position, Quaternion rotation, float previewRibbonWidth)
        {
            if ((currentShape != null && currentShape.vertices != null && currentShape.vertices.Count >= 2) ||
                (currentShape != null && currentShape.graphEdges != null && currentShape.graphEdges.Count > 0 && currentShape.graphNodes != null && currentShape.graphNodes.Count > 0))
            {
                // 更新形状预览
                UpdateShapePreviewTransforms(position, rotation, previewRibbonWidth);
                SetShapePreviewActive(true);
                SetMainPreviewActive(false);
            }
            else
            {
                // 更新默认圆柱体预览（厚度固定，不随笔刷宽度变化）
                mainPreviewObject.transform.position = position;
                mainPreviewObject.transform.rotation = rotation;
                float radius = 0f;
                if (lastPreviewSphereWorldRadius > 0f)
                {
                    radius = lastPreviewSphereWorldRadius * (5f / 6f);
                }
                else
                {
                    radius = useHelioVisuals ? VRBrush.Core.Visual.Element.HelioEdgeVisual.DefaultPreviewWorldRadius
                                              : VRBrush.Core.Visual.Element.EditorEdgeVisual.DefaultPreviewWorldRadius;
                }
                // 高度也固定到半径量级，形成小“标记”
                float height = Mathf.Max(0.001f, radius * 1.2f);
                mainPreviewObject.transform.localScale = new Vector3(radius * 2f, height, radius * 2f);
                SetMainPreviewActive(true);
                SetShapePreviewActive(false);
            }
        }

        private void UpdateShapePreviewTransforms(Vector3 position, Quaternion rotation, float previewRibbonWidth)
        {
            if (shapePreviewObjects.Count == 0 || currentShape == null) return;
            // 支持两种模式：
            // - 如果 graphEdges 存在，按 edges 列表渲染每一条边作为独立线段
            // - 否则按 vertices 顺序渲染（原来的行为，vertices[i] -> vertices[(i+1)%n]）
            bool useGraphEdges = currentShape.graphEdges != null && currentShape.graphEdges.Count > 0;
            int edgeCount = useGraphEdges ? currentShape.graphEdges.Count : currentShape.vertices.Count;
            if (edgeCount < 1) return;
            if (shapePreviewObjects.Count != edgeCount)
            {
                UpdateShapePreview();
                if (shapePreviewObjects.Count != edgeCount) return;
            }

            if (useGraphEdges)
            {
                // 使用 graphNodes/graphEdges
                for (int i = 0; i < edgeCount; i++)
                {
                    var e = currentShape.graphEdges[i];
                    if (e.x < 0 || e.y < 0 || e.x >= currentShape.graphNodes.Count || e.y >= currentShape.graphNodes.Count)
                    {
                        shapePreviewObjects[i].SetActive(false);
                        continue;
                    }
                    Vector2 v0 = currentShape.graphNodes[e.x];
                    Vector2 v1 = currentShape.graphNodes[e.y];
                    Vector3 wp0 = VRBrush.Util.PreviewGeometry.Map2DToWorld(position, rotation, v0, previewRibbonWidth);
                    Vector3 wp1 = VRBrush.Util.PreviewGeometry.Map2DToWorld(position, rotation, v1, previewRibbonWidth);
                    var edgeObj = shapePreviewObjects[i];
                    if (useHelioVisuals)
                    {
                        var comp = edgeObj.GetComponent<HelioEdgeVisualComponent>();
                        if (comp != null)
                        {
                            Vector3 lp0 = parentTransform.InverseTransformPoint(wp0);
                            Vector3 lp1 = parentTransform.InverseTransformPoint(wp1);
                            comp.UpdateBetweenLocalPoints(lp0, lp1);
                            edgeObj.SetActive(true);
                        }
                        else edgeObj.SetActive(false);
                    }
                    else
                    {
                        // 边的可视圆柱半径固定（不随笔刷宽度变化）
                        float edgeRadius = 0f;
                        if (lastPreviewSphereWorldRadius > 0f)
                        {
                            edgeRadius = lastPreviewSphereWorldRadius * (5f / 6f);
                        }
                        else
                        {
                            edgeRadius = VRBrush.Core.Visual.Element.EditorEdgeVisual.DefaultPreviewWorldRadius;
                        }
                        if (!VRBrush.Util.PreviewGeometry.ComputeCylinderBetween(wp0, wp1, edgeRadius,
                            out var mid, out var rot, out var scale)) { edgeObj.SetActive(false); continue; }
                        edgeObj.SetActive(true);
                        edgeObj.transform.position = mid;
                        edgeObj.transform.rotation = rot;
                        edgeObj.transform.localScale = scale;
                    }
                }

                // 更新点位置
                if (showShapePoints && useHelioVisuals && shapePointObjects.Count == currentShape.graphNodes.Count)
                {
                    for (int i = 0; i < currentShape.graphNodes.Count; i++)
                    {
                        Vector2 v = currentShape.graphNodes[i];
                        Vector3 wp = VRBrush.Util.PreviewGeometry.Map2DToWorld(position, rotation, v, previewRibbonWidth);
                        var p = shapePointObjects[i];
                        if (p != null)
                        {
                            p.transform.localPosition = parentTransform.InverseTransformPoint(wp);
                        }
                    }
                }
            }
            else
            {
                for (int i = 0; i < edgeCount; i++)
                {
                    int next = (i + 1) % edgeCount;
                    Vector2 v0 = currentShape.vertices[i];
                    Vector2 v1 = currentShape.vertices[next];
                    // Map vertex coordinates using standard coordinate transformation
                    Vector3 wp0 = VRBrush.Util.PreviewGeometry.Map2DToWorld(position, rotation, v0, previewRibbonWidth);
                    Vector3 wp1 = VRBrush.Util.PreviewGeometry.Map2DToWorld(position, rotation, v1, previewRibbonWidth);
                    var edgeObj = shapePreviewObjects[i];
                    if (useHelioVisuals)
                    {
                        var comp = edgeObj.GetComponent<HelioEdgeVisualComponent>();
                        if (comp != null)
                        {
                            Vector3 lp0 = parentTransform.InverseTransformPoint(wp0);
                            Vector3 lp1 = parentTransform.InverseTransformPoint(wp1);
                            comp.UpdateBetweenLocalPoints(lp0, lp1);
                            edgeObj.SetActive(true);
                        }
                        else edgeObj.SetActive(false);
                    }
                    else
                    {
                            float edgeRadius = 0f;
                            if (lastPreviewSphereWorldRadius > 0f)
                            {
                                edgeRadius = lastPreviewSphereWorldRadius * (5f / 6f);
                            }
                            else
                            {
                                edgeRadius = VRBrush.Core.Visual.Element.EditorEdgeVisual.DefaultPreviewWorldRadius;
                            }
                        if (!VRBrush.Util.PreviewGeometry.ComputeCylinderBetween(wp0, wp1, edgeRadius,
                            out var mid, out var rot, out var scale)) { edgeObj.SetActive(false); continue; }
                        edgeObj.SetActive(true);
                        edgeObj.transform.position = mid;
                        edgeObj.transform.rotation = rot;
                        edgeObj.transform.localScale = scale;
                    }
                }

                // 更新点位置（按顶点列表）
                if (showShapePoints && useHelioVisuals && shapePointObjects.Count == currentShape.vertices.Count)
                {
                    for (int i = 0; i < currentShape.vertices.Count; i++)
                    {
                        Vector2 v = currentShape.vertices[i];
                        Vector3 wp = VRBrush.Util.PreviewGeometry.Map2DToWorld(position, rotation, v, previewRibbonWidth);
                        var p = shapePointObjects[i];
                        if (p != null)
                        {
                            p.transform.localPosition = parentTransform.InverseTransformPoint(wp);
                        }
                    }
                }
            }
        }

        // Orientation disk removed
        #endregion

        #region 辅助方法
        private void SetAllPreviewsActive(bool active)
        {
            SetMainPreviewActive(active);
            SetShapePreviewActive(active);
        }

        private void SetMainPreviewActive(bool active)
        {
            if (mainPreviewObject != null)
            {
                mainPreviewObject.SetActive(active);
            }
        }

        private void SetOrientationDiskActive(bool active)
        {
            // orientation disk removed
        }

        private void SetShapePreviewActive(bool active)
        {
            foreach (var obj in shapePreviewObjects)
            {
                if (obj != null) obj.SetActive(active);
            }
        }

        /// <summary>
        /// 应用摇杆输入来调整笔刷朝向
        /// </summary>
        /// <param name="joystickInput">摇杆输入向量（-1到1）</param>
        /// <param name="currentRotation">当前旋转</param>
        /// <returns>调整后的旋转</returns>
        public Quaternion ApplyJoystickAdjustment(Vector2 joystickInput, Quaternion currentRotation)
        {
            // 使用累积摇杆输入来旋转，但保持中心位置不变（只返回旋转）
            if (joystickInput.sqrMagnitude < 0.0001f) return currentRotation;

            // 将摇杆输入映射为在圆盘平面内的偏航和俯仰（度）
            float yawDeg = joystickInput.x * 90f; // 左右
            float pitchDeg = joystickInput.y * 90f; // 上下

            // 生成局部旋转：先绕局部右轴做pitch，再绕全局up做yaw，以保证不改变中心位置
            Vector3 localRight = currentRotation * Vector3.right;
            Quaternion pitch = Quaternion.AngleAxis(pitchDeg, localRight);
            Quaternion yaw = Quaternion.AngleAxis(yawDeg, Vector3.up);

            // 合成旋转（先 pitch 后 yaw 保持直觉）
            return yaw * pitch * currentRotation;
        }
        #endregion

        #region 清理方法
        /// <summary>
        /// Hide all preview visuals without destroying them
        /// </summary>
        public void Hide()
        {
            if (mainPreviewObject != null)
            {
                mainPreviewObject.SetActive(false);
            }

            // Hide all shape preview objects
            foreach (var obj in shapePreviewObjects)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                }
            }

            // Hide all shape point objects
            foreach (var obj in shapePointObjects)
            {
                if (obj != null)
                {
                    obj.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Show all preview visuals
        /// </summary>
        public void Show()
        {
            if (mainPreviewObject != null)
            {
                mainPreviewObject.SetActive(true);
            }

            // Show all shape preview objects
            foreach (var obj in shapePreviewObjects)
            {
                if (obj != null)
                {
                    obj.SetActive(true);
                }
            }

            // Show all shape point objects (only if enabled)
            if (showShapePoints)
            {
                foreach (var obj in shapePointObjects)
                {
                    if (obj != null)
                    {
                        obj.SetActive(true);
                    }
                }
            }
        }

        public void Destroy()
        {
            if (mainPreviewObject != null)
            {
                Object.DestroyImmediate(mainPreviewObject);
                mainPreviewObject = null;
            }

            ClearShapePreview();
        }
        #endregion
    }
}
