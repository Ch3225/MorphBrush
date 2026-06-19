using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace VRBrush.Core
{
    /// <summary>
    /// 笔画撤销管理器
    /// 用于撤销最近绘制的笔画（不影响正在绘制的笔画）
    /// 独立于ADBrush实现，通过查找strokesParent下的子对象来工作
    /// </summary>
    public class StrokeUndoManager : MonoBehaviour
    {
        [Header("Stroke Parent Reference")]
        [Tooltip("笔画的父对象，所有完成的笔画都是其子对象")]
        [SerializeField] private Transform strokesParent;

        [Tooltip("如果未指定strokesParent，则使用此名称在场景中查找")]
        [SerializeField] private string strokesParentName = "BrushStrokes";

        [Header("Events")]
        [Tooltip("撤销操作的回调事件")]
        public System.Action OnUndoPerformed;

        // 撤销次数统计
        private int undoCount = 0;

        /// <summary>
        /// 获取撤销次数
        /// </summary>
        public int UndoCount => undoCount;

        private void Start()
        {
            // 如果未指定strokesParent，尝试在场景中查找
            if (strokesParent == null)
            {
                FindStrokesParent();
            }
        }

        /// <summary>
        /// 在场景中查找笔画父对象
        /// </summary>
        private void FindStrokesParent()
        {
            // 首先尝试通过VRBrushSystem获取
            var vrBrushSystem = FindFirstObjectByType<VRBrushSystem>();
            if (vrBrushSystem != null)
            {
                // 使用反射获取strokesParent
                var field = vrBrushSystem.GetType().GetField("strokesParent",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    strokesParent = field.GetValue(vrBrushSystem) as Transform;
                    if (strokesParent != null)
                    {
                        Debug.Log($"StrokeUndoManager: Found strokesParent from VRBrushSystem: {strokesParent.name}");
                        return;
                    }
                }
            }

            // 否则通过名称查找
            var found = GameObject.Find(strokesParentName);
            if (found != null)
            {
                strokesParent = found.transform;
                Debug.Log($"StrokeUndoManager: Found strokesParent by name: {strokesParent.name}");
            }
            else
            {
                Debug.LogWarning($"StrokeUndoManager: Could not find strokesParent '{strokesParentName}'");
            }
        }

        /// <summary>
        /// 撤销最近的一个笔画
        /// </summary>
        /// <returns>是否成功撤销</returns>
        public bool UndoLastStroke()
        {
            if (strokesParent == null)
            {
                FindStrokesParent();
                if (strokesParent == null)
                {
                    Debug.LogWarning("StrokeUndoManager: Cannot undo - strokesParent not found");
                    return false;
                }
            }

            // 获取所有子对象（笔画）
            int childCount = strokesParent.childCount;
            if (childCount == 0)
            {
                Debug.Log("StrokeUndoManager: No strokes to undo");
                return false;
            }

            // 查找最后一个完成的笔画（不是正在绘制的）
            // 正在绘制的笔画通常没有MeshFilter或其mesh为空/顶点很少
            Transform lastCompletedStroke = null;
            for (int i = childCount - 1; i >= 0; i--)
            {
                Transform child = strokesParent.GetChild(i);
                
                // 检查是否是有效的完成笔画
                if (IsCompletedStroke(child.gameObject))
                {
                    lastCompletedStroke = child;
                    break;
                }
            }

            if (lastCompletedStroke == null)
            {
                Debug.Log("StrokeUndoManager: No completed strokes to undo");
                return false;
            }

            // 删除找到的笔画
            string strokeName = lastCompletedStroke.name;
            DestroyImmediate(lastCompletedStroke.gameObject);
            undoCount++;

            Debug.Log($"StrokeUndoManager: Undid stroke '{strokeName}'. Total undo count: {undoCount}");
            
            // 触发回调
            OnUndoPerformed?.Invoke();

            return true;
        }

        /// <summary>
        /// 检查一个GameObject是否是已完成的笔画
        /// </summary>
        private bool IsCompletedStroke(GameObject obj)
        {
            if (obj == null) return false;

            // 检查是否有MeshFilter组件
            var meshFilter = obj.GetComponent<MeshFilter>();
            if (meshFilter == null) return false;

            // 检查是否有有效的mesh
            var mesh = meshFilter.sharedMesh;
            if (mesh == null) return false;

            // 检查mesh是否有足够的顶点（正在绘制的笔画可能顶点很少）
            // 一个有效的完成笔画至少应该有一些顶点
            if (mesh.vertexCount < 3) return false;

            return true;
        }

        /// <summary>
        /// 获取当前可撤销的笔画数量
        /// </summary>
        public int GetUndoableStrokeCount()
        {
            if (strokesParent == null)
            {
                FindStrokesParent();
                if (strokesParent == null) return 0;
            }

            int count = 0;
            for (int i = 0; i < strokesParent.childCount; i++)
            {
                if (IsCompletedStroke(strokesParent.GetChild(i).gameObject))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// 重置撤销计数
        /// </summary>
        public void ResetUndoCount()
        {
            undoCount = 0;
        }

        /// <summary>
        /// 设置笔画父对象（供外部调用）
        /// </summary>
        public void SetStrokesParent(Transform parent)
        {
            strokesParent = parent;
            Debug.Log($"StrokeUndoManager: strokesParent set to '{parent?.name ?? "null"}'");
        }
    }
}
