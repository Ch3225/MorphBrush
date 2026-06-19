using UnityEngine;

namespace VRBrush.Core.Visual.Element
{
    /// <summary>
    /// Creates a flat disk visual (uses Cylinder primitive) and provides update methods.
    /// </summary>
    public static class DiskVisual
    {
        public static GameObject Create(Transform parent, string name, float radius, float thickness = 0.002f, Material material = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.localRotation = Quaternion.identity;
            UpdateScale(go.transform, radius, thickness);

            if (material != null)
            {
                var r = go.GetComponent<Renderer>();
                if (r != null) r.material = material;
            }

            var comp = go.AddComponent<DiskVisualComponent>();
            comp.Initialize(radius, thickness);
            return go;
        }

        private static void UpdateScale(Transform t, float radius, float thickness)
        {
            float planeScaleY = thickness; // half-height approx in our usage
            t.localScale = new Vector3(radius * 2f, planeScaleY, radius * 2f);
            t.localPosition = Vector3.zero;
        }
    }

    public class DiskVisualComponent : MonoBehaviour
    {
        private float radius;
        private float thickness;

        public void Initialize(float r, float t)
        {
            radius = r;
            thickness = t;
        }

        public void UpdateRadius(float r)
        {
            radius = r;
            transform.localScale = new Vector3(radius * 2f, thickness, radius * 2f);
        }
    }
}
