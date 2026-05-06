using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Globalization;

public class RoomInfoUI : MonoBehaviour
{
    [Header("Electrical Texts")]
    public TMP_Text voltageText;
    public TMP_Text currentText;
    public TMP_Text powerText;
    public TMP_Text energyText;
    public TMP_Text frequencyText;
    public TMP_Text powerFactorText;
    public TMP_Text statusText;

    [Header("Air Conditioner Texts")]
    public TMP_Text airConditionerSetTempText;
    public TMP_Text airConditionerIndoorTempText;
    public TMP_Text airConditionerHumidityText;

    [Header("Light Labels")]
    public TMP_Text light1Label;
    public TMP_Text light2Label;
    public TMP_Text light3Label;

    [Header("Light Buttons")]
    public Button light1Button;
    public Button light2Button;
    public Button light3Button;

    [Header("Light Button Texts")]
    public TMP_Text light1ButtonText;
    public TMP_Text light2ButtonText;
    public TMP_Text light3ButtonText;

    [Header("Air Conditioner")]
    public TMP_Text airConditionerLabel;

    [Header("Air Conditioner Buttons")]
    public Button airConditionerOnButton;
    public Button airConditionerOffButton;

    [Header("Air Conditioner Temperature Buttons")]
    public Button airConditionerTempUpButton;
    public Button airConditionerTempDownButton;

    [Header("Colors")]
    public Color offColor = new Color(0.85f, 0.85f, 0.85f);
    public Color normalColor = new Color(0.35f, 0.95f, 0.85f);
    public Color warningColor = new Color(1.00f, 0.72f, 0.25f);
    public Color dangerColor = new Color(1.00f, 0.35f, 0.35f);
    public Color powerColor = new Color(1.00f, 0.88f, 0.35f);
    public Color energyColor = new Color(0.40f, 0.82f, 1.00f);

    [Header("Button Colors")]
    public Color buttonOnColor = new Color(0.20f, 0.85f, 0.45f);
    public Color buttonOffColor = new Color(0.30f, 0.30f, 0.33f);
    public Color buttonBusyColor = new Color(1.00f, 0.62f, 0.20f);

    [Header("Button Text Colors")]
    public Color activeTextColor = Color.white;
    public Color inactiveTextColor = new Color(0.25f, 0.25f, 0.25f);
    public Color unknownTextColor = new Color(0.75f, 0.75f, 0.75f);

    private Image light1ButtonImage;
    private Image light2ButtonImage;
    private Image light3ButtonImage;

    private Image airConditionerOnImage;
    private Image airConditionerOffImage;
    private Image airConditionerTempUpImage;
    private Image airConditionerTempDownImage;

    private TMP_Text airConditionerOnText;
    private TMP_Text airConditionerOffText;
    private TMP_Text airConditionerTempUpText;
    private TMP_Text airConditionerTempDownText;

    private void Awake()
    {
        BindAllReferences();

        if (light1Label != null) light1Label.text = "Đèn 1:";
        if (light2Label != null) light2Label.text = "Đèn 2:";
        if (light3Label != null) light3Label.text = "Đèn 3:";
        if (airConditionerLabel != null) airConditionerLabel.text = "Máy lạnh:";

        if (airConditionerTempUpText != null) airConditionerTempUpText.text = "+";
        if (airConditionerTempDownText != null) airConditionerTempDownText.text = "-";

        SetDefaultTexts();
    }

