using System.Collections.Generic;
using UnityEngine;
using VRBrush.Core.Model;
using VRBrush.Core.Visual;
using VRBrush.Interface;
using VRBrush.Util;

namespace VRBrush.Interface
{
    /// <summary>
    /// 笔刷编辑器控制器 - 专门用于编辑和测试笔刷参数
    /// 不继承BaseBrushController，具有独立的编辑器特有功能
    /// </summary>
    public class BrushEditorController : MonoBehaviour, IBrushController
    {
        #region IBrushController Implementation
        public string BrushName => "BrushEditor";
        public string ControllerName => BrushName; // IInterfaceController implementation
        public float BrushDistance { get; set; } = 0f;
        public float BrushAngleUp { get; set; } = 0f;
        public float BrushWidth { get; set; } = 0.02f;
        public Vector2 BrushWidthRange => new Vector2(0.001f, 4.0f);

        public void SetBrushDistance(float distance) { BrushDistance = distance; }
        public void SetBrushAngleUp(float angle) { BrushAngleUp = angle; }
        public void SetBrushWidth(float width) { BrushWidth = width; }
    public void ClearAllStrokes() { graphBuilder?.ClearGraph(); }
        public void SetInputSource(GameObject inputSource) { inputSourceObject = inputSource; }
        public void SetStrokesParent(Transform parent) { /* 编辑器不需要stroke parent */ }
        #endregion

        #region Properties
        public bool IsInEditMode => isInEditMode;
        public bool IsDrawing => isInEditMode && isDrawingState; // Drawing when in edit mode and drawing state
        public Vector3 RightControllerPosition => inputSourceObject?.transform.position ?? Vector3.zero;
        public Vector3 LeftControllerPosition => leftController?.position ?? Vector3.zero;
        public Quaternion LeftControllerRotation => leftController?.rotation ?? Quaternion.identity;
        public int PointCount => graphBuilder?.NodeCount ?? 0;
        public int EdgeCount => graphBuilder?.EdgeCount ?? 0;
        #endregion

        #region Serialized Fields
        [Header("编辑器参数")]
        [SerializeField] private float coordinateSystemSize = 0.1f;
        [SerializeField] private float pointRadius = 0.003f;
        [SerializeField] private float lineRadius = 0.001f;
        [SerializeField] private float snapThreshold = 0.05f;
        [SerializeField] private float connectThreshold = 0.03f;
        [SerializeField] private float removeThreshold = 0.02f;
        [SerializeField] private float smoothingFactor = 10f; // 控制器跟随平滑因子
        [SerializeField] private Material pointMaterial;
        [SerializeField] private Material edgeMaterial;
        [SerializeField] private Material coordinateMaterial;
        [SerializeField] private bool autoActivateEditMode = false;
        [SerializeField] private bool liftGeometryAbovePlane = true;
        [SerializeField] private bool addCenterIndicator = true;
        [SerializeField] private bool verboseDebug = true;
        #endregion

        #region Private Fields
        private BrushShapeBuilder graphBuilder;
        private BrushShape currentBrushShape;
        private BrushEditorDisplay editorDisplay;
        private GameObject inputSourceObject; // previously provided by BaseBrushController in other controllers
    // Legacy fields kept for compatibility but no longer used for rendering
    private GameObject coordinateSystem;
    private float editingPlaneHalfThickness = 0f;
    private float geometryLift = 0f;
        private bool isInEditMode = false;
        // Left controller (set via SetLeftInputSource)
        private Transform leftController;
        private Vector3 smoothedLeftControllerPosition;
    private Quaternion smoothedLeftControllerRotation;
        // Input state tracking
    private bool previousAState = false;     // A键上一帧状态（primaryButton）
    private bool previousBState = false;     // B键上一帧状态（secondaryButton）
    private bool isDrawingState = false; // 当前是否处于绘制状态
    // trigger tracking similar to BaseBrushController
    [System.NonSerialized]
    private bool previousTriggerState = false;
    // Initialization state (similar to MorphEditorController)
    private bool isDisplayInitialized = false;
        #endregion

