using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CarMoverTrigger : MonoBehaviour
{
    public Transform startPoint;
    public Transform endPoint;
    public float speed = 5f;
    public float rotateSpeed = 8f;
    public float stopDistance = 0.2f;

    private bool isMoving;

    void Start()
    {
        if (startPoint != null)
        {
            transform.position = startPoint.position;
            transform.rotation = startPoint.rotation;
        }
    }

    void Update()
    {
        // ✅ Nhấn phím số 1 để bắt đầu chạy
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            StartMove();
        }

        if (!isMoving || endPoint == null) return;

        Vector3 dir = endPoint.position - transform.position;
        dir.y = 0f;

        if (dir.magnitude <= stopDistance)
        {
            isMoving = false;
            Debug.Log("✅ Xe đã tới điểm cuối!");
            return;
        }

        Vector3 moveDir = dir.normalized;
        transform.position += moveDir * speed * Time.deltaTime;

        Quaternion desiredRot = Quaternion.LookRotation(moveDir, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, desiredRot, rotateSpeed * Time.deltaTime);
    }

    public void StartMove()
    {
        isMoving = true;
        Debug.Log("🚗 Xe bắt đầu chạy!");
    }
}
