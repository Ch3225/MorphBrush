using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRBrush.Core;
using VRBrush.Interface.Brush.ADBrush;

namespace VRBrush.Interface
{
    /// <summary>
    /// 实验2专用的Brush UI Manager
    /// 锁定使用完整版ADBrush（V04），提供四个界面：绘制、基础形状编辑、变形编辑、自由移动
    /// 启动后立即开始计时，记录撤销次数，支持导出OBJ和TOML
    /// </summary>
    public class BrushUIManager4Ex2 : MonoBehaviour
    {
        [Header("UI Controls - Two-Level Structure")]
        [SerializeField]
        private TMP_Dropdown brushSystemDropdown; // Level 1: 只有一个选项 "ADBrush (Full)"

        [SerializeField]
        private TMP_Dropdown interfaceDropdown; // Level 2: Available interfaces

        [Header("Common UI Controls")]
        [SerializeField]
        private Slider brushDistanceSlider;

        [SerializeField]
        private TMP_Text brushDistanceMinText;

        [SerializeField]
        private TMP_Text brushDistanceMaxText;

        [SerializeField]
        private Slider brushAngleUpSlider;

        [SerializeField]
        private TMP_Text brushAngleMinText;

        [SerializeField]
        private TMP_Text brushAngleMaxText;

        [SerializeField]
        private Button clearButton;

        [SerializeField]
        private Button undoButton; // 撤销按钮

        [Header("Shape/Morph Editor Controls")]
        [SerializeField]
        private Slider brushWidthSlider;

        [SerializeField]
        private TMP_Text brushWidthMinText;

        [SerializeField]
        private TMP_Text brushWidthMaxText;

        [SerializeField]
        private TMP_Dropdown brushShapeDropdown;

        [SerializeField]
        private Button saveBrushButton;

        [SerializeField]
        private TMP_Text saveBrushButtonText;

        [Header("Morph Items UI")]
        [SerializeField]
        private GameObject morphItemPrefab;

        [SerializeField]
        private Transform morphItemsParent;

        [Header("XR Locomotion Control")]
        [SerializeField]
        private GameObject locomotionSystemObject;

        [SerializeField]
        private MonoBehaviour[] locomotionComponents;

        [Header("Slider Settings")]
        [SerializeField]
        private float minBrushDistance = 0.0f;

        [SerializeField]
        private float maxBrushDistance = 0.0f;

        [SerializeField]
        private float minBrushAngleUp = 0f;

        [SerializeField]
        private float maxBrushAngleUp = 0f;

        [Header("Brush Hierarchy")]
        [SerializeField]
        private Transform brushesParent;

        [Header("Undo Manager")]
        [SerializeField]
        private StrokeUndoManager strokeUndoManager;

        [Header("Experiment Recorder")]
        [SerializeField]
        private ExperimentRecorder experimentRecorder;

        [SerializeField]
        private Button saveButton; // 保存按钮（导出OBJ+TOML）

        // Interface信息结构
        private class InterfaceInfo
        {
            public string DisplayName;
            public System.Type ControllerType;
            public bool IsLocomotionEnabled;

            public InterfaceInfo(string name, System.Type type, bool enableLocomotion = false)
            {
                DisplayName = name;
                ControllerType = type;
                IsLocomotionEnabled = enableLocomotion;
            }
        }

        // 完整版ADBrush的界面列表（默认Free Move）
        private List<InterfaceInfo> interfaces;

        // 状态追踪
        private InterfaceInfo currentInterface;
        private Dictionary<System.Type, MonoBehaviour> controllerInstances = new Dictionary<System.Type, MonoBehaviour>();
        
        private List<IBrushController> availableBrushes = new List<IBrushController>();
        private IBrushController activeBrush;
        private BrushEditorController brushEditor;
        private bool isInEditMode = false;
        private List<string> availableShapes = new List<string>();

        private readonly List<VRBrush.Interface.UI.MorphItemUI> morphItems = new List<VRBrush.Interface.UI.MorphItemUI>();

        void Awake()
        {
            InitializeInterfaces();
            DiscoverControllers();
            
            brushEditor = FindFirstObjectByType<BrushEditorController>();
            if (brushEditor == null)
            {
                var allEditors = Resources.FindObjectsOfTypeAll<BrushEditorController>();
                if (allEditors != null && allEditors.Length > 0)
                {
                    brushEditor = allEditors[0];
                }
            }

            FindAllBrushes();
        }

