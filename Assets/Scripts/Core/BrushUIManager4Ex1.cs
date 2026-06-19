using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRBrush.Core;
using VRBrush.Interface.Brush.ADBrush;
using VRBrush.Interface.Brush.AdaptiBrush;
using VRBrush.Interface.Brush.GravitySketch;
using VRBrush.Interface.Brush.CavePainting;

namespace VRBrush.Interface
{
    /// <summary>
    /// 实验1专用的Brush UI Manager
    /// 管理四个ADBrush变体和对应的界面选项
    /// V01: 无Rolling控制，无自定义截面功能 -> 界面：绘制、自由移动
    /// V02: 无Rolling控制，有自定义截面功能 -> 界面：绘制、基础形状编辑、变形编辑、自由移动
    /// V03: 有Rolling控制，无自定义截面功能 -> 界面：绘制、自由移动
    /// V04(ADBrush): 有Rolling控制，有自定义截面功能 -> 界面：绘制、基础形状编辑、变形编辑、自由移动
    /// </summary>
    public class BrushUIManager4Ex1 : MonoBehaviour
    {
        [Header("UI Controls - Two-Level Structure")]
        [SerializeField]
        private TMP_Dropdown brushSystemDropdown; // Level 1: V01, V02, V03, V04

        [SerializeField]
        private TMP_Dropdown interfaceDropdown; // Level 2: Available interfaces for selected system

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

        // 实验1的笔刷系统
        private enum Ex1BrushSystem
        {
            GravitySketch,
            CavePainting,
            AdaptiBrush,
            ADBrush
        }

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

        // 各系统对应的界面映射
        private Dictionary<Ex1BrushSystem, List<InterfaceInfo>> systemInterfaceMap;

        // 状态追踪
        private Ex1BrushSystem currentSystem = Ex1BrushSystem.GravitySketch;
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
            InitializeSystemInterfaceMap();
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

        private void InitializeSystemInterfaceMap()
        {
            systemInterfaceMap = new Dictionary<Ex1BrushSystem, List<InterfaceInfo>>()
            {
                // GravitySketch: 交叉积方法（tangent × Up），丝带笔刷
                {
                    Ex1BrushSystem.GravitySketch, new List<InterfaceInfo>()
                    {
                        new InterfaceInfo("Free Move", typeof(NoActionController), true),
                        new InterfaceInfo("Drawing", typeof(GravitySketchController))
                    }
                },
                // CavePainting: 直接方法（Up轴），丝带笔刷
                {
                    Ex1BrushSystem.CavePainting, new List<InterfaceInfo>()
                    {
                        new InterfaceInfo("Free Move", typeof(NoActionController), true),
                        new InterfaceInfo("Drawing", typeof(CavePaintingController))
                    }
                },
                // AdaptiBrush: 自适应方法，丝带笔刷
                {
                    Ex1BrushSystem.AdaptiBrush, new List<InterfaceInfo>()
                    {
                        new InterfaceInfo("Free Move", typeof(NoActionController), true),
                        new InterfaceInfo("Drawing", typeof(AdaptiBrushController))
                    }
                },
                // ADBrush: 完整版（有Rolling，有自定义截面）
                {
                    Ex1BrushSystem.ADBrush, new List<InterfaceInfo>()
                    {
                        new InterfaceInfo("Free Move", typeof(NoActionController), true),
                        new InterfaceInfo("Drawing", typeof(ADBrushController)),
                        new InterfaceInfo("Base Shape Editor", typeof(BrushEditorController)),
                        new InterfaceInfo("Morph Editor", typeof(MorphEditorController))
                    }
                }
            };
        }

        private void DiscoverControllers()
        {
            controllerInstances.Clear();
            
            var allTypes = new System.Type[]
            {
                typeof(NoActionController),
                typeof(GravitySketchController),
                typeof(CavePaintingController),
                typeof(AdaptiBrushController),
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
                    Debug.Log($"BrushUIManager4Ex1: Found {type.Name}: {instance.gameObject.name}");
                }
            }
        }

        void Start()
        {
            InitializeUI();
            InitializeNewUI();
        }

        private void InitializeNewUI()
        {
            if (brushSystemDropdown == null || interfaceDropdown == null)
            {
                Debug.LogWarning("BrushUIManager4Ex1: UI dropdowns not assigned");
                return;
            }

            // 初始化Brush System下拉菜单
            brushSystemDropdown.ClearOptions();
            var systemNames = new List<string>
            {
                "GravitySketch",
                "CavePainting",
                "AdaptiBrush",
                "ADBrush (Full)"
            };
            brushSystemDropdown.AddOptions(systemNames);
            brushSystemDropdown.value = 0;
            brushSystemDropdown.onValueChanged.AddListener(OnBrushSystemChanged);

            UpdateInterfaceDropdown();
            interfaceDropdown.onValueChanged.AddListener(OnInterfaceChanged);

            // 默认选择Free Move（索引0）
            OnInterfaceChanged(0);

            Debug.Log("BrushUIManager4Ex1: UI initialized");
        }