    private void BindAllReferences()
    {
        if (light1Button != null) light1ButtonImage = light1Button.GetComponent<Image>();
        if (light2Button != null) light2ButtonImage = light2Button.GetComponent<Image>();
        if (light3Button != null) light3ButtonImage = light3Button.GetComponent<Image>();

        if (airConditionerOnButton != null)
        {
            airConditionerOnImage = airConditionerOnButton.GetComponent<Image>();
            airConditionerOnText = airConditionerOnButton.GetComponentInChildren<TMP_Text>(true);
        }

        if (airConditionerOffButton != null)
        {
            airConditionerOffImage = airConditionerOffButton.GetComponent<Image>();
            airConditionerOffText = airConditionerOffButton.GetComponentInChildren<TMP_Text>(true);
        }

        if (airConditionerTempUpButton != null)
        {
            airConditionerTempUpImage = airConditionerTempUpButton.GetComponent<Image>();
            airConditionerTempUpText = airConditionerTempUpButton.GetComponentInChildren<TMP_Text>(true);
        }

        if (airConditionerTempDownButton != null)
        {
            airConditionerTempDownImage = airConditionerTempDownButton.GetComponent<Image>();
            airConditionerTempDownText = airConditionerTempDownButton.GetComponentInChildren<TMP_Text>(true);
        }
    }

    private void SetDefaultTexts()
    {
        SetVoltage("--");
        SetCurrent("--");
        SetPower("--");
        SetEnergy("--");
        SetFrequency("--");
        SetPowerFactor("--");

        SetLight1State("--");
        SetLight2State("--");
        SetLight3State("--");

        SetAirConditionerState("--");
        SetAirConditionerSetTemp("--");
        SetAirConditionerIndoorTemp("--");
        SetAirConditionerHumidity("--");

        SetAirConditionerTempButtonsBusy(false);

        SetConnectionStatus("Đang kết nối...");
    }

    public void SetVoltage(string value)
    {
        if (voltageText == null) return;
        voltageText.text = "Điện áp: " + SafeValue(value) + " V";
        voltageText.color = GetVoltageColor(value);
    }

    public void SetCurrent(string value)
    {
        if (currentText == null) return;
        currentText.text = "Dòng điện: " + SafeValue(value) + " A";
        currentText.color = GetCurrentColor(value);
    }

    public void SetPower(string value)
    {
        if (powerText == null) return;
        powerText.text = "Công suất: " + SafeValue(value) + " W";
        powerText.color = value == "--" ? offColor : powerColor;
    }

    public void SetEnergy(string value)
    {
        if (energyText == null) return;
        energyText.text = "Điện năng: " + SafeValue(value) + " kWh";
        energyText.color = value == "--" ? offColor : energyColor;
    }

    public void SetFrequency(string value)
    {
        if (frequencyText == null) return;
        frequencyText.text = "Tần số: " + SafeValue(value) + " Hz";
        frequencyText.color = value == "--" ? offColor : normalColor;
    }

    public void SetPowerFactor(string value)
    {
        if (powerFactorText == null) return;
        powerFactorText.text = "Hệ số CS: " + SafeValue(value);
        powerFactorText.color = GetPowerFactorColor(value);
    }

    public void SetConnectionStatus(string value)
    {
        if (statusText == null) return;

        string safe = string.IsNullOrWhiteSpace(value) ? "--" : value;
        statusText.text = "Kết nối: " + safe;
        statusText.color = GetConnectionColor(safe);
    }

    public void SetAirConditionerState(string value)
    {
        BindAllReferences();

        string safe = SafeValue(value).Trim().ToUpperInvariant();
        bool isUnknown = safe == "--";
        bool isOn = safe == "ON" || safe == "1" || safe == "TRUE";

        if (airConditionerOnText != null) airConditionerOnText.text = "ON";
        if (airConditionerOffText != null) airConditionerOffText.text = "OFF";

        // Không đổi màu nền image theo dữ liệu nữa.
        // Bạn tự set màu sẵn trong Inspector/prefab.

        if (isUnknown)
        {
            if (airConditionerOnText != null) airConditionerOnText.color = unknownTextColor;
            if (airConditionerOffText != null) airConditionerOffText.color = unknownTextColor;

            if (airConditionerOnButton != null) airConditionerOnButton.interactable = true;
            if (airConditionerOffButton != null) airConditionerOffButton.interactable = true;
            return;
        }

        if (isOn)
        {
            if (airConditionerOnText != null) airConditionerOnText.color = activeTextColor;
            if (airConditionerOffText != null) airConditionerOffText.color = inactiveTextColor;
        }
        else
        {
            if (airConditionerOnText != null) airConditionerOnText.color = inactiveTextColor;
            if (airConditionerOffText != null) airConditionerOffText.color = activeTextColor;
        }

        if (airConditionerOnButton != null) airConditionerOnButton.interactable = true;
        if (airConditionerOffButton != null) airConditionerOffButton.interactable = true;
    }