        private void InitializeInterfaces()
        {
            // 完整版ADBrush的四个界面（默认Free Move）
            interfaces = new List<InterfaceInfo>()
            {
                new InterfaceInfo("Free Move", typeof(NoActionController), true),
                new InterfaceInfo("Drawing", typeof(ADBrushController)),
                new InterfaceInfo("Base Shape Editor", typeof(BrushEditorController)),
                new InterfaceInfo("Morph Editor", typeof(MorphEditorController))
            };
        }

        private void DiscoverControllers()
        {
            controllerInstances.Clear();
            
            var allTypes = new System.Type[]
            {
                typeof(NoActionController),
                typeof(ADBrushController),
                typeof(BrushEditorController),
                typeof(MorphEditorController)
            };

            foreach (var type in allTypes)
            {
                MonoBehaviour instance = null;
                
                if (brushesParent != null)
                {
                    var found = brushesParent.GetComponentsInChildren(type, true);
                    if (found != null && found.Length > 0)
                    {
                        instance = found[0] as MonoBehaviour;
                    }
                }
                else
                {
                    instance = FindFirstObjectByType(type, FindObjectsInactive.Include) as MonoBehaviour;
                }

                if (instance != null)
                {
                    controllerInstances[type] = instance;
                    Debug.Log($"BrushUIManager4Ex2: Found {type.Name}: {instance.gameObject.name}");
                }
            }
        }

        void Start()
        {
            InitializeUI();
            InitializeDropdowns();
            
            // 查找ExperimentRecorder（计时会在检测到第一笔时自动开始）
            if (experimentRecorder == null)
            {
                experimentRecorder = FindFirstObjectByType<ExperimentRecorder>();
            }
        }

        private void InitializeDropdowns()
        {
            // 初始化 Brush System 下拉菜单（只有一个选项，锁定为 ADBrush Full）
            if (brushSystemDropdown != null)
            {
                brushSystemDropdown.ClearOptions();
                brushSystemDropdown.AddOptions(new List<string> { "ADBrush (Full)" });
                brushSystemDropdown.value = 0;
                brushSystemDropdown.interactable = false; // 禁用交互，因为只有一个选项
            }

            // 初始化 Interface 下拉菜单
            if (interfaceDropdown == null)
            {
                Debug.LogWarning("BrushUIManager4Ex2: Interface dropdown not assigned");
                return;
            }

            interfaceDropdown.ClearOptions();
            var interfaceNames = interfaces.Select(i => i.DisplayName).ToList();
            interfaceDropdown.AddOptions(interfaceNames);
            interfaceDropdown.value = 0; // 默认Free Move
            interfaceDropdown.onValueChanged.AddListener(OnInterfaceChanged);

            // 设置初始界面
            OnInterfaceChanged(0);

            Debug.Log("BrushUIManager4Ex2: UI initialized (ADBrush Full locked)");
        }

        private void OnInterfaceChanged(int index)
        {
            if (index < 0 || index >= interfaces.Count)
            {
                return;
            }

            currentInterface = interfaces[index];
            Debug.Log($"BrushUIManager4Ex2: Interface changed to {currentInterface.DisplayName}");

            ActivateController(currentInterface.ControllerType);
            SetLocomotionEnabled(currentInterface.IsLocomotionEnabled);
            UpdateUIVisibility();
        }

        private void ActivateController(System.Type controllerType)
        {
            foreach (var kvp in controllerInstances)
            {
                if (kvp.Value != null)
                {
                    CleanupControllerVisuals(kvp.Value);
                    kvp.Value.enabled = false;
                }
            }

            if (controllerInstances.TryGetValue(controllerType, out var controller))
            {
                if (controller != null)
                {
                    if (!controller.gameObject.activeSelf)
                    {
                        controller.gameObject.SetActive(true);
                    }
                    
                    controller.enabled = true;

                    if (controller is IBrushController brushCtrl)
                    {
                        activeBrush = brushCtrl;
                        UpdateCommonSliders();
                        ShowUniversalBrushPreview(controller);
                    }

                    if (controller is BrushEditorController || controller is MorphEditorController)
                    {
                        ShowEditorDisplay(controller);
                    }

                    isInEditMode = (controllerType == typeof(BrushEditorController));
                    
                    if (controllerType == typeof(ADBrushController))
                    {
                        StartCoroutine(DelayedMorphUISetup());
                    }
                }
            }
        }

