using UnityEngine;
using UnityEngine.InputSystem;

public class Block : MonoBehaviour
{
    public Color color = Color.red;

    bool dragging;
    Vector3 offset;

    void Start()
    {
        GetComponent<Renderer>().material.color = color;
    }

    void Update()
    {
        var pointer = Pointer.current;
        if (pointer == null) return;

        Vector2 screenPos = pointer.position.ReadValue();
        Vector3 point = PointerOnTable(screenPos);

        bool pressed = pointer.press.wasPressedThisFrame;
        if (pressed && IsPointerOnMe(screenPos))
        {
            dragging = true;
            offset = transform.position - point;
        }

        if (pointer.press.wasReleasedThisFrame)
            dragging = false;

        if (dragging)
            transform.position = point + offset;
    }

    bool IsPointerOnMe(Vector2 screenPos)
    {
        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        if (!Physics.Raycast(ray, out RaycastHit hit))
            return false;
        return hit.transform == transform;
    }

    Vector3 PointerOnTable(Vector2 screenPos)
    {
        Ray ray = Camera.main.ScreenPointToRay(screenPos);
        Plane table = new Plane(Vector3.up, transform.position);
        table.Raycast(ray, out float distance);
        return ray.GetPoint(distance);
    }
}
