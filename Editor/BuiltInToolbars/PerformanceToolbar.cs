using System.Reflection;
using UnityEditor;
using UnityEngine;

[ToolbarSectionAttribute("Performance")]
public class PerformanceToolbar : IEditorToolbar
{
    private const string FPSDisplayEnabledPrefKey = "PerformanceToolbar.FPSDisplayEnabled";
    
    private bool _fpsDisplayEnabled;
    
    public PerformanceToolbar()
    {
        // Load preference (default to true - Unity's default behavior)
        _fpsDisplayEnabled = EditorPrefs.GetBool(FPSDisplayEnabledPrefKey, true);
        ApplyFPSDisplaySetting();
    }
    
    public bool ShouldShow()
    {
        return true;
    }
    
    public void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("FPS Display:", GUILayout.Width(80));
        
        bool newValue = EditorGUILayout.Toggle(_fpsDisplayEnabled, GUILayout.Width(20));
        if (newValue != _fpsDisplayEnabled)
        {
            _fpsDisplayEnabled = newValue;
            EditorPrefs.SetBool(FPSDisplayEnabledPrefKey, _fpsDisplayEnabled);
            ApplyFPSDisplaySetting();
        }
        
        string labelText = _fpsDisplayEnabled ? "On" : "Off";
        EditorGUILayout.LabelField(labelText, EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
    }
    
    private void ApplyFPSDisplaySetting()
    {
        // Use reflection to access Unity's internal GameView class
        var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        if (gameViewType != null)
        {
            try
            {
                // Get all open GameView windows
                var gameViews = Resources.FindObjectsOfTypeAll(gameViewType);
                
                // Try to find the m_ShowStats field
                var showStatsField = gameViewType.GetField("m_ShowStats", BindingFlags.NonPublic | BindingFlags.Instance);
                
                if (showStatsField != null)
                {
                    // Update all open GameView windows
                    foreach (var gameView in gameViews)
                    {
                        showStatsField.SetValue(gameView, _fpsDisplayEnabled);
                        if (gameView is EditorWindow window)
                        {
                            window.Repaint();
                        }
                    }
                }
                
                // Also try setting via the property if field doesn't work
                if (showStatsField == null)
                {
                    var showStatsProperty = gameViewType.GetProperty("ShowStats", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                    if (showStatsProperty != null)
                    {
                        foreach (var gameView in gameViews)
                        {
                            showStatsProperty.SetValue(gameView, _fpsDisplayEnabled);
                            if (gameView is EditorWindow window)
                            {
                                window.Repaint();
                            }
                        }
                    }
                }
                
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[PerformanceToolbar] Failed to toggle FPS display: {ex.Message}");
            }
        }
        
        // Store preference for future GameView windows
        // Unity's GameView uses this EditorPrefs key to determine initial stats visibility
        EditorPrefs.SetBool("GameView.ShowStats", _fpsDisplayEnabled);
    }
}
