using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;

[Serializable]
public class FFmpegCameraItem
{
    public string cameraName;
    [TextArea] public string rtspUrl;
    public string outputFileName;

    public int width = 1280;
    public int height = 720;
    public int fps = 5;
    public int jpegQuality = 2;
}

public class FFmpegAutoPlay : MonoBehaviour
{
    [Header("FFmpeg")]
    public string ffmpegRelativePath = "FFmpeg/ffmpeg.exe";

    [Header("Output Folder")]
    public string outputFolderName = "CameraFramesRuntime";

    [Header("Camera List")]
    public List<FFmpegCameraItem> cameras = new List<FFmpegCameraItem>();

    private readonly List<Process> runningProcesses = new List<Process>();

    public static string FramesFolderPath { get; private set; }

    private string ffmpegPath;

    private void Awake()
    {
        ffmpegPath = Path.Combine(Application.streamingAssetsPath, ffmpegRelativePath);

        FramesFolderPath = Path.GetFullPath(
            Path.Combine(Application.dataPath, "..", outputFolderName)
        );

        if (!Directory.Exists(FramesFolderPath))
            Directory.CreateDirectory(FramesFolderPath);

        Debug.Log("[FFmpeg] EXE Path: " + ffmpegPath);
        Debug.Log("[FFmpeg] Frames Folder: " + FramesFolderPath);
    }

    private void Start()
    {
        StartAllFFmpeg();
    }

    private void OnDisable()
    {
        StopAllFFmpeg();
    }

    private void OnDestroy()
    {
        StopAllFFmpeg();
    }

    private void OnApplicationQuit()
    {
        StopAllFFmpeg();
    }

    public void StartAllFFmpeg()
    {
        if (!File.Exists(ffmpegPath))
        {
            Debug.LogError("[FFmpeg] Khong tim thay ffmpeg.exe tai: " + ffmpegPath);
            return;
        }

        StopAllFFmpeg();

        foreach (var cam in cameras)
            StartOneFFmpeg(cam);
    }

    public void StopAllFFmpeg()
    {
        for (int i = 0; i < runningProcesses.Count; i++)
        {
            try
            {
                if (runningProcesses[i] != null && !runningProcesses[i].HasExited)
                {
                    runningProcesses[i].Kill();
                    runningProcesses[i].WaitForExit(1000);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[FFmpeg] Loi khi stop process: " + ex.Message);
            }
            finally
            {
                try { runningProcesses[i]?.Dispose(); } catch { }
            }
        }

        runningProcesses.Clear();
    }

    private void StartOneFFmpeg(FFmpegCameraItem cam)
    {
        if (string.IsNullOrWhiteSpace(cam.rtspUrl))
        {
            Debug.LogWarning("[FFmpeg] rtspUrl rong cho camera: " + cam.cameraName);
            return;
        }

        if (string.IsNullOrWhiteSpace(cam.outputFileName))
        {
            Debug.LogWarning("[FFmpeg] outputFileName rong cho camera: " + cam.cameraName);
            return;
        }

        string outputPath = Path.Combine(FramesFolderPath, cam.outputFileName);

        string args =
            $"-hide_banner " +
            $"-loglevel info " +
            $"-nostdin " +
            $"-y " +
            $"-rtsp_transport tcp " +
            $"-i \"{cam.rtspUrl}\" " +
            $"-vf \"fps={cam.fps},scale={cam.width}:{cam.height}\" " +
            $"-q:v {cam.jpegQuality} " +
            $"-update 1 " +
            $"-f image2 " +
            $"\"{outputPath}\"";

        Process process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            WorkingDirectory = Path.GetDirectoryName(ffmpegPath)
        };

        process.EnableRaisingEvents = true;

        process.OutputDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                Debug.Log($"[FFmpeg:{cam.cameraName}] {e.Data}");
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data))
                Debug.LogWarning($"[FFmpeg:{cam.cameraName}] {e.Data}");
        };

        process.Exited += (sender, e) =>
        {
            Debug.LogError($"[FFmpeg:{cam.cameraName}] Process da thoat.");
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            runningProcesses.Add(process);

            Debug.Log($"[FFmpeg] Started: {cam.cameraName} -> {outputPath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[FFmpeg] Khong start duoc camera {cam.cameraName}: {ex.Message}");
        }
    }
}