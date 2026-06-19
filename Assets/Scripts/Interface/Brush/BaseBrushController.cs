using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Samples.StarterAssets;
using VRBrush.Core;
using VRBrush.Core.Model;
using VRBrush.Core.Input;
using VRBrush.Interface;
using System.Collections.Generic;

namespace VRBrush.Interface
{
    /// <summary>
    /// 基础笔刷控制器的具体实现。
    /// 负责处理通用的绘制流程、状态管理、输入处理以及预览和视觉反馈的组合。
    /// 提供标准的笔刷位置控制方法。
    /// </summary>
    public class BaseBrushController : MonoBehaviour, IBrushController
    {
    [Header("Base Brush Settings")]
        [SerializeField] protected float ribbonWidth = 0.02f;
        [SerializeField] protected Vector2 ribbonWidthRange = new Vector2(0.001f, 4.0f);
        [SerializeField] protected Material ribbonMaterial;
        [SerializeField] protected Color brushColor = Color.blue;
        [SerializeField] protected float minPointDistance = 0.005f;

        [Header("Brush Position Settings")]
        [SerializeField] protected float brushDistance = 0.05f;
        [SerializeField, Range(0f, 90f)] protected float brushAngleUp = 30f;
        [SerializeField, Range(0f, 90f)] protected float brushAngleRight = 0f;

        [Header("Input Settings")]
        [SerializeField] protected float triggerThreshold = 0.5f; // 扳机阈值，0-1之间

    [Header("Preview")]
    [SerializeField] protected bool showBrushPreview = true;
    [SerializeField] protected GameObject brushPreviewPrefab;
    [SerializeField] protected bool showPreviewWhileDrawing = false; // 控制绘制时是否显示预览，默认为false

        [Header("Debug")]
        [SerializeField] protected bool showDebugInfo = false;

        // 输入源引用
        protected GameObject inputSourceObject;
        protected Transform controllerTransform;
        // XRI组件引用
        protected UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor controllerInputManager;
        protected UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor controllerInteractor;
        // 输入管理器
    protected VRBrush.Core.Input.VRInputManagerSimple simpleInputManager;

        // 绘制状态
        protected bool isDrawing = false;
        protected Stroke currentStroke;
        protected GameObject currentStrokeObject;
        protected List<GameObject> completedStrokes = new List<GameObject>();
        protected int pointCount = 0;

        // 笔画管理
        private Transform strokesParent; // 所有笔画的父对象
        private const string STROKES_PARENT_NAME = "Strokes";
        private const string BRUSH_STROKE_TAG = "BrushStroke"; // 统一的笔画标签

        // 预览和反馈
    // 统一仅使用 UniversalBrushPreview
    protected UniversalBrushPreview universalBrushPreview; // 通用预览系统（唯一）

        // 摇杆输入相关
        [Header("Joystick Accumulation Settings")]
        [SerializeField, Tooltip("摇杆累积衰减系数（接近1表示恢复更慢）")] protected float joystickDecay = 0.98f; // 恢复为原始较快回弹
        protected Vector2 currentJoystickInput = Vector2.zero;
        protected Quaternion joystickAdjustedRotation = Quaternion.identity;
        protected bool useUniversalPreview = true; // 是否使用通用预览系统

    // 上下文信息（移除 BrushContext，直接使用原始参数）
        protected Vector3 lastBrushPosition;
        protected float lastTime;
        // 绘制时用于预览朝向的缓存（来自最新笔迹点的切线与生成线）
        protected Vector3 lastPreviewTangent = Vector3.zero;
        protected Vector3 lastPreviewRuling = Vector3.zero;
        protected bool hasPreviewOrientation = false;

        #region IBrushController Implementation
        public virtual string BrushName => "BaseBrush";
        public string ControllerName => BrushName; // IInterfaceController implementation
        public float BrushDistance => brushDistance;
        public float BrushAngleUp => brushAngleUp;
        public float BrushWidth => ribbonWidth;
        public Vector2 BrushWidthRange => ribbonWidthRange;

        public virtual void SetBrushDistance(float distance) => this.brushDistance = distance;
        public virtual void SetBrushAngleUp(float angle) => this.brushAngleUp = angle;
        public virtual void SetBrushWidth(float width)
        {
            ribbonWidth = Mathf.Clamp(width, ribbonWidthRange.x, ribbonWidthRange.y);
            if (universalBrushPreview != null) universalBrushPreview.RibbonWidth = ribbonWidth;
        }