    public void SetAirConditionerBusy(bool busy)
    {
        BindAllReferences();

        if (airConditionerOnButton != null) airConditionerOnButton.interactable = !busy;
        if (airConditionerOffButton != null) airConditionerOffButton.interactable = !busy;

        // Không ép màu nền nút khi busy nữa.
        // Chỉ khóa/mở nút, giữ nguyên màu bạn đã set sẵn.
    }

    public void SetAirConditionerTempButtonsBusy(bool busy)
    {
        BindAllReferences();

        if (airConditionerTempUpText != null) airConditionerTempUpText.text = "+";
        if (airConditionerTempDownText != null) airConditionerTempDownText.text = "-";

        if (airConditionerTempUpButton != null) airConditionerTempUpButton.interactable = !busy;
        if (airConditionerTempDownButton != null) airConditionerTempDownButton.interactable = !busy;

        if (busy)
        {
            if (airConditionerTempUpImage != null) airConditionerTempUpImage.color = buttonBusyColor;
            if (airConditionerTempDownImage != null) airConditionerTempDownImage.color = buttonBusyColor;

            if (airConditionerTempUpText != null) airConditionerTempUpText.color = activeTextColor;
            if (airConditionerTempDownText != null) airConditionerTempDownText.color = activeTextColor;
        }
        else
        {
            if (airConditionerTempUpImage != null) airConditionerTempUpImage.color = buttonOnColor;
            if (airConditionerTempDownImage != null) airConditionerTempDownImage.color = buttonOnColor;

            if (airConditionerTempUpText != null) airConditionerTempUpText.color = activeTextColor;
            if (airConditionerTempDownText != null) airConditionerTempDownText.color = activeTextColor;
        }
    }

    public void SetAirConditionerSetTemp(string value)
    {
        if (airConditionerSetTempText == null) return;

        string safe = SafeValue(value);
        airConditionerSetTempText.text = safe == "--"
            ? "Nhiệt độ máy lạnh: --"
            : "Nhiệt độ máy lạnh: " + safe + " °C";

        airConditionerSetTempText.color = safe == "--" ? offColor : GetTemperatureColor(safe);
    }

    public void SetAirConditionerIndoorTemp(string value)
    {
        if (airConditionerIndoorTempText == null) return;

        string safe = SafeValue(value);
        airConditionerIndoorTempText.text = safe == "--"
            ? "Nhiệt độ phòng: --"
            : "Nhiệt độ phòng: " + safe + " °C";

        airConditionerIndoorTempText.color = safe == "--" ? offColor : GetTemperatureColor(safe);
    }

    public void SetAirConditionerHumidity(string value)
    {
        if (airConditionerHumidityText == null) return;

        string safe = SafeValue(value);
        airConditionerHumidityText.text = safe == "--"
            ? "Độ ẩm: --"
            : "Độ ẩm: " + safe + " %";

        airConditionerHumidityText.color = safe == "--" ? offColor : GetHumidityColor(safe);
    }

    public void SetLight1State(string value)
    {
        SetLightButtonState(1, value);
    }

    public void SetLight2State(string value)
    {
        SetLightButtonState(2, value);
    }

    public void SetLight3State(string value)
    {
        SetLightButtonState(3, value);
    }

    public void SetLightBusy(int lightIndex, bool busy)
    {
        Button btn = null;
        TMP_Text txt = null;
        Image img = null;

        GetLightRefs(lightIndex, ref btn, ref txt, ref img);
        SetToggleButtonBusy(btn, txt, img, busy);
    }

