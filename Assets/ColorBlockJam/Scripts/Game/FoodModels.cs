using System.Collections.Generic;
using UnityEngine;

namespace ColorBlockJam
{
    // Yemek teması: her blok rengi bir meyve modeline karşılık gelir (Assets/Art/Resources/Food).
    // Modeller farklı boyutlarda geldiği için hücreye sığacak şekilde otomatik ölçeklenir.
    public static class FoodModels
    {
        static readonly Dictionary<string, (GameObject prefab, Vector3 center, Vector3 size, float bottom)> cache = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => cache.Clear();

        public static string For(BlockColor c) => c switch
        {
            BlockColor.Red => "strawberry",
            BlockColor.Blue => "blueberry",
            BlockColor.Green => "kiwi",
            BlockColor.Yellow => "lemon",
            BlockColor.Purple => "grapes",
            BlockColor.Orange => "orange",
            BlockColor.Pink => "peach",
            _ => "macaron",   // turkuaz meyve olmadığı için tatlı
        };

        // Modeli parent'ın içine koyar: tabanı localPos'a oturur, yatayda en fazla `size` kadar yer kaplar.
        public static GameObject Spawn(BlockColor color, Transform parent, Vector3 localPos, float size, float yaw)
        {
            if (!TryGet(For(color), out var info)) return null;

            float footprint = Mathf.Max(info.size.x, info.size.z, info.size.y * 0.8f);
            float scale = footprint > 0f ? size / footprint : 1f;
            var yawRotation = Quaternion.Euler(0f, yaw, 0f);
            var pivotOffset = yawRotation * (new Vector3(info.center.x, info.bottom, info.center.z) * scale);

            // Modelin kendi kök dönüşü/ölçeği korunur (ör. Blender'dan gelen FBX'lerdeki eksen düzeltmesi).
            var root = info.prefab.transform;
            var go = Object.Instantiate(info.prefab, parent, false);
            go.name = info.prefab.name;
            go.transform.localRotation = yawRotation * root.localRotation;
            go.transform.localScale = root.localScale * scale;
            go.transform.localPosition = localPos - pivotOffset;
            foreach (var col in go.GetComponentsInChildren<Collider>()) Object.Destroy(col);
            return go;
        }

        static bool TryGet(string name, out (GameObject prefab, Vector3 center, Vector3 size, float bottom) info)
        {
            if (cache.TryGetValue(name, out info)) return info.prefab != null;

            var prefab = Resources.Load<GameObject>($"Food/{name}");
            info = (prefab, Vector3.zero, Vector3.one, 0f);
            if (prefab == null)
            {
                Debug.LogWarning($"[ColorBlockJam] Resources/Food/{name} bulunamadı");
                cache[name] = info;
                return false;
            }

            // Prefab sahnede olmadığı için Renderer.bounds kullanılamaz; mesh sınırlarını, modelin
            // kök dönüşü ve ölçeği dahil (sadece konumu hariç) tek bir uzayda birleştiriyoruz.
            var root = Matrix4x4.Translate(-prefab.transform.position);
            var bounds = new Bounds();
            bool any = false;
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var m = root * mf.transform.localToWorldMatrix;
                var b = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var p = m.MultiplyPoint3x4(corner);
                    if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; }
                    else bounds.Encapsulate(p);
                }
            }

            info = (prefab, bounds.center, bounds.size, bounds.min.y);
            cache[name] = info;
            return true;
        }
    }
}
