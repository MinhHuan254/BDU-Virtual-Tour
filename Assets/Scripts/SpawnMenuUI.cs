using UnityEngine;

public class SpawnMenuUI : MonoBehaviour
{
    public GameObject menuPanel;
    public Transform player;

    private bool isOpen = false;

    void Start()
    {
        if (menuPanel != null)
            menuPanel.SetActive(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
            ToggleMenu();
    }

    public void ToggleMenu()
    {
        isOpen = !isOpen;

        if (menuPanel != null)
            menuPanel.SetActive(isOpen);

        Cursor.lockState = isOpen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = isOpen;
    }

    public void TeleportToSpawn(Transform spawnPoint)
    {
        if (player == null || spawnPoint == null) return;

        CharacterController cc = player.GetComponent<CharacterController>();
        if (cc != null)
            cc.enabled = false;

        player.position = spawnPoint.position;
        player.rotation = spawnPoint.rotation;

        if (cc != null)
            cc.enabled = true;

        // Reset trigger UI sau khi teleport
        ResetAllTriggerUI();

        isOpen = false;

        if (menuPanel != null)
            menuPanel.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void ResetAllTriggerUI()
    {
        InfoPanelTriggerGroup[] infoGroups = FindObjectsOfType<InfoPanelTriggerGroup>(true);
        foreach (InfoPanelTriggerGroup group in infoGroups)
        {
            group.ForceReset();
        }

        CameraRoomTriggerGroup[] cameraGroups = FindObjectsOfType<CameraRoomTriggerGroup>(true);
        foreach (CameraRoomTriggerGroup group in cameraGroups)
        {
            group.ForceReset();
        }
    }
}