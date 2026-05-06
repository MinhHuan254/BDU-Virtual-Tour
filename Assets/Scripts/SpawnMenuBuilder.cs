using System.Collections.Generic;
using UnityEngine;

public class SpawnMenuBuilder : MonoBehaviour
{
    public SpawnMenuUI spawnMenuUI;
    public Transform contentParent;
    public GameObject spawnButtonPrefab;
    public List<Transform> spawnPoints = new List<Transform>();

    void Start()
    {
        BuildMenu();
    }

    public void BuildMenu()
    {
        if (contentParent == null)
        {
            Debug.LogError("Content Parent chua duoc gan");
            return;
        }

        if (spawnButtonPrefab == null)
        {
            Debug.LogError("Spawn Button Prefab chua duoc gan");
            return;
        }

        if (spawnMenuUI == null)
        {
            Debug.LogError("SpawnMenuUI chua duoc gan");
            return;
        }

        foreach (Transform child in contentParent)
        {
            Destroy(child.gameObject);
        }

        foreach (Transform spawn in spawnPoints)
        {
            GameObject item = Instantiate(spawnButtonPrefab, contentParent);
            SpawnButtonItem buttonItem = item.GetComponent<SpawnButtonItem>();

            if (buttonItem != null)
            {
                buttonItem.Setup(spawn.name, spawn, spawnMenuUI);
            }
            else
            {
                Debug.LogError("Prefab khong co SpawnButtonItem");
            }
        }
    }
}