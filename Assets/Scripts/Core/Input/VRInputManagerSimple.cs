using UnityEngine;
using UnityEngine.XR;
using System.Collections.Generic;

namespace VRBrush.Core.Input
{
    /// <summary>
    /// 基于历史版本的简单VR输入管理器，使用UnityEngine.XR API
    /// </summary>
    public class VRInputManagerSimple : MonoBehaviour
    {
        [Header("Input Settings")]
        [SerializeField] private float triggerThreshold = 0.1f;
        [SerializeField] private XRNode inputNode = XRNode.RightHand;
        [Header("Debug")]
        [SerializeField] private bool showDebugInfo = false;

        // 输入状态
        public bool IsTriggerPressed { get; private set; }
        public bool TriggerJustPressed { get; private set; }
        public bool TriggerJustReleased { get; private set; }

        // Primary Button 状态
        public bool PrimaryButtonPressed { get; private set; }
        public bool PrimaryButtonJustPressed { get; private set; }
        public bool PrimaryButtonJustReleased { get; private set; }

        // 位置和旋转
        public Vector3 ControllerPosition { get; private set; }
        public Quaternion ControllerRotation { get; private set; }

        private bool deviceFound = false;
        // 状态跟踪
        private bool wasTriggerPressed = false;
        private bool wasPrimaryButtonPressed = false;

        private InputDevice targetDevice;

        private void Start()
        {
            // 初始化位置
            ControllerPosition = transform.position;
        }

        void Update()
        {
            UpdateInputDevice();
            UpdateTriggerInput();
            UpdatePrimaryButtonInput();
            UpdatePositionTracking();
        }

        /// <summary>
        /// 更新输入设备
        /// </summary>
        private void UpdateInputDevice()
        {
            if (!deviceFound)
            {
                List<InputDevice> devices = new List<InputDevice>();
                InputDevices.GetDevicesAtXRNode(inputNode, devices);
                if (devices.Count > 0)
                {
                    targetDevice = devices[0];
                    deviceFound = true;
                    Debug.Log($"VRInputManagerSimple: Found device: {targetDevice.name}");
                }
            }
        }

        /// <summary>
        /// 更新触发器输入状态
        /// </summary>
        void UpdateTriggerInput()
        {
            if (!deviceFound) return;

            // 获取触发器值
            float triggerValue = 0f;
            bool success = targetDevice.TryGetFeatureValue(CommonUsages.trigger, out triggerValue);

            if (!success)
            {
                if (showDebugInfo)
                    Debug.LogWarning("VRInputManagerSimple: Failed to read trigger value");
                return;
            }

            // 计算状态
            bool currentTriggerPressed = triggerValue > triggerThreshold;

            // 检测按下和释放事件
            TriggerJustPressed = currentTriggerPressed && !wasTriggerPressed;
            TriggerJustReleased = !currentTriggerPressed && wasTriggerPressed;
            IsTriggerPressed = currentTriggerPressed;

            // 更新历史状态
            wasTriggerPressed = currentTriggerPressed;

            if (showDebugInfo && (TriggerJustPressed || TriggerJustReleased))
            {
                Debug.Log($"VRInputManagerSimple - Trigger: {triggerValue:F3}, Threshold: {triggerThreshold:F3}, Pressed: {currentTriggerPressed}, JustPressed: {TriggerJustPressed}, JustReleased: {TriggerJustReleased}");
            }
        }

        /// <summary>
        /// 更新Primary Button输入状态（通常是A键）
        /// </summary>
        void UpdatePrimaryButtonInput()
        {
            if (!deviceFound) return;

            // 获取Primary Button值
            bool primaryButtonValue = false;
            bool success = targetDevice.TryGetFeatureValue(CommonUsages.primaryButton, out primaryButtonValue);

            if (!success)
            {
                if (showDebugInfo)
                    Debug.LogWarning("VRInputManagerSimple: Failed to read primary button value");
                return;
            }

            // 检测按下和释放事件
            PrimaryButtonJustPressed = primaryButtonValue && !wasPrimaryButtonPressed;
            PrimaryButtonJustReleased = !primaryButtonValue && wasPrimaryButtonPressed;
            PrimaryButtonPressed = primaryButtonValue;

            // 更新历史状态
            wasPrimaryButtonPressed = primaryButtonValue;

            if (showDebugInfo && (PrimaryButtonJustPressed || PrimaryButtonJustReleased))
            {
                Debug.Log($"VRInputManagerSimple - Primary Button: {primaryButtonValue}, JustPressed: {PrimaryButtonJustPressed}, JustReleased: {PrimaryButtonJustReleased}");
            }
        }

        /// <summary>
        /// 更新位置和旋转追踪
        /// </summary>
        private void UpdatePositionTracking()
        {
            if (!deviceFound) return;

            // 获取当前位置和旋转
            Vector3 position;
            Quaternion rotation;

            if (targetDevice.TryGetFeatureValue(CommonUsages.devicePosition, out position))
            {
                ControllerPosition = position;
            }

            if (targetDevice.TryGetFeatureValue(CommonUsages.deviceRotation, out rotation))
            {
                ControllerRotation = rotation;
            }
        }

        /// <summary>
        /// 获取触发器输入值
        /// </summary>
        public float GetTriggerValue()
        {
            if (!deviceFound) return 0f;

            float triggerValue = 0f;
            targetDevice.TryGetFeatureValue(CommonUsages.trigger, out triggerValue);
            return triggerValue;
        }

        /// <summary>
        /// 获取前进方向轴
        /// </summary>
        public Vector3 GetForwardAxis() => ControllerRotation * Vector3.forward;

        /// <summary>
        /// 获取上方向轴
        /// </summary>
        public Vector3 GetUpAxis() => ControllerRotation * Vector3.up;

        /// <summary>
        /// 获取右方向轴
        /// </summary>
        public Vector3 GetRightAxis() => ControllerRotation * Vector3.right;

        private void OnGUI()
        {
            if (!showDebugInfo) return;

            GUILayout.BeginArea(new Rect(10, 10, 350, 250));
            GUILayout.Label("=== VRInputManagerSimple ===");
            GUILayout.Label($"设备已连接: {deviceFound}");
            if (deviceFound)
            {
                GUILayout.Label($"设备名称: {targetDevice.name}");
                GUILayout.Label($"触发器值: {GetTriggerValue():F3}");
            }
            GUILayout.Label($"扳机按下: {IsTriggerPressed}");
            GUILayout.Label($"刚按下: {TriggerJustPressed}");
            GUILayout.Label($"刚释放: {TriggerJustReleased}");
            GUILayout.Label($"位置: {ControllerPosition:F2}");
            GUILayout.Label($"旋转: {ControllerRotation.eulerAngles:F1}");
            GUILayout.EndArea();
        }
    }
}