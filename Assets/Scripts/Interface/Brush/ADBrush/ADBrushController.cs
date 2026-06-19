using UnityEngine;
using VRBrush.Core;
using VRBrush.Core.Model;
using VRBrush.Core.Model.ADBrush;
using VRBrush.Core.Visual.Element;
using VRBrush.Core.Visual;
using VRBrush.Interface;
using System.Collections.Generic;
using TMPro;
using VRBrush.Interface.UI;

namespace VRBrush.Interface.Brush.ADBrush
{
    /// <summary>
    /// ADBrush控制器
    /// 实现通过多次扣下扳机并移动控制器创建可变形截面的3D笔画
    /// 交互方式：
    /// - 按下扳机：记录起始位置作为关键点位置
    /// - 拖动控制器：实时显示切线方向预览
    /// - 松开扳机：确认关键点和切线方向
    /// - B键：结束当前笔画，开始新笔画
    /// </summary>
    public class ADBrushController : BaseBrushController
    {
        [Header("ADBrush Settings")]
        [SerializeField] private int subdivisionLevel = 10;
        [SerializeField] private string shapeDirectory = "Assets/Brushes";
        [SerializeField] private string selectedShapeName = "triangle";

        public override string BrushName => "ADBrush";

        // ADBrush专用状态
        private ADBrushShapeBuilder shapeBuilder;
        private MorphableShape currentMorphableShape;
        private CurveVisual curvePreview; // 曲线预览组件
        private ReferenceLineVisual referenceLine; // 参考线（按下点→当前点）
        private UniversalBrushPreview endPointPreview; // 曲线末端预览
        private SectionEdgePreview sectionEdgePreview; // 截面棱预览（连接上一锚点到当前位置）

        // 绘制状态追踪
        private Vector3 triggerPressPosition; // 扳机按下时的位置
        private Quaternion triggerPressRotation; // 扳机按下时的旋转
        private bool isTriggerPressed = false; // 扳机是否按下
        private bool hasConfirmedFirstPoint = false; // 是否已确认第一个点
        
        // 扭转角度追踪（新的累积方式）
        private float accumulatedRollingAngle = 0f; // 累积的扭转角度（可以超过360度）
        private Vector3 lastTipDirectionProjection = Vector3.zero; // 上一帧控制器尖端方向在垂直平面上的投影
        private bool hasInitializedRolling = false; // 是否已初始化rolling追踪
        
        // 角度显示UI
        private GameObject angleDisplayObject;
        private TextMeshPro angleDisplayText;

    // 在按下扳机瞬间记录的权重快照（用于该关键点）
    private List<float> lastPressWeights;
    
    // 截面标记（用于可视化确认的截面位置）
    private List<SectionMarker> sectionMarkers = new List<SectionMarker>();

    // 减少提示噪音：同一笔画中"点数不足"的警告只提示一次
    private bool warnedInsufficientPoints = false;

    // 性能优化：限制更新频率
    private float lastMeshUpdateTime = 0f;
    private float lastCurveUpdateTime = 0f;
    private float lastPreviewShapeUpdateTime = 0f;
    private const float meshUpdateInterval = 0.05f; // 20fps for mesh updates
    private const float curveUpdateInterval = 0.033f; // 30fps for curve preview
    private const float previewShapeUpdateInterval = 0.05f; // 20fps for shape preview updates
    private Vector3 lastUpdatePosition;
    private bool lastTriggerState = false;
    
    // Morph 面板逻辑已合并到 BrushUIManager。
    // ADBrushController 仅保留对 Morph 权重的控制接口，不再直接生成或管理 UI。

        protected override void Start()
        {
            base.Start();

            // 加载形状
            LoadShape(selectedShapeName);

            // 初始化ShapeBuilder（使用默认的权重：缩放=1，无morph）
            if (currentMorphableShape != null)
            {
                shapeBuilder = new ADBrushShapeBuilder(currentMorphableShape);
                
                // **设置预览样式（与 HelioBrush 一致）**
                if (universalBrushPreview != null && currentMorphableShape.BaseShape != null)
                {
                    // 采用 Helio 风格可视化
                    universalBrushPreview.SetUseHelioVisuals(true);
                    universalBrushPreview.SetShowShapePoints(true);
                    universalBrushPreview.SetPreviewColor(new Color(0.9f, 0.5f, 0.1f, 0.7f)); // 橙色以区分
                    
                    // 设置形状
                    universalBrushPreview.CurrentShape = currentMorphableShape.BaseShape;
                    Debug.Log($"ADBrushController: Set preview shape to '{currentMorphableShape.BaseShape.Name}' with {currentMorphableShape.BaseShape.NodeCount} nodes");
                }
            }
            else
            {
                Debug.LogWarning("ADBrushController: No morphable shape loaded, using fallback");
            }

            // 创建曲线预览组件
            CreateCurvePreview();
            
            // 创建角度显示UI
            CreateAngleDisplay();
            
            // 创建截面棱预览组件
            CreateSectionEdgePreview();

            // UI绑定已移除 - 由 BrushUIManager 统一管理

            Debug.Log($"ADBrushController: Initialized with shape '{selectedShapeName}'");

            // 构建 Morph 面板
            RebuildMorphUI();
        }

        // 将第一笔按下时的初始化与“绘制中再次按下”的初始化复用
        private void BeginPressForPoint()
        {
            // 记录扳机按下位置和旋转
            triggerPressPosition = GetDrawingPosition();
            triggerPressRotation = GetControllerRotation();
            isTriggerPressed = true;

            // 在按下瞬间记录当前 slider 权重快照
            if (shapeBuilder != null)
            {
                lastPressWeights = new List<float>(shapeBuilder.MorphWeights);
            }

            // 参考线启用，并初始化端点
            if (referenceLine != null)
            {
                referenceLine.SetVisible(true);
                referenceLine.UpdateEndpoints(triggerPressPosition, triggerPressPosition);
            }
            
            // 重置rolling追踪状态
            hasInitializedRolling = false;
            accumulatedRollingAngle = 0f;
            lastTipDirectionProjection = Vector3.zero;
            
            // 隐藏角度显示
            HideAngleDisplay();
        }

