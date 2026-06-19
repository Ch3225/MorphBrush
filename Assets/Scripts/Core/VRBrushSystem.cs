using UnityEngine;
using VRBrush.Interface;
using System.Collections.Generic;
using System.Linq;

namespace VRBrush.Core
{
    /// <summary>
    /// VRBrush system central manager.
    /// Works with BrushUIManager (or Ex1/Ex2 variants) to manage brush selection and input configuration.
    /// </summary>
    public class VRBrushSystem : MonoBehaviour
    {
        [Header("Controller Configuration")]
        [SerializeField] private GameObject leftControllerObject;
        [SerializeField] private GameObject rightControllerObject;

        [Header("Stroke Management")]
        [SerializeField] private Transform strokesParent;
        [SerializeField] private string strokesParentName = "BrushStrokes";

        [Header("Brush Management")]
        [Tooltip("可绑定 BrushUIManager、BrushUIManager4Ex1 或 BrushUIManager4Ex2")]
        [SerializeField] private MonoBehaviour brushUIManagerObject; // 使用 MonoBehaviour 以支持多种 UI Manager
        [SerializeField] private Transform brushesParent; // Parent object for all brushes

        // Cached reference to the actual UI manager (for GetActiveBrush)
        private BrushUIManager brushUIManager;
        private BrushUIManager4Ex1 brushUIManager4Ex1;
        private BrushUIManager4Ex2 brushUIManager4Ex2;

        // References to maintain proper state management
        private List<IBrushController> allBrushControllers = new List<IBrushController>();

        void Awake()
        {
            InitializeSystem();
        }

        /// <summary>
        /// Initialize the entire VRBrush system
        /// </summary>
        private void InitializeSystem()
        {
            if (rightControllerObject == null)
            {
                Debug.LogError("VRBrushSystem: Right Controller Object not assigned!", this);
                return;
            }

            CreateStrokesParent();
            SetupBrushSystem();

            // Ensure left controller is configured if available
            if (leftControllerObject != null)
            {
                Debug.Log($"VRBrushSystem: Configuring left controller '{leftControllerObject.name}' during initialization");
                SetLeftController(leftControllerObject);
            }
            else
            {
                Debug.LogWarning("VRBrushSystem: Left Controller Object not assigned - two-handed brushes like BrushEditor may not work properly!");
            }
        }

        /// <summary>
        /// Setup brush system with proper state management
        /// </summary>
        private void SetupBrushSystem()
        {
            // Find all brush controllers - use brushesParent if available
            List<IBrushController> allBrushes;
            if (brushesParent != null)
            {
                // Search only under the specified Brushes parent
                Debug.Log($"VRBrushSystem: Searching for brushes under parent '{brushesParent.name}'");
                var childBrushes = brushesParent.GetComponentsInChildren<MonoBehaviour>(true).OfType<IBrushController>().ToList();
                allBrushes = childBrushes;
                Debug.Log($"VRBrushSystem: Found {allBrushes.Count} brushes under '{brushesParent.name}': {string.Join(", ", allBrushes.Select(b => b.BrushName))}");
            }
            else
            {
                // Fallback: search entire scene
                Debug.LogWarning("VRBrushSystem: No brushesParent assigned, searching entire scene for brushes.");
                allBrushes = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                                    .OfType<IBrushController>()
                                    .ToList();
            }

            if (allBrushes.Count == 0)
            {
                Debug.LogWarning("VRBrushSystem: No brush controllers found in scene.", this);
                return;
            }

            allBrushControllers = allBrushes;

            // Configure all brushes with input sources and stroke parent
            foreach (var brush in allBrushControllers)
            {
                brush.SetInputSource(rightControllerObject);
                brush.SetStrokesParent(strokesParent);

                // For two-handed brushes, also set left controller if available
                if (leftControllerObject != null && brush is MonoBehaviour mono && mono.GetType().GetMethod("SetLeftInputSource") != null)
                {
                    Debug.Log($"VRBrushSystem: Setting left controller '{leftControllerObject.name}' for brush '{brush.BrushName}'");
                    mono.GetType().GetMethod("SetLeftInputSource").Invoke(mono, new object[] { leftControllerObject });
                }
                else if (brush is MonoBehaviour mono2 && mono2.GetType().GetMethod("SetLeftInputSource") != null)
                {
                    Debug.LogWarning($"VRBrushSystem: Brush '{brush.BrushName}' supports two-handed operation but leftControllerObject is null!");
                }

                // Initially disable all brushes - BrushUIManager will manage activation
                // NOTE: Disable the component, not the GameObject, so child UI elements stay active
                var monoBehavior = brush as MonoBehaviour;
                if (monoBehavior != null)
                {
                    monoBehavior.enabled = false;  // Only disable the script, not the GameObject
                    Debug.Log($"VRBrushSystem: Disabled brush component '{brush.BrushName}' (keeping child GameObjects active)");
                }
            }

            // Find or reference BrushUIManager (支持多种类型)
            DetectUIManager();

            if (brushUIManager != null)
            {
                // Pass brushesParent reference to BrushUIManager if available
                if (brushesParent != null)
                {
                    brushUIManager.SetBrushesParent(brushesParent);
                }

                // Let BrushUIManager handle brush activation and selection
                Debug.Log($"VRBrushSystem: Setup complete. Found {allBrushControllers.Count} brushes. BrushUIManager will handle selection.", this);
            }
            else if (brushUIManager4Ex1 != null)
            {
                Debug.Log($"VRBrushSystem: Setup complete. Found {allBrushControllers.Count} brushes. BrushUIManager4Ex1 will handle selection.", this);
            }
            else if (brushUIManager4Ex2 != null)
            {
                Debug.Log($"VRBrushSystem: Setup complete. Found {allBrushControllers.Count} brushes. BrushUIManager4Ex2 will handle selection.", this);
            }
            else
            {
                Debug.LogWarning("VRBrushSystem: No BrushUIManager found. Brush selection may not work properly.", this);
            }
        }

