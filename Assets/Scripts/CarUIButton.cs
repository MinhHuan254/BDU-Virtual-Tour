using UnityEngine;
using UnityEngine.EventSystems;

public class CarUIButton : MonoBehaviour, IPointerClickHandler
{
    public int index;
    public ParkingUIManager manager;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (manager == null) return;

        if (eventData.button == PointerEventData.InputButton.Left)
            manager.HandleCarIn(index);

        else if (eventData.button == PointerEventData.InputButton.Right)
            manager.HandleCarOut(index);
    }
}