        private void UpdateInterfaceDropdown()
        {
            if (interfaceDropdown == null) return;

            interfaceDropdown.ClearOptions();
            
            if (systemInterfaceMap.TryGetValue(currentSystem, out var interfaces))
            {
                var interfaceNames = interfaces.Select(i => i.DisplayName).ToList();
                interfaceDropdown.AddOptions(interfaceNames);
            }
        }

        private void OnBrushSystemChanged(int index)
        {
            currentSystem = (Ex1BrushSystem)index;
            Debug.Log($"BrushUIManager4Ex1: Brush system changed to {currentSystem}");
            
            UpdateInterfaceDropdown();
            interfaceDropdown.value = 0; // 默认选择Free Move
            OnInterfaceChanged(0);
        }

        private void OnInterfaceChanged(int index)
        {
            if (!systemInterfaceMap.TryGetValue(currentSystem, out var interfaces))
            {
                return;
            }

            if (index < 0 || index >= interfaces.Count)
            {
                return;
            }

            currentInterface = interfaces[index];
            Debug.Log($"BrushUIManager4Ex1: Interface changed to {currentInterface.DisplayName}");

            ActivateController(currentInterface.ControllerType);
            SetLocomotionEnabled(currentInterface.IsLocomotionEnabled);
            UpdateUIVisibility();
        }

        private void ActivateController(System.Type controllerType)
        {
            // 先禁用所有控制器
            foreach (var kvp in controllerInstances)
            {
                if (kvp.Value != null)
                {
                    CleanupControllerVisuals(kvp.Value);
                    kvp.Value.enabled = false;
                }
            }

            // 激活选中的控制器
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
                    
                    // 如果激活的是支持Morph的控制器，设置Morph UI
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
            
            bool isCustomSystem = currentSystem == Ex1BrushSystem.ADBrush;
            
            if (isCustomSystem && morphItemsParent != null)
            {
                SetupMorphItemsForCurrentBrush();
            }
        }

