using UnityEngine;

namespace VRBrush.Interface
{
    /// <summary>
    /// 所有笔刷控制器都应实现的接口
    /// 用于UI管理器与不同类型的笔刷进行交互
    /// </summary>
    public interface IBrushController : IInterfaceController
    {
        /// <summary>
        /// 笔刷的名称，用于在UI中显示
        /// </summary>
        string BrushName { get; }

        /// <summary>
        /// 获取笔刷到控制器中心的距离
        /// </summary>
        float BrushDistance { get; }

        /// <summary>
        /// 获取笔刷相对前方向上的角度
        /// </summary>
        float BrushAngleUp { get; }

        /// <summary>
        /// 当前笔刷宽度（截面整体尺度）
        /// </summary>
        float BrushWidth { get; }
        /// <summary>
        /// 可用宽度范围（每个笔刷自带）
        /// </summary>
        UnityEngine.Vector2 BrushWidthRange { get; }

        /// <summary>
        /// 设置笔刷到控制器中心的距离
        /// </summary>
        /// <param name="distance">距离值</param>
        void SetBrushDistance(float distance);

        /// <summary>
        /// 设置笔刷相对前方向上的角度
        /// </summary>
        /// <param name="angle">角度值（度）</param>
        void SetBrushAngleUp(float angle);

        /// <summary>
        /// 设置笔刷宽度
        /// </summary>
        void SetBrushWidth(float width);

        /// <summary>
        /// 清除该笔刷绘制的所有痕迹
        /// </summary>
        void ClearAllStrokes();

        /// <summary>
        /// 设置笔画父对象
        /// </summary>
        /// <param name="parent">笔画父对象</param>
        void SetStrokesParent(Transform parent);
    }
}