        public virtual void ClearAllStrokes()
        {
            foreach (var stroke in completedStrokes)
            {
                if (stroke != null) Object.DestroyImmediate(stroke);
            }
            completedStrokes.Clear();

            if (isDrawing)
            {
                StopDrawing();
            }
        }

        /// <summary>
        /// 设置笔画父对象（由VRBrushSystem调用）
        /// </summary>
        public virtual void SetStrokesParent(Transform parent)
        {
            strokesParent = parent;
            Debug.Log($"{BrushName}: 笔画父对象已设置为 '{parent.name}'。", this);
        }
        #endregion

        protected virtual void Awake()
        {
            // 设置默认材质
            if (ribbonMaterial == null)
            {
                ribbonMaterial = CreateDefaultMaterial();
            }

            // 创建笔画父对象
            CreateStrokesParent();

            // 初始化预览和反馈
            InitializePreviewAndFeedback();
        }

        protected virtual void Start()
        {
            // 子类可以重写以执行特定的Start逻辑

            // 初始化预览尺寸
            // Universal 预览尺寸初始化
            if (universalBrushPreview != null) universalBrushPreview.RibbonWidth = ribbonWidth;
        }

        protected virtual void Update()
        {
            if (showDebugInfo)
            {
                float triggerValue = GetTriggerValue();
                if (triggerValue > 0.01f) // 只显示有意义的值
                {
                    Debug.Log($"{BrushName}: Update - Trigger value: {triggerValue}, IsDrawing: {isDrawing}");
                }
            }

            HandleDrawingInput();
            HandleAngleInputFromTouchpad();
            UpdatePreviewAndFeedback();

            if (isDrawing)
            {
                UpdateCurrentStroke();
            }
        }

        private void CreateStrokesParent()
        {
            // 如果笔画父对象还没有被VRBrushSystem设置，则创建默认的
            if (strokesParent == null)
            {
                strokesParent = transform.Find(STROKES_PARENT_NAME);
                if (strokesParent == null)
                {
                    strokesParent = new GameObject(STROKES_PARENT_NAME).transform;
                    strokesParent.SetParent(transform);
                    strokesParent.localPosition = Vector3.zero;
                    Debug.Log($"{BrushName}: 创建了默认的笔画父对象。", this);
                }
            }
        }

        private void InitializePreviewAndFeedback()
        {
            if (showBrushPreview)
            {
                // 直接创建通用预览系统并强制在绘制时显示
                universalBrushPreview = new UniversalBrushPreview(transform, /*showWhileDrawing*/ true);
                universalBrushPreview.RibbonWidth = ribbonWidth;
            }
        }

        // ...existing code...

        // 视觉反馈已移除（删除 IVisualFeedback 系列），如需恢复请在子类自行实现。

        private void HandleDrawingInput()
        {
            if (controllerTransform == null)
            {
                if (showDebugInfo && Time.frameCount % 300 == 0) // 每5秒输出一次
                {
                    Debug.LogWarning($"{BrushName}: ControllerTransform is null, cannot handle input");
                }
                return;
            }

            float triggerValue = GetTriggerValue();
            bool currentTriggerPressed = triggerValue > triggerThreshold;

            // 更详细的调试信息
            if (showDebugInfo && Time.frameCount % 60 == 0) // 每秒输出一次
            {
                Debug.Log($"{BrushName}: Trigger value: {triggerValue:F3}, Threshold: {triggerThreshold:F3}, " +
                         $"Current: {currentTriggerPressed}, Previous: {previousTriggerState}, IsDrawing: {isDrawing}");
            }

            // 检测扳机按下（从未按下变为按下）
            if (currentTriggerPressed && !previousTriggerState)
            {
                if (!isDrawing)
                {
                    if (showDebugInfo) Debug.Log($"{BrushName}: Trigger pressed (value: {triggerValue}), starting drawing");
                    StartDrawing();
                }
                else
                {
                    if (showDebugInfo) Debug.Log($"{BrushName}: Trigger pressed while drawing");
                    OnTriggerPressedWhileDrawing();
                }
            }
            // 检测扳机释放（从按下变为未按下）
            else if (!currentTriggerPressed && previousTriggerState && isDrawing)
            {
                if (showDebugInfo) Debug.Log($"{BrushName}: Trigger released (value: {triggerValue})");
                OnTriggerReleasedWhileDrawing();
            }

            // 更新前一帧状态
            previousTriggerState = currentTriggerPressed;
        }

