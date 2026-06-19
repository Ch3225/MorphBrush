using UnityEngine;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using VRBrush.Core.Model;
using VRBrush.Core.Visual;
using TMPro;
using System.Text.RegularExpressions;

namespace VRBrush.Interface
{
    /// <summary>
    /// MorphEditor控制器 - 用于编辑现有形状并创建变形（Morph）
    /// 参考 BrushEditorController 的实现模式
    /// </summary>
    public class MorphEditorController : MonoBehaviour, IBrushController
    {
        public enum MorphNameStrategy
        {
            Sequential, // Morph1, Morph2, ...（默认）
            Timestamp,  // Morph_yyyyMMdd_HHmmss
            Guid        // Morph_<short-guid>
        }

        #region IBrushController Implementation
        public string BrushName => "MorphEditor";
        public string ControllerName => BrushName;
        public float BrushDistance => 0f;
        public float BrushAngleUp => 0f;
        public float BrushWidth => 1f;
        public Vector2 BrushWidthRange => new Vector2(0.001f, 4.0f);

        public void SetBrushDistance(float distance) { }
        public void SetBrushAngleUp(float angle) { }
        public void SetBrushWidth(float width) { }
        public void ClearAllStrokes() { shapeBuilder?.ClearGraph(); }
        public void SetStrokesParent(Transform parent) { /* Not used */ }
        #endregion

        #region Serialized Fields
        [Header("编辑器参数")]
        [SerializeField] private float coordinateSystemSize = 0.2f;
        [SerializeField] private float pointRadius = 0.01f;
        [SerializeField] private float lineRadius = 0.005f;
        [SerializeField] private float snapThreshold = 0.05f;
        [SerializeField] private float connectThreshold = 0.035f;
        [SerializeField] private float removeThreshold = 0.03f;
        [SerializeField] private float smoothingFactor = 10f;
        [SerializeField] private bool addCenterIndicator = true;
        [SerializeField] private bool verboseDebug = true;

    [Header("形状资源路径")]
    [Tooltip("相对于 Assets 的形状目录，默认 Assets/Brushes")]
    [SerializeField] private string shapeDirectoryRelative = "Assets/Brushes";

        [Header("Morph 保存命名策略")]
        [SerializeField] private MorphNameStrategy morphNaming = MorphNameStrategy.Sequential;
        [SerializeField] private string morphNamePrefix = "Morph";
        #endregion

        #region Private Fields
        // Input sources (set via SetInputSource and SetLeftInputSource)
        private GameObject inputSourceObject;
        private Transform leftController;
        private Vector3 smoothedLeftControllerPosition;
        private Quaternion smoothedLeftControllerRotation;

        // Editor state
        private bool isEditing = false;
        private BrushShape baseShape;
        private BrushShape editableShape; // 当前编辑的形状（深拷贝）
        private string baseShapePath;
        private BrushEditorDisplay editorDisplay;
        private BrushShapeBuilder shapeBuilder;
        
        // 视觉元素
        private BrushShapeVisual shadowVisual; // 只读影子副本
        private BrushShapeVisual editableVisual; // 可编辑的形状

        // Input state
        private bool previousAState = false;
        private bool previousBState = false;
        private int? highlightedPointIndex = null;
        
        // Initialization state
        private bool isDisplayInitialized = false;
        
        // 当前加载的形状名称（供BrushUIManager使用）
        private string currentShapeName;
        #endregion

        #region Unity Lifecycle
        void Awake()
        {
            // 初始化图结构
            string graphName = $"MorphShape_{System.DateTime.Now:yyyyMMdd_HHmmss}";
            shapeBuilder = new BrushShapeBuilder(new BrushShape(graphName));

            // 初始化显示系统
            CreateEditorDisplay();

            Debug.Log($"MorphEditorController Awake: instanceId={this.GetInstanceID()}, gameObject.name={gameObject.name}");
        }

        void Start()
        {
            Debug.Log("MorphEditorController.Start: Called (GameObject may be disabled by VRBrushSystem).");
            // Note: Start() may not be called if GameObject is disabled at scene start
            // We will initialize in OnEnable() instead
        }

