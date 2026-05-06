using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Globalization;

public class PCClickToggle : MonoBehaviour
{
    public WaterSprinklerController waterController;

    [Header("UI Panel")]
    public GameObject infoPanel;

    [Header("Left Column")]
    public TMP_Text deviceNameValue;
    public TMP_Text statusValue;
    public TMP_Text temperatureValue;
    public TMP_Text lastSeenValue;

    [Header("Right Column")]
    public TMP_Text humidityValue;
    public TMP_Text soilValue;
    public TMP_Text pumpModeValue;

    [Header("Pump Button")]
    public Button pumpButton;
    public TMP_Text pumpButtonText;
    public Image pumpButtonImage;

    [Header("Text Colors")]
    public Color defaultTextColor = Color.white;
    public Color secondaryTextColor = new Color(0.85f, 0.85f, 0.85f);

    public Color deviceNameColor = new Color(0.55f, 0.9f, 1f);

    public Color onlineColor = new Color(0.2f, 1f, 0.2f);
    public Color offlineColor = new Color(1f, 0.3f, 0.3f);

    public Color coolTempColor = new Color(0.4f, 0.85f, 1f);
    public Color normalTempColor = new Color(1f, 0.85f, 0.2f);
    public Color hotTempColor = new Color(1f, 0.45f, 0.2f);

    public Color lowHumidityColor = new Color(1f, 0.45f, 0.2f);
    public Color normalHumidityColor = new Color(0.2f, 1f, 0.2f);
    public Color highHumidityColor = new Color(0.4f, 0.85f, 1f);

    public Color drySoilColor = new Color(1f, 0.3f, 0.3f);
    public Color mediumSoilColor = new Color(1f, 0.85f, 0.2f);
    public Color wetSoilColor = new Color(0.2f, 1f, 0.2f);

    public Color manualColor = new Color(1f, 0.85f, 0.2f);
    public Color autoColor = new Color(0.3f, 0.8f, 1f);

    [Header("Pump Button Colors")]
    public Color pumpOnButtonColor = new Color(0.85f, 0.2f, 0.2f);
    public Color pumpOffButtonColor = new Color(0.2f, 0.7f, 0.25f);
    public Color pumpBusyButtonColor = new Color(0.8f, 0.6f, 0.2f);

    private Renderer objectRenderer;
    private Color originalColor;
    private bool isShowing = false;

    void Start()
    {
        objectRenderer = GetComponent<Renderer>();

        if (objectRenderer != null)
            originalColor = objectRenderer.material.color;

        if (infoPanel != null)
            infoPanel.SetActive(false);

        if (pumpButton != null)
        {
            pumpButton.onClick.RemoveListener(OnPumpButtonClicked);
            pumpButton.onClick.AddListener(OnPumpButtonClicked);
        }
    }

    void Update()
    {
        if (isShowing && waterController != null)
        {
            UpdateGardenUI();
        }
    }

    private void OnMouseEnter()
    {
        if (objectRenderer != null)
            objectRenderer.material.color = Color.yellow;
    }

    private void OnMouseExit()
    {
        if (objectRenderer != null)
            objectRenderer.material.color = originalColor;
    }

    private void OnMouseDown()
    {
        if (waterController == null)
        {
            Debug.LogWarning("Chua gan WaterSprinklerController cho PCClickToggle");
            return;
        }

        isShowing = !isShowing;

        if (infoPanel != null)
            infoPanel.SetActive(isShowing);

        if (isShowing)
            UpdateGardenUI();
    }

    public void CloseInfoPanel()
    {
        isShowing = false;

        if (infoPanel != null)
            infoPanel.SetActive(false);
    }

    public void OnPumpButtonClicked()
    {
        if (waterController == null)
            return;

        if (waterController.IsSendingPumpCommand)
            return;

        waterController.TogglePumpFromApi();
    }

