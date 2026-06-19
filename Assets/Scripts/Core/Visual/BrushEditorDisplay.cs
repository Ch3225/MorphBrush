using System.Collections.Generic;
using UnityEngine;
using VRBrush.Core.Model;
using VRBrush.Core.Visual.Element;
using VRBrush.Util;

namespace VRBrush.Core.Visual
{
    /// <summary>
    /// BrushEditor 的完整显示界面控制器
    /// 重新实现原始BrushEditor的核心视觉功能：坐标系跟随、右手投影、动态圆盘等
    /// </summary>
    public partial class BrushEditorDisplay : MonoBehaviour
    {

    // 核心组件引用
    private BrushShapeBuilder shapeBuilder;
        
        // 坐标系组件
        private GameObject coordinateSystem;
        private GameObject editPlane; // 圆盘编辑平面
        private GameObject radialLine; // 动态圆圈
        private GameObject angularLine; // 射线
        private GameObject projectedPointIndicator; // 投影点指示器
        private GameObject tempPreviewEdge; // 预览连线
        private GameObject centerIndicator; // 中心指示器
        
        // 显示的点和边 - 现在由BrushShapeBuilder本身管理
        private int? highlightedIndex = null;

        // 位置和姿态控制
        private Transform leftController;
        private Vector3 smoothedLeftControllerPosition;
        private Quaternion smoothedLeftControllerRotation;
        private float smoothingFactor = 10f; // 从控制器传入的平滑因子
    private bool hasSmoothInit = false;
    private bool isVisible = false;
        
        // 编辑平面 - 现在配置在AxisVisual中
        private Plane editingPlane;

        #region 初始化和配置

        /// <summary>
        /// 初始化显示系统
        /// </summary>
        /// <param name="builder">关联的 BrushShapeBuilder</param>
        /// <param name="leftControllerTransform">左手控制器 Transform</param>
        public void Initialize(BrushShapeBuilder builder, Transform leftControllerTransform)
        {
            Debug.Log($"BrushEditorDisplay.Initialize: leftController={leftControllerTransform?.name}, builder={(builder != null ? "ok" : "null")}");
            // 记录当前可见性，避免重复初始化时被强制隐藏
            bool wasVisible = isVisible || (coordinateSystem != null && coordinateSystem.activeSelf);

            // 解除之前的订阅，避免重复
            if (shapeBuilder != null)
            {
                shapeBuilder.OnNodeCreated -= OnNodeCreated;
                shapeBuilder.OnEdgeCreated -= OnEdgeCreated;
                shapeBuilder.OnNodeRemoved -= OnNodeRemoved;
            }

            shapeBuilder = builder;
            leftController = leftControllerTransform;
            
            // 重要：重置平滑初始化状态，确保坐标系能立即跟随新的控制器位置
            hasSmoothInit = false;

            // 第一次初始化时创建坐标系；再次初始化仅更新引用
            if (coordinateSystem == null)
            {
                CreateCoordinateSystem();
            }
            // 材质现在由Element类自动管理，无需手动创建

            // 订阅 BrushShapeBuilder 事件
            if (shapeBuilder != null)
            {
                shapeBuilder.OnNodeCreated += OnNodeCreated;
                shapeBuilder.OnEdgeCreated += OnEdgeCreated;
                shapeBuilder.OnNodeRemoved += OnNodeRemoved;
            }

            // 恢复可见性
            SetVisible(wasVisible);
        }

        /// <summary>
        /// Apply controller-provided parameters (called by BrushEditorController)
        /// 现在配置通过Element类预设提供，不再需要外部参数
        /// </summary>
        public void ApplyControllerSettings(float coordSize, float ptRadius, float lnRadius, float snapThreshold, float connectThreshold, float removeThreshold, bool addCenterIndicator, bool verbose, float smoothingFactor)
        {
            // 配置现在在Element类中预设，无需外部设置
            // 但是保存smoothingFactor以备将来使用
            this.smoothingFactor = smoothingFactor;
        }

