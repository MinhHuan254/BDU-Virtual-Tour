using UnityEngine;
using UnityEngine.Video;
using TMPro;

public class HLSCCTVInteract : MonoBehaviour
{
    [Header("Interaction")]
    public string playerTag = "Player";
    public KeyCode interactKey = KeyCode.E;
    public bool turnOffWhenExit = true;

    [Header("Cursor Control")]
    public bool unlockCursorWhenPanelOpen = true;
    public bool lockCursorWhenPanelClose = true;

    [Header("UI Root")]
    public GameObject cameraPanel;

    [Header("Video UI")]
    public GameObject videoScreenRoot;
    public RenderTexture targetTexture;
    public bool autoPlayVideoWhenPanelOpen = false;

    [Header("Parking Info UI")]
    public TMP_Text titleText;
    public TMP_Text statusText;
    public TMP_Text totalLabelText;
    public TMP_Text totalValueText;
    public TMP_Text occupiedLabelText;
    public TMP_Text occupiedValueText;
    public TMP_Text availableLabelText;
    public TMP_Text availableValueText;

    [Header("19 Slot Boxes")]
    public TMP_Text[] slotBoxTexts = new TMP_Text[19];

    [Header("Slot Colors")]
    public Color occupiedSlotColor = Color.white;
    public Color emptySlotColor = Color.white;

    [Header("Parking Data")]
    public ParkingSimFixedSlotMapping parkingSim;

    [Header("Stream URL")]
    public string url = "http://127.0.0.1:8080/live.mjpg";

    private VideoPlayer vp;
    private bool inRange = false;
    private bool isOn = false;
    private bool isVideoVisible = false;

    private CursorLockMode previousCursorLockMode;
    private bool previousCursorVisible;

    private void Awake()
    {
        vp = GetComponent<VideoPlayer>();
        if (vp == null)
            vp = gameObject.AddComponent<VideoPlayer>();

        vp.source = VideoSource.Url;
        vp.renderMode = VideoRenderMode.RenderTexture;
        vp.playOnAwake = false;
        vp.isLooping = true;
        vp.waitForFirstFrame = true;
        vp.skipOnDrop = true;
        vp.audioOutputMode = VideoAudioOutputMode.None;
        vp.errorReceived += OnVideoError;

        if (targetTexture == null)
            Debug.LogError("[CCTV] targetTexture chưa được gán.");

        vp.targetTexture = targetTexture;

        if (parkingSim == null)
            parkingSim = FindFirstObjectByType<ParkingSimFixedSlotMapping>();

        if (parkingSim != null)
            parkingSim.OnParkingStatsChanged += HandleParkingStatsChanged;
    }

    private void Start()
    {
        if (cameraPanel != null)
            cameraPanel.SetActive(false);

        if (videoScreenRoot != null)
            videoScreenRoot.SetActive(false);

        SetupStaticTexts();
        RefreshParkingInfo();
        RefreshSlotBoxes();
    }

    private void OnDestroy()
    {
        if (parkingSim != null)
            parkingSim.OnParkingStatsChanged -= HandleParkingStatsChanged;

        if (vp != null)
            vp.errorReceived -= OnVideoError;
    }

    private void Update()
    {
        if (inRange && Input.GetKeyDown(interactKey))
        {
            if (isOn) TurnOff();
            else TurnOn();
        }
    }

    private void TurnOn()
    {
        isOn = true;

        if (cameraPanel != null)
            cameraPanel.SetActive(true);

        UnlockCursorForUI();
        RefreshParkingInfo();
        RefreshSlotBoxes();

        if (autoPlayVideoWhenPanelOpen)
            ShowVideoScreen();
        else
            HideVideoScreen();
    }

    private void TurnOff()
    {
        isOn = false;
        HideVideoScreen();

        if (cameraPanel != null)
            cameraPanel.SetActive(false);

        RestoreCursorAfterUI();
    }

