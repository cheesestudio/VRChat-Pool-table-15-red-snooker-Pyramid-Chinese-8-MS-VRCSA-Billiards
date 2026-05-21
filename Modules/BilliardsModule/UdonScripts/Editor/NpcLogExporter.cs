using UnityEngine;
using UnityEditor;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;

[InitializeOnLoad]
public class NpcLogExporter : EditorWindow
{
    private static string logPath = "Assets/npc_log.txt";
    private static int maxLines = 5000;

    static NpcLogExporter()
    {
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            ClearLog();
        }
        else if (state == PlayModeStateChange.ExitingPlayMode)
        {
            ExportLog();
        }
    }

    private static string[] ReadLogFileShared()
    {
        string logFile = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), "Unity", "Editor", "Editor.log");
        if (!File.Exists(logFile))
        {
            logFile = Path.Combine(Application.persistentDataPath, "Player.log");
        }
        if (!File.Exists(logFile))
        {
            Debug.LogError("[NpcLogExporter] Cannot find log file");
            return null;
        }

        var lines = new List<string>();
        using (var fs = new FileStream(logFile, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var reader = new StreamReader(fs))
        {
            string line;
            while ((line = reader.ReadLine()) != null)
                lines.Add(line);
        }
        return lines.ToArray();
    }

    [MenuItem("Tools/NPC Log/Export Console Log")]
    static void ExportLog()
    {
        var lines = ReadLogFileShared();
        if (lines == null) return;

        int count = 0;
        using (var writer = new StreamWriter(logPath, false))
        {
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("[BilliardsModule]") || lines[i].Contains("[NPC]") || lines[i].Contains("[TEST"))
                {
                    // Skip base64 game state lines (huge, ~600 chars each)
                    if (lines[i].Contains("latest game state is v4:")) continue;
                    string clean = Regex.Replace(lines[i], "<.*?>", "");
                    writer.WriteLine(clean);
                    count++;
                }
            }
        }
        AssetDatabase.Refresh();
        Debug.Log("[NpcLogExporter] Exported " + count + " lines (base64 filtered) to " + logPath);
    }

    [MenuItem("Tools/NPC Log/Export NPC Lines Only")]
    static void ExportNpcOnly()
    {
        var lines = ReadLogFileShared();
        if (lines == null) return;

        int count = 0;
        using (var writer = new StreamWriter(logPath, false))
        {
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("[NPC]") || lines[i].Contains("[TEST"))
                {
                    if (lines[i].Contains("latest game state is v4:")) continue;
                    string clean = Regex.Replace(lines[i], "<.*?>", "");
                    writer.WriteLine(clean);
                    count++;
                }
            }
        }
        AssetDatabase.Refresh();
        Debug.Log("[NpcLogExporter] Exported " + count + " NPC lines to " + logPath);
    }

    [MenuItem("Tools/NPC Log/Clear Log File")]
    static void ClearLog()
    {
        if (File.Exists(logPath))
        {
            File.WriteAllText(logPath, "");
            Debug.Log("[NpcLogExporter] Log file cleared");
        }
        else
        {
            Debug.LogWarning("[NpcLogExporter] No log file to clear");
        }
    }

    [MenuItem("Tools/NPC Log/Open Log File")]
    static void OpenLog()
    {
        if (File.Exists(logPath))
            EditorUtility.OpenWithDefaultApp(logPath);
        else
            Debug.LogWarning("[NpcLogExporter] No log file yet. Run Export first.");
    }
}
