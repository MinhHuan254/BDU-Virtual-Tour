using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class LightApiController : MonoBehaviour
{
    public string apiUrl = "http://192.168.190.52:8000/rooms/2/data";
    public float refreshTime = 2f;
    public int requestTimeout = 5;
    public string targetLightDeviceId = "light-bdu-001";
    public Light[] roomLights;

    private bool? lastState = null;

    private void Start()
    {
        Debug.Log("[LightApiController] Start on object: " + gameObject.name);
        StartCoroutine(PollData());
    }

    private IEnumerator PollData()
    {
        while (true)
        {
            yield return StartCoroutine(GetRoomData());
            yield return new WaitForSeconds(refreshTime);
        }
    }

    private IEnumerator GetRoomData()
    {
        using (UnityWebRequest request = UnityWebRequest.Get(apiUrl))
        {
            request.timeout = requestTimeout;
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[LightApiController] API Error: " + request.error);
                yield break;
            }

            string json = request.downloadHandler.text;
            RoomResponse response = JsonUtility.FromJson<RoomResponse>(json);

            if (response == null || response.devices == null)
            {
                Debug.LogWarning("[LightApiController] Parse fail or no devices");
                yield break;
            }

            foreach (Device device in response.devices)
            {
                if (device == null || device.device_id != targetLightDeviceId)
                    continue;

                string stateValue = "";
                float brightnessValue = -1f;

                if (device.data != null && device.data.state != null && !string.IsNullOrEmpty(device.data.state.value))
                    stateValue = device.data.state.value.Trim().ToUpper();

                if (device.data != null && device.data.brightness != null)
                    brightnessValue = device.data.brightness.value;

                Debug.Log($"[LightApiController] API -> state={stateValue}, brightness={brightnessValue}");

                bool isOn = (stateValue == "ON");

                UpdateLights(isOn);
                yield break;
            }

            Debug.LogWarning("[LightApiController] Device not found: " + targetLightDeviceId);
        }
    }

    private void UpdateLights(bool isOn)
    {
        Debug.Log($"[LightApiController] UpdateLights -> {(isOn ? "ON" : "OFF")}");

        if (lastState.HasValue && lastState.Value == isOn)
            return;

        lastState = isOn;

        foreach (Light l in roomLights)
        {
            if (l != null)
            {
                Debug.Log($"[LightApiController] Set {l.name}.enabled = {isOn}");
                l.enabled = isOn;
            }
        }
    }
}

[System.Serializable]
public class RoomResponse
{
    public Device[] devices;
}

[System.Serializable]
public class Device
{
    public string device_id;
    public string ten_thiet_bi;
    public string loai_thiet_bi;
    public string trang_thai;
    public long last_seen;
    public int phong_id;
    public DeviceData data;
}

[System.Serializable]
public class DeviceData
{
    public ValueField state;
    public BrightnessField brightness;
    public CommandField cmd_on;
    public CommandField cmd_off;
    public ValueField temperature;
    public ValueField humidity;
    public ValueField setpoint;
}

[System.Serializable]
public class ValueField
{
    public string value;
    public string don_vi;
    public string mo_ta;
    public long timestamp;
}

[System.Serializable]
public class BrightnessField
{
    public float value;
    public string don_vi;
    public string mo_ta;
    public long timestamp;
}

[System.Serializable]
public class CommandField
{
    public string value;
    public string don_vi;
    public string mo_ta;
    public long timestamp;
}