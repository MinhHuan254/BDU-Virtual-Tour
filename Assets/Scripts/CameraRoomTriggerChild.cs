using UnityEngine;

public class CameraRoomTriggerChild : MonoBehaviour
{
    public CameraRoomTriggerGroup group;

    private void OnTriggerEnter(Collider other)
    {
        if (group != null)
            group.NotifyEnter(other);
    }

    private void OnTriggerExit(Collider other)
    {
        if (group != null)
            group.NotifyExit(other);
    }
}