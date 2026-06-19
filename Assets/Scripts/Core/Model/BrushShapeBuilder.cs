using System.Collections.Generic;
using UnityEngine;

namespace VRBrush.Core.Model
{
    /// <summary>
    /// 负责构建BrushShape的辅助类，封装了构建过程中的状态和操作
    /// 主要由BrushEditor在构建过程中使用
    /// </summary>
    public class BrushShapeBuilder
    {
        private BrushShape targetShape;
        private int? previousNodeIndex = null;  // "上一个点"的索�?
        // 预览点（用于长按预览�?- 不写入图，仅作临时目�?
        private Vector2? previewNode = null;

        // 事件通知
        public System.Action<int> OnNodeCreated;
        public System.Action<Vector2Int> OnEdgeCreated;
        public System.Action<int> OnNodeRemoved;
        public System.Action<int, Vector2> OnNodeMoved; // Add this
        public System.Action<int> OnPreviousNodeChanged;

        public BrushShapeBuilder(BrushShape shape)
        {
            targetShape = shape ?? throw new System.ArgumentNullException(nameof(shape));
        }

        // Note: Do not expose the internal BrushShape reference directly.
        // Provide safe read-only and mutation APIs instead.

        /// <summary>
        /// 连接阈值 - 用于确定何时连接到现有节点
        /// </summary>
        public float ConnectThreshold { get; set; } = 0.035f;

        /// <summary>
        /// 吸附阈值 - 用于确定何时吸附到现有节点
        /// </summary>
        public float SnapThreshold { get; set; } = 0.05f;

        /// <summary>
        /// 节点数量（只读）
        /// </summary>
        public int NodeCount => targetShape.NodeCount;

        /// <summary>
        /// 边数量（只读�?
        /// </summary>
        public int EdgeCount => targetShape.EdgeCount;

        /// <summary>
        /// 获取指定索引的节点（局部坐标）
        /// </summary>
        public Vector2 GetNode(int index) => targetShape.Nodes[index];

        /// <summary>
        /// 获取只读节点集合
        /// </summary>
        public IReadOnlyList<Vector2> GetNodes() => targetShape.Nodes;

        /// <summary>
        /// 获取只读边集�?
        /// </summary>
        public IReadOnlyList<Vector2Int> GetEdges() => targetShape.Edges;

