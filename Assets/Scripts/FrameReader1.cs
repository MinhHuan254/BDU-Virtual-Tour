using UnityEngine;
using UnityEngine.UI;
using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using Debug = UnityEngine.Debug;

public class FrameReader1 : MonoBehaviour
{
    [Header("Target UI")]
    public RawImage rawImage;

    [Header("FFmpeg")]
    public string ffmpegRelativePath = "FFmpeg/ffmpeg.exe";

    [Header("RTSP Source")]
    [TextArea] public string rtspUrl = "";

    [Header("Video Settings")]
    public int width = 1280;
    public int height = 720;
    public int fps = 5;

    [Header("Debug")]
    public bool showDebugLog = true;

    private Texture2D texture;
    private Process ffmpegProcess;
    private Thread readThread;
    private volatile bool isRunning = false;

    private byte[] latestFrame;
    private bool hasNewFrame = false;
    private readonly object frameLock = new object();
    private int frameSize;

    private void Awake()
    {
        if (rawImage == null)
            rawImage = GetComponent<RawImage>();

        if (rawImage == null)
        {
            Debug.LogError("[FrameReader1] Chua gan RawImage.");
            enabled = false;
            return;
        }

        rawImage.color = Color.white;
        rawImage.uvRect = new Rect(0f, 0f, 1f, 1f);
    }

    private void OnEnable()
    {
        if (!string.IsNullOrWhiteSpace(rtspUrl))
            RestartStream();
    }

    private void Update()
    {
        if (!hasNewFrame)
            return;

        lock (frameLock)
        {
            if (latestFrame != null && texture != null)
            {
                texture.LoadRawTextureData(latestFrame);
                texture.Apply(false);
                hasNewFrame = false;
            }
        }
    }

    public void SetSource(string newRtspUrl, int newWidth = 1280, int newHeight = 720, int newFps = 5)
    {
        rtspUrl = newRtspUrl;
        width = newWidth;
        height = newHeight;
        fps = newFps;

        RestartStream();
    }

    public void RestartStream()
    {
        StopStream();

        if (string.IsNullOrWhiteSpace(rtspUrl))
        {
            Debug.LogWarning("[FrameReader1] rtspUrl dang rong.");
            return;
        }

        string ffmpegPath = Path.Combine(Application.streamingAssetsPath, ffmpegRelativePath);
        if (!File.Exists(ffmpegPath))
        {
            Debug.LogError("[FrameReader1] Khong tim thay ffmpeg.exe tai: " + ffmpegPath);
            return;
        }

        frameSize = width * height * 3; // RGB24
        texture = new Texture2D(width, height, TextureFormat.RGB24, false);
        rawImage.texture = texture;

        string args =
            $"-hide_banner " +
            $"-loglevel warning " +
            $"-nostdin " +
            $"-fflags nobuffer " +
            $"-flags low_delay " +
            $"-rtsp_transport tcp " +
            $"-i \"{rtspUrl}\" " +
            $"-vf \"fps={fps},scale={width}:{height},vflip\" " +
            $"-an -sn -dn " +
            $"-pix_fmt rgb24 " +
            $"-vcodec rawvideo " +
            $"-f rawvideo pipe:1";

        ffmpegProcess = new Process();
        ffmpegProcess.StartInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(ffmpegPath)
        };

        ffmpegProcess.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                Debug.LogWarning("[FrameReader1][ffmpeg] " + e.Data);
        };

        try
        {
            ffmpegProcess.Start();
            ffmpegProcess.BeginErrorReadLine();

            isRunning = true;
            readThread = new Thread(ReadLoop);
            readThread.IsBackground = true;
            readThread.Start();

            if (showDebugLog)
            {
                Debug.Log("[FrameReader1] Start stream: " + rtspUrl);
                Debug.Log("[FrameReader1] Size: " + width + "x" + height + " @ " + fps + "fps");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[FrameReader1] Khong start duoc ffmpeg: " + ex.Message);
        }
    }

    private void ReadLoop()
    {
        try
        {
            Stream stream = ffmpegProcess.StandardOutput.BaseStream;
            byte[] buffer = new byte[frameSize];

            while (isRunning)
            {
                int totalRead = 0;

                while (totalRead < frameSize)
                {
                    int read = stream.Read(buffer, totalRead, frameSize - totalRead);
                    if (read <= 0)
                    {
                        isRunning = false;
                        return;
                    }

                    totalRead += read;
                }

                lock (frameLock)
                {
                    if (latestFrame == null || latestFrame.Length != frameSize)
                        latestFrame = new byte[frameSize];

                    Buffer.BlockCopy(buffer, 0, latestFrame, 0, frameSize);
                    hasNewFrame = true;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[FrameReader1] ReadLoop loi: " + ex.Message);
        }
    }

    public void StopStream()
    {
        isRunning = false;

        try
        {
            if (readThread != null && readThread.IsAlive)
                readThread.Join(300);
        }
        catch { }

        try
        {
            if (ffmpegProcess != null && !ffmpegProcess.HasExited)
            {
                ffmpegProcess.Kill();
                ffmpegProcess.WaitForExit(500);
            }
        }
        catch { }

        try { ffmpegProcess?.Dispose(); } catch { }

        ffmpegProcess = null;
        readThread = null;
    }

    private void OnDisable()
    {
        StopStream();
    }

    private void OnDestroy()
    {
        StopStream();
    }

    private void OnApplicationQuit()
    {
        StopStream();
    }
}