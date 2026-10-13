using UnityEngine;

namespace ColorBlockJam
{
    public static class Shapes3D
    {
        public static GameObject Cube(Transform parent, Vector3 localPos, Vector3 scale, Color color,
            bool keepCollider = false, float smoothness = 0.5f, string name = "Cube") =>
            Primitive(PrimitiveType.Cube, parent, localPos, scale, color, keepCollider, smoothness, name);

        // Unity silindiri 2 birim yüksektir: scale.y = 0.06 → 0.12 yükseklik.
        public static GameObject Cylinder(Transform parent, Vector3 localPos, Vector3 scale, Color color,
            bool keepCollider = false, float smoothness = 0.5f, string name = "Cylinder") =>
            Primitive(PrimitiveType.Cylinder, parent, localPos, scale, color, keepCollider, smoothness, name);

        static GameObject Primitive(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Color color,
            bool keepCollider, float smoothness, string name)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Palette.Mat(color, smoothness);
            var collider = go.GetComponent<Collider>();
            if (!keepCollider && collider != null) Object.Destroy(collider);
            return go;
        }
    }
}
