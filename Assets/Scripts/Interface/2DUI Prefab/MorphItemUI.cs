using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace VRBrush.Interface.UI
{
    /// <summary>
    /// 单个 Morph 项的 UI 逻辑（基于 MorphItem.prefab）
    /// 预制体层级要求：
    /// - Label (TMP_Text)
    /// - MinMax Slider (包含 Slider 组件)
    ///   - Fill Area Background
    ///   - Handle Slide Area
    /// - Value Text (TMP_Text)
    /// - Type Text (TMP_Text)
    /// </summary>
    public class MorphItemUI : MonoBehaviour
    {
        [Header("Bindings")]
        [SerializeField] private TMP_Text label;
        [SerializeField] private Slider slider; // 挂在“MinMax Slider”节点上
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private TMP_Text typeText;

        // metadata
        public int MorphIndex { get; private set; } // 0 表示 Size（缩放），1..N 表示各 morph
        public string MorphName { get; private set; }

        // 回调由宿主（如 ADBrushController）注入
        private System.Action<int, float> _onValueChanged;

        public void Initialize(string morphName, int morphIndex, float min, float max, float defaultValue,
            System.Action<int, float> onValueChanged,
            string typeLabel = "Morph")
        {
            MorphName = morphName;
            MorphIndex = morphIndex;
            _onValueChanged = onValueChanged;

            if (label) label.text = morphName;
            if (typeText) typeText.text = typeLabel;

            if (slider)
            {
                slider.minValue = min;
                slider.maxValue = max;
                slider.wholeNumbers = false;
                slider.onValueChanged.RemoveAllListeners();
                slider.onValueChanged.AddListener(OnSliderChanged);
                slider.SetValueWithoutNotify(defaultValue);
            }

            UpdateValueText(defaultValue, min, max);
        }

        public void SetValue(float v)
        {
            if (slider)
            {
                slider.SetValueWithoutNotify(v);
                UpdateValueText(v, slider.minValue, slider.maxValue);
            }
        }

        private void OnSliderChanged(float v)
        {
            UpdateValueText(v, slider ? slider.minValue : 0f, slider ? slider.maxValue : 1f);
            _onValueChanged?.Invoke(MorphIndex, v);
        }

        private void UpdateValueText(float v, float min, float max)
        {
            if (valueText)
            {
                // 显示为 0.00 ~ 1.00 或实际范围
                if (Mathf.Approximately(min, 0f) && Mathf.Approximately(max, 1f))
                    valueText.text = v.ToString("0.00");
                else
                    valueText.text = v.ToString("0.00");
            }
        }
    }
}
