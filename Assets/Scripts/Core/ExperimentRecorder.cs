using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Globalization;

namespace VRBrush.Core
{
    /// <summary>
    /// 实验记录器
    /// 用于实验2：记录绘制时间、撤销次数，导出OBJ和TOML文件
    /// </summary>
    public class ExperimentRecorder : MonoBehaviour
    {
        [Header("Recording Settings")]
        [Tooltip("笔画的父对象")]
        [SerializeField] private Transform strokesParent;

        [Tooltip("如果未指定strokesParent，则使用此名称查找")]
        [SerializeField] private string strokesParentName = "BrushStrokes";

        [Tooltip("导出目录")]
        [SerializeField] private string exportDirectory = "Assets/Works";

        [Header("References")]
        [SerializeField] private StrokeUndoManager strokeUndoManager;

        [Header("Auto Start Settings")]
        [Tooltip("是否在检测到第一笔时自动开始计时")]
        [SerializeField] private bool autoStartOnFirstStroke = true;

        // 计时相关
        private float startTime = 0f;
        private bool isRecording = false;
        private int lastKnownStrokeCount = 0;

        // 撤销计数（独立于StrokeUndoManager的计数，因为可能在启动前就有撤销）
        private int undoCount = 0;

        // 实验类型和笔刷信息
        private string experimentType = "Unknown";
        private string brushVariant = "Unknown";
        private string shapeName = "Unknown";

        /// <summary>
        /// 获取已记录的时间（秒）
        /// </summary>
        public float ElapsedTime => isRecording ? (Time.time - startTime) : 0f;

        /// <summary>
        /// 获取撤销次数
        /// </summary>
        public int UndoCount => undoCount;

        /// <summary>
        /// 是否正在记录
        /// </summary>
        public bool IsRecording => isRecording;

        /// <summary>
        /// 设置实验类型信息
        /// </summary>
        /// <param name="expType">实验类型，如 "Ex1" 或 "Ex2"</param>
        /// <param name="variant">笔刷变种，如 "V01_NoRoll_NoCustom"</param>
        /// <param name="shape">当前使用的形状名称</param>
        public void SetExperimentInfo(string expType, string variant, string shape)
        {
            experimentType = expType ?? "Unknown";
            brushVariant = variant ?? "Unknown";
            shapeName = shape ?? "Unknown";
            Debug.Log($"ExperimentRecorder: Set experiment info - Type: {experimentType}, Variant: {brushVariant}, Shape: {shapeName}");
        }

        private void Start()
        {
            // 确保导出目录存在
            if (!Directory.Exists(exportDirectory))
            {
                Directory.CreateDirectory(exportDirectory);
                Debug.Log($"ExperimentRecorder: Created export directory: {exportDirectory}");
            }

            // 查找strokesParent
            if (strokesParent == null)
            {
                FindStrokesParent();
            }

            // 查找StrokeUndoManager
            if (strokeUndoManager == null)
            {
                strokeUndoManager = FindFirstObjectByType<StrokeUndoManager>();
            }

            // 记录初始笔画数量（通常为0）
            if (strokesParent != null)
            {
                lastKnownStrokeCount = strokesParent.childCount;
            }
        }

        private void Update()
        {
            // 自动检测第一笔开始计时
            if (autoStartOnFirstStroke && !isRecording && strokesParent != null)
            {
                int currentCount = strokesParent.childCount;
                if (currentCount > lastKnownStrokeCount)
                {
                    // 检测到新笔画，自动开始记录
                    StartRecording();
                    Debug.Log("ExperimentRecorder: Auto-started recording on first stroke");
                }
            }
        }

        private void FindStrokesParent()
        {
            var vrBrushSystem = FindFirstObjectByType<VRBrushSystem>();
            if (vrBrushSystem != null)
            {
                var field = vrBrushSystem.GetType().GetField("strokesParent",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    strokesParent = field.GetValue(vrBrushSystem) as Transform;
                    if (strokesParent != null)
                    {
                        Debug.Log($"ExperimentRecorder: Found strokesParent from VRBrushSystem");
                        return;
                    }
                }
            }

            var found = GameObject.Find(strokesParentName);
            if (found != null)
            {
                strokesParent = found.transform;
                Debug.Log($"ExperimentRecorder: Found strokesParent by name");
            }
        }

