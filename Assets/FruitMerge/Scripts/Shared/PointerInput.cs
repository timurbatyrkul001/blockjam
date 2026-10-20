using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FruitMerge
{
    // Fare (Editor) ve dokunmatik (iPhone) için tek bir giriş noktası.
    // Proje yeni Input System'i de eskisini de kullansa çalışır.
    public static class PointerInput
    {
#if ENABLE_INPUT_SYSTEM
        public static bool Pressed => Pointer.current != null && Pointer.current.press.wasPressedThisFrame;
        public static bool Held => Pointer.current != null && Pointer.current.press.isPressed;
        public static Vector2 Position => Pointer.current != null ? Pointer.current.position.ReadValue() : Vector2.zero;
#else
        public static bool Pressed => Input.GetMouseButtonDown(0);
        public static bool Held => Input.GetMouseButton(0);
        public static Vector2 Position => Input.mousePosition;
#endif
    }
}