        void OnEnable()
        {
            Debug.Log("MorphEditorController.OnEnable: Activating edit mode.");
            
            // Initialize display system if not already done
            if (!isDisplayInitialized && editorDisplay != null && shapeBuilder != null)
            {
                editorDisplay.Initialize(shapeBuilder, leftController);
                editorDisplay.ApplyControllerSettings(
                    coordinateSystemSize, pointRadius, lineRadius,
                    snapThreshold, connectThreshold, removeThreshold,
                    addCenterIndicator, verboseDebug, smoothingFactor
                );
                isDisplayInitialized = true;
                Debug.Log("MorphEditorController.OnEnable: Initialized editorDisplay.");
            }
            
            // Enable edit mode
            SetEditMode(true);
        }

        void OnDisable()
        {
            // When MorphEditor is deactivated, disable edit mode
            Debug.Log("MorphEditorController.OnDisable: Deactivating edit mode.");
            SetEditMode(false);
        }

        void Update()
        {
            if (!isEditing) return;

            // 更新坐标系跟随左手控制器
            if (editorDisplay != null)
            {
                editorDisplay.UpdateCoordinateSystem();
            }

            // 更新右手控制器投影几何
            if (editorDisplay != null && inputSourceObject != null)
            {
                BrushEditorPoint lastPoint = null;
                if (shapeBuilder != null && shapeBuilder.PreviousNodeIndex.HasValue)
                {
                    lastPoint = editorDisplay.GetPointByIndex(shapeBuilder.PreviousNodeIndex.Value);
                }
                editorDisplay.UpdateProjectionGeometry(inputSourceObject.transform, lastPoint);
            }

            // 处理右手控制器输入
            HandleRightControllerInput();
        }
        #endregion

        #region Input Source Setup
        public void SetInputSource(GameObject source)
        {
            this.inputSourceObject = source;
            Debug.Log($"MorphEditorController.SetInputSource: Set to '{(source != null ? source.name : "null")}'.");
        }

        public void SetLeftInputSource(GameObject source)
        {
            Debug.Log($"MorphEditorController.SetLeftInputSource: Setting left controller to '{(source != null ? source.name : "null")}'.");
            this.leftController = source != null ? source.transform : null;
            
            // Initialize smoothed position if left controller is set
            if (leftController != null)
            {
                smoothedLeftControllerPosition = leftController.position;
                smoothedLeftControllerRotation = leftController.rotation;
            }
            
            if (editorDisplay != null)
            {
                // Re-initialize display with new left controller
                Debug.Log("MorphEditorController.SetLeftInputSource: Re-initializing editor display with new left controller.");
                editorDisplay.Initialize(shapeBuilder, leftController);
            }
        }
        #endregion

        #region Display System
        private void CreateEditorDisplay()
        {
            var displayGO = new GameObject("MorphEditorDisplay");
            displayGO.transform.SetParent(transform);
            editorDisplay = displayGO.AddComponent<BrushEditorDisplay>();
            Debug.Log("MorphEditorController: Created BrushEditorDisplay component.");
        }
        #endregion

        #region Edit Mode Control
        public void SetEditMode(bool enabled)
        {
            Debug.Log($"MorphEditorController.SetEditMode: Setting edit mode to {enabled}.");
            
            if (isEditing == enabled)
            {
                Debug.Log($"MorphEditorController: Already in edit mode {enabled}, skipping.");
                return;
            }

            isEditing = enabled;

            // 控制显示系统的可见性
            if (editorDisplay != null)
            {
                editorDisplay.SetVisible(enabled);
                Debug.Log($"MorphEditorController.SetEditMode: Set editorDisplay visibility to {enabled}.");
            }
            else if (enabled)
            {
                Debug.LogError("MorphEditorController.SetEditMode: Attempted to enable mode, but editorDisplay is null!");
            }

            if (enabled && leftController != null)
            {
                smoothedLeftControllerPosition = leftController.position;
                smoothedLeftControllerRotation = leftController.rotation;
                editorDisplay?.UpdateCoordinateSystem();
            }
        }
        #endregion

