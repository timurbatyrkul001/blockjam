using UnityEngine;

namespace FruitMerge
{
    // iPhone çentiği / Dynamic Island ve alttaki home çubuğunun altına UI girmesin.
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        Rect applied;

        void Update()
        {
            var safe = Screen.safeArea;
            if (safe == applied || Screen.width == 0 || Screen.height == 0) return;
            applied = safe;

            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            rt.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
