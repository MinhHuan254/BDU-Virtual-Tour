using UnityEngine;
using UnityEngine.UI;

public class CameraVideoZoomUI : MonoBehaviour
{
    [Header("Video")]
    public RawImage videoScreen;

    [Header("Zoom")]
    public float zoomStep = 0.2f;
    public float minZoom = 1f;
    public float maxZoom = 4f;

    [Header("Pan Sliders")]
    public Slider horizontalPanSlider; // trái-phải
    public Slider verticalPanSlider;   // trên-dưới

    private float currentZoom = 1f;
    private Vector2 focus = new Vector2(0.5f, 0.5f);

    private void Start()
    {
        if (videoScreen == null)
            videoScreen = GetComponentInChildren<RawImage>();

        SetupSliders();
        ApplyZoom();
    }

    private void SetupSliders()
    {
        if (horizontalPanSlider != null)
        {
            horizontalPanSlider.minValue = 0f;
            horizontalPanSlider.maxValue = 1f;
            horizontalPanSlider.value = 0.5f;
            horizontalPanSlider.onValueChanged.AddListener(OnHorizontalPanChanged);
        }

        if (verticalPanSlider != null)
        {
            verticalPanSlider.minValue = 0f;
            verticalPanSlider.maxValue = 1f;
            verticalPanSlider.value = 0.5f;
            verticalPanSlider.onValueChanged.AddListener(OnVerticalPanChanged);
        }
    }

    public void ZoomIn()
    {
        currentZoom = Mathf.Clamp(currentZoom + zoomStep, minZoom, maxZoom);
        ApplyZoom();
    }

    public void ZoomOut()
    {
        currentZoom = Mathf.Clamp(currentZoom - zoomStep, minZoom, maxZoom);
        ApplyZoom();
    }

    public void ResetZoom()
    {
        currentZoom = 1f;
        focus = new Vector2(0.5f, 0.5f);

        if (horizontalPanSlider != null)
            horizontalPanSlider.value = 0.5f;

        if (verticalPanSlider != null)
            verticalPanSlider.value = 0.5f;

        ApplyZoom();
    }

    public void OnHorizontalPanChanged(float value)
    {
        focus.x = value;
        ApplyZoom();
    }

    public void OnVerticalPanChanged(float value)
    {
        focus.y = value;
        ApplyZoom();
    }

    private void ApplyZoom()
    {
        if (videoScreen == null)
        {
            Debug.LogWarning("Chưa gán videoScreen!");
            return;
        }

        float width = 1f / currentZoom;
        float height = 1f / currentZoom;

        // Giới hạn tâm focus để không vượt ra ngoài ảnh
        focus.x = Mathf.Clamp(focus.x, width * 0.5f, 1f - width * 0.5f);
        focus.y = Mathf.Clamp(focus.y, height * 0.5f, 1f - height * 0.5f);

        float x = focus.x - width * 0.5f;
        float y = focus.y - height * 0.5f;

        videoScreen.uvRect = new Rect(x, y, width, height);
    }
}