        /// <summary>
        /// 开始记录
        /// </summary>
        public void StartRecording()
        {
            startTime = Time.time;
            isRecording = true;
            undoCount = 0;
            Debug.Log("ExperimentRecorder: Started recording");
        }

        /// <summary>
        /// 停止记录
        /// </summary>
        public void StopRecording()
        {
            isRecording = false;
            Debug.Log($"ExperimentRecorder: Stopped recording. Elapsed time: {ElapsedTime:F2}s, Undo count: {undoCount}");
        }

        /// <summary>
        /// 增加撤销计数
        /// </summary>
        public void IncrementUndoCount()
        {
            undoCount++;
            Debug.Log($"ExperimentRecorder: Undo count incremented to {undoCount}");
        }

        /// <summary>
        /// 导出所有内容（OBJ + TOML）
        /// </summary>
        public void ExportAll()
        {
            if (strokesParent == null)
            {
                FindStrokesParent();
                if (strokesParent == null)
                {
                    Debug.LogError("ExperimentRecorder: Cannot export - strokesParent not found");
                    return;
                }
            }

            // 生成时间戳文件名
            string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string objFileName = $"Drawing_{timestamp}.obj";
            string tomlFileName = $"Drawing_{timestamp}.toml";

            string objFilePath = Path.Combine(exportDirectory, objFileName);
            string tomlFilePath = Path.Combine(exportDirectory, tomlFileName);

            // 确保目录存在
            if (!Directory.Exists(exportDirectory))
            {
                Directory.CreateDirectory(exportDirectory);
            }

            // 导出OBJ
            bool objSuccess = ExportToOBJ(objFilePath);

            // 导出TOML
            bool tomlSuccess = ExportToTOML(tomlFilePath, objFileName);

            if (objSuccess && tomlSuccess)
            {
                Debug.Log($"ExperimentRecorder: Successfully exported to {exportDirectory}");
                Debug.Log($"  - OBJ: {objFileName}");
                Debug.Log($"  - TOML: {tomlFileName}");
            }
        }

        /// <summary>
        /// 导出所有笔画为OBJ文件
        /// </summary>
        private bool ExportToOBJ(string filePath)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# VRBrush Export");
                sb.AppendLine($"# Generated: {System.DateTime.Now}");
                sb.AppendLine();

                int vertexOffset = 0;
                int objectIndex = 0;

                for (int i = 0; i < strokesParent.childCount; i++)
                {
                    Transform child = strokesParent.GetChild(i);
                    MeshFilter meshFilter = child.GetComponent<MeshFilter>();
                    
                    if (meshFilter == null || meshFilter.sharedMesh == null)
                        continue;

                    Mesh mesh = meshFilter.sharedMesh;
                    if (mesh.vertexCount < 3)
                        continue;

                    sb.AppendLine($"o Stroke_{objectIndex}");

                    // 获取世界坐标变换
                    Matrix4x4 localToWorld = child.localToWorldMatrix;

                    // 写入顶点
                    Vector3[] vertices = mesh.vertices;
                    foreach (Vector3 v in vertices)
                    {
                        Vector3 worldV = localToWorld.MultiplyPoint3x4(v);
                        sb.AppendLine($"v {worldV.x.ToString("F6", CultureInfo.InvariantCulture)} {worldV.y.ToString("F6", CultureInfo.InvariantCulture)} {worldV.z.ToString("F6", CultureInfo.InvariantCulture)}");
                    }

                    // 写入法线（如果有）
                    Vector3[] normals = mesh.normals;
                    if (normals != null && normals.Length > 0)
                    {
                        foreach (Vector3 n in normals)
                        {
                            Vector3 worldN = localToWorld.MultiplyVector(n).normalized;
                            sb.AppendLine($"vn {worldN.x.ToString("F6", CultureInfo.InvariantCulture)} {worldN.y.ToString("F6", CultureInfo.InvariantCulture)} {worldN.z.ToString("F6", CultureInfo.InvariantCulture)}");
                        }
                    }

                    // 写入UV（如果有）
                    Vector2[] uvs = mesh.uv;
                    if (uvs != null && uvs.Length > 0)
                    {
                        foreach (Vector2 uv in uvs)
                        {
                            sb.AppendLine($"vt {uv.x.ToString("F6", CultureInfo.InvariantCulture)} {uv.y.ToString("F6", CultureInfo.InvariantCulture)}");
                        }
                    }

                    // 写入面
                    int[] triangles = mesh.triangles;
                    bool hasNormals = normals != null && normals.Length > 0;
                    bool hasUVs = uvs != null && uvs.Length > 0;

                    for (int j = 0; j < triangles.Length; j += 3)
                    {
                        int v1 = triangles[j] + 1 + vertexOffset;
                        int v2 = triangles[j + 1] + 1 + vertexOffset;
                        int v3 = triangles[j + 2] + 1 + vertexOffset;

                        if (hasNormals && hasUVs)
                        {
                            sb.AppendLine($"f {v1}/{v1}/{v1} {v2}/{v2}/{v2} {v3}/{v3}/{v3}");
                        }
                        else if (hasNormals)
                        {
                            sb.AppendLine($"f {v1}//{v1} {v2}//{v2} {v3}//{v3}");
                        }
                        else if (hasUVs)
                        {
                            sb.AppendLine($"f {v1}/{v1} {v2}/{v2} {v3}/{v3}");
                        }
                        else
                        {
                            sb.AppendLine($"f {v1} {v2} {v3}");
                        }
                    }

                    vertexOffset += vertices.Length;
                    objectIndex++;
                    sb.AppendLine();
                }

