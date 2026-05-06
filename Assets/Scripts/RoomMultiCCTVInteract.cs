using UnityEngine;
using TMPro;

public class RoomMultiCCTVInteract : MonoBehaviour
{
    [System.Serializable]
    public class RoomCameraSource
    {
        public string cameraName = "Camera";

        [TextArea]
        public string rtspUrl = "";

        public int width = 1280;
        public int height = 720;
        public int fps = 5;
    }

    [Header("Interaction")]
    public string playerTag = "Player";
    public KeyCode interactKey = KeyCode.E;
    public bool turnOffWhenExit = true;

    [Header("Cursor")]
    public bool unlockCursorWhenOpen = true;
    public bool lockCursorWhenClose = true;

    [Header("UI")]
    public GameObject cameraPanel;
    public GameObject videoScreenRoot;

    [Header("Optional UI Text")]
    public TMP_Text cameraTitleText;
    public TMP_Text cameraHintText;

    [Header("Frame Readers")]
    public FrameReader1[] frameReaders;

    [Header("Room Cameras")]
    public RoomCameraSource[] cameras;

    private bool inRange = false;
    private bool isOn = false;

    private CursorLockMode prevLock;
    private bool prevVisible;

    private void Start()
    {
        if (cameraPanel != null)
            cameraPanel.SetActive(false);

        if (videoScreenRoot != null)
            videoScreenRoot.SetActive(false);

        RefreshCameraTexts();
    }

    private void Update()
    {
        if (inRange && Input.GetKeyDown(interactKey))
        {
            if (isOn)
                TurnOff();
            else
                TurnOn();
        }
    }

    private void TurnOn()
    {
        if (cameras == null || cameras.Length == 0)
        {
            Debug.LogWarning("[RoomMultiCCTVInteract] Chua gan camera nao trong Room Cameras.");
            return;
        }

        if (frameReaders == null || frameReaders.Length == 0)
        {
            Debug.LogWarning("[RoomMultiCCTVInteract] Chua gan FrameReaders.");
            return;
        }

        isOn = true;

        if (cameraPanel != null)
            cameraPanel.SetActive(true);

        if (videoScreenRoot != null)
            videoScreenRoot.SetActive(true);

        UnlockCursor();
        ApplyAllCameras();
        RefreshCameraTexts();
    }

    private void TurnOff()
    {
        isOn = false;

        if (cameraPanel != null)
            cameraPanel.SetActive(false);

        if (videoScreenRoot != null)
            videoScreenRoot.SetActive(false);

        if (frameReaders != null)
        {
            for (int i = 0; i < frameReaders.Length; i++)
            {
                if (frameReaders[i] != null)
                    frameReaders[i].StopStream();
            }
        }

        RestoreCursor();
    }

    private void ApplyAllCameras()
    {
        int readerCount = frameReaders != null ? frameReaders.Length : 0;
        int cameraCount = cameras != null ? cameras.Length : 0;

        for (int i = 0; i < readerCount; i++)
        {
            if (frameReaders[i] == null)
                continue;

            GameObject readerObj = frameReaders[i].gameObject;

            if (i < cameraCount && cameras[i] != null && !string.IsNullOrWhiteSpace(cameras[i].rtspUrl))
            {
                readerObj.SetActive(true);

                frameReaders[i].SetSource(
                    cameras[i].rtspUrl,
                    cameras[i].width,
                    cameras[i].height,
                    cameras[i].fps
                );

                Debug.Log("[Room CCTV] Slot " + i +
                          " | Camera: " + cameras[i].cameraName +
                          " | RTSP: " + cameras[i].rtspUrl);
            }
            else
            {
                readerObj.SetActive(false);
            }
        }
    }

    private void RefreshCameraTexts()
    {
        if (cameraTitleText != null)
        {
            if (cameras != null && cameras.Length > 0)
                cameraTitleText.text = "CAMERA PHONG";
            else
                cameraTitleText.text = "KHONG CO CAMERA";
        }

        if (cameraHintText != null)
            cameraHintText.text = "";
    }

    private void UnlockCursor()
    {
        prevLock = Cursor.lockState;
        prevVisible = Cursor.visible;

        if (unlockCursorWhenOpen)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    private void RestoreCursor()
    {
        if (lockCursorWhenClose)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        else
        {
            Cursor.lockState = prevLock;
            Cursor.visible = prevVisible;
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

            if (turnOffWhenExit)
                TurnOff();
        }
    }
}