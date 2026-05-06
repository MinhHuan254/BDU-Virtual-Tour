using UnityEngine;

public class WheelAnimator : MonoBehaviour
{
    [Header("Wheel Mesh Transforms")]
    public Transform wheelFL;
    public Transform wheelFR;
    public Transform wheelRL;
    public Transform wheelRR;

    [Header("Wheel Settings")]
    public float wheelRadius = 0.35f;      // bán kính bánh xe (m)
    public float spinSpeed = 1f;           // tốc độ quay bánh (1 = chuẩn)

    public bool rotateAroundLocalX = true; // nếu quay sai trục thì đổi false

    private Vector3 lastPosition;

    void Start()
    {
        // Lưu vị trí ban đầu của xe
        lastPosition = transform.position;
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        // ✅ Tính quãng đường xe đã di chuyển
        Vector3 delta = transform.position - lastPosition;
        float distance = delta.magnitude;

        // ✅ Nếu xe đứng yên thì KHÔNG quay bánh
        if (distance < 0.0005f)
        {
            lastPosition = transform.position;
            return;
        }

        // ✅ Tính chu vi bánh xe
        float circumference = 2f * Mathf.PI * wheelRadius;

        // ✅ Góc quay bánh
        float rotationDegrees = (distance / circumference) * 360f * spinSpeed;

        // ✅ Quay bánh xe (FR và RR quay ngược lại)
        RotateWheel(wheelFL, rotationDegrees, false);
        RotateWheel(wheelFR, rotationDegrees, true);  // ✅ đảo chiều
        RotateWheel(wheelRL, rotationDegrees, false);
        RotateWheel(wheelRR, rotationDegrees, true);  // ✅ đảo chiều

        // Cập nhật vị trí xe
        lastPosition = transform.position;
    }

    // ✅ Hàm quay bánh có thêm tham số đảo chiều
    void RotateWheel(Transform wheel, float degrees, bool invert)
    {
        if (wheel == null) return;

        // Nếu invert = true thì quay ngược lại
        float d = invert ? -degrees : degrees;

        if (rotateAroundLocalX)
        {
            wheel.Rotate(d, 0f, 0f, Space.Self);
        }
        else
        {
            wheel.Rotate(0f, 0f, -d, Space.Self);
        }
    }
}
