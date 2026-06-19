using UnityEngine;
using VRBrush.Core;
using VRBrush.Core.Model;
using VRBrush.Interface;
using System.Collections.Generic;
using TMPro;

namespace VRBrush.Interface.Brush.HelioBrush
{
    /// <summary>
    /// HelioBrush控制器
    /// 协调位置计算器和渲染器，实现完整的HelioBrush功能
    /// </summary>
    public class HelioBrushController : BaseBrushController
    {
        [Header("========== ROTATION DEBUG CONTROLS ==========")]
        [Tooltip("Disk rotation mode: 0=XYZ, 1=XZY, 2=YXZ, 3=YZX, 4=ZXY, 5=ZYX")]
        [Range(0, 5)] public int diskRotationMode = 2;

        [Tooltip("Capsule rotation mode: 0=XYZ, 1=XZY, 2=YXZ, 3=YZX, 4=ZXY, 5=ZYX")]
        [Range(0, 5)] public int capsuleRotationMode = 0;

        [Tooltip("Enable manual disk rotation testing")]
        public bool enableDiskRotationDebug = true;

        [Tooltip("Enable manual capsule rotation testing")]
        public bool enableCapsuleRotationDebug = false;

        [Tooltip("Show rotation calculation details in console")]
        public bool showRotationDebugInfo = false;

        [System.Serializable]
        public struct HelioParams
        {
            public float rollSensitivity;
            public float minSegmentLengthClamp;
            public float twistAngleThresholdRad;
            public float maxTwistPerMeter;
            public float smallLengthScale;
            [Range(0f,1f)] public float rateSmoothing;
            public float rollBufferThreshold;
            [Range(0f,2f)] public float bufferCatchupFactor;
            public int warmupSmoothFrames;

            public static HelioParams Default => new HelioParams
            {
                rollSensitivity = 1.0f,
                minSegmentLengthClamp = 0.005f,
                twistAngleThresholdRad = 1f * Mathf.Deg2Rad,
                maxTwistPerMeter = 10f,
                smallLengthScale = 0.05f,
                rateSmoothing = 0.2f,
                rollBufferThreshold = 0.15f,
                bufferCatchupFactor = 0.5f,
                warmupSmoothFrames = 10
            };
        }

        [Header("========== HELIO BRUSH PARAMETERS ==========")]
        [SerializeField] private HelioParams helioBrushParams = HelioParams.Default;

        [Header("Extended Shape Settings")]
    [SerializeField] private bool enableExtendedShapes = true;
    // orientation disk removed
    [Tooltip("Shape JSON directory")][SerializeField] private string shapeDirectory = VRBrush.Core.Model.BrushShapeLoader.DefaultDirectory;
        [Tooltip("Currently selected shape name")][SerializeField] private string selectedShapeName;
        [Tooltip("Auto load shape list on start")][SerializeField] private bool autoLoadOnStart = true;
        [Tooltip("Whether to cap ends")][SerializeField] private bool capEnds = false;

        [Header("Trigger Width Scale")]
        [Tooltip("当按下扳机时的最小缩放倍率")]
        [SerializeField, Range(0.01f, 1f)] private float triggerMinScale = 0.2f;
        
        [Header("Trigger Tuning")]
        [Tooltip("HelioBrush临时降低扳机阈值")]
        [SerializeField, Range(0.01f, 0.5f)] private float helioTriggerThreshold = 0.05f;

        public override string BrushName => "HelioBrush";

    // 状态
    private HelioBrushAlgorithm.HelioBrushState helioState = new HelioBrushAlgorithm.HelioBrushState();
        
        // 形状管理
        private List<BrushShape> loadedShapes = new List<BrushShape>();
        private BrushShape activeShape;
        private float cachedBaseRibbonWidth = -1f;
        
        // UI
        private BrushUIManager uiManager;

        protected override void Start()
        {
            base.Start();

            // 加载形状（如启用扩展形状）
            if (enableExtendedShapes && autoLoadOnStart)
            {
                LoadShapes();
                ActivateShape(selectedShapeName);
                // PopulateDropdown 已移除 - 由 BrushUIManager 统一管理
            }

            // 设置预览颜色与可视样式
            if (universalBrushPreview != null)
            {
                // 采用 Helio 风格可视化（仿 BrushEditor）
                universalBrushPreview.SetUseHelioVisuals(true);
                universalBrushPreview.SetShowShapePoints(true);
                universalBrushPreview.SetPreviewColor(new Color(0.1f, 0.9f, 0.9f, 0.7f));

                if (enableExtendedShapes && activeShape != null)
                {
                    universalBrushPreview.CurrentShape = activeShape;
                }
            }

            // 找到UI管理器
            uiManager = FindFirstObjectByType<BrushUIManager>();

            // 临时降低扳机阈值
            triggerThreshold = Mathf.Clamp01(helioTriggerThreshold);

            // 调试信息
            if (showDebugInfo)
            {
                Debug.Log($"HelioBrush Controller Initialized - Extended Shapes: {enableExtendedShapes}");
            }
        }

