using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using VRBrush.Core;
using VRBrush.Interface.Brush.AdaptiBrush;
using VRBrush.Interface.Brush.ADBrush;
using VRBrush.Interface.Brush.HelioBrush;

namespace VRBrush.Interface
{
    /// <summary>
    /// Brush UI Manager
    /// Manages brush components in the scene directly
    /// </summary>
    public class BrushUIManager : MonoBehaviour
    {
        [Header("UI Controls - New Two-Level Structure")]
        [SerializeField]
        private TMP_Dropdown brushSystemDropdown; // Level 1: AdaptiBrush, HelioBrush, ADBrush

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

        [Header("Shape/Morph Editor Controls")]
        [SerializeField]
        private Slider brushWidthSlider; // Width slider placed under Shape/Morph area

        [SerializeField]
        private TMP_Text brushWidthMinText;

        [SerializeField]
        private TMP_Text brushWidthMaxText;

        [SerializeField]
        private TMP_Dropdown brushShapeDropdown; // Shape selection dropdown

        [SerializeField]
        private Button saveBrushButton; // Save button (text changes based on context)

        [SerializeField]
        private TMP_Text saveBrushButtonText; // Text component of save button

        [Header("Morph Items UI")]
        [SerializeField]
        private GameObject morphItemPrefab; // Prefab for a single morph row (MorphItem.prefab)

        [SerializeField]
        private Transform morphItemsParent; // Container where dynamic morph sliders will be instantiated

        [Header("XR Locomotion Control")]
        [SerializeField]
        private GameObject locomotionSystemObject; // XR Origin's Locomotion System or provider

        [SerializeField]
        private MonoBehaviour[] locomotionComponents; // Alternative: array of components to enable/disable

        [Header("Slider Settings (taken from UI sliders)")]
        [SerializeField]
        private float minBrushDistance = 0.0f; // default 0, can be overridden from UI

        [SerializeField]
        private float maxBrushDistance = 0.0f; // 0 means use slider's current max

        [SerializeField]
        private float minBrushAngleUp = 0f; // default 0

        [SerializeField]
        private float maxBrushAngleUp = 0f; // 0 means use slider's current max
        // Removed size slider settings and all brush prefabs

        [Header("Brush Hierarchy")]
        [Tooltip(
            "Parent object containing all brush GameObjects (Brushes folder in hierarchy). If not set, will search entire scene."
        )]
        [SerializeField]
        private Transform brushesParent;

        // Brush System enum
        private enum BrushSystem
        {
            AdaptiBrush,
            HelioBrush,
            ADBrush
        }

        // Interface mapping structure
        private class InterfaceInfo
        {
            public string DisplayName;
            public System.Type ControllerType;
            public bool IsLocomotionEnabled; // True for Free Move interface

            public InterfaceInfo(string name, System.Type type, bool enableLocomotion = false)
            {
                DisplayName = name;
                ControllerType = type;
                IsLocomotionEnabled = enableLocomotion;
            }
        }

        // System-to-interfaces mapping
        private Dictionary<BrushSystem, List<InterfaceInfo>> systemInterfaceMap = new Dictionary<BrushSystem, List<InterfaceInfo>>()
        {
            {
                BrushSystem.AdaptiBrush, new List<InterfaceInfo>()
                {
                    new InterfaceInfo("Free Move", typeof(NoActionController), true),
                    new InterfaceInfo("AdaptiBrush Drawing", typeof(AdaptiBrushController))
                }
            },
            {
                BrushSystem.HelioBrush, new List<InterfaceInfo>()
                {
                    new InterfaceInfo("Free Move", typeof(NoActionController), true),
                    new InterfaceInfo("Base Brush Editor", typeof(BrushEditorController)),
                    new InterfaceInfo("HelioBrush Drawing", typeof(HelioBrushController))
                }
            },
            {
                BrushSystem.ADBrush, new List<InterfaceInfo>()
                {
                    new InterfaceInfo("Free Move", typeof(NoActionController), true),
                    new InterfaceInfo("ADBrush Drawing", typeof(ADBrushController)),
                    new InterfaceInfo("Base Brush Editor", typeof(BrushEditorController)),
                    new InterfaceInfo("Morph Editor", typeof(MorphEditorController))
                }
            }
        };

        // State tracking
        private BrushSystem currentSystem = BrushSystem.AdaptiBrush;
        private InterfaceInfo currentInterface;
        private Dictionary<System.Type, MonoBehaviour> controllerInstances = new Dictionary<System.Type, MonoBehaviour>();
        
        // Legacy support
        private List<IBrushController> availableBrushes = new List<IBrushController>();
        private IBrushController activeBrush;
        private BrushEditorController brushEditor;
        private bool isInEditMode = false;
        private List<string> availableShapes = new List<string>();

    // Morph UI state
    private readonly List<VRBrush.Interface.UI.MorphItemUI> morphItems = new List<VRBrush.Interface.UI.MorphItemUI>();

        void Awake()
        {
            // Discover all controller instances in the scene
            DiscoverControllers();

            // Find existing editor controller - prefer active objects; fallback include inactive
            brushEditor = FindFirstObjectByType<BrushEditorController>();
            if (brushEditor == null)
            {
                // Fallback: include inactive objects
                var allEditors = Resources.FindObjectsOfTypeAll<BrushEditorController>();
                if (allEditors != null && allEditors.Length > 0)
                {
                    brushEditor = allEditors[0];
                    Debug.Log($"BrushUIManager: Found inactive BrushEditorController: {brushEditor.gameObject.name} (ID: {brushEditor.GetInstanceID()})");
                }
                else
                {
                    Debug.LogWarning("BrushUIManager: No BrushEditorController found in scene. Please manually add one to the scene.");
                    // DO NOT auto-create to avoid instance conflicts
                }
            }
            else
            {
                Debug.Log($"BrushUIManager: Found existing BrushEditorController: {brushEditor.gameObject.name} (ID: {brushEditor.GetInstanceID()})");
            }

            // Now find all brushes implementing IBrushController interface in the scene
            FindAllBrushes();

            if (availableBrushes.Count == 0)
            {
                Debug.LogError("No brushes implementing IBrushController interface found. Please ensure at least one brush controller is in the scene.");
                return;
            }

            // Load available brush shapes (kept for AdaptiBrush context)
            LoadAvailableShapes();
        }

