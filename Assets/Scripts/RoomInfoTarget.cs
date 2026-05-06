using UnityEngine;

public class RoomInfoTarget : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public GameObject infoPanel;
    public GameObject interactHint;

    [Header("Player Control Scripts")]
    public MonoBehaviour playerLookScript;
    public MonoBehaviour playerMoveScript;

    [Header("Interaction Settings")]
    public float interactDistance = 3f;
    public KeyCode interactKey = KeyCode.E;
    public KeyCode cancelKey = KeyCode.Escape;

    [Header("Options")]
    public bool hidePanelWhenFar = false;
    public bool autoCloseWhenOutOfRange = true;

    private bool isPlayerInRange = false;
    private bool isInteracting = false;

    private void Start()
    {
        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
                player = playerObj.transform;
        }

        if (interactHint != null)
            interactHint.SetActive(false);

        if (infoPanel != null && hidePanelWhenFar)
            infoPanel.SetActive(false);

        ForceGameplayMode();
    }

    private void Update()
    {
        if (player == null)
            return;

        float distance = Vector3.Distance(player.position, transform.position);
        isPlayerInRange = distance <= interactDistance;

        if (autoCloseWhenOutOfRange && isInteracting && !isPlayerInRange)
        {
            ExitInteractionMode();
            return;
        }

        if (!isInteracting)
        {
            if (interactHint != null)
                interactHint.SetActive(isPlayerInRange);

            if (isPlayerInRange && Input.GetKeyDown(interactKey))
            {
                EnterInteractionMode();
            }
        }
        else
        {
            if (interactHint != null)
                interactHint.SetActive(false);

            if (Input.GetKeyDown(interactKey) || Input.GetKeyDown(cancelKey))
            {
                ExitInteractionMode();
            }
        }
    }

    public void EnterInteractionMode()
    {
        isInteracting = true;

        if (infoPanel != null && hidePanelWhenFar)
            infoPanel.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (playerLookScript != null)
            playerLookScript.enabled = false;

        if (playerMoveScript != null)
            playerMoveScript.enabled = false;
    }

    public void ExitInteractionMode()
    {
        isInteracting = false;

        if (infoPanel != null && hidePanelWhenFar)
            infoPanel.SetActive(false);

        ForceGameplayMode();
    }

    private void ForceGameplayMode()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        if (playerLookScript != null)
            playerLookScript.enabled = true;

        if (playerMoveScript != null)
            playerMoveScript.enabled = true;
    }

    public bool IsInteracting()
    {
        return isInteracting;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactDistance);
    }
}