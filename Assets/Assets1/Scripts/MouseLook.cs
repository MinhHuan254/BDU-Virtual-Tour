using UnityEngine;
using UnityEngine.EventSystems;

public class MouseLook : MonoBehaviour
{
    [Header("References")]
    public Transform playerBody;

    [Header("Settings")]
    public float mouseSensitivity = 200f;
    public float touchSensitivity = 0.2f;

    private float xRotation = 0f;

    void Start()
    {
#if UNITY_STANDALONE || UNITY_EDITOR
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
#endif
    }

    void Update()
    {
        // Nếu đang có touch thì chỉ xử lý touch, không xử lý mouse
        if (Input.touchCount > 0)
        {
            HandleTouchLook();
            return;
        }

        // PC: xoay bình thường bằng chuột
        HandleMouseLook();
    }

    void HandleMouseLook()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        RotateCamera(mouseX, mouseY);
    }

    void HandleTouchLook()
    {
        Touch touch = Input.GetTouch(0);

        // Nếu touch đang ở trên UI thì bỏ qua hoàn toàn
        if (EventSystem.current != null &&
            EventSystem.current.IsPointerOverGameObject(touch.fingerId))
        {
            return;
        }

        // Chỉ xoay khi vuốt
        if (touch.phase == TouchPhase.Moved)
        {
            float touchX = touch.deltaPosition.x * touchSensitivity;
            float touchY = touch.deltaPosition.y * touchSensitivity;

            RotateCamera(touchX, touchY);
        }
    }

    void RotateCamera(float deltaX, float deltaY)
    {
        xRotation -= deltaY;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        playerBody.Rotate(Vector3.up * deltaX);
    }
}