        #region Input Handling
        private void HandleRightControllerInput()
        {
            if (inputSourceObject == null) return;

            // 使用 XR 按钮：A(主) / B(副)
            bool currentAButton = GetAButtonValue();
            bool currentBButton = GetBButtonValue();

            if (verboseDebug)
            {
                if (currentAButton || currentBButton)
                {
                    Debug.Log($"MorphEditor Input: A={currentAButton}, B={currentBButton}");
                }
            }

            // A键短按：选择/移动节点
            if (currentAButton && !previousAState)
            {
                OnPrimaryButtonPressed();
            }

            // B键短按：删除节点
            if (currentBButton && !previousBState)
            {
                OnSecondaryButtonPressed();
            }

            // 更新前一帧状态
            previousAState = currentAButton;
            previousBState = currentBButton;

            // 更新高亮显示
            UpdateHighlightedNode();
        }

        private void OnPrimaryButtonPressed()
        {
            Vector3 worldPoint = GetRightControllerPlanePosition();
            if (worldPoint == Vector3.zero || editorDisplay?.CoordinateTransform == null)
            {
                return;
            }

            Vector2 localPoint = BrushShapeBuilder.WorldToLocalPosition(worldPoint, editorDisplay.CoordinateTransform);
            
            // 尝试移动最近的节点
            int nearestIndex = FindNearestNode(localPoint, snapThreshold);
            if (nearestIndex >= 0 && editableShape != null)
            {
                // 更新可编辑形状的节点位置
                if (editableShape.SetNode(nearestIndex, localPoint))
                {
                    // 更新可编辑可视化
                    if (editableVisual != null)
                    {
                        editableVisual.UpdateNodePosition(nearestIndex, localPoint);
                    }
                    Debug.Log($"MorphEditor: Moved node {nearestIndex} to {localPoint}");
                }
            }
        }

        private void OnSecondaryButtonPressed()
        {
            Vector3 worldPoint = GetRightControllerPlanePosition();
            if (worldPoint == Vector3.zero || editorDisplay?.CoordinateTransform == null)
            {
                return;
            }

            Vector2 localPoint = BrushShapeBuilder.WorldToLocalPosition(worldPoint, editorDisplay.CoordinateTransform);
            
            // 删除最近的节点
            int nearestIndex = FindNearestNode(localPoint, removeThreshold);
            if (nearestIndex >= 0 && editableShape != null)
            {
                if (editableShape.RemoveNode(nearestIndex))
                {
                    // 重建可编辑可视化
                    if (editableVisual != null)
                    {
                        editableVisual.RebuildVisuals();
                    }
                    Debug.Log($"MorphEditor: Removed node {nearestIndex}");
                }
            }
        }

        private void UpdateHighlightedNode()
        {
            Vector3 worldPoint = GetRightControllerPlanePosition();
            if (worldPoint == Vector3.zero || editorDisplay?.CoordinateTransform == null)
            {
                highlightedPointIndex = null;
                return;
            }

            Vector2 localPoint = BrushShapeBuilder.WorldToLocalPosition(worldPoint, editorDisplay.CoordinateTransform);
            
            // 查找最近的节点
            int nearestIndex = FindNearestNode(localPoint, snapThreshold);
            highlightedPointIndex = nearestIndex >= 0 ? nearestIndex : null;
            
            // TODO: 高亮显示（可以通过改变材质实现）
        }
        
        /// <summary>
        /// 查找最近的节点
        /// </summary>
        private int FindNearestNode(Vector2 localPoint, float threshold)
        {
            if (editableShape == null)
                return -1;
                
            return editableShape.FindNearestNode(localPoint, threshold);
        }