        /// <summary>
        /// Set smoothing factor for controller following
        /// </summary>
        public void SetSmoothingFactor(float factor)
        {
            smoothingFactor = factor;
        }

        #endregion

        #region 坐标系创建

        private void CreateCoordinateSystem()
        {
            // 不能直接创建GameObject，必须使用Element类
            // 但coordinateSystem是容器，需要特殊处理
            coordinateSystem = new GameObject("BrushEditorCoordinateSystem");
            coordinateSystem.transform.SetParent(transform);

            // 使用DiskVisual创建编辑平面（通过AxisVisual.CreateEditingPlane调用）
            editPlane = AxisVisual.CreateEditingPlane(coordinateSystem.transform);
            
            // 使用CircleVisual创建极坐标系径向等值线（动态圆圈）
            radialLine = CircleVisual.CreatePolarRadialLine(coordinateSystem.transform, "RadialLine", 
                AxisVisual.CoordinateSystemSize, AxisVisual.GeometryLift);

            // 使用AxisVisual创建极坐标系角度等值线（射线）
            angularLine = AxisVisual.CreatePolarAngularLine(coordinateSystem.transform, "AngularLine", 
                AxisVisual.CoordinateSystemSize);

            // 不强制隐藏，由 SetVisible 控制
            coordinateSystem.SetActive(isVisible);

            // 使用EditorPointVisual创建中心指示
            centerIndicator = EditorPointVisual.Create(coordinateSystem.transform, "CenterIndicator", 
                EditorPointVisual.BasePointRadius, PointStyle.Default);
            centerIndicator.transform.localScale = Vector3.one * EditorPointVisual.BasePointRadius * 1.5f;

            // 使用EditorPointVisual创建右手投影点小球（初始隐藏，使用预览样式）
            projectedPointIndicator = EditorPointVisual.Create(coordinateSystem.transform, "ProjectedPoint", 
                EditorPointVisual.BasePointRadius, PointStyle.Preview);
            projectedPointIndicator.SetActive(false);
        }

        // CreateCircle和CreateLine方法已移至AxisVisual类

        // CreateTransparentMaterials方法已移至各Element类中自动处理

        #endregion

        #region 位置和可见性控制

        /// <summary>
        /// 更新坐标系跟随左手控制器（重现原始BrushEditor功能）
        /// </summary>
        public void UpdateCoordinateSystem()
        {
            if (coordinateSystem == null || leftController == null) {
                // Too noisy for update loop, but uncomment if needed for debugging
                // Debug.LogWarning($"BrushEditorDisplay.UpdateCoordinateSystem aborted: coordSys={coordinateSystem}, leftCtrl={leftController}");
                return;
            }

            if (!hasSmoothInit)
            {
                smoothedLeftControllerPosition = leftController.position;
                smoothedLeftControllerRotation = leftController.rotation;
                hasSmoothInit = true;
            }
            else
            {
                smoothedLeftControllerPosition = Vector3.Lerp(smoothedLeftControllerPosition, leftController.position, smoothingFactor * Time.deltaTime);
                if (smoothedLeftControllerRotation.w == 0f) smoothedLeftControllerRotation = leftController.rotation;
                smoothedLeftControllerRotation = Quaternion.Slerp(smoothedLeftControllerRotation, leftController.rotation, smoothingFactor * Time.deltaTime);
            }
            coordinateSystem.transform.position = smoothedLeftControllerPosition;
            coordinateSystem.transform.rotation = smoothedLeftControllerRotation;
            // 平面法线使用坐标系的 up，与圆盘朝向一致
            editingPlane = new Plane(coordinateSystem.transform.up, coordinateSystem.transform.position);

            // 更新所有点/边的可视化位置（坐标系移动后需要重算世界坐标）
            UpdateAllVisualsPositions();
        }