    private void SetLightButtonState(int lightIndex, string value)
    {
        Button btn = null;
        TMP_Text txt = null;
        Image img = null;

        GetLightRefs(lightIndex, ref btn, ref txt, ref img);
        SetToggleButtonState(btn, txt, img, value);
    }

    private void SetToggleButtonState(Button btn, TMP_Text txt, Image img, string value)
    {
        if (txt == null) return;

        string safe = SafeValue(value).Trim().ToUpperInvariant();
        bool isOn = safe == "ON" || safe == "1" || safe == "TRUE";

        if (safe == "--")
        {
            txt.text = "--";
            txt.color = unknownTextColor;

            if (img != null) img.color = buttonOffColor;
            if (btn != null) btn.interactable = true;
            return;
        }

        txt.text = isOn ? "ON" : "OFF";
        txt.color = isOn ? activeTextColor : inactiveTextColor;

        if (img != null) img.color = isOn ? buttonOnColor : buttonOffColor;
        if (btn != null) btn.interactable = true;
    }

    private void SetToggleButtonBusy(Button btn, TMP_Text txt, Image img, bool busy)
    {
        if (btn == null || txt == null) return;

        btn.interactable = !busy;

        if (busy)
        {
            txt.text = "...";
            txt.color = activeTextColor;

            if (img != null) img.color = buttonBusyColor;
        }
    }

    private void GetLightRefs(int lightIndex, ref Button btn, ref TMP_Text txt, ref Image img)
    {
        switch (lightIndex)
        {
            case 1:
                btn = light1Button;
                txt = light1ButtonText;
                img = light1ButtonImage;
                break;
            case 2:
                btn = light2Button;
                txt = light2ButtonText;
                img = light2ButtonImage;
                break;
            case 3:
                btn = light3Button;
                txt = light3ButtonText;
                img = light3ButtonImage;
                break;
        }
    }

    private string SafeValue(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? "--" : value;
    }

    private Color GetVoltageColor(string value)
    {
        if (!TryParseFloat(value, out float v))
            return offColor;

        if (v < 200f) return dangerColor;
        if (v < 220f) return warningColor;
        if (v <= 240f) return normalColor;
        return warningColor;
    }

    private Color GetCurrentColor(string value)
    {
        if (!TryParseFloat(value, out float c))
            return offColor;

        if (c <= 0f) return offColor;
        if (c < 10f) return normalColor;
        if (c < 30f) return warningColor;
        return dangerColor;
    }

    private Color GetPowerFactorColor(string value)
    {
        if (!TryParseFloat(value, out float pf))
            return offColor;

        if (pf >= 0.9f) return normalColor;
        if (pf >= 0.7f) return warningColor;
        return dangerColor;
    }

    private Color GetTemperatureColor(string value)
    {
        if (!TryParseFloat(value, out float t))
            return offColor;

        if (t < 18f) return warningColor;
        if (t <= 30f) return energyColor;
        if (t <= 35f) return warningColor;
        return dangerColor;
    }

    private Color GetHumidityColor(string value)
    {
        if (!TryParseFloat(value, out float h))
            return offColor;

        if (h < 30f) return warningColor;
        if (h <= 70f) return normalColor;
        if (h <= 85f) return warningColor;
        return dangerColor;
    }

    private Color GetConnectionColor(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return dangerColor;

        string v = value.Trim().ToLowerInvariant();

        if (v.Contains("online") || v.Contains("connected") || v.Contains("ok") || v.Contains("đã đăng nhập"))
            return normalColor;

        if (v.Contains("đang kết nối") || v.Contains("warning") || v.Contains("slow"))
            return warningColor;

        return dangerColor;
    }

    private bool TryParseFloat(string value, out float result)
    {
        result = 0f;

        if (string.IsNullOrWhiteSpace(value) || value == "--")
            return false;

        return float.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out result) ||
               float.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out result);
    }
}