        /// <summary>
        /// 添加一条边（安全包装），会在成功时触发 OnEdgeCreated
        /// </summary>
        public bool AddEdge(int a, int b)
        {
            var edge = new Vector2Int(a, b);
            if (targetShape.AddEdge(edge))
            {
                OnEdgeCreated?.Invoke(edge);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 移除节点（安全包装），会在成功时触发 OnNodeRemoved
        /// </summary>
        public bool RemoveNode(int index)
        {
            if (targetShape.RemoveNode(index))
            {
                OnNodeRemoved?.Invoke(index);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Moves a node to a new local position.
        /// </summary>
        public bool MoveNode(int index, Vector2 newLocalPosition)
        {
            if (targetShape.SetNode(index, newLocalPosition))
            {
                OnNodeMoved?.Invoke(index, newLocalPosition);
                return true;
            }
            return false;
        }

        #region State Management

        // 状态查�?
        public bool HasPreviousNode => previousNodeIndex.HasValue &&
                                       previousNodeIndex.Value >= 0 &&
                                       previousNodeIndex.Value < targetShape.NodeCount;

        public int? PreviousNodeIndex => HasPreviousNode ? previousNodeIndex : null;

        /// <summary>
        /// 是否存在预览�?
        /// </summary>
        public bool HasPreviewNode => previewNode.HasValue;

        /// <summary>
        /// 获取当前预览点（局部坐标），当不存在时抛出
        /// </summary>
        public Vector2 PreviewNode => previewNode.Value;

        /// <summary>
        /// 设置预览点（临时�?
        /// </summary>
        public void SetPreviewNode(Vector2 localPosition)
        {
            previewNode = localPosition;
        }

        /// <summary>
        /// 清除预览�?
        /// </summary>
        public void ClearPreviewNode()
        {
            previewNode = null;
        }

        /// <summary>
        /// 设置上一个节�?
        /// </summary>
        /// <param name="nodeIndex">节点索引</param>
        public void SetPreviousNode(int nodeIndex)
        {
            if (nodeIndex >= 0 && nodeIndex < targetShape.NodeCount)
            {
                previousNodeIndex = nodeIndex;
                OnPreviousNodeChanged?.Invoke(nodeIndex);
            }
        }

        /// <summary>
        /// 清空上一个节�?
        /// </summary>
        public void ClearPreviousNode()
        {
            if (previousNodeIndex.HasValue)
            {
                previousNodeIndex = null;
                OnPreviousNodeChanged?.Invoke(-1); // -1表示清空
            }
        }

        /// <summary>
        /// 重置构建器状�?
        /// </summary>
        public void Reset()
        {
            previousNodeIndex = null;
            OnPreviousNodeChanged?.Invoke(-1);
        }

        #endregion

        #region Controller Input Operations

        /// <summary>
        /// 处理右手控制器A键短按：创建顶点并连接
        /// </summary>
        /// <param name="worldPosition">世界坐标位置</param>
        /// <param name="localPosition">局部坐标位置</param>
        /// <returns>创建的节点索引</returns>
        public int HandlePrimaryButtonShortPress(Vector3 worldPosition, Vector2 localPosition)
        {
            // 创建新顶�?
            int newNodeIndex = targetShape.AddNode(localPosition);

            // 如果存在previousNode，则创建�?
            if (HasPreviousNode)
            {
                var edge = new Vector2Int(previousNodeIndex.Value, newNodeIndex);
                targetShape.AddEdge(edge);
                OnEdgeCreated?.Invoke(edge);
            }

            // 更新previousNode
            SetPreviousNode(newNodeIndex);

            OnNodeCreated?.Invoke(newNodeIndex);
            return newNodeIndex;
        }

        /// <summary>
        /// 处理右手控制器A键长按结束：查找最近节点并连接
        /// 这个方法调用两个函数：1.查询最近节点 2.生成连接
        /// </summary>
        /// <param name="targetPosition">目标位置（局部坐标）</param>
        /// <param name="maxDistance">最大连接距离阈值</param>
        public void HandlePrimaryButtonLongPressEnd(Vector2 targetPosition, float maxDistance)
        {
            // 1. 查询离目标位置最近的节点
            int nearestNodeIndex = FindNearestNodeForConnection(targetPosition, maxDistance);

            if (nearestNodeIndex >= 0)
            {
                // 2. 生成连接该节点到"上一节点"的线
                ConnectToPreviousNode(nearestNodeIndex);
            }
        }

        /// <summary>
        /// 查询离某个位置最近的节点（距离可以作为可选参数用threshold�?
        /// 此方法应该在BrushGraph里，这里是为了与现有接口兼容的包�?
        /// </summary>
        /// <param name="position">目标位置</param>
        /// <param name="maxDistance">最大距离阈�?/param>
        /// <returns>最近节点索引，-1表示未找�?/returns>
        public int FindNearestNodeForConnection(Vector2 position, float maxDistance)
        {
            return targetShape.FindNearestNode(position, maxDistance);
        }

        /// <summary>
        /// 生成连接指定节点�?上一节点"的线
        /// </summary>
        /// <param name="targetNodeIndex">要连接的节点索引</param>
        public void ConnectToPreviousNode(int targetNodeIndex)
        {
            if (HasPreviousNode && targetNodeIndex != previousNodeIndex.Value)
            {
                // 创建边连接previousNode和targetNode
                var edge = new Vector2Int(previousNodeIndex.Value, targetNodeIndex);
                if (targetShape.AddEdge(edge))
                {
                    OnEdgeCreated?.Invoke(edge);
                }

                // 更新previousNode为targetNode
                SetPreviousNode(targetNodeIndex);
            }
        }

        /// <summary>
        /// 处理右手控制器B键短按：清空previousNode
        /// </summary>
        public void HandleSecondaryButtonShortPress()
        {
            ClearPreviousNode();
        }

        /// <summary>
        /// 处理右手控制器扳机按下：清空previousNode（当扳机值超过阈值时�?
        /// </summary>
        /// <param name="triggerValue">扳机�?/param>
        /// <param name="triggerThreshold">扳机阈�?/param>
        public void HandleTriggerPress(float triggerValue, float triggerThreshold = 0.3f)
        {
            if (triggerValue > triggerThreshold)
            {
                ClearPreviousNode();
            }
        }

        #endregion

        #region Utility Methods

        /// <summary>
        /// 将局部坐标转换为世界坐标
        /// </summary>
        /// <param name="localCoord">局部坐标 (x, y)</param>
        /// <param name="coordinateTransform">坐标系变换</param>
        /// <returns>世界坐标</returns>
        public static Vector3 LocalToWorldPosition(Vector2 localCoord, Transform coordinateTransform)
        {
            // 局部坐标转世界坐标
            Vector3 localCartesian = new Vector3(localCoord.x, 0f, localCoord.y);
            return coordinateTransform.TransformPoint(localCartesian);
        }

        /// <summary>
        /// 将世界坐标转换为局部坐标
        /// </summary>
        /// <param name="worldPosition">世界坐标</param>
        /// <param name="coordinateTransform">坐标系变换</param>
        /// <returns>局部坐标 (x, y)</returns>
        public static Vector2 WorldToLocalPosition(Vector3 worldPosition, Transform coordinateTransform)
        {
            Vector3 localPoint = coordinateTransform.InverseTransformPoint(worldPosition);
            return new Vector2(localPoint.x, localPoint.z);
        }

        /// <summary>
        /// 尝试移除最近的节点
        /// </summary>
        /// <param name="targetPosition">参考位置（局部坐标）</param>
        /// <param name="maxDistance">移除距离阈值</param>
        /// <returns>是否成功移除节点</returns>
        public bool TryRemoveNearestNode(Vector2 targetPosition, float maxDistance)
        {
            int nearestNodeIndex = targetShape.FindNearestNode(targetPosition, maxDistance);

            if (nearestNodeIndex >= 0)
            {
                // 如果要删除的是previousNode，清空它
                if (previousNodeIndex == nearestNodeIndex)
                {
                    ClearPreviousNode();
                }
                // 如果previousNode的索引大于被删除的节点，需要调�?
                else if (previousNodeIndex > nearestNodeIndex)
                {
                    previousNodeIndex--;
                }

                targetShape.RemoveNode(nearestNodeIndex);
                OnNodeRemoved?.Invoke(nearestNodeIndex);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 检查两个节点之间是否可以连�?
        /// </summary>
        /// <param name="nodeA">节点A索引</param>
        /// <param name="nodeB">节点B索引</param>
        /// <param name="coordinateTransform">坐标系变�?/param>
        /// <param name="maxDistance">可选的距离阈�?/param>
        /// <returns>是否可以连接</returns>
        public bool CanConnectNodes(int nodeA, int nodeB, Transform coordinateTransform = null, float maxDistance = float.MaxValue)
        {
            if (nodeA < 0 || nodeA >= targetShape.NodeCount ||
                nodeB < 0 || nodeB >= targetShape.NodeCount ||
                nodeA == nodeB)
                return false;

            // 检查边是否已存�?
            if (targetShape.HasEdge(nodeA, nodeB))
                return false;

            // 如果提供了坐标变换，检查距离阈值
            if (coordinateTransform != null)
            {
                Vector3 posA = LocalToWorldPosition(targetShape.Nodes[nodeA], coordinateTransform);
                Vector3 posB = LocalToWorldPosition(targetShape.Nodes[nodeB], coordinateTransform);
                float distance = Vector3.Distance(posA, posB);

                return distance <= maxDistance;
            }

            return true;
        }

        /// <summary>
        /// 获取构建统计信息
        /// </summary>
        /// <returns>统计信息字符�?/returns>
        public string GetBuildStats()
        {
            return $"Nodes: {targetShape.NodeCount}, Edges: {targetShape.EdgeCount}, " +
                   $"PreviousNode: {(HasPreviousNode ? previousNodeIndex.ToString() : "None")}";
        }

        /// <summary>
        /// 获取图的名称（只读代理）
        /// </summary>
        public string Name => targetShape.Name;

        /// <summary>
        /// 将图保存到文件（代理�?BrushGraph.SaveToFile�?
        /// </summary>
        public bool SaveToFile(string fileName = null)
        {
            return targetShape.SaveToFile(fileName);
        }

        /// <summary>
        /// 清空底层图数据并重置构建器状�?
        /// </summary>
        public void ClearGraph()
        {
            targetShape.Clear();
            Reset();
        }

        #endregion
    }
}
