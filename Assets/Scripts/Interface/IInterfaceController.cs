using UnityEngine;

namespace VRBrush.Interface
{
    /// <summary>
    /// 所有界面控制器组件的通用接口
    /// 用于UI管理器与不同类型的控制器进行统一交互
    /// </summary>
    public interface IInterfaceController
    {
        /// <summary>
        /// 控制器的名称，用于在UI中显示
        /// </summary>
        string ControllerName { get; }

        /// <summary>
        /// 设置输入源（控制器对象）
        /// </summary>
        /// <param name="inputSource">输入源对象</param>
        void SetInputSource(GameObject inputSource);
    }
}