        private void ConfirmPointOnRelease()
        {
            if (!isTriggerPressed) return;

            // 扳机松开，确认当前点
            Vector3 releasePosition = GetDrawingPosition();
            Quaternion releaseRotation = GetControllerRotation();
            
            // 计算拖拽方向（从按下位置到松开位置）
            float dragDistance = Vector3.Distance(releasePosition, triggerPressPosition);
            Vector3 dragDirection = (releasePosition - triggerPressPosition).normalized;
            
            // 特殊处理第一个点：没有拖拽时使用控制器朝向
            if (!hasConfirmedFirstPoint && dragDistance < 0.01f)
            {
                dragDirection = triggerPressRotation * Vector3.forward;
                Vector3 firstUpDirection = triggerPressRotation * Vector3.up;
                Quaternion firstRotation = Quaternion.LookRotation(dragDirection, firstUpDirection);
                
                ADBrushShapePoint firstPoint = new ADBrushShapePoint(triggerPressPosition, firstRotation);
                if (shapeBuilder != null)
                {
                    var snapshot = lastPressWeights != null ? new List<float>(lastPressWeights) : new List<float>(shapeBuilder.MorphWeights);
                    float currentSize = ribbonWidth;
                    shapeBuilder.AddConfirmedPoint(firstPoint, snapshot, currentSize);
                    hasConfirmedFirstPoint = true;
                    shapeBuilder.ClearHead();
                    
                    // 创建截面标记
                    CreateSectionMarker(triggerPressPosition, firstRotation, snapshot, currentSize);
                }
                
                UpdateStrokeCurvePreview();
                isTriggerPressed = false;
                if (referenceLine != null) referenceLine.SetVisible(false);
                HideAngleDisplay();
                hasInitializedRolling = false;
                accumulatedRollingAngle = 0f;
                return;
            }
            
            if (dragDistance < 0.01f)
            {
                dragDirection = triggerPressRotation * Vector3.forward;
            }

            // 拖拽距离阈值：大于 0.1m 才允许手腕旋转控制 rolling
            const float minDragDistanceForRolling = 0.1f;
            Quaternion rotation;

            if (dragDistance >= minDragDistanceForRolling)
            {
                // 长拖拽：先从上一个锚点无扭转转过来，再应用累积的rolling角度
                if (hasConfirmedFirstPoint && shapeBuilder != null && shapeBuilder.PointCount > 0)
                {
                    var lastPoint = shapeBuilder.GetLastConfirmedPoint();
                    if (lastPoint.HasValue)
                    {
                        // 从上一个锚点的旋转无扭转转到当前拖拽方向
                        Quaternion baseRotation = ADBrushShapePoint.RotateWithoutTwist(lastPoint.Value.rotation, dragDirection);
                        // 再绕拖拽方向应用累积的rolling角度
                        rotation = Quaternion.AngleAxis(accumulatedRollingAngle, dragDirection) * baseRotation;
                    }
                    else
                    {
                        // fallback：使用世界up
                        Vector3 baseUp = Vector3.ProjectOnPlane(Vector3.up, dragDirection).normalized;
                        if (baseUp.sqrMagnitude < 1e-6f) baseUp = Vector3.Cross(dragDirection, Vector3.right).normalized;
                        rotation = Quaternion.AngleAxis(accumulatedRollingAngle, dragDirection) * Quaternion.LookRotation(dragDirection, baseUp);
                    }
                }
                else
                {
                    // 第一个点的长拖拽：使用控制器旋转作为基准
                    Quaternion baseRotation = Quaternion.LookRotation(dragDirection, triggerPressRotation * Vector3.up);
                    rotation = Quaternion.AngleAxis(accumulatedRollingAngle, dragDirection) * baseRotation;
                }
            }
            else
            {
                // 短拖拽：从上一个锚点无扭转转过来，不应用任何rolling
                if (hasConfirmedFirstPoint && shapeBuilder != null && shapeBuilder.PointCount > 0)
                {
                    var lastPoint = shapeBuilder.GetLastConfirmedPoint();
                    if (lastPoint.HasValue)
                    {
                        // 从上一个锚点的旋转无扭转转到当前拖拽方向
                        rotation = ADBrushShapePoint.RotateWithoutTwist(lastPoint.Value.rotation, dragDirection);
                    }
                    else
                    {
                        // fallback
                        Vector3 upDirection = Vector3.ProjectOnPlane(Vector3.up, dragDirection).normalized;
                        if (upDirection.sqrMagnitude < 1e-6f) upDirection = Vector3.Cross(dragDirection, Vector3.right).normalized;
                        rotation = Quaternion.LookRotation(dragDirection, upDirection);
                    }
                }
                else
                {
                    // 第一个点的短拖拽：使用控制器旋转
                    rotation = Quaternion.LookRotation(dragDirection, triggerPressRotation * Vector3.up);
                }
            }

            ADBrushShapePoint newPoint = new ADBrushShapePoint(triggerPressPosition, rotation);
            if (shapeBuilder != null)
            {
                if (hasConfirmedFirstPoint)
                {
                    var lastPoint = shapeBuilder.GetLastConfirmedPoint();
                    if (lastPoint.HasValue)
                    {
                        Vector2 targetNormal = new Vector2(
                            Mathf.Atan2(dragDirection.x, dragDirection.z),
                            Mathf.Asin(dragDirection.y)
                        );
                        var adjusted = ADBrushShapePoint.GetNewOneByLatestOne(lastPoint.Value, targetNormal);
                        newPoint = new ADBrushShapePoint(triggerPressPosition, rotation);
                    }
                }

                var snapshot = lastPressWeights != null ? new List<float>(lastPressWeights) : new List<float>(shapeBuilder.MorphWeights);
                float currentSize = ribbonWidth;  // 捕获当前 size
                shapeBuilder.AddConfirmedPoint(newPoint, snapshot, currentSize);
                hasConfirmedFirstPoint = true;
                shapeBuilder.ClearHead(); // 结束预览段
                
                // 创建截面标记
                CreateSectionMarker(triggerPressPosition, rotation, snapshot, currentSize);
            }

            // 松开后立即更新mesh一次，让新确认的段可见（即使会在下次按下时因 Catmull-Rom 调整而变化）
            if (shapeBuilder.PointCount >= 2)
            {
                UpdateStrokeMesh(forceUpdate: true);
            }

            // 松开后刷新曲线预览（至少应看到点/段）
            UpdateStrokeCurvePreview();

            isTriggerPressed = false;
            if (referenceLine != null) referenceLine.SetVisible(false);
            
            // 松开扳机后隐藏角度显示
            HideAngleDisplay();
            
            // 重置rolling状态
            hasInitializedRolling = false;
            accumulatedRollingAngle = 0f;
        }

        /// <summary>
        /// 创建曲线预览组件
        /// </summary>
        private void CreateCurvePreview()
        {
            GameObject previewObj = new GameObject("ADBrush_CurvePreview");
            previewObj.transform.SetParent(transform);
            curvePreview = previewObj.AddComponent<CurveVisual>();
            curvePreview.SetColor(new Color(0.2f, 0.8f, 1f, 0.8f));
            curvePreview.SetWidth(0.004f); // 提高可见性，单点也更容易看到
            curvePreview.SetShowPoints(true, 0.005f);
            // 创建参考线
            GameObject refObj = new GameObject("ADBrush_ReferenceLine");
            refObj.transform.SetParent(transform);
            referenceLine = refObj.AddComponent<ReferenceLineVisual>();
            referenceLine.Initialize();
            // 参考线改为与主曲线相同的青色，避免出现“黄色折线”观感
            referenceLine.SetColor(new Color(0.2f, 0.8f, 1f, 0.8f));
            referenceLine.SetRadius(0.003f);
            referenceLine.SetVisible(false);
        }

