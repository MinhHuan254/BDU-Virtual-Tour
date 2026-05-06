using UnityEngine;

public class InfoPanelTriggerChild : MonoBehaviour
{
    public InfoPanelTriggerGroup group;

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