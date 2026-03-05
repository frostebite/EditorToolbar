using System.Reflection;
using UnityEditor;
using UnityEngine;

[ToolbarSectionAttribute("Debugger")]
public class DebuggerToolbar : IEditorToolbar
{
    private const string PauseOnErrorPrefKey = "DebuggerToolbar.PauseOnError";
    private const string BreakOnExceptionPrefKey = "DebuggerToolbar.BreakOnException";
    
    private bool _pauseOnError;
    private bool _breakOnException;

    public DebuggerToolbar()
    {
        // Load preferences
        _pauseOnError = EditorPrefs.GetBool(PauseOnErrorPrefKey, false);
        _breakOnException = EditorPrefs.GetBool(BreakOnExceptionPrefKey, false);
        ApplyDebugSettings();
    }

    public bool ShouldShow()
    {
        return true;
    }

    public void OnGUI()
    {
        // Clear Console button - compact
        if (GUILayout.Button(new GUIContent("Clear", "Clear Console"), GUILayout.Width(50)))
        {
            ClearConsole();
        }

        EditorGUILayout.Space(2);

        // Pause on Error toggle - compact horizontal layout
        EditorGUILayout.BeginHorizontal(GUILayout.Width(100));
        bool newPauseOnError = EditorGUILayout.Toggle(_pauseOnError, GUILayout.Width(15));
        if (newPauseOnError != _pauseOnError)
        {
            _pauseOnError = newPauseOnError;
            EditorPrefs.SetBool(PauseOnErrorPrefKey, _pauseOnError);
            ApplyDebugSettings();
        }
        EditorGUILayout.LabelField("Pause", EditorStyles.miniLabel, GUILayout.Width(35));
        EditorGUILayout.EndHorizontal();

        // Break on Exception toggle - compact horizontal layout
        EditorGUILayout.BeginHorizontal(GUILayout.Width(100));
        bool newBreakOnException = EditorGUILayout.Toggle(_breakOnException, GUILayout.Width(15));
        if (newBreakOnException != _breakOnException)
        {
            _breakOnException = newBreakOnException;
            EditorPrefs.SetBool(BreakOnExceptionPrefKey, _breakOnException);
            ApplyDebugSettings();
        }
        EditorGUILayout.LabelField("Break", EditorStyles.miniLabel, GUILayout.Width(35));
        EditorGUILayout.EndHorizontal();
    }

    private void ClearConsole()
    {
        var logEntries = System.Type.GetType("UnityEditor.LogEntries,UnityEditor.dll");
        if (logEntries != null)
        {
            var clearMethod = logEntries.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public);
            if (clearMethod != null)
            {
                clearMethod.Invoke(null, null);
            }
        }
    }

    private void ApplyDebugSettings()
    {
        // Skip window operations in batch mode (no graphics device available)
        if (Application.isBatchMode)
        {
            // Still set preferences even in batch mode, but skip window operations
            try
            {
                EditorPrefs.SetBool("ConsoleWindow.PauseOnError", _pauseOnError);
                EditorPrefs.SetBool("ConsoleWindow.PauseOnException", _breakOnException);
            }
            catch
            {
                // If setting fails, just store our preferences
            }
            return;
        }
        
        // Access Unity's Console window to set pause/break preferences
        var consoleWindowType = typeof(EditorWindow).Assembly.GetType("UnityEditor.ConsoleWindow");
        if (consoleWindowType != null)
        {
            // Use reflection to set the pause/break flags
            var pauseOnErrorField = consoleWindowType.GetField("ms_EntryCount", BindingFlags.NonPublic | BindingFlags.Static);
            
            // Try to set Unity's internal preferences
            // Unity stores these in EditorPrefs with specific keys
            try
            {
                // Unity's Console window uses these preferences internally
                EditorPrefs.SetBool("ConsoleWindow.PauseOnError", _pauseOnError);
                EditorPrefs.SetBool("ConsoleWindow.PauseOnException", _breakOnException);
            }
            catch
            {
                // If setting fails, just store our preferences
                // The Console window will need to be manually configured
            }
            
            // Force Console window to refresh if it's open
            var consoleWindow = EditorWindow.GetWindow(consoleWindowType, false, null, false);
            if (consoleWindow != null)
            {
                consoleWindow.Repaint();
            }
        }
    }
}

