using UnityEngine;

public class CameraZoomUI : MonoBehaviour
{
    public RectTransform videoScreen;

    [Header("Zoom Settings")]
    public float zoomStep = 0.1f;
    public float minScale = 1f;
    public float maxScale = 3f;

    private Vector3 defaultScale;
    private Vector2 defaultAnchoredPosition;

    private void Start()
    {
        if (videoScreen != null)
        {
            defaultScale = videoScreen.localScale;
            defaultAnchoredPosition = videoScreen.anchoredPosition;
        }
    }

    public void ZoomIn()
    {
        if (videoScreen == null) return;

        float nextScale = Mathf.Clamp(videoScreen.localScale.x + zoomStep, minScale, maxScale);
        videoScreen.localScale = new Vector3(nextScale, nextScale, 1f);
    }

    public void ZoomOut()
    {
        if (videoScreen == null) return;

        float nextScale = Mathf.Clamp(videoScreen.localScale.x - zoomStep, minScale, maxScale);
        videoScreen.localScale = new Vector3(nextScale, nextScale, 1f);

        // nếu thu nhỏ về mặc định thì trả lại đúng tâm
        if (Mathf.Approximately(nextScale, minScale))
        {
            videoScreen.anchoredPosition = defaultAnchoredPosition;
        }
    }

    public void ResetZoom()
    {
        if (videoScreen == null) return;

        videoScreen.localScale = defaultScale;
        videoScreen.anchoredPosition = defaultAnchoredPosition;
    }
}