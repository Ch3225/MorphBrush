using UnityEngine;
using VRBrush.Core;
using VRBrush.Core.Model;

namespace VRBrush.Interface.Brush.AdaptiBrush
{
    /// <summary>
    /// AdaptiBrush控制器（简化版）
    /// 直接使用 AdaptiBrushAlgorithm 计算 ruling，依赖基类进行网格生成。
    /// </summary>
    public class AdaptiBrushController : BaseBrushController
    {
        public override string BrushName => "AdaptiBrush";

        // 内部状态（替代 PositionCalculator）
        private Vector3 prevPos = Vector3.zero;
        private Vector3 prevPos2 = Vector3.zero;
        private Vector3 prevRuling = Vector3.zero;
        private Vector3 prevTangent = Vector3.zero;
        private bool initialized = false;

        // 圆盘预览（仅 AdaptiBrush 使用）
        [Header("AdaptiBrush Disk Preview")]
        [SerializeField] private Color diskColor = new Color(1f, 0.92f, 0.016f, 0.5f); // 黄色半透明
        [SerializeField] private float diskRadius = 0.05f; // 圆盘半径
        private GameObject orientationDisk;
        private Material diskMaterial;
        private Vector3 currentTangentForPreview = Vector3.forward;

        protected override void Start()
        {
            base.Start();

            // 设置预览颜色
            if (universalBrushPreview != null)
            {
                // Adapti 与 Helio 使用相同的预览逻辑（UniversalBrushPreview），
                // 只是在 Adapti 里形状固定为“丝带/线段”截面。
                universalBrushPreview.SetUseHelioVisuals(true);
                // Adapti 希望两端显示小球顶点以匹配 ribbon 的端点效果
                universalBrushPreview.SetShowShapePoints(true);

                // 为 Adapti 明确设定一个合适的小球世界半径以保证边的宽度与 Helio 一致
                // Helio 的 desiredPointWorldRadius = GetDefaultPreviewSphereRadius() * pointToCylinderMultiplier (1.2f)
                universalBrushPreview.SetPreviewSphereWorldRadius(UniversalBrushPreview.GetDefaultPreviewSphereRadius() * 1.2f);

                // 设置固定的线段形状作为截面预览
                var ribbonShape = CreateRibbonLineSegmentShape();
                universalBrushPreview.CurrentShape = ribbonShape;
            }

            // 创建 AdaptiBrush 专用的方向圆盘
            CreateOrientationDisk();
        }

        /// <summary>
        /// 创建黄色方向圆盘，用于显示垂直于移动方向的平面
        /// </summary>
        private void CreateOrientationDisk()
        {
            orientationDisk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            orientationDisk.name = "AdaptiBrush_OrientationDisk";
            
            // 移除碰撞体
            var collider = orientationDisk.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);

            // 创建半透明材质
            diskMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            diskMaterial.SetFloat("_Surface", 1f); // Transparent
            diskMaterial.SetFloat("_Blend", 0f);
            diskMaterial.SetFloat("_AlphaClip", 0f);
            diskMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            diskMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            diskMaterial.SetInt("_ZWrite", 0);
            diskMaterial.renderQueue = 3000;
            diskMaterial.color = diskColor;
            diskMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            orientationDisk.GetComponent<Renderer>().material = diskMaterial;
            orientationDisk.SetActive(true);
        }

        /// <summary>
        /// 更新圆盘的位置和旋转
        /// </summary>
        private void UpdateOrientationDisk(Vector3 position, Vector3 tangent)
        {
            if (orientationDisk == null) return;

            // 圆盘位置跟随画笔位置
            orientationDisk.transform.position = position;

            // 圆盘的Y轴（圆柱体的高度方向）应该与切线方向对齐
            // 这样圆盘面就垂直于移动方向
            if (tangent.sqrMagnitude > 0.001f)
            {
                orientationDisk.transform.rotation = Quaternion.FromToRotation(Vector3.up, tangent.normalized);
            }

            // 设置圆盘大小：扁平的圆盘
            float thickness = 0.002f; // 很薄
            float radius = diskRadius;
            orientationDisk.transform.localScale = new Vector3(radius * 2f, thickness, radius * 2f);
        }

