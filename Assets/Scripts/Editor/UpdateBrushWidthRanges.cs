using UnityEngine;
using UnityEditor;
using VRBrush.Interface;

namespace VRBrush.Editor
{
    /// <summary>
    /// 编辑器工具：批量更新所有 BaseBrushController 的 ribbonWidthRange 上限为 4.0
    /// </summary>
    public class UpdateBrushWidthRanges : EditorWindow
    {
        [MenuItem("VRBrush/Fix Brush Width Ranges")]
        public static void UpdateAllBrushWidthRanges()
        {
            int updatedCount = 0;

            // 查找场景中所有的 BaseBrushController（包括未激活对象），不需要排序
            var allBrushes = Object.FindObjectsByType<BaseBrushController>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (var brush in allBrushes)
            {
                // 使用反射强制更新序列化字段
                var field = typeof(BaseBrushController).GetField("ribbonWidthRange", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                
                if (field != null)
                {
                    Vector2 currentRange = (Vector2)field.GetValue(brush);
                    
                    // 如果最大值小于 4.0，强制更新
                    if (currentRange.y < 4.0f)
                    {
                        field.SetValue(brush, new Vector2(0.001f, 4.0f));
                        EditorUtility.SetDirty(brush);
                        updatedCount++;
                        Debug.Log($"Updated {brush.gameObject.name}: ribbonWidthRange from {currentRange} to (0.001, 4.0)");
                    }
                }
            }
            
            if (updatedCount > 0)
            {
                Debug.Log($"<color=green>Successfully updated {updatedCount} brush(es) width range to (0.001, 4.0)</color>");
                EditorUtility.DisplayDialog("Update Complete", 
                    $"Updated {updatedCount} brush controller(s) width range to 4.0.\n\nPlease save the scene!", "OK");
            }
            else
            {
                Debug.Log("<color=yellow>All brushes already have correct width range (4.0)</color>");
                EditorUtility.DisplayDialog("Already Up-to-date", 
                    "All brush controllers already have the correct width range (4.0).", "OK");
            }
        }
    }
}
