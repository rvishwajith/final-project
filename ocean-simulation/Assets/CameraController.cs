using UnityEngine;

[AddComponentMenu("Camera/Click-Drag Rotate")]
public class CameraController : MonoBehaviour
{
    [Header("Mouse Settings")]
    [Tooltip("Which mouse button to hold for rotation (0 = left, 1 = right, 2 = middle)")]
    public int mouseButton = 0;
    [Tooltip("Speed multiplier for rotation")]
    public float rotationSpeed = 200f;
    [Tooltip("Minimum vertical angle (degrees)")]
    public float minPitch = -80f;
    [Tooltip("Maximum vertical angle (degrees)")]
    public float maxPitch = 80f;

    private float yaw;
    private float pitch;
    private bool isDragging;
    private Vector3 lastMousePos;

    void Start()
    {
        var angles = transform.rotation.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;
    }

    void Update()
    {
        // Begin drag
        if (Input.GetMouseButtonDown(mouseButton))
        {
            isDragging = true;
            lastMousePos = Input.mousePosition;
        }

        // End drag
        if (Input.GetMouseButtonUp(mouseButton))
        {
            isDragging = false;
        }

        if (isDragging)
        {
            Vector3 delta = Input.mousePosition - lastMousePos;
            lastMousePos = Input.mousePosition;

            // Convert pixel delta to degrees
            yaw += delta.x * rotationSpeed * Time.deltaTime;
            pitch -= delta.y * rotationSpeed * Time.deltaTime;
            pitch = Mathf.Clamp(pitch, minPitch, maxPitch);

            // Apply rotation
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
    }
}