        #region Unity Lifecycle
        protected virtual void Awake()
        {
            // 初始化图结构
            string graphName = $"BrushShape_{System.DateTime.Now:yyyyMMdd_HHmmss}";
            graphBuilder = new BrushShapeBuilder(new BrushShape(graphName));

            // 初始化显示系统
            CreateEditorDisplay();

            // 初始化当前笔刷形状
            if (currentBrushShape == null)
            {
                currentBrushShape = new BrushShape(graphName);
            }

            Debug.Log($"BrushEditorController Awake: instanceId={this.GetInstanceID()}, gameObject.name={gameObject.name}");
        }

        protected virtual void Start()
        {
            // 完成显示系统的初始化，包括订阅事件
            // Skip if already initialized in OnEnable (happens when component was enabled after creation)
            if (!isDisplayInitialized && editorDisplay != null)
            {
                // Pass leftController transform (may be null). BrushEditorDisplay stores the transform internally.
                editorDisplay.Initialize(graphBuilder, leftController);

                // Apply controller serialized settings to display so the fields are actually used
                editorDisplay.ApplyControllerSettings(coordinateSystemSize, pointRadius, lineRadius, snapThreshold, connectThreshold, removeThreshold, addCenterIndicator, verboseDebug, smoothingFactor);

                // 订阅图变化事件
                graphBuilder.OnNodeCreated += OnNodeCreated;
                graphBuilder.OnEdgeCreated += OnEdgeCreated;
                graphBuilder.OnNodeRemoved += OnNodeRemoved;
                graphBuilder.OnPreviousNodeChanged += OnPreviousNodeChanged;
                
                isDisplayInitialized = true;
            }
        }

        void OnEnable()
        {
            if (coordinateSystem == null)
                CreateCoordinateSystem();

            // Initialize display system if not already done (similar to MorphEditorController)
            // This ensures display is properly initialized when the controller is enabled,
            // especially if Start() was never called (component was disabled at start)
            if (!isDisplayInitialized && editorDisplay != null && graphBuilder != null)
            {
                editorDisplay.Initialize(graphBuilder, leftController);
                editorDisplay.ApplyControllerSettings(
                    coordinateSystemSize, pointRadius, lineRadius,
                    snapThreshold, connectThreshold, removeThreshold,
                    addCenterIndicator, verboseDebug, smoothingFactor
                );
                
                // Subscribe to graph events
                graphBuilder.OnNodeCreated += OnNodeCreated;
                graphBuilder.OnEdgeCreated += OnEdgeCreated;
                graphBuilder.OnNodeRemoved += OnNodeRemoved;
                graphBuilder.OnPreviousNodeChanged += OnPreviousNodeChanged;
                
                isDisplayInitialized = true;
                Debug.Log("BrushEditorController.OnEnable: Initialized editorDisplay.");
            }

            if (autoActivateEditMode && !isInEditMode)
            {
                SetEditMode(true);
            }
        }

        protected virtual void Update()
        {
            if (!isInEditMode) return;

            // 更新坐标系跟随左手控制器
            if (editorDisplay != null)
            {
                editorDisplay.UpdateCoordinateSystem();
            }
            
            // 更新右手控制器投影几何
            if (editorDisplay != null && inputSourceObject != null)
            {
                BrushEditorPoint lastPoint = null;
                if (graphBuilder != null && graphBuilder.PreviousNodeIndex.HasValue)
                {
                    lastPoint = editorDisplay.GetPointByIndex(graphBuilder.PreviousNodeIndex.Value);
                }
                editorDisplay.UpdateProjectionGeometry(inputSourceObject.transform, lastPoint);
            }

            // 处理右手控制器输入
            HandleRightControllerInput();
        }
        #endregion

        #region Display System
        private void CreateEditorDisplay()
        {
            var displayGO = new GameObject("BrushEditorDisplay");
            displayGO.transform.SetParent(transform);
            editorDisplay = displayGO.AddComponent<BrushEditorDisplay>();
        }
        #endregion

        #region Coordinate System
        private void CreateCoordinateSystem()
        {
            // 由 BrushEditorDisplay 管理坐标系，这里保留兼容但不创建重复对象
            coordinateSystem = null;
            editingPlaneHalfThickness = 0.002f;
            geometryLift = liftGeometryAbovePlane ? (editingPlaneHalfThickness + 0.0002f) : 0f;
            Debug.Log($"BrushEditorController: CoordinateSystem handled by BrushEditorDisplay on {gameObject.name}");
        }

