using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text;
using System.Reflection;

public class ConsoleCopyTool : EditorWindow
{
    [MenuItem("Tools/Console Copy Tool")]
    public static void ShowWindow()
    {
        GetWindow<ConsoleCopyTool>("Console Copy Tool");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Console Copy Tool", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        if (GUILayout.Button("Copy All Errors"))
        {
            CopyConsoleErrors();
        }

        if (GUILayout.Button("Copy All Warnings"))
        {
            CopyConsoleWarnings();
        }

        if (GUILayout.Button("Copy All Messages"))
        {
            CopyAllConsoleMessages();
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox("Click buttons above to copy console messages to clipboard.", MessageType.Info);
    }

    private void CopyConsoleErrors()
    {
        var messages = GetConsoleMessages();
        var errors = new StringBuilder();

        foreach (var message in messages)
        {
            if (message.type == LogType.Error || message.type == LogType.Exception)
            {
                errors.AppendLine($"[{message.type}] {message.message}");
                if (!string.IsNullOrEmpty(message.stackTrace))
                {
                    errors.AppendLine(message.stackTrace);
                }
                errors.AppendLine();
            }
        }

        if (errors.Length > 0)
        {
            EditorGUIUtility.systemCopyBuffer = errors.ToString();
            Debug.Log($"Copied {GetErrorCount(messages)} error(s) to clipboard");
        }
        else
        {
            Debug.Log("No errors found in console");
        }
    }

    private void CopyConsoleWarnings()
    {
        var messages = GetConsoleMessages();
        var warnings = new StringBuilder();

        foreach (var message in messages)
        {
            if (message.type == LogType.Warning)
            {
                warnings.AppendLine($"[{message.type}] {message.message}");
                if (!string.IsNullOrEmpty(message.stackTrace))
                {
                    warnings.AppendLine(message.stackTrace);
                }
                warnings.AppendLine();
            }
        }

        if (warnings.Length > 0)
        {
            EditorGUIUtility.systemCopyBuffer = warnings.ToString();
            Debug.Log($"Copied {GetWarningCount(messages)} warning(s) to clipboard");
        }
        else
        {
            Debug.Log("No warnings found in console");
        }
    }

    private void CopyAllConsoleMessages()
    {
        var messages = GetConsoleMessages();
        var allMessages = new StringBuilder();

        foreach (var message in messages)
        {
            allMessages.AppendLine($"[{message.type}] {message.message}");
            if (!string.IsNullOrEmpty(message.stackTrace))
            {
                allMessages.AppendLine(message.stackTrace);
            }
            allMessages.AppendLine();
        }

        if (allMessages.Length > 0)
        {
            EditorGUIUtility.systemCopyBuffer = allMessages.ToString();
            Debug.Log($"Copied {messages.Count} message(s) to clipboard");
        }
        else
        {
            Debug.Log("No messages found in console");
        }
    }

    private List<ConsoleMessage> GetConsoleMessages()
    {
        var messages = new List<ConsoleMessage>();

        try
        {
            var logEntriesType = System.Type.GetType("UnityEditor.LogEntries, UnityEditor");
            if (logEntriesType != null)
            {
                var getCountMethod = logEntriesType.GetMethod("GetCount", BindingFlags.Static | BindingFlags.Public);
                var getEntryInternalMethod = logEntriesType.GetMethod("GetEntryInternal", BindingFlags.Static | BindingFlags.Public);

                if (getCountMethod != null && getEntryInternalMethod != null)
                {
                    int count = (int)getCountMethod.Invoke(null, null);

                    for (int i = 0; i < count; i++)
                    {
                        var logEntry = System.Activator.CreateInstance(System.Type.GetType("UnityEditor.LogEntry, UnityEditor"));
                        var args = new object[] { i, logEntry };
                        getEntryInternalMethod.Invoke(null, args);

                        var messageField = logEntry.GetType().GetField("message");
                        var fileField = logEntry.GetType().GetField("file");
                        var lineField = logEntry.GetType().GetField("line");
                        var modeField = logEntry.GetType().GetField("mode");

                        if (messageField != null && modeField != null)
                        {
                            string message = messageField.GetValue(logEntry)?.ToString() ?? "";
                            int mode = (int)(modeField.GetValue(logEntry) ?? 0);

                            LogType logType = LogType.Log;
                            if ((mode & (1 << 0)) != 0) logType = LogType.Error;
                            else if ((mode & (1 << 1)) != 0) logType = LogType.Warning;
                            else if ((mode & (1 << 2)) != 0) logType = LogType.Log;

                            string file = fileField?.GetValue(logEntry)?.ToString() ?? "";
                            int line = (int)(lineField?.GetValue(logEntry) ?? 0);
                            string stackTrace = "";

                            if (!string.IsNullOrEmpty(file) && line > 0)
                            {
                                stackTrace = $"at {file}:{line}";
                            }

                            messages.Add(new ConsoleMessage
                            {
                                message = message,
                                type = logType,
                                stackTrace = stackTrace
                            });
                        }
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to get console messages: {e.Message}");
        }

        return messages;
    }

    private int GetErrorCount(List<ConsoleMessage> messages)
    {
        int count = 0;
        foreach (var message in messages)
        {
            if (message.type == LogType.Error || message.type == LogType.Exception)
                count++;
        }
        return count;
    }

    private int GetWarningCount(List<ConsoleMessage> messages)
    {
        int count = 0;
        foreach (var message in messages)
        {
            if (message.type == LogType.Warning)
                count++;
        }
        return count;
    }

    private struct ConsoleMessage
    {
        public string message;
        public LogType type;
        public string stackTrace;
    }
}