        /// <summary>
        /// Detect and cache the UI Manager reference (supports multiple types)
        /// </summary>
        private void DetectUIManager()
        {
            // 首先尝试使用 Inspector 中绑定的对象
            if (brushUIManagerObject != null)
            {
                if (brushUIManagerObject is BrushUIManager bum)
                {
                    brushUIManager = bum;
                    Debug.Log("VRBrushSystem: Using assigned BrushUIManager");
                    return;
                }
                else if (brushUIManagerObject is BrushUIManager4Ex1 ex1)
                {
                    brushUIManager4Ex1 = ex1;
                    Debug.Log("VRBrushSystem: Using assigned BrushUIManager4Ex1");
                    return;
                }
                else if (brushUIManagerObject is BrushUIManager4Ex2 ex2)
                {
                    brushUIManager4Ex2 = ex2;
                    Debug.Log("VRBrushSystem: Using assigned BrushUIManager4Ex2");
                    return;
                }
            }

            // 否则自动查找
            brushUIManager = FindFirstObjectByType<BrushUIManager>();
            if (brushUIManager != null)
            {
                Debug.Log("VRBrushSystem: Auto-detected BrushUIManager");
                return;
            }

            brushUIManager4Ex1 = FindFirstObjectByType<BrushUIManager4Ex1>();
            if (brushUIManager4Ex1 != null)
            {
                Debug.Log("VRBrushSystem: Auto-detected BrushUIManager4Ex1");
                return;
            }

            brushUIManager4Ex2 = FindFirstObjectByType<BrushUIManager4Ex2>();
            if (brushUIManager4Ex2 != null)
            {
                Debug.Log("VRBrushSystem: Auto-detected BrushUIManager4Ex2");
                return;
            }
        }

        /// <summary>
        /// Create stroke parent object
        /// </summary>
        private void CreateStrokesParent()
        {
            if (strokesParent == null)
            {
                GameObject strokesObj = GameObject.Find(strokesParentName);
                if (strokesObj == null)
                {
                    strokesObj = new GameObject(strokesParentName);
                    strokesObj.transform.SetParent(transform);
                    strokesObj.transform.localPosition = Vector3.zero;
                }
                strokesParent = strokesObj.transform;
                Debug.Log($"VRBrushSystem: Found or created stroke parent object '{strokesParentName}'.", this);
            }
        }

        /// <summary>
        /// Get reference to the currently active brush
        /// </summary>
        public IBrushController GetActiveBrush()
        {
            if (brushUIManager != null)
            {
                return brushUIManager.GetActiveBrush();
            }
            if (brushUIManager4Ex1 != null)
            {
                return brushUIManager4Ex1.GetActiveBrush();
            }
            if (brushUIManager4Ex2 != null)
            {
                return brushUIManager4Ex2.GetActiveBrush();
            }
            return null;
        }

        /// <summary>
        /// Get all available brush controllers
        /// </summary>
        public List<IBrushController> GetAllBrushControllers()
        {
            return new List<IBrushController>(allBrushControllers);
        }

        /// <summary>
        /// Add left controller support for two-handed operations
        /// </summary>
        public void SetLeftController(GameObject leftController)
        {
            leftControllerObject = leftController;
            Debug.Log($"VRBrushSystem: Setting left controller to '{leftController?.name ?? "NULL"}'");

            // Configure brushes that support two-handed operation
            foreach (var brush in allBrushControllers)
            {
                // Some brushes might have SetLeftInputSource method for two-handed operations
                if (brush is MonoBehaviour mono && mono.GetType().GetMethod("SetLeftInputSource") != null)
                {
                    if (leftController != null)
                    {
                        Debug.Log($"VRBrushSystem: Configuring left controller for brush '{brush.BrushName}'");
                        mono.GetType().GetMethod("SetLeftInputSource").Invoke(mono, new object[] { leftController });
                    }
                    else
                    {
                        Debug.LogWarning($"VRBrushSystem: Attempting to set null left controller for brush '{brush.BrushName}'");
                    }
                }
            }
        }

        /// <summary>
        /// Refresh all brush configurations (useful after scene changes)
        /// </summary>
        public void RefreshSystem()
        {
            SetupBrushSystem();
            if (brushUIManager != null)
            {
                brushUIManager.RefreshBrushList();
            }
            // Ex1 和 Ex2 没有 RefreshBrushList 方法，跳过
        }

    }
}