        private void UpdateCoordinateSystem()
        {
            // 委托给显示层更新坐标系
            if (editorDisplay == null || leftController == null) return;

            editorDisplay.UpdateCoordinateSystem();
        }

        private void CreateTransparentMaterials()
        {
            if (coordinateMaterial == null)
            {
                coordinateMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                coordinateMaterial.SetFloat("_Mode", 3f);
                coordinateMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                coordinateMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                coordinateMaterial.SetInt("_ZWrite", 0);
                coordinateMaterial.EnableKeyword("_ALPHABLEND_ON");
                coordinateMaterial.renderQueue = 3000;
                coordinateMaterial.color = new Color(0.5f, 0.8f, 1f, 0.3f);
            }
        }
        #endregion

        #region Graph Events
        private void OnNodeCreated(int nodeIndex)
        {
            Debug.Log($"BrushEditor: Node created at index {nodeIndex}");
        }

        private void OnEdgeCreated(Vector2Int edge)
        {
            Debug.Log($"BrushEditor: Edge created between nodes {edge.x} and {edge.y}");
        }

        private void OnNodeRemoved(int nodeIndex)
        {
            Debug.Log($"BrushEditor: Node removed at index {nodeIndex}");
        }

        private void OnPreviousNodeChanged(int nodeIndex)
        {
            Debug.Log($"BrushEditor: Previous node changed to {(nodeIndex >= 0 ? nodeIndex.ToString() : "None")}");
        }
        #endregion

        #region Public Interface
        public void SetEditMode(bool enabled)
        {
            Debug.Log($"BrushEditorController: SetEditMode called with {enabled} on {gameObject.name}");

            if (isInEditMode == enabled)
            {
                Debug.Log($"BrushEditorController: Already in edit mode {enabled}, skipping");
                return;
            }

            isInEditMode = enabled;
            
            // 控制显示系统的可见性
            if (editorDisplay != null)
            {
                editorDisplay.SetVisible(enabled);
            }
            
            // 控制旧的坐标系（如果还存在）
            // 坐标系可见性交由显示层控制
                
            if (enabled && leftController != null)
            {
                smoothedLeftControllerPosition = leftController.position;
                smoothedLeftControllerRotation = leftController.rotation;
                editorDisplay?.UpdateCoordinateSystem();
            }
            
            if (enabled)
            {
                // 清除当前图形，开始新的编辑
                if (graphBuilder != null)
                {
                        graphBuilder.ClearGraph();
                }
                isDrawingState = false;
                    SetPreviousTriggerState(false);
                Debug.Log("BrushEditorController: Edit mode enabled, cleared current graph");
            }
        }

        public void HandlePrimaryButtonShortPress(Vector3 worldPoint)
        {
            if (!isInEditMode || editorDisplay?.CoordinateTransform == null) return;

            Vector2 localPoint = BrushShapeBuilder.WorldToLocalPosition(worldPoint, editorDisplay.CoordinateTransform);
            graphBuilder.HandlePrimaryButtonShortPress(worldPoint, localPoint);
            SyncGraphRuntime();
        }

        public void HandlePrimaryButtonLongPress(Vector3 worldPoint)
        {
            if (!isInEditMode || editorDisplay?.CoordinateTransform == null) return;

            Vector2 localPoint = BrushShapeBuilder.WorldToLocalPosition(worldPoint, editorDisplay.CoordinateTransform);
            // BrushShapeBuilder exposes HandlePrimaryButtonLongPressEnd(...) for long-press behavior
            graphBuilder.HandlePrimaryButtonLongPressEnd(localPoint, connectThreshold);
            SyncGraphRuntime();
        }
        #endregion

        #region Input Handling (Right Controller)
        
        // 输入状态跟踪
        private bool isLongPressing = false;
        private float longPressTimer = 0f;
        private const float LONG_PRESS_DURATION = 0.5f;
        private int? highlightedNodeIndex = null;
        
