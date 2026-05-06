using UnityEngine;

public class AIOutput : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip clip1;
    public AudioClip clip2;
    public AudioClip clip3;

    public void Speak(string message)
    {
        Debug.Log("AI: " + message);

        if (message.Contains("gợi ý"))
        {
            PlayClip(clip1);
        }
        else if (message.Contains("thư viện"))
        {
            PlayClip(clip2);
        }
        else
        {
            PlayClip(clip3);
        }
    }

    void PlayClip(AudioClip clip)
    {
        if (clip == null) return;

        audioSource.clip = clip;
        audioSource.Play();
    }
}