        private Vector3 GetRightControllerPlanePosition()
        {
            if (inputSourceObject == null || editorDisplay == null || editorDisplay.CoordinateTransform == null)
                return Vector3.zero;

            Transform rightController = inputSourceObject.transform;
            var ct = editorDisplay.CoordinateTransform;

            // 垂直投影到平面
            Vector3 planeNormal = ct.up;
            Vector3 planePoint = ct.position;
            Plane editingPlane = new Plane(planeNormal, planePoint);

            Vector3 tipWorld = rightController.position;
            float signedDistance = editingPlane.GetDistanceToPoint(tipWorld);
            Vector3 worldHit = tipWorld - editingPlane.normal * signedDistance;

            return worldHit;
        }

        // XR Input helpers
        private bool GetAButtonValue()
        {
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
                if (verboseDebug) Debug.LogWarning($"MorphEditor: Read A button failed: {ex.Message}");
            }
            return false;
        }

        private bool GetBButtonValue()
        {
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
                }
            }
            catch (System.Exception ex)
            {
                if (verboseDebug) Debug.LogWarning($"MorphEditor: Read B button failed: {ex.Message}");
            }
            return false;
        }
        #endregion

        #region Shape Loading
        
        /// <summary>
        /// 获取当前可用的形状名称列表（供BrushUIManager使用）
        /// </summary>
        public List<string> GetAvailableShapeNames()
        {
            string brushDir = GetShapesDirectoryAbsolute();
            
            // 确保目录存在
            if (!Directory.Exists(brushDir))
            {
                Directory.CreateDirectory(brushDir);
                return new List<string>();
            }

            // 扫描所有 .json 文件
            var shapeFiles = Directory.GetFiles(brushDir, "*.json", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileNameWithoutExtension)
                .OrderBy(n => n)
                .ToList();

            return shapeFiles;
        }

        /// <summary>
        /// 获取当前激活的形状名称（供BrushUIManager使用）
        /// </summary>
        public string GetActiveShapeName()
        {
            return currentShapeName;
        }

        /// <summary>
        /// 切换到指定形状（由BrushUIManager调用）
        /// </summary>
        public void ActivateShape(string shapeName)
        {
            string brushDir = GetShapesDirectoryAbsolute();
            string path = Path.Combine(brushDir, $"{shapeName}.json");

            if (!File.Exists(path))
            {
                Debug.LogError($"MorphEditorController: Shape file not found at {path}");
                return;
            }

            currentShapeName = shapeName;
            LoadShape(path);
            Debug.Log($"MorphEditorController: Activated shape '{shapeName}'");
        }
        
        private void LoadShape(string path)
        {
            Debug.Log($"MorphEditorController.LoadShape: Starting to load shape from {path}");
            
            if (!File.Exists(path))
            {
                Debug.LogError($"MorphEditorController: Shape file not found at {path}");
                return;
            }
            baseShapePath = path;
            baseShape = BrushShape.LoadFromFile(path);
            Debug.Log($"MorphEditorController.LoadShape: baseShape loaded with {baseShape?.NodeCount ?? 0} nodes");
            
            // 创建可编辑形状的深拷贝
            editableShape = BrushShape.LoadFromFile(path);
            Debug.Log($"MorphEditorController.LoadShape: editableShape loaded with {editableShape?.NodeCount ?? 0} nodes");

            // Re-initialize the builder with the editable shape
            shapeBuilder = new BrushShapeBuilder(editableShape);

            if (editorDisplay != null)
            {
                Debug.Log($"MorphEditorController.LoadShape: editorDisplay exists, leftController={(leftController != null ? leftController.name : "NULL")}");
                
                // 先清除旧的视觉元素，防止残留叠加
                editorDisplay.ResetForNewShape();
                
                // Re-initialize the display with the new builder
                editorDisplay.Initialize(shapeBuilder, leftController);
                
                // 重新应用设置以确保显示正确
                editorDisplay.ApplyControllerSettings(
                    coordinateSystemSize, pointRadius, lineRadius,
                    snapThreshold, connectThreshold, removeThreshold,
                    addCenterIndicator, verboseDebug, smoothingFactor
                );
                Debug.Log($"MorphEditorController.LoadShape: editorDisplay.CoordinateTransform={(editorDisplay.CoordinateTransform != null ? editorDisplay.CoordinateTransform.name : "NULL")}");
            }
            else
            {
                Debug.LogWarning("MorphEditorController.LoadShape: editorDisplay is NULL!");
            }

            // 创建双可视化系统
            CreateDualVisualSystem();
            
            // 更新当前形状名称
            currentShapeName = Path.GetFileNameWithoutExtension(path);

            Debug.Log($"MorphEditorController: Loaded shape '{currentShapeName}' with {baseShape.NodeCount} points and {baseShape.EdgeCount} edges.");
        }

        /// <summary>
        /// 获取形状目录的绝对路径（支持设置为 Assets/ 开头的相对路径）
        /// </summary>
        private string GetShapesDirectoryAbsolute()
        {
            if (string.IsNullOrEmpty(shapeDirectoryRelative))
            {
                return Path.Combine(Application.dataPath, "Brushes");
            }

            // 规范化分隔符
            string rel = shapeDirectoryRelative.Replace('\\', '/');
            if (rel.StartsWith("Assets/", System.StringComparison.OrdinalIgnoreCase))
            {
                string sub = rel.Substring("Assets/".Length); // 去掉 "Assets/"
                return Path.Combine(Application.dataPath, sub);
            }
            if (string.Equals(rel, "Assets", System.StringComparison.OrdinalIgnoreCase))
            {
                return Application.dataPath;
            }
            // 其他情况：作为相对 Assets 的子目录处理
            return Path.Combine(Application.dataPath, rel);
        }

        /// <summary>
        /// 创建双可视化系统：影子（只读）+ 可编辑
        /// </summary>
        private void CreateDualVisualSystem()
        {
            Debug.Log($"MorphEditorController.CreateDualVisualSystem: baseShape={baseShape != null}, editableShape={editableShape != null}, editorDisplay={editorDisplay != null}, CoordinateTransform={editorDisplay?.CoordinateTransform != null}");
            
            if (baseShape == null || editableShape == null || editorDisplay == null || editorDisplay.CoordinateTransform == null)
            {
                Debug.LogWarning($"MorphEditorController: Cannot create dual visual system, missing dependencies. " +
                    $"baseShape={baseShape != null}, editableShape={editableShape != null}, " +
                    $"editorDisplay={editorDisplay != null}, CoordinateTransform={(editorDisplay?.CoordinateTransform != null ? "exists" : "NULL")}");
                return;
            }
            
            // 清除旧的可视化（立即销毁，避免残留）
            if (shadowVisual != null)
            {
                DestroyImmediate(shadowVisual.gameObject);
                shadowVisual = null;
            }
            if (editableVisual != null)
            {
                DestroyImmediate(editableVisual.gameObject);
                editableVisual = null;
            }
            
            // 创建影子可视化（只读，半透明灰色）
            GameObject shadowGO = new GameObject("ShadowShapeVisual");
            shadowGO.transform.SetParent(editorDisplay.CoordinateTransform);
            shadowGO.transform.localPosition = Vector3.zero;
            shadowGO.transform.localRotation = Quaternion.identity;
            shadowVisual = shadowGO.AddComponent<BrushShapeVisual>();
            // 影子的点稍微小一点（75%），线更细（60%）
            shadowVisual.Initialize(baseShape, editorDisplay.CoordinateTransform, editable: false, pointRadius * 0.75f, lineRadius * 0.6f);
            
            // 创建可编辑可视化（白色，可交互）
            GameObject editableGO = new GameObject("EditableShapeVisual");
            editableGO.transform.SetParent(editorDisplay.CoordinateTransform);
            editableGO.transform.localPosition = Vector3.zero;
            editableGO.transform.localRotation = Quaternion.identity;
            editableVisual = editableGO.AddComponent<BrushShapeVisual>();
            editableVisual.Initialize(editableShape, editorDisplay.CoordinateTransform, editable: true, pointRadius, lineRadius);
            
            Debug.Log($"MorphEditorController: Created dual visual system (shadow + editable) with {baseShape.NodeCount} nodes and {baseShape.EdgeCount} edges.");
        }

        /// <summary>
        /// 保存当前编辑的形状为新的 Morph（添加到基础形状文件中）
        /// </summary>
        public void SaveCurrentMorph()
        {
            if (editableShape == null || baseShape == null || string.IsNullOrEmpty(baseShapePath))
            {
                Debug.LogError("MorphEditorController: Cannot save morph, missing required data.");
                return;
            }

            try
            {
                // 读取现有的JSON文件
                string json = File.ReadAllText(baseShapePath);
                var jsonData = JsonUtility.FromJson<BrushShapeJson>(json);

                if (jsonData == null)
                {
                    Debug.LogError($"MorphEditorController: Failed to parse JSON from {baseShapePath}");
                    return;
                }

                // 初始化morphs列表（如果不存在）
                if (jsonData.morphs == null)
                {
                    jsonData.morphs = new List<MorphData>();
                }

                // 生成新的 Morph 名称
                string morphName = GenerateMorphName(jsonData.morphs);

                // 创建 MorphData
                var morphData = new MorphData
                {
                    name = morphName,
                    points = new List<BrushShapePointRaw>()
                };

                // 添加编辑后的节点坐标
                foreach (var node in editableShape.Nodes)
                {
                    morphData.points.Add(new BrushShapePointRaw { x = node.x, y = node.y });
                }

                // 添加到morphs列表
                jsonData.morphs.Add(morphData);

                // 保存回文件
                string updatedJson = JsonUtility.ToJson(jsonData, true);
                File.WriteAllText(baseShapePath, updatedJson);

                Debug.Log($"MorphEditorController: Saved morph '{morphName}' with {morphData.points.Count} nodes to '{baseShapePath}'. Total morphs: {jsonData.morphs.Count}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"MorphEditorController: Failed to save morph. Error: {e.Message}");
            }
        }

        private string GenerateMorphName(List<MorphData> existing)
        {
            string prefix = string.IsNullOrWhiteSpace(morphNamePrefix) ? "Morph" : morphNamePrefix.Trim();
            switch (morphNaming)
            {
                case MorphNameStrategy.Timestamp:
                    return $"{prefix}_{System.DateTime.Now:yyyyMMdd_HHmmss}";
                case MorphNameStrategy.Guid:
                    string shortGuid = System.Guid.NewGuid().ToString("N").Substring(0, 8);
                    return $"{prefix}_{shortGuid}";
                case MorphNameStrategy.Sequential:
                default:
                    // 查找现有同前缀的最大序号，生成下一个
                    int maxIndex = 0;
                    var regex = new Regex($"^{Regex.Escape(prefix)}(?<n>\\d+)$", RegexOptions.IgnoreCase);
                    foreach (var m in existing)
                    {
                        if (m == null || string.IsNullOrEmpty(m.name)) continue;
                        var match = regex.Match(m.name);
                        if (match.Success && int.TryParse(match.Groups["n"].Value, out int n))
                        {
                            if (n > maxIndex) maxIndex = n;
                        }
                    }
                    return $"{prefix}{maxIndex + 1}";
            }
        }

        /// <summary>
        /// 获取当前加载形状的所有 Morph 名称列表（供 UI 显示用）
        /// </summary>
        public List<string> GetMorphNames()
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(baseShapePath) || !File.Exists(baseShapePath))
            {
                return result;
            }
            
            try
            {
                string json = File.ReadAllText(baseShapePath);
                var jsonData = JsonUtility.FromJson<BrushShapeJson>(json);
                if (jsonData?.morphs != null)
                {
                    foreach (var morph in jsonData.morphs)
                    {
                        if (morph != null && !string.IsNullOrEmpty(morph.name))
                        {
                            result.Add(morph.name);
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"MorphEditorController.GetMorphNames: Failed to read morphs from {baseShapePath}: {ex.Message}");
            }
            
            return result;
        }

        #endregion
    }
}
