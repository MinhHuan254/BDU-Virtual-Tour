using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class VADRecorder : MonoBehaviour
{
    public AudioSource audioSource;

    private AudioClip micClip;
    private int sampleRate = 44100;

    private bool isTalking = false;
    private float silenceTimer = 0f;
    private int startSample = 0;

    [Header("VAD Settings")]
    public float threshold = 0.01f;
    public float silenceDuration = 1.2f;

    void Start()
    {
        micClip = Microphone.Start(null, true, 20, sampleRate);
    }

    void Update()
    {
        // ❗ CHẶN LOOP AI
        if (audioSource.isPlaying) return;

        float volume = GetVolume();

        if (volume > threshold)
        {
            silenceTimer = 0f;

            if (!isTalking)
            {
                isTalking = true;
                startSample = Microphone.GetPosition(null);
                Debug.Log("🎤 Start Talking");
            }
        }
        else
        {
            if (isTalking)
            {
                silenceTimer += Time.deltaTime;

                if (silenceTimer > silenceDuration)
                {
                    isTalking = false;

                    int endSample = Microphone.GetPosition(null);

                    Debug.Log("⏹ Stop Talking");

                    ProcessAudio(startSample, endSample);
                }
            }
        }
    }

    float GetVolume()
    {
        int micPos = Microphone.GetPosition(null);
        if (micPos < 128) return 0;

        float[] samples = new float[128];
        micClip.GetData(samples, micPos - 128);

        float sum = 0;
        for (int i = 0; i < samples.Length; i++)
        {
            sum += Mathf.Abs(samples[i]);
        }

        return sum / samples.Length;
    }

    void ProcessAudio(int start, int end)
    {
        if (end <= start) return;

        int length = end - start;

        float[] samples = new float[length];
        micClip.GetData(samples, start);

        byte[] pcmData = FloatToPCM(samples);

        StartCoroutine(SendAudio(pcmData));
    }

    byte[] FloatToPCM(float[] samples)
    {
        byte[] bytes = new byte[samples.Length * 2];

        for (int i = 0; i < samples.Length; i++)
        {
            short value = (short)(samples[i] * 32767);
            BitConverter.GetBytes(value).CopyTo(bytes, i * 2);
        }

        return bytes;
    }

    IEnumerator SendAudio(byte[] pcmData)
    {
        UnityWebRequest www = new UnityWebRequest("http://localhost:5000/audio", "POST");
        www.uploadHandler = new UploadHandlerRaw(pcmData);
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/octet-stream");

        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError("Request failed: " + www.error);
            yield break;
        }

        byte[] audioData = www.downloadHandler.data;
        StartCoroutine(PlayAudio(audioData));
    }

    IEnumerator PlayAudio(byte[] data)
    {
        string path = Application.persistentDataPath + "/res.mp3";
        System.IO.File.WriteAllBytes(path, data);

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + path, AudioType.MPEG))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Audio load failed");
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
            audioSource.clip = clip;
            audioSource.Play();
        }
    }
}