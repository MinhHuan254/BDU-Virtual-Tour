using UnityEngine;
using UnityEngine.Video;

public class AutoStreamPlayer : MonoBehaviour
{
    public VideoPlayer vp;

    void Start()
    {
        vp.playOnAwake = false;
        vp.prepareCompleted += OnPrepared;
        vp.errorReceived += OnError;
        vp.Prepare();
    }

    void OnPrepared(VideoPlayer source)
    {
        vp.Play();
        Debug.Log("Stream started");
    }

    void OnError(VideoPlayer source, string message)
    {
        Debug.LogError("Video Error: " + message);
    }
}