        /// <summary>
        /// 处理触控板/摇杆二维输入以调节笔刷角度（映射到 -180~180 两个轴 -> 俯仰与侧向角度）。
        /// 现在支持新的通用预览系统的摇杆控制。
        /// </summary>
        private void HandleAngleInputFromTouchpad()
        {
            // 优先通过 XR 新输入系统（如果有）尝试读取 primary2DAxis / joystick
            Vector2 axis = Vector2.zero;

            // 尝试简单输入管理器（XRNode）
            if (simpleInputManager != null)
            {
                // 使用 UnityEngine.XR_CommonUsages.primary2DAxis
                try
                {
                    var node = UnityEngine.XR.XRNode.RightHand; // TODO: 可配置
                    var devices = new System.Collections.Generic.List<UnityEngine.XR.InputDevice>();
                    UnityEngine.XR.InputDevices.GetDevicesAtXRNode(node, devices);
                    if (devices.Count > 0)
                    {
                        var dev = devices[0];
                        if (dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out Vector2 primary))
                        {
                            axis = primary;
                        }
                    }
                }
                catch { }
            }



            // 累积摇杆输入（用户要求为累积）
            // 即使当前帧没有输入，也要对已累积值做衰减，使其在停止输入后逐步回落
            float decay = Mathf.Clamp01(joystickDecay);
            currentJoystickInput *= decay;
            if (axis.sqrMagnitude > 0.0001f)
            {
                // 在有输入时根据当前轴值补偿衰减量
                currentJoystickInput += axis * (1f - decay);
            }

            // 不直接映射到 brushAngleUp/Right（这两个只控制位置相关参数，不应影响旋转）
        }

        /// <summary>
        /// 从输入源获取扳机是否刚刚按下
        /// </summary>
        private bool IsTriggerJustPressed()
        {
            if (controllerTransform == null) return false;
            // 此方法不再直接用于启动/停止，但保留以备将来使用
            float triggerValue = GetTriggerValue();
            bool currentTriggerState = triggerValue > triggerThreshold;
            bool wasTriggerPressed = GetPreviousTriggerState();
            SetPreviousTriggerState(currentTriggerState);
            return currentTriggerState && !wasTriggerPressed;
        }

        // 预览更新已整合到 UpdatePreviewAndFeedback 中，移除 BrushContext