        /// <summary>
        /// 更新右手控制器投影几何（重现原始BrushEditor功能）
        /// </summary>
        public void UpdateProjectionGeometry(Transform rightController, BrushEditorPoint lastPoint)
        {
            if (!isVisible || coordinateSystem == null || rightController == null) return;
            if (projectedPointIndicator == null || radialLine == null || angularLine == null) return;
            if (!coordinateSystem.activeSelf) return;

            // 1. 控制器"尖端"位置
            Vector3 tipWorld = rightController.position;
            
            // 2. 投影到编辑平面
            // 垂直投影：使用与 Universal/现有实现一致的逻辑封装
            Vector3 worldHit = VRBrush.Util.PreviewGeometry.ProjectPointOntoPlane(
                tipWorld, coordinateSystem.transform.position, coordinateSystem.transform.up);
            Vector3 localHit = coordinateSystem.transform.InverseTransformPoint(worldHit);
            localHit.y = 0f; // 平面局部 y=0

            // 3. 投影小球放在投影点
            projectedPointIndicator.SetActive(true);
            localHit.y = AxisVisual.GeometryLift;
            projectedPointIndicator.transform.localPosition = localHit;

            // 4. 更新角度等值线（射线：原点 -> 投影点）
            AxisVisual.UpdatePolarAngularLine(angularLine, localHit);

            // 5. 更新径向等值线（圆心=平面中心(原点)，半径=原点到投影点距离）
            float radius = new Vector2(localHit.x, localHit.z).magnitude;
            if (radius < 0.0001f) radius = 0.0001f;
            CircleVisual.UpdatePolarRadialLine(radialLine, radius, AxisVisual.GeometryLift);

            // 6. 预览连线：优先使用传入的 lastPoint；如为空则尝试使用构建器的 previousNode
            BrushEditorPoint lp = lastPoint;
            if (lp == null && shapeBuilder != null && shapeBuilder.PreviousNodeIndex.HasValue)
            {
                lp = GetPointByIndex(shapeBuilder.PreviousNodeIndex.Value);
            }
            UpdatePreviewLine(lp, worldHit);
        }

        private void UpdatePreviewLine(BrushEditorPoint lastPoint, Vector3 worldHit)
        {
            // 如果没有 lastPoint 则确保临时连线隐藏并早退
            if (lastPoint == null)
            {
                if (tempPreviewEdge != null) tempPreviewEdge.SetActive(false);
                return;
            }

            // 有 lastPoint 时显示临时连线
            Vector3 a = lastPoint.worldPosition;
            Vector3 b = worldHit;
            float dist = Vector3.Distance(a, b);
            if (dist > 0.0005f)
            {
                if (tempPreviewEdge == null)
                {
                    // 使用EditorEdgeVisual创建预览连线
                    tempPreviewEdge = EditorEdgeVisual.Create(coordinateSystem.transform, "TempPreviewEdge", 
                        EditorEdgeVisual.BaseLineRadius, EdgeStyle.Preview);
                }

                Vector3 localA = coordinateSystem.transform.InverseTransformPoint(a);
                Vector3 localB = coordinateSystem.transform.InverseTransformPoint(b);
                
                tempPreviewEdge.SetActive(true);
                var edgeComponent = tempPreviewEdge.GetComponent<EditorEdgeVisualComponent>();
                if (edgeComponent != null)
                {
                    edgeComponent.UpdateBetweenLocalPoints(localA, localB);
                }
            }
            else
            {
                if (tempPreviewEdge != null) tempPreviewEdge.SetActive(false);
            }
        }

        /// <summary>
        /// 设置显示可见性
        /// </summary>
        public void SetVisible(bool visible)
        {
            isVisible = visible;
            if (coordinateSystem != null)
            {
                coordinateSystem.SetActive(visible);
            }
        }

