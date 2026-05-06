using System;
using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class CarMover : MonoBehaviour
{
    [Header("Waypoints for this car")]
    public Transform[] points;

    [Header("Move Settings")]
    public float speed = 12f;
    public float speedMultiplier = 1f;
    public float rotateSpeed = 10f;
    public float stopDistance = 0.2f;

    [Header("Smoothing")]
    public bool useSmoothMove = true;
    public float acceleration = 20f;
    public float decelDistance = 1.5f;
    public float minMoveSpeed = 1.5f;

    [Header("Parking Snap")]
    public bool snapOnStop = true;
    public float snapDuration = 0.35f;

    [Header("Spawn / Hide")]
    public bool hideOnStart = true;
    public float spawnYOffset = 0f;

    [Header("Physics Mode")]
    public bool forceKinematicIfRigidbodyExists = true;

    private int currentIndex = 1;
    private bool isMoving = false;
    private int direction = 1;
    private bool exiting = false;

    private Renderer[] renderers;
    private Collider[] colliders;
    private Rigidbody rb;

    [Header("Ground Stick")]
    public bool stickToGround = true;

    [Tooltip("LayerMask của ground. Nếu để 0 và không có layer 'Ground', script sẽ fallback sang Everything.")]
    public LayerMask groundMask;

    [Tooltip("Độ cao bắt đầu cast xuống.")]
    public float castHeight = 3f;

    [Tooltip("Bán kính sphere cast.")]
    public float castRadius = 0.45f;

    [Tooltip("Khoảng hở giữa bánh xe và ground.")]
    public float groundClearance = 0.02f;

    [Tooltip("Tốc độ kéo Y xuống ground (mượt).")]
    public float maxYSnapSpeed = 25f;

    [Tooltip("Nếu bật: snap Y xuống ground ngay lập tức (cứng), tắt: kéo mượt.")]
    public bool hardSnapToGround = false;

    [Tooltip("Fallback raycast nếu spherecast không hit.")]
    public bool useRaycastFallback = true;

    [Header("Slope Align (Nghiêng theo dốc)")]
    public bool alignToSlope = true;

    [Tooltip("Tốc độ nghiêng theo normal (càng cao càng bám nhanh).")]
    public float groundAlignSpeed = 8f;

    [Tooltip("Giới hạn góc dốc để cho phép nghiêng (độ). Quá góc này sẽ dùng Vector3.up.")]
    public float maxSlopeAngle = 55f;

    private float currentSpeed = 0f;
    private bool snapping = false;
    private float bottomOffsetFixed = 0.0f;

    // Lưu normal gần nhất để dùng cho rotation
    private Vector3 lastGroundNormal = Vector3.up;

    public bool IsMoving => isMoving;
    public event Action<CarMover> OnExitFinished;

    void Awake()
    {
        renderers = GetComponentsInChildren<Renderer>(true);
        colliders = GetComponentsInChildren<Collider>(true);
        rb = GetComponent<Rigidbody>();

        // Auto ground layer
        if (groundMask == 0)
        {
            int groundLayer = LayerMask.NameToLayer("Ground");
            if (groundLayer != -1) groundMask = 1 << groundLayer;
            else groundMask = ~0; // Everything
        }

        bottomOffsetFixed = ComputeBottomOffset();

        if (rb != null)
        {
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            if (forceKinematicIfRigidbodyExists)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            }
        }
    }

    void Start()
    {
        if (hideOnStart)
            SetVisible(false);
    }

    void FixedUpdate()
    {
        if (rb == null) return;
        StepMove(Time.fixedDeltaTime, useRigidbody: true);
    }

    void Update()
    {
        if (rb != null) return;
        StepMove(Time.deltaTime, useRigidbody: false);
    }

    private void StepMove(float dt, bool useRigidbody)
    {
        if (!isMoving || snapping) return;
        if (points == null || points.Length < 2) return;

        if (currentIndex < 0 || currentIndex >= points.Length)
        {
            StopInternal();
            return;
        }

        Transform wp = points[currentIndex];

        Vector3 pos = useRigidbody ? rb.position : transform.position;

        Vector3 toTarget = wp.position - pos;
        Vector3 flatDir = new Vector3(toTarget.x, 0f, toTarget.z);

        float dist = flatDir.magnitude;

        if (dist <= stopDistance)
        {
            currentIndex += direction;

            if (direction == -1 && currentIndex < 0)
            {
                FinishExitAndHide();
                return;
            }

            if (direction == 1 && currentIndex >= points.Length)
            {
                if (snapOnStop)
                    StartCoroutine(SnapToParkPose(points[points.Length - 1], useRigidbody));
                else
                    StopInternal();

                return;
            }

            return;
        }

        Vector3 moveDir = flatDir.normalized;

        float targetSpeed = speed * Mathf.Max(0.01f, speedMultiplier);

        if (useSmoothMove && dist < decelDistance)
        {
            float t = Mathf.Clamp01(dist / decelDistance);
            targetSpeed = Mathf.Lerp(minMoveSpeed, targetSpeed, t);
        }

        currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, acceleration * dt);

        Vector3 nextPos = pos + moveDir * currentSpeed * dt;

        // --- Stick to ground + lấy normal ---
        if (stickToGround)
        {
            Vector3 groundN;
            nextPos = StickToGround(nextPos, pos.y, out groundN);
            lastGroundNormal = groundN;
        }
        else
        {
            lastGroundNormal = Vector3.up;
        }

        // --- Rotation ---
        Quaternion currentRot = useRigidbody ? rb.rotation : transform.rotation;
        Quaternion targetRot;

        if (alignToSlope && stickToGround)
        {
            Vector3 up = lastGroundNormal;

            // Nếu dốc quá gắt -> bỏ nghiêng
            float slopeAngle = Vector3.Angle(up, Vector3.up);
            if (slopeAngle > maxSlopeAngle) up = Vector3.up;

            // Forward phải nằm trên mặt phẳng slope
            Vector3 forwardOnSlope = Vector3.ProjectOnPlane(moveDir, up);
            if (forwardOnSlope.sqrMagnitude < 0.0001f)
                forwardOnSlope = moveDir;

            targetRot = Quaternion.LookRotation(forwardOnSlope.normalized, up);

            // mượt nghiêng theo dốc (groundAlignSpeed)
            float rotLerp = Mathf.Max(rotateSpeed, groundAlignSpeed);
            targetRot = Quaternion.Slerp(currentRot, targetRot, rotLerp * dt);
        }
        else
        {
            targetRot = Quaternion.Slerp(
                currentRot,
                Quaternion.LookRotation(moveDir, Vector3.up),
                rotateSpeed * dt
            );
        }

        if (useRigidbody)
        {
            rb.MovePosition(nextPos);
            rb.MoveRotation(targetRot);
        }
        else
        {
            transform.position = nextPos;
            transform.rotation = targetRot;
        }
    }

    // ================= PUBLIC API =================

    public void ResetToStartHidden()
    {
        if (points == null || points.Length < 1) return;

        snapping = false;
        isMoving = false;
        exiting = false;
        currentSpeed = 0f;

        Vector3 p = points[0].position + Vector3.up * spawnYOffset;

        if (stickToGround)
        {
            Vector3 n;
            p = StickToGround(p, p.y, out n);
            lastGroundNormal = n;
        }

        if (rb != null)
        {
            rb.MovePosition(p);
            rb.MoveRotation(points[0].rotation);
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        else
        {
            transform.position = p;
            transform.rotation = points[0].rotation;
        }

        SetVisible(false);
    }

    public void StartFromBeginning()
    {
        if (points == null || points.Length < 2) return;

        snapping = false;
        exiting = false;
        direction = 1;

        SetVisible(true);

        Vector3 p = points[0].position + Vector3.up * spawnYOffset;

        if (stickToGround)
        {
            Vector3 n;
            p = StickToGround(p, p.y, out n);
            lastGroundNormal = n;
        }

        if (rb != null)
        {
            rb.MovePosition(p);
            rb.MoveRotation(points[0].rotation);
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        else
        {
            transform.position = p;
            transform.rotation = points[0].rotation;
        }

        currentIndex = 1;
        currentSpeed = 0f;
        isMoving = true;
    }

    public void ExitToBeginningAndHide()
    {
        if (points == null || points.Length < 2) return;

        snapping = false;
        exiting = true;
        direction = -1;

        SetVisible(true);

        int last = points.Length - 1;

        Vector3 p = points[last].position;

        if (stickToGround)
        {
            Vector3 n;
            p = StickToGround(p, p.y, out n);
            lastGroundNormal = n;
        }

        if (rb != null)
        {
            rb.MovePosition(p);
            rb.MoveRotation(points[last].rotation);
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        else
        {
            transform.position = p;
            transform.rotation = points[last].rotation;
        }

        currentIndex = last - 1;
        currentSpeed = 0f;
        isMoving = true;
    }

    public void HideCar()
    {
        snapping = false;
        isMoving = false;
        exiting = false;
        currentSpeed = 0f;
        SetVisible(false);
    }

    public void TeleportToEnd()
    {
        if (points == null || points.Length == 0) return;

        snapping = false;
        isMoving = false;
        exiting = false;
        currentSpeed = 0f;

        Transform park = points[points.Length - 1];

        Vector3 p = park.position;

        if (stickToGround)
        {
            Vector3 n;
            p = StickToGround(p, p.y, out n);
            lastGroundNormal = n;
        }

        if (rb != null)
        {
            rb.MovePosition(p);
            rb.MoveRotation(park.rotation);
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        else
        {
            transform.position = p;
            transform.rotation = park.rotation;
        }

        SetVisible(true);
    }

    // ================= INTERNAL =================

    private void FinishExitAndHide()
    {
        SetVisible(false);

        bool wasExit = exiting;
        snapping = false;
        isMoving = false;
        exiting = false;
        currentSpeed = 0f;

        if (wasExit)
            OnExitFinished?.Invoke(this);
    }

    private void StopInternal()
    {
        snapping = false;
        isMoving = false;
        exiting = false;
        currentSpeed = 0f;
    }

    private void SetVisible(bool visible)
    {
        foreach (var r in renderers) if (r) r.enabled = visible;
        foreach (var c in colliders) if (c) c.enabled = visible;
    }

    IEnumerator SnapToParkPose(Transform parkPoint, bool useRigidbody)
    {
        snapping = true;

        Vector3 startPos = useRigidbody && rb != null ? rb.position : transform.position;
        Quaternion startRot = useRigidbody && rb != null ? rb.rotation : transform.rotation;

        Vector3 endPos = parkPoint.position;

        Vector3 endN = Vector3.up;
        if (stickToGround)
            endPos = StickToGround(endPos, startPos.y, out endN);

        Quaternion endRot = parkPoint.rotation;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.0001f, snapDuration);

            Vector3 p = Vector3.Lerp(startPos, endPos, t);
            Quaternion q = Quaternion.Slerp(startRot, endRot, t);

            if (useRigidbody && rb != null)
            {
                rb.MovePosition(p);
                rb.MoveRotation(q);
            }
            else
            {
                transform.position = p;
                transform.rotation = q;
            }

            yield return null;
        }

        snapping = false;
        StopInternal();
    }

    private float ComputeBottomOffset()
    {
        if (colliders == null || colliders.Length == 0) return 0f;

        float minY = float.MaxValue;
        foreach (var col in colliders)
        {
            if (!col) continue;
            minY = Mathf.Min(minY, col.bounds.min.y);
        }
        if (minY == float.MaxValue) return 0f;

        return transform.position.y - minY;
    }

    private Vector3 StickToGround(Vector3 desiredPos, float fallbackY, out Vector3 groundNormal)
    {
        groundNormal = Vector3.up;

        RaycastHit hit;
        Vector3 origin = desiredPos + Vector3.up * castHeight;

        bool hasHit = Physics.SphereCast(
            origin, castRadius, Vector3.down, out hit,
            castHeight * 2f, groundMask, QueryTriggerInteraction.Ignore
        );

        if (!hasHit && useRaycastFallback)
        {
            hasHit = Physics.Raycast(
                origin, Vector3.down, out hit,
                castHeight * 2f, groundMask, QueryTriggerInteraction.Ignore
            );
        }

        if (hasHit)
        {
            groundNormal = hit.normal;

            float targetY = hit.point.y + bottomOffsetFixed + groundClearance;

            if (hardSnapToGround)
                desiredPos.y = targetY;
            else
                desiredPos.y = Mathf.MoveTowards(fallbackY, targetY, maxYSnapSpeed * Time.deltaTime);

            return desiredPos;
        }

        // không hit ground => giữ Y hiện tại để tránh bay
        desiredPos.y = fallbackY;
        groundNormal = Vector3.up;
        return desiredPos;
    }
}