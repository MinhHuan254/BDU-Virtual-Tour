using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class AutoVoiceRecorder : MonoBehaviour
{
    [Header("Mic Settings")]
    public int requestedSampleRate = 16000;
    public int micBufferSeconds = 20;

    [Header("Voice Detection")]
    public float voiceThreshold = 0.003f;
    public float silenceDurationToStop = 0.9f;
    public float minVoiceDurationToAccept = 0.25f;
    public float maxVoiceDuration = 8.0f;
    public int startVoiceFramesRequired = 2;

    [Header("Pre-roll")]
    public float preRollSeconds = 0.5f;

    [Header("Server")]
    public string chatAudioUrl = "http://127.0.0.1:5001/chat-audio-binary";

    [Header("Mic Selection")]
    public string preferredMicNameContains = "Headset";

    [Header("Audio Output")]
    public AudioSource audioSource;

    [Header("Unity Action Targets")]
    public Transform player;

    public Transform congTruoc;
    public Transform congSau;
    public Transform khuA;
    public Transform khuB;
    public Transform baiXe;
    public Transform khuCongNgheCao;
    public Transform vienAidti;
    public Transform vanPhongKhoaFira;
    public Transform dslab;
    public Transform smartlab;
    public Transform fablab;
    public Transform vuonThongMinh;
    public Transform thuVien;
    public Transform hoiTruong;
    public Transform phongHopAi;

    [Header("Debug")]
    public bool showVolumeLog = false;
    public bool showDetailedStateLog = false;
    public bool saveDebugWav = true;
    public bool saveReceivedMp3 = true;

    private const string ForcedChatUrl = "http://127.0.0.1:5001/chat-audio-binary";

    private string micDevice;
    private AudioClip micClip;

    private int actualSampleRate = 16000;
    private int micChannels = 1;

    private int lastSamplePosition = 0;
    private bool isListening = false;
    private bool isRecordingVoice = false;
    private bool isSending = false;

    private float silenceTimer = 0f;
    private float voiceDuration = 0f;
    private int loudFrameCount = 0;

    private readonly List<float> recordedSamplesMono = new List<float>();
    private readonly Queue<float> preRollBufferMono = new Queue<float>();
    private int preRollMaxSamples = 0;

    void Awake()
    {
        chatAudioUrl = ForcedChatUrl;
        Debug.Log("[CHAT-UNITY] Forced URL: " + chatAudioUrl);
    }

    void Start()
    {
        StartCoroutine(StartMicListening());
    }

    void OnDisable()
    {
        StopMicListening();
        ResetCaptureState();
    }

    void OnApplicationQuit()
    {
        StopMicListening();
        ResetCaptureState();
    }

    IEnumerator StartMicListening()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("[CHAT-UNITY] No microphone found.");
            yield break;
        }

        Debug.Log("[CHAT-UNITY] === AVAILABLE MICROPHONES ===");

        for (int i = 0; i < Microphone.devices.Length; i++)
        {
            Debug.Log($"[CHAT-UNITY] Mic {i}: {Microphone.devices[i]}");
        }

        micDevice = SelectPreferredMic();
        Debug.Log("[CHAT-UNITY] Using mic: " + micDevice);

        int minFreq;
        int maxFreq;
        Microphone.GetDeviceCaps(micDevice, out minFreq, out maxFreq);

        if (minFreq == 0 && maxFreq == 0)
        {
            actualSampleRate = requestedSampleRate;
        }
        else
        {
            if (requestedSampleRate < minFreq)
            {
                actualSampleRate = minFreq;
            }
            else if (requestedSampleRate > maxFreq)
            {
                actualSampleRate = maxFreq;
            }
            else
            {
                actualSampleRate = requestedSampleRate;
            }
        }

        micClip = Microphone.Start(micDevice, true, micBufferSeconds, actualSampleRate);

        if (micClip == null)
        {
            Debug.LogError("[CHAT-UNITY] Failed to start microphone.");
            yield break;
        }

        float waitStart = Time.time;

        while (Microphone.GetPosition(micDevice) <= 0)
        {
            if (Time.time - waitStart > 5f)
            {
                Debug.LogError("[CHAT-UNITY] Microphone warm-up timeout.");
                yield break;
            }

            yield return null;
        }

        micChannels = Mathf.Max(1, micClip.channels);
        preRollMaxSamples = Mathf.Max(1, Mathf.RoundToInt(preRollSeconds * actualSampleRate));

        lastSamplePosition = Microphone.GetPosition(micDevice);
        isListening = true;

        Debug.Log($"[CHAT-UNITY] Microphone listening started. channels={micChannels}, frequency={actualSampleRate}");
    }

    string SelectPreferredMic()
    {
        if (!string.IsNullOrWhiteSpace(preferredMicNameContains))
        {
            for (int i = 0; i < Microphone.devices.Length; i++)
            {
                string name = Microphone.devices[i];

                if (name.IndexOf(preferredMicNameContains, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return name;
                }
            }
        }

        return Microphone.devices[0];
    }

    void StopMicListening()
    {
        if (!string.IsNullOrEmpty(micDevice) && Microphone.IsRecording(micDevice))
        {
            Microphone.End(micDevice);
            Debug.Log("[CHAT-UNITY] Microphone stopped.");
        }

        isListening = false;
    }

    void Update()
    {
        if (!isListening || isSending || micClip == null)
            return;

        if (!Microphone.IsRecording(micDevice))
            return;

        int currentPosition = Microphone.GetPosition(micDevice);

        if (currentPosition < 0 || currentPosition == lastSamplePosition)
            return;

        float[] interleavedSamples = ReadMicrophoneSamples(lastSamplePosition, currentPosition);
        lastSamplePosition = currentPosition;

        if (interleavedSamples == null || interleavedSamples.Length == 0)
            return;

        float[] monoSamples = ConvertInterleavedToMono(interleavedSamples, micChannels);

        if (monoSamples == null || monoSamples.Length == 0)
            return;

        float volume = GetRmsVolume(monoSamples);
        float chunkDuration = (float)monoSamples.Length / actualSampleRate;

        if (showVolumeLog)
            Debug.Log($"[CHAT-UNITY] Volume={volume:F6}");

        AppendToPreRoll(monoSamples);

        if (!isRecordingVoice)
        {
            if (volume >= voiceThreshold)
            {
                loudFrameCount++;

                if (loudFrameCount >= startVoiceFramesRequired)
                {
                    isRecordingVoice = true;
                    silenceTimer = 0f;
                    voiceDuration = 0f;
                    recordedSamplesMono.Clear();

                    recordedSamplesMono.AddRange(preRollBufferMono);
                    recordedSamplesMono.AddRange(monoSamples);
                    voiceDuration += chunkDuration;

                    Debug.Log("[CHAT-UNITY] Voice detected.");
                }
            }
            else
            {
                loudFrameCount = 0;
            }

            return;
        }

        recordedSamplesMono.AddRange(monoSamples);
        voiceDuration += chunkDuration;

        if (volume >= voiceThreshold)
        {
            silenceTimer = 0f;
        }
        else
        {
            silenceTimer += chunkDuration;
        }

        bool shouldStop =
            silenceTimer >= silenceDurationToStop ||
            voiceDuration >= maxVoiceDuration;

        if (shouldStop)
        {
            isRecordingVoice = false;
            loudFrameCount = 0;

            if (voiceDuration >= minVoiceDurationToAccept)
            {
                StartCoroutine(ProcessCapturedVoice());
            }
            else
            {
                ResetCaptureState();
            }
        }
    }

    float[] ReadMicrophoneSamples(int fromPosition, int toPosition)
    {
        if (micClip == null)
            return null;

        int clipSamplesPerChannel = micClip.samples;
        int totalChannels = Mathf.Max(1, micClip.channels);

        if (toPosition > fromPosition)
        {
            int lengthPerChannel = toPosition - fromPosition;
            float[] samples = new float[lengthPerChannel * totalChannels];
            micClip.GetData(samples, fromPosition);
            return samples;
        }
        else
        {
            int tailLengthPerChannel = clipSamplesPerChannel - fromPosition;
            int headLengthPerChannel = toPosition;

            float[] tail = new float[tailLengthPerChannel * totalChannels];
            float[] head = new float[headLengthPerChannel * totalChannels];

            if (tailLengthPerChannel > 0)
                micClip.GetData(tail, fromPosition);

            if (headLengthPerChannel > 0)
                micClip.GetData(head, 0);

            float[] combined = new float[tail.Length + head.Length];

            Array.Copy(tail, 0, combined, 0, tail.Length);
            Array.Copy(head, 0, combined, tail.Length, head.Length);

            return combined;
        }
    }

    float[] ConvertInterleavedToMono(float[] interleavedSamples, int channels)
    {
        if (interleavedSamples == null || interleavedSamples.Length == 0)
            return null;

        channels = Mathf.Max(1, channels);

        if (channels == 1)
            return interleavedSamples;

        int frames = interleavedSamples.Length / channels;

        if (frames <= 0)
            return null;

        float[] mono = new float[frames];

        int index = 0;

        for (int i = 0; i < frames; i++)
        {
            float sum = 0f;

            for (int ch = 0; ch < channels; ch++)
            {
                sum += interleavedSamples[index++];
            }

            mono[i] = sum / channels;
        }

        return mono;
    }

    void AppendToPreRoll(float[] samplesMono)
    {
        if (samplesMono == null || samplesMono.Length == 0)
            return;

        for (int i = 0; i < samplesMono.Length; i++)
        {
            preRollBufferMono.Enqueue(samplesMono[i]);
        }

        while (preRollBufferMono.Count > preRollMaxSamples)
        {
            preRollBufferMono.Dequeue();
        }
    }

    IEnumerator ProcessCapturedVoice()
    {
        isSending = true;

        if (PythonServerManager.Instance == null || !PythonServerManager.Instance.IsServerReady)
        {
            Debug.LogError("[CHAT-UNITY] Python server is not ready.");
            ResetCaptureState();
            yield break;
        }

        float[] finalSamplesMono = recordedSamplesMono.ToArray();
        byte[] wavBytes = SamplesToWav(finalSamplesMono, 1, actualSampleRate);

        if (wavBytes == null || wavBytes.Length == 0)
        {
            Debug.LogError("[CHAT-UNITY] Failed to create WAV bytes.");
            ResetCaptureState();
            yield break;
        }

        if (saveDebugWav)
        {
            string debugPath = Path.Combine(Application.persistentDataPath, "last_sent.wav");
            File.WriteAllBytes(debugPath, wavBytes);
            Debug.Log("[CHAT-UNITY] Saved debug WAV: " + debugPath);
        }

        yield return StartCoroutine(SendToChatAudioBinary(wavBytes));

        ResetCaptureState();
    }

    IEnumerator SendToChatAudioBinary(byte[] wavBytes)
    {
        using (UnityWebRequest req = new UnityWebRequest(chatAudioUrl, UnityWebRequest.kHttpVerbPOST))
        {
            req.uploadHandler = new UploadHandlerRaw(wavBytes);
            req.downloadHandler = new DownloadHandlerBuffer();

            req.disposeUploadHandlerOnDispose = true;
            req.disposeDownloadHandlerOnDispose = true;

            req.SetRequestHeader("Content-Type", "audio/wav");
            req.SetRequestHeader("Accept", "audio/mpeg");

            req.timeout = 120;

            yield return req.SendWebRequest();

            Debug.Log("[CHAT-UNITY] Request result: " + req.result);
            Debug.Log("[CHAT-UNITY] HTTP status: " + req.responseCode);

            if (req.responseCode == 204)
            {
                string ignoredText = DecodeUrl(req.GetResponseHeader("X-User-Text"));

                Debug.Log("[CHAT-UNITY] Wake word not detected. Ignored.");
                Debug.Log("[CHAT-UNITY] Ignored STT text: " + ignoredText);

                yield break;
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                string responseText = "";

                if (req.downloadHandler != null)
                {
                    responseText = req.downloadHandler.text;
                }

                Debug.LogError("[CHAT-UNITY] Request failed: " + req.error);
                Debug.LogError("[CHAT-UNITY] Response text: " + responseText);

                yield break;
            }

            string userText = DecodeUrl(req.GetResponseHeader("X-User-Text"));
            string commandText = DecodeUrl(req.GetResponseHeader("X-Command-Text"));
            string assistantText = DecodeUrl(req.GetResponseHeader("X-Assistant-Text"));

            string action = req.GetResponseHeader("X-Action");
            string targetLocation = req.GetResponseHeader("X-Target-Location");
            string cameraId = req.GetResponseHeader("X-Camera-Id");
            string confidence = req.GetResponseHeader("X-Confidence");

            Debug.Log("[CHAT-UNITY] User text: " + userText);
            Debug.Log("[CHAT-UNITY] Command text: " + commandText);
            Debug.Log("[CHAT-UNITY] Assistant text: " + assistantText);
            Debug.Log("[CHAT-UNITY] Action: " + action);
            Debug.Log("[CHAT-UNITY] Target location: " + targetLocation);
            Debug.Log("[CHAT-UNITY] Camera ID: " + cameraId);
            Debug.Log("[CHAT-UNITY] Confidence: " + confidence);

            byte[] mp3Bytes = req.downloadHandler.data;

            if (mp3Bytes == null || mp3Bytes.Length == 0)
            {
                Debug.LogError("[CHAT-UNITY] Received empty MP3 binary.");
                yield break;
            }

            if (saveReceivedMp3)
            {
                string mp3Path = Path.Combine(Application.persistentDataPath, "assistant_reply.mp3");
                File.WriteAllBytes(mp3Path, mp3Bytes);
                Debug.Log("[CHAT-UNITY] Saved assistant MP3: " + mp3Path);
            }

            yield return StartCoroutine(PlayMp3Bytes(mp3Bytes));

            HandleUnityAction(action, targetLocation, cameraId);
        }
    }

    IEnumerator PlayMp3Bytes(byte[] mp3Bytes)
    {
        string mp3Path = Path.Combine(Application.persistentDataPath, "assistant_reply_runtime.mp3");
        File.WriteAllBytes(mp3Path, mp3Bytes);

        string url = "file://" + mp3Path;

        using (UnityWebRequest req = UnityWebRequestMultimedia.GetAudioClip(url, AudioType.MPEG))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[CHAT-UNITY] Audio load failed: " + req.error);
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(req);

            if (clip == null)
            {
                Debug.LogError("[CHAT-UNITY] AudioClip is null.");
                yield break;
            }

            if (audioSource == null)
            {
                Debug.LogError("[CHAT-UNITY] AudioSource is missing.");
                yield break;
            }

            audioSource.Stop();
            audioSource.clip = clip;
            audioSource.Play();

            Debug.Log("[CHAT-UNITY] Playing assistant audio...");
        }
    }

    void HandleUnityAction(string action, string targetLocation, string cameraId)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            Debug.Log("[CHAT-UNITY] Empty action. Skip Unity action.");
            return;
        }

        switch (action)
        {
            case "answer_only":
                Debug.Log("[CHAT-UNITY] answer_only: no scene action.");
                break;

            case "suggest_locations":
                ShowLocationSuggestions();
                break;

            case "describe_location":
                HighlightLocation(targetLocation);
                break;

            case "teleport":
                TeleportTo(targetLocation);
                break;

            case "guide_route":
                StartRouteGuide(targetLocation);
                break;

            case "open_camera":
                OpenCameraUI(cameraId, targetLocation);
                break;

            case "iot_control":
                Debug.Log("[CHAT-UNITY] iot_control received. Add your IoT control logic here.");
                break;

            case "clarify":
                Debug.Log("[CHAT-UNITY] clarify: assistant asked user to repeat or clarify.");
                break;

            case "ignore":
                Debug.Log("[CHAT-UNITY] ignore action.");
                break;

            default:
                Debug.LogWarning("[CHAT-UNITY] Unknown action: " + action);
                break;
        }
    }

    void TeleportTo(string locationId)
    {
        if (player == null)
        {
            Debug.LogError("[CHAT-UNITY] Player Transform is missing.");
            return;
        }

        Transform target = GetLocationTransform(locationId);

        if (target == null)
        {
            Debug.LogWarning("[CHAT-UNITY] Unknown target location: " + locationId);
            return;
        }

        CharacterController controller = player.GetComponent<CharacterController>();

        if (controller != null)
            controller.enabled = false;

        player.position = target.position;
        player.rotation = target.rotation;

        if (controller != null)
            controller.enabled = true;

        Debug.Log("[CHAT-UNITY] Teleported to: " + locationId);
    }

    Transform GetLocationTransform(string locationId)
    {
        switch (locationId)
        {
            case "cong_truoc":
                return congTruoc;

            case "cong_sau":
                return congSau;

            case "khu_a":
                return khuA;

            case "khu_b":
                return khuB;

            case "bai_xe":
                return baiXe;

            case "khu_cong_nghe_cao":
                return khuCongNgheCao;

            case "vien_aidti":
                return vienAidti;

            case "van_phong_khoa_fira":
                return vanPhongKhoaFira;

            case "dslab":
                return dslab;

            case "smartlab":
                return smartlab;

            case "fablab":
                return fablab;

            case "vuon_thong_minh":
                return vuonThongMinh;

            case "thu_vien":
                return thuVien;

            case "hoi_truong":
                return hoiTruong;

            case "phong_hop_ai":
                return phongHopAi;

            default:
                return null;
        }
    }

    void ShowLocationSuggestions()
    {
        Debug.Log("[CHAT-UNITY] Suggested locations:");
        Debug.Log("- Cổng trước");
        Debug.Log("- Cổng sau");
        Debug.Log("- Khu A");
        Debug.Log("- Khu B");
        Debug.Log("- Bãi xe");
        Debug.Log("- Khu công nghệ cao");
        Debug.Log("- Viện AIDTI");
        Debug.Log("- Văn phòng Khoa FIRA");
        Debug.Log("- DSLAB");
        Debug.Log("- SMARTLAB");
        Debug.Log("- FABLAB");
        Debug.Log("- Vườn thông minh");
        Debug.Log("- Thư viện");
        Debug.Log("- Hội trường");
        Debug.Log("- Phòng họp AI");

        // Nếu có UI Canvas danh sách địa điểm, gọi mở UI tại đây.
        // Ví dụ:
        // locationSuggestionPanel.SetActive(true);
    }

    void HighlightLocation(string locationId)
    {
        Debug.Log("[CHAT-UNITY] Highlight location: " + locationId);

        // Nếu có marker, outline, icon hoặc hiệu ứng sáng thì xử lý tại đây.
    }

    void StartRouteGuide(string locationId)
    {
        Debug.Log("[CHAT-UNITY] Start route guide to: " + locationId);

        // Nếu có NavMeshAgent, LineRenderer hoặc hệ thống chỉ đường thì gọi tại đây.
    }

    void OpenCameraUI(string cameraId, string targetLocation)
    {
        Debug.Log("[CHAT-UNITY] Open camera UI. cameraId=" + cameraId + ", targetLocation=" + targetLocation);

        // Gắn với hệ thống camera RTSP/WebRTC/HLS của bạn tại đây.
        // Ví dụ:
        // cameraPanel.SetActive(true);
        // cameraController.OpenCamera(cameraId);
    }

    void ResetCaptureState()
    {
        recordedSamplesMono.Clear();
        silenceTimer = 0f;
        voiceDuration = 0f;
        loudFrameCount = 0;
        isSending = false;
    }

    float GetRmsVolume(float[] samples)
    {
        if (samples == null || samples.Length == 0)
            return 0f;

        double sum = 0.0;

        for (int i = 0; i < samples.Length; i++)
        {
            sum += samples[i] * samples[i];
        }

        return Mathf.Sqrt((float)(sum / samples.Length));
    }

    byte[] SamplesToWav(float[] samples, int channels, int sampleRate)
    {
        if (samples == null || samples.Length == 0)
            return null;

        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            short bitsPerSample = 16;
            short blockAlign = (short)(channels * (bitsPerSample / 8));
            int byteRate = sampleRate * blockAlign;

            byte[] pcmData = new byte[samples.Length * 2];
            int pcmIndex = 0;

            for (int i = 0; i < samples.Length; i++)
            {
                float clamped = Mathf.Clamp(samples[i], -1f, 1f);
                short sampleInt = (short)Mathf.RoundToInt(clamped * 32767f);

                pcmData[pcmIndex++] = (byte)(sampleInt & 0xff);
                pcmData[pcmIndex++] = (byte)((sampleInt >> 8) & 0xff);
            }

            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + pcmData.Length);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write(blockAlign);
            writer.Write(bitsPerSample);

            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(pcmData.Length);
            writer.Write(pcmData);

            writer.Flush();
            return stream.ToArray();
        }
    }

    string DecodeUrl(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        return Uri.UnescapeDataString(value);
    }
}