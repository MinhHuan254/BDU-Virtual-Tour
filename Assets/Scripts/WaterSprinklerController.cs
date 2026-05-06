using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class WaterSprinklerController : MonoBehaviour
{
    [Header("Danh sach cac voi nuoc")]
    public ParticleSystem[] waterParticles;

    [Header("API")]
    public string apiUrl = "http://192.168.69.152:8000/rooms/3/data";
    public string baseUrl = "http://192.168.69.152:8000";
    public string loginUrl = "http://192.168.69.152:8000/login";
    public float refreshInterval = 5f;
    public bool autoRefresh = true;

    [Header("Admin Login")]
    public string loginUsername = "tramgianguyen";
    public string loginPassword = "123456";

    [Header("Pump Relay Config")]
    public int pumpRelayNumber = 1;

    [SerializeField] private string bearerToken = "";

    private bool isWatering = false;
    private bool isSendingPumpCommand = false;
    private bool isLoggingIn = false;
    private GardenDevicePayload currentDevice;
    private bool hasData = false;

    private string localPumpStateOverride = null;

    public bool IsWatering => isWatering;
    public bool HasData => hasData;
    public bool IsSendingPumpCommand => isSendingPumpCommand;

    void Start()
    {
        StopWatering();
        StartCoroutine(InitializeAndStart());
    }

    private IEnumerator InitializeAndStart()
    {
        yield return Login();

        if (autoRefresh)
            StartCoroutine(RefreshLoop());
        else
            yield return FetchGardenData();
    }

    private IEnumerator RefreshLoop()
    {
        while (true)
        {
            if (!isSendingPumpCommand)
                yield return FetchGardenData();

            yield return new WaitForSeconds(refreshInterval);
        }
    }

    private string GetSanitizedToken()
    {
        if (string.IsNullOrWhiteSpace(bearerToken))
            return "";

        string token = bearerToken.Trim();
        if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            token = token.Substring(7).Trim();

        return token;
    }

    private void ApplyAuthHeader(UnityWebRequest request)
    {
        string token = GetSanitizedToken();
        if (!string.IsNullOrWhiteSpace(token))
            request.SetRequestHeader("Authorization", "Bearer " + token);
    }

    public IEnumerator Login()
    {
        if (isLoggingIn)
            yield break;

        if (string.IsNullOrWhiteSpace(loginUrl))
        {
            Debug.LogWarning("loginUrl dang rong.");
            yield break;
        }

        isLoggingIn = true;

        LoginRequestBody loginBody = new LoginRequestBody
        {
            username = loginUsername,
            password = loginPassword
        };

        string jsonBody = JsonUtility.ToJson(loginBody);

        using (UnityWebRequest request = UnityWebRequest.Post(loginUrl, jsonBody, "application/json"))
        {
            Debug.Log("Dang dang nhap: " + loginUrl);
            yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            bool hasError = request.result == UnityWebRequest.Result.ConnectionError ||
                            request.result == UnityWebRequest.Result.ProtocolError;
#else
            bool hasError = request.isNetworkError || request.isHttpError;
#endif

            if (hasError)
            {
                Debug.LogError("Loi dang nhap: " + request.error +
                               " | URL: " + loginUrl +
                               " | ResponseCode: " + request.responseCode +
                               " | Response: " + request.downloadHandler.text);
                isLoggingIn = false;
                yield break;
            }

            string responseText = request.downloadHandler.text;
            LoginResponseBody loginResponse = null;

            try
            {
                loginResponse = JsonUtility.FromJson<LoginResponseBody>(responseText);
            }
            catch (Exception e)
            {
                Debug.LogError("Khong parse duoc response login: " + e.Message + " | Response: " + responseText);
                isLoggingIn = false;
                yield break;
            }

            if (loginResponse == null || string.IsNullOrWhiteSpace(loginResponse.access_token))
            {
                Debug.LogError("Login thanh cong nhung khong tim thay access_token. Response: " + responseText);
                isLoggingIn = false;
                yield break;
            }

            bearerToken = loginResponse.access_token.Trim();
            Debug.Log("Dang nhap thanh cong. Role = " + loginResponse.vai_tro);
        }

        isLoggingIn = false;
    }

    public IEnumerator FetchGardenData()
    {
        if (string.IsNullOrWhiteSpace(GetSanitizedToken()))
            yield return Login();

        using (UnityWebRequest request = UnityWebRequest.Get(apiUrl))
        {
            ApplyAuthHeader(request);

            Debug.Log("Dang goi API: " + apiUrl);
            yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            bool hasError = request.result == UnityWebRequest.Result.ConnectionError ||
                            request.result == UnityWebRequest.Result.ProtocolError;
#else
            bool hasError = request.isNetworkError || request.isHttpError;
#endif

            if (request.responseCode == 401)
            {
                Debug.LogWarning("Token het han hoac khong hop le, dang dang nhap lai...");
                yield return Login();

                using (UnityWebRequest retryRequest = UnityWebRequest.Get(apiUrl))
                {
                    ApplyAuthHeader(retryRequest);
                    yield return retryRequest.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
                    bool retryError = retryRequest.result == UnityWebRequest.Result.ConnectionError ||
                                      retryRequest.result == UnityWebRequest.Result.ProtocolError;
#else
                    bool retryError = retryRequest.isNetworkError || retryRequest.isHttpError;
#endif

                    if (retryError)
                    {
                        Debug.LogError("Loi goi API sau khi dang nhap lai: " + retryRequest.error +
                                       " | URL: " + apiUrl +
                                       " | Response: " + retryRequest.downloadHandler.text);
                        yield break;
                    }

                    yield return ProcessGardenDataResponse(retryRequest.downloadHandler.text);
                    yield break;
                }
            }

            if (hasError)
            {
                Debug.LogError("Loi goi API: " + request.error +
                               " | URL: " + apiUrl +
                               " | Response: " + request.downloadHandler.text);
                yield break;
            }

            yield return ProcessGardenDataResponse(request.downloadHandler.text);
        }
    }

    private IEnumerator ProcessGardenDataResponse(string json)
    {
        GardenApiResponse response = null;

        try
        {
            response = JsonUtility.FromJson<GardenApiResponse>(json);
        }
        catch (Exception e)
        {
            Debug.LogError("Khong parse duoc JSON: " + e.Message + " | Response: " + json);
            yield break;
        }

        if (response == null || response.devices == null || response.devices.Length == 0)
        {
            Debug.LogWarning("API khong co du lieu devices.");
            yield break;
        }

        currentDevice = response.devices[0];
        hasData = true;
        localPumpStateOverride = null;

        if (IsPumpCurrentlyOn())
            StartWatering();
        else
            StopWatering();
    }

    public void RefreshNow()
    {
        StartCoroutine(FetchGardenData());
    }

    public void TogglePumpFromApi()
    {
        if (isSendingPumpCommand)
            return;

        if (!hasData || currentDevice == null || string.IsNullOrEmpty(currentDevice.device_id))
        {
            Debug.LogWarning("Chua co device_id de dieu khien bom.");
            return;
        }

        StartCoroutine(SetPumpStateFromApi(!IsPumpCurrentlyOn()));
    }

    public IEnumerator SetPumpStateFromApi(bool turnOn)
    {
        if (isSendingPumpCommand)
            yield break;

        if (string.IsNullOrWhiteSpace(GetSanitizedToken()))
            yield return Login();

        isSendingPumpCommand = true;

        string url = baseUrl + "/devices/" + currentDevice.device_id + "/control-relay";
        string state = turnOn ? "ON" : "OFF";

        RelayControlRequest bodyObject = new RelayControlRequest
        {
            relay = pumpRelayNumber,
            state = state
        };

        string jsonBody = JsonUtility.ToJson(bodyObject);

        yield return SendPumpCommand(url, jsonBody, state, true);

        isSendingPumpCommand = false;
    }

    private IEnumerator SendPumpCommand(string url, string jsonBody, string state, bool allowRetryOn401)
    {
        using (UnityWebRequest request = UnityWebRequest.Post(url, jsonBody, "application/json"))
        {
            ApplyAuthHeader(request);

            Debug.Log("Sending: " + state + " | URL: " + url + " | Body: " + jsonBody);
            yield return request.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            bool hasError = request.result == UnityWebRequest.Result.ConnectionError ||
                            request.result == UnityWebRequest.Result.ProtocolError;
#else
            bool hasError = request.isNetworkError || request.isHttpError;
#endif

            if (request.responseCode == 401 && allowRetryOn401)
            {
                Debug.LogWarning("POST dieu khien bom bi 401, dang dang nhap lai va thu lai...");
                yield return Login();
                yield return SendPumpCommand(url, jsonBody, state, false);
                yield break;
            }

            if (hasError)
            {
                Debug.LogError("Loi dieu khien bom: " + request.error +
                               " | URL: " + url +
                               " | ResponseCode: " + request.responseCode +
                               " | Response: " + request.downloadHandler.text);
                yield break;
            }

            Debug.Log("Dieu khien bom thanh cong: " + request.downloadHandler.text);

            localPumpStateOverride = state;

            if (currentDevice != null && currentDevice.data != null && currentDevice.data.relay_1_pump != null)
            {
                currentDevice.data.relay_1_pump.value = state;
                currentDevice.data.relay_1_pump.timestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            }

            if (state == "ON")
                StartWatering();
            else
                StopWatering();

            yield return new WaitForSeconds(0.15f);
            yield return FetchGardenData();
        }
    }

    public bool IsPumpCurrentlyOn()
    {
        if (!string.IsNullOrEmpty(localPumpStateOverride))
            return localPumpStateOverride == "ON";

        if (!hasData || currentDevice == null || currentDevice.data == null || currentDevice.data.relay_1_pump == null)
            return false;

        string value = currentDevice.data.relay_1_pump.value;
        return !string.IsNullOrEmpty(value) && value.Trim().ToUpper() == "ON";
    }

    public string GetPumpButtonLabel()
    {
        if (isSendingPumpCommand)
            return "DANG GUI...";

        return IsPumpCurrentlyOn() ? "OFF" : "ON";
    }

    public void StartWatering()
    {
        isWatering = true;

        foreach (ParticleSystem ps in waterParticles)
            if (ps != null && !ps.isPlaying) ps.Play();
    }

    public void StopWatering()
    {
        isWatering = false;

        foreach (ParticleSystem ps in waterParticles)
            if (ps != null && ps.isPlaying) ps.Stop();
    }

    public string GetDeviceName() => !hasData || currentDevice == null ? "-" : Safe(currentDevice.ten_thiet_bi);
    public string GetDeviceStatus() => !hasData || currentDevice == null ? "-" : Safe(currentDevice.trang_thai);
    public string GetTemperatureText() => !hasData || currentDevice?.data == null ? "-" : GetFloatText(currentDevice.data.temperature);
    public string GetLastSeenText() => !hasData || currentDevice == null ? "-" : FormatUnixTime(currentDevice.last_seen);
    public string GetHumidityText() => !hasData || currentDevice?.data == null ? "-" : GetFloatText(currentDevice.data.humidity);
    public string GetSoilMoistureText() => !hasData || currentDevice?.data == null ? "-" : GetFloatText(currentDevice.data.soil_moisture);
    public string GetPumpModeText() => !hasData || currentDevice?.data == null ? "-" : GetStringValue(currentDevice.data.pump_mode);
    public string GetPumpStateText()
    {
        if (!string.IsNullOrEmpty(localPumpStateOverride))
            return localPumpStateOverride;

        return !hasData || currentDevice?.data == null ? "-" : GetStringValue(currentDevice.data.relay_1_pump);
    }

    private string Safe(string value) => string.IsNullOrEmpty(value) ? "-" : value;

    private string GetStringValue(GardenStringMetric metric)
    {
        if (metric == null || string.IsNullOrEmpty(metric.value))
            return "-";

        return metric.value;
    }

    private string GetFloatText(GardenFloatMetric metric)
    {
        if (metric == null)
            return "-";

        string unit = string.IsNullOrEmpty(metric.don_vi) ? "" : (" " + metric.don_vi);
        return metric.value.ToString("0.##") + unit;
    }

    private string FormatUnixTime(long unixTime)
    {
        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixTime)
                .ToLocalTime()
                .ToString("dd/MM/yyyy HH:mm:ss");
        }
        catch
        {
            return unixTime.ToString();
        }
    }
}