        private void HandleRightControllerInput()
        {
            if (inputSourceObject == null) return;

            // 使用XR按钮作为BrushEditor的三种模式输入：A(主) / B(副)
            bool currentAButton = GetAButtonValue();      // A = primaryButton（右手）
            bool currentBButton = GetBButtonValue();      // B = secondaryButton（右手）

            if (verboseDebug)
            {
                Debug.Log($"BrushEditor Input: A={currentAButton} (prev={previousAState}), B={currentBButton} (prev={previousBState}), drawing={isDrawingState}");
            }

            // 处理B键点击（结束绘制链）
            if (currentBButton && !previousBState)
            {
                OnTriggerPressed();
                if (verboseDebug) Debug.Log("BrushEditor: B键被按下，结束绘制链");
            }

            // 处理A键点击（修复的短按/长按逻辑）
            if (currentAButton && !previousAState)
            {
                // 按下时仅开始计时，不立即执行短按
                longPressTimer = 0f;
                isLongPressing = false;
                if (verboseDebug) Debug.Log("BrushEditor: A键按下，开始计时");
            }
            else if (currentAButton)
            {
                // 持续按住时更新计时器
                longPressTimer += Time.deltaTime;
                if (longPressTimer >= LONG_PRESS_DURATION && !isLongPressing)
                {
                    isLongPressing = true;
                    OnLongPressStarted();
                    if (verboseDebug) Debug.Log("BrushEditor: 进入长按模式");
                }
            }
            else if (!currentAButton && previousAState)
            {
                // 松开时根据是否长按决定执行哪个操作
                if (isLongPressing)
                {
                    OnLongPressEnded();
                    if (verboseDebug) Debug.Log("BrushEditor: 长按结束");
                }
                else
                {
                    // 只有在松开时才执行短按操作
                    OnPrimaryButtonPressed();
                    if (verboseDebug) Debug.Log("BrushEditor: 短按执行");
                }
                isLongPressing = false;
                longPressTimer = 0f;
            }

            // 更新前一帧状态
            previousAState = currentAButton;
            previousBState = currentBButton;

            // 处理长按期间的高亮
            if (isLongPressing)
            {
                UpdateHighlightedNode();
            }
        }

        private void OnPrimaryButtonPressed()
        {
            Vector3 worldPoint = GetRightControllerPlanePosition();
            if (worldPoint == Vector3.zero)
            {
                if (verboseDebug) Debug.Log("BrushEditor: A键按下但投影点不在平面上");
                return; // 不在平面上
            }

            if (editorDisplay?.CoordinateTransform == null)
            {
                if (verboseDebug) Debug.Log("BrushEditor: A键按下但坐标系未初始化");
                return;
            }

            Vector2 localPoint = BrushShapeBuilder.WorldToLocalPosition(worldPoint, editorDisplay.CoordinateTransform);

            // 通过 BrushShapeBuilder 创建节点
            bool hadPreviousNode = graphBuilder.HasPreviousNode;
            int? prevNodeIndex = graphBuilder.PreviousNodeIndex;
            int newNodeIndex = graphBuilder.HandlePrimaryButtonShortPress(worldPoint, localPoint);
            isDrawingState = true;

            if (verboseDebug) 
            {
                Debug.Log($"BrushEditor: A键短按创建节点 {newNodeIndex} 在位置 {worldPoint}");
                Debug.Log($"BrushEditor: 前一个节点: {(hadPreviousNode ? prevNodeIndex.ToString() : "无")}, 总节点数: {graphBuilder.NodeCount}");
            }
        }

        private void OnLongPressStarted()
        {
            UpdateHighlightedNode();
        }

        private void OnLongPressEnded()
        {
            if (highlightedNodeIndex.HasValue && isDrawingState)
            {
                Vector3 worldPoint = GetRightControllerPlanePosition();
                if (worldPoint != Vector3.zero && editorDisplay?.CoordinateTransform != null)
                {
                    Vector2 localPoint = BrushShapeBuilder.WorldToLocalPosition(worldPoint, editorDisplay.CoordinateTransform);
                    // 连接到高亮的节点
                    graphBuilder.HandlePrimaryButtonLongPressEnd(localPoint, connectThreshold);
                    if (verboseDebug) Debug.Log($"BrushEditor: A键长按结束，连接到节点 {highlightedNodeIndex.Value}");
                }
            }
            else if (verboseDebug)
            {
                Debug.Log($"BrushEditor: A键长按结束但无有效连接目标 (highlighted={highlightedNodeIndex}, drawing={isDrawingState})");
            }
            
            // 清除高亮状态
            editorDisplay?.SetHighlightedNode(null);
            highlightedNodeIndex = null;
            highlightedNodeIndex = null;
        }

