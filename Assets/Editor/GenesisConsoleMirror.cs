#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DuelGenesis.EditorTools
{
    /// <summary>
    /// Mirrors the Unity Console to Logs/DG-Console.log inside the project so tools (and people) can
    /// read build/import results without the Console window. The file is trimmed when it grows large.
    /// </summary>
    [InitializeOnLoad]
    public static class GenesisConsoleMirror
    {
        private const string LogPath = "Logs/DG-Console.log";
        private const long MaxBytes = 4 * 1024 * 1024;
        private static readonly object Gate = new object();

        static GenesisConsoleMirror()
        {
            Application.logMessageReceivedThreaded -= OnLog;
            Application.logMessageReceivedThreaded += OnLog;
            Write($"==== domain reload {DateTime.Now:HH:mm:ss} ====");
        }

        private static void OnLog(string message, string stackTrace, LogType type)
        {
            string line = $"[{DateTime.Now:HH:mm:ss}] {type}: {message}";
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
                line += "\n" + stackTrace;
            Write(line);
        }

        private static void Write(string line)
        {
            try
            {
                lock (Gate)
                {
                    Directory.CreateDirectory("Logs");
                    if (File.Exists(LogPath) && new FileInfo(LogPath).Length > MaxBytes)
                    {
                        string prev = LogPath + ".1";
                        if (File.Exists(prev)) File.Delete(prev);
                        File.Move(LogPath, prev);
                    }
                    File.AppendAllText(LogPath, line + "\n");
                }
            }
            catch
            {
                // Never let logging break the editor.
            }
        }
    }
}
#endif