[Serializable]
public class LoginRequestBody
{
    public string username;
    public string password;
}

[Serializable]
public class LoginResponseBody
{
    public string access_token;
    public string token_type;
    public string vai_tro;
}

[Serializable]
public class RelayControlRequest
{
    public int relay;
    public string state;
}

[Serializable]
public class GardenApiResponse
{
    public GardenDevicePayload[] devices;
}

[Serializable]
public class GardenDevicePayload
{
    public string device_id;
    public string ten_thiet_bi;
    public string loai_thiet_bi;
    public string trang_thai;
    public long last_seen;
    public int phong_id;
    public GardenDeviceData data;
    public GardenRelayPayload[] relays;
}

[Serializable]
public class GardenDeviceData
{
    public GardenStringMetric cmd_edge_relay;
    public GardenStringMetric cmd_raw;
    public GardenStringMetric fan_mode;
    public GardenFloatMetric humidity;
    public GardenStringMetric pump_mode;
    public GardenFloatMetric relay;
    public GardenStringMetric relay_1_pump;
    public GardenStringMetric relay_2_light;
    public GardenStringMetric relay_3_fan;
    public GardenStringMetric relay_4_spare;
    public GardenFloatMetric soil_moisture;
    public GardenStringMetric state;
    public GardenFloatMetric temperature;
}

[Serializable]
public class GardenRelayPayload
{
    public int relay_number;
    public string ten_relay;
    public string topic;
}

[Serializable]
public class GardenStringMetric
{
    public string value;
    public string don_vi;
    public string mo_ta;
    public long timestamp;
}

[Serializable]
public class GardenFloatMetric
{
    public float value;
    public string don_vi;
    public string mo_ta;
    public long timestamp;
}