        private void CleanupControllerVisuals(MonoBehaviour controller)
        {
            if (controller == null) return;

            var controllerType = controller.GetType();

            if (controller is IBrushController)
            {
                var previewField = controllerType.BaseType?.GetField(
                    "universalBrushPreview",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                if (previewField != null)
                {
                    var preview = previewField.GetValue(controller) as UniversalBrushPreview;
                    if (preview != null)
                    {
                        preview.Hide();
                    }
                }
            }

            if (controller is BrushEditorController)
            {
                var displayField = controllerType.GetField(
                    "editorDisplay",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                if (displayField != null)
                {
                    var display = displayField.GetValue(controller) as VRBrush.Core.Visual.BrushEditorDisplay;
                    if (display != null)
                    {
                        display.Hide();
                    }
                }
            }
        }

        private void ShowUniversalBrushPreview(MonoBehaviour controller)
        {
            if (controller == null) return;

            var controllerType = controller.GetType();
            var previewField = controllerType.BaseType?.GetField(
                "universalBrushPreview",
                BindingFlags.NonPublic | BindingFlags.Instance
            );

            if (previewField != null)
            {
                var preview = previewField.GetValue(controller) as UniversalBrushPreview;
                if (preview != null)
                {
                    preview.Show();
                }
            }
        }

        private void ShowEditorDisplay(MonoBehaviour controller)
        {
            if (controller == null) return;

            var controllerType = controller.GetType();
            var displayField = controllerType.GetField(
                "editorDisplay",
                BindingFlags.NonPublic | BindingFlags.Instance
            );

            if (displayField != null)
            {
                var display = displayField.GetValue(controller);
                if (display != null)
                {
                    var showMethod = display.GetType().GetMethod("Show");
                    if (showMethod != null)
                    {
                        showMethod.Invoke(display, null);
                    }
                }
            }
        }

        private void SetLocomotionEnabled(bool enabled)
        {
            if (locomotionSystemObject != null)
            {
                locomotionSystemObject.SetActive(enabled);
            }

            if (locomotionComponents != null)
            {
                foreach (var component in locomotionComponents)
                {
                    if (component != null)
                    {
                        component.enabled = enabled;
                    }
                }
            }
        }

        private System.Collections.IEnumerator DelayedMorphUISetup()
        {
            yield return null;
            
            if (morphItemsParent != null)
            {
                SetupMorphItemsForADBrush();
            }
        }

        private void UpdateUIVisibility()
        {
            bool isBrushEditor = currentInterface != null && currentInterface.ControllerType == typeof(BrushEditorController);
            bool isMorphEditor = currentInterface != null && currentInterface.ControllerType == typeof(MorphEditorController);
            bool isDrawingMode = currentInterface != null && currentInterface.ControllerType == typeof(ADBrushController);

            bool showSaveButton = isBrushEditor || isMorphEditor;

            if (saveBrushButton != null)
            {
                saveBrushButton.gameObject.SetActive(showSaveButton);
                
                if (showSaveButton && saveBrushButtonText != null)
                {
                    saveBrushButtonText.text = isBrushEditor ? "Save Base Shape" : "Save Morphs";
                }
            }

            if (morphItemsParent != null)
            {
                bool showMorphUI = isDrawingMode || isBrushEditor || isMorphEditor;
                morphItemsParent.gameObject.SetActive(showMorphUI);
                
                if (showMorphUI)
                {
                    SetupMorphItemsForADBrush();
                }
            }

            if (brushShapeDropdown != null)
            {
                bool showShapeDropdown = isBrushEditor || isMorphEditor || isDrawingMode;
                brushShapeDropdown.gameObject.SetActive(showShapeDropdown);
                
                if (showShapeDropdown)
                {
                    UpdateBrushShapeDropdown();
                }
            }
        }

        private void UpdateCommonSliders()
        {
            if (activeBrush == null) return;

            if (brushDistanceSlider != null)
            {
                brushDistanceSlider.value = activeBrush.BrushDistance;
            }

            if (brushAngleUpSlider != null)
            {
                brushAngleUpSlider.value = activeBrush.BrushAngleUp;
            }

            if (brushWidthSlider != null)
            {
                RefreshWidthSliderRange();
                brushWidthSlider.SetValueWithoutNotify(activeBrush.BrushWidth);

                var range = activeBrush.BrushWidthRange;
                UpdateValueText(brushWidthMinText, range.x, "m", 3);
                UpdateValueText(brushWidthMaxText, range.y, "m", 3);
            }
        }

        private void SetupMorphItemsForADBrush()
        {
            if (morphItemPrefab == null || morphItemsParent == null) return;

            // 检查是否为 MorphEditor 模式
            bool isMorphEditor = currentInterface != null && currentInterface.ControllerType == typeof(MorphEditorController);
            if (isMorphEditor)
            {
                SetupMorphItemsForMorphEditor();
                return;
            }

            if (!controllerInstances.TryGetValue(typeof(ADBrushController), out var controller)) return;

            var adBrush = controller as ADBrushController;
            if (adBrush == null) return;

            if (!adBrush.gameObject.activeInHierarchy)
            {
                adBrush.gameObject.SetActive(true);
            }

            var morphNames = adBrush.GetMorphNames();
            var morphWeights = adBrush.GetMorphWeights();

            if (morphNames == null || morphWeights == null || morphNames.Count == 0) return;

            ClearMorphItems();

            for (int i = 0; i < morphNames.Count; i++)
            {
                var go = Instantiate(morphItemPrefab, morphItemsParent);
                go.name = $"MorphItem_{morphNames[i]}";
                
                var morphItemUI = go.GetComponent<VRBrush.Interface.UI.MorphItemUI>();
                if (morphItemUI == null)
                {
                    morphItemUI = go.AddComponent<VRBrush.Interface.UI.MorphItemUI>();
                }

                int morphIndex = i;
                float currentWeight = (i < morphWeights.Count) ? morphWeights[i] : 0f;
                
                morphItemUI.Initialize(
                    morphNames[i],
                    morphIndex,
                    0f,
                    1f,
                    currentWeight,
                    (idx, newValue) => adBrush.OnExternalMorphValueChanged(idx, newValue),
                    "Morph"
                );

                morphItems.Add(morphItemUI);
            }
        }

        /// <summary>
        /// 为 MorphEditor 模式设置 Morph 项目列表（只读显示已有 morphs）
        /// </summary>
        private void SetupMorphItemsForMorphEditor()
        {
            ClearMorphItems();
            
            if (!controllerInstances.TryGetValue(typeof(MorphEditorController), out var morphMono))
            {
                return;
            }
            
            var morphEditor = morphMono as MorphEditorController;
            if (morphEditor == null) return;
            
            var morphNames = morphEditor.GetMorphNames();
            if (morphNames == null || morphNames.Count == 0)
            {
                Debug.Log("BrushUIManager4Ex2: No morphs found for current shape in MorphEditor");
                return;
            }
            
            for (int i = 0; i < morphNames.Count; i++)
            {
                var go = Instantiate(morphItemPrefab, morphItemsParent);
                go.name = $"MorphItem_{morphNames[i]}";
                
                var morphItemUI = go.GetComponent<VRBrush.Interface.UI.MorphItemUI>();
                if (morphItemUI == null)
                {
                    morphItemUI = go.AddComponent<VRBrush.Interface.UI.MorphItemUI>();
                }
                
                int morphIndex = i;
                string morphName = morphNames[i];
                
                morphItemUI.Initialize(
                    morphName,
                    morphIndex,
                    0f,
                    1f,
                    0f,
                    null,
                    "Saved"
                );
                
                morphItems.Add(morphItemUI);
            }
            
            Debug.Log($"BrushUIManager4Ex2: Setup {morphNames.Count} morph items for MorphEditor");
        }

        private void ClearMorphItems()
        {
            foreach (var morphItem in morphItems)
            {
                if (morphItem != null)
                {
                    Destroy(morphItem.gameObject);
                }
            }
            morphItems.Clear();
        }

        private void FindAllBrushes()
        {
            availableBrushes.Clear();
            var allBrushes = new List<IBrushController>();

            if (brushesParent != null)
            {
                var childBrushes = brushesParent.GetComponentsInChildren<MonoBehaviour>(true).OfType<IBrushController>();
                allBrushes.AddRange(childBrushes);
            }
            else
            {
                var foundActive = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                allBrushes.AddRange(foundActive.OfType<IBrushController>());
            }

            availableBrushes = allBrushes.GroupBy(b => b.BrushName).Select(g => g.First()).ToList();
        }

        private void InitializeUI()
        {
            if (brushDistanceSlider != null)
            {
                float min = minBrushDistance != 0f ? minBrushDistance : brushDistanceSlider.minValue;
                float max = maxBrushDistance != 0f ? maxBrushDistance : brushDistanceSlider.maxValue;
                brushDistanceSlider.minValue = min;
                brushDistanceSlider.maxValue = max;

                if (brushDistanceMinText != null)
                    UpdateValueText(brushDistanceMinText, min, "m", 2);
                if (brushDistanceMaxText != null)
                    UpdateValueText(brushDistanceMaxText, max, "m", 2);

                brushDistanceSlider.onValueChanged.AddListener(OnBrushDistanceChanged);
            }

            if (brushAngleUpSlider != null)
            {
                float min = minBrushAngleUp != 0f ? minBrushAngleUp : brushAngleUpSlider.minValue;
                float max = maxBrushAngleUp != 0f ? maxBrushAngleUp : brushAngleUpSlider.maxValue;
                brushAngleUpSlider.minValue = min;
                brushAngleUpSlider.maxValue = max;

                if (brushAngleMinText != null)
                    UpdateValueText(brushAngleMinText, min, "°", 0);
                if (brushAngleMaxText != null)
                    UpdateValueText(brushAngleMaxText, max, "°", 0);

                brushAngleUpSlider.onValueChanged.AddListener(OnBrushAngleUpChanged);
            }

            if (clearButton != null)
            {
                clearButton.onClick.AddListener(OnClearButtonClicked);
            }

            if (undoButton != null)
            {
                undoButton.onClick.AddListener(OnUndoButtonClicked);
            }

            if (saveButton != null)
            {
                saveButton.onClick.AddListener(OnSaveButtonClicked);
            }

            if (brushWidthSlider != null)
            {
                RefreshWidthSliderRange();
                brushWidthSlider.onValueChanged.AddListener(OnBrushWidthChanged);
            }

            if (saveBrushButton != null)
            {
                saveBrushButton.onClick.AddListener(OnSaveBrushClicked);
            }

            InitializeBrushShapeDropdown();
        }

        private void OnBrushDistanceChanged(float value)
        {
            if (activeBrush != null)
            {
                activeBrush.SetBrushDistance(value);
            }
        }

        private void OnBrushAngleUpChanged(float value)
        {
            if (activeBrush != null)
            {
                activeBrush.SetBrushAngleUp(value);
            }
        }

        private void OnBrushWidthChanged(float value)
        {
            if (activeBrush != null)
            {
                activeBrush.SetBrushWidth(value);
            }
        }

        private void RefreshWidthSliderRange()
        {
            if (brushWidthSlider == null || activeBrush == null) return;
            var range = activeBrush.BrushWidthRange;
            brushWidthSlider.minValue = range.x;
            brushWidthSlider.maxValue = range.y;
        }

        private void OnClearButtonClicked()
        {
            foreach (var brush in availableBrushes)
            {
                brush.ClearAllStrokes();
            }
            Debug.Log("BrushUIManager4Ex2: Cleared all strokes");
        }

        private void OnUndoButtonClicked()
        {
            if (strokeUndoManager == null)
            {
                strokeUndoManager = FindFirstObjectByType<StrokeUndoManager>();
            }
            
            if (strokeUndoManager != null)
            {
                bool undoSuccess = strokeUndoManager.UndoLastStroke();
                
                // 只有撤销成功时才通知ExperimentRecorder撤销次数增加
                if (undoSuccess && experimentRecorder != null)
                {
                    experimentRecorder.IncrementUndoCount();
                }
            }
        }

        private void OnSaveButtonClicked()
        {
            // 导出所有笔画
            if (experimentRecorder == null)
            {
                experimentRecorder = FindFirstObjectByType<ExperimentRecorder>();
            }

            if (experimentRecorder != null)
            {
                // 设置实验信息
                string expType = "Ex2";
                string variant = activeBrush != null ? activeBrush.BrushName : "Unknown";
                string shape = GetCurrentShapeName();
                experimentRecorder.SetExperimentInfo(expType, variant, shape);
                
                experimentRecorder.ExportAll();
            }
            else
            {
                Debug.LogWarning("BrushUIManager4Ex2: ExperimentRecorder not found, cannot export");
            }
        }

        private string GetCurrentShapeName()
        {
            if (brushShapeDropdown != null && brushShapeDropdown.value >= 0 && brushShapeDropdown.value < availableShapes.Count)
            {
                return availableShapes[brushShapeDropdown.value];
            }
            return "Unknown";
        }

        private void InitializeBrushShapeDropdown()
        {
            if (brushShapeDropdown == null) return;
            brushShapeDropdown.onValueChanged.RemoveListener(OnBrushShapeChanged);
            UpdateBrushShapeDropdown();
            brushShapeDropdown.onValueChanged.AddListener(OnBrushShapeChanged);
        }

        private void UpdateBrushShapeDropdown()
        {
            if (brushShapeDropdown == null) return;

            if (activeBrush != null)
            {
                var method = activeBrush.GetType().GetMethod("GetAvailableShapeNames");
                if (method != null)
                {
                    var shapes = method.Invoke(activeBrush, null) as List<string>;
                    if (shapes != null)
                    {
                        availableShapes.Clear();
                        availableShapes.AddRange(shapes);
                    }
                }
            }

            brushShapeDropdown.ClearOptions();
            
            if (isInEditMode)
            {
                var options = new List<string> { "New Brush" };
                options.AddRange(availableShapes);
                brushShapeDropdown.AddOptions(options);
            }
            else
            {
                brushShapeDropdown.AddOptions(availableShapes);
            }
        }

        private void OnBrushShapeChanged(int index)
        {
            if (index < 0 || index >= availableShapes.Count) return;
            string shapeName = availableShapes[index];
            
            if (currentInterface != null)
            {
                var controllerType = currentInterface.ControllerType;
                
                // BrushEditorController 创建新形状，不需要从 Dropdown 加载
                if (controllerType == typeof(BrushEditorController))
                {
                    return;
                }
                
                // MorphEditorController 需要加载形状进行编辑
                if (controllerType == typeof(MorphEditorController))
                {
                    if (controllerInstances.TryGetValue(typeof(MorphEditorController), out var morphMono))
                    {
                        var morphEditor = morphMono as MorphEditorController;
                        if (morphEditor != null)
                        {
                            morphEditor.ActivateShape(shapeName);
                            Debug.Log($"BrushUIManager4Ex2: Activated shape '{shapeName}' for MorphEditor");
                            // 切换形状后更新 Morph UI
                            SetupMorphItemsForMorphEditor();
                        }
                    }
                    return;
                }
            }

            if (activeBrush == null) return;
            
            var activateMethod = activeBrush.GetType().GetMethod("ActivateShape");
            if (activateMethod != null)
            {
                activateMethod.Invoke(activeBrush, new object[] { shapeName });
                // 切换形状后更新 Morph UI (绘制模式)
                SetupMorphItemsForADBrush();
            }
        }

        private void OnSaveBrushClicked()
        {
            if (currentInterface != null && currentInterface.ControllerType == typeof(BrushEditorController))
            {
                if (controllerInstances.TryGetValue(typeof(BrushEditorController), out var editorMono))
                {
                    var editor = editorMono as BrushEditorController;
                    if (editor != null)
                    {
                        editor.SaveCurrentBrush(null);
                        UpdateBrushShapeDropdown();
                    }
                }
            }
            else if (currentInterface != null && currentInterface.ControllerType == typeof(MorphEditorController))
            {
                if (controllerInstances.TryGetValue(typeof(MorphEditorController), out var morphMono))
                {
                    var morphEditor = morphMono as MorphEditorController;
                    if (morphEditor != null)
                    {
                        morphEditor.SaveCurrentMorph();
                    }
                }
            }
        }

        private void UpdateValueText(TMP_Text target, float value, string unit, int decimals)
        {
            if (target == null) return;
            string fmt = decimals > 0 ? value.ToString("F" + decimals) : Mathf.RoundToInt(value).ToString();
            target.text = fmt + unit;
        }

        public void SetBrushesParent(Transform parent)
        {
            brushesParent = parent;
        }

        public IBrushController GetActiveBrush()
        {
            return activeBrush;
        }
    }
}