        private void OnTriggerPressed()
        {
            if (!isDrawingState) return;

            // 结束当前绘制链 - 清空previousNode以断开连续绘制
            graphBuilder.ClearPreviousNode();
            isDrawingState = false;
            
            if (verboseDebug)
            {
                Debug.Log($"BrushEditor: 结束绘制链，当前有 {PointCount} 个点，{EdgeCount} 条边");
            }
        }

        // Trigger previous state helpers (mirror of BaseBrushController) ----------------
        protected bool GetPreviousTriggerState()
        {
            return previousTriggerState;
        }

        protected void SetPreviousTriggerState(bool state)
        {
            previousTriggerState = state;
        }

        private void UpdateHighlightedNode()
        {
            Vector3 worldPoint = GetRightControllerPlanePosition();
            if (worldPoint == Vector3.zero || editorDisplay?.CoordinateTransform == null) return;

            Vector2 localPoint = BrushShapeBuilder.WorldToLocalPosition(worldPoint, editorDisplay.CoordinateTransform);
            
            // 查找最近的节点
            int nearestIndex = graphBuilder.FindNearestNodeForConnection(localPoint, snapThreshold);
            highlightedNodeIndex = nearestIndex >= 0 ? nearestIndex : null;

            // 同步到显示进行高亮
            if (editorDisplay != null)
            {
                editorDisplay.SetHighlightedNode(highlightedNodeIndex);
            }
        }

        private Vector3 GetRightControllerPlanePosition()
        {
            if (inputSourceObject == null || editorDisplay == null || editorDisplay.CoordinateTransform == null)
                return Vector3.zero;

            Transform rightController = inputSourceObject.transform;
            var ct = editorDisplay.CoordinateTransform;

            // 使用与BrushEditorDisplay一致的投影方式：垂直投影到平面
            Vector3 planeNormal = ct.up;
            Vector3 planePoint = ct.position;
            Plane editingPlane = new Plane(planeNormal, planePoint);

            // 垂直投影到平面
            Vector3 tipWorld = rightController.position;
            float signedDistance = editingPlane.GetDistanceToPoint(tipWorld);
            Vector3 worldHit = tipWorld - editingPlane.normal * signedDistance;

            return worldHit;
        }

        #endregion

        #region Editor Public API (used by UI/VRBrushSystem)
        /// <summary>
        /// Expose coordinate system size to UI
        /// </summary>
        public float CoordinateSystemSize
        {
            get => coordinateSystemSize;
            set
            {
                coordinateSystemSize = value;
                if (editorDisplay != null)
                {
                    // If the display supports updating its size, try to call a method; otherwise display reads size on next init
                    var method = editorDisplay.GetType().GetMethod("SetCoordinateSystemSize");
                    if (method != null) method.Invoke(editorDisplay, new object[] { coordinateSystemSize });
                }
            }
        }

        /// <summary>
        /// Expose smoothing factor to UI
        /// </summary>
        public float SmoothingFactor
        {
            get => smoothingFactor;
            set
            {
                smoothingFactor = value;
                if (editorDisplay != null)
                {
                    // Update the display with new smoothing factor
                    var method = editorDisplay.GetType().GetMethod("SetSmoothingFactor");
                    if (method != null) method.Invoke(editorDisplay, new object[] { smoothingFactor });
                }
            }
        }

        /// <summary>
        /// Return build/stats string for UI
        /// </summary>
        public string GetDataStats()
        {
            return graphBuilder != null ? graphBuilder.GetBuildStats() : string.Empty;
        }

        /// <summary>
        /// Save current brush/graph to file (delegates to BrushShapeBuilder/BrushShape)
        /// </summary>
        public bool SaveCurrentBrush(string fileName)
        {
            // Ensure runtime graph is synced to currentBrushShape
            SyncGraphRuntime();
            if (graphBuilder != null)
            {
                return graphBuilder.SaveToFile(fileName);
            }
            return false;
        }

