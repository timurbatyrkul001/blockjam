using UnityEngine;
using UnityEngine.SceneManagement;

namespace BentoBlast
{
    // Masadaki süsler (Kenney Food Kit: Resources/Decor). Oyun alanına girmesin diye ekranın boş
    // kenarlarına, kameranın gördüğü yere göre yerleşir; ekran oranı değişince yeniden yerleşir.
    // Collider yok: parça tutmayı engellemez.
    public class BentoDecor : MonoBehaviour
    {
        // model, ekrandaki yeri (viewport 0..1), boyu (birim), yön (derece)
        static readonly (string model, Vector2 viewport, float size, float yaw)[] Items =
        {
            ("chopstick",    new Vector2(0.14f, 0.10f), 3.4f,  28f),
            ("chopstick",    new Vector2(0.20f, 0.09f), 3.4f,  34f),
            ("dim-sum",      new Vector2(0.58f, 0.06f), 1.3f,  10f),
            ("cup-tea",      new Vector2(0.86f, 0.11f), 1.6f, -20f),
            ("maki-salmon",  new Vector2(0.02f, 0.73f), 1.5f,  15f),
            ("sushi-salmon", new Vector2(0.98f, 0.72f), 1.7f, -25f),
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (SceneManager.GetActiveScene().name != "BentoBlast") return;
            if (FindAnyObjectByType<BentoDecor>() != null) return;
            new GameObject("Decor").AddComponent<BentoDecor>();
        }

        readonly Transform[] views = new Transform[Items.Length];
        Camera cam;
        float lastAspect = -1f;
        Vector3 lastCamPos;

        void Start()
        {
            for (int i = 0; i < Items.Length; i++)
                views[i] = Spawn(Items[i].model, Items[i].size, Items[i].yaw);
        }

        // Kamerayı BentoGame ayarlıyor; o değişince süsler de yer değiştirir.
        void LateUpdate()
        {
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            if (Mathf.Approximately(cam.aspect, lastAspect) && cam.transform.position == lastCamPos) return;
            lastAspect = cam.aspect;
            lastCamPos = cam.transform.position;

            var table = new Plane(Vector3.up, Vector3.up * -0.5f);
            for (int i = 0; i < Items.Length; i++)
            {
                if (views[i] == null) continue;
                var ray = cam.ViewportPointToRay(Items[i].viewport);
                if (table.Raycast(ray, out float d)) views[i].position = ray.GetPoint(d);
            }
        }

        // Modeli tabanı masaya oturacak ve en uzun kenarı `size` olacak şekilde koyar.
        Transform Spawn(string model, float size, float yaw)
        {
            var prefab = Resources.Load<GameObject>($"Decor/{model}");
            if (prefab == null) { Debug.LogWarning($"[BentoBlast] Resources/Decor/{model} yok"); return null; }

            var holder = new GameObject(model).transform;
            holder.SetParent(transform, false);
            var go = Instantiate(prefab, holder, false);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f) * prefab.transform.localRotation;
            foreach (var col in go.GetComponentsInChildren<Collider>()) Destroy(col);

            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return holder;
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            float longest = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (longest > 0f) go.transform.localScale *= size / longest;

            b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            go.transform.position += new Vector3(holder.position.x - b.center.x, holder.position.y - b.min.y, holder.position.z - b.center.z);
            return holder;
        }
    }
}
