using UnityEngine;

public class CameraRoomTriggerGroup : MonoBehaviour
{
    public CameraViewTarget targetCamera;
    private int insideCount = 0;

    public void NotifyEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        insideCount++;
    }

    public void NotifyExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        insideCount = Mathf.Max(insideCount - 1, 0);

        if (insideCount == 0 && targetCamera != null)
        {
            targetCamera.CloseCanvas();
        }
    }

    public bool IsPlayerInside()
    {
        return insideCount > 0;
    }

    public void ForceReset()
    {
        insideCount = 0;

        if (targetCamera != null)
            targetCamera.CloseCanvas();
    }
}