        /// <summary>
        /// 获取当前可用的形状名称列表（供BrushUIManager使用）
        /// </summary>
        public List<string> GetAvailableShapeNames()
        {
            // 确保目录存在
            if (!System.IO.Directory.Exists(shapeDirectory))
            {
                System.IO.Directory.CreateDirectory(shapeDirectory);
            }

            var files = System.IO.Directory.GetFiles(shapeDirectory, "*.json");
            var options = new List<string>();
            foreach (var f in files)
            {
                options.Add(System.IO.Path.GetFileNameWithoutExtension(f));
            }
            if (options.Count == 0)
            {
                options.Add("triangle");
            }
            return options;
        }

        /// <summary>
        /// 获取当前激活的形状名称（供BrushUIManager使用）
        /// </summary>
        public string GetActiveShapeName()
        {
            return selectedShapeName;
        }

        /// <summary>
        /// 切换到指定形状（由BrushUIManager调用）
        /// </summary>
        public void ActivateShape(string shapeName)
        {
            selectedShapeName = shapeName;
            LoadShape(shapeName);
            if (currentMorphableShape != null)
            {
                shapeBuilder = new ADBrushShapeBuilder(currentMorphableShape);
                if (curvePreview != null) curvePreview.Clear();
                
                // 切换形状时也要更新预览
                if (universalBrushPreview != null && currentMorphableShape.BaseShape != null)
                {
                    universalBrushPreview.SetUseHelioVisuals(true);
                    universalBrushPreview.SetShowShapePoints(true);
                    universalBrushPreview.SetPreviewColor(new Color(0.9f, 0.5f, 0.1f, 0.7f));
                    universalBrushPreview.CurrentShape = currentMorphableShape.BaseShape;
                    Debug.Log($"ADBrushController: Changed preview shape to '{currentMorphableShape.BaseShape.Name}'");
                }
                
                // 更新末端预览的形状
                if (endPointPreview != null && currentMorphableShape.BaseShape != null)
                {
                    endPointPreview.CurrentShape = currentMorphableShape.BaseShape;
                }

                // 重新生成 Morph UI
                RebuildMorphUI();
            }
        }

        protected override void Update()
        {
            base.Update();

            // 处理B键结束当前笔画
            if (GetBButtonPressed())
            {
                FinishCurrentStroke();
            }
            
            // 绘制阶段：
            // - 按下：更新参考线与网格（网格仅在按下时更新）
            // - 未按：更新 head（幽灵点）
            // - 始终更新曲线预览
            if (isDrawing)
            {
                Vector3 currentPos = GetDrawingPosition();
                bool stateChanged = (isTriggerPressed != lastTriggerState);
                bool positionChanged = Vector3.Distance(currentPos, lastUpdatePosition) > 0.001f;
                
                if (isTriggerPressed)
                {
                    UpdateShadowPoint();
                    
                    // Mesh 更新频率限制：时间间隔或状态改变或位置显著变化时更新
                    float timeSinceLastMesh = Time.time - lastMeshUpdateTime;
                    if (stateChanged || timeSinceLastMesh >= meshUpdateInterval || positionChanged)
                    {
                        UpdateStrokeMesh();
                        lastMeshUpdateTime = Time.time;
                    }
                }
                else
                {
                    UpdateIdleHeadPreview();
                }

                // 曲线预览更新频率限制
                float timeSinceLastCurve = Time.time - lastCurveUpdateTime;
                if (stateChanged || timeSinceLastCurve >= curveUpdateInterval || positionChanged)
                {
                    UpdateStrokeCurvePreview();
                    UpdateSectionEdgePreview(); // 更新截面棱预览
                    lastCurveUpdateTime = Time.time;
                }
                
                lastUpdatePosition = currentPos;
                lastTriggerState = isTriggerPressed;
            }
            
            // **设置预览朝向：始终使用控制器旋转（让预览跟随控制器）**
            // 注意：这只影响未按下扳机时的预览。按下扳机时，GetMainPreviewRotationOverride 会返回基于拖拽的旋转。
            hasPreviewOrientation = true;
            lastPreviewTangent = GetControllerRotation() * Vector3.forward;
            lastPreviewRuling = GetControllerRotation() * Vector3.up;
        }

        /// <summary>
        /// 基于当前 morph 权重更新通用预览的截面形状
        /// </summary>
        private void UpdatePreviewShapeFromWeights()
        {
            if (universalBrushPreview == null || currentMorphableShape == null || shapeBuilder == null)
                return;

            var weights = new System.Collections.Generic.List<float>(shapeBuilder.MorphWeights);
            var morphed = currentMorphableShape.GetBrushShape(weights);
            if (morphed != null)
            {
                universalBrushPreview.CurrentShape = morphed;
            }
        }

        /// <summary>
        /// 加载形状文件
        /// </summary>
        private void LoadShape(string shapeName)
        {
            string filePath = System.IO.Path.Combine(shapeDirectory, $"{shapeName}.json");
            currentMorphableShape = MorphableShape.LoadFromFile(filePath);

            if (currentMorphableShape == null)
            {
                Debug.LogWarning($"ADBrushController: Failed to load shape '{shapeName}', using fallback");
                // 创建一个默认的圆形截面
                CreateDefaultCircleShape();
            }
        }

        /// <summary>
        /// 创建默认的圆形截面
        /// </summary>
        private void CreateDefaultCircleShape()
        {
            var circleShape = new BrushShape("DefaultCircle");
            int segments = 8;
            for (int i = 0; i < segments; i++)
            {
                float angle = (float)i / segments * 2f * Mathf.PI;
                circleShape.AddNode(new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 0.5f);
            }
            for (int i = 0; i < segments; i++)
            {
                circleShape.AddEdge(i, (i + 1) % segments);
            }
            currentMorphableShape = new MorphableShape(circleShape);
        }

        /// <summary>
        /// 重建 Morph 相关数据（不再直接创建 UI）。
        /// UI 层应查询 currentMorphableShape.MorphNames/MorphCount 和 shapeBuilder.MorphWeights
        /// 来生成对应的 MorphItemUI，并将 OnExternalMorphValueChanged 绑定为回调。
        /// </summary>
        private void RebuildMorphUI()
        {
            // 若还没有 builder，则先创建，保证有权重数组
            if (shapeBuilder == null && currentMorphableShape != null)
            {
                shapeBuilder = new ADBrushShapeBuilder(currentMorphableShape);
            }

            // 此处不再直接操作任何 UI，具体 UI 由 BrushUIManager 负责。
        }

