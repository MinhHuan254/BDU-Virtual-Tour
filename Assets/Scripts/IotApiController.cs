using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class IotApiController : MonoBehaviour
{
    [Header("API Config")]
    public string baseUrl = "http://YOUR_SERVER_IP:8000/api";
    public string username = "admin@bdu.edu.vn";
    public string password = "yourpass";
    public string deviceId = "gateway-701e68b1";

    [Header("Debug")]
    public bool autoLoginOnStart = true;

    private string accessToken;
    public bool IsLoggedIn => !string.IsNullOrEmpty(accessToken);

    [Serializable]
    private class TokenResponse
    {
        public string access_token;
        public string token_type;
    }

    [Serializable]
    private class RelayPayload
    {
        public int relay;
        public string state;
    }

    [Serializable]
    private class ControlRequest
    {
        public string action;
        public RelayPayload raw_payload;
    }

    private IEnumerator Start()
    {
        if (autoLoginOnStart)
            yield return Login();
    }

    public IEnumerator Login(Action<bool, string> callback = null)
    {
        string url = $"{baseUrl}/token";

        WWWForm form = new WWWForm();
        form.AddField("username", username);
        form.AddField("password", password);

        using (UnityWebRequest req = UnityWebRequest.Post(url, form))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Login failed: " + req.error);
                callback?.Invoke(false, req.error);
                yield break;
            }

            string json = req.downloadHandler.text;
            TokenResponse tokenRes = JsonUtility.FromJson<TokenResponse>(json);

            if (tokenRes != null && !string.IsNullOrEmpty(tokenRes.access_token))
            {
                accessToken = tokenRes.access_token;
                Debug.Log("Login success");
                callback?.Invoke(true, "OK");
            }
            else
            {
                Debug.LogError("Login failed: invalid token response");
                callback?.Invoke(false, "Invalid token response");
            }
        }
    }

    public IEnumerator SetRelay(int relayNumber, bool turnOn, Action<bool, string> callback = null)
    {
        if (!IsLoggedIn)
        {
            yield return Login();
            if (!IsLoggedIn)
            {
                callback?.Invoke(false, "Cannot login");
                yield break;
            }
        }

        string url = $"{baseUrl}/devices/{deviceId}/control";

        ControlRequest bodyObj = new ControlRequest
        {
            action = "relay",
            raw_payload = new RelayPayload
            {
                relay = relayNumber,
                state = turnOn ? "ON" : "OFF"
            }
        };

        string jsonBody = JsonUtility.ToJson(bodyObj);

        using (UnityWebRequest req = new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);
            req.uploadHandler = new UploadHandlerRaw(bodyRaw);
            req.downloadHandler = new DownloadHandlerBuffer();

            req.SetRequestHeader("Content-Type", "application/json");
            req.SetRequestHeader("Authorization", $"Bearer {accessToken}");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"SetRelay failed: {req.error} | {req.downloadHandler.text}");
                callback?.Invoke(false, req.error);
            }
            else
            {
                Debug.Log($"Relay {relayNumber} => {(turnOn ? "ON" : "OFF")}");
                callback?.Invoke(true, req.downloadHandler.text);
            }
        }
    }
}