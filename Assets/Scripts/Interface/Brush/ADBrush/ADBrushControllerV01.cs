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
    /// ADBrush控制器变体V01
    /// 特点：无Rolling控制（扭转角度锁定为0），无自定义截面功能（固定使用line_segment.json）
    /// 用于实验1的条件1
    /// </summary>
    public class ADBrushControllerV01 : BaseBrushController
    {
        [Header("ADBrush V01 Settings")]
        [SerializeField] private int subdivisionLevel = 10;
        [SerializeField] private string shapeDirectory = "Assets/Brushes";
        
        // 固定使用line_segment形状，不可更改
        private const string FIXED_SHAPE_NAME = "line_segment";

        public override string BrushName => "ADBrush V01 (NoRoll/NoCustom)";

        // ADBrush专用状态
        private ADBrushShapeBuilder shapeBuilder;
        private MorphableShape currentMorphableShape;
        private CurveVisual curvePreview;
        private ReferenceLineVisual referenceLine;
        private SectionEdgePreview sectionEdgePreview; // 截面棱预览

        // 绘制状态追踪
        private Vector3 triggerPressPosition;
        private Quaternion triggerPressRotation;
        private bool isTriggerPressed = false;
        private bool hasConfirmedFirstPoint = false;

        // 减少提示噪音
        private bool warnedInsufficientPoints = false;

        // 性能优化
        private float lastMeshUpdateTime = 0f;
        private float lastCurveUpdateTime = 0f;
        private const float meshUpdateInterval = 0.05f;
        private const float curveUpdateInterval = 0.033f;
        private Vector3 lastUpdatePosition;
        private bool lastTriggerState = false;
        
        // 权重快照
        private List<float> lastPressWeights;
        
        // 截面标记（用于可视化确认的截面位置）
        private List<SectionMarker> sectionMarkers = new List<SectionMarker>();

        protected override void Start()
        {
            base.Start();

            // 固定加载line_segment形状
            LoadShape(FIXED_SHAPE_NAME);

            if (currentMorphableShape != null)
            {
                shapeBuilder = new ADBrushShapeBuilder(currentMorphableShape);
                
                if (universalBrushPreview != null && currentMorphableShape.BaseShape != null)
                {
                    universalBrushPreview.SetUseHelioVisuals(true);
                    universalBrushPreview.SetShowShapePoints(true);
                    universalBrushPreview.SetPreviewColor(new Color(0.5f, 0.5f, 0.9f, 0.7f)); // 蓝色标识V01
                    universalBrushPreview.CurrentShape = currentMorphableShape.BaseShape;
                }
            }

            CreateCurvePreview();
            CreateSectionEdgePreview();
            Debug.Log($"ADBrushControllerV01: Initialized with fixed shape '{FIXED_SHAPE_NAME}'");
        }
        
        /// <summary>
        /// 创建截面棱预览组件
        /// </summary>
        private void CreateSectionEdgePreview()
        {
            var previewObj = new GameObject("ADBrushV01_SectionEdgePreview");
            previewObj.transform.SetParent(transform, worldPositionStays: false);
            sectionEdgePreview = previewObj.AddComponent<SectionEdgePreview>();
            sectionEdgePreview.Initialize(null, new Color(0.5f, 0.5f, 0.9f, 0.8f), 0.002f); // 蓝色
        }
        
        /// <summary>
        /// 计算当前截面的旋转
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
            
            // V01: 无Rolling，使用无扭转旋转从上一个锚点转过来
            Quaternion rotation;
            if (hasConfirmedFirstPoint && shapeBuilder != null && shapeBuilder.PointCount > 0)
            {
                var lastPoint = shapeBuilder.GetLastConfirmedPoint();
                if (lastPoint.HasValue)
                {
                    rotation = ADBrushShapePoint.RotateWithoutTwist(lastPoint.Value.rotation, dragDirection);
                }
                else
                {
                    Vector3 upDirection = GetNoRollingUpDirection(dragDirection);
                    rotation = Quaternion.LookRotation(dragDirection, upDirection);
                }
            }
            else
            {
                rotation = Quaternion.LookRotation(dragDirection, triggerPressRotation * Vector3.up);
            }
            
            return rotation;
        }
        
        /// <summary>
        /// 更新截面棱预览
        /// </summary>
        private void UpdateSectionEdgePreview()
        {
            if (sectionEdgePreview == null || shapeBuilder == null || currentMorphableShape == null) return;
            
            if (!isTriggerPressed || shapeBuilder.PointCount < 1)
            {
                sectionEdgePreview.SetVisible(false);
                return;
            }
            
            var lastPoint = shapeBuilder.Points[shapeBuilder.PointCount - 1];
            var lastWeights = shapeBuilder.GetWeightsForPointIndex(shapeBuilder.PointCount - 1);
            float lastWidth = shapeBuilder.GetSizeForPointIndex(shapeBuilder.PointCount - 1);
            
            var lastShape = currentMorphableShape.GetBrushShape(lastWeights);
            Vector2[] lastShapeVerts = lastShape?.vertices?.ToArray();
            
            if (lastShapeVerts == null || lastShapeVerts.Length == 0)
            {
                sectionEdgePreview.SetVisible(false);
                return;
            }
            
            sectionEdgePreview.SetLastAnchor(lastPoint.position, lastPoint.rotation, lastShapeVerts, lastWidth);
            
            Vector3 currentPos = triggerPressPosition;
            Quaternion currentRot = GetCurrentSectionRotation();
            float currentWidth = ribbonWidth;
            
            var currentWeights = new List<float>(shapeBuilder.MorphWeights);
            var currentShape = currentMorphableShape.GetBrushShape(currentWeights);
            Vector2[] currentShapeVerts = currentShape?.vertices?.ToArray();
            
            if (currentShapeVerts == null || currentShapeVerts.Length == 0)
            {
                sectionEdgePreview.SetVisible(false);
                return;
            }
            
            Vector3 prevAnchorPos = lastPoint.position;
            sectionEdgePreview.UpdatePreview(currentPos, currentRot, currentShapeVerts, currentWidth, prevAnchorPos);
        }

        private void BeginPressForPoint()
        {
            triggerPressPosition = GetDrawingPosition();
            triggerPressRotation = GetControllerRotation();
            isTriggerPressed = true;

            if (shapeBuilder != null)
            {
                lastPressWeights = new List<float>(shapeBuilder.MorphWeights);
            }

            if (referenceLine != null)
            {
                referenceLine.SetVisible(true);
                referenceLine.UpdateEndpoints(triggerPressPosition, triggerPressPosition);
            }
        }

        private void ConfirmPointOnRelease()
        {
            if (!isTriggerPressed) return;

            Vector3 releasePosition = GetDrawingPosition();
            Quaternion releaseRotation = GetControllerRotation();
            
            float dragDistance = Vector3.Distance(releasePosition, triggerPressPosition);
            Vector3 dragDirection = (releasePosition - triggerPressPosition).normalized;
            
            // 特殊处理第一个点
            if (!hasConfirmedFirstPoint && dragDistance < 0.01f)
            {
                dragDirection = triggerPressRotation * Vector3.forward;
                // V01特点：无Rolling控制，第一个点使用控制器up
                Vector3 upDirection = triggerPressRotation * Vector3.up;
                Quaternion firstRotation = Quaternion.LookRotation(dragDirection, upDirection);
                
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
                return;
            }
            
            if (dragDistance < 0.01f)
            {
                dragDirection = triggerPressRotation * Vector3.forward;
            }

            // V01特点：无Rolling控制，使用无扭转旋转从上一个锚点转过来
            Quaternion rotation;
            if (hasConfirmedFirstPoint && shapeBuilder != null && shapeBuilder.PointCount > 0)
            {
                var lastPoint = shapeBuilder.GetLastConfirmedPoint();
                if (lastPoint.HasValue)
                {
                    rotation = ADBrushShapePoint.RotateWithoutTwist(lastPoint.Value.rotation, dragDirection);
                }
                else
                {
                    Vector3 upDir = GetNoRollingUpDirection(dragDirection);
                    rotation = Quaternion.LookRotation(dragDirection, upDir);
                }
            }
            else
            {
                rotation = Quaternion.LookRotation(dragDirection, triggerPressRotation * Vector3.up);
            }

            ADBrushShapePoint newPoint = new ADBrushShapePoint(triggerPressPosition, rotation);
            if (shapeBuilder != null)
            {
                var snapshot = lastPressWeights != null ? new List<float>(lastPressWeights) : new List<float>(shapeBuilder.MorphWeights);
                float currentSize = ribbonWidth;
                shapeBuilder.AddConfirmedPoint(newPoint, snapshot, currentSize);
                hasConfirmedFirstPoint = true;
                shapeBuilder.ClearHead();
                
                // 创建截面标记
                CreateSectionMarker(triggerPressPosition, rotation, snapshot, currentSize);
            }

            if (shapeBuilder.PointCount >= 2)
            {
                UpdateStrokeMesh(forceUpdate: true);
            }

            UpdateStrokeCurvePreview();
            isTriggerPressed = false;
            if (referenceLine != null) referenceLine.SetVisible(false);
        }

        /// <summary>
        /// 获取无Rolling控制时的固定up方向（用于fallback）
        /// </summary>
        private Vector3 GetNoRollingUpDirection(Vector3 tangent)
        {
            // 使用世界坐标的up投影到垂直于切线的平面上
            Vector3 upDirection = Vector3.ProjectOnPlane(Vector3.up, tangent);
            
            if (upDirection.sqrMagnitude < 1e-6f)
            {
                // 如果切线接近垂直，使用forward作为备选
                upDirection = Vector3.ProjectOnPlane(Vector3.forward, tangent);
                if (upDirection.sqrMagnitude < 1e-6f)
                {
                    upDirection = Vector3.Cross(tangent, Vector3.right);
                }
            }
            
            return upDirection.normalized;
        }

        /// <summary>
        /// V01专用：预览旋转使用无扭转方式（无Rolling）
        /// </summary>
        protected override Quaternion? GetMainPreviewRotationOverride()
        {
            if (!isDrawing || !isTriggerPressed)
            {
                return null;
            }

            Vector3 currentPos = GetDrawingPosition();
            Vector3 drag = currentPos - triggerPressPosition;
            float dragDistance = drag.magnitude;
            Vector3 forward = dragDistance > 1e-3f ? drag.normalized : (GetControllerRotation() * Vector3.forward);
            
            // V01: 无Rolling，使用无扭转旋转从上一个锚点转过来
            Quaternion rotation;
            if (hasConfirmedFirstPoint && shapeBuilder != null && shapeBuilder.PointCount > 0)
            {
                var lastPoint = shapeBuilder.GetLastConfirmedPoint();
                if (lastPoint.HasValue)
                {
                    rotation = ADBrushShapePoint.RotateWithoutTwist(lastPoint.Value.rotation, forward);
                }
                else
                {
                    Vector3 up = GetNoRollingUpDirection(forward);
                    rotation = Quaternion.LookRotation(forward, up);
                }
            }
            else
            {
                rotation = Quaternion.LookRotation(forward, triggerPressRotation * Vector3.up);
            }

            return rotation;
        }

        private void CreateCurvePreview()
        {
            GameObject previewObj = new GameObject("ADBrushV01_CurvePreview");
            previewObj.transform.SetParent(transform);
            curvePreview = previewObj.AddComponent<CurveVisual>();
            curvePreview.SetColor(new Color(0.5f, 0.5f, 1f, 0.8f));
            curvePreview.SetWidth(0.004f);
            curvePreview.SetShowPoints(true, 0.005f);

            GameObject refObj = new GameObject("ADBrushV01_ReferenceLine");
            refObj.transform.SetParent(transform);
            referenceLine = refObj.AddComponent<ReferenceLineVisual>();
            referenceLine.Initialize();
            referenceLine.SetColor(new Color(0.5f, 0.5f, 1f, 0.8f));
            referenceLine.SetRadius(0.003f);
            referenceLine.SetVisible(false);
        }

        private void LoadShape(string shapeName)
        {
            string filePath = System.IO.Path.Combine(shapeDirectory, $"{shapeName}.json");
            currentMorphableShape = MorphableShape.LoadFromFile(filePath);

            if (currentMorphableShape == null)
            {
                Debug.LogWarning($"ADBrushControllerV01: Failed to load shape '{shapeName}', using fallback");
                CreateDefaultLineSegmentShape();
            }
        }

        private void CreateDefaultLineSegmentShape()
        {
            var lineShape = new BrushShape("DefaultLineSegment");
            lineShape.AddNode(new Vector2(-0.5f, 0f));
            lineShape.AddNode(new Vector2(0.5f, 0f));
            lineShape.AddEdge(0, 1);
            currentMorphableShape = new MorphableShape(lineShape);
        }

        protected override void Update()
        {
            base.Update();

            if (GetBButtonPressed())
            {
                FinishCurrentStroke();
            }

            if (isDrawing)
            {
                Vector3 currentPos = GetDrawingPosition();
                bool stateChanged = (isTriggerPressed != lastTriggerState);
                bool positionChanged = Vector3.Distance(currentPos, lastUpdatePosition) > 0.001f;
                
                if (isTriggerPressed)
                {
                    UpdateShadowPoint();
                    
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

                float timeSinceLastCurve = Time.time - lastCurveUpdateTime;
                if (stateChanged || timeSinceLastCurve >= curveUpdateInterval || positionChanged)
                {
                    UpdateStrokeCurvePreview();
                    lastCurveUpdateTime = Time.time;
                }
                
                lastUpdatePosition = currentPos;
                lastTriggerState = isTriggerPressed;
            }
            
            if (!isDrawing)
            {
                hasPreviewOrientation = true;
                lastPreviewTangent = GetControllerRotation() * Vector3.forward;
                lastPreviewRuling = GetControllerRotation() * Vector3.up;
            }
        }

        private void UpdateShadowPoint()
        {
            if (!isTriggerPressed || shapeBuilder == null) return;

            Vector3 currentPosition = GetDrawingPosition();
            float dragDistance = Vector3.Distance(currentPosition, triggerPressPosition);
            Vector3 dragDirection = (currentPosition - triggerPressPosition).normalized;

            if (dragDistance < 0.01f)
            {
                dragDirection = triggerPressRotation * Vector3.forward;
            }

            // V01: 无Rolling控制，使用无扭转旋转
            
            if (referenceLine != null)
            {
                referenceLine.SetVisible(true);
                referenceLine.UpdateEndpoints(triggerPressPosition, currentPosition);
            }
            
            // 更新截面棱预览
            UpdateSectionEdgePreview();
        }

        private void UpdateIdleHeadPreview()
        {
            if (!isDrawing || isTriggerPressed || shapeBuilder == null) return;

            Vector3 pos = GetDrawingPosition();
            Quaternion rot = GetControllerRotation();
            
            // V01: 调整旋转以移除rolling
            Vector3 forward = rot * Vector3.forward;
            Vector3 up = GetNoRollingUpDirection(forward);
            Quaternion adjustedRot = Quaternion.LookRotation(forward, up);
            
            var head = new ADBrushShapePoint(pos, adjustedRot);
            shapeBuilder.SetHead(head);

            if (referenceLine != null) referenceLine.SetVisible(false);
        }

        private void UpdateStrokeMesh(bool forceUpdate = false)
        {
            if (shapeBuilder == null) return;
            if (!forceUpdate && !isTriggerPressed) return;
            if (shapeBuilder.PointCount < 2)
            {
                if (shapeBuilder.PointCount > 0 && !warnedInsufficientPoints)
                {
                    Debug.LogWarning($"ADBrushControllerV01: Not enough points ({shapeBuilder.PointCount}/2 minimum)");
                    warnedInsufficientPoints = true;
                }
                return;
            }
            if (currentStrokeObject == null) return;

            Stroke stroke = shapeBuilder.Build3DMesh(subdivisionLevel, ribbonWidth);
            if (stroke != null && currentStrokeObject != null)
            {
                if (referenceLine != null) referenceLine.SetVisible(false);
                var meshFilter = currentStrokeObject.GetComponent<MeshFilter>();
                if (meshFilter != null)
                {
                    meshFilter.mesh = stroke.GetMesh();
                }
            }
        }

        private void UpdateStrokeCurvePreview()
        {
            if (curvePreview == null || shapeBuilder == null) return;
            
            bool includeShadow = !isTriggerPressed;
            StrokeCurve previewCurve = shapeBuilder.BuildStrokeCurve(subdivisionLevel, includeShadow: includeShadow);
            curvePreview.UpdateCurve(previewCurve);

            var anchors = new List<Vector3>();
            var pts = shapeBuilder.Points;
            for (int i = 0; i < pts.Count; i++) anchors.Add(pts[i].position);
            curvePreview.UpdateAnchorPoints(anchors);

            var head = shapeBuilder.GetShadowPoint();
            if (head.HasValue)
            {
                curvePreview.UpdateGhostHead(head.Value.position);
            }
            else
            {
                curvePreview.UpdateGhostHead(null);
            }
        }

        protected override void OnStrokeStart()
        {
            base.OnStrokeStart();

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
            lastMeshUpdateTime = 0f;
            lastCurveUpdateTime = 0f;
            lastUpdatePosition = Vector3.zero;
            lastTriggerState = false;

            if (curvePreview != null) curvePreview.Clear();

            warnedInsufficientPoints = false;
        }

        protected override void OnStrokeEnd()
        {
            base.OnStrokeEnd();

            if (shapeBuilder != null)
            {
                shapeBuilder.ClearHead();
            }
        }

        protected override void UpdateCurrentStroke()
        {
            // ADBrush不使用连续添加点的方式
        }
        
        // 预览位置在按下时固定于按下点；空闲时隐藏笔刷预览
        protected override void UpdateUniversalBrushPreview(Vector3 position, Quaternion controllerRotation, bool drawing, float speed)
        {
            if (universalBrushPreview == null) return;
            
            // 空闲时（未按下扳机）隐藏笔刷预览
            if (drawing && !isTriggerPressed)
            {
                universalBrushPreview.Hide();
                return;
            }
            
            Vector3 pos = position;
            if (drawing && isTriggerPressed)
            {
                pos = triggerPressPosition;
            }
            base.UpdateUniversalBrushPreview(pos, controllerRotation, drawing, speed);
        }

        protected override void StartDrawing()
        {
            if (!isDrawing)
            {
                base.StartDrawing();
            }
            BeginPressForPoint();
        }

        protected override void StopDrawing()
        {
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
                    Object.DestroyImmediate(currentStrokeObject);
                }
            }

            // 隐藏参考线
            if (referenceLine != null) referenceLine.SetVisible(false);
            
            // 清理截面标记
            ClearSectionMarkers();

            OnStrokeEnd();
            isDrawing = false;
            currentStroke = null;
            currentStrokeObject = null;
        }

        private void FinishCurrentStroke()
        {
            if (isDrawing)
            {
                if (shapeBuilder != null)
                {
                    shapeBuilder.ClearHead();
                }

                if (shapeBuilder != null && shapeBuilder.PointCount >= 2)
                {
                    UpdateStrokeMesh(forceUpdate: true);
                    
                    if (currentStrokeObject != null && !completedStrokes.Contains(currentStrokeObject))
                    {
                        completedStrokes.Add(currentStrokeObject);
                    }
                }
                else
                {
                    if (currentStrokeObject != null)
                    {
                        Object.DestroyImmediate(currentStrokeObject);
                    }
                }

                if (curvePreview != null) curvePreview.Clear();
                if (referenceLine != null) referenceLine.SetVisible(false);
                
                // 清理截面标记
                ClearSectionMarkers();

                OnStrokeEnd();
                isDrawing = false;
                currentStroke = null;
                currentStrokeObject = null;
                isTriggerPressed = false;
                hasConfirmedFirstPoint = false;
                
                if (shapeBuilder != null) shapeBuilder.Clear();
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

        protected override void OnTriggerPressedWhileDrawing()
        {
            BeginPressForPoint();
        }

        protected override void OnTriggerReleasedWhileDrawing()
        {
            ConfirmPointOnRelease();
        }

        private bool GetBButtonPressed()
        {
            try
            {
                var devices = new List<UnityEngine.XR.InputDevice>();
                UnityEngine.XR.InputDevices.GetDevicesAtXRNode(UnityEngine.XR.XRNode.RightHand, devices);
                if (devices.Count > 0)
                {
                    var device = devices[0];
                    if (device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out bool bButton))
                    {
                        bool pressed = bButton && !previousBButtonState;
                        previousBButtonState = bButton;
                        return pressed;
                    }
                }
            }
            catch (System.Exception) { }
            return false;
        }

        private bool previousBButtonState = false;

        // V01不支持形状切换，返回空列表
        public List<string> GetAvailableShapeNames()
        {
            return new List<string> { FIXED_SHAPE_NAME };
        }

        public string GetActiveShapeName()
        {
            return FIXED_SHAPE_NAME;
        }

        // V01不支持Morph
        public List<string> GetMorphNames()
        {
            return new List<string>();
        }

        public List<float> GetMorphWeights()
        {
            return new List<float>();
        }
    }
}