                File.WriteAllText(filePath, sb.ToString());
                Debug.Log($"ExperimentRecorder: Exported {objectIndex} strokes to OBJ");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"ExperimentRecorder: Failed to export OBJ - {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 导出实验信息为TOML文件
        /// </summary>
        private bool ExportToTOML(string filePath, string objFileName)
        {
            try
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("# VRBrush Experiment Record");
                sb.AppendLine($"# Generated: {System.DateTime.Now}");
                sb.AppendLine();

                sb.AppendLine("[experiment]");
                sb.AppendLine($"timestamp = \"{System.DateTime.Now:yyyy-MM-dd HH:mm:ss}\"");
                sb.AppendLine($"type = \"{experimentType}\"");
                sb.AppendLine($"brush_variant = \"{brushVariant}\"");
                sb.AppendLine($"elapsed_time_seconds = {ElapsedTime.ToString("F2", CultureInfo.InvariantCulture)}");
                sb.AppendLine($"undo_count = {undoCount}");
                sb.AppendLine();

                sb.AppendLine("[output]");
                sb.AppendLine($"obj_file = \"{objFileName}\"");
                sb.AppendLine();

                sb.AppendLine("[statistics]");
                int strokeCount = GetStrokeCount();
                int totalVertices = GetTotalVertexCount();
                int totalTriangles = GetTotalTriangleCount();
                sb.AppendLine($"stroke_count = {strokeCount}");
                sb.AppendLine($"total_vertices = {totalVertices}");
                sb.AppendLine($"total_triangles = {totalTriangles}");

                File.WriteAllText(filePath, sb.ToString());
                Debug.Log($"ExperimentRecorder: Exported experiment info to TOML");
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"ExperimentRecorder: Failed to export TOML - {e.Message}");
                return false;
            }
        }

        private int GetStrokeCount()
        {
            if (strokesParent == null) return 0;

            int count = 0;
            for (int i = 0; i < strokesParent.childCount; i++)
            {
                var meshFilter = strokesParent.GetChild(i).GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null && meshFilter.sharedMesh.vertexCount >= 3)
                {
                    count++;
                }
            }
            return count;
        }

        private int GetTotalVertexCount()
        {
            if (strokesParent == null) return 0;

            int count = 0;
            for (int i = 0; i < strokesParent.childCount; i++)
            {
                var meshFilter = strokesParent.GetChild(i).GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    count += meshFilter.sharedMesh.vertexCount;
                }
            }
            return count;
        }

        private int GetTotalTriangleCount()
        {
            if (strokesParent == null) return 0;

            int count = 0;
            for (int i = 0; i < strokesParent.childCount; i++)
            {
                var meshFilter = strokesParent.GetChild(i).GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    count += meshFilter.sharedMesh.triangles.Length / 3;
                }
            }
            return count;
        }

        /// <summary>
        /// 设置笔画父对象
        /// </summary>
        public void SetStrokesParent(Transform parent)
        {
            strokesParent = parent;
        }

        /// <summary>
        /// 重置记录
        /// </summary>
        public void ResetRecording()
        {
            startTime = Time.time;
            undoCount = 0;
            Debug.Log("ExperimentRecorder: Recording reset");
        }
    }
}
