using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

public class TTSPlayer : MonoBehaviour
{
    public string ttsApiUrl = "http://127.0.0.1:5002/tts-from-files";
    public string latestAudioUrl = "http://127.0.0.1:5002/audio/latest";
    public AudioSource audioSource;

    public string textFilePath;
    public string speakerWavPath;

    public void StartTTS()
    {
        StartCoroutine(UploadFilesAndPlay());
    }

    IEnumerator UploadFilesAndPlay()
    {
        if (!File.Exists(textFilePath))
        {
            Debug.LogError("Text file not found: " + textFilePath);
            yield break;
        }

        if (!File.Exists(speakerWavPath))
        {
            Debug.LogError("Speaker wav not found: " + speakerWavPath);
            yield break;
        }

        byte[] textBytes = File.ReadAllBytes(textFilePath);
        byte[] wavBytes = File.ReadAllBytes(speakerWavPath);

        WWWForm form = new WWWForm();
        form.AddBinaryData("text_file", textBytes, Path.GetFileName(textFilePath), "text/plain");
        form.AddBinaryData("speaker_wav", wavBytes, Path.GetFileName(speakerWavPath), "audio/wav");

        using (UnityWebRequest req = UnityWebRequest.Post(ttsApiUrl, form))
        {
            yield return req.SendWebRequest();

            Debug.Log("TTS upload result: " + req.result);
            Debug.Log("TTS upload error: " + req.error);
            Debug.Log("TTS response: " + req.downloadHandler.text);

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("TTS request failed: " + req.error);
                yield break;
            }
        }

        using (UnityWebRequest audioReq = UnityWebRequestMultimedia.GetAudioClip(latestAudioUrl, AudioType.WAV))
        {
            yield return audioReq.SendWebRequest();

            if (audioReq.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("Audio download failed: " + audioReq.error);
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(audioReq);
            audioSource.clip = clip;
            audioSource.Play();
        }
    }
}