        /// <summary>
        /// 为加载新形状做准备：彻底清理现有坐标系与临时可视元素，防止残留叠加。
        /// Controller 在调用 Initialize(builder, left) 之前应先调用本方法。
        /// </summary>
        public void ResetForNewShape()
        {
            // 解除旧订阅
            if (shapeBuilder != null)
            {
                shapeBuilder.OnNodeCreated -= OnNodeCreated;
                shapeBuilder.OnEdgeCreated -= OnEdgeCreated;
                shapeBuilder.OnNodeRemoved -= OnNodeRemoved;
            }

            // 销毁整套坐标系容器（其下子物体包括点/边/圆盘/指示器等会一并清理）
            if (coordinateSystem != null)
            {
                Destroy(coordinateSystem);
            }

            // 清空引用，等待 Initialize 重新创建
            coordinateSystem = null;
            editPlane = null;
            radialLine = null;
            angularLine = null;
            projectedPointIndicator = null;
            tempPreviewEdge = null;
            centerIndicator = null;
            highlightedIndex = null;
            hasSmoothInit = false;
        }

        #endregion

        #region 事件处理

        private void OnNodeCreated(int nodeIndex)
        {
            if (shapeBuilder == null || CoordinateTransform == null) return;

            // 计算世界坐标
            Vector2 local = shapeBuilder.GetNode(nodeIndex);
            Vector3 world = BrushShapeBuilder.LocalToWorldPosition(local, CoordinateTransform);

            // 抬升到geometryLift高度以与投影点保持一致
            Vector3 localPos = CoordinateTransform.InverseTransformPoint(world);
            localPos.y = AxisVisual.GeometryLift;
            world = CoordinateTransform.TransformPoint(localPos);

            // 使用Element类创建点 - 现在直接创建GameObject，BrushShapeBuilder管理引用
            var pointVisual = EditorPointVisual.Create(coordinateSystem.transform, $"Point_{nodeIndex}", 
                EditorPointVisual.BasePointRadius, PointStyle.Default);
            pointVisual.transform.position = world;

            Debug.Log($"BrushEditorDisplay: Created point {nodeIndex} at world position {world} using Element class");
        }

        private void OnEdgeCreated(Vector2Int edgeIndices)
        {
            if (shapeBuilder == null || CoordinateTransform == null) return;
            int a = edgeIndices.x;
            int b = edgeIndices.y;
            if (a < 0 || b < 0) return;

            // 确保点存在
            if (a >= shapeBuilder.NodeCount || b >= shapeBuilder.NodeCount) return;

            // 获取点的世界坐标
            Vector2 localPosA = shapeBuilder.GetNode(a);
            Vector2 localPosB = shapeBuilder.GetNode(b);
            Vector3 worldA = BrushShapeBuilder.LocalToWorldPosition(localPosA, CoordinateTransform);
            Vector3 worldB = BrushShapeBuilder.LocalToWorldPosition(localPosB, CoordinateTransform);

            // 抬升坐标
            Vector3 localA = CoordinateTransform.InverseTransformPoint(worldA);
            Vector3 localB = CoordinateTransform.InverseTransformPoint(worldB);
            localA.y = AxisVisual.GeometryLift;
            localB.y = AxisVisual.GeometryLift;

            // 使用Element类创建边
            var edgeVisual = EditorEdgeVisual.Create(coordinateSystem.transform, $"Edge_{a}_{b}", 
                EditorEdgeVisual.BaseLineRadius, EdgeStyle.Default);
            var edgeComponent = edgeVisual.GetComponent<EditorEdgeVisualComponent>();
            if (edgeComponent != null)
            {
                edgeComponent.UpdateBetweenLocalPoints(localA, localB);
            }

            Debug.Log($"BrushEditorDisplay: Created edge between points {edgeIndices.x} and {edgeIndices.y} using Element class");
        }

        private void OnNodeRemoved(int nodeIndex)
        {
            // 查找并删除对应的点视觉对象
            var pointVisual = coordinateSystem.transform.Find($"Point_{nodeIndex}");
            if (pointVisual != null)
            {
                Destroy(pointVisual.gameObject);
            }
            
            // 查找并删除相关的边视觉对象
            var edgesToRemove = new List<Transform>();
            foreach (Transform child in coordinateSystem.transform)
            {
                if (child.name.StartsWith("Edge_") && 
                    (child.name.Contains($"_{nodeIndex}_") || child.name.EndsWith($"_{nodeIndex}")))
                {
                    edgesToRemove.Add(child);
                }
            }
            
            foreach (var edge in edgesToRemove)
            {
                Destroy(edge.gameObject);
            }
        }

