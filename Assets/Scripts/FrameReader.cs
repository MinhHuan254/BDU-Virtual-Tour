using UnityEngine;
using UnityEngine.UI;
using System.IO;
using System;

public class FrameReader : MonoBehaviour
{
    [Header("Target UI")]
    public RawImage rawImage;

    [Header("Image Source")]
    public string folderPath = "";
    public string fileName = "frame_parking.jpg";

    [Header("Refresh")]
    public float refreshInterval = 0.2f;

    private Texture2D texture;
    private float timer = 0f;
    private DateTime lastWriteTime = DateTime.MinValue;
    private bool loggedMissingFile = false;
    private bool loggedFirstFrame = false;

    private void Start()
    {
        if (rawImage == null)
            rawImage = GetComponent<RawImage>();

        folderPath = CleanText(folderPath);
        fileName = CleanText(fileName);

        if (string.IsNullOrWhiteSpace(folderPath))
        {
            folderPath = Path.GetFullPath(
                Path.Combine(Application.dataPath, "..", "CameraFramesRuntime")
            );
        }

        texture = new Texture2D(2, 2, TextureFormat.RGB24, false);

        if (rawImage != null)
            rawImage.texture = texture;
        else
            Debug.LogError("[FrameReader] rawImage chua duoc gan.");

        Debug.Log("[FrameReader] Folder Path = " + folderPath);
        Debug.Log("[FrameReader] File Name   = " + fileName);
        Debug.Log("[FrameReader] Full Path   = " + Path.Combine(folderPath, fileName));
    }

    private void Update()
    {
        timer += Time.deltaTime;

        if (timer >= refreshInterval)
        {
            timer = 0f;
            LoadFrame();
        }
    }

    private void LoadFrame()
    {
        if (string.IsNullOrWhiteSpace(folderPath) || string.IsNullOrWhiteSpace(fileName))
            return;

        string fullPath = Path.Combine(folderPath, fileName);

        if (!File.Exists(fullPath))
        {
            if (!loggedMissingFile)
            {
                Debug.LogWarning("[FrameReader] Chua tim thay file: " + fullPath);
                loggedMissingFile = true;
            }
            return;
        }

        loggedMissingFile = false;

        DateTime writeTime = File.GetLastWriteTimeUtc(fullPath);
        if (writeTime == lastWriteTime)
            return;

        try
        {
            byte[] data;

            using (FileStream fs = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (MemoryStream ms = new MemoryStream())
            {
                fs.CopyTo(ms);
                data = ms.ToArray();
            }

            if (data == null || data.Length == 0)
                return;

            bool ok = texture.LoadImage(data);
            if (!ok)
            {
                Debug.LogWarning("[FrameReader] LoadImage that bai: " + fullPath);
                return;
            }

            rawImage.texture = texture;
            lastWriteTime = writeTime;

            if (!loggedFirstFrame)
            {
                loggedFirstFrame = true;
                Debug.Log("[FrameReader] Da load frame dau tien: " + fullPath);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[FrameReader] Khong doc duoc file: " + fullPath + "\n" + e.Message);
        }
    }

    private string CleanText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value.Trim().Trim('"');
    }
}