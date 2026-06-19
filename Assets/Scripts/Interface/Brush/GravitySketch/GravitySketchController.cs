using UnityEngine;
using VRBrush.Core;
using VRBrush.Core.Model;

namespace VRBrush.Interface.Brush.GravitySketch
{
    /// <summary>
    /// GravitySketch控制器
    /// 固定笔刷：ruling方向始终跟随控制器的Forward轴
    /// 无论是否在绘制，笔刷方向始终与控制器绑定
    /// 触发模式：按下扳机即绘制
    /// </summary>
    public class GravitySketchController : BaseBrushController
    {
        public override string BrushName => "GravitySketch";

        protected override void Start()
        {
            base.Start();

            // 设置预览样式
            if (universalBrushPreview != null)
            {
                universalBrushPreview.SetUseHelioVisuals(true);
                universalBrushPreview.SetShowShapePoints(true);

                // 设置固定的线段形状作为截面预览
                var ribbonShape = CreateRibbonLineSegmentShape();
                universalBrushPreview.CurrentShape = ribbonShape;
            }
        }

        /// <summary>
        /// 创建丝带状线段形状（两点一边）
        /// </summary>
        private BrushShape CreateRibbonLineSegmentShape()
        {
            var shape = new BrushShape("gravitysketch_ribbon_segment");
            // 使用局部2D坐标：(-0.5,0) 到 (0.5,0)
            shape.AddNode(new Vector2(-0.5f, 0f));
            shape.AddNode(new Vector2(0.5f, 0f));
            shape.AddEdge(0, 1);
            return shape;
        }

        /// <summary>
        /// 固定笔刷：预览始终跟随控制器，无论是否在绘制
        /// 返回null让基类使用controllerRotation
        /// </summary>
        protected override Quaternion? GetCustomDrawingPreviewRotation()
        {
            return null;
        }

        /// <summary>
        /// 重写主预览旋转：构造让预览的up方向对齐到控制器Forward轴的旋转
        /// PreviewGeometry.Map2DToWorld 使用 orientation.up 作为 ruling 方向
        /// </summary>
        protected override Quaternion? GetMainPreviewRotationOverride()
        {
            // GravitySketch: ruling = 控制器Forward轴
            // 需要构造一个旋转使得 rotation * Vector3.up = controllerForward
            Vector3 ruling = GetControllerForwardAxis();
            Vector3 right = GetControllerRightAxis();
            
            // 构造旋转：up -> ruling
            // 使用 LookRotation(forward, up)，我们需要 up = ruling
            // forward 设为与 ruling 和 right 都垂直的方向
            Vector3 perpForward = Vector3.Cross(ruling, right).normalized;
            if (perpForward.sqrMagnitude < 0.01f)
            {
                perpForward = Vector3.Cross(ruling, Vector3.up).normalized;
            }
            return Quaternion.LookRotation(perpForward, ruling);
        }

        /// <summary>
        /// 固定笔刷：覆盖预览更新，确保空闲时也使用正确的旋转
        /// UniversalBrushPreview.UpdatePreview 在 isDrawing=false 时使用第一个旋转参数
        /// 所以我们需要把两个旋转参数都设为正确的预览旋转
        /// </summary>
        protected override void UpdateUniversalBrushPreview(Vector3 position, Quaternion controllerRotation, bool isDrawing, float speed)
        {
            if (universalBrushPreview == null) return;

            // 计算正确的预览旋转（空闲和绘制时都使用相同的旋转）
            Quaternion previewRotation = GetMainPreviewRotationOverride() ?? controllerRotation;

            // 计算预览宽度
            float previewWidth = isDrawing ? GetCurrentPointWidth() : ribbonWidth;
            
            // 传递相同的旋转给两个参数，确保空闲时也使用正确的旋转
            universalBrushPreview.UpdatePreview(position, previewRotation, previewRotation, isDrawing, previewWidth);
        }

        /// <summary>
        /// 计算Ruling方向（GravitySketch方法：直接使用控制器Forward轴）
        /// 固定笔刷：ruling完全由控制器决定，不受切线影响
        /// </summary>
        protected override Vector3 ComputeRulingDirection(Vector3 currentTangent, Vector3 currentPosition)
        {
            // 固定笔刷：直接使用控制器的Forward轴作为ruling
            // 这使得丝带的宽度方向始终与控制器的前向一致
            return GetControllerForwardAxis();
        }
    }
}