        protected override void Update()
        {
            base.Update();

            // 更新圆盘预览
            Vector3 position = GetDrawingPosition();
            UpdateOrientationDisk(position, currentTangentForPreview);
        }

        private void OnEnable()
        {
            // 当笔刷激活时显示圆盘
            if (orientationDisk != null)
            {
                orientationDisk.SetActive(true);
            }
        }

        private void OnDisable()
        {
            // 当笔刷禁用时隐藏圆盘
            if (orientationDisk != null)
            {
                orientationDisk.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            // 清理圆盘
            if (orientationDisk != null)
            {
                Object.Destroy(orientationDisk);
            }
            if (diskMaterial != null)
            {
                Object.Destroy(diskMaterial);
            }
        }

        // 为 Adapti 的预览创建一个固定的线段形状（两点一边，长度≈2个单位，按 ribbonWidth 缩放）。
        private BrushShape CreateRibbonLineSegmentShape()
        {
            var shape = new BrushShape("adapti_ribbon_segment");
            // 使用局部2D坐标：(-0.5,0) 到 (0.5,0)
            // Universal 会用 up/right * previewRibbonWidth 进行缩放
            // 这样线段总长度 == previewRibbonWidth，与实际笔刷的 width 一致
            shape.AddNode(new Vector2(-0.5f, 0f));
            shape.AddNode(new Vector2(0.5f, 0f));
            shape.AddEdge(0, 1);
            return shape;
        }

        /// <summary>
        /// 论文中的C²连续三次贝塞尔切线构造（与算法一致）
        /// </summary>
        private static Vector3 ComputeTrajectoryTangent(Vector3 p, Vector3 p1, Vector3 p2)
        {
            Vector3 h1 = p1 + (p2 - p) / 6f;
            Vector3 h0 = h1 + (p1 - p) / 3f;
            return (p - h0).normalized;
        }

        protected override void OnStrokeStart()
        {
            // 重置状态
            prevPos = GetDrawingPosition();
            prevPos2 = Vector3.zero;
            prevRuling = Vector3.zero;
            prevTangent = Vector3.zero;
            initialized = false;
        }

        protected override void OnStrokeEnd()
        {
            // 保持基类的收尾逻辑
            initialized = false;
        }

        protected override Vector3 ComputeRulingDirection(Vector3 currentTangent, Vector3 currentPosition)
        {
            // 计算控制器坐标系
            var rot = GetControllerRotation();
            Matrix4x4 frame = Matrix4x4.identity;
            frame.SetColumn(0, rot * Vector3.right);
            frame.SetColumn(1, rot * Vector3.up);
            frame.SetColumn(2, rot * Vector3.forward);

            Vector3 tangent;

            Vector3 ruling;
            if (!initialized || prevRuling == Vector3.zero)
            {
                // 首帧初始化：论文使用 p'->p 作为切线
                Vector3 initTangent = (currentPosition - prevPos).normalized;
                Vector3 upAxis = frame.GetColumn(1);
                Vector3 fwdAxis = frame.GetColumn(2);
                ruling = AdaptiBrushAlgorithm.InitializeRuling(initTangent, upAxis, fwdAxis);
                tangent = initTangent;
                initialized = true;
            }
            else
            {
                // 后续帧调用核心算法
                Vector3 vTilde;
                ruling = AdaptiBrushAlgorithm.ComputeRulingDirection(
                    out vTilde,
                    currentPosition,
                    prevPos,
                    prevPos2,
                    frame,
                    prevRuling,
                    prevTangent
                );
                tangent = ComputeTrajectoryTangent(currentPosition, prevPos, prevPos2);
            }

            // 更新历史
            prevPos2 = prevPos;
            prevPos = currentPosition;
            prevTangent = tangent;
            prevRuling = ruling;

            // 更新圆盘预览的切线方向
            currentTangentForPreview = tangent;

            return ruling.normalized;
        }
    }
}