        protected override void OnStrokeStart()
        {
            // 重置状态
            HelioBrushAlgorithm.ResetState(ref helioState);

            // 缓存原始宽度并应用扳机缩放
            if (cachedBaseRibbonWidth < 0)
            {
                cachedBaseRibbonWidth = ribbonWidth;
            }
        }

        protected override void OnStrokeEnd()
        {
            // 恢复原始宽度
            if (cachedBaseRibbonWidth > 0)
            {
                ribbonWidth = cachedBaseRibbonWidth;
                cachedBaseRibbonWidth = -1f;
            }
        }

        protected override Vector3 ComputeRulingDirection(Vector3 currentTangent, Vector3 currentPosition)
        {
            return HelioBrushAlgorithm.ComputeRulingDirection(
                currentPosition,
                GetControllerRotation(),
                ref helioState,
                false,
                helioBrushParams.rollSensitivity,
                helioBrushParams.minSegmentLengthClamp,
                helioBrushParams.twistAngleThresholdRad,
                helioBrushParams.maxTwistPerMeter,
                helioBrushParams.smallLengthScale,
                helioBrushParams.rateSmoothing,
                helioBrushParams.rollBufferThreshold,
                helioBrushParams.bufferCatchupFactor,
                helioBrushParams.warmupSmoothFrames
            );
        }

        protected override void AddPointToStroke(Vector3 position)
        {
            // 应用扳机压力缩放：通过覆盖 GetCurrentPointWidth() 已生效，这里仅保持逻辑存在
            float _ = GetCurrentTriggerValue();
            base.AddPointToStroke(position);
        }

        protected override float GetCurrentPointWidth()
        {
            float currentTriggerValue = GetCurrentTriggerValue();
            return Mathf.Lerp(ribbonWidth * triggerMinScale, ribbonWidth, currentTriggerValue);
        }

        public override void ClearAllStrokes()
        {
            base.ClearAllStrokes();
        }

        #region Shape Management

        private void LoadShapes()
        {
            // 确保目录存在
            if (!System.IO.Directory.Exists(shapeDirectory))
            {
                System.IO.Directory.CreateDirectory(shapeDirectory);
                Debug.Log($"HelioBrush: Created missing shapes directory: {shapeDirectory}");
            }

            loadedShapes = VRBrush.Core.Model.BrushShapeLoader.LoadAllShapes(shapeDirectory);
            Debug.Log($"HelioBrush: Loaded {loadedShapes.Count} shapes from {shapeDirectory}");
        }

        public void ActivateShape(string shapeName)
        {
            // 确保形状列表已加载
            if (loadedShapes == null || loadedShapes.Count == 0)
            {
                LoadShapes();
            }
            // 如果仍然没有形状，清空并返回
            if (loadedShapes == null || loadedShapes.Count == 0)
            {
                activeShape = null;
                selectedShapeName = null;
                // 同步预览为空形状
                if (universalBrushPreview != null)
                {
                    universalBrushPreview.CurrentShape = null;
                }
                return;
            }

            // 未指定形状名时，默认选择第一个形状
            if (string.IsNullOrEmpty(shapeName))
            {
                activeShape = loadedShapes[0];
                selectedShapeName = activeShape.name;
            }
            else
            {
                activeShape = loadedShapes.Find(s => s.name == shapeName);
                if (activeShape == null)
                {
                    activeShape = loadedShapes[0];
                    selectedShapeName = activeShape.name;
                }
            }

            // 更新预览的当前形状（若预览系统已存在）
            if (universalBrushPreview != null)
            {
                universalBrushPreview.SetUseHelioVisuals(true);
                universalBrushPreview.SetShowShapePoints(true);
                universalBrushPreview.CurrentShape = activeShape;
            }

            Debug.Log($"HelioBrush: Activated shape '{activeShape?.name ?? "None"}'");
        }

        /// <summary>
        /// 获取当前已加载的所有形状名称列表（供BrushUIManager使用）
        /// </summary>
        public List<string> GetAvailableShapeNames()
        {
            // 确保形状列表是最新的
            LoadShapes();
            var names = new List<string>();
            foreach (var shape in loadedShapes)
            {
                names.Add(shape.name);
            }
            return names;
        }

        /// <summary>
        /// 获取当前激活的形状名称（供BrushUIManager使用）
        /// </summary>
        public string GetActiveShapeName()
        {
            return activeShape?.name ?? selectedShapeName;
        }

        #endregion

        #region Public Methods

        // Orientation disk control methods removed

        /// <summary>
        /// 设置HelioBrush参数
        /// </summary>
        public void SetHelioBrushParameters(HelioParams parameters)
        {
            helioBrushParams = parameters;
        }

        #endregion

        protected override Stroke CreateStroke()
        {
            if (enableExtendedShapes && activeShape != null)
            {
                return new Stroke(activeShape, capEnds);
            }
            return base.CreateStroke();
        }

        private float GetCurrentTriggerValue()
        {
            // 优先从简单输入管理器读取，失败则回退到基类读数
            if (simpleInputManager != null)
            {
                return Mathf.Clamp01(simpleInputManager.GetTriggerValue());
            }
            return Mathf.Clamp01(GetTriggerValue());
        }
    }
}