        /// <summary>
        /// 供外部 UI（如 BrushUIManager 中的 MorphItemUI）调用的回调，用于更新指定 morph 权重。
        /// </summary>
        public void OnExternalMorphValueChanged(int morphIndex, float value)
        {
            // 直接设置对应 morph（不包含 size）
            SetMorphWeight(morphIndex, value);

            // 如果正在按下扳机，同步更新 lastPressWeights，确保实际绘制的 mesh 也反映变化
            if (isTriggerPressed && lastPressWeights != null && morphIndex < lastPressWeights.Count)
            {
                lastPressWeights[morphIndex] = Mathf.Clamp01(value);
            }

            // 预览形状更新频率限制（避免拖动滑块时每帧都重建形状）
            float timeSinceLastUpdate = Time.time - lastPreviewShapeUpdateTime;
            if (timeSinceLastUpdate >= previewShapeUpdateInterval)
            {
                UpdatePreviewShapeFromWeights();
                lastPreviewShapeUpdateTime = Time.time;
            }
        }

        /// <summary>
        /// 获取所有 Morph 的名称列表（用于 UI 显示）
        /// </summary>
        public List<string> GetMorphNames()
        {
            // 如果还没加载形状，先尝试加载
            if (currentMorphableShape == null)
            {
                LoadShape(selectedShapeName);
                
                // 如果加载后还是null（fallback创建了默认形状），需要初始化builder
                if (currentMorphableShape != null && shapeBuilder == null)
                {
                    shapeBuilder = new ADBrushShapeBuilder(currentMorphableShape);
                }
            }
            
            if (currentMorphableShape == null)
            {
                Debug.LogWarning("ADBrushController.GetMorphNames: currentMorphableShape is still null after loading");
                return new List<string>();
            }

            return new List<string>(currentMorphableShape.MorphNames);
        }

        /// <summary>
        /// 获取所有 Morph 的当前权重列表
        /// </summary>
        public List<float> GetMorphWeights()
        {
            // 确保builder存在
            if (shapeBuilder == null && currentMorphableShape != null)
            {
                shapeBuilder = new ADBrushShapeBuilder(currentMorphableShape);
            }
            
            if (shapeBuilder?.MorphWeights == null)
            {
                Debug.LogWarning("ADBrushController.GetMorphWeights: shapeBuilder or MorphWeights is null");
                return new List<float>();
            }

            return new List<float>(shapeBuilder.MorphWeights);
        }

        protected override void OnStrokeStart()
        {
            base.OnStrokeStart();

            // 重置ADBrush状态
            if (shapeBuilder == null)
            {
                shapeBuilder = new ADBrushShapeBuilder(currentMorphableShape);
            }
            else
            {
                shapeBuilder.Clear();
            }

            hasConfirmedFirstPoint = false;
            isTriggerPressed = false;

            // 重置性能优化相关变量
            lastMeshUpdateTime = 0f;
            lastCurveUpdateTime = 0f;
            lastPreviewShapeUpdateTime = 0f;
            lastUpdatePosition = Vector3.zero;
            lastTriggerState = false;

            // 清除曲线预览
            if (curvePreview != null)
            {
                curvePreview.Clear();
            }

            Debug.Log("ADBrushController: Started new stroke");

            warnedInsufficientPoints = false;
        }

        protected override void OnStrokeEnd()
        {
            base.OnStrokeEnd();

            // 清空未确认的 head（不自动确认）
            if (shapeBuilder != null)
            {
                shapeBuilder.ClearHead();
            }

            Debug.Log($"ADBrushController: Ended stroke with {shapeBuilder?.PointCount ?? 0} points");
        }

        /// <summary>
        /// 重写UpdateCurrentStroke以实现ADBrush的绘制逻辑
        /// </summary>
        protected override void UpdateCurrentStroke()
        {
            // ADBrush不使用连续添加点的方式
            // 而是通过扳机按下/松开来确定关键点
            // 所以这里不做任何操作
        }

        /// <summary>
        /// 重写StartDrawing以实现扳机按下逻辑
        /// </summary>
        protected override void StartDrawing()
        {
            if (!isDrawing)
            {
                // 第一次按下扳机，开始整个笔画
                base.StartDrawing();
                Debug.Log($"ADBrushController: Started new stroke, currentStrokeObject={(currentStrokeObject != null ? "created" : "NULL!")}");
            }
            BeginPressForPoint();
            Debug.Log($"ADBrushController: Trigger pressed at {triggerPressPosition}, rotation={triggerPressRotation.eulerAngles}");
        }

        /// <summary>
        /// 重写StopDrawing以实现扳机松开逻辑
        /// </summary>
        // 自定义 StopDrawing：保留当前 mesh，不交给基类销毁逻辑
        protected override void StopDrawing()
        {
            // 根据已确认点数量决定保留或丢弃当前笔画对象
            bool keep = shapeBuilder != null && shapeBuilder.PointCount >= 2;

            if (keep)
            {
                if (currentStrokeObject != null && !completedStrokes.Contains(currentStrokeObject))
                {
                    completedStrokes.Add(currentStrokeObject);
                }
            }
            else
            {
                if (currentStrokeObject != null)
                {
                    UnityEngine.Object.DestroyImmediate(currentStrokeObject);
                }
            }

            // 隐藏参考线
            if (referenceLine != null) referenceLine.SetVisible(false);
            
            // 清理截面标记
            ClearSectionMarkers();

            // 调用结束钩子
            OnStrokeEnd();

            // 结束状态，但保留（或已销毁）场景中的 mesh 对象
            isDrawing = false;
            currentStroke = null; // ADBrush 不使用基类 Stroke 数据
            currentStrokeObject = null; // 释放引用（对象本身已被保留为完成笔画或被销毁）
        }