    public void UpdateGardenUI()
    {
        if (waterController == null) return;

        if (deviceNameValue != null)
        {
            string value = waterController.GetDeviceName();
            deviceNameValue.text = value;
            deviceNameValue.color = deviceNameColor;
        }

        if (statusValue != null)
        {
            string status = waterController.GetDeviceStatus();
            statusValue.text = status;
            statusValue.color = GetStatusColor(status);
        }

        if (temperatureValue != null)
        {
            string value = waterController.GetTemperatureText();
            temperatureValue.text = value;
            temperatureValue.color = GetTemperatureColor(value);
        }

        if (lastSeenValue != null)
        {
            string value = waterController.GetLastSeenText();
            lastSeenValue.text = value;
            lastSeenValue.color = secondaryTextColor;
        }

        if (humidityValue != null)
        {
            string value = waterController.GetHumidityText();
            humidityValue.text = value;
            humidityValue.color = GetHumidityColor(value);
        }

        if (soilValue != null)
        {
            string value = waterController.GetSoilMoistureText();
            soilValue.text = value;
            soilValue.color = GetSoilColor(value);
        }

        if (pumpModeValue != null)
        {
            string pumpMode = waterController.GetPumpModeText();
            pumpModeValue.text = pumpMode;
            pumpModeValue.color = GetPumpModeColor(pumpMode);
        }

        UpdatePumpButtonUI();
    }

    private void UpdatePumpButtonUI()
    {
        if (pumpButtonText != null)
        {
            pumpButtonText.text = waterController.GetPumpButtonLabel();
            pumpButtonText.color = Color.white;
        }

        if (pumpButtonImage != null)
        {
            if (waterController.IsSendingPumpCommand)
            {
                pumpButtonImage.color = pumpBusyButtonColor;
            }
            else if (waterController.IsPumpCurrentlyOn())
            {
                pumpButtonImage.color = pumpOnButtonColor;
            }
            else
            {
                pumpButtonImage.color = pumpOffButtonColor;
            }
        }

        if (pumpButton != null)
        {
            pumpButton.interactable = !waterController.IsSendingPumpCommand;
        }
    }

    private Color GetStatusColor(string status)
    {
        if (string.IsNullOrEmpty(status))
            return defaultTextColor;

        status = status.Trim().ToUpper();

        if (status == "ONLINE")
            return onlineColor;

        if (status == "OFFLINE")
            return offlineColor;

        return defaultTextColor;
    }

    private Color GetPumpModeColor(string mode)
    {
        if (string.IsNullOrEmpty(mode))
            return defaultTextColor;

        mode = mode.Trim().ToUpper();

        if (mode == "MANUAL")
            return manualColor;

        if (mode == "AUTO")
            return autoColor;

        return defaultTextColor;
    }

    private Color GetTemperatureColor(string value)
    {
        if (!TryParseNumber(value, out float temp))
            return defaultTextColor;

        if (temp < 25f)
            return coolTempColor;

        if (temp <= 32f)
            return normalTempColor;

        return hotTempColor;
    }

    private Color GetHumidityColor(string value)
    {
        if (!TryParseNumber(value, out float humidity))
            return defaultTextColor;

        if (humidity < 40f)
            return lowHumidityColor;

        if (humidity <= 75f)
            return normalHumidityColor;

        return highHumidityColor;
    }

    private Color GetSoilColor(string value)
    {
        if (!TryParseNumber(value, out float soil))
            return defaultTextColor;

        if (soil < 30f)
            return drySoilColor;

        if (soil <= 70f)
            return mediumSoilColor;

        return wetSoilColor;
    }

    private bool TryParseNumber(string text, out float result)
    {
        result = 0f;

        if (string.IsNullOrEmpty(text))
            return false;

        string cleaned = "";

        foreach (char c in text)
        {
            if (char.IsDigit(c) || c == '.' || c == ',')
                cleaned += c;
            else if (cleaned.Length > 0)
                break;
        }

        cleaned = cleaned.Replace(',', '.');

        return float.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out result);
    }
}