        private void UpdateUIVisibility()
        {
            bool isBrushEditor = currentInterface != null && currentInterface.ControllerType == typeof(BrushEditorController);
            bool isMorphEditor = currentInterface != null && currentInterface.ControllerType == typeof(MorphEditorController);
            bool isCustomSystem = currentSystem == Ex1BrushSystem.ADBrush;
            bool isDrawingMode = currentInterface != null && 
                                (currentInterface.ControllerType == typeof(GravitySketchController) ||
                                 currentInterface.ControllerType == typeof(CavePaintingController) ||
                                 currentInterface.ControllerType == typeof(AdaptiBrushController) ||
                                 currentInterface.ControllerType == typeof(ADBrushController));

            bool showSaveButton = isBrushEditor || isMorphEditor;

            if (saveBrushButton != null)
            {
                saveBrushButton.gameObject.SetActive(showSaveButton);
                
                if (showSaveButton && saveBrushButtonText != null)
                {
                    saveBrushButtonText.text = isBrushEditor ? "Save Base Shape" : "Save Morphs";
                }
            }

            // 只有支持自定义截面的系统才显示Morph UI
            if (morphItemsParent != null)
            {
                bool showMorphUI = isCustomSystem && (isDrawingMode || isBrushEditor || isMorphEditor);
                morphItemsParent.gameObject.SetActive(showMorphUI);
                
                if (showMorphUI)
                {
                    SetupMorphItemsForCurrentBrush();
                }
            }

            // 只有支持自定义截面的系统才显示Shape下拉菜单
            if (brushShapeDropdown != null)
            {
                bool showShapeDropdown = isCustomSystem && (isBrushEditor || isMorphEditor || isDrawingMode);
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

        private void SetupMorphItemsForCurrentBrush()
        {
            Debug.Log($"BrushUIManager4Ex1.SetupMorphItemsForCurrentBrush: morphItemPrefab={morphItemPrefab != null}, morphItemsParent={morphItemsParent != null}, currentSystem={currentSystem}");
            
            if (morphItemPrefab == null || morphItemsParent == null)
            {
                Debug.LogWarning($"BrushUIManager4Ex1: Cannot setup morph items - morphItemPrefab={(morphItemPrefab != null ? "OK" : "NULL")}, morphItemsParent={(morphItemsParent != null ? "OK" : "NULL")}");
                return;
            }

            // 检查是否为 MorphEditor 模式
            bool isMorphEditor = currentInterface != null && currentInterface.ControllerType == typeof(MorphEditorController);
            if (isMorphEditor)
            {
                SetupMorphItemsForMorphEditor();
                return;
            }

            // 只有 ADBrush 系统支持 Morph
            if (currentSystem != Ex1BrushSystem.ADBrush)
            {
                Debug.Log($"BrushUIManager4Ex1: Current system {currentSystem} does not support morphs, clearing.");
                ClearMorphItems();
                return;
            }

            System.Type brushType = typeof(ADBrushController);

            if (!controllerInstances.TryGetValue(brushType, out var controller))
            {
                Debug.LogWarning($"BrushUIManager4Ex1: Controller of type {brushType.Name} not found in controllerInstances");
                return;
            }

            List<string> morphNames = null;
            List<float> morphWeights = null;
            System.Action<int, float> onMorphChanged = null;

            if (controller is ADBrushController full)
            {
                morphNames = full.GetMorphNames();
                morphWeights = full.GetMorphWeights();
                onMorphChanged = (idx, val) => full.OnExternalMorphValueChanged(idx, val);
                Debug.Log($"BrushUIManager4Ex1: Got morphs from ADBrush - names={morphNames?.Count ?? 0}, weights={morphWeights?.Count ?? 0}");
            }

            if (morphNames == null || morphWeights == null || morphNames.Count == 0)
            {
                Debug.LogWarning($"BrushUIManager4Ex1: No morph data available - names={(morphNames != null ? morphNames.Count.ToString() : "null")}, weights={(morphWeights != null ? morphWeights.Count.ToString() : "null")}");
                return;
            }

            ClearMorphItems();
            Debug.Log($"BrushUIManager4Ex1: Creating {morphNames.Count} morph items");

            for (int i = 0; i < morphNames.Count; i++)
            {
                var go = Instantiate(morphItemPrefab, morphItemsParent);
                go.name = $"MorphItem_{morphNames[i]}";
                Debug.Log($"BrushUIManager4Ex1: Created morph item '{morphNames[i]}' under {morphItemsParent.name}");
                
                var morphItemUI = go.GetComponent<VRBrush.Interface.UI.MorphItemUI>();
                if (morphItemUI == null)
                {
                    morphItemUI = go.AddComponent<VRBrush.Interface.UI.MorphItemUI>();
                }

                int morphIndex = i;
                float currentWeight = (i < morphWeights.Count) ? morphWeights[i] : 0f;
                var callback = onMorphChanged;
                
                morphItemUI.Initialize(
                    morphNames[i],
                    morphIndex,
                    0f,
                    1f,
                    currentWeight,
                    (idx, newValue) => callback(idx, newValue),
                    "Morph"
                );

                morphItems.Add(morphItemUI);
            }
            
            Debug.Log($"BrushUIManager4Ex1: Successfully created {morphItems.Count} morph items");
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
                Debug.Log("BrushUIManager4Ex1: No morphs found for current shape in MorphEditor");
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
                
                // MorphEditor 模式下，morphs 作为只读信息显示（没有权重滑块交互）
                // 或者可以用作选择要编辑的目标 morph
                morphItemUI.Initialize(
                    morphName,
                    morphIndex,
                    0f,
                    1f,
                    0f, // 初始权重为0，MorphEditor不使用权重
                    null, // 无回调 - MorphEditor 模式下只是显示列表
                    "Saved"
                );
                
                morphItems.Add(morphItemUI);
            }
            
            Debug.Log($"BrushUIManager4Ex1: Setup {morphNames.Count} morph items for MorphEditor");
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

            // 绑定保存按钮
            if (saveButton != null)
            {
                saveButton.onClick.AddListener(OnSaveButtonClicked);
            }

            // 查找ExperimentRecorder
            if (experimentRecorder == null)
            {
                experimentRecorder = FindFirstObjectByType<ExperimentRecorder>();
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
            Debug.Log("BrushUIManager4Ex1: Cleared all strokes");
        }

        private void OnUndoButtonClicked()
        {
            bool undoSuccess = false;
            
            if (strokeUndoManager != null)
            {
                undoSuccess = strokeUndoManager.UndoLastStroke();
            }
            else
            {
                // 尝试查找StrokeUndoManager
                strokeUndoManager = FindFirstObjectByType<StrokeUndoManager>();
                if (strokeUndoManager != null)
                {
                    undoSuccess = strokeUndoManager.UndoLastStroke();
                }
                else
                {
                    Debug.LogWarning("BrushUIManager4Ex1: StrokeUndoManager not found");
                }
            }
            
            // 如果撤销成功，增加ExperimentRecorder的计数
            if (undoSuccess && experimentRecorder != null)
            {
                experimentRecorder.IncrementUndoCount();
            }
        }

        private void OnSaveButtonClicked()
        {
            if (experimentRecorder != null)
            {
                // 设置实验信息
                string expType = "Ex1";
                string variant = currentSystem.ToString();
                string shape = GetCurrentShapeName();
                experimentRecorder.SetExperimentInfo(expType, variant, shape);
                
                experimentRecorder.ExportAll();
            }
            else
            {
                Debug.LogWarning("BrushUIManager4Ex1: ExperimentRecorder not found");
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

            // 从当前激活的笔刷加载形状
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
                            Debug.Log($"BrushUIManager4Ex1: Activated shape '{shapeName}' for MorphEditor");
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
                SetupMorphItemsForCurrentBrush();
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