        /// <summary>
        /// Called reflectively by VRBrushSystem to set left-hand controller GameObject
        /// </summary>
        public void SetLeftInputSource(GameObject leftControllerObject)
        {
            if (leftControllerObject != null)
            {
                leftController = leftControllerObject.transform;
                Debug.Log($"BrushEditorController.SetLeftInputSource: Setting left controller to '{leftControllerObject.name}'");
                
                // 初始化平滑位置
                smoothedLeftControllerPosition = leftController.position;
                smoothedLeftControllerRotation = leftController.rotation;
            }
            else
            {
                Debug.LogWarning("BrushEditorController.SetLeftInputSource: Received null left controller object.");
                leftController = null;
            }

            // Inform display if initialized
            if (editorDisplay != null)
            {
                Debug.Log($"BrushEditorController: (Re)initializing display with left controller: {(leftController != null ? leftController.name : "NULL")}");
                editorDisplay.Initialize(graphBuilder, leftController);
            }
        }
        #endregion

        #region Graph Editing Helpers
        private void SyncGraphRuntime()
        {
            currentBrushShape.graphNodes.Clear();
            currentBrushShape.graphNodes.AddRange(graphBuilder.GetNodes());

            currentBrushShape.graphEdges.Clear();
            currentBrushShape.graphEdges.AddRange(graphBuilder.GetEdges());
        }
        #endregion

        #region Helper Methods
        public BrushShape GetCurrentBrushShape()
        {
            SyncGraphRuntime();
            return currentBrushShape;
        }

        private Vector3 PolarToCartesian(Vector2 polar)
        {
            Vector2 cartesian2D = VRBrush.Util.Math2D.PolarToCartesian(polar.x, polar.y);
            return new Vector3(cartesian2D.x, geometryLift, cartesian2D.y);
        }

        /// <summary>
        /// 获取辅助按钮状态（通常是B键）
        /// </summary>
        private bool GetSecondaryButtonValue()
        {
            // 尝试通过XR输入系统获取辅助按钮状态
            try
            {
                var devices = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();
                UnityEngine.XR.InputDevices.GetDevicesAtXRNode(UnityEngine.XR.XRNode.RightHand, devices);
                if (devices.Count > 0)
                {
                    var device = devices[0];

                    // 尝试获取Menu按钮（通常是B键）
                    if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.menuButton, out bool menuButton))
                    {
                        return menuButton;
                    }

                    // 尝试获取Secondary2DAxisClick（通常是右摇杆按下）
                    if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondary2DAxisClick, out bool secondary2DAxisClick))
                    {
                        return secondary2DAxisClick;
                    }
                }
            }
            catch (System.Exception ex)
            {
                if (verboseDebug)
                {
                    Debug.LogWarning($"BrushEditor: Could not read secondary button: {ex.Message}");
                }
            }
            return false;
        }

        private bool GetAButtonValue()
        {
            // A键：右手 primaryButton（Oculus/Meta: A 按钮）
            try
            {
                var devices = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();
                UnityEngine.XR.InputDevices.GetDevicesAtXRNode(UnityEngine.XR.XRNode.RightHand, devices);
                if (devices.Count > 0)
                {
                    var device = devices[0];
                    if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool primaryButton))
                    {
                        return primaryButton;
                    }
                }
            }
            catch (System.Exception ex)
            {
                if (verboseDebug) Debug.LogWarning($"BrushEditor: Read A(primaryButton) failed: {ex.Message}");
            }
            return false;
        }

        private bool GetBButtonValue()
        {
            // B键：右手 secondaryButton（Oculus/Meta: B 按钮）
            try
            {
                var devices = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();
                UnityEngine.XR.InputDevices.GetDevicesAtXRNode(UnityEngine.XR.XRNode.RightHand, devices);
                if (devices.Count > 0)
                {
                    var device = devices[0];
                    if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool secondaryButton))
                    {
                        return secondaryButton;
                    }
                    // 兜底：某些设备可能把B映射为menuButton或摇杆按下
                    if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.menuButton, out bool menuButton) && menuButton)
                        return true;
                    if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondary2DAxisClick, out bool secondary2DAxisClick) && secondary2DAxisClick)
                        return true;
                }
            }
            catch (System.Exception ex)
            {
                if (verboseDebug) Debug.LogWarning($"BrushEditor: Read B(secondaryButton) failed: {ex.Message}");
            }
            return false;
        }
        #endregion
    }
}