        public void SetHighlightedNode(int? index)
        {
            if (highlightedIndex == index) return;

            // 清除旧高亮
            if (highlightedIndex.HasValue)
            {
                int old = highlightedIndex.Value;
                var oldPointVisual = coordinateSystem.transform.Find($"Point_{old}");
                if (oldPointVisual != null)
                {
                    var component = oldPointVisual.GetComponent<EditorPointVisualComponent>();
                    component?.SetStyle(PointStyle.Default);
                }
            }

            highlightedIndex = index;

            // 应用新高亮
            if (highlightedIndex.HasValue)
            {
                int i = highlightedIndex.Value;
                var pointVisual = coordinateSystem.transform.Find($"Point_{i}");
                if (pointVisual != null)
                {
                    var component = pointVisual.GetComponent<EditorPointVisualComponent>();
                    component?.SetStyle(PointStyle.Highlight);
                }
            }
        }

        private void UpdateAllVisualsPositions()
        {
            if (shapeBuilder == null || CoordinateTransform == null) return;

            // 更新点位置
            for (int i = 0; i < shapeBuilder.NodeCount; i++)
            {
                Vector2 localCoord = shapeBuilder.GetNode(i);
                Vector3 world = BrushShapeBuilder.LocalToWorldPosition(localCoord, CoordinateTransform);
                
                // 抬升到geometryLift高度以与投影点保持一致
                Vector3 localPos = CoordinateTransform.InverseTransformPoint(world);
                localPos.y = AxisVisual.GeometryLift;
                world = CoordinateTransform.TransformPoint(localPos);
                
                // 查找或创建点视觉对象
                var pointVisual = coordinateSystem.transform.Find($"Point_{i}");
                if (pointVisual == null)
                {
                    // 延迟创建
                    OnNodeCreated(i);
                }
                else
                {
                    // 更新位置
                    pointVisual.transform.position = world;
                }
            }

            // 更新边位置 - 通过场景查找
            foreach (Transform child in coordinateSystem.transform)
            {
                if (child.name.StartsWith("Edge_"))
                {
                    var parts = child.name.Split('_');
                    if (parts.Length == 3 && int.TryParse(parts[1], out int indexA) && int.TryParse(parts[2], out int indexB))
                    {
                        if (indexA >= 0 && indexA < shapeBuilder.NodeCount && indexB >= 0 && indexB < shapeBuilder.NodeCount)
                        {
                            // 获取最新的点位置
                            Vector2 localPosA = shapeBuilder.GetNode(indexA);
                            Vector2 localPosB = shapeBuilder.GetNode(indexB);
                            Vector3 worldA = BrushShapeBuilder.LocalToWorldPosition(localPosA, CoordinateTransform);
                            Vector3 worldB = BrushShapeBuilder.LocalToWorldPosition(localPosB, CoordinateTransform);

                            // 转换为局部坐标并抬升
                            Vector3 localA = CoordinateTransform.InverseTransformPoint(worldA);
                            Vector3 localB = CoordinateTransform.InverseTransformPoint(worldB);
                            localA.y = AxisVisual.GeometryLift;
                            localB.y = AxisVisual.GeometryLift;

                            // 更新边视觉位置
                            var edgeComponent = child.GetComponent<EditorEdgeVisualComponent>();
                            if (edgeComponent != null)
                            {
                                edgeComponent.UpdateBetweenLocalPoints(localA, localB);
                            }
                        }
                    }
                }
            }
        }

        #endregion
    }

    #region 数据结构

    /// <summary>
    /// 可视化点的数据结构
    /// </summary>
    public class BrushEditorPoint
    {
        public Vector2 localPosition;
        public Vector3 worldPosition;
        public GameObject visual;
        private EditorPointVisualComponent visualComponent;

