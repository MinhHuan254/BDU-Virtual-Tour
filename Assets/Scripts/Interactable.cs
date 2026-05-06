using UnityEngine;

public class Interactable : MonoBehaviour
{
    public virtual void Interact()
    {
        Debug.Log("Đã tương tác với: " + gameObject.name);
    }
}   