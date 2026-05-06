using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class RoomApiController : MonoBehaviour
{
    public enum ReadAuthMode
    {
        Auto,
        AuthorizationHeader,
        QueryToken,
        HeaderAndQuery
    }

    [Header("Read API (Port 8000)")]
    public string apiUrl = "http://192.168.69.152:8000/rooms/2/data";
    public float refreshTime = 3.0f;
    public int requestTimeout = 20;
    public int readRequestRetryCount = 2;

    [Header("Read API Auth")]
    public ReadAuthMode readAuthMode = ReadAuthMode.QueryToken;
    public string readTokenQueryKey = "token";

    [Header("Read API Login (Port 8000)")]
    public bool useDedicatedReadLogin = false;
    public string readLoginUrl = "";
    public string readUsername = "";
    public string readPassword = "";
    public int readLoginTimeout = 10;

    [Header("Auth + Control API (Port 8001)")]
    public string controlBaseUrl = "http://192.168.69.152:8001";
    public string username = "tramgianguyen";
    public string password = "123456";
    public int controlTimeout = 5;

    [Header("Read Token Refresh")]
    public float readTokenRefreshMinutes = 58f;

    [Header("AC Control")]
    public int acCommandTimeout = 5;
    public int acCommandRetryCount = 2;

    [Header("Gateway Lights")]
    public string gatewayDeviceId = "gateway-8161a515";

    [Header("Air Conditioner Read")]
    public string airConditionerDeviceId = "gateway-8161a515";

    [Header("Relay Lights In Scene")]
    public Light[] relay1Lights;
    public Light[] relay2Lights;
    public Light[] relay3Lights;

    [Header("UI")]
    public RoomInfoUI roomInfoUI;

    [Header("Cursor")]
    public KeyCode interactKey = KeyCode.E;
    public KeyCode cancelKey = KeyCode.Escape;

    [Header("Control")]
    public string fixedControlEndpoint = "/relay/control";
    public string acControlEndpoint = "/ac/control";

    [Header("AC Temperature Limits")]
    public int minAcTemp = 16;
    public int maxAcTemp = 30;

    [Header("AC Sync")]
    public float acRefreshDelayAfterControl = 0.25f;

    [Header("Read Stability")]
    public float retryDelaySeconds = 0.5f;
    public bool skipNextPollAfterManualControl = true;
    public int maxConsecutiveReadFailuresBeforeDisconnect = 3;

    private bool isCursorVisible = false;

    private bool? lastRelay1State = null;
    private bool? lastRelay2State = null;
    private bool? lastRelay3State = null;
    private bool? lastAirConditionerState = null;
    private int? lastAirConditionerSetTemp = null;

    private string readAccessToken = "";
    private string readAccessTokenType = "Bearer";
    private DateTime readTokenIssuedAtUtc = DateTime.MinValue;

    private string controlAccessToken = "";
    private string controlAccessTokenType = "Bearer";

    private bool isLoggingInRead = false;
    private bool isLoggingInControl = false;
    private bool isSendingRelayControl = false;
    private bool isSendingAcControl = false;
    private bool isReadingRoomData = false;
    private bool hasLoggedInSuccessfully = false;

    private bool hasResolvedReadAuthMode = false;
    private ReadAuthMode resolvedReadAuthMode = ReadAuthMode.QueryToken;

    private float nextAllowedPollTime = 0f;
    private int consecutiveReadFailures = 0;

    [Serializable]
    private class LoginRequest
    {
        public string username;
        public string password;
    }

    [Serializable]
    private class UserInfo
    {
        public string username;
        public string role;
    }

    [Serializable]
    private class LoginResponse
    {
        public string access_token;
        public string token;
        public string token_type;
        public UserInfo user_info;
    }

    [Serializable]
    private class RelayControlRequest
    {
        public int relay;
        public string state;
        public string device_id;
    }

    private void Start()
    {
        Debug.Log("[RoomApiController] Start");
        Debug.Log("[RoomApiController] apiUrl = " + apiUrl);
        Debug.Log("[RoomApiController] readLoginUrl = " + GetResolvedReadLoginUrl());
        Debug.Log("[RoomApiController] readAuthMode = " + readAuthMode);
        Debug.Log("[RoomApiController] useDedicatedReadLogin = " + useDedicatedReadLogin);

        if (roomInfoUI != null)
        {
            roomInfoUI.SetVoltage("--");
            roomInfoUI.SetCurrent("--");
            roomInfoUI.SetPower("--");
            roomInfoUI.SetEnergy("--");
            roomInfoUI.SetFrequency("--");
            roomInfoUI.SetPowerFactor("--");

            roomInfoUI.SetLight1State("--");
            roomInfoUI.SetLight2State("--");
            roomInfoUI.SetLight3State("--");

            roomInfoUI.SetAirConditionerState("--");
            roomInfoUI.SetAirConditionerSetTemp("--");
            roomInfoUI.SetAirConditionerIndoorTemp("--");
            roomInfoUI.SetAirConditionerHumidity("--");
            roomInfoUI.SetAirConditionerBusy(false);
            roomInfoUI.SetAirConditionerTempButtonsBusy(false);

            roomInfoUI.SetConnectionStatus("Đang kết nối...");
        }

        HideCursor();
        StartCoroutine(StartupRoutine());
    }

    private void Update()
    {
        HandleCursorToggle();
    }

    private IEnumerator StartupRoutine()
    {
        fixedControlEndpoint = "/relay/control";

        if (roomInfoUI != null)
            roomInfoUI.SetConnectionStatus("Đang lấy token...");

        yield return StartCoroutine(LoginControlApi());

        if (string.IsNullOrEmpty(controlAccessToken))
        {
            Debug.LogError("[RoomApiController] Startup failed: controlAccessToken is empty after LoginControlApi()");
            if (roomInfoUI != null)
                roomInfoUI.SetConnectionStatus("Login lỗi");
            yield break;
        }

        SyncReadTokenFromControl();
        hasLoggedInSuccessfully = true;

        if (roomInfoUI != null)
            roomInfoUI.SetConnectionStatus("Đã lấy token, đang đọc dữ liệu...");

        bool firstReadOk = false;
        yield return StartCoroutine(GetRoomData(ok => firstReadOk = ok));

        if (!firstReadOk)
            Debug.LogWarning("[RoomApiController] Startup: login thành công nhưng chưa lấy được dữ liệu đầu tiên.");

        StartCoroutine(PollData());
    }

    private void SyncReadTokenFromControl()
    {
        readAccessToken = controlAccessToken;
        readAccessTokenType = controlAccessTokenType;
        readTokenIssuedAtUtc = DateTime.UtcNow;

        Debug.Log("[RoomApiController] Sync read token from control token");
        Debug.Log("[RoomApiController] Synced token preview = " + GetTokenPreview(readAccessToken));
    }

    private IEnumerator PollData()
    {
        Debug.Log("[RoomApiController] PollData started");

        while (true)
        {
            if (Time.time >= nextAllowedPollTime)
            {
                if (!isSendingRelayControl && !isSendingAcControl && !isReadingRoomData)
                    yield return StartCoroutine(GetRoomData(null));
            }

            yield return new WaitForSeconds(refreshTime);
        }
    }

    private string GetResolvedReadLoginUrl()
    {
        if (!string.IsNullOrWhiteSpace(readLoginUrl))
            return readLoginUrl.Trim();

        string source = apiUrl.Trim();
        int schemeIdx = source.IndexOf("://", StringComparison.Ordinal);
        if (schemeIdx < 0)
            return source + "/login";

        int pathStart = source.IndexOf('/', schemeIdx + 3);
        if (pathStart < 0)
            return source.TrimEnd('/') + "/login";

        string origin = source.Substring(0, pathStart);
        return origin + "/login";
    }

    private string GetReadUsername()
    {
        return string.IsNullOrWhiteSpace(readUsername) ? username : readUsername;
    }

    private string GetReadPassword()
    {
        return string.IsNullOrWhiteSpace(readPassword) ? password : readPassword;
    }

    private bool ShouldRefreshReadToken()
    {
        if (string.IsNullOrEmpty(readAccessToken))
            return true;

        if (readTokenIssuedAtUtc == DateTime.MinValue)
            return true;

        double ageMinutes = (DateTime.UtcNow - readTokenIssuedAtUtc).TotalMinutes;
        return ageMinutes >= readTokenRefreshMinutes;
    }

    private void MarkReadTokenRefreshed()
    {
        readTokenIssuedAtUtc = DateTime.UtcNow;
        Debug.Log("[RoomApiController] Read token issued at UTC = " + readTokenIssuedAtUtc.ToString("O"));
    }

    private IEnumerator LoginReadApi()
    {
        if (!useDedicatedReadLogin)
        {
            if (string.IsNullOrEmpty(controlAccessToken))
                yield return StartCoroutine(LoginControlApi());

            if (!string.IsNullOrEmpty(controlAccessToken))
            {
                SyncReadTokenFromControl();
                yield break;
            }

            Debug.LogError("[RoomApiController] LoginReadApi fallback from control failed because control token is empty.");
            yield break;
        }

        if (isLoggingInRead)
            yield break;

        isLoggingInRead = true;
        readAccessToken = "";
        readAccessTokenType = "Bearer";

        string url = GetResolvedReadLoginUrl();
        string user = GetReadUsername();
        string pass = GetReadPassword();

        string jsonBody = JsonUtility.ToJson(new LoginRequest
        {
            username = user,
            password = pass
        });

        string formBody = "username=" + UnityWebRequest.EscapeURL(user) +
                          "&password=" + UnityWebRequest.EscapeURL(pass);

        string[] candidateBodies = new string[]
        {
            jsonBody,
            formBody
        };

        string[] candidateContentTypes = new string[]
        {
            "application/json",
            "application/x-www-form-urlencoded"
        };

        for (int i = 0; i < candidateBodies.Length; i++)
        {
            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                byte[] bodyBytes = Encoding.UTF8.GetBytes(candidateBodies[i]);
                request.timeout = readLoginTimeout;
                request.uploadHandler = new UploadHandlerRaw(bodyBytes);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", candidateContentTypes[i]);
                request.SetRequestHeader("Accept", "application/json");
                request.SetRequestHeader("Cache-Control", "no-cache");
                request.SetRequestHeader("Pragma", "no-cache");

                Debug.Log("[RoomApiController] Read Login URL = " + url);
                Debug.Log("[RoomApiController] Read Login Content-Type = " + candidateContentTypes[i]);
                Debug.Log("[RoomApiController] Read Login Body = " + candidateBodies[i]);

                float startedAt = Time.realtimeSinceStartup;
                yield return request.SendWebRequest();
                float elapsed = Time.realtimeSinceStartup - startedAt;

                string responseText = SafeResponseText(request);

                Debug.Log("[RoomApiController] Read Login elapsed = " + elapsed.ToString("0.00") + "s");
                Debug.Log("[RoomApiController] Read Login Result = " + request.result);
                Debug.Log("[RoomApiController] Read Login Response Code = " + request.responseCode);
                Debug.Log("[RoomApiController] Read Login Response Body = " + responseText);

                if (request.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("[RoomApiController] Read Login attempt failed: " +
                                     request.error +
                                     " | code=" + request.responseCode +
                                     " | body=" + responseText);
                    continue;
                }

                string parsedToken = "";
                string parsedType = "";

                if (TryExtractTokenInfo(responseText, out parsedToken, out parsedType))
                {
                    readAccessToken = parsedToken;
                    readAccessTokenType = NormalizeTokenType(parsedType);
                    MarkReadTokenRefreshed();

                    Debug.Log("[RoomApiController] Read Login thành công. token_type = " + readAccessTokenType);
                    Debug.Log("[RoomApiController] Read token preview = " + GetTokenPreview(readAccessToken));
                    isLoggingInRead = false;
                    yield break;
                }

                Debug.LogWarning("[RoomApiController] Read Login success nhưng không parse được token: " + responseText);
            }
        }

        Debug.LogError("[RoomApiController] Read Login thất bại: không lấy được token từ " + url);
        isLoggingInRead = false;
    }

    private IEnumerator LoginControlApi()
    {
        if (isLoggingInControl)
            yield break;

        isLoggingInControl = true;
        controlAccessToken = "";
        controlAccessTokenType = "Bearer";

        string url = controlBaseUrl.TrimEnd('/') + "/auth/login";

        LoginRequest loginData = new LoginRequest
        {
            username = username,
            password = password
        };

        string jsonBody = JsonUtility.ToJson(loginData);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.timeout = controlTimeout;
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Cache-Control", "no-cache");
            request.SetRequestHeader("Pragma", "no-cache");

            Debug.Log("[RoomApiController] Control Login URL = " + url);
            Debug.Log("[RoomApiController] Control Login Body = " + jsonBody);

            float startedAt = Time.realtimeSinceStartup;
            yield return request.SendWebRequest();
            float elapsed = Time.realtimeSinceStartup - startedAt;

            string responseText = SafeResponseText(request);

            Debug.Log("[RoomApiController] Control Login elapsed = " + elapsed.ToString("0.00") + "s");
            Debug.Log("[RoomApiController] Control Login Result = " + request.result);
            Debug.Log("[RoomApiController] Control Login Response Code = " + request.responseCode);
            Debug.Log("[RoomApiController] Control Login Response Body = " + responseText);

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[RoomApiController] Control Login Error: " +
                               request.error +
                               " | result=" + request.result +
                               " | code=" + request.responseCode +
                               " | body=" + responseText);

                controlAccessToken = "";
                controlAccessTokenType = "Bearer";
                isLoggingInControl = false;
                yield break;
            }

            string parsedToken = "";
            string parsedType = "";

            if (!TryExtractTokenInfo(responseText, out parsedToken, out parsedType))
            {
                Debug.LogError("[RoomApiController] Control Login success nhưng không parse được token: " + responseText);
                controlAccessToken = "";
                controlAccessTokenType = "Bearer";
                isLoggingInControl = false;
                yield break;
            }

            controlAccessToken = parsedToken;
            controlAccessTokenType = NormalizeTokenType(parsedType);

            Debug.Log("[RoomApiController] Control Login thành công. token_type = " + controlAccessTokenType);
            Debug.Log("[RoomApiController] Control token preview = " + GetTokenPreview(controlAccessToken));

            if (!useDedicatedReadLogin)
                SyncReadTokenFromControl();
        }

        isLoggingInControl = false;
    }

    private bool TryExtractTokenInfo(string responseText, out string token, out string tokenType)
    {
        token = "";
        tokenType = "Bearer";

        if (string.IsNullOrEmpty(responseText))
            return false;

        LoginResponse loginResponse = null;

        try
        {
            loginResponse = JsonUtility.FromJson<LoginResponse>(responseText);
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[RoomApiController] Parse token JSON warning: " + ex.Message);
        }

        if (loginResponse != null)
        {
            if (!string.IsNullOrEmpty(loginResponse.access_token))
                token = loginResponse.access_token;
            else if (!string.IsNullOrEmpty(loginResponse.token))
                token = loginResponse.token;

            if (!string.IsNullOrEmpty(loginResponse.token_type))
                tokenType = loginResponse.token_type;
        }

        if (string.IsNullOrEmpty(token))
            token = ExtractJsonStringValue(responseText, "access_token");

        if (string.IsNullOrEmpty(token))
            token = ExtractJsonStringValue(responseText, "token");

        if (string.IsNullOrEmpty(token))
            token = ExtractJsonStringValue(responseText, "accessToken");

        string extractedType = ExtractJsonStringValue(responseText, "token_type");
        if (string.IsNullOrEmpty(extractedType))
            extractedType = ExtractJsonStringValue(responseText, "tokenType");

        if (!string.IsNullOrEmpty(extractedType))
            tokenType = extractedType;

        return !string.IsNullOrEmpty(token);
    }

    private IEnumerator GetRoomData(Action<bool> onDone)
    {
        if (isReadingRoomData)
        {
            onDone?.Invoke(false);
            yield break;
        }

        isReadingRoomData = true;
        bool success = false;
        string lastUnauthorizedMessage = "";

        if (!useDedicatedReadLogin)
        {
            if (string.IsNullOrEmpty(controlAccessToken) || ShouldRefreshReadToken())
            {
                Debug.Log("[RoomApiController] Control token empty/expired, login control again...");
                yield return StartCoroutine(LoginControlApi());
            }

            if (!string.IsNullOrEmpty(controlAccessToken))
                SyncReadTokenFromControl();
        }
        else
        {
            if (ShouldRefreshReadToken())
            {
                Debug.Log("[RoomApiController] Read token needs refresh. Logging in again...");
                yield return StartCoroutine(LoginReadApi());
            }
        }

        for (int loginAttempt = 0; loginAttempt < 2; loginAttempt++)
        {
            if (string.IsNullOrEmpty(readAccessToken))
                yield return StartCoroutine(LoginReadApi());

            if (string.IsNullOrEmpty(readAccessToken))
            {
                Debug.LogError("[RoomApiController] Không có read token để đọc dữ liệu");

                if (roomInfoUI != null)
                    roomInfoUI.SetConnectionStatus("Lỗi token đọc");

                isReadingRoomData = false;
                onDone?.Invoke(false);
                yield break;
            }

            List<ReadAuthMode> authModesToTry = GetReadAuthModesToTry();
            bool sawUnauthorized = false;

            for (int modeIndex = 0; modeIndex < authModesToTry.Count; modeIndex++)
            {
                ReadAuthMode currentMode = authModesToTry[modeIndex];

                for (int requestAttempt = 0; requestAttempt < Mathf.Max(1, readRequestRetryCount); requestAttempt++)
                {
                    string finalReadUrl = BuildReadApiUrl(currentMode);

                    using (UnityWebRequest request = UnityWebRequest.Get(finalReadUrl))
                    {
                        request.timeout = Mathf.Max(requestTimeout, 20);
                        request.downloadHandler = new DownloadHandlerBuffer();
                        request.SetRequestHeader("Accept", "application/json");
                        request.SetRequestHeader("Cache-Control", "no-cache");
                        request.SetRequestHeader("Pragma", "no-cache");
                        ApplyReadAuth(request, currentMode);

                        float startedAt = Time.realtimeSinceStartup;

                        Debug.Log("[RoomApiController] Read Mode = " + currentMode);
                        Debug.Log("[RoomApiController] Read Attempt = " + (requestAttempt + 1) + "/" + Mathf.Max(1, readRequestRetryCount));
                        Debug.Log("[RoomApiController] Read URL = " + finalReadUrl);
                        Debug.Log("[RoomApiController] Read using token preview = " + GetTokenPreview(readAccessToken));
                        Debug.Log("[RoomApiController] Read Auth Header = " +
                                  ((currentMode == ReadAuthMode.AuthorizationHeader || currentMode == ReadAuthMode.HeaderAndQuery)
                                      ? BuildAuthorizationHeaderPreview(readAccessToken, readAccessTokenType)
                                      : "(none)"));

                        yield return request.SendWebRequest();

                        float elapsed = Time.realtimeSinceStartup - startedAt;
                        long code = request.responseCode;
                        string json = SafeResponseText(request);
                        bool hasUsableBody = code == 200 && !string.IsNullOrEmpty(json);
                        bool isTimeout = IsTimeoutError(request.error);

                        Debug.Log("[RoomApiController] Read elapsed = " + elapsed.ToString("0.00") + "s");
                        Debug.Log("[RoomApiController] Read Result = " + request.result);
                        Debug.Log("[RoomApiController] Read Response Code = " + code);
                        Debug.Log("[RoomApiController] Read Error = " + request.error);
                        Debug.Log("[RoomApiController] Read Response Body = " + json);

                        if (IsUnauthorized(code))
                        {
                            string detail = ExtractErrorDetail(json);
                            lastUnauthorizedMessage = string.IsNullOrEmpty(detail) ? "Invalid token" : detail;
                            sawUnauthorized = true;

                            Debug.LogWarning("[RoomApiController] Read API bị 401/403 với mode=" + currentMode +
                                             " | code=" + code +
                                             " | detail=" + lastUnauthorizedMessage +
                                             " | body=" + json);
                            break;
                        }

                        if (request.result != UnityWebRequest.Result.Success && !hasUsableBody)
                        {
                            Debug.LogError("[RoomApiController] API Error: " +
                                           request.error +
                                           " | result=" + request.result +
                                           " | code=" + code +
                                           " | elapsed=" + elapsed.ToString("0.00") + "s" +
                                           " | body=" + json);

                            if (isTimeout && requestAttempt < Mathf.Max(1, readRequestRetryCount) - 1)
                            {
                                Debug.LogWarning("[RoomApiController] Read request timeout, retry sau " + retryDelaySeconds + "s");
                                yield return new WaitForSeconds(retryDelaySeconds);
                                continue;
                            }

                            consecutiveReadFailures++;

                            if (roomInfoUI != null)
                            {
                                if (consecutiveReadFailures >= maxConsecutiveReadFailuresBeforeDisconnect)
                                    roomInfoUI.SetConnectionStatus("Mất kết nối dữ liệu");
                                else
                                    roomInfoUI.SetConnectionStatus("Đang thử kết nối lại...");
                            }

                            isReadingRoomData = false;
                            onDone?.Invoke(false);
                            yield break;
                        }

                        if (request.result != UnityWebRequest.Result.Success && hasUsableBody)
                        {
                            Debug.LogWarning("[RoomApiController] API lỗi nhưng vẫn có body hợp lệ, tiếp tục xử lý." +
                                             " | result=" + request.result +
                                             " | code=" + code +
                                             " | error=" + request.error);
                        }

                        if (string.IsNullOrEmpty(json))
                        {
                            Debug.LogError("[RoomApiController] JSON rỗng");

                            consecutiveReadFailures++;

                            if (roomInfoUI != null)
                            {
                                if (consecutiveReadFailures >= maxConsecutiveReadFailuresBeforeDisconnect)
                                    roomInfoUI.SetConnectionStatus("Không có dữ liệu");
                                else
                                    roomInfoUI.SetConnectionStatus("Đang thử kết nối lại...");
                            }

                            isReadingRoomData = false;
                            onDone?.Invoke(false);
                            yield break;
                        }

                        consecutiveReadFailures = 0;
                        hasResolvedReadAuthMode = true;
                        resolvedReadAuthMode = currentMode;

                        json = NormalizeJson(json);

                        string gatewayBlock = GetDeviceBlock(json, gatewayDeviceId);
                        string airBlock = GetDeviceBlock(json, airConditionerDeviceId);

                        bool foundGateway = !string.IsNullOrEmpty(gatewayBlock);
                        bool foundAir = !string.IsNullOrEmpty(airBlock);

                        if (foundGateway)
                            UpdateGatewayInfo(gatewayBlock);
                        else
                            Debug.LogWarning("[RoomApiController] Không tìm thấy gateway điện: " + gatewayDeviceId);

                        if (foundAir)
                            UpdateAirConditionerInfo(airBlock);
                        else if (roomInfoUI != null)
                        {
                            roomInfoUI.SetAirConditionerState("--");
                            roomInfoUI.SetAirConditionerSetTemp("--");
                            roomInfoUI.SetAirConditionerIndoorTemp("--");
                            roomInfoUI.SetAirConditionerHumidity("--");
                        }

                        if (roomInfoUI != null)
                        {
                            if (foundGateway || foundAir)
                                roomInfoUI.SetConnectionStatus("Đã kết nối dữ liệu");
                            else
                                roomInfoUI.SetConnectionStatus("API có trả về nhưng không khớp device_id");
                        }

                        success = foundGateway || foundAir;
                        isReadingRoomData = false;
                        onDone?.Invoke(success);
                        yield break;
                    }
                }
            }

            if (!sawUnauthorized)
                break;

            Debug.LogWarning("[RoomApiController] Token đọc bị từ chối. Login lại...");
            readAccessToken = "";
            readAccessTokenType = "Bearer";
            readTokenIssuedAtUtc = DateTime.MinValue;
            hasResolvedReadAuthMode = false;

            if (!useDedicatedReadLogin)
            {
                controlAccessToken = "";
                controlAccessTokenType = "Bearer";
            }
        }

        Debug.LogError("[RoomApiController] Đọc dữ liệu thất bại sau khi retry login. Last detail = " + lastUnauthorizedMessage);

        if (roomInfoUI != null)
            roomInfoUI.SetConnectionStatus(string.IsNullOrEmpty(lastUnauthorizedMessage) ? "Không đọc được dữ liệu" : lastUnauthorizedMessage);

        isReadingRoomData = false;
        onDone?.Invoke(success);
    }

    private List<ReadAuthMode> GetReadAuthModesToTry()
    {
        List<ReadAuthMode> modes = new List<ReadAuthMode>();

        if (readAuthMode == ReadAuthMode.AuthorizationHeader)
        {
            modes.Add(ReadAuthMode.AuthorizationHeader);
            return modes;
        }

        if (readAuthMode == ReadAuthMode.QueryToken)
        {
            modes.Add(ReadAuthMode.QueryToken);
            return modes;
        }

        if (readAuthMode == ReadAuthMode.HeaderAndQuery)
        {
            modes.Add(ReadAuthMode.HeaderAndQuery);
            return modes;
        }

        if (hasResolvedReadAuthMode)
            modes.Add(resolvedReadAuthMode);

        if (!modes.Contains(ReadAuthMode.QueryToken))
            modes.Add(ReadAuthMode.QueryToken);

        if (!modes.Contains(ReadAuthMode.HeaderAndQuery))
            modes.Add(ReadAuthMode.HeaderAndQuery);

        if (!modes.Contains(ReadAuthMode.AuthorizationHeader))
            modes.Add(ReadAuthMode.AuthorizationHeader);

        return modes;
    }

    private string BuildReadApiUrl(ReadAuthMode mode)
    {
        string key = string.IsNullOrWhiteSpace(readTokenQueryKey) ? "token" : readTokenQueryKey.Trim();
        string url = RemoveQueryParam(apiUrl.Trim(), key);

        bool shouldAttachQuery =
            mode == ReadAuthMode.QueryToken ||
            mode == ReadAuthMode.HeaderAndQuery;

        if (!shouldAttachQuery || string.IsNullOrEmpty(readAccessToken))
            return url;

        return AppendQueryParam(url, key, readAccessToken);
    }

    private void ApplyReadAuth(UnityWebRequest request, ReadAuthMode mode)
    {
        if (request == null)
            return;

        bool shouldAttachHeader =
            mode == ReadAuthMode.AuthorizationHeader ||
            mode == ReadAuthMode.HeaderAndQuery;

        if (!shouldAttachHeader)
            return;

        string authHeader = BuildAuthorizationHeaderValue(readAccessToken, readAccessTokenType);
        if (!string.IsNullOrEmpty(authHeader))
            request.SetRequestHeader("Authorization", authHeader);
    }

    private string BuildAuthorizationHeaderValue(string token, string tokenType)
    {
        if (string.IsNullOrEmpty(token))
            return "";

        string scheme = NormalizeTokenType(tokenType);
        return scheme + " " + token;
    }

    private string BuildAuthorizationHeaderPreview(string token, string tokenType)
    {
        if (string.IsNullOrEmpty(token))
            return "(empty)";

        return NormalizeTokenType(tokenType) + " " + GetTokenPreview(token);
    }

    private string NormalizeTokenType(string rawTokenType)
    {
        if (string.IsNullOrWhiteSpace(rawTokenType))
            return "Bearer";

        string value = rawTokenType.Trim();

        if (string.Equals(value, "bearer", StringComparison.OrdinalIgnoreCase))
            return "Bearer";

        return value;
    }

    private string GetTokenPreview(string token)
    {
        if (string.IsNullOrEmpty(token))
            return "(empty)";

        int len = Mathf.Min(20, token.Length);
        return token.Substring(0, len) + "...";
    }

    private bool IsUnauthorized(long code)
    {
        return code == 401 || code == 403;
    }

    private bool IsTimeoutError(string error)
    {
        if (string.IsNullOrEmpty(error))
            return false;

        string e = error.Trim().ToLowerInvariant();
        return e.Contains("timeout") || e.Contains("timed out") || e.Contains("request timeout");
    }

    private string NormalizeJson(string json)
    {
        if (string.IsNullOrEmpty(json))
            return "";
        return json.Replace("\n", "").Replace("\r", "").Replace("\t", "");
    }

    private void ApplyDataUnavailableUI()
    {
        if (roomInfoUI != null)
        {
            roomInfoUI.SetVoltage("--");
            roomInfoUI.SetCurrent("--");
            roomInfoUI.SetPower("--");
            roomInfoUI.SetEnergy("--");
            roomInfoUI.SetFrequency("--");
            roomInfoUI.SetPowerFactor("--");

            roomInfoUI.SetAirConditionerState("--");
            roomInfoUI.SetAirConditionerSetTemp("--");
            roomInfoUI.SetAirConditionerIndoorTemp("--");
            roomInfoUI.SetAirConditionerHumidity("--");

            roomInfoUI.SetConnectionStatus(hasLoggedInSuccessfully ? "Mất kết nối dữ liệu" : "Login lỗi");

            if (!isSendingRelayControl)
            {
                roomInfoUI.SetLightBusy(1, false);
                roomInfoUI.SetLightBusy(2, false);
                roomInfoUI.SetLightBusy(3, false);
            }

            if (!isSendingAcControl)
            {
                roomInfoUI.SetAirConditionerBusy(false);
                roomInfoUI.SetAirConditionerTempButtonsBusy(false);
            }
        }
    }

    private void UpdateGatewayInfo(string gatewayBlock)
    {
        string connectionStatus = GetTopLevelString(gatewayBlock, "trang_thai");
        if (string.IsNullOrEmpty(connectionStatus))
            connectionStatus = GetFlexibleValue(gatewayBlock, "state");

        string voltage = GetFlexibleValue(gatewayBlock, "voltage");
        if (string.IsNullOrEmpty(voltage)) voltage = GetFlexibleValue(gatewayBlock, "dien_ap");
        if (string.IsNullOrEmpty(voltage)) voltage = GetFlexibleValue(gatewayBlock, "data_voltage");

        string current = GetFlexibleValue(gatewayBlock, "current");
        if (string.IsNullOrEmpty(current)) current = GetFlexibleValue(gatewayBlock, "dong_dien");
        if (string.IsNullOrEmpty(current)) current = GetFlexibleValue(gatewayBlock, "data_current");

        string power = GetFlexibleValue(gatewayBlock, "power");
        if (string.IsNullOrEmpty(power)) power = GetFlexibleValue(gatewayBlock, "cong_suat");
        if (string.IsNullOrEmpty(power)) power = GetFlexibleValue(gatewayBlock, "data_power");

        string energy = GetFlexibleValue(gatewayBlock, "energy");
        if (string.IsNullOrEmpty(energy)) energy = GetFlexibleValue(gatewayBlock, "energy_kwh");
        if (string.IsNullOrEmpty(energy)) energy = GetFlexibleValue(gatewayBlock, "data_energy");

        string frequency = GetFlexibleValue(gatewayBlock, "frequency");
        if (string.IsNullOrEmpty(frequency)) frequency = GetFlexibleValue(gatewayBlock, "tan_so");
        if (string.IsNullOrEmpty(frequency)) frequency = GetFlexibleValue(gatewayBlock, "data_frequency");

        string powerFactor = GetFlexibleValue(gatewayBlock, "power_factor");
        if (string.IsNullOrEmpty(powerFactor)) powerFactor = GetFlexibleValue(gatewayBlock, "he_so_cong_suat");
        if (string.IsNullOrEmpty(powerFactor)) powerFactor = GetFlexibleValue(gatewayBlock, "data_power_factor");

        string relay1State = GetFlexibleValue(gatewayBlock, "relay_1_state");
        if (string.IsNullOrEmpty(relay1State)) relay1State = GetFlexibleValue(gatewayBlock, "data_relay_1_state");

        string relay2State = GetFlexibleValue(gatewayBlock, "relay_2_state");
        if (string.IsNullOrEmpty(relay2State)) relay2State = GetFlexibleValue(gatewayBlock, "data_relay_2_state");

        string relay3State = GetFlexibleValue(gatewayBlock, "relay_3_state");
        if (string.IsNullOrEmpty(relay3State)) relay3State = GetFlexibleValue(gatewayBlock, "data_relay_3_state");

        bool relay1On = IsStateOn(relay1State);
        bool relay2On = IsStateOn(relay2State);
        bool relay3On = IsStateOn(relay3State);

        UpdateRelayLights(relay1Lights, relay1On, ref lastRelay1State, "Relay 1");
        UpdateRelayLights(relay2Lights, relay2On, ref lastRelay2State, "Relay 2");
        UpdateRelayLights(relay3Lights, relay3On, ref lastRelay3State, "Relay 3");

        if (roomInfoUI != null)
        {
            roomInfoUI.SetVoltage(FormatNumber(voltage));
            roomInfoUI.SetCurrent(FormatNumber(current));
            roomInfoUI.SetPower(FormatNumber(power));
            roomInfoUI.SetEnergy(FormatNumber(energy));
            roomInfoUI.SetFrequency(FormatNumber(frequency));
            roomInfoUI.SetPowerFactor(FormatNumber(powerFactor));

            roomInfoUI.SetLight1State(string.IsNullOrEmpty(relay1State) ? "--" : relay1State);
            roomInfoUI.SetLight2State(string.IsNullOrEmpty(relay2State) ? "--" : relay2State);
            roomInfoUI.SetLight3State(string.IsNullOrEmpty(relay3State) ? "--" : relay3State);

            if (!isSendingRelayControl)
            {
                roomInfoUI.SetLightBusy(1, false);
                roomInfoUI.SetLightBusy(2, false);
                roomInfoUI.SetLightBusy(3, false);
            }

            roomInfoUI.SetConnectionStatus(string.IsNullOrEmpty(connectionStatus) ? "Đã kết nối dữ liệu" : connectionStatus);
        }
    }

    private void UpdateAirConditionerInfo(string airBlock)
    {
        string onValue = GetFlexibleValue(airBlock, "relay_5_state");
        if (string.IsNullOrEmpty(onValue)) onValue = GetFlexibleValue(airBlock, "ac_on");
        if (string.IsNullOrEmpty(onValue)) onValue = GetFlexibleValue(airBlock, "on");
        if (string.IsNullOrEmpty(onValue)) onValue = GetFlexibleValue(airBlock, "power");
        if (string.IsNullOrEmpty(onValue)) onValue = GetFlexibleValue(airBlock, "running");
        if (string.IsNullOrEmpty(onValue)) onValue = GetFlexibleValue(airBlock, "state");

        string setTemp = GetFlexibleValue(airBlock, "ac_setpoint_temp");
        if (string.IsNullOrEmpty(setTemp)) setTemp = GetFlexibleValue(airBlock, "temp");

        string indoorTemp = GetFlexibleValue(airBlock, "indoor_temp");
        if (string.IsNullOrEmpty(indoorTemp)) indoorTemp = GetFlexibleValue(airBlock, "indoorTemp");

        string humidity = GetFlexibleValue(airBlock, "indoor_humidity");
        if (string.IsNullOrEmpty(humidity)) humidity = GetFlexibleValue(airBlock, "humidity");

        bool hasResolvedState = !string.IsNullOrEmpty(onValue);
        bool acOn = hasResolvedState && IsAirConditionerOn(onValue);

        string finalState = hasResolvedState ? (acOn ? "ON" : "OFF") : "--";
        lastAirConditionerState = hasResolvedState ? acOn : (bool?)null;

        int parsedTemp;
        if (int.TryParse(setTemp, NumberStyles.Any, CultureInfo.InvariantCulture, out parsedTemp))
            lastAirConditionerSetTemp = parsedTemp;
        else if (int.TryParse(setTemp, NumberStyles.Any, CultureInfo.CurrentCulture, out parsedTemp))
            lastAirConditionerSetTemp = parsedTemp;
        else
            lastAirConditionerSetTemp = null;

        Debug.Log("[RoomApiController] AC raw state = " + onValue);
        Debug.Log("[RoomApiController] AC final state = " + finalState);
        Debug.Log("[RoomApiController] AC set temp = " + setTemp);
        Debug.Log("[RoomApiController] AC indoor temp = " + indoorTemp);
        Debug.Log("[RoomApiController] AC humidity = " + humidity);

        if (roomInfoUI != null)
        {
            roomInfoUI.SetAirConditionerState(finalState);
            roomInfoUI.SetAirConditionerSetTemp(FormatNumber(setTemp));
            roomInfoUI.SetAirConditionerIndoorTemp(FormatNumber(indoorTemp));
            roomInfoUI.SetAirConditionerHumidity(FormatNumber(humidity));

            if (!isSendingAcControl)
            {
                roomInfoUI.SetAirConditionerBusy(false);
                roomInfoUI.SetAirConditionerTempButtonsBusy(false);
            }
        }
    }

    private void UpdateRelayLights(Light[] targetLights, bool isOn, ref bool? lastState, string relayName)
    {
        if (lastState.HasValue && lastState.Value == isOn)
            return;

        lastState = isOn;

        if (targetLights == null || targetLights.Length == 0)
        {
            Debug.LogWarning("[RoomApiController] Chưa gán đèn cho " + relayName);
            return;
        }

        foreach (Light lightObj in targetLights)
        {
            if (lightObj != null)
                lightObj.enabled = isOn;
        }
    }

    private bool IsStateOn(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        string v = value.Trim().ToUpperInvariant();
        return v == "ON" || v == "TRUE" || v == "1";
    }

    private bool IsAirConditionerOn(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string v = value.Trim().ToLowerInvariant();

        if (v == "1" || v == "true" || v == "on" || v == "enabled")
            return true;

        if (v == "0" || v == "false" || v == "off" || v == "disabled")
            return false;

        if (v == "cool" || v == "heat" || v == "dry" || v == "fan" || v == "auto")
            return true;

        return false;
    }

    private string FormatNumber(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "--";

        float f;
        if (float.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out f))
        {
            if (Mathf.Abs(f) >= 1000f)
                return f.ToString("#,0.##", CultureInfo.InvariantCulture);

            return f.ToString("0.##", CultureInfo.InvariantCulture);
        }

        return raw;
    }

    private string GetDeviceBlock(string json, string deviceId)
    {
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(deviceId))
            return "";

        string marker = "\"device_id\":\"" + deviceId + "\"";
        int start = json.IndexOf(marker, StringComparison.Ordinal);

        if (start == -1)
        {
            marker = "\"device_id\": \"" + deviceId + "\"";
            start = json.IndexOf(marker, StringComparison.Ordinal);
        }

        if (start == -1)
            return "";

        int braceStart = json.LastIndexOf("{", start, StringComparison.Ordinal);
        if (braceStart == -1)
            return "";

        int depth = 0;
        bool inString = false;

        for (int i = braceStart; i < json.Length; i++)
        {
            char c = json[i];

            if (c == '"' && (i == 0 || json[i - 1] != '\\'))
                inString = !inString;

            if (inString)
                continue;

            if (c == '{') depth++;
            else if (c == '}') depth--;

            if (depth == 0)
                return json.Substring(braceStart, i - braceStart + 1);
        }

        return "";
    }

    private string GetTopLevelString(string deviceBlock, string fieldName)
    {
        return ExtractSimpleFieldValue(deviceBlock, fieldName);
    }

    private string GetFlexibleValue(string block, string key)
    {
        if (string.IsNullOrEmpty(block) || string.IsNullOrEmpty(key))
            return "";

        string nested = ExtractNestedValue(block, key);
        if (!string.IsNullOrEmpty(nested))
            return nested;

        string simple = ExtractSimpleFieldValue(block, key);
        if (!string.IsNullOrEmpty(simple))
            return simple;

        return "";
    }

    private string ExtractNestedValue(string block, string key)
    {
        int keyIndex = FindJsonKey(block, key);
        if (keyIndex < 0)
            return "";

        int colonIndex = FindColonAfterKey(block, keyIndex);
        if (colonIndex < 0)
            return "";

        int valueStart = SkipWhitespace(block, colonIndex + 1);
        if (valueStart < 0 || valueStart >= block.Length || block[valueStart] != '{')
            return "";

        int objectEnd = FindMatchingBrace(block, valueStart);
        if (objectEnd < 0)
            return "";

        string objectText = block.Substring(valueStart, objectEnd - valueStart + 1);
        return ExtractSimpleFieldValue(objectText, "value");
    }

    private string ExtractSimpleFieldValue(string json, string key)
    {
        int keyIndex = FindJsonKey(json, key);
        if (keyIndex < 0)
            return "";

        int colonIndex = FindColonAfterKey(json, keyIndex);
        if (colonIndex < 0)
            return "";

        int i = SkipWhitespace(json, colonIndex + 1);
        if (i < 0 || i >= json.Length)
            return "";

        if (json[i] == '"')
        {
            int endQuote = FindStringEnd(json, i);
            if (endQuote < 0)
                return "";
            return json.Substring(i + 1, endQuote - i - 1);
        }

        if (json[i] == '{' || json[i] == '[')
            return "";

        int end = i;
        while (end < json.Length && json[end] != ',' && json[end] != '}' && json[end] != ']')
            end++;

        if (end <= i)
            return "";

        return json.Substring(i, end - i).Trim();
    }

    private int FindJsonKey(string json, string key)
    {
        string marker1 = "\"" + key + "\":";
        int idx = json.IndexOf(marker1, StringComparison.Ordinal);
        if (idx >= 0) return idx;

        string marker2 = "\"" + key + "\" :";
        idx = json.IndexOf(marker2, StringComparison.Ordinal);
        if (idx >= 0) return idx;

        return -1;
    }

    private int FindColonAfterKey(string json, int keyIndex)
    {
        if (keyIndex < 0 || keyIndex >= json.Length)
            return -1;

        return json.IndexOf(':', keyIndex);
    }

    private int SkipWhitespace(string text, int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
            index++;
        return index;
    }

    private int FindStringEnd(string text, int startQuoteIndex)
    {
        for (int i = startQuoteIndex + 1; i < text.Length; i++)
        {
            if (text[i] == '"' && text[i - 1] != '\\')
                return i;
        }
        return -1;
    }

    private int FindMatchingBrace(string text, int startIndex)
    {
        int depth = 0;
        bool inString = false;

        for (int i = startIndex; i < text.Length; i++)
        {
            char c = text[i];

            if (c == '"' && (i == 0 || text[i - 1] != '\\'))
                inString = !inString;

            if (inString)
                continue;

            if (c == '{') depth++;
            else if (c == '}') depth--;

            if (depth == 0)
                return i;
        }

        return -1;
    }

    private void ApplyRelayStateLocal(int relayNumber, bool isOn)
    {
        string state = isOn ? "ON" : "OFF";

        switch (relayNumber)
        {
            case 1:
                UpdateRelayLights(relay1Lights, isOn, ref lastRelay1State, "Relay 1");
                if (roomInfoUI != null) roomInfoUI.SetLight1State(state);
                break;

            case 2:
                UpdateRelayLights(relay2Lights, isOn, ref lastRelay2State, "Relay 2");
                if (roomInfoUI != null) roomInfoUI.SetLight2State(state);
                break;

            case 3:
                UpdateRelayLights(relay3Lights, isOn, ref lastRelay3State, "Relay 3");
                if (roomInfoUI != null) roomInfoUI.SetLight3State(state);
                break;
        }
    }

    private void ApplyAirConditionerStateLocal(bool isOn)
    {
        lastAirConditionerState = isOn;

        if (roomInfoUI != null)
            roomInfoUI.SetAirConditionerState(isOn ? "ON" : "OFF");
    }

    public void ToggleLight1()
    {
        bool currentIsOn = lastRelay1State.HasValue && lastRelay1State.Value;
        StartCoroutine(SetRelayFromUI(1, !currentIsOn, 1));
    }

    public void ToggleLight2()
    {
        bool currentIsOn = lastRelay2State.HasValue && lastRelay2State.Value;
        StartCoroutine(SetRelayFromUI(2, !currentIsOn, 2));
    }

    public void ToggleLight3()
    {
        bool currentIsOn = lastRelay3State.HasValue && lastRelay3State.Value;
        StartCoroutine(SetRelayFromUI(3, !currentIsOn, 3));
    }

    public void ToggleAirConditioner()
    {
        bool currentIsOn = lastAirConditionerState.HasValue && lastAirConditionerState.Value;
        StartCoroutine(SetAirConditionerFromUI(!currentIsOn));
    }

    public void TurnOnAirConditioner()
    {
        StartCoroutine(SetAirConditionerFromUI(true));
    }

    public void TurnOffAirConditioner()
    {
        StartCoroutine(SetAirConditionerFromUI(false));
    }

    public void IncreaseAirConditionerTemp()
    {
        Debug.Log("[RoomApiController] Clicked TEMP UP");

        if (lastAirConditionerSetTemp.HasValue && lastAirConditionerSetTemp.Value >= maxAcTemp)
        {
            Debug.LogWarning("[RoomApiController] AC temp already at max: " + maxAcTemp);
            return;
        }

        StartCoroutine(ChangeAirConditionerTemp("up"));
    }

    public void DecreaseAirConditionerTemp()
    {
        Debug.Log("[RoomApiController] Clicked TEMP DOWN");

        if (lastAirConditionerSetTemp.HasValue && lastAirConditionerSetTemp.Value <= minAcTemp)
        {
            Debug.LogWarning("[RoomApiController] AC temp already at min: " + minAcTemp);
            return;
        }

        StartCoroutine(ChangeAirConditionerTemp("down"));
    }

    private IEnumerator SetRelayFromUI(int relayNumber, bool newState, int lightIndex)
    {
        if (isSendingRelayControl)
            yield break;

        isSendingRelayControl = true;

        if (roomInfoUI != null)
            roomInfoUI.SetLightBusy(lightIndex, true);

        ApplyRelayStateLocal(relayNumber, newState);

        bool success = false;
        yield return StartCoroutine(SetRelay(relayNumber, newState, gatewayDeviceId, result => success = result));

        if (!success)
        {
            yield return StartCoroutine(GetRoomData(null));
        }
        else
        {
            if (skipNextPollAfterManualControl)
                nextAllowedPollTime = Time.time + Mathf.Max(acRefreshDelayAfterControl, 0.25f);

            yield return new WaitForSeconds(0.05f);
        }

        if (roomInfoUI != null)
            roomInfoUI.SetLightBusy(lightIndex, false);

        isSendingRelayControl = false;
    }

    private IEnumerator SetAirConditionerFromUI(bool newState)
    {
        if (isSendingAcControl)
            yield break;

        isSendingAcControl = true;

        if (roomInfoUI != null)
            roomInfoUI.SetAirConditionerBusy(true);

        ApplyAirConditionerStateLocal(newState);

        bool success = false;
        yield return StartCoroutine(ControlAc(newState ? "on" : "off", result => success = result));

        if (!success)
        {
            yield return StartCoroutine(GetRoomData(null));
        }
        else
        {
            if (skipNextPollAfterManualControl)
                nextAllowedPollTime = Time.time + Mathf.Max(acRefreshDelayAfterControl, 0.25f);

            yield return new WaitForSeconds(acRefreshDelayAfterControl);
            yield return StartCoroutine(GetRoomData(null));
        }

        if (roomInfoUI != null)
            roomInfoUI.SetAirConditionerBusy(false);

        isSendingAcControl = false;
    }

    private IEnumerator ChangeAirConditionerTemp(string command)
    {
        if (isSendingAcControl)
        {
            Debug.LogWarning("[RoomApiController] AC control is busy, ignore command: " + command);
            yield break;
        }

        isSendingAcControl = true;

        if (roomInfoUI != null)
            roomInfoUI.SetAirConditionerTempButtonsBusy(true);

        bool success = false;
        yield return StartCoroutine(ControlAcTemperature(command, result => success = result));

        if (!success)
        {
            yield return StartCoroutine(GetRoomData(null));
        }
        else
        {
            if (skipNextPollAfterManualControl)
                nextAllowedPollTime = Time.time + Mathf.Max(acRefreshDelayAfterControl, 0.25f);

            yield return new WaitForSeconds(acRefreshDelayAfterControl);
            yield return StartCoroutine(GetRoomData(null));
        }

        if (roomInfoUI != null)
            roomInfoUI.SetAirConditionerTempButtonsBusy(false);

        isSendingAcControl = false;
    }

    private IEnumerator SetRelay(int relayNumber, bool turnOn, string targetDeviceId, Action<bool> onDone)
    {
        if (string.IsNullOrEmpty(controlAccessToken))
            yield return StartCoroutine(LoginControlApi());

        if (string.IsNullOrEmpty(controlAccessToken))
        {
            Debug.LogError("[RoomApiController] Không có control token để điều khiển");
            SetControlErrorUI("Lỗi token điều khiển");
            if (onDone != null) onDone(false);
            yield break;
        }

        string endpoint = ResolveEndpointTemplate(fixedControlEndpoint);
        bool success = false;

        yield return StartCoroutine(SendControlToEndpoint(endpoint, relayNumber, turnOn, targetDeviceId, result => success = result));

        if (!success)
        {
            SetControlErrorUI("Điều khiển lỗi");
            if (onDone != null) onDone(false);
            yield break;
        }

        Debug.Log("[RoomApiController] Relay " + relayNumber + " -> " + (turnOn ? "ON" : "OFF") + " | device=" + targetDeviceId);
        if (onDone != null) onDone(true);
    }

    private IEnumerator ControlAc(string command, Action<bool> onDone)
    {
        if (command == "on")
        {
            yield return StartCoroutine(SetRelay(5, true, gatewayDeviceId, onDone));
            yield break;
        }

        if (command == "off")
        {
            yield return StartCoroutine(SetRelay(5, false, gatewayDeviceId, onDone));
            yield break;
        }

        Debug.LogError("[RoomApiController] ControlAc command không hợp lệ: " + command);
        if (onDone != null) onDone(false);
    }

    private IEnumerator ControlAcTemperature(string command, Action<bool> onDone)
    {
        int relayNumber = 0;

        if (command == "up")
            relayNumber = 6;
        else if (command == "down")
            relayNumber = 7;

        if (relayNumber == 0)
        {
            Debug.LogError("[RoomApiController] ControlAcTemperature command không hợp lệ: " + command);
            if (onDone != null) onDone(false);
            yield break;
        }

        bool pressOnOk = false;
        yield return StartCoroutine(SetRelay(relayNumber, true, gatewayDeviceId, result => pressOnOk = result));

        if (!pressOnOk)
        {
            if (onDone != null) onDone(false);
            yield break;
        }

        yield return new WaitForSeconds(0.25f);

        bool pressOffOk = false;
        yield return StartCoroutine(SetRelay(relayNumber, false, gatewayDeviceId, result => pressOffOk = result));

        if (onDone != null) onDone(pressOffOk);
    }

    private IEnumerator SendControlToEndpoint(string endpoint, int relayNumber, bool turnOn, string targetDeviceId, Action<bool> onDone)
    {
        string url = controlBaseUrl.TrimEnd('/') + endpoint;
        List<string> candidateBodies = BuildCandidateBodies(relayNumber, turnOn, targetDeviceId);

        for (int i = 0; i < candidateBodies.Count; i++)
        {
            string jsonBody = candidateBodies[i];

            using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
            {
                request.timeout = controlTimeout;
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SetRequestHeader("Authorization", BuildAuthorizationHeaderValue(controlAccessToken, controlAccessTokenType));
                request.SetRequestHeader("Accept", "application/json");
                request.SetRequestHeader("Cache-Control", "no-cache");
                request.SetRequestHeader("Pragma", "no-cache");

                Debug.Log("[RoomApiController] Control URL = " + url);
                Debug.Log("[RoomApiController] Control Body = " + jsonBody);

                float startedAt = Time.realtimeSinceStartup;
                yield return request.SendWebRequest();
                float elapsed = Time.realtimeSinceStartup - startedAt;

                long code = request.responseCode;
                string responseText = SafeResponseText(request);

                Debug.Log("[RoomApiController] Control elapsed = " + elapsed.ToString("0.00") + "s");
                Debug.Log("[RoomApiController] Control Result = " + request.result);
                Debug.Log("[RoomApiController] Control Response Code = " + code);
                Debug.Log("[RoomApiController] Control Response Body = " + responseText);

                if (request.result == UnityWebRequest.Result.Success)
                {
                    if (onDone != null) onDone(true);
                    yield break;
                }

                if (code == 401 || code == 403)
                {
                    controlAccessToken = "";
                    controlAccessTokenType = "Bearer";
                    yield return StartCoroutine(LoginControlApi());

                    if (!string.IsNullOrEmpty(controlAccessToken))
                    {
                        using (UnityWebRequest retry = new UnityWebRequest(url, "POST"))
                        {
                            retry.timeout = controlTimeout;
                            retry.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                            retry.downloadHandler = new DownloadHandlerBuffer();
                            retry.SetRequestHeader("Content-Type", "application/json");
                            retry.SetRequestHeader("Authorization", BuildAuthorizationHeaderValue(controlAccessToken, controlAccessTokenType));
                            retry.SetRequestHeader("Accept", "application/json");
                            retry.SetRequestHeader("Cache-Control", "no-cache");
                            retry.SetRequestHeader("Pragma", "no-cache");

                            float retryStartedAt = Time.realtimeSinceStartup;
                            yield return retry.SendWebRequest();
                            float retryElapsed = Time.realtimeSinceStartup - retryStartedAt;

                            Debug.Log("[RoomApiController] Control retry elapsed = " + retryElapsed.ToString("0.00") + "s");
                            Debug.Log("[RoomApiController] Control retry result = " + retry.result);
                            Debug.Log("[RoomApiController] Control retry code = " + retry.responseCode);
                            Debug.Log("[RoomApiController] Control retry body = " + SafeResponseText(retry));

                            if (retry.result == UnityWebRequest.Result.Success)
                            {
                                if (onDone != null) onDone(true);
                                yield break;
                            }
                        }
                    }
                }
            }
        }

        if (onDone != null) onDone(false);
    }

    private List<string> BuildCandidateBodies(int relayNumber, bool turnOn, string targetDeviceId)
    {
        string state = turnOn ? "ON" : "OFF";
        List<string> bodies = new List<string>();

        RelayControlRequest simple = new RelayControlRequest
        {
            relay = relayNumber,
            state = state,
            device_id = targetDeviceId
        };

        bodies.Add(JsonUtility.ToJson(simple));
        return bodies;
    }

    private string ResolveEndpointTemplate(string endpoint)
    {
        if (string.IsNullOrEmpty(endpoint))
            endpoint = "/relay/control";

        if (!endpoint.StartsWith("/"))
            endpoint = "/" + endpoint;

        return endpoint;
    }

    private string SafeResponseText(UnityWebRequest request)
    {
        if (request == null || request.downloadHandler == null)
            return "";

        try
        {
            return request.downloadHandler.text ?? "";
        }
        catch
        {
            return "";
        }
    }

    private string ExtractJsonStringValue(string json, string key)
    {
        if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(key))
            return "";

        return ExtractSimpleFieldValue(json, key);
    }

    private string ExtractErrorDetail(string json)
    {
        if (string.IsNullOrEmpty(json))
            return "";

        string detail = ExtractJsonStringValue(json, "detail");
        return string.IsNullOrEmpty(detail) ? "" : detail.Trim();
    }

    private void SetControlErrorUI(string message)
    {
        if (roomInfoUI != null)
        {
            roomInfoUI.SetConnectionStatus(message);
            roomInfoUI.SetLightBusy(1, false);
            roomInfoUI.SetLightBusy(2, false);
            roomInfoUI.SetLightBusy(3, false);
            roomInfoUI.SetAirConditionerBusy(false);
            roomInfoUI.SetAirConditionerTempButtonsBusy(false);
        }
    }

    private void HandleCursorToggle()
    {
        if (!isCursorVisible)
        {
            if (Input.GetKeyDown(interactKey))
                ShowCursor();
        }
        else
        {
            if (Input.GetKeyDown(interactKey) || Input.GetKeyDown(cancelKey))
                HideCursor();
        }
    }

    private void ShowCursor()
    {
        isCursorVisible = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void HideCursor()
    {
        isCursorVisible = false;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private string RemoveQueryParam(string url, string paramName)
    {
        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(paramName))
            return url;

        string fragment = "";
        int hashIndex = url.IndexOf('#');
        if (hashIndex >= 0)
        {
            fragment = url.Substring(hashIndex);
            url = url.Substring(0, hashIndex);
        }

        int questionIndex = url.IndexOf('?');
        if (questionIndex < 0)
            return url + fragment;

        string baseUrl = url.Substring(0, questionIndex);
        string query = url.Substring(questionIndex + 1);

        string[] parts = query.Split('&');
        List<string> kept = new List<string>();

        for (int i = 0; i < parts.Length; i++)
        {
            string part = parts[i];
            if (string.IsNullOrEmpty(part))
                continue;

            string key = part;
            int equalsIndex = part.IndexOf('=');
            if (equalsIndex >= 0)
                key = part.Substring(0, equalsIndex);

            if (!string.Equals(key, paramName, StringComparison.OrdinalIgnoreCase))
                kept.Add(part);
        }

        if (kept.Count == 0)
            return baseUrl + fragment;

        return baseUrl + "?" + string.Join("&", kept.ToArray()) + fragment;
    }

    private string AppendQueryParam(string url, string paramName, string paramValue)
    {
        if (string.IsNullOrEmpty(url))
            return url;

        string separator = url.Contains("?") ? "&" : "?";
        return url + separator + paramName + "=" + UnityWebRequest.EscapeURL(paramValue ?? "");
    }
}