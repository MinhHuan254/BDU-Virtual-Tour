using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float moveSpeed = 5f; // Tốc độ di chuyển
    public float sensitivity = 2f; // Độ nhạy chuột
    public Transform playerCamera; // Camera của nhân vật

    private float rotationX = 0f;
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked; // Khóa chuột vào trung tâm
        Cursor.visible = false; // Ẩn chuột
    }
    void Update()
    {
        // Di chuyển nhân vật
        float moveX = Input.GetAxis("Horizontal") * moveSpeed * Time.deltaTime;
        float moveZ = Input.GetAxis("Vertical") * moveSpeed * Time.deltaTime;

        // Di chuyển nhân vật theo trục X và Z
        transform.Translate(moveX, 0, moveZ);

        // Xoay nhân vật bằng chuột
        float mouseX = Input.GetAxis("Mouse X") * sensitivity;
        float mouseY = -Input.GetAxis("Mouse Y") * sensitivity;

        // Xoay theo trục Y cho thân nhân vật (xoay qua lại)
        transform.Rotate(0, mouseX, 0);

        // Điều chỉnh góc nhìn theo trục X (lên/xuống)
        rotationX += mouseY;
        rotationX = Mathf.Clamp(rotationX, -90f, 90f); // Giới hạn góc nhìn lên/xuống
        playerCamera.localRotation = Quaternion.Euler(rotationX, 0, 0);
    }
}