        /// <summary>
        /// Register special controllers (editor, no action, etc.)
        /// </summary>
        private void RegisterSpecialControllers()
        {
            // Register editor controller
            if (brushEditor != null)
            {
                if (brushEditor is IBrushController brushCtrl && !availableBrushes.Contains(brushCtrl))
                {
                    availableBrushes.Add(brushCtrl);
                }
                // Ensure dynamically created brushEditor gets proper input configuration
                ConfigureDynamicController(brushEditor);
            }

            // Create and register no action controller
            var noActionController = FindFirstObjectByType<NoActionController>();
            if (noActionController == null)
            {
                GameObject noActionGO = new GameObject("NoActionController");
                noActionController = noActionGO.AddComponent<NoActionController>();
                noActionController.gameObject.SetActive(false);
            }

            // NoActionController does not implement IBrushController; configure it but do not add to availableBrushes
            ConfigureDynamicController(noActionController);

            // Don't reorder here - ordering will happen after in FindAllBrushes
            // availableBrushes = availableBrushes.OrderBy(b => b.BrushName).ToList();
        }

        /// <summary>
        /// Configure a dynamically created controller with proper input sources
        /// </summary>
        private void ConfigureDynamicController(object controllerObj)
        {
            // Find VRBrushSystem to get controller references
            var vrBrushSystem = FindFirstObjectByType<VRBrushSystem>();
            if (vrBrushSystem != null)
            {
                // Use reflection to get controller references from VRBrushSystem
                var rightControllerField = vrBrushSystem.GetType().GetField("rightControllerObject",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var leftControllerField = vrBrushSystem.GetType().GetField("leftControllerObject",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                var strokesParentField = vrBrushSystem.GetType().GetField("strokesParent",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (rightControllerField != null)
                {
                    var rightController = rightControllerField.GetValue(vrBrushSystem) as GameObject;
                    if (rightController != null)
                    {
                        if (controllerObj is IBrushController brushCtrl)
                        {
                            brushCtrl.SetInputSource(rightController);
                        }
                        else if (controllerObj is MonoBehaviour mb && mb.GetType().GetMethod("SetInputSource") != null)
                        {
                            mb.GetType().GetMethod("SetInputSource").Invoke(mb, new object[] { rightController });
                        }
                    }
                }

                if (strokesParentField != null)
                {
                    var strokesParent = strokesParentField.GetValue(vrBrushSystem) as Transform;
                    if (strokesParent != null)
                    {
                        if (controllerObj is IBrushController brushCtrl2)
                        {
                            brushCtrl2.SetStrokesParent(strokesParent);
                        }
                        else if (controllerObj is MonoBehaviour mb2 && mb2.GetType().GetMethod("SetStrokesParent") != null)
                        {
                            mb2.GetType().GetMethod("SetStrokesParent").Invoke(mb2, new object[] { strokesParent });
                        }
                    }
                }

                // For two-handed brushes, also set left controller if available
                if (leftControllerField != null)
                {
                    var leftController = leftControllerField.GetValue(vrBrushSystem) as GameObject;
                    if (leftController != null)
                    {
                        if (controllerObj is MonoBehaviour monoObj && monoObj.GetType().GetMethod("SetLeftInputSource") != null)
                        {
                            monoObj.GetType().GetMethod("SetLeftInputSource").Invoke(monoObj, new object[] { leftController });
                        }
                    }
                }

                string name = controllerObj is IBrushController bc ? bc.BrushName : (controllerObj is MonoBehaviour m ? m.gameObject.name : "UnknownController");
                Debug.Log($"BrushUIManager: Configured dynamic controller '{name}' with input sources");
            }
            else
            {
                string name = controllerObj is IBrushController bc2 ? bc2.BrushName : (controllerObj is MonoBehaviour m2 ? m2.gameObject.name : "UnknownController");
                Debug.LogWarning($"BrushUIManager: VRBrushSystem not found, couldn't configure controller '{name}'");
            }
        }

        void Start()
        {
            // Legacy support - initialize old UI if components are assigned
            InitializeUI();

            // Initialize the new two-level UI system
            // This MUST come after InitializeUI to ensure the new system takes precedence
            InitializeNewUI();

            // NOTE: We no longer call ForceRefreshBrushState() here because:
            // 1. It only manages IBrushController instances (drawing controllers)
            // 2. The new two-level system also manages IInterfaceController (e.g., NoActionController for Free Move)
            // 3. Calling ForceRefreshBrushState() here would override the interface controller set by InitializeNewUI()
            // 4. The new system's ActivateController() already handles enabling/disabling all controllers correctly

            // 在启动后，若当前笔刷支持形状且尚未指定激活形状，自动选择一个默认形状并同步下拉
            AutoActivateShapeForActiveBrush();
        }

        #region New Two-Level UI System

        /// <summary>
        /// Discover all controller instances in the scene and cache them
        /// </summary>
        private void DiscoverControllers()
        {
            controllerInstances.Clear();
            
            // Search for all controller types we need
            var allTypes = new System.Type[]
            {
                typeof(NoActionController),
                typeof(AdaptiBrushController),
                typeof(HelioBrushController),
                typeof(ADBrushController),
                typeof(BrushEditorController),
                typeof(MorphEditorController)
            };

            foreach (var type in allTypes)
            {
                // Search in brushesParent if assigned, otherwise search scene
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
                    Debug.Log($"BrushUIManager: Found {type.Name}: {instance.gameObject.name}");

                    // Subscribe to initialization events for ADBrushController so we can build morph UI when ready
                    if (type == typeof(ADBrushController))
                    {
                        // ADBrushController specific handling (if needed)
                        // Morph UI will be setup when controller is activated via DelayedMorphUISetup()
                    }
                }
                else
                {
                    Debug.LogWarning($"BrushUIManager: Could not find controller of type {type.Name}");
                }
            }
        }

        /// <summary>
        /// Initialize the new two-level UI system
        /// </summary>
        private void InitializeNewUI()
        {
            if (brushSystemDropdown == null || interfaceDropdown == null)
            {
                Debug.LogWarning("BrushUIManager: New UI dropdowns not assigned, skipping new UI initialization");
                return;
            }

            // Initialize Brush System dropdown
            brushSystemDropdown.ClearOptions();
            var systemNames = System.Enum.GetNames(typeof(BrushSystem));
            brushSystemDropdown.AddOptions(new List<string>(systemNames));
            brushSystemDropdown.value = 0;
            brushSystemDropdown.onValueChanged.AddListener(OnBrushSystemChanged);

            // Initialize Interface dropdown
            UpdateInterfaceDropdown();
            interfaceDropdown.onValueChanged.AddListener(OnInterfaceChanged);

            // Set initial interface
            OnInterfaceChanged(0);

            Debug.Log("BrushUIManager: New two-level UI initialized");
        }

        /// <summary>
        /// Update interface dropdown based on current brush system
        /// </summary>
        private void UpdateInterfaceDropdown()
        {
            if (interfaceDropdown == null) return;

            interfaceDropdown.ClearOptions();
            
            if (systemInterfaceMap.TryGetValue(currentSystem, out var interfaces))
            {
                var interfaceNames = interfaces.Select(i => i.DisplayName).ToList();
                interfaceDropdown.AddOptions(interfaceNames);
                Debug.Log($"BrushUIManager: Updated interface dropdown with {interfaceNames.Count} options for {currentSystem}");
            }
        }

        /// <summary>
        /// Handle brush system dropdown change
        /// </summary>
        private void OnBrushSystemChanged(int index)
        {
            currentSystem = (BrushSystem)index;
            Debug.Log($"BrushUIManager: Brush system changed to {currentSystem}");
            
            // Update interface dropdown and reset to first option
            UpdateInterfaceDropdown();
            interfaceDropdown.value = 0;
            OnInterfaceChanged(0);
        }

        /// <summary>
        /// Handle interface dropdown change
        /// </summary>
        private void OnInterfaceChanged(int index)
        {
            if (!systemInterfaceMap.TryGetValue(currentSystem, out var interfaces))
            {
                Debug.LogError($"BrushUIManager: No interfaces found for system {currentSystem}");
                return;
            }

            if (index < 0 || index >= interfaces.Count)
            {
                Debug.LogError($"BrushUIManager: Invalid interface index {index}");
                return;
            }

            currentInterface = interfaces[index];
            Debug.Log($"BrushUIManager: Interface changed to {currentInterface.DisplayName} (Type: {currentInterface.ControllerType.Name})");

            // Activate the selected controller
            ActivateController(currentInterface.ControllerType);

            // Control locomotion system
            SetLocomotionEnabled(currentInterface.IsLocomotionEnabled);

            // Update UI visibility
            UpdateUIVisibility();
        }

        /// <summary>
        /// Activate a specific controller and deactivate others
        /// </summary>
        private void ActivateController(System.Type controllerType)
        {
            // First, cleanup visual elements from all controllers before switching
            foreach (var kvp in controllerInstances)
            {
                if (kvp.Value != null)
                {
                    // Try to call cleanup methods on the controller
                    CleanupControllerVisuals(kvp.Value);
                    
                    // Then disable the controller script
                    kvp.Value.enabled = false;
                }
            }

            // Activate the selected controller
            if (controllerInstances.TryGetValue(controllerType, out var controller))
            {
                if (controller != null)
                {
                    // Ensure the GameObject is active so Awake/Start/Update run
                    if (!controller.gameObject.activeSelf)
                    {
                        controller.gameObject.SetActive(true);
                    }
                    
                    controller.enabled = true;
                    Debug.Log($"BrushUIManager: Activated {controllerType.Name}");

                    // Update activeBrush reference if it's a brush controller
                    if (controller is IBrushController brushCtrl)
                    {
                        activeBrush = brushCtrl;
                        UpdateCommonSliders();
                        
                        // Show universal brush preview for drawing controllers
                        ShowUniversalBrushPreview(controller);
                    }

                    // Show editor display for BrushEditor/MorphEditor
                    if (controller is BrushEditorController || controller is MorphEditorController)
                    {
                        ShowEditorDisplay(controller);
                    }

                    // Update edit mode state
                    isInEditMode = (controllerType == typeof(BrushEditorController));
                    
                    // If we just activated ADBrush, ensure morph UI is built if applicable
                    if (controllerType == typeof(ADBrushController))
                    {
                        // Use coroutine to delay setup by one frame
                        StartCoroutine(DelayedMorphUISetup());
                    }
                }
                else
                {
                    Debug.LogError($"BrushUIManager: Controller instance for {controllerType.Name} is null");
                }
            }
            else
            {
                Debug.LogError($"BrushUIManager: No controller instance found for type {controllerType.Name}");
            }
        }

        /// <summary>
        /// Cleanup visual elements (3D previews, editor discs, etc.) from a controller
        /// </summary>
        private void CleanupControllerVisuals(MonoBehaviour controller)
        {
            if (controller == null) return;

            var controllerType = controller.GetType();

            // 1. Hide universal brush preview (for all brush controllers)
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
                        Debug.Log($"BrushUIManager: Hid universal preview for {controllerType.Name}");
                    }
                }
            }

