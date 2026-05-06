using UnityEngine;

// Nếu dùng UniWebView thì cần namespace của plugin (tuỳ plugin)
// using UniWebView;

public class CameraWebViewManager : MonoBehaviour
{
    [Header("Kéo thả panel full screen vào đây")]
    public GameObject panel;

    [Header("URL trang camera (login/realtime)")]
    public string cameraUrl = "https://your-site.com";

    // UniWebView webView;  // Nếu dùng UniWebView
    // (Nếu plugin của bạn khác, đổi kiểu tương ứng)

    void Awake()
    {
        if (panel != null) panel.SetActive(false);
    }

    public void Open()
    {
        if (panel == null) return;

        panel.SetActive(true);

        // ====== UniWebView (ví dụ API thường gặp) ======
        // Nếu bạn đã add UniWebView prefab/component vào panel thì lấy component ra:
        // webView = panel.GetComponentInChildren<UniWebView>();
        // if (webView == null) { Debug.LogError("Thiếu UniWebView component trong panel!"); return; }
        //
        // webView.Load(cameraUrl);
        // webView.Show();
        //
        // Một số site cần bật toolbar hoặc allow inline media tùy plugin settings.

        // ====== Nếu plugin khác ======
        // 1) Create WebView
        // 2) Load(cameraUrl)
        // 3) Show()
    }

    public void Close()
    {
        if (panel == null) return;

        // if (webView != null) webView.Hide(); // UniWebView
        panel.SetActive(false);
    }

    public bool IsOpen()
    {
        return panel != null && panel.activeSelf;
    }
}
