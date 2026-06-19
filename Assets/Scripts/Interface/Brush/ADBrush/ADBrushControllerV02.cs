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
    /// ADBrush控制器变体V02
    /// 特点：无Rolling控制（扭转角度锁定为0），有自定义截面功能
    /// 用于实验1的条件2
    /// </summary>
    public class ADBrushControllerV02 : BaseBrushController
    {
        [Header("ADBrush V02 Settings")]
        [SerializeField] private int subdivisionLevel = 10;
        [SerializeField] private string shapeDirectory = "Assets/Brushes";
        [SerializeField] private string selectedShapeName = "triangle";

        public override string BrushName => "ADBrush V02 (NoRoll/Custom)";

        // ADBrush专用状态
        private ADBrushShapeBuilder shapeBuilder;
        private MorphableShape currentMorphableShape;
        private CurveVisual curvePreview;
        private ReferenceLineVisual referenceLine;
        private SectionEdgePreview sectionEdgePreview; // 截面棱预览
        private List<SectionMarker> sectionMarkers = new List<SectionMarker>(); // 截面标记列表

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
        private float lastPreviewShapeUpdateTime = 0f;
        private const float meshUpdateInterval = 0.05f;
        private const float curveUpdateInterval = 0.033f;
        private const float previewShapeUpdateInterval = 0.05f;
        private Vector3 lastUpdatePosition;
        private bool lastTriggerState = false;
        
        // 权重快照
        private List<float> lastPressWeights;

        protected override void Start()
        {
            base.Start();

            LoadShape(selectedShapeName);

            if (currentMorphableShape != null)
            {
                shapeBuilder = new ADBrushShapeBuilder(currentMorphableShape);
                
                if (universalBrushPreview != null && currentMorphableShape.BaseShape != null)
                {
                    universalBrushPreview.SetUseHelioVisuals(true);
                    universalBrushPreview.SetShowShapePoints(true);
                    universalBrushPreview.SetPreviewColor(new Color(0.5f, 0.9f, 0.5f, 0.7f)); // 绿色标识V02
                    universalBrushPreview.CurrentShape = currentMorphableShape.BaseShape;
                }
            }

            CreateCurvePreview();
            CreateSectionEdgePreview();
            RebuildMorphUI();
            Debug.Log($"ADBrushControllerV02: Initialized with shape '{selectedShapeName}'");
        }
        
        /// <summary>
        /// 创建截面棱预览组件
        /// </summary>
        private void CreateSectionEdgePreview()
        {
            var previewObj = new GameObject("ADBrushV02_SectionEdgePreview");
            previewObj.transform.SetParent(transform, worldPositionStays: false);
            sectionEdgePreview = previewObj.AddComponent<SectionEdgePreview>();
            sectionEdgePreview.Initialize(null, new Color(0.5f, 0.9f, 0.5f, 0.8f), 0.002f); // 绿色
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
            
            // V02: 无Rolling，使用无扭转旋转从上一个锚点转过来
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
            
            if (!hasConfirmedFirstPoint && dragDistance < 0.01f)
            {
                dragDirection = triggerPressRotation * Vector3.forward;
                // V02特点：无Rolling控制，第一个点使用控制器up
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

            // V02特点：无Rolling控制，使用无扭转旋转从上一个锚点转过来
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
            Vector3 upDirection = Vector3.ProjectOnPlane(Vector3.up, tangent);
            
            if (upDirection.sqrMagnitude < 1e-6f)
            {
                upDirection = Vector3.ProjectOnPlane(Vector3.forward, tangent);
                if (upDirection.sqrMagnitude < 1e-6f)
                {
                    upDirection = Vector3.Cross(tangent, Vector3.right);
                }
            }
            
            return upDirection.normalized;
        }

        /// <summary>
        /// V02专用：预览旋转使用无扭转方式（无Rolling）
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
            
            // V02: 无Rolling，使用无扭转旋转从上一个锚点转过来
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
            GameObject previewObj = new GameObject("ADBrushV02_CurvePreview");
            previewObj.transform.SetParent(transform);
            curvePreview = previewObj.AddComponent<CurveVisual>();
            curvePreview.SetColor(new Color(0.5f, 1f, 0.5f, 0.8f));
            curvePreview.SetWidth(0.004f);
            curvePreview.SetShowPoints(true, 0.005f);

            GameObject refObj = new GameObject("ADBrushV02_ReferenceLine");
            refObj.transform.SetParent(transform);
            referenceLine = refObj.AddComponent<ReferenceLineVisual>();
            referenceLine.Initialize();
            referenceLine.SetColor(new Color(0.5f, 1f, 0.5f, 0.8f));
            referenceLine.SetRadius(0.003f);
            referenceLine.SetVisible(false);
        }

        public List<string> GetAvailableShapeNames()
        {
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

        public string GetActiveShapeName()
        {
            return selectedShapeName;
        }

        public void ActivateShape(string shapeName)
        {
            selectedShapeName = shapeName;
            LoadShape(shapeName);
            if (currentMorphableShape != null)
            {
                shapeBuilder = new ADBrushShapeBuilder(currentMorphableShape);
                if (curvePreview != null) curvePreview.Clear();
                
                if (universalBrushPreview != null && currentMorphableShape.BaseShape != null)
                {
                    universalBrushPreview.SetUseHelioVisuals(true);
                    universalBrushPreview.SetShowShapePoints(true);
                    universalBrushPreview.SetPreviewColor(new Color(0.5f, 0.9f, 0.5f, 0.7f));
                    universalBrushPreview.CurrentShape = currentMorphableShape.BaseShape;
                }

                RebuildMorphUI();
            }
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

        private void UpdatePreviewShapeFromWeights()
        {
            if (universalBrushPreview == null || currentMorphableShape == null || shapeBuilder == null)
                return;

            var weights = new List<float>(shapeBuilder.MorphWeights);
            var morphed = currentMorphableShape.GetBrushShape(weights);
            if (morphed != null)
            {
                universalBrushPreview.CurrentShape = morphed;
            }
        }

        private void LoadShape(string shapeName)
        {
            string filePath = System.IO.Path.Combine(shapeDirectory, $"{shapeName}.json");
            currentMorphableShape = MorphableShape.LoadFromFile(filePath);

            if (currentMorphableShape == null)
            {
                Debug.LogWarning($"ADBrushControllerV02: Failed to load shape '{shapeName}', using fallback");
                CreateDefaultCircleShape();
            }
        }

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

        private void RebuildMorphUI()
        {
            if (shapeBuilder == null && currentMorphableShape != null)
            {
                shapeBuilder = new ADBrushShapeBuilder(currentMorphableShape);
            }
        }

        public void OnExternalMorphValueChanged(int morphIndex, float value)
        {
            SetMorphWeight(morphIndex, value);

            // 如果正在按下扳机，同步更新 lastPressWeights，确保实际绘制的 mesh 也反映变化
            if (isTriggerPressed && lastPressWeights != null && morphIndex < lastPressWeights.Count)
            {
                lastPressWeights[morphIndex] = Mathf.Clamp01(value);
            }

            float timeSinceLastUpdate = Time.time - lastPreviewShapeUpdateTime;
            if (timeSinceLastUpdate >= previewShapeUpdateInterval)
            {
                UpdatePreviewShapeFromWeights();
                lastPreviewShapeUpdateTime = Time.time;
            }
        }

        public List<string> GetMorphNames()
        {
            if (currentMorphableShape == null)
            {
                LoadShape(selectedShapeName);
                
                if (currentMorphableShape != null && shapeBuilder == null)
                {
                    shapeBuilder = new ADBrushShapeBuilder(currentMorphableShape);
                }
            }
            
            if (currentMorphableShape == null)
            {
                return new List<string>();
            }

            return new List<string>(currentMorphableShape.MorphNames);
        }

        public List<float> GetMorphWeights()
        {
            if (shapeBuilder == null && currentMorphableShape != null)
            {
                shapeBuilder = new ADBrushShapeBuilder(currentMorphableShape);
            }
            
            if (shapeBuilder?.MorphWeights == null)
            {
                return new List<float>();
            }

            return new List<float>(shapeBuilder.MorphWeights);
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
            
            Vector3 forward = rot * Vector3.forward;
            Vector3 up = GetNoRollingUpDirection(forward);
            Quaternion adjustedRot = Quaternion.LookRotation(forward, up);
            
            var head = new ADBrushShapePoint(pos, adjustedRot);
            shapeBuilder.SetHead(head);

            // 空闲时不显示参考线，只有拖拽时才显示
            if (referenceLine != null) referenceLine.SetVisible(false);
            
            // 更新截面棱预览
            if (hasConfirmedFirstPoint && shapeBuilder.PointCount > 0)
            {
                UpdateSectionEdgePreview();
            }
        }

        private void UpdateStrokeMesh(bool forceUpdate = false)
        {
            if (shapeBuilder == null) return;
            if (!forceUpdate && !isTriggerPressed) return;
            if (shapeBuilder.PointCount < 2)
            {
                if (shapeBuilder.PointCount > 0 && !warnedInsufficientPoints)
                {
                    Debug.LogWarning($"ADBrushControllerV02: Not enough points ({shapeBuilder.PointCount}/2 minimum)");
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
            lastPreviewShapeUpdateTime = 0f;
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

        public void SetMorphWeight(int morphIndex, float weight)
        {
            if (shapeBuilder != null)
            {
                shapeBuilder.SetMorphWeight(morphIndex, weight);
                if (isTriggerPressed)
                {
                    UpdateStrokeMesh();
                }
            }
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
    }
}
