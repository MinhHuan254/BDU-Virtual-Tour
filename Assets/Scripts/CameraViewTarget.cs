using UnityEngine;

public class CameraViewTarget : MonoBehaviour
{
    public GameObject cameraUIRoot;
    public CameraRoomTriggerGroup roomTriggerGroup;
    public FrameReader1 roomFrameReader;

    public bool CanOpen()
    {
        return roomTriggerGroup != null && roomTriggerGroup.IsPlayerInside();
    }

    public void OpenCanvas()
    {
        if (cameraUIRoot == null) return;

        cameraUIRoot.SetActive(true);

        if (roomFrameReader != null)
            roomFrameReader.RestartStream();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void CloseCanvas()
    {
        if (roomFrameReader != null)
            roomFrameReader.StopStream();

        if (cameraUIRoot == null) return;

        cameraUIRoot.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}