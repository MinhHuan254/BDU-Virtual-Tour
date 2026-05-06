using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ParkingUIManager : MonoBehaviour
{
    [Header("Toggle UI")]
    public KeyCode toggleKey = KeyCode.B;
    public GameObject panel;

    [Header("Buttons Area")]
    public Transform content;
    public Button buttonTemplate;

    [Header("Cars List")]
    public List<CarMover> cars = new List<CarMover>();

    [Header("Freeze Camera When UI Open")]
    public MonoBehaviour[] lookScriptsToDisable;
    public bool lockCursorWhenClose = true;

    [Header("UI Behavior")]
    public bool autoCloseAfterChoose = false;

    public static bool UIIsOpen { get; private set; }

    void Start()
    {
        if (panel != null) panel.SetActive(false);
        UIIsOpen = false;
        BuildButtons();
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey) && panel != null)
            SetUI(!panel.activeSelf);
    }

    void SetUI(bool show)
    {
        panel.SetActive(show);
        UIIsOpen = show;

        if (show)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;

            if (lookScriptsToDisable != null)
                foreach (var s in lookScriptsToDisable)
                    if (s != null) s.enabled = false;
        }
        else
        {
            if (lookScriptsToDisable != null)
                foreach (var s in lookScriptsToDisable)
                    if (s != null) s.enabled = true;

            if (lockCursorWhenClose)
            {
                Cursor.visible = false;
                Cursor.lockState = CursorLockMode.Locked;
            }
            else
            {
                Cursor.visible = true;
                Cursor.lockState = CursorLockMode.None;
            }
        }
    }

    void BuildButtons()
    {
        if (content == null || buttonTemplate == null) return;

        // Clear old buttons (trừ template)
        for (int i = content.childCount - 1; i >= 0; i--)
        {
            var child = content.GetChild(i);
            if (child.gameObject != buttonTemplate.gameObject)
                Destroy(child.gameObject);
        }

        for (int i = 0; i < cars.Count; i++)
        {
            int index = i;

            Button btn = Instantiate(buttonTemplate, content);
            btn.gameObject.SetActive(true);

            var tmp = btn.GetComponentInChildren<TMP_Text>();
            if (tmp != null) tmp.text = (index + 1).ToString();

            // ✅ Gắn script bắt chuột trái/phải
            var clicker = btn.GetComponent<CarUIButton>();
            if (clicker == null) clicker = btn.gameObject.AddComponent<CarUIButton>();
            clicker.index = index;
            clicker.manager = this;

            // Optional: vẫn xoá onClick để khỏi hiểu lầm
            btn.onClick.RemoveAllListeners();
        }
    }

    // ====== Các hàm được CarUIButton gọi ======

    public void HandleCarIn(int index)
    {
        if (index < 0 || index >= cars.Count || cars[index] == null) return;
        cars[index].StartFromBeginning();
        Debug.Log("IN car: " + (index + 1));

        if (autoCloseAfterChoose) SetUI(false);
    }

    public void HandleCarOut(int index)
    {
        if (index < 0 || index >= cars.Count || cars[index] == null) return;
        cars[index].ExitToBeginningAndHide();  // ✅ cần có trong CarMover
        Debug.Log("OUT car: " + (index + 1));

        if (autoCloseAfterChoose) SetUI(false);
    }
}