        /// <summary>
        /// 获取扳机值
        /// </summary>
        protected float GetTriggerValue()
        {
            // 优先选择：SimpleInputManager（最稳定的输入源）
            if (simpleInputManager != null)
            {
                return simpleInputManager.GetTriggerValue();
            }



            // 尝试从ControllerInputActionManager获取输入
            if (controllerInputManager != null)
            {
                try
                {
                    // 使用反射获取selectAction属性
                    var selectActionProperty = controllerInputManager.GetType().GetProperty("selectAction");
                    if (selectActionProperty != null)
                    {
                        var selectActionRef = selectActionProperty.GetValue(controllerInputManager) as UnityEngine.InputSystem.InputActionReference;
                        if (selectActionRef != null && selectActionRef.action != null)
                        {
                            var triggerValue = selectActionRef.action.ReadValue<float>();
                            if (showDebugInfo && triggerValue > 0.1f) Debug.Log($"{BrushName}: Trigger value from ControllerInputActionManager: {triggerValue}");
                            return triggerValue;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    if (showDebugInfo) Debug.LogWarning($"Could not read selectAction from ControllerInputActionManager: {ex.Message}");
                }
            }

            // 尝试从XRBaseInteractor获取输入（使用反射）
            if (controllerInteractor != null)
            {
                try
                {
                    // 使用反射获取selectAction属性
                    var selectActionProperty = controllerInteractor.GetType().GetProperty("selectAction");
                    if (selectActionProperty != null)
                    {
                        var selectActionRef = selectActionProperty.GetValue(controllerInteractor) as UnityEngine.InputSystem.InputActionReference;
                        if (selectActionRef != null && selectActionRef.action != null)
                        {
                            var triggerValue = selectActionRef.action.ReadValue<float>();
                            if (showDebugInfo && triggerValue > 0.1f) Debug.Log($"{BrushName}: Trigger value from {controllerInteractor.GetType().Name}: {triggerValue}");
                            return triggerValue;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    if (showDebugInfo) Debug.LogWarning($"Could not read selectAction from {controllerInteractor.GetType().Name}: {ex.Message}");
                }
            }

            // 如果所有方法都失败，返回0
            if (showDebugInfo && Time.frameCount % 300 == 0) // 每5秒输出一次警告
            {
                Debug.LogWarning($"{BrushName}: Unable to get trigger input from any source. " +
                               $"SimpleInputManager: {(simpleInputManager != null ? "Found" : "Missing")}, " +
                               $"ControllerInputManager: {(controllerInputManager != null ? "Found" : "Missing")}, " +
                               $"ControllerInteractor: {(controllerInteractor != null ? controllerInteractor.GetType().Name : "Missing")}");
            }
            return 0f;
        }

    // 用于跟踪前一帧的扳机状态
    [System.NonSerialized]
    private bool previousTriggerState = false;

        protected bool GetPreviousTriggerState()
        {
            return previousTriggerState;
        }

        protected void SetPreviousTriggerState(bool state)
        {
            previousTriggerState = state;
        }

        protected virtual void StartDrawing()
        {
            if (isDrawing) return; // 防止重复开始

            isDrawing = true;
            pointCount = 0;
            lastTime = Time.time;

            // 创建新的笔画数据
            currentStroke = CreateStroke();
            if (currentStroke == null)
            {
                currentStroke = new Stroke();
            }
            currentStroke.color = brushColor;
            currentStroke.material = ribbonMaterial;

            // 创建笔画GameObject，并设置父对象和标签
            currentStrokeObject = new GameObject($"{BrushName}_Stroke_{System.DateTime.Now:HHmmss}");
            currentStrokeObject.transform.SetParent(strokesParent);
            currentStrokeObject.tag = BRUSH_STROKE_TAG;

            // 添加Mesh组件
            currentStrokeObject.AddComponent<MeshFilter>();
            var meshRenderer = currentStrokeObject.AddComponent<MeshRenderer>();
            meshRenderer.material = ribbonMaterial;

            // 记录初始位置
            lastBrushPosition = GetDrawingPosition();

            // 在第一帧就为预览设置初始朝向（使用控制器前向作为切线，调用算法计算ruling）
            try
            {
                Vector3 initialTangent = GetControllerForwardAxis();
                Vector3 initialRuling = ComputeRulingDirection(initialTangent, lastBrushPosition);
                if (initialRuling.sqrMagnitude < 1e-6f) initialRuling = GetControllerUpAxis();
                lastPreviewTangent = initialTangent.normalized;
                lastPreviewRuling = initialRuling.normalized;
                hasPreviewOrientation = true;
            }
            catch { /* 若子类算法抛错，不影响开始绘制 */ }

            // 调用子类的钩子
            OnStrokeStart();

            if (showDebugInfo)
            {
                Debug.Log($"{BrushName}: 开始绘制新笔画 at position {lastBrushPosition}");
                Debug.Log($"{BrushName}: Stroke parent: {(strokesParent != null ? strokesParent.name : "null")}");
                Debug.Log($"{BrushName}: Material: {(ribbonMaterial != null ? ribbonMaterial.name : "null")}");
            }
        }

        protected virtual void UpdateCurrentStroke()
        {
            if (currentStroke == null)
            {
                if (showDebugInfo) Debug.LogWarning($"{BrushName}: UpdateCurrentStroke called but currentStroke is null");
                return;
            }

            Vector3 currentPos = GetDrawingPosition();

            if (currentStroke.points == null || currentStroke.points.Count == 0 ||
                Vector3.Distance(currentPos, currentStroke.points[currentStroke.points.Count - 1].position) > minPointDistance)
            {
                AddPointToStroke(currentPos);
            }
        }

        protected virtual void AddPointToStroke(Vector3 position)
        {
            if (currentStroke == null)
            {
                if (showDebugInfo) Debug.LogWarning($"{BrushName}: AddPointToStroke called but currentStroke is null");
                return;
            }

            if (currentStrokeObject == null)
            {
                if (showDebugInfo) Debug.LogWarning($"{BrushName}: AddPointToStroke called but currentStrokeObject is null");
                return;
            }

            Vector3 tangent = Vector3.zero;
            if (currentStroke.points.Count > 0)
            {
                tangent = (position - lastBrushPosition).normalized;
            }
            else
            {
                // 第一个点，使用控制器的前向作为切线
                tangent = GetControllerForwardAxis();
            }

            // 核心算法：调用子类实现的ruling计算
            Vector3 rulingDirection = ComputeRulingDirection(tangent, position);

            // 确保ruling方向是单位向量
            if (rulingDirection.magnitude > 0.001f)
            {
                rulingDirection = rulingDirection.normalized;
            }
            else
            {
                // 如果ruling方向为零，使用控制器的up轴作为默认
                rulingDirection = GetControllerUpAxis();
            }

            // 缓存预览用方向(用于让 UniversalBrushPreview 在绘制时跟随笔迹方向)
            lastPreviewTangent = tangent;
            lastPreviewRuling = rulingDirection;
            hasPreviewOrientation = (lastPreviewTangent.sqrMagnitude > 1e-6f && lastPreviewRuling.sqrMagnitude > 1e-6f);

            float width = GetCurrentPointWidth();
            StrokePoint strokePoint = new StrokePoint(position, rulingDirection, tangent, width, Time.time);
            currentStroke.AddPoint(strokePoint);
            pointCount++;

            if (showDebugInfo && pointCount <= 5)
            {
                Debug.Log($"{BrushName}: Added point {pointCount} at {position} with ruling {rulingDirection}");
            }

            // 更新mesh（需要至少2个点）
            if (currentStroke.points.Count >= 2)
            {
                currentStroke.UpdateMesh();
                var meshFilter = currentStrokeObject.GetComponent<MeshFilter>();
                if (meshFilter != null)
                {
                    meshFilter.mesh = currentStroke.GetMesh();
                    if (showDebugInfo && pointCount == 2)
                    {
                        Debug.Log($"{BrushName}: First mesh update with {currentStroke.points.Count} points");
                    }
                }
            }

            lastBrushPosition = position;
            lastTime = Time.time;
        }

    /// <summary>
    /// 子类可覆盖，逐点调整笔刷宽度。默认返回全局 ribbonWidth。
    /// </summary>
    protected virtual float GetCurrentPointWidth() => ribbonWidth;

        protected virtual void StopDrawing()
        {
            if (currentStroke != null && currentStroke.points.Count >= 2)
            {
                currentStroke.Complete();
                currentStrokeObject.GetComponent<MeshFilter>().mesh = currentStroke.GetMesh();
                completedStrokes.Add(currentStrokeObject);
                if (showDebugInfo) Debug.Log($"{BrushName}: 完成笔画，包含 {currentStroke.points.Count} 个点");
            }
            else if (currentStrokeObject != null)
            {
                Object.DestroyImmediate(currentStrokeObject);
            }

            // 调用子类的钩子
            OnStrokeEnd();

            isDrawing = false;
            currentStroke = null;
            currentStrokeObject = null;
        }

        // 当处于绘制状态时的按下/释放回调，子类可覆盖
        protected virtual void OnTriggerPressedWhileDrawing() { }
        protected virtual void OnTriggerReleasedWhileDrawing()
        {
            // 默认行为：结束当前笔画（与旧逻辑一致）
            StopDrawing();
        }

        // 移除 BrushContext：通过虚方法提供可覆盖的预览旋转/覆盖值
        protected virtual Quaternion? GetMainPreviewRotationOverride() => null;
        protected virtual Quaternion? GetCustomDrawingPreviewRotation()
        {
            if (!hasPreviewOrientation) return null;
            Vector3 up = lastPreviewRuling;
            Vector3 forward = lastPreviewTangent;
            if (up.sqrMagnitude < 1e-6f || forward.sqrMagnitude < 1e-6f) return null;
            up.Normalize();
            forward.Normalize();
            // 构建正交坐标系：right = up x forward, forward = right x up
            Vector3 right = Vector3.Cross(up, forward);
            if (right.sqrMagnitude < 1e-6f)
            {
                // 退化处理：选择一个与 up 不平行的向量
                right = Vector3.Cross(up, Vector3.right);
                if (right.sqrMagnitude < 1e-6f) right = Vector3.Cross(up, Vector3.forward);
            }
            right.Normalize();
            forward = Vector3.Cross(right, up).normalized;
            return Quaternion.LookRotation(forward, up);
        }
        protected virtual Quaternion? GetDiskRotationOverride() => null;

        /// <summary>
        /// 更新预览和视觉反馈。
        /// 子类应通过填充BrushContext的AdditionalData来声明其预览需求。
        /// 现在集成了通用预览系统。
        /// </summary>
        protected virtual void UpdatePreviewAndFeedback()
        {
            // 计算基础参数
            Vector3 position = GetDrawingPosition();
            Quaternion controllerRotation = GetControllerRotation();
            float deltaTime = Time.deltaTime;
            float speed = (deltaTime > 0 && isDrawing) ? Vector3.Distance(position, lastBrushPosition) / Mathf.Max(1e-5f, deltaTime) : 0f;

            // 更新通用预览系统
            if (universalBrushPreview != null)
            {
                UpdateUniversalBrushPreview(position, controllerRotation, isDrawing, speed);
            }

            // 视觉反馈组件已移除
        }

        /// <summary>
        /// 更新通用预览系统（无 BrushContext 版本）
        /// </summary>
        protected virtual void UpdateUniversalBrushPreview(Vector3 position, Quaternion controllerRotation, bool isDrawing, float speed)
        {
            if (universalBrushPreview == null) return;

            // 应用摇杆输入调整（仅未绘制时）
            Quaternion adjustedControllerRotation = controllerRotation;
            if (!isDrawing && currentJoystickInput.sqrMagnitude > 0.0001f)
            {
                adjustedControllerRotation = universalBrushPreview.ApplyJoystickAdjustment(currentJoystickInput, controllerRotation);
            }

            // 计算笔刷旋转，允许子类覆盖
            Quaternion brushRotation = GetMainPreviewRotationOverride() ?? GetCustomDrawingPreviewRotation() ?? adjustedControllerRotation;

            // Orientation disk removed; compute preview width (支持按压)
            float previewWidth = isDrawing ? GetCurrentPointWidth() : ribbonWidth;
            universalBrushPreview.UpdatePreview(position, adjustedControllerRotation, brushRotation, isDrawing, previewWidth);
        }

        protected virtual Material CreateDefaultMaterial()
        {
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.color = brushColor;
            mat.SetFloat("_Surface", 0f);
            mat.SetInt("_CullMode", 0);
            mat.SetFloat("_Smoothness", 0.8f);
            mat.SetFloat("_Metallic", 0.5f);
            return mat;
        }

        private void OnDestroy()
        {
            if (universalBrushPreview != null) universalBrushPreview.Destroy();
        }

        #region Virtual Hooks for Subclasses
        /// <summary>
        /// 计算ruling方向的默认实现，子类可以重写以实现特定算法
        /// </summary>
        protected virtual Vector3 ComputeRulingDirection(Vector3 currentTangent, Vector3 currentPosition)
        {
            // 默认实现：使用控制器的上方向作为ruling方向
            return GetControllerUpDirection();
        }

        /// <summary>
        /// 兼容包装：返回控制器的上方向（供子类或老代码调用）
        /// </summary>
        protected virtual Vector3 GetControllerUpDirection()
        {
            return GetControllerUpAxis();
        }

        /// <summary>
        /// 笔画开始时的钩子，子类可以重写以执行特定逻辑
        /// </summary>
        protected virtual void OnStrokeStart() { }

        /// <summary>
        /// 笔画结束时的钩子，子类可以重写以执行特定逻辑
        /// </summary>
        protected virtual void OnStrokeEnd() { }
        #endregion

        /// <summary>
        /// 子类可覆盖以创建自定义的 Stroke 类型（例如多边形截面）。
        /// 基类默认返回 Stroke。
        /// </summary>
        protected virtual Stroke CreateStroke()
        {
            return new Stroke();
        }

        /// <summary>
        /// 由外部系统（如VRBrushSystem）调用，用于设置笔刷的输入源。
        /// 这使得笔刷可以与任何输入源（左手、右手、脚等）解耦。
        /// </summary>
        /// <param name="controllerObject">提供输入数据的控制器GameObject</param>
        public void SetInputSource(GameObject controllerObject)
        {
            this.inputSourceObject = controllerObject;
            if (this.inputSourceObject != null)
            {
                // 直接使用控制器的 Transform 来获取位置和旋转
                this.controllerTransform = this.inputSourceObject.transform;

                // 获取输入组件
                this.simpleInputManager = this.inputSourceObject.GetComponent<VRBrush.Core.Input.VRInputManagerSimple>();

                // 添加简单输入管理器（基于历史版本）
                if (this.simpleInputManager == null)
                {
                    this.simpleInputManager = this.inputSourceObject.AddComponent<VRBrush.Core.Input.VRInputManagerSimple>();
                    Debug.Log($"BaseBrushController: 已为 {this.inputSourceObject.name} 自动添加 VRInputManagerSimple 组件", this);
                }

                this.controllerInputManager = this.inputSourceObject.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor>();
                this.controllerInteractor = this.inputSourceObject.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor>();

                // 如果没有找到标准的XRBaseInteractor，尝试获取其他类型的交互器
                if (this.controllerInteractor == null)
                {
                    // 尝试XRDirectInteractor
                    var directInteractor = this.inputSourceObject.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRDirectInteractor>();
                    if (directInteractor != null)
                    {
                        this.controllerInteractor = directInteractor;
                        if (showDebugInfo) Debug.Log($"Found XRDirectInteractor on {this.inputSourceObject.name}");
                    }
                    else
                    {
                        // 尝试XRRayInteractor
                        var rayInteractor = this.inputSourceObject.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>();
                        if (rayInteractor != null)
                        {
                            this.controllerInteractor = rayInteractor;
                            if (showDebugInfo) Debug.Log($"Found XRRayInteractor on {this.inputSourceObject.name}");
                        }
                        else
                        {
                            // 尝试NearFarInteractor
                            var nearFarInteractor = this.inputSourceObject.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.NearFarInteractor>();
                            if (nearFarInteractor != null)
                            {
                                this.controllerInteractor = nearFarInteractor;
                                if (showDebugInfo) Debug.Log($"Found NearFarInteractor on {this.inputSourceObject.name}");
                            }
                        }
                    }
                }

                Debug.Log($"BaseBrushController: Input source set to {this.inputSourceObject.name}. " +
                         $"SimpleInputManager: {(this.simpleInputManager != null ? "Found" : "Not Found")}, " +
                         $"ControllerInputManager: {(this.controllerInputManager != null ? "Found" : "Not Found")}, " +
                         $"ControllerInteractor: {(this.controllerInteractor != null ? this.controllerInteractor.GetType().Name : "Not Found")}", this);
            }
            else
            {
                Debug.LogError("BaseBrushController: Input source GameObject is null!", this);
            }
        }

        #region Helper Methods
        protected virtual Vector3 GetDrawingPosition()
        {
            Vector3 brushDirection = GetBrushDirection();
            return GetControllerPosition() + brushDirection * brushDistance;
        }

        protected virtual Vector3 GetBrushDirection()
        {
            Vector3 forward = GetControllerForwardAxis();
            Vector3 up = GetControllerUpAxis();
            Vector3 right = GetControllerRightAxis();
            Vector3 direction = forward;
            if (brushAngleUp > 0) direction = Quaternion.AngleAxis(brushAngleUp, right) * direction;
            if (brushAngleRight > 0) direction = Quaternion.AngleAxis(brushAngleRight, up) * direction;
            return direction.normalized;
        }

        protected virtual Vector3 GetControllerPosition()
        {
            if (controllerTransform != null)
            {
                return controllerTransform.position;
            }
            Debug.LogWarning("BaseBrushController: ControllerTransform is null, returning Vector3.zero for position.", this);
            return Vector3.zero;
        }

        protected virtual Quaternion GetControllerRotation()
        {
            if (controllerTransform != null)
            {
                return controllerTransform.rotation;
            }
            Debug.LogWarning("BaseBrushController: ControllerTransform is null, returning Quaternion.identity for rotation.", this);
            return Quaternion.identity;
        }

        protected virtual Vector3 GetControllerForwardAxis()
        {
            if (controllerTransform != null)
            {
                return controllerTransform.forward;
            }
            return Vector3.forward;
        }

        protected virtual Vector3 GetControllerUpAxis()
        {
            if (controllerTransform != null)
            {
                return controllerTransform.up;
            }
            return Vector3.up;
        }

        protected virtual Vector3 GetControllerRightAxis()
        {
            if (controllerTransform != null)
            {
                return controllerTransform.right;
            }
            return Vector3.right;
        }
        #endregion

    }
}
