using System.Collections.Generic;
using UnityEngine;

public class InfoPanelTriggerGroup : MonoBehaviour
{
    public GameObject infoPanel;

    // Mỗi player root sẽ có số trigger đang overlap
    private readonly Dictionary<Transform, int> overlapCounts = new Dictionary<Transform, int>();

    private void Start()
    {
        if (infoPanel != null)
            infoPanel.SetActive(false);
    }

    public void NotifyEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        Transform playerRoot = other.transform.root;

        if (overlapCounts.ContainsKey(playerRoot))
            overlapCounts[playerRoot]++;
        else
            overlapCounts[playerRoot] = 1;

        UpdatePanelState();
    }

    public void NotifyExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        Transform playerRoot = other.transform.root;

        if (overlapCounts.ContainsKey(playerRoot))
        {
            overlapCounts[playerRoot]--;

            if (overlapCounts[playerRoot] <= 0)
                overlapCounts.Remove(playerRoot);
        }

        UpdatePanelState();
    }

    private void UpdatePanelState()
    {
        if (infoPanel != null)
            infoPanel.SetActive(overlapCounts.Count > 0);
    }

    public void ForceReset()
    {
        overlapCounts.Clear();

        if (infoPanel != null)
            infoPanel.SetActive(false);
    }
}