using UnityEngine;

public class RoomInfoInteract : MonoBehaviour
{
    public Camera playerCamera;
    public float cameraRayDistance = 50f;
    public GameObject infoCanvas;

    void Start()
    {
        if (infoCanvas != null)
            infoCanvas.SetActive(false);

        CloseAllCameraCanvases();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
            TryInteract();

        if (Input.GetKeyDown(KeyCode.Escape))
            CloseAllUI();
    }

    void TryInteract()
    {
        if (playerCamera == null)
        {
            Debug.LogWarning("Chua gan playerCamera");
            return;
        }

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        RaycastHit[] hits = Physics.RaycastAll(
            ray,
            cameraRayDistance,
            Physics.DefaultRaycastLayers,
            QueryTriggerInteraction.Ignore
        );

        if (hits.Length == 0) return;

        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        foreach (RaycastHit hit in hits)
        {
            CameraViewTarget cameraTarget = hit.collider.GetComponentInParent<CameraViewTarget>();
            if (cameraTarget != null)
            {
                if (cameraTarget.CanOpen())
                {
                    CloseAllCameraCanvases();
                    cameraTarget.OpenCanvas();
                }
                return;
            }
        }
    }

    void CloseAllCameraCanvases()
    {
        CameraViewTarget[] allTargets = FindObjectsOfType<CameraViewTarget>(true);
        foreach (CameraViewTarget target in allTargets)
        {
            target.CloseCanvas();
        }
    }

    void CloseAllUI()
    {
        if (infoCanvas != null)
            infoCanvas.SetActive(false);

        CloseAllCameraCanvases();

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}