        public BrushEditorPoint(Vector2 local, Vector3 world, Transform parent, float pointRadius, int nodeIndex)
        {
            localPosition = local;
            worldPosition = world;
            
            // 使用Element类创建视觉对象
            visual = EditorPointVisual.Create(parent, $"Point_{nodeIndex}", pointRadius, PointStyle.Default);
            visualComponent = visual.GetComponent<EditorPointVisualComponent>();
            visual.transform.position = world;
        }

        public void SetHighlighted(bool highlighted)
        {
            if (visualComponent != null)
            {
                visualComponent.SetStyle(highlighted ? PointStyle.Highlight : PointStyle.Default);
            }
        }

        public void UpdateWorldPosition(Vector3 newWorldPosition)
        {
            worldPosition = newWorldPosition;
            if (visual != null)
            {
                visual.transform.position = newWorldPosition;
            }
        }
    }

    /// <summary>
    /// 可视化边的数据结构
    /// </summary>
    public class BrushEditorEdge
    {
        public BrushEditorPoint pointA;
        public BrushEditorPoint pointB;
        public GameObject visual;
        public Vector2Int indices;
        private EditorEdgeVisualComponent visualComponent;

        public BrushEditorEdge(BrushEditorPoint a, BrushEditorPoint b, Vector2Int edgeIndices, Transform parent, float lineRadius)
        {
            pointA = a;
            pointB = b;
            indices = edgeIndices;
            
            // 使用Element类创建视觉对象
            visual = EditorEdgeVisual.Create(parent, $"Edge_{edgeIndices.x}_{edgeIndices.y}", lineRadius, EdgeStyle.Default);
            visualComponent = visual.GetComponent<EditorEdgeVisualComponent>();
            
            // 初始位置更新
            UpdateVisualPosition();
        }

        public void SetPreviewStyle(bool isPreview)
        {
            if (visualComponent != null)
            {
                visualComponent.SetStyle(isPreview ? EdgeStyle.Preview : EdgeStyle.Default);
            }
        }

        public void UpdateVisualPosition()
        {
            if (visualComponent != null && pointA != null && pointB != null)
            {
                Vector3 localA = visual.transform.parent.InverseTransformPoint(pointA.worldPosition);
                Vector3 localB = visual.transform.parent.InverseTransformPoint(pointB.worldPosition);
                visualComponent.UpdateBetweenLocalPoints(localA, localB);
            }
        }
    }

    #endregion

    public partial class BrushEditorDisplay
    {
        /// <summary>
        /// Hide all editor visuals (coordinate system, disc, points, edges)
        /// </summary>
        public void Hide()
        {
            isVisible = false;
            if (coordinateSystem != null)
            {
                coordinateSystem.SetActive(false);
            }
        }

        /// <summary>
        /// Show all editor visuals
        /// </summary>
        public void Show()
        {
            isVisible = true;
            if (coordinateSystem != null)
            {
                coordinateSystem.SetActive(true);
            }
        }

        /// <summary>
        /// 提供坐标系 Transform 给外部（Controller）进行坐标换算
        /// </summary>
        public Transform CoordinateTransform => coordinateSystem != null ? coordinateSystem.transform : null;

        /// <summary>
        /// 通过索引获取可视化点（如果存在）
        /// 现在直接从场景中查找，因为visualPoints集合已被移除
        /// </summary>
        public BrushEditorPoint GetPointByIndex(int index)
        {
            if (index < 0 || shapeBuilder == null || index >= shapeBuilder.NodeCount) return null;
            
            var pointVisual = coordinateSystem.transform.Find($"Point_{index}");
            if (pointVisual == null) return null;
            
            // 获取当前数据并创建临时的BrushEditorPoint对象
            Vector2 localCoord = shapeBuilder.GetNode(index);
            Vector3 world = BrushShapeBuilder.LocalToWorldPosition(localCoord, CoordinateTransform);
            Vector3 localPos = CoordinateTransform.InverseTransformPoint(world);
            localPos.y = AxisVisual.GeometryLift;
            world = CoordinateTransform.TransformPoint(localPos);
            
            return new BrushEditorPoint(localCoord, world, coordinateSystem.transform, EditorPointVisual.BasePointRadius, index);
        }
    }
}