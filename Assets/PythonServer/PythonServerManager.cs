using System;
using System.Diagnostics;
using System.IO;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class PythonServerManager : MonoBehaviour
{
    public static PythonServerManager Instance { get; private set; }

    public bool IsServerReady { get; private set; } = false;

    private Process pythonProcess;

    [Header("Python Config")]
    public string pythonExe = "C:/Users/MSI/AppData/Local/Programs/Python/Python313/python.exe";

    [Header("Server Config")]
    public string serverRelativePath = "PythonServer/server.py";
    public string healthUrl = "http://127.0.0.1:5001/health";
    public float healthCheckTimeout = 30f;
    public float retryInterval = 0.5f;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        StartCoroutine(StartPythonAndWait());
    }

    void OnApplicationQuit()
    {
        StopPython();
    }

    IEnumerator StartPythonAndWait()
    {
        IsServerReady = false;
        StopPython();

        string scriptPath = Path.Combine(Application.dataPath, serverRelativePath);

        UnityEngine.Debug.Log("[STT-UNITY] Python exe: " + pythonExe);
        UnityEngine.Debug.Log("[STT-UNITY] Server script: " + scriptPath);

        if (!File.Exists(scriptPath))
        {
            UnityEngine.Debug.LogError("[STT-UNITY] server.py not found: " + scriptPath);
            yield break;
        }

        if (!File.Exists(pythonExe))
        {
            UnityEngine.Debug.LogError("[STT-UNITY] python.exe not found: " + pythonExe);
            yield break;
        }

        ProcessStartInfo start = new ProcessStartInfo
        {
            FileName = pythonExe,
            Arguments = $"-u \"{scriptPath}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = Path.GetDirectoryName(scriptPath)
        };

        pythonProcess = new Process
        {
            StartInfo = start,
            EnableRaisingEvents = true
        };

        try
        {
            pythonProcess.OutputDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                    UnityEngine.Debug.Log("[PYTHON] " + args.Data);
            };

            pythonProcess.ErrorDataReceived += (sender, args) =>
            {
                if (string.IsNullOrEmpty(args.Data))
                    return;

                string line = args.Data.Trim();

                if (line.StartsWith("WARNING:", StringComparison.OrdinalIgnoreCase) ||
                    line.Contains("This is a development server") ||
                    line.Contains("Do not use it in a production deployment") ||
                    line.Contains("Use a production WSGI server instead"))
                {
                    UnityEngine.Debug.Log("[PYTHON WARNING] " + line);
                    return;
                }

                if (line.Contains("Traceback") ||
                    line.Contains("Exception") ||
                    line.Contains("Error") ||
                    line.Contains("[GLOBAL SERVER ERROR]") ||
                    line.Contains("[STT ERROR]"))
                {
                    UnityEngine.Debug.LogError("[PYTHON ERROR] " + line);
                }
                else
                {
                    UnityEngine.Debug.Log("[PYTHON STDERR] " + line);
                }
            };

            pythonProcess.Exited += (sender, args) =>
            {
                UnityEngine.Debug.LogError("[STT-UNITY] Python server process exited unexpectedly.");
                IsServerReady = false;
            };

            pythonProcess.Start();
            pythonProcess.BeginOutputReadLine();
            pythonProcess.BeginErrorReadLine();

            UnityEngine.Debug.Log("[STT-UNITY] Python server starting...");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError("[STT-UNITY] Failed to start Python: " + e);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < healthCheckTimeout)
        {
            if (pythonProcess == null || pythonProcess.HasExited)
            {
                UnityEngine.Debug.LogError("[STT-UNITY] Python process exited before health check passed.");
                IsServerReady = false;
                yield break;
            }

            using (UnityWebRequest req = UnityWebRequest.Get(healthUrl))
            {
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    IsServerReady = true;
                    UnityEngine.Debug.Log("[STT-UNITY] Python server is ready.");
                    UnityEngine.Debug.Log("[STT-UNITY] Health response: " + req.downloadHandler.text);
                    yield break;
                }
            }

            elapsed += retryInterval;
            yield return new WaitForSeconds(retryInterval);
        }

        UnityEngine.Debug.LogError("[STT-UNITY] Python server health check failed.");
        IsServerReady = false;
    }

    void StopPython()
    {
        IsServerReady = false;

        if (pythonProcess == null)
            return;

        try
        {
            if (!pythonProcess.HasExited)
            {
                pythonProcess.Kill();
                pythonProcess.WaitForExit(2000);
            }

            pythonProcess.Dispose();
            pythonProcess = null;
            UnityEngine.Debug.Log("[STT-UNITY] Python server stopped.");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning("[STT-UNITY] Error stopping Python server: " + e.Message);
        }
    }
}