    private void UnlockCursorForUI()
    {
        previousCursorLockMode = Cursor.lockState;
        previousCursorVisible = Cursor.visible;

        if (unlockCursorWhenPanelOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void RestoreCursorAfterUI()
    {
        if (lockCursorWhenPanelClose)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = previousCursorLockMode;
            Cursor.visible = previousCursorVisible;
        }
    }

    public void ToggleVideoScreen()
    {
        if (!isOn) return;

        if (isVideoVisible) HideVideoScreen();
        else ShowVideoScreen();
    }

    public void ShowVideoScreen()
    {
        if (!isOn) return;

        isVideoVisible = true;

        if (videoScreenRoot != null)
            videoScreenRoot.SetActive(true);

        if (vp != null)
        {
            vp.Stop();
            vp.url = url;
            vp.Play();
        }
    }

    public void HideVideoScreen()
    {
        isVideoVisible = false;

        if (videoScreenRoot != null)
            videoScreenRoot.SetActive(false);

        if (vp != null)
            vp.Stop();
    }

    private void OnVideoError(VideoPlayer source, string message)
    {
        Debug.LogError("[CCTV] VideoPlayer error: " + message + " | URL: " + url);
    }

    private void SetupStaticTexts()
    {
        if (titleText != null) titleText.text = "THÔNG TIN BÃI XE";
        if (totalLabelText != null) totalLabelText.text = "Tổng slot";
        if (occupiedLabelText != null) occupiedLabelText.text = "Đã chiếm";
        if (availableLabelText != null) availableLabelText.text = "Còn trống";
    }

    private void HandleParkingStatsChanged(int total, int occupied, int available)
    {
        RefreshParkingInfo();
        RefreshSlotBoxes();
    }

    private void RefreshParkingInfo()
    {
        if (parkingSim == null)
        {
            if (statusText != null) statusText.text = "Không có dữ liệu";
            if (totalValueText != null) totalValueText.text = "--";
            if (occupiedValueText != null) occupiedValueText.text = "--";
            if (availableValueText != null) availableValueText.text = "--";
            return;
        }

        if (totalValueText != null) totalValueText.text = parkingSim.TotalSlots.ToString();
        if (occupiedValueText != null) occupiedValueText.text = parkingSim.OccupiedSlots.ToString();
        if (availableValueText != null) availableValueText.text = parkingSim.AvailableSlots.ToString();

        float ratio = parkingSim.TotalSlots > 0
            ? (float)parkingSim.OccupiedSlots / parkingSim.TotalSlots
            : 0f;

        if (statusText != null)
        {
            if (ratio >= 1f)
            {
                statusText.text = "Bãi xe đã đầy";
                statusText.color = new Color32(239, 68, 68, 255);
            }
            else if (ratio >= 0.8f)
            {
                statusText.text = "Bãi xe sắp đầy";
                statusText.color = new Color32(245, 158, 11, 255);
            }
            else
            {
                statusText.text = "Bãi xe thông thoáng";
                statusText.color = new Color32(34, 197, 94, 255);
            }
        }
    }

    private void RefreshSlotBoxes()
    {
        if (slotBoxTexts == null || slotBoxTexts.Length == 0)
            return;

        for (int i = 0; i < slotBoxTexts.Length; i++)
        {
            TMP_Text slotText = slotBoxTexts[i];
            if (slotText == null) continue;

            int slotNumber = i + 1;

            if (parkingSim == null)
            {
                slotText.text = $"Slot {slotNumber}\nTrống";
                slotText.color = emptySlotColor;
                continue;
            }

            slotText.text = parkingSim.GetSlotBoxText(slotNumber);
            slotText.color = parkingSim.IsSlotOccupied(slotNumber)
                ? occupiedSlotColor
                : emptySlotColor;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag))
            inRange = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag(playerTag))
        {
            inRange = false;
            if (turnOffWhenExit) TurnOff();
        }
    }
}