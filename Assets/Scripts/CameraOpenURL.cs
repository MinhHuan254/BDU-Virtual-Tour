using UnityEngine;
using TMPro;

public class CameraProximityOpenURL : MonoBehaviour
{
    [Header("Camera URL")]
    public string url = "http://192.168.88.118/";

    [Header("Interaction")]
    public KeyCode openKey = KeyCode.E;

    [Header("UI Prompt")]
    public GameObject promptUI; // Text: "Nhấn E để xem camera"

    [Header("Parking Info UI")]
    public GameObject parkingInfoPanel;   // panel chứa text thông tin bãi xe
    public TMP_Text parkingInfoText;      // text hiển thị số liệu

    [Header("Parking Data")]
    public ParkingSimFixedSlotMapping parkingSim;

    [Header("Options")]
    public bool openBrowserWhenPressed = true;
    public bool onlyOpenBrowserOnce = true;

    private bool playerInRange = false;
    private bool panelVisible = false;
    private bool hasOpenedUrlThisSession = false;

    void Start()
    {
        if (promptUI != null)
            promptUI.SetActive(false);

        if (parkingInfoPanel != null)
            parkingInfoPanel.SetActive(false);

        if (parkingSim == null)
            parkingSim = FindObjectOfType<ParkingSimFixedSlotMapping>();

        if (parkingSim != null)
            parkingSim.OnParkingStatsChanged += HandleParkingStatsChanged;
        else
            Debug.LogWarning("[CameraUI] Không tìm thấy ParkingSimFixedSlotMapping trong scene.");

        if (parkingInfoText == null)
            Debug.LogWarning("[CameraUI] parkingInfoText chưa được gán.");

        if (parkingInfoPanel == null)
            Debug.LogWarning("[CameraUI] parkingInfoPanel chưa được gán.");

        RefreshParkingInfo();
    }

    void OnDestroy()
    {
        if (parkingSim != null)
            parkingSim.OnParkingStatsChanged -= HandleParkingStatsChanged;
    }

    void Update()
    {
        if (!playerInRange)
            return;

        if (Input.GetKeyDown(openKey))
        {
            ToggleCameraAndInfo();
        }

        // Panel đang mở thì luôn refresh để chắc chắn text hiện đúng
        if (panelVisible)
        {
            RefreshParkingInfo();
        }
    }

    private void ToggleCameraAndInfo()
    {
        panelVisible = !panelVisible;

        if (parkingInfoPanel != null)
            parkingInfoPanel.SetActive(panelVisible);

        if (panelVisible && openBrowserWhenPressed)
        {
            if (!onlyOpenBrowserOnce || !hasOpenedUrlThisSession)
            {
                OpenCamera();
                hasOpenedUrlThisSession = true;
            }
        }

        RefreshParkingInfo();
    }

    private void OpenCamera()
    {
        if (!string.IsNullOrEmpty(url))
        {
            Debug.Log("[CameraUI] Open camera: " + url);
            Application.OpenURL(url);
        }
    }

    private void HandleParkingStatsChanged(int total, int occupied, int available)
    {
        RefreshParkingInfo();
    }

    private void RefreshParkingInfo()
    {
        if (parkingInfoText == null)
            return;

        if (parkingSim == null)
        {
            parkingInfoText.text =
                "THÔNG TIN BÃI XE\n" +
                "Không tìm thấy dữ liệu bãi xe.";
            return;
        }

        parkingInfoText.text =
            "THÔNG TIN BÃI XE\n" +
            $"Tổng slot: {parkingSim.TotalSlots}\n" +
            $"Đã chiếm: {parkingSim.OccupiedSlots}\n" +
            $"Còn trống: {parkingSim.AvailableSlots}";
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInRange = true;

        if (promptUI != null)
            promptUI.SetActive(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        playerInRange = false;

        if (promptUI != null)
            promptUI.SetActive(false);

        panelVisible = false;

        if (parkingInfoPanel != null)
            parkingInfoPanel.SetActive(false);
    }
}