using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SpawnButtonItem : MonoBehaviour
{
    public Text label;
    public TMP_Text tmpLabel;

    private Transform targetSpawn;
    private SpawnMenuUI spawnMenuUI;

    public void Setup(string displayName, Transform spawnPoint, SpawnMenuUI menu)
    {
        targetSpawn = spawnPoint;
        spawnMenuUI = menu;

        if (label != null)
            label.text = displayName;

        if (tmpLabel != null)
            tmpLabel.text = displayName;

        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(OnClickTeleport);
        }

        Debug.Log("Created spawn item: " + displayName);
    }

    void OnClickTeleport()
    {
        if (spawnMenuUI != null && targetSpawn != null)
        {
            spawnMenuUI.TeleportToSpawn(targetSpawn);
        }
    }
}