        /// <summary>
        /// 更新幽灵点（实时预览）
        /// </summary>
        private void UpdateShadowPoint()
        {
            if (!isTriggerPressed || shapeBuilder == null)
            {
                return;
            }

            Vector3 currentPosition = GetDrawingPosition();
            Quaternion currentRotation = GetControllerRotation();
            
            // 计算拖拽距离和方向
            float dragDistance = Vector3.Distance(currentPosition, triggerPressPosition);
            Vector3 dragDirection = (currentPosition - triggerPressPosition).normalized;

            // 如果拖动距离太短，使用控制器前向
            if (dragDistance < 0.01f)
            {
                dragDirection = triggerPressRotation * Vector3.forward;
            }

            // 无论距离多短，都显示角度（距离太短时显示0°，距离够长时显示累积角度）
            // 拖拽距离阈值：大于 0.1m 才允许手腕旋转控制 rolling
            const float minDragDistanceForRolling = 0.1f;
            Vector3 upDirection;

            if (dragDistance >= minDragDistanceForRolling)
            {
                // 距离足够长：使用新的累积rolling方式
                // 控制器尖端方向
                Vector3 tipDirection = currentRotation * Vector3.forward;
                
                // 计算尖端方向与拉扯方向的夹角
                float angleWithDrag = Vector3.Angle(tipDirection, dragDirection);
                float sinValue = Mathf.Sin(angleWithDrag * Mathf.Deg2Rad);
                
                // 当夹角的sin值足够大时（夹角足够大），才进行rolling累积
                if (sinValue > 0.1f)
                {
                    // 将尖端方向投影到垂直于拉扯方向的平面上
                    Vector3 currentProjection = Vector3.ProjectOnPlane(tipDirection, dragDirection).normalized;
                    
                    if (!hasInitializedRolling)
                    {
                        // 初始化：记录初始投影方向作为0度参考
                        lastTipDirectionProjection = currentProjection;
                        hasInitializedRolling = true;
                        accumulatedRollingAngle = 0f;
                    }
                    else if (lastTipDirectionProjection.sqrMagnitude > 0.01f && currentProjection.sqrMagnitude > 0.01f)
                    {
                        // 计算当前帧相对于上一帧的角度变化
                        // 使用拉扯方向作为旋转轴
                        float deltaAngle = Vector3.SignedAngle(lastTipDirectionProjection, currentProjection, dragDirection);
                        
                        // 累积角度（允许超过360度）
                        accumulatedRollingAngle += deltaAngle;
                        
                        // 更新上一帧投影方向
                        lastTipDirectionProjection = currentProjection;
                    }
                }
                
                // 使用累积角度计算upDirection
                // 选择一个基准up方向（垂直于拉扯方向）
                Vector3 baseUp = Vector3.ProjectOnPlane(Vector3.up, dragDirection);
                if (baseUp.sqrMagnitude < 1e-6f)
                {
                    baseUp = Vector3.ProjectOnPlane(Vector3.forward, dragDirection);
                }
                baseUp.Normalize();
                
                // 绕拉扯方向旋转baseUp，得到最终的upDirection
                upDirection = Quaternion.AngleAxis(accumulatedRollingAngle, dragDirection) * baseUp;
                
                // 距离足够长时，始终显示当前累积角度
                ShowAngleDisplay(currentPosition, accumulatedRollingAngle);
            }
            else
            {
                // 距离不够长：强制对齐到离切线最近的四元数，禁用 rolling 控制
                // 使用控制器 Up 投影到垂直于切线的平面上，移除绕切线的旋转分量
                Vector3 controllerUp = currentRotation * Vector3.up;
                upDirection = Vector3.ProjectOnPlane(controllerUp, dragDirection);
                
                // 如果投影后向量太小（接近平行），使用备用方向
                if (upDirection.sqrMagnitude < 1e-6f)
                {
                    upDirection = Vector3.Cross(dragDirection, Vector3.right);
                    if (upDirection.sqrMagnitude < 1e-6f)
                    {
                        upDirection = Vector3.Cross(dragDirection, Vector3.up);
                    }
                }
                upDirection.Normalize();
                
                // 距离不够长时，显示 0° （因为此时不允许 rolling，保持初始方向）
                ShowAngleDisplay(currentPosition, 0f);
            }

            Quaternion rotation = Quaternion.LookRotation(dragDirection, upDirection);

            // 如果有前一个点，保持旋转连续性
            var lastPoint = shapeBuilder.GetLastConfirmedPoint();
            if (lastPoint.HasValue)
            {
                Vector2 targetNormal = new Vector2(
                    Mathf.Atan2(dragDirection.x, dragDirection.z),
                    Mathf.Asin(dragDirection.y)
                );
                var adjustedPoint = ADBrushShapePoint.GetNewOneByLatestOne(lastPoint.Value, targetNormal);
                // 保持计算的法线方向，但应用当前的 upDirection（已根据距离处理过）
                rotation = Quaternion.LookRotation(dragDirection, upDirection);
            }

            // 拖拽阶段：仅更新参考线（按下点→当前点）
            if (referenceLine != null)
            {
                referenceLine.SetVisible(true);
                referenceLine.UpdateEndpoints(triggerPressPosition, currentPosition);
            }
        }

        /// <summary>
        /// 非按压时的 head 预览：以控制器当前位置/朝向作为候选点
        /// </summary>
        private void UpdateIdleHeadPreview()
        {
            if (!isDrawing || isTriggerPressed || shapeBuilder == null)
            {
                return;
            }

            Vector3 pos = GetDrawingPosition();
            Quaternion rot = GetControllerRotation();
            var head = new ADBrushShapePoint(pos, rot);
            shapeBuilder.SetHead(head);

            // 空闲时不显示参考线，只有拖拽时才显示
            if (referenceLine != null) referenceLine.SetVisible(false);
            
            // 更新截面棱预览
            if (hasConfirmedFirstPoint && shapeBuilder.PointCount > 0)
            {
                UpdateSectionEdgePreview();
            }
        }

        /// <summary>
        /// 更新笔画Mesh
        /// </summary>
        /// <param name="forceUpdate">强制更新，忽略 isTriggerPressed 检查（用于松开扳机后的最后一次更新）</param>
        private void UpdateStrokeMesh(bool forceUpdate = false)
        {
            if (shapeBuilder == null)
            {
                Debug.LogWarning("ADBrushController.UpdateStrokeMesh: shapeBuilder is null!");
                return;
            }
            // 仅在扳机按下时实时更新网格，或者强制更新时
            if (!forceUpdate && !isTriggerPressed)
            {
                return;
            }
            if (shapeBuilder.PointCount < 2)
            {
                // 当还没有确认任何点时（0个），不提示噪音；只有在已有点但不足2个时提示一次
                if (shapeBuilder.PointCount > 0 && !warnedInsufficientPoints)
                {
                    Debug.LogWarning($"ADBrushController.UpdateStrokeMesh: Not enough points ({shapeBuilder.PointCount}/2 minimum). Keep pressing/releasing trigger!");
                    warnedInsufficientPoints = true;
                }
                return;
            }
            if (currentStrokeObject == null)
            {
                Debug.LogError("ADBrushController.UpdateStrokeMesh: currentStrokeObject is NULL! This should never happen!");
                return;
            }

            Debug.Log($"ADBrushController.UpdateStrokeMesh: Building mesh with {shapeBuilder.PointCount} points.");
            // 构建Mesh（全局截面宽度使用 ribbonWidth，保持与预览一致）
            Stroke stroke = shapeBuilder.Build3DMesh(subdivisionLevel, ribbonWidth);
            if (stroke != null && currentStrokeObject != null)
            {
                // 松开后隐藏参考线
                if (referenceLine != null) referenceLine.SetVisible(false);
                var meshFilter = currentStrokeObject.GetComponent<MeshFilter>();
                if (meshFilter != null)
                {
                    meshFilter.mesh = stroke.GetMesh();
                    // Debug.Log($"ADBrushController.UpdateStrokeMesh: Mesh updated! Vertices={stroke.GetMesh().vertexCount}, Triangles={stroke.GetMesh().triangles.Length / 3}");
                }
                else
                {
                    Debug.LogError("ADBrushController.UpdateStrokeMesh: MeshFilter component not found!");
                }
            }
            else
            {
                Debug.LogWarning($"ADBrushController.UpdateStrokeMesh: Build3DMesh returned null! stroke={(stroke != null ? "OK" : "NULL")}, currentStrokeObject={(currentStrokeObject != null ? "OK" : "NULL")}");
            }
        }

        // 让通用预览在绘制时使用基于拖拽的朝向，与实际mesh生成保持一致
        protected override Quaternion? GetMainPreviewRotationOverride()
        {
            // 非绘制状态时：返回 null，让基类使用 controllerRotation
            // 绘制状态时：
            //   - 未按下扳机：返回 null，让基类使用 GetCustomDrawingPreviewRotation()
            //   - 按下扳机时：返回基于拖拽方向的旋转
            if (!isDrawing || !isTriggerPressed)
            {
                return null; // 走基类默认
            }

            Vector3 currentPos = GetDrawingPosition();
            Vector3 drag = currentPos - triggerPressPosition;
            float dragDistance = drag.magnitude;
            Vector3 forward = dragDistance > 1e-3f ? drag.normalized : (GetControllerRotation() * Vector3.forward);
            
            const float minDragDistanceForRolling = 0.1f;
            Quaternion rotation;
            
            if (dragDistance >= minDragDistanceForRolling)
            {
                // 长拖拽：先从上一个锚点无扭转转过来，再应用累积的rolling角度
                if (hasConfirmedFirstPoint && shapeBuilder != null && shapeBuilder.PointCount > 0)
                {
                    var lastPoint = shapeBuilder.GetLastConfirmedPoint();
                    if (lastPoint.HasValue)
                    {
                        Quaternion baseRotation = ADBrushShapePoint.RotateWithoutTwist(lastPoint.Value.rotation, forward);
                        rotation = Quaternion.AngleAxis(accumulatedRollingAngle, forward) * baseRotation;
                    }
                    else
                    {
                        Vector3 baseUp = Vector3.ProjectOnPlane(Vector3.up, forward).normalized;
                        if (baseUp.sqrMagnitude < 1e-6f) baseUp = Vector3.Cross(forward, Vector3.right).normalized;
                        rotation = Quaternion.AngleAxis(accumulatedRollingAngle, forward) * Quaternion.LookRotation(forward, baseUp);
                    }
                }
                else
                {
                    Quaternion baseRotation = Quaternion.LookRotation(forward, triggerPressRotation * Vector3.up);
                    rotation = Quaternion.AngleAxis(accumulatedRollingAngle, forward) * baseRotation;
                }
            }
            else
            {
                // 短拖拽：从上一个锚点无扭转转过来
                if (hasConfirmedFirstPoint && shapeBuilder != null && shapeBuilder.PointCount > 0)
                {
                    var lastPoint = shapeBuilder.GetLastConfirmedPoint();
                    if (lastPoint.HasValue)
                    {
                        rotation = ADBrushShapePoint.RotateWithoutTwist(lastPoint.Value.rotation, forward);
                    }
                    else
                    {
                        Vector3 up = Vector3.ProjectOnPlane(Vector3.up, forward).normalized;
                        if (up.sqrMagnitude < 1e-6f) up = Vector3.Cross(forward, Vector3.right).normalized;
                        rotation = Quaternion.LookRotation(forward, up);
                    }
                }
                else
                {
                    rotation = Quaternion.LookRotation(forward, triggerPressRotation * Vector3.up);
                }
            }

            return rotation;
        }

        /// <summary>
        /// 更新曲线预览
        /// </summary>
        private void UpdateStrokeCurvePreview()
        {
            if (curvePreview == null || shapeBuilder == null)
            {
                return;
            }
            
            // Debug.Log("ADBrushController.UpdateStrokeCurvePreview: Updating curve preview.");
            // 仅在未按下时包含幽灵点(head)
            bool includeShadow = !isTriggerPressed;
            StrokeCurve previewCurve = shapeBuilder.BuildStrokeCurve(subdivisionLevel, includeShadow: includeShadow);
            curvePreview.UpdateCurve(previewCurve);

            // 显示已确认锚点
            var anchors = new System.Collections.Generic.List<Vector3>();
            var pts = shapeBuilder.Points;
            for (int i = 0; i < pts.Count; i++) anchors.Add(pts[i].position);
            curvePreview.UpdateAnchorPoints(anchors);

            // 显示幽灵 head（如果存在）
            var head = shapeBuilder.GetShadowPoint();
            if (head.HasValue)
            {
                curvePreview.UpdateGhostHead(head.Value.position);
            }
            else
            {
                curvePreview.UpdateGhostHead(null);
            }
            
            // 更新末端预览
            UpdateEndPointPreview();
        }

        /// <summary>
        /// 结束当前笔画（B键）
        /// </summary>
        private void FinishCurrentStroke()
        {
            if (isDrawing)
            {
                Debug.Log($"ADBrushController: Finishing current stroke (B button pressed), PointCount={shapeBuilder?.PointCount ?? 0}");

                // 清空 head 预览点
                if (shapeBuilder != null)
                {
                    shapeBuilder.ClearHead();
                }

                // 最终更新一次 Mesh，确保所有已确认的点都在 mesh 中
                if (shapeBuilder != null && shapeBuilder.PointCount >= 2)
                {
                    UpdateStrokeMesh(forceUpdate: true);
                    
                    // 确保 mesh 被添加到完成列表中（保留在场景中）
                    if (currentStrokeObject != null && !completedStrokes.Contains(currentStrokeObject))
                    {
                        completedStrokes.Add(currentStrokeObject);
                        Debug.Log($"ADBrushController: Stroke saved with {shapeBuilder.PointCount} points, parent={currentStrokeObject.transform.parent?.name ?? "null"}");
                    }
                }
                else
                {
                    // 点数不足，销毁未完成的笔画对象
                    if (currentStrokeObject != null)
                    {
                        Debug.Log("ADBrushController: Not enough points, destroying stroke object");
                        UnityEngine.Object.DestroyImmediate(currentStrokeObject);
                    }
                }

                // 清除曲线预览（只清预览，不清mesh）
                if (curvePreview != null)
                {
                    curvePreview.Clear();
                }

                // 隐藏参考线
                if (referenceLine != null)
                {
                    referenceLine.SetVisible(false);
                }
                
                // 清除截面棱预览
                if (sectionEdgePreview != null)
                {
                    sectionEdgePreview.ClearLastAnchor();
                }
                
                // 清理截面标记
                ClearSectionMarkers();

                // 调用结束钩子
                OnStrokeEnd();

                // 结束绘制状态
                isDrawing = false;
                currentStroke = null;
                currentStrokeObject = null; // 释放引用（对象已保留在场景或已销毁）

                // 重置状态，准备下一笔
                isTriggerPressed = false;
                hasConfirmedFirstPoint = false;
                
                // 清空 builder 数据，准备下一笔画
                if (shapeBuilder != null)
                {
                    shapeBuilder.Clear();
                }
            }
        }
        
        /// <summary>
        /// 创建截面标记
        /// </summary>
        private void CreateSectionMarker(Vector3 position, Quaternion rotation, List<float> weights, float width)
        {
            if (currentMorphableShape == null) return;
            
            var morphedShape = currentMorphableShape.GetBrushShape(weights);
            if (morphedShape == null || morphedShape.vertices == null || morphedShape.vertices.Count < 2) return;
            
            Vector2[] shapeVerts = morphedShape.vertices.ToArray();
            Color markerColor = new Color(0.5f, 0.9f, 0.5f, 0.5f); // 半透明绿色
            
            var marker = SectionMarker.Create(position, rotation, shapeVerts, width, markerColor, 0.002f, transform);
            if (marker != null)
            {
                sectionMarkers.Add(marker);
            }
        }
        
        /// <summary>
        /// 清理所有截面标记
        /// </summary>
        private void ClearSectionMarkers()
        {
            foreach (var marker in sectionMarkers)
            {
                if (marker != null)
                {
                    marker.Destroy();
                }
            }
            sectionMarkers.Clear();
        }

        // 基类在绘制中再次按下/释放的回调
        protected override void OnTriggerPressedWhileDrawing()
        {
            BeginPressForPoint();
        }
        protected override void OnTriggerReleasedWhileDrawing()
        {
            ConfirmPointOnRelease();
            // 不结束笔画，继续 isDrawing 状态，允许下一次按下添加更多点
        }

        // 预览位置在按下时固定于按下点；确认第一个点后松开扳机时显示笔刷预览跟随控制器
        protected override void UpdateUniversalBrushPreview(Vector3 position, Quaternion controllerRotation, bool drawing, float speed)
        {
            if (universalBrushPreview == null) return;
            
            // 空闲时（未按下扳机）的处理：
            // - 如果还没确认第一个点，隐藏笔刷预览
            // - 如果已确认第一个点，显示笔刷预览跟随控制器（和未开始绘制时一样的行为）
            if (drawing && !isTriggerPressed)
            {
                if (hasConfirmedFirstPoint)
                {
                    // 已确认第一个点后，笔刷预览跟随控制器
                    base.UpdateUniversalBrushPreview(position, controllerRotation, drawing, speed);
                }
                else
                {
                    // 还没确认第一个点，隐藏预览
                    universalBrushPreview.Hide();
                }
                return;
            }
            
            Vector3 pos = position;
            if (drawing && isTriggerPressed)
            {
                pos = triggerPressPosition; // 按下阶段固定在按下位置
            }
            base.UpdateUniversalBrushPreview(pos, controllerRotation, drawing, speed);
        }

        /// <summary>
        /// 获取B键按下状态
        /// </summary>
        private bool GetBButtonPressed()
        {
            try
            {
                var devices = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();
                UnityEngine.XR.InputDevices.GetDevicesAtXRNode(UnityEngine.XR.XRNode.RightHand, devices);
                if (devices.Count > 0)
                {
                    var device = devices[0];
                    if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool bButton))
                    {
                        // 检测按下事件（从未按下到按下）
                        bool pressed = bButton && !previousBButtonState;
                        previousBButtonState = bButton;
                        return pressed;
                    }
                }
            }
            catch (System.Exception)
            {
                // 忽略错误
            }
            return false;
        }

        private bool previousBButtonState = false;

        /// <summary>
        /// 设置变形权重（供Radial Menu调用）
        /// </summary>
        public void SetMorphWeight(int morphIndex, float weight)
        {
            if (shapeBuilder != null)
            {
                shapeBuilder.SetMorphWeight(morphIndex, weight);
                if (isTriggerPressed)
                {
                    UpdateStrokeMesh(); // 仅在按下时实时更新
                }
            }
        }

        /// <summary>
        /// 设置缩放
        /// </summary>
        // 注意：Size 由 BrushSetting（ribbonWidth）统一控制，这里不再设置 size morph。
        
        /// <summary>
        /// 创建角度显示UI（世界空间TextMeshPro - Billboard方式）
        /// </summary>
        private void CreateAngleDisplay()
        {
            // 创建一个简单的GameObject，不使用Canvas
            angleDisplayObject = new GameObject("ADBrush_AngleDisplay");

            // 默认挂在控制器（刷子）的 transform 底下，这样会跟随刷子移动
            angleDisplayObject.transform.SetParent(transform, worldPositionStays: false);

            // 直接添加TextMeshPro组件（世界空间）
            angleDisplayText = angleDisplayObject.AddComponent<TextMeshPro>();
            angleDisplayText.fontSize = 1; // 缩小到原来的1/10
            angleDisplayText.alignment = TextAlignmentOptions.Center;
            angleDisplayText.color = Color.yellow;
            angleDisplayText.text = "0°";
            angleDisplayText.enableAutoSizing = false;

            // 把文字稍微抬高一点，避免被笔刷预览遮挡
            angleDisplayObject.transform.localPosition = Vector3.up * 0.1f;

            // 初始隐藏
            angleDisplayObject.SetActive(false);
        }
        
        /// <summary>
        /// 显示角度UI并更新位置（挂在笔刷预览附近，始终朝向相机）
        /// </summary>
        private void ShowAngleDisplay(Vector3 fallbackPosition, float angle)
        {
            if (angleDisplayObject == null || angleDisplayText == null) return;

            angleDisplayObject.SetActive(true);

            // 强制将文字的世界位置设置到当前笔刷预览位置（fallbackPosition）上方一点
            // 这样无论父子关系如何，文字都会出现在笔刷头顶
            angleDisplayObject.transform.position = fallbackPosition + Vector3.up * 0.1f;

            // Billboard效果：让文字背对相机（反向）
            Camera mainCam = Camera.main;
            Vector3 forward = Vector3.back; // 默认背向
            if (mainCam != null)
            {
                Vector3 toCamera = mainCam.transform.position - angleDisplayObject.transform.position;
                if (toCamera.sqrMagnitude > 1e-6f)
                {
                    forward = -toCamera.normalized; // 反向，背对相机
                }
            }
            angleDisplayObject.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);

            // 更新文本，保留一位小数
            angleDisplayText.text = $"{angle:F1}°";
        }
        
        /// <summary>
        /// 隐藏角度UI
        /// </summary>
        private void HideAngleDisplay()
        {
            if (angleDisplayObject != null)
            {
                angleDisplayObject.SetActive(false);
            }
        }
        
        /// <summary>
        /// 创建截面棱预览组件
        /// </summary>
        private void CreateSectionEdgePreview()
        {
            var previewObj = new GameObject("ADBrush_SectionEdgePreview");
            previewObj.transform.SetParent(transform, worldPositionStays: false);
            sectionEdgePreview = previewObj.AddComponent<SectionEdgePreview>();
            sectionEdgePreview.Initialize(null, new Color(0.3f, 0.9f, 0.3f, 0.8f), 0.002f);
        }
        
        /// <summary>
        /// 更新截面棱预览
        /// </summary>
        private void UpdateSectionEdgePreview()
        {
            if (sectionEdgePreview == null || shapeBuilder == null || currentMorphableShape == null) return;
            
            // 只有在按下扳机且有至少一个确认点后才显示截面棱预览
            // 空闲时不显示
            if (!isTriggerPressed || shapeBuilder.PointCount < 1)
            {
                sectionEdgePreview.SetVisible(false);
                return;
            }
            
            // 获取上一个确认点的数据
            var lastPoint = shapeBuilder.Points[shapeBuilder.PointCount - 1];
            var lastWeights = shapeBuilder.GetWeightsForPointIndex(shapeBuilder.PointCount - 1);
            float lastWidth = shapeBuilder.GetSizeForPointIndex(shapeBuilder.PointCount - 1);
            
            // 获取上一个确认点的截面形状
            var lastShape = currentMorphableShape.GetBrushShape(lastWeights);
            Vector2[] lastShapeVerts = lastShape?.vertices?.ToArray();
            
            if (lastShapeVerts == null || lastShapeVerts.Length == 0)
            {
                sectionEdgePreview.SetVisible(false);
                return;
            }
            
            // 设置上一个锚点数据
            sectionEdgePreview.SetLastAnchor(lastPoint.position, lastPoint.rotation, lastShapeVerts, lastWidth);
            
            // 计算当前位置的截面数据
            Vector3 currentPos;
            Quaternion currentRot;
            float currentWidth = ribbonWidth;
            
            if (isTriggerPressed)
            {
                // 按下时：使用按下位置和当前计算的旋转
                currentPos = triggerPressPosition;
                currentRot = GetCurrentSectionRotation();
            }
            else
            {
                // 空闲时：使用控制器位置和旋转
                currentPos = GetDrawingPosition();
                currentRot = GetControllerRotation();
            }
            
            // 获取当前权重的截面形状
            var currentWeights = new List<float>(shapeBuilder.MorphWeights);
            var currentShape = currentMorphableShape.GetBrushShape(currentWeights);
            Vector2[] currentShapeVerts = currentShape?.vertices?.ToArray();
            
            if (currentShapeVerts == null || currentShapeVerts.Length == 0)
            {
                sectionEdgePreview.SetVisible(false);
                return;
            }
            
            // 获取前一个锚点位置（用于Catmull-Rom插值）
            Vector3? prevAnchorPos = null;
            if (shapeBuilder.PointCount >= 2)
            {
                prevAnchorPos = shapeBuilder.Points[shapeBuilder.PointCount - 2].position;
            }
            
            // 更新预览
            sectionEdgePreview.UpdatePreview(currentPos, currentRot, currentShapeVerts, currentWidth, prevAnchorPos);
        }
        
        /// <summary>
        /// 获取当前截面的旋转（与实际绘制时使用的逻辑一致）
        /// </summary>
        private Quaternion GetCurrentSectionRotation()
        {
            Vector3 currentPos = GetDrawingPosition();
            float dragDistance = Vector3.Distance(currentPos, triggerPressPosition);
            Vector3 dragDirection = (currentPos - triggerPressPosition).normalized;
            
            if (dragDistance < 0.01f)
            {
                dragDirection = triggerPressRotation * Vector3.forward;
            }
            
            const float minDragDistanceForRolling = 0.1f;
            Quaternion rotation;
            
            if (dragDistance >= minDragDistanceForRolling)
            {
                // 长拖拽：先从上一个锚点无扭转转过来，再应用累积的rolling角度
                if (hasConfirmedFirstPoint && shapeBuilder != null && shapeBuilder.PointCount > 0)
                {
                    var lastPoint = shapeBuilder.GetLastConfirmedPoint();
                    if (lastPoint.HasValue)
                    {
                        Quaternion baseRotation = ADBrushShapePoint.RotateWithoutTwist(lastPoint.Value.rotation, dragDirection);
                        rotation = Quaternion.AngleAxis(accumulatedRollingAngle, dragDirection) * baseRotation;
                    }
                    else
                    {
                        Vector3 baseUp = Vector3.ProjectOnPlane(Vector3.up, dragDirection).normalized;
                        if (baseUp.sqrMagnitude < 1e-6f) baseUp = Vector3.Cross(dragDirection, Vector3.right).normalized;
                        rotation = Quaternion.AngleAxis(accumulatedRollingAngle, dragDirection) * Quaternion.LookRotation(dragDirection, baseUp);
                    }
                }
                else
                {
                    Quaternion baseRotation = Quaternion.LookRotation(dragDirection, triggerPressRotation * Vector3.up);
                    rotation = Quaternion.AngleAxis(accumulatedRollingAngle, dragDirection) * baseRotation;
                }
            }
            else
            {
                // 短拖拽：从上一个锚点无扭转转过来
                if (hasConfirmedFirstPoint && shapeBuilder != null && shapeBuilder.PointCount > 0)
                {
                    var lastPoint = shapeBuilder.GetLastConfirmedPoint();
                    if (lastPoint.HasValue)
                    {
                        rotation = ADBrushShapePoint.RotateWithoutTwist(lastPoint.Value.rotation, dragDirection);
                    }
                    else
                    {
                        Vector3 upDirection = Vector3.ProjectOnPlane(Vector3.up, dragDirection).normalized;
                        if (upDirection.sqrMagnitude < 1e-6f) upDirection = Vector3.Cross(dragDirection, Vector3.right).normalized;
                        rotation = Quaternion.LookRotation(dragDirection, upDirection);
                    }
                }
                else
                {
                    rotation = Quaternion.LookRotation(dragDirection, triggerPressRotation * Vector3.up);
                }
            }
            
            return rotation;
        }
        
        /// <summary>
        /// 更新曲线末端预览位置（该静态末端预览已废弃，不再使用）
        /// </summary>
        private void UpdateEndPointPreview()
        {
            // 用户不再需要“列表最后一个元素”的静态末端预览，这里直接隐藏并退出
            if (endPointPreview != null)
            {
                endPointPreview.Hide();
            }
        }
    }
}