            // 2. Hide BrushEditor display (coordinate system, disc, etc.)
            if (controller is BrushEditorController editorCtrl)
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
                        Debug.Log($"BrushUIManager: Hid editor display for BrushEditorController");
                    }
                }
            }

            // 3. Hide MorphEditor display (if it has similar visuals)
            if (controller is MorphEditorController)
            {
                // MorphEditor may have similar display system
                var displayField = controllerType.GetField(
                    "editorDisplay",
                    BindingFlags.NonPublic | BindingFlags.Instance
                );

                if (displayField != null)
                {
                    var display = displayField.GetValue(controller);
                    if (display != null)
                    {
                        // Try to call Hide method if it exists
                        var hideMethod = display.GetType().GetMethod("Hide");
                        if (hideMethod != null)
                        {
                            hideMethod.Invoke(display, null);
                            Debug.Log($"BrushUIManager: Hid editor display for MorphEditorController");
                        }
                    }
                }
            }

            // 4. Clear any active strokes being drawn (optional)
            if (controller is IBrushController brushCtrl && brushCtrl != null)
            {
                // Don't clear completed strokes, just ensure no preview/drawing state remains
                // Controllers will handle this in their OnDisable if needed
            }
        }

        /// <summary>
        /// Show universal brush preview for a brush controller
        /// </summary>
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
                    Debug.Log($"BrushUIManager: Showed universal preview for {controllerType.Name}");
                }
            }
        }

        /// <summary>
        /// Show editor display for BrushEditor/MorphEditor
        /// </summary>
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
                    // Try to call Show method
                    var showMethod = display.GetType().GetMethod("Show");
                    if (showMethod != null)
                    {
                        showMethod.Invoke(display, null);
                        Debug.Log($"BrushUIManager: Showed editor display for {controllerType.Name}");
                    }
                }
            }
        }

        /// <summary>
        /// Enable or disable XR locomotion system
        /// </summary>
        private void SetLocomotionEnabled(bool enabled)
        {
            if (locomotionSystemObject != null)
            {
                locomotionSystemObject.SetActive(enabled);
                Debug.Log($"BrushUIManager: Locomotion system {(enabled ? "enabled" : "disabled")}");
            }

            if (locomotionComponents != null && locomotionComponents.Length > 0)
            {
                foreach (var component in locomotionComponents)
                {
                    if (component != null)
                    {
                        component.enabled = enabled;
                    }
                }
                Debug.Log($"BrushUIManager: {locomotionComponents.Length} locomotion components {(enabled ? "enabled" : "disabled")}");
            }
        }

        /// <summary>
        /// Delayed Morph UI setup coroutine (waits one frame for controller initialization)
        /// </summary>
        private System.Collections.IEnumerator DelayedMorphUISetup()
        {
            yield return null; // Wait one frame
            
            if (currentSystem == BrushSystem.ADBrush && morphItemsParent != null)
            {
                SetupMorphItemsForADBrush();
                Debug.Log("BrushUIManager: Delayed morph UI setup completed");
            }
        }
        
        /// <summary>
        /// Update UI element visibility based on current interface
        /// </summary>
        private void UpdateUIVisibility()
        {
            bool isBrushEditor = currentInterface != null && currentInterface.ControllerType == typeof(BrushEditorController);
            bool isMorphEditor = currentInterface != null && currentInterface.ControllerType == typeof(MorphEditorController);
            bool isADBrushDrawing = currentInterface != null && currentInterface.ControllerType == typeof(ADBrushController);
            bool showSaveButton = isBrushEditor || isMorphEditor;

            // Update save button visibility and text
            if (saveBrushButton != null)
            {
                saveBrushButton.gameObject.SetActive(showSaveButton);
                
                if (showSaveButton && saveBrushButtonText != null)
                {
                    saveBrushButtonText.text = isBrushEditor ? "Save Base Shape" : "Save Morphs";
                }
            }

            // Show morph items container for all ADBrush-related interfaces
            // (ADBrush Drawing, Base Brush Editor when using ADBrush, and Morph Editor)
            if (morphItemsParent != null)
            {
                bool showMorphUI = (currentSystem == BrushSystem.ADBrush) && 
                                   (isADBrushDrawing || isBrushEditor || isMorphEditor);
                morphItemsParent.gameObject.SetActive(showMorphUI);
                
                // Populate morph sliders when showing the UI
                if (showMorphUI)
                {
                    SetupMorphItemsForADBrush();
                }
            }

            // Update shape dropdown visibility and content
            if (brushShapeDropdown != null)
            {
                // Show for editors or brushes that support shapes
                bool showShapeDropdown = isBrushEditor || isMorphEditor || SupportsBrushShapes(activeBrush);
                brushShapeDropdown.gameObject.SetActive(showShapeDropdown);
                
                // Rebuild dropdown options when interface changes
                if (showShapeDropdown)
                {
                    UpdateBrushShapeDropdown();
                }
            }

            Debug.Log($"BrushUIManager: UI updated - System: {currentSystem}, Save button: {showSaveButton}, Morph UI visible: {morphItemsParent != null && morphItemsParent.gameObject.activeSelf}");
        }

        /// <summary>
        /// Update common slider values from active brush
        /// </summary>
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

                // also refresh width min/max labels when brush changes
                var range = activeBrush.BrushWidthRange;
                UpdateValueText(brushWidthMinText, range.x, "m", 3);
                UpdateValueText(brushWidthMaxText, range.y, "m", 3);
            }
        }

        /// <summary>
        /// Setup morph item sliders for ADBrush (参考原 MorphPanelUI 逻辑)
        /// </summary>
        private void SetupMorphItemsForADBrush()
        {
            if (morphItemPrefab == null || morphItemsParent == null)
            {
                Debug.LogWarning("BrushUIManager: Cannot setup morph items - prefab or parent is null");
                return;
            }

            // 检查是否为 MorphEditor 模式
            bool isMorphEditor = currentInterface != null && currentInterface.ControllerType == typeof(MorphEditorController);
            if (isMorphEditor)
            {
                SetupMorphItemsForMorphEditor();
                return;
            }

            // Get the ADBrush controller instance
            if (!controllerInstances.TryGetValue(typeof(ADBrushController), out var controller))
            {
                Debug.LogWarning("BrushUIManager: ADBrushController not found in controllerInstances");
                return;
            }

            var adBrush = controller as ADBrushController;
            if (adBrush == null)
            {
                Debug.LogError("BrushUIManager: Controller is not an ADBrushController");
                return;
            }

            // Ensure ADBrush is initialized (Awake has run)
            if (!adBrush.gameObject.activeInHierarchy)
            {
                Debug.LogWarning("BrushUIManager: ADBrushController GameObject is inactive, activating it to ensure initialization.");
                adBrush.gameObject.SetActive(true);
            }

            // Get morph data from ADBrushController
            var morphNames = adBrush.GetMorphNames();
            var morphWeights = adBrush.GetMorphWeights();

            Debug.Log($"BrushUIManager.SetupMorphItemsForADBrush: morphNames={(morphNames != null ? morphNames.Count.ToString() : "null")}, morphWeights={(morphWeights != null ? morphWeights.Count.ToString() : "null")}, controller.enabled={controller.enabled}");

            if (morphNames == null || morphWeights == null || morphNames.Count == 0)
            {
                Debug.LogWarning($"BrushUIManager: Invalid or empty morph data from ADBrushController - morphNames={(morphNames != null ? morphNames.Count.ToString() : "null")}, morphWeights={(morphWeights != null ? morphWeights.Count.ToString() : "null")}");
                // If the ADBrush controller is active but still initializing, try to retry a few times
                if (adBrush != null)
                {
                    StartCoroutine(RetrySetupMorphItemsForADBrush(adBrush, attempts: 3, delaySeconds: 0.1f));
                }
                return;
            }

            // Always rebuild to ensure correct state
            ClearMorphItems();

            // Create a morph slider for each morph target (参考 MorphPanelUI.AddItem)
            for (int i = 0; i < morphNames.Count; i++)
            {
                var go = Instantiate(morphItemPrefab, morphItemsParent);
                go.name = $"MorphItem_{morphNames[i]}";
                
                var morphItemUI = go.GetComponent<VRBrush.Interface.UI.MorphItemUI>();
                if (morphItemUI == null)
                {
                    morphItemUI = go.AddComponent<VRBrush.Interface.UI.MorphItemUI>();
                }

                // Initialize with callback to ADBrushController
                int morphIndex = i; // Capture index for closure
                float currentWeight = (i < morphWeights.Count) ? morphWeights[i] : 0f;
                
                morphItemUI.Initialize(
                    morphNames[i],      // displayName
                    morphIndex,         // morphIndex
                    0f,                 // min
                    1f,                 // max
                    currentWeight,      // defaultValue
                    (idx, newValue) => adBrush.OnExternalMorphValueChanged(idx, newValue), // onValueChanged
                    "Morph"             // typeLabel
                );

                morphItems.Add(morphItemUI);
            }

            Debug.Log($"BrushUIManager: Created {morphItems.Count} morph sliders for ADBrush");
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
                Debug.Log("BrushUIManager: No morphs found for current shape in MorphEditor");
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
            
            Debug.Log($"BrushUIManager: Setup {morphNames.Count} morph items for MorphEditor");
        }

        private System.Collections.IEnumerator RetrySetupMorphItemsForADBrush(ADBrushController adBrush, int attempts, float delaySeconds)
        {
            for (int i = 0; i < attempts; i++)
            {
                yield return new WaitForSeconds(delaySeconds);
                if (adBrush == null) yield break;
                var names = adBrush.GetMorphNames();
                if (names != null && names.Count > 0)
                {
                    Debug.Log("BrushUIManager: RetrySetupMorphItemsForADBrush succeeded");
                    SetupMorphItemsForADBrush();
                    yield break;
                }
            }
            Debug.LogWarning("BrushUIManager: RetrySetupMorphItemsForADBrush exhausted attempts without morph data");
        }

        /// <summary>
        /// Clear all morph item UI elements
        /// </summary>
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

        #endregion

        /// <summary>
        /// Find all brushes implementing IBrushController interface in the scene
        /// </summary>
        /// <summary>
        /// Debug helper to show hierarchy structure
        /// </summary>
        private void DebugHierarchy(Transform parent, int depth)
        {
            string indent = new string(' ', depth * 2);
            var components = parent.GetComponents<MonoBehaviour>();
            string componentInfo = components.Length > 0 ? $" [{string.Join(", ", components.Select(c => c.GetType().Name))}]" : "";
            Debug.Log($"{indent}- {parent.name}{componentInfo}");

            for (int i = 0; i < parent.childCount; i++)
            {
                DebugHierarchy(parent.GetChild(i), depth + 1);
            }
        }

        private void FindAllBrushes()
        {
            availableBrushes.Clear();
            var allBrushes = new List<IBrushController>();

            if (brushesParent != null)
            {
                // Search only under the specified Brushes parent
                Debug.Log($"BrushUIManager: Searching for brushes under parent '{brushesParent.name}'");

                // Debug: Show hierarchy structure
                DebugHierarchy(brushesParent, 0);

                var childBrushes = brushesParent.GetComponentsInChildren<MonoBehaviour>(true).OfType<IBrushController>();
                allBrushes.AddRange(childBrushes);
                Debug.Log($"Found {allBrushes.Count} brushes under '{brushesParent.name}': {string.Join(", ", allBrushes.Select(b => b.BrushName))}");

                // Debug: Show all MonoBehaviour components found
                var allMonos = brushesParent.GetComponentsInChildren<MonoBehaviour>(true);
                Debug.Log($"Total MonoBehaviour components under '{brushesParent.name}': {allMonos.Length}");
                foreach (var mono in allMonos)
                {
                    bool implementsIBrush = mono is IBrushController;
                    Debug.Log($"  - {mono.GetType().Name} on '{mono.gameObject.name}' - IBrushController: {implementsIBrush}");
                }
            }
            else
            {
                // Fallback: search entire scene (legacy behavior)
                Debug.LogWarning("BrushUIManager: No brushesParent assigned, searching entire scene for brushes.");
                // 首先尝试查找场景中激活的对象
                var foundActive = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
                allBrushes.AddRange(foundActive.OfType<IBrushController>());
                // 如果仍未找到，进一步包含未激活对象（避免被其他系统在Awake中禁用导致找不到）
                if (allBrushes.Count == 0)
                {
                    var foundAll = Resources.FindObjectsOfTypeAll<MonoBehaviour>();
                    allBrushes.AddRange(foundAll.OfType<IBrushController>());
                    Debug.Log($"BrushUIManager: Active search found none, fallback(include inactive) found {allBrushes.Count} brushes: {string.Join(", ", allBrushes.Select(b => b.BrushName))}");
                }
                else
                {
                    Debug.Log($"BrushUIManager: Found {allBrushes.Count} active brushes in entire scene: {string.Join(", ", allBrushes.Select(b => b.BrushName))}");
                }
            }

            // First collect all brushes, then register special controllers
            availableBrushes = allBrushes.ToList();

            // Ensure special controllers are in the list
            RegisterSpecialControllers();

            // Group brushes by name and sort to ensure each brush appears only once in UI
            // This must happen AFTER RegisterSpecialControllers to avoid duplicates
            availableBrushes = availableBrushes
                .GroupBy(b => b.BrushName)
                .Select(g => g.First())
                .OrderBy(b => b.BrushName)
                .ToList();

            // Initialize with proper state management - only activate first brush
            if (availableBrushes.Count > 0)
            {
                // First ensure ALL brushes are deactivated
                foreach (var brush in availableBrushes)
                {
                    var mono = brush as MonoBehaviour;
                    if (mono != null)
                    {
                        mono.enabled = false;  // Disable script, not GameObject
                    }
                }

                // Then activate only the first brush
                activeBrush = availableBrushes[0];
                var activeMono = activeBrush as MonoBehaviour;
                if (activeMono != null)
                {
                    activeMono.enabled = true;  // Enable script, not GameObject
                }

                Debug.Log($"Activated initial brush: {activeBrush.BrushName}");
            }

            Debug.Log($"Found {availableBrushes.Count} different brush types: {string.Join(", ", availableBrushes.Select(b => b.BrushName))}");
        }

        /// <summary>
        /// Load available brush shapes
        /// </summary>
        private void LoadAvailableShapes()
        {
            // Ensure directory exists to avoid noisy warnings
            var dir = VRBrush.Core.Model.BrushShapeLoader.DefaultDirectory;
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
                Debug.Log($"BrushUIManager: Created missing shapes directory: {dir}");
            }
            var shapes = VRBrush.Core.Model.BrushShapeLoader.LoadAllShapes();
            availableShapes.Clear();
            foreach (var shape in shapes)
            {
                availableShapes.Add(shape.name);
            }
        }



        /// <summary>
        /// Can be called externally to refresh (e.g., after dynamically adding new brushes).
        /// </summary>
        public void RefreshBrushList()
        {
            FindAllBrushes();

            // Force state refresh - ensure only active brush is enabled
            if (activeBrush != null)
            {
                ForceRefreshBrushState();
            }
        }

        /// <summary>
        /// Force refresh the brush state to ensure only active brush is enabled
        /// </summary>
        public void ForceRefreshBrushState()
        {
            if (activeBrush == null && availableBrushes.Count > 0)
            {
                activeBrush = availableBrushes[0];
            }

            foreach (var brush in availableBrushes)
            {
                var mono = brush as MonoBehaviour;
                if (mono != null)
                {
                    // 统一策略：保持 GameObject 处于激活状态，仅通过启用/禁用脚本来切换笔刷
                    // 这样可避免禁用整棵对象层级导致的3D UI/预览等子物体被隐藏的问题
                    if (!mono.gameObject.activeSelf)
                    {
                        mono.gameObject.SetActive(true); // 确保对象处于激活状态
                    }

                    bool shouldBeEnabled = (brush == activeBrush);
                    if (mono.enabled != shouldBeEnabled)
                    {
                        mono.enabled = shouldBeEnabled; // 仅切换脚本启用状态
                        Debug.Log($"Force setting {brush.BrushName} script enabled to: {shouldBeEnabled}");
                    }
                }
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Refresh Brush List")]
        private void CM_Refresh()
        {
            RefreshBrushList();
            Debug.Log("BrushUIManager: Brush list refreshed.");
        }
        
        [ContextMenu("Force Refresh Brush State")]
        private void CM_ForceRefresh()
        {
            ForceRefreshBrushState();
            Debug.Log("BrushUIManager: Brush state force refreshed.");
        }
#endif

        /// <summary>
        /// Initialize UI controls (sliders, buttons, shape dropdown). Two-level dropdowns are handled separately.
        /// </summary>
        private void InitializeUI()
        {
            // Initialize brush distance slider
            if (brushDistanceSlider != null)
            {
                // use configured values if non-zero, otherwise use slider's own range from UI
                float min = minBrushDistance != 0f ? minBrushDistance : brushDistanceSlider.minValue;
                float max = maxBrushDistance != 0f ? maxBrushDistance : brushDistanceSlider.maxValue;
                brushDistanceSlider.minValue = min;
                brushDistanceSlider.maxValue = max;

                float initial = activeBrush != null ? activeBrush.BrushDistance : 0f;
                brushDistanceSlider.value = initial;

                if (brushDistanceMinText != null)
                    UpdateValueText(brushDistanceMinText, min, "m", 2);
                if (brushDistanceMaxText != null)
                    UpdateValueText(brushDistanceMaxText, max, "m", 2);

                brushDistanceSlider.onValueChanged.AddListener(OnBrushDistanceChanged);
            }

            // Initialize brush angle slider
            if (brushAngleUpSlider != null)
            {
                float min = minBrushAngleUp != 0f ? minBrushAngleUp : brushAngleUpSlider.minValue;
                float max = maxBrushAngleUp != 0f ? maxBrushAngleUp : brushAngleUpSlider.maxValue;
                brushAngleUpSlider.minValue = min;
                brushAngleUpSlider.maxValue = max;

                float initial = activeBrush != null ? activeBrush.BrushAngleUp : 0f;
                brushAngleUpSlider.value = initial;

                if (brushAngleMinText != null)
                    UpdateValueText(brushAngleMinText, min, "°", 0);
                if (brushAngleMaxText != null)
                    UpdateValueText(brushAngleMaxText, max, "°", 0);

                brushAngleUpSlider.onValueChanged.AddListener(OnBrushAngleUpChanged);
            }

            // Initialize clear button
            if (clearButton != null)
            {
                clearButton.onClick.AddListener(OnClearButtonClicked);
            }

            // Initialize width slider
            if (brushWidthSlider != null)
            {
                RefreshWidthSliderRange();
                brushWidthSlider.onValueChanged.AddListener(OnBrushWidthChanged);

                float initial = activeBrush != null ? activeBrush.BrushWidth : brushWidthSlider.minValue;
                brushWidthSlider.SetValueWithoutNotify(initial);

                // update min/max labels from current range
                if (activeBrush != null)
                {
                    var range = activeBrush.BrushWidthRange;
                    if (brushWidthMinText != null)
                        UpdateValueText(brushWidthMinText, range.x, "m", 3);
                    if (brushWidthMaxText != null)
                        UpdateValueText(brushWidthMaxText, range.y, "m", 3);
                }
            }

            // Removed size slider

            // Initialize save brush button
            if (saveBrushButton != null)
            {
                saveBrushButton.onClick.AddListener(OnSaveBrushClicked);
            }

            // Initialize brush shape dropdown
            InitializeBrushShapeDropdown();
        }

        #region UI Event Handlers

        /// <summary>
        /// Called when brush distance slider value changes
        /// </summary>
        private void OnBrushDistanceChanged(float value)
        {
            if (activeBrush != null)
            {
                activeBrush.SetBrushDistance(value);
            }
        }

        /// <summary>
        /// Called when brush angle slider value changes
        /// </summary>
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

        // Removed size slider handler

        private void RefreshWidthSliderRange()
        {
            if (brushWidthSlider == null || activeBrush == null) return;
            // 使用 brush 的范围设置（统一管理）
            var range = activeBrush.BrushWidthRange;
            brushWidthSlider.minValue = range.x;
            brushWidthSlider.maxValue = range.y;
        }

        /// <summary>
        /// Called when clear button is clicked
        /// </summary>
        private void OnClearButtonClicked()
        {
            // Notify all brushes to clear strokes
            foreach (var brush in availableBrushes)
            {
                brush.ClearAllStrokes();
            }
            Debug.Log($"Cleared all strokes.");
        }

        #endregion

        #region Edit Mode Related Methods

        /// <summary>
        /// Set edit mode
        /// </summary>
        private void SetEditMode(bool enabled)
        {
            isInEditMode = enabled;
            Debug.Log($"BrushUIManager: Setting edit mode to {enabled}");

            if (brushEditor != null)
            {
                Debug.Log($"BrushUIManager: Calling SetEditMode({enabled}) on brushEditor {brushEditor.gameObject.name}");
                brushEditor.SetEditMode(enabled);
            }
            else
            {
                Debug.LogWarning("BrushUIManager: brushEditor is null!");
            }

            // Update UI control visibility
            UpdateUIForEditMode();
        }

        /// <summary>
        /// Update UI for edit mode
        /// </summary>
        private void UpdateUIForEditMode()
        {
            // In edit mode, some controls may need different behavior
            if (saveBrushButton != null)
            {
                // 在编辑器(BrushEditor)模式或MorphEditor激活时显示保存按钮
                bool showSave = isInEditMode || (activeBrush is MorphEditorController);
                saveBrushButton.gameObject.SetActive(showSave);
            }
        }

        /// <summary>
        /// Initialize brush shape dropdown
        /// </summary>
        private void InitializeBrushShapeDropdown()
        {
            if (brushShapeDropdown == null) return;
            brushShapeDropdown.onValueChanged.RemoveListener(OnBrushShapeChanged);
            UpdateBrushShapeDropdown();
            brushShapeDropdown.onValueChanged.AddListener(OnBrushShapeChanged);
        }

        /// <summary>
        /// Update brush shape dropdown
        /// </summary>
        private void UpdateBrushShapeDropdown()
        {
            if (brushShapeDropdown == null)
            {
                Debug.LogWarning("BrushUIManager: brushShapeDropdown is null, cannot update");
                return;
            }

            // 检查当前笔刷是否支持形状管理
            bool brushSupportsShapes = SupportsBrushShapes(activeBrush);

            // 根据笔刷类型加载对应的形状列表
            if (brushSupportsShapes && !isInEditMode)
            {
                // 从当前笔刷获取可用形状列表
                LoadShapesFromActiveBrush();
            }
            else
            {
                // 编辑模式或没有激活笔刷时，使用默认形状目录
                LoadAvailableShapes();
            }

            // 先清空所有选项
            brushShapeDropdown.ClearOptions();
            
            // 构建选项列表
            List<string> options = new List<string>();

            if (isInEditMode)
            {
                // Edit mode: add "New Brush" option
                options.Add("New Brush");
                options.AddRange(availableShapes);
                Debug.Log($"BrushUIManager: Shape dropdown in edit mode - {options.Count} options: {string.Join(", ", options)}");
            }
            else
            {
                // Normal mode: only show existing shapes
                options.AddRange(availableShapes);
                Debug.Log($"BrushUIManager: Shape dropdown in normal mode - {options.Count} options: {string.Join(", ", options)}");
            }

            // 添加选项到下拉菜单 (必须在设置值之前)
            if (options.Count > 0)
            {
                brushShapeDropdown.AddOptions(options);
                
                // 设置选中值
                if (isInEditMode)
                {
                    brushShapeDropdown.SetValueWithoutNotify(0); // Default select "New Brush"
                }
                else if (brushSupportsShapes && activeBrush != null)
                {
                    // 如果笔刷支持形状，尝试选中当前激活的形状
                    string activeShapeName = GetActiveShapeFromBrush(activeBrush);
                    if (!string.IsNullOrEmpty(activeShapeName))
                    {
                        // 在正常模式下，index直接对应availableShapes的索引
                        int shapeIndex = availableShapes.IndexOf(activeShapeName);
                        if (shapeIndex >= 0)
                        {
                            brushShapeDropdown.SetValueWithoutNotify(shapeIndex);
                            Debug.Log($"BrushUIManager: Set dropdown to active shape '{activeShapeName}' at index {shapeIndex}");
                        }
                    }
                }
            }
            else
            {
                Debug.LogWarning($"BrushUIManager: No shapes available for dropdown (isInEditMode: {isInEditMode}, brushSupportsShapes: {brushSupportsShapes})");
            }

            // 控制下拉菜单的可见性（与旧版一致：支持形状或处于编辑模式时显示；不再依赖是否有选项）
            bool shouldBeActive = (isInEditMode || brushSupportsShapes);  
            brushShapeDropdown.gameObject.SetActive(shouldBeActive);
            Debug.Log($"BrushUIManager: Shape dropdown active: {shouldBeActive} (isInEditMode: {isInEditMode}, brushSupportsShapes: {brushSupportsShapes}, optionCount: {options.Count}, activeBrush: {activeBrush?.BrushName})");
        }

        /// <summary>
        /// 当当前笔刷支持形状但尚未有激活形状时，自动选择可用列表中的一个形状进行激活；同时同步下拉选中项。
        /// - 编辑模式下不做任何事（BrushEditor 仅用于保存，不负责读取/激活）。
        /// - 对 HelioBrush、MorphEditor 等需要读取形状的控制器生效。
        /// </summary>
        private void AutoActivateShapeForActiveBrush()
        {
            if (isInEditMode) return; // 编辑模式由用户选择是否新建/覆盖
            if (activeBrush == null) return;
            if (!SupportsBrushShapes(activeBrush)) return;

            // 从当前笔刷获取可用形状
            LoadShapesFromActiveBrush();

            // 获取当前激活形状名称
            string activeShapeName = GetActiveShapeFromBrush(activeBrush);

            // 若笔刷尚未有激活形状，或当前激活形状不在列表中，则选择第一个
            if (string.IsNullOrEmpty(activeShapeName) || (availableShapes.Count > 0 && !availableShapes.Contains(activeShapeName)))
            {
                if (availableShapes.Count == 0) return; // 无可用形状可激活
                activeShapeName = availableShapes[0];

                // 反射调用 ActivateShape(shapeName)
                var method = activeBrush.GetType().GetMethod("ActivateShape");
                if (method != null)
                {
                    method.Invoke(activeBrush, new object[] { activeShapeName });
                    Debug.Log($"BrushUIManager: Auto-activated default shape '{activeShapeName}' for brush '{activeBrush.BrushName}'");
                }
            }

            // 将下拉列表的选中项同步到激活形状
            if (brushShapeDropdown != null && brushShapeDropdown.gameObject.activeInHierarchy)
            {
                int idx = availableShapes.IndexOf(activeShapeName);
                if (idx >= 0)
                {
                    brushShapeDropdown.SetValueWithoutNotify(idx);
                }
            }
        }

        /// <summary>
        /// 检查笔刷是否支持形状管理
        /// </summary>
        private bool SupportsBrushShapes(IBrushController brush)
        {
            if (brush == null) return false;
            
            // 检查是否有 GetAvailableShapeNames 方法
            var method = brush.GetType().GetMethod("GetAvailableShapeNames");
            return method != null;
        }

        /// <summary>
        /// 从当前激活的笔刷加载形状列表
        /// </summary>
        private void LoadShapesFromActiveBrush()
        {
            if (activeBrush == null) return;
            
            var method = activeBrush.GetType().GetMethod("GetAvailableShapeNames");
            if (method != null)
            {
                var shapes = method.Invoke(activeBrush, null) as List<string>;
                if (shapes != null)
                {
                    availableShapes.Clear();
                    availableShapes.AddRange(shapes);
                    Debug.Log($"BrushUIManager: Loaded {availableShapes.Count} shapes from {activeBrush.BrushName}");
                }
            }
        }

        /// <summary>
        /// 从笔刷获取当前激活的形状名称
        /// </summary>
        private string GetActiveShapeFromBrush(IBrushController brush)
        {
            if (brush == null) return null;
            
            var method = brush.GetType().GetMethod("GetActiveShapeName");
            if (method != null)
            {
                return method.Invoke(brush, null) as string;
            }
            return null;
        }

        private void OnBrushShapeChanged(int index)
        {
            // 正常绘制模式：从 index 0 开始是 shapes
            if (index < 0 || index >= availableShapes.Count) return;
            string shapeName = availableShapes[index];
            
            if (currentInterface != null)
            {
                var controllerType = currentInterface.ControllerType;
                
                // BrushEditorController 创建新形状，不需要从 Dropdown 加载
                if (controllerType == typeof(BrushEditorController))
                {
                    Debug.Log($"BrushUIManager: Shape dropdown changed in BrushEditor mode (index {index}), no activation needed");
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
                            Debug.Log($"BrushUIManager: Activated shape '{shapeName}' for MorphEditor");
                            // 切换形状后更新 Morph UI
                            SetupMorphItemsForMorphEditor();
                        }
                    }
                    return;
                }
            }

            if (activeBrush == null) return;
            
            // 检查当前笔刷是否支持形状切换
            if (activeBrush is HelioBrushController helio)
            {
                helio.ActivateShape(shapeName);
                Debug.Log($"BrushUIManager: Activated HelioBrush shape via dropdown: {shapeName}");
                // 切换形状后更新 Morph UI (绘制模式)
                SetupMorphItemsForADBrush();
            }
            else if (activeBrush.GetType().GetMethod("ActivateShape") != null)
            {
                // 使用反射调用任何支持 ActivateShape 方法的笔刷
                activeBrush.GetType().GetMethod("ActivateShape").Invoke(activeBrush, new object[] { shapeName });
                Debug.Log($"BrushUIManager: Activated {activeBrush.BrushName} shape via dropdown: {shapeName}");
                // 切换形状后更新 Morph UI (绘制模式)
                SetupMorphItemsForADBrush();
            }
            else
            {
                Debug.LogWarning($"BrushUIManager: Active brush '{activeBrush.BrushName}' does not support shape switching");
            }
        }

        /// <summary>
        /// Save brush button click event
        /// </summary>
        private void OnSaveBrushClicked()
        {
            // Check if current interface is Base Brush Editor
            if (currentInterface != null && currentInterface.ControllerType == typeof(BrushEditorController))
            {
                if (controllerInstances.TryGetValue(typeof(BrushEditorController), out var editorMono))
                {
                    var editor = editorMono as BrushEditorController;
                    if (editor != null)
                    {
                        string fileName = null;

                        // Check dropdown selection
                        if (brushShapeDropdown != null && brushShapeDropdown.value > 0)
                        {
                            // Selected existing brush, overwrite save
                            string selectedShape = availableShapes[brushShapeDropdown.value - 1];
                            fileName = System.IO.Path.Combine(VRBrush.Core.Model.BrushShapeLoader.DefaultDirectory, selectedShape + ".json");
                        }

                        // Save brush
                        editor.SaveCurrentBrush(fileName);

                        // Reload shape list
                        LoadAvailableShapes();
                        UpdateBrushShapeDropdown();
                        
                        Debug.Log("BrushUIManager: Saved base shape via BrushEditorController");
                        return;
                    }
                }
            }

            // Check if current interface is Morph Editor
            if (currentInterface != null && currentInterface.ControllerType == typeof(MorphEditorController))
            {
                if (controllerInstances.TryGetValue(typeof(MorphEditorController), out var morphMono))
                {
                    var morphEditor = morphMono as MorphEditorController;
                    if (morphEditor != null)
                    {
                        morphEditor.SaveCurrentMorph();
                        Debug.Log("BrushUIManager: Saved morph via MorphEditorController");
                        return;
                    }
                }
            }

            Debug.LogWarning("BrushUIManager: Save clicked, but no editor is active. No action taken.");
        }

        #endregion

        #region Debug and Test Methods

        [ContextMenu("Show All Available Brushes")]
        public void DebugShowAllBrushes()
        {
            Debug.Log($"=== Current {availableBrushes.Count} available brush types ===");
            for (int i = 0; i < availableBrushes.Count; i++)
            {
                var brush = availableBrushes[i];
                string status = brush == activeBrush ? " [Currently Active]" : "";
                Debug.Log($"{i}: {brush.BrushName}{status}");
            }
        }

        [ContextMenu("Test Edit Mode Toggle")]
        public void DebugTestEditMode()
        {
            if (brushEditor != null)
            {
                SetEditMode(!isInEditMode);
                Debug.Log($"Edit mode: {(isInEditMode ? "Enabled" : "Disabled")}");
            }
            else
            {
                Debug.LogWarning("Editor controller not found");
            }
        }

        #endregion

        /// <summary>
        /// Called by VRBrushSystem to set the currently active brush controller
        /// </summary>
        public void SetActiveBrush(IBrushController brush)
        {
            activeBrush = brush;

            // Update slider values to reflect new brush state
            if (brushDistanceSlider != null && activeBrush != null)
            {
                brushDistanceSlider.value = activeBrush.BrushDistance;
            }
            if (brushAngleUpSlider != null && activeBrush != null)
            {
                brushAngleUpSlider.value = activeBrush.BrushAngleUp;
            }
            if (brushWidthSlider != null && activeBrush != null)
            {
                brushWidthSlider.SetValueWithoutNotify(activeBrush.BrushWidth);
            }
            // Size slider removed
        }

        /// <summary>
        /// Get the currently active brush controller
        /// </summary>
        public IBrushController GetActiveBrush()
        {
            return activeBrush;
        }

        /// <summary>
        /// Get all available brush controllers
        /// </summary>
        public List<IBrushController> GetAllBrushControllers()
        {
            return new List<IBrushController>(availableBrushes);
        }

        /// <summary>
        /// Set the brushes parent reference (called by VRBrushSystem)
        /// </summary>
        public void SetBrushesParent(Transform parent)
        {
            brushesParent = parent;
            Debug.Log($"BrushUIManager: Set brushesParent reference to '{(parent != null ? parent.name : "null")}'");

            // Refresh brush list with new parent reference
            if (parent != null)
            {
                RefreshBrushList();
            }
        }

        /// <summary>
        /// Set available shapes for the brush shape dropdown
        /// </summary>
        public void SetAvailableShapes(List<string> shapeNames)
        {
            availableShapes = shapeNames;
            UpdateBrushShapeDropdown();
        }

        private void UpdateValueText(TMP_Text target, float value, string unit, int decimals)
        {
            if (target == null) return;
            string fmt = decimals > 0 ? value.ToString("F" + decimals) : Mathf.RoundToInt(value).ToString();
            target.text